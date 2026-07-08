"""Admin helper for the Innola-facing Compute review web map.

The script is terminal-first and safe by default: dry-run/export modes build
the expected Enterprise web map definition without contacting Portal. Live
provisioning updates or creates the web map item once an Innola-facing feature
layer view URL is configured.
"""

from __future__ import annotations

import argparse
import datetime as _dt
import getpass
import json
import os
from pathlib import Path
from typing import Any
from urllib.parse import urlencode
from urllib.request import Request, urlopen


DEFAULT_SCHEMA_VERSION = "sidwell_enterprise_innola_map_v1"
DEFAULT_TOKEN_ENV_VAR = "ARCGIS_PORTAL_TOKEN"
DEFAULT_VIEW_NAME = "working_review_innola_view"
DEFAULT_WEB_MAP_NAME = "innola_compute_review_map"
WORKING_ROLES = ("polygons", "lines", "points", "issues")
REQUIRED_WORKING_ROLES = ("polygons", "lines", "points")
REFERENCE_LAYER_ORDER = (
    "parish",
    "survey_cadastre",
    "legal_cadastre",
    "fiscal_cadastre",
)
WORKING_LAYER_ORDER = ("polygons", "lines", "points", "issues")
LINE_LABEL_EXPRESSION = (
    "var len = IIf(IsEmpty($feature.length_txt), $feature.distance_txt, $feature.length_txt); "
    "When(IsEmpty($feature.bearing_txt) && IsEmpty(len), '', "
    "IsEmpty($feature.bearing_txt), len, "
    "IsEmpty(len), $feature.bearing_txt, "
    "$feature.bearing_txt + TextFormatting.NewLine + len)"
)
POLYGON_LABEL_EXPRESSION = (
    "When(IsEmpty($feature.SUID), $feature.parcel_name, "
    "$feature.parcel_name + TextFormatting.NewLine + $feature.SUID)"
)


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Provision the Innola Compute review Enterprise web map.")
    parser.add_argument("mode", choices=("validate", "provision", "export-config"))
    parser.add_argument("--config", default="")
    parser.add_argument("--portal-url", default="")
    parser.add_argument("--schema-version", default=DEFAULT_SCHEMA_VERSION)
    parser.add_argument("--feature-layer-view-name", default="")
    parser.add_argument("--feature-layer-view-url", default="")
    parser.add_argument("--feature-layer-view-item-id", default="")
    parser.add_argument("--source-feature-layer-item-id", default="")
    parser.add_argument("--web-map-name", default="")
    parser.add_argument("--web-map-item-id", default="")
    parser.add_argument("--sharing-group-id", default="")
    parser.add_argument("--transaction-scope-field", default="")
    parser.add_argument("--completion-filter", default="")
    parser.add_argument("--transaction-filter-template", default="")
    parser.add_argument("--extent-mode", default="")
    parser.add_argument("--extent-padding-percent", type=float, default=None)
    parser.add_argument("--allow-source-service-map", action="store_true", default=False)
    parser.add_argument("--token-env-var", default=DEFAULT_TOKEN_ENV_VAR)
    parser.add_argument("--dry-run", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--output-json", default="")
    return parser.parse_args(argv)


def build_payload(args: argparse.Namespace) -> dict[str, Any]:
    settings = _load_settings(args.config)
    config = _resolve_config(args, settings)
    config["_token_env_var"] = args.token_env_var
    errors: list[str] = []
    warnings: list[str] = []

    _validate_config(config, errors, warnings)

    if args.mode == "provision" and not args.dry_run and not errors:
        token_env_var = args.token_env_var
        if not os.environ.get(token_env_var, ""):
            errors.append(f"Live provisioning requires a portal token in {token_env_var}.")

    outputs = {
        "feature_layer_view_item_id": config["feature_layer_view_item_id"],
        "feature_layer_view_url": config["feature_layer_view_url"],
        "web_map_item_id": config["web_map_item_id"],
        "web_map_url": _web_map_url(config["portal_url"], config["web_map_item_id"]),
    }

    live_result: dict[str, Any] = {}
    if args.mode == "provision" and not args.dry_run and not errors:
        view_result = _ensure_feature_layer_view(config, errors, warnings)
        if view_result.get("feature_layer_view_item_id"):
            config["feature_layer_view_item_id"] = str(view_result["feature_layer_view_item_id"])
            outputs["feature_layer_view_item_id"] = config["feature_layer_view_item_id"]
        if view_result.get("feature_layer_view_url"):
            config["feature_layer_view_url"] = str(view_result["feature_layer_view_url"])
            outputs["feature_layer_view_url"] = config["feature_layer_view_url"]

    web_map = build_web_map_definition(config)

    if args.mode == "provision" and not args.dry_run and not errors:
        live_result = _provision_live_web_map(config, web_map, errors, warnings)
        if view_result:
            live_result["feature_layer_view"] = view_result
        if live_result.get("web_map_item_id"):
            outputs["web_map_item_id"] = live_result["web_map_item_id"]
            outputs["web_map_url"] = _web_map_url(config["portal_url"], live_result["web_map_item_id"])

    status = "failed" if errors else _status_for(args.mode, args.dry_run)
    return {
        "schema_version": args.schema_version,
        "mode": args.mode,
        "status": status,
        "dry_run": args.dry_run,
        "operator": getpass.getuser(),
        "timestamp_utc": _dt.datetime.now(_dt.timezone.utc).isoformat(),
        "target": {
            "portal_url": config["portal_url"],
            "feature_layer_view_name": config["feature_layer_view_name"],
            "web_map_name": config["web_map_name"],
            "sharing_group_id": config["sharing_group_id"],
        },
        "filters": {
            "transaction_scope_field": config["transaction_scope_field"],
            "completion_filter": config["completion_filter"],
            "transaction_filter_template": config["transaction_filter_template"],
            "transaction_url_template": _transaction_url_template(outputs["web_map_url"], config),
        },
        "extent": {
            "mode": config["extent_mode"],
            "padding_percent": config["extent_padding_percent"],
        },
        "layers": {
            "reference_layers": config["reference_layers"],
            "working_layers": _working_layer_urls(config),
            "layer_order": _layer_order_summary(config),
        },
        "outputs": outputs,
        "generated_settings": _generated_settings(config, outputs),
        "web_map_definition": web_map,
        "live_result": live_result,
        "validation": {
            "errors": errors,
            "warnings": warnings,
            "messages": _messages_for(args.mode, args.dry_run, errors),
        },
    }


def build_web_map_definition(config: dict[str, Any]) -> dict[str, Any]:
    operational_layers: list[dict[str, Any]] = []
    for key in REFERENCE_LAYER_ORDER:
        layer = config["reference_layers"].get(key) or {}
        url = str(layer.get("url") or layer.get("item_id") or "").strip()
        if not url:
            continue
        operational_layers.append(_reference_operational_layer(key, layer))

    for role in WORKING_LAYER_ORDER:
        url = _working_layer_url(config, role)
        if not url:
            continue
        operational_layers.append(_working_operational_layer(role, url, config))

    return {
        "operationalLayers": operational_layers,
        "baseMap": _base_map(config),
        "spatialReference": {"wkid": 102100, "latestWkid": 3857},
        "version": "2.30",
        "authoringApp": "Sidwell Parcel Workflow",
        "authoringAppVersion": "7.12",
        "initialState": {
            "viewpoint": {
                "targetGeometry": config["default_extent"],
            }
        },
    }


def write_payload(payload: dict[str, Any], output_json: str) -> None:
    text = json.dumps(payload, indent=2, sort_keys=True)
    if output_json:
        path = Path(output_json)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
    else:
        print(text)


def _load_settings(path: str) -> dict[str, Any]:
    if not path:
        return {}
    settings_path = Path(path)
    if not settings_path.exists():
        raise FileNotFoundError(f"Settings file not found: {settings_path}")
    return json.loads(settings_path.read_text(encoding="utf-8"))


def _resolve_config(args: argparse.Namespace, settings: dict[str, Any]) -> dict[str, Any]:
    map_settings = settings.get("enterprise_innola_map_view") if isinstance(settings, dict) else {}
    map_settings = map_settings if isinstance(map_settings, dict) else {}
    working_settings = settings.get("enterprise_working_review") if isinstance(settings, dict) else {}
    working_settings = working_settings if isinstance(working_settings, dict) else {}
    admin_settings = settings.get("enterprise_working_admin") if isinstance(settings, dict) else {}
    admin_settings = admin_settings if isinstance(admin_settings, dict) else {}

    outputs = map_settings.get("outputs") if isinstance(map_settings.get("outputs"), dict) else {}
    reference_layers = _normalize_reference_layers(map_settings.get("reference_layers"))
    working_layers = working_settings.get("layers") if isinstance(working_settings.get("layers"), dict) else {}

    portal_url = _first_non_empty(args.portal_url, map_settings.get("portal_url"), admin_settings.get("portal_url"))
    transaction_scope_field = _first_non_empty(
        args.transaction_scope_field,
        map_settings.get("transaction_scope_field"),
        working_settings.get("transaction_scope_field"),
        "transaction_number",
    )
    return {
        "portal_url": portal_url.rstrip("/") if portal_url else "",
        "feature_layer_view_name": _first_non_empty(
            args.feature_layer_view_name,
            map_settings.get("feature_layer_view_name"),
            DEFAULT_VIEW_NAME,
        ),
        "feature_layer_view_url": _first_non_empty(
            args.feature_layer_view_url,
            outputs.get("feature_layer_view_url"),
            map_settings.get("feature_layer_view_url"),
        ),
        "source_feature_layer_item_id": _first_non_empty(
            args.source_feature_layer_item_id,
            map_settings.get("source_feature_layer_item_id"),
            outputs.get("source_feature_layer_item_id"),
        ),
        "feature_layer_view_item_id": _first_non_empty(
            args.feature_layer_view_item_id,
            outputs.get("feature_layer_view_item_id"),
            map_settings.get("feature_layer_view_item_id"),
        ),
        "web_map_name": _first_non_empty(args.web_map_name, map_settings.get("web_map_name"), DEFAULT_WEB_MAP_NAME),
        "web_map_item_id": _first_non_empty(
            args.web_map_item_id,
            outputs.get("web_map_item_id"),
            map_settings.get("web_map_item_id"),
        ),
        "sharing_group_id": _first_non_empty(args.sharing_group_id, map_settings.get("sharing_group_id")),
        "transaction_scope_field": transaction_scope_field,
        "completion_filter": _first_non_empty(
            args.completion_filter,
            map_settings.get("completion_filter"),
            "is_active = 1 AND case_status = 'review_closed'",
        ),
        "transaction_filter_template": _first_non_empty(
            args.transaction_filter_template,
            map_settings.get("transaction_filter_template"),
            f"{transaction_scope_field} = '{{transaction_number}}'",
        ),
        "extent_mode": _first_non_empty(
            args.extent_mode,
            map_settings.get("extent_mode"),
            "transaction_polygons_with_padding",
        ),
        "extent_padding_percent": (
            args.extent_padding_percent
            if args.extent_padding_percent is not None
            else float(map_settings.get("extent_padding_percent") or 15)
        ),
        "allow_source_service_map": bool(args.allow_source_service_map or map_settings.get("allow_source_service_map")),
        "reference_layers": reference_layers,
        "working_layers": {str(k): str(v) for k, v in working_layers.items() if v},
        "default_extent": map_settings.get("default_extent") if isinstance(map_settings.get("default_extent"), dict) else _jamaica_extent(),
    }


def _normalize_reference_layers(value: Any) -> dict[str, dict[str, str]]:
    result: dict[str, dict[str, str]] = {}
    if not isinstance(value, dict):
        return result
    for key, raw in value.items():
        if isinstance(raw, dict):
            result[str(key)] = {
                "url": str(raw.get("url") or ""),
                "item_id": str(raw.get("item_id") or ""),
                "title": str(raw.get("title") or _title_from_key(str(key))),
            }
        elif raw:
            result[str(key)] = {"url": str(raw), "item_id": "", "title": _title_from_key(str(key))}
    return result


def _validate_config(config: dict[str, Any], errors: list[str], warnings: list[str]) -> None:
    if not config["portal_url"]:
        errors.append("portal_url is required.")
    if not config["feature_layer_view_name"]:
        errors.append("feature_layer_view_name is required.")
    if not config["web_map_name"]:
        errors.append("web_map_name is required.")
    if not config["transaction_scope_field"]:
        errors.append("transaction_scope_field is required.")

    for role in REQUIRED_WORKING_ROLES:
        if not _working_layer_url(config, role):
            errors.append(f"working {role} layer URL is required.")

    if not config["feature_layer_view_url"]:
        warnings.append(
            "feature_layer_view_url is not configured. Dry-run will use working_review layer URLs; "
            "live external consumption should use a read-only Enterprise view."
        )
    if not config["reference_layers"]:
        warnings.append("No reference layers are configured for the Innola web map.")
    for key in ("survey_cadastre", "legal_cadastre", "fiscal_cadastre", "parish"):
        if key not in config["reference_layers"]:
            warnings.append(f"Reference layer '{key}' is not configured.")


def _reference_operational_layer(key: str, layer: dict[str, str]) -> dict[str, Any]:
    title = layer.get("title") or _title_from_key(key)
    url = layer.get("url") or ""
    item_id = layer.get("item_id") or ""
    payload = {
        "id": f"reference_{key}",
        "title": title,
        "visibility": key == "parish",
        "opacity": 0.65 if key != "parish" else 0.9,
        "layerType": "ArcGISFeatureLayer",
    }
    if url:
        payload["url"] = url
    if item_id:
        payload["itemId"] = item_id
    return payload


def _working_operational_layer(role: str, url: str, config: dict[str, Any]) -> dict[str, Any]:
    layer = {
        "id": f"working_{role}",
        "title": _working_title(role),
        "url": url,
        "visibility": role in {"polygons", "lines"},
        "opacity": 0.82 if role == "polygons" else 1,
        "layerType": "ArcGISFeatureLayer",
        "layerDefinition": {
            "definitionExpression": config["completion_filter"],
            "drawingInfo": _drawing_info(role),
        },
        "popupInfo": _popup_info(role),
    }
    labels = _labeling_info(role)
    if labels:
        layer["layerDefinition"]["drawingInfo"]["labelingInfo"] = labels
        layer["showLabels"] = True
    return layer


def _drawing_info(role: str) -> dict[str, Any]:
    if role == "polygons":
        return {
            "renderer": {
                "type": "simple",
                "symbol": {
                    "type": "esriSFS",
                    "style": "esriSFSSolid",
                    "color": [255, 229, 128, 95],
                    "outline": {"type": "esriSLS", "style": "esriSLSSolid", "color": [0, 113, 117, 255], "width": 1.4},
                },
            }
        }
    if role == "lines":
        return {
            "renderer": {
                "type": "simple",
                "symbol": {
                    "type": "esriSLS",
                    "style": "esriSLSSolid",
                    "color": [0, 112, 116, 255],
                    "width": 1.1,
                },
            }
        }
    if role == "points":
        return {
            "renderer": {
                "type": "simple",
                "symbol": {
                    "type": "esriSMS",
                    "style": "esriSMSCircle",
                    "color": [22, 122, 130, 255],
                    "size": 5,
                    "outline": {"color": [255, 255, 255, 255], "width": 1},
                },
            }
        }
    return {
        "renderer": {
            "type": "simple",
            "symbol": {
                "type": "esriSMS",
                "style": "esriSMSDiamond",
                "color": [214, 73, 51, 255],
                "size": 8,
            },
        }
    }


def _labeling_info(role: str) -> list[dict[str, Any]]:
    if role == "lines":
        return [_label_class("COGO Segment", LINE_LABEL_EXPRESSION, [255, 255, 255, 255], 9)]
    if role == "polygons":
        return [_label_class("Parcel + SUID", POLYGON_LABEL_EXPRESSION, [40, 40, 40, 255], 10)]
    return []


def _label_class(name: str, expression: str, color: list[int], size: int) -> dict[str, Any]:
    return {
        "labelExpressionInfo": {"expression": expression},
        "labelPlacement": "esriServerPolygonPlacementAlwaysHorizontal" if "Parcel" in name else "esriServerLinePlacementAboveAlong",
        "where": "",
        "useCodedValues": True,
        "name": name,
        "symbol": {
            "type": "esriTS",
            "color": color,
            "haloColor": [0, 0, 0, 120] if color[0] > 200 else [255, 255, 255, 200],
            "haloSize": 1,
            "font": {"family": "Arial", "size": size, "style": "normal", "weight": "bold"},
        },
        "minScale": 5000 if "Parcel" in name else 2500,
        "maxScale": 0,
    }


def _popup_info(role: str) -> dict[str, Any]:
    fields_by_role = {
        "polygons": [
            "transaction_number",
            "parcel_name",
            "SUID",
            "area_sq_m",
            "review_decision",
            "case_status",
            "workflow_stage",
            "created_by",
            "last_saved_utc",
        ],
        "lines": [
            "transaction_number",
            "parcel_name",
            "line_id",
            "start_pt",
            "end_pt",
            "bearing_txt",
            "length_txt",
            "distance_txt",
            "source_txt",
        ],
        "points": [
            "transaction_number",
            "parcel_name",
            "point_id",
            "parcel_group_id",
            "point_role",
            "status_txt",
            "source_txt",
        ],
        "issues": ["transaction_number", "issue_type", "issue_text", "review_decision", "case_status"],
    }
    fields = fields_by_role.get(role, [])
    return {
        "title": f"{_working_title(role)} - {{{fields[0]}}}" if fields else _working_title(role),
        "fieldInfos": [{"fieldName": field, "label": _title_from_key(field), "visible": True} for field in fields],
        "description": "",
        "showAttachments": False,
    }


def _base_map(config: dict[str, Any]) -> dict[str, Any]:
    imagery = config["reference_layers"].get("esri_world_imagery") or {}
    osm = config["reference_layers"].get("open_street_map") or {}
    base_layers: list[dict[str, Any]] = []
    if imagery.get("url") or imagery.get("item_id"):
        layer = _reference_operational_layer("esri_world_imagery", imagery)
        layer["id"] = "basemap_esri_world_imagery"
        base_layers.append(layer)
    elif osm.get("url") or osm.get("item_id"):
        layer = _reference_operational_layer("open_street_map", osm)
        layer["id"] = "basemap_open_street_map"
        base_layers.append(layer)
    else:
        base_layers.append(
            {
                "id": "default_world_imagery",
                "layerType": "ArcGISTiledMapServiceLayer",
                "title": "Esri World Imagery",
                "url": "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer",
                "visibility": True,
                "opacity": 1,
            }
        )
    return {"baseMapLayers": base_layers, "title": "Imagery"}


def _working_layer_urls(config: dict[str, Any]) -> dict[str, str]:
    return {role: _working_layer_url(config, role) for role in WORKING_ROLES if _working_layer_url(config, role)}


def _working_layer_url(config: dict[str, Any], role: str) -> str:
    if config["feature_layer_view_url"]:
        ids = {"issues": 0, "points": 1, "lines": 2, "polygons": 3}
        if role in ids:
            return f"{config['feature_layer_view_url'].rstrip('/')}/{ids[role]}"
    return str(config["working_layers"].get(role) or "")


def _layer_order_summary(config: dict[str, Any]) -> list[str]:
    names: list[str] = []
    for key in REFERENCE_LAYER_ORDER:
        if key in config["reference_layers"]:
            names.append(key)
    names.extend(role for role in WORKING_LAYER_ORDER if _working_layer_url(config, role))
    return names


def _generated_settings(config: dict[str, Any], outputs: dict[str, str]) -> dict[str, Any]:
    return {
        "enterprise_innola_map_view": {
            "enabled": True,
            "portal_url": config["portal_url"],
            "source_feature_layer_item_id": config["source_feature_layer_item_id"],
            "feature_layer_view_name": config["feature_layer_view_name"],
            "web_map_name": config["web_map_name"],
            "transaction_scope_field": config["transaction_scope_field"],
            "completion_filter": config["completion_filter"],
            "transaction_filter_template": config["transaction_filter_template"],
            "extent_mode": config["extent_mode"],
            "extent_padding_percent": config["extent_padding_percent"],
            "sharing_group_id": config["sharing_group_id"],
            "reference_layers": config["reference_layers"],
            "outputs": outputs,
        }
    }


def _ensure_feature_layer_view(
    config: dict[str, Any],
    errors: list[str],
    warnings: list[str],
) -> dict[str, Any]:
    if config["feature_layer_view_url"]:
        _validate_feature_server_has_children(config["feature_layer_view_url"], config.get("_token_env_var") or DEFAULT_TOKEN_ENV_VAR)
        return {
            "action": "configured",
            "source_feature_layer_item_id": config["source_feature_layer_item_id"],
            "feature_layer_view_item_id": config["feature_layer_view_item_id"],
            "feature_layer_view_url": config["feature_layer_view_url"],
        }

    token_env_var = config.get("_token_env_var") or DEFAULT_TOKEN_ENV_VAR
    token = os.environ.get(token_env_var, "")
    if not token:
        errors.append(f"Live feature layer view provisioning requires a portal token in {token_env_var}.")
        return {}

    try:
        source_item_id = config["source_feature_layer_item_id"] or _read_source_feature_layer_item_id(config, token_env_var)
        try:
            result = _create_or_get_feature_layer_view_rest(config, source_item_id, token_env_var)
        except Exception as rest_exc:  # noqa: BLE001 - fall back to ArcGIS API for deployments that require it.
            warnings.append(f"REST feature layer view creation failed; trying ArcGIS Python API fallback: {rest_exc}")
            result = _create_or_get_feature_layer_view_with_arcgis_api(
                portal_url=config["portal_url"],
                token=token,
                source_item_id=source_item_id,
                view_name=config["feature_layer_view_name"],
                view_item_id=config["feature_layer_view_item_id"],
                sharing_group_id=config["sharing_group_id"],
            )
        if result.get("source_feature_layer_item_id"):
            config["source_feature_layer_item_id"] = str(result["source_feature_layer_item_id"])
        return result
    except Exception as exc:  # noqa: BLE001 - admin diagnostics must stay concise and non-secret.
        if config.get("allow_source_service_map"):
            warnings.append(
                "Live feature layer view provisioning failed, so the web map will use the editable "
                f"working_review source service because allow_source_service_map is enabled: {type(exc).__name__}: {exc!r}"
            )
            return {
                "action": "source_service_fallback",
                "source_feature_layer_item_id": config["source_feature_layer_item_id"],
                "feature_layer_view_item_id": "",
                "feature_layer_view_url": "",
            }
        errors.append(
            "Live feature layer view provisioning failed: "
            f"{type(exc).__name__}: {exc!r}"
        )
        warnings.append(
            "Install/use the ArcGIS Pro Python environment with the arcgis package, or create "
            "working_review_innola_view manually and configure feature_layer_view_url."
        )
        return {}


def _create_or_get_feature_layer_view_rest(
    config: dict[str, Any],
    source_item_id: str,
    token_env_var: str,
) -> dict[str, Any]:
    username = _read_portal_username(config["portal_url"], token_env_var)
    existing_id = config["feature_layer_view_item_id"] or _find_item_id(
        config["portal_url"],
        username,
        config["feature_layer_view_name"],
        "Feature Service",
        token_env_var,
    )
    if existing_id:
        item = _read_portal_item(config["portal_url"], existing_id, token_env_var)
        view_url = str(item.get("url") or "")
        _validate_feature_server_has_children(view_url, token_env_var)
        return {
            "action": "reused",
            "source_feature_layer_item_id": source_item_id,
            "feature_layer_view_item_id": existing_id,
            "feature_layer_view_url": view_url,
        }

    source_url = _working_service_url(config)
    source_metadata = _post_form(source_url, {}, token_env_var)
    create_result = _create_view_service_rest(config, username, source_metadata, token_env_var)
    view_item_id = str(create_result.get("itemId") or create_result.get("itemid") or create_result.get("id") or "")
    if not view_item_id:
        raise RuntimeError(f"createService did not return a view item id: {_summarize_result(create_result)}")

    view_item = _read_portal_item(config["portal_url"], view_item_id, token_env_var)
    view_url = str(view_item.get("url") or "").rstrip("/")
    if not view_url:
        raise RuntimeError(f"View item {view_item_id} did not expose a FeatureServer URL.")

    add_definition = _build_view_add_to_definition(source_url, source_metadata)
    _add_view_definition_rest(view_url, add_definition, token_env_var)
    _validate_feature_server_has_children(view_url, token_env_var)
    _update_view_capabilities_rest(view_url, token_env_var)
    if config["sharing_group_id"]:
        _share_item(config["portal_url"], username, view_item_id, config["sharing_group_id"], token_env_var)

    return {
        "action": "created",
        "source_feature_layer_item_id": source_item_id,
        "feature_layer_view_item_id": view_item_id,
        "feature_layer_view_url": view_url,
    }


def _create_view_service_rest(
    config: dict[str, Any],
    username: str,
    source_metadata: dict[str, Any],
    token_env_var: str,
) -> dict[str, Any]:
    create_parameters = {
        "name": config["feature_layer_view_name"],
        "isView": True,
        "sourceSchemaChangesAllowed": True,
        "isUpdatableView": False,
        "spatialReference": source_metadata.get("spatialReference") or {"wkid": 102100, "latestWkid": 3857},
        "initialExtent": source_metadata.get("initialExtent") or source_metadata.get("fullExtent") or _jamaica_extent(),
        "capabilities": "Query",
        "preserveLayerIds": True,
        "options": {"dataSourceType": "relational"},
    }
    form = {
        "isView": "true",
        "outputType": "featureService",
        "createParameters": json.dumps(create_parameters, separators=(",", ":")),
        "tags": "Sidwell,Innola,Compute Review,working_review",
        "snippet": "Innola read-only view of completed Compute review working layers.",
        "description": "Read-only hosted feature layer view used by Innola Compute review web map.",
    }
    return _post_form(
        f"{config['portal_url']}/sharing/rest/content/users/{username}/createService",
        form,
        token_env_var,
    )


def _build_view_add_to_definition(source_url: str, source_metadata: dict[str, Any]) -> dict[str, Any]:
    source_service_name = _source_service_name(source_url)
    layers = [
        _view_child_definition(child, source_service_name, "Feature Layer")
        for child in source_metadata.get("layers", [])
        if isinstance(child, dict)
    ]
    tables = [
        _view_child_definition(child, source_service_name, "Table")
        for child in source_metadata.get("tables", [])
        if isinstance(child, dict)
    ]
    return {"layers": layers, "tables": tables}


def _view_child_definition(child: dict[str, Any], source_service_name: str, child_type: str) -> dict[str, Any]:
    layer_id = int(child.get("id"))
    definition = {
        "id": layer_id,
        "name": str(child.get("name") or f"layer_{layer_id}"),
        "adminLayerInfo": {
            "viewLayerDefinition": {
                "sourceServiceName": source_service_name,
                "sourceLayerId": layer_id,
                "sourceLayerFields": "*",
            }
        },
    }
    if child_type == "Table":
        definition["type"] = "Table"
    return definition


def _add_view_definition_rest(view_url: str, add_definition: dict[str, Any], token_env_var: str) -> None:
    errors: list[str] = []
    for url in _add_to_definition_urls(view_url):
        try:
            _post_form(
                url,
                {"addToDefinition": json.dumps(add_definition, separators=(",", ":"))},
                token_env_var,
            )
            return
        except Exception as exc:  # noqa: BLE001 - try supported URL variants before failing.
            errors.append(f"{url}: {exc}")
    raise RuntimeError("Could not add child layer/table definitions to feature layer view. " + " | ".join(errors))


def _update_view_capabilities_rest(view_url: str, token_env_var: str) -> None:
    for url in _update_definition_urls(view_url):
        try:
            _post_form(url, {"updateDefinition": json.dumps({"capabilities": "Query"})}, token_env_var)
            return
        except Exception:
            continue


def _add_to_definition_urls(feature_server_url: str) -> list[str]:
    root, service_path = _split_server_service_path(feature_server_url)
    urls = [f"{feature_server_url.rstrip('/')}/addToDefinition", f"{feature_server_url.rstrip('/')}/admin/addToDefinition"]
    if service_path:
        urls.extend(
            [
                f"{root}/rest/admin/services/{service_path}.FeatureServer/addToDefinition",
                f"{root}/admin/services/{service_path}.FeatureServer/addToDefinition",
            ]
        )
    return list(dict.fromkeys(urls))


def _update_definition_urls(feature_server_url: str) -> list[str]:
    root, service_path = _split_server_service_path(feature_server_url)
    urls = [f"{feature_server_url.rstrip('/')}/updateDefinition", f"{feature_server_url.rstrip('/')}/admin/updateDefinition"]
    if service_path:
        urls.extend(
            [
                f"{root}/rest/admin/services/{service_path}.FeatureServer/updateDefinition",
                f"{root}/admin/services/{service_path}.FeatureServer/updateDefinition",
            ]
        )
    return list(dict.fromkeys(urls))


def _split_server_service_path(feature_server_url: str) -> tuple[str, str]:
    text = feature_server_url.rstrip("/")
    marker = "/rest/services/"
    if marker not in text or not text.endswith("/FeatureServer"):
        return text, ""
    root, service = text.split(marker, 1)
    service_path = service[: -len("/FeatureServer")]
    return root, service_path


def _source_service_name(feature_server_url: str) -> str:
    _root, service_path = _split_server_service_path(feature_server_url)
    return service_path.split("/")[-1] if service_path else ""


def _create_or_get_feature_layer_view_with_arcgis_api(
    *,
    portal_url: str,
    token: str,
    source_item_id: str,
    view_name: str,
    view_item_id: str = "",
    sharing_group_id: str = "",
) -> dict[str, Any]:
    if not source_item_id:
        raise RuntimeError("source_feature_layer_item_id could not be resolved from working_review.")

    try:
        from arcgis.features import FeatureLayerCollection  # type: ignore
        from arcgis.gis import GIS  # type: ignore
    except Exception as exc:  # noqa: BLE001 - module availability depends on ArcGIS Pro Python.
        raise RuntimeError("ArcGIS Python API package 'arcgis' is required to create hosted feature layer views.") from exc

    gis = GIS(portal_url, token=token)
    view_item = _find_feature_service_item(gis, view_name, view_item_id)

    action = "reused"
    if view_item is None:
        source_item = gis.content.get(source_item_id)
        if source_item is None:
            raise RuntimeError(f"Source Feature Service item was not found: {source_item_id}")
        collection = FeatureLayerCollection.fromitem(source_item)
        try:
            view_item = collection.manager.create_view(name=view_name)
        except Exception as exc:  # noqa: BLE001 - ArcGIS API can throw after Portal creates the item.
            view_item = _find_feature_service_item(gis, view_name, "")
            if view_item is None:
                raise RuntimeError(
                    "ArcGIS Python API create_view failed and no matching view item was found after retry: "
                    f"{type(exc).__name__}: {exc!r}"
                ) from exc
            action = "reused_after_create_exception"
        else:
            action = "created"

    _make_feature_layer_view_query_only(view_item)
    if sharing_group_id:
        view_item.share(groups=sharing_group_id)

    return {
        "action": action,
        "source_feature_layer_item_id": source_item_id,
        "feature_layer_view_item_id": str(getattr(view_item, "id", "") or ""),
        "feature_layer_view_url": str(getattr(view_item, "url", "") or ""),
    }


def _find_feature_service_item(gis: Any, title: str, item_id: str = "") -> Any:
    if item_id:
        item = gis.content.get(item_id)
        if item is not None:
            return item

    matches = gis.content.search(
        query=f'title:"{title}" AND type:"Feature Service"',
        item_type="Feature Service",
        max_items=10,
    )
    for item in matches:
        if str(getattr(item, "title", "")).lower() == title.lower():
            return item
    return None


def _make_feature_layer_view_query_only(view_item: Any) -> None:
    try:
        from arcgis.features import FeatureLayerCollection  # type: ignore

        collection = FeatureLayerCollection.fromitem(view_item)
        collection.manager.update_definition({"capabilities": "Query"})
    except Exception:
        # Enterprise deployments vary in whether hosted view capabilities can be
        # changed here. The view is still safer than the editable source; the web
        # map itself remains query-oriented.
        return


def _read_source_feature_layer_item_id(config: dict[str, Any], token_env_var: str) -> str:
    service_url = _working_service_url(config)
    if not service_url:
        raise RuntimeError("Could not derive working_review FeatureServer URL from configured working layer URLs.")
    metadata = _post_form(service_url, {}, token_env_var)
    item_id = str(metadata.get("serviceItemId") or metadata.get("serviceItemID") or "").strip()
    if not item_id:
        raise RuntimeError("working_review FeatureServer metadata did not expose serviceItemId.")
    return item_id


def _validate_feature_server_has_children(feature_server_url: str, token_env_var: str) -> None:
    if not feature_server_url:
        raise RuntimeError("FeatureServer URL is required.")
    metadata = _post_form(feature_server_url.rstrip("/"), {}, token_env_var)
    layers = metadata.get("layers") if isinstance(metadata.get("layers"), list) else []
    tables = metadata.get("tables") if isinstance(metadata.get("tables"), list) else []
    if not layers and not tables:
        raise RuntimeError(
            f"FeatureServer has no child layers/tables and cannot be used for the Innola map: {feature_server_url}"
        )


def _working_service_url(config: dict[str, Any]) -> str:
    for url in config["working_layers"].values():
        text = str(url or "").strip().rstrip("/")
        if "/FeatureServer" not in text:
            continue
        root, _suffix = text.split("/FeatureServer", 1)
        return f"{root}/FeatureServer"
    return ""


def _provision_live_web_map(
    config: dict[str, Any],
    web_map: dict[str, Any],
    errors: list[str],
    warnings: list[str],
) -> dict[str, Any]:
    token_env_var = config.get("_token_env_var") or DEFAULT_TOKEN_ENV_VAR
    token = os.environ.get(token_env_var, "")
    if not token:
        errors.append(f"Live web map provisioning requires a portal token in {token_env_var}.")
        return {}

    if not config["feature_layer_view_url"]:
        errors.append("Live provisioning requires feature_layer_view_url. Create/configure the read-only view first.")
        return {}

    try:
        username = _read_portal_username(config["portal_url"], token_env_var)
        item_id = config["web_map_item_id"] or _find_item_id(
            config["portal_url"],
            username,
            config["web_map_name"],
            "Web Map",
            token_env_var,
        )
        if item_id:
            result = _update_web_map_item(config["portal_url"], username, item_id, config, web_map, token_env_var)
            action = "updated"
        else:
            result = _add_web_map_item(config["portal_url"], username, config, web_map, token_env_var)
            item_id = str(result.get("id") or "")
            action = "created"

        if config["sharing_group_id"] and item_id:
            _share_item(config["portal_url"], username, item_id, config["sharing_group_id"], token_env_var)

        return {"action": action, "web_map_item_id": item_id, "portal_response": _redact(result)}
    except Exception as exc:  # noqa: BLE001 - return admin diagnostics instead of leaking stack traces to UI.
        errors.append(f"Live web map provisioning failed: {exc}")
        warnings.append("No Portal tokens or passwords were written to diagnostics.")
        return {}


def _add_web_map_item(
    portal_url: str,
    username: str,
    config: dict[str, Any],
    web_map: dict[str, Any],
    token_env_var: str,
) -> dict[str, Any]:
    return _post_form(
        f"{portal_url}/sharing/rest/content/users/{username}/addItem",
        _web_map_item_form(config, web_map),
        token_env_var,
    )


def _update_web_map_item(
    portal_url: str,
    username: str,
    item_id: str,
    config: dict[str, Any],
    web_map: dict[str, Any],
    token_env_var: str,
) -> dict[str, Any]:
    return _post_form(
        f"{portal_url}/sharing/rest/content/users/{username}/items/{item_id}/update",
        _web_map_item_form(config, web_map),
        token_env_var,
    )


def _web_map_item_form(config: dict[str, Any], web_map: dict[str, Any]) -> dict[str, str]:
    return {
        "title": config["web_map_name"],
        "type": "Web Map",
        "text": json.dumps(web_map, separators=(",", ":")),
        "snippet": "Innola Compute review transaction map.",
        "description": "Curated map for completed Compute review geometry from working_review.",
        "tags": "Sidwell,Innola,Compute Review,working_review",
    }


def _share_item(portal_url: str, username: str, item_id: str, group_id: str, token_env_var: str) -> None:
    _post_form(
        f"{portal_url}/sharing/rest/content/users/{username}/items/{item_id}/share",
        {"groups": group_id},
        token_env_var,
    )


def _find_item_id(portal_url: str, username: str, title: str, item_type: str, token_env_var: str) -> str:
    query = f'title:"{title}" AND owner:{username} AND type:"{item_type}"'
    result = _post_form(f"{portal_url}/sharing/rest/search", {"q": query, "num": "10"}, token_env_var)
    for item in result.get("results", []):
        if str(item.get("title") or "").lower() == title.lower():
            return str(item.get("id") or "")
    return ""


def _read_portal_username(portal_url: str, token_env_var: str) -> str:
    profile = _post_form(f"{portal_url}/sharing/rest/community/self", {}, token_env_var)
    username = str(profile.get("username") or "").strip()
    if not username:
        raise RuntimeError("Portal profile did not return a username.")
    return username


def _read_portal_item(portal_url: str, item_id: str, token_env_var: str) -> dict[str, Any]:
    return _post_form(f"{portal_url}/sharing/rest/content/items/{item_id}", {}, token_env_var)


def _post_form(url: str, form: dict[str, Any], token_env_var: str, *, raise_on_error: bool = True) -> dict[str, Any]:
    payload = {"f": "json", **{key: str(value) for key, value in form.items()}}
    token = os.environ.get(token_env_var, "") if token_env_var else ""
    if token:
        payload["token"] = token
    data = urlencode(payload).encode("utf-8")
    request = Request(url, data=data, headers={"Content-Type": "application/x-www-form-urlencoded"})
    with urlopen(request, timeout=60) as response:
        result = json.loads(response.read().decode("utf-8"))
    if raise_on_error and _is_error_response(result):
        raise RuntimeError(_summarize_error(result))
    return result


def _is_error_response(result: dict[str, Any]) -> bool:
    return bool(result.get("error")) or str(result.get("status") or "").lower() == "error" or result.get("success") is False


def _summarize_error(result: dict[str, Any]) -> str:
    error = result.get("error") if isinstance(result.get("error"), dict) else {}
    messages = result.get("messages") if isinstance(result.get("messages"), list) else []
    parts = [
        str(error.get("message") or ""),
        "; ".join(str(message) for message in messages),
        str(result.get("status") or ""),
    ]
    return "ArcGIS Enterprise returned error: " + " | ".join(part for part in parts if part)


def _summarize_result(result: dict[str, Any]) -> str:
    redacted = _redact(result)
    text = json.dumps(redacted, sort_keys=True)
    return text[:1000]


def _redact(value: Any) -> Any:
    if isinstance(value, dict):
        return {
            key: "***REDACTED***" if "token" in key.lower() or "password" in key.lower() else _redact(val)
            for key, val in value.items()
        }
    if isinstance(value, list):
        return [_redact(item) for item in value]
    return value


def _status_for(mode: str, dry_run: bool) -> str:
    if mode == "validate":
        return "validated"
    if mode == "export-config":
        return "exported"
    return "provision_ready" if dry_run else "provisioned"


def _messages_for(mode: str, dry_run: bool, errors: list[str]) -> list[str]:
    if errors:
        return ["Innola map view request failed validation. No Enterprise web map changes were made."]
    if mode == "validate":
        return ["Innola map view settings validated."]
    if mode == "export-config":
        return ["Innola map view definition exported without touching Portal."]
    return ["Innola map view provision request is ready."] if dry_run else ["Innola map web map item was provisioned."]


def _transaction_url_template(web_map_url: str, config: dict[str, Any]) -> str:
    if not web_map_url:
        return ""
    separator = "&" if "?" in web_map_url else "?"
    return f"{web_map_url}{separator}transaction_number={{transaction_number}}"


def _web_map_url(portal_url: str, item_id: str) -> str:
    if not portal_url or not item_id:
        return ""
    return f"{portal_url.rstrip('/')}/home/webmap/viewer.html?webmap={item_id}"


def _first_non_empty(*values: Any) -> str:
    for value in values:
        if value is None:
            continue
        text = str(value).strip()
        if text:
            return text
    return ""


def _working_title(role: str) -> str:
    return {
        "polygons": "Compute Review Parcels",
        "lines": "Compute Review Lines",
        "points": "Compute Review Points",
        "issues": "Compute Review Issues",
    }.get(role, _title_from_key(role))


def _title_from_key(key: str) -> str:
    return key.replace("_", " ").strip().title()


def _jamaica_extent() -> dict[str, Any]:
    return {
        "xmin": -8780000,
        "ymin": 1970000,
        "xmax": -8420000,
        "ymax": 2185000,
        "spatialReference": {"wkid": 102100, "latestWkid": 3857},
    }


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    payload = build_payload(args)
    # Preserve token env setting only internally during live execution.
    payload.pop("_token_env_var", None)
    write_payload(payload, args.output_json)
    return 1 if payload["status"] == "failed" else 0


if __name__ == "__main__":
    raise SystemExit(main())
