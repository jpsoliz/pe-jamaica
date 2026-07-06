"""Admin helper for Enterprise generic working layer provisioning.

The default mode is dry-run so tests and Settings validation never require
portal credentials. Live ArcGIS Enterprise calls can be added behind the same
contract without changing the Settings workspace.
"""

from __future__ import annotations

import argparse
import datetime as _dt
import getpass
import json
import os
import time
from pathlib import Path
from typing import Any
from urllib.parse import urlencode
from urllib.request import Request, urlopen


LAYER_ROLES = ("points", "lines", "polygons", "case_index", "issues")
REQUIRED_LAYER_ROLES = ("points", "lines", "polygons", "case_index")
SCHEMA_TEMPLATE_ENV_VAR = "ARCGIS_WORKING_SCHEMA_TEMPLATE"
SHARED_FIELDS = (
    "transaction_number",
    "transaction_id",
    "task_id",
    "workflow_stage",
    "review_state",
    "case_status",
    "created_by",
    "created_utc",
    "last_saved_by",
    "last_saved_utc",
    "run_id",
    "review_decision",
    "review_decision_by",
    "review_decision_utc",
    "review_comment",
    "official_comparison_status",
    "official_reference_ids",
    "is_active",
    "edit_generation",
)
DISPOSITION_FIELDS = (
    "review_decision",
    "review_decision_by",
    "review_decision_utc",
    "review_comment",
    "official_comparison_status",
    "official_reference_ids",
)
REVIEW_DECISION_VALUES = ("pending", "approved", "rejected", "postponed")
FIELD_SPECS = {
    "transaction_number": ("esriFieldTypeString", 64),
    "transaction_id": ("esriFieldTypeString", 64),
    "task_id": ("esriFieldTypeString", 64),
    "workflow_stage": ("esriFieldTypeString", 64),
    "review_state": ("esriFieldTypeString", 64),
    "case_status": ("esriFieldTypeString", 64),
    "created_by": ("esriFieldTypeString", 128),
    "created_utc": ("esriFieldTypeDate", None),
    "last_saved_by": ("esriFieldTypeString", 128),
    "last_saved_utc": ("esriFieldTypeDate", None),
    "run_id": ("esriFieldTypeString", 64),
    "review_decision": ("esriFieldTypeString", 32),
    "review_decision_by": ("esriFieldTypeString", 128),
    "review_decision_utc": ("esriFieldTypeDate", None),
    "review_comment": ("esriFieldTypeString", 1024),
    "official_comparison_status": ("esriFieldTypeString", 64),
    "official_reference_ids": ("esriFieldTypeString", 512),
    "is_active": ("esriFieldTypeInteger", None),
    "edit_generation": ("esriFieldTypeInteger", None),
    "point_id": ("esriFieldTypeString", 64),
    "parcel_group_id": ("esriFieldTypeString", 64),
    "parcel_name": ("esriFieldTypeString", 128),
    "point_role": ("esriFieldTypeString", 64),
    "status_txt": ("esriFieldTypeString", 64),
    "source_txt": ("esriFieldTypeString", 1024),
    "row_id": ("esriFieldTypeString", 64),
    "line_id": ("esriFieldTypeString", 64),
    "start_pt": ("esriFieldTypeString", 64),
    "end_pt": ("esriFieldTypeString", 64),
    "bearing_txt": ("esriFieldTypeString", 64),
    "distance_txt": ("esriFieldTypeString", 64),
    "length_txt": ("esriFieldTypeString", 128),
    "line_type": ("esriFieldTypeString", 32),
    "seg_index": ("esriFieldTypeInteger", None),
    "parcel_type": ("esriFieldTypeString", 64),
    "validation_status": ("esriFieldTypeString", 64),
    "closure_status": ("esriFieldTypeString", 64),
    "area_sq_m": ("esriFieldTypeDouble", None),
    "perimeter_m": ("esriFieldTypeDouble", None),
    "review_note": ("esriFieldTypeString", 512),
    "SUID": ("esriFieldTypeString", 64),
    "case_id": ("esriFieldTypeString", 64),
    "workflow_name": ("esriFieldTypeString", 64),
    "assigned_user": ("esriFieldTypeString", 128),
    "assigned_group": ("esriFieldTypeString", 128),
    "output_summary_ref": ("esriFieldTypeString", 256),
    "working_publish_ref": ("esriFieldTypeString", 256),
    "recoverability_state": ("esriFieldTypeString", 64),
    "spatial_unit_id": ("esriFieldTypeString", 64),
    "spatial_unit_api_status": ("esriFieldTypeString", 64),
    "issue_type": ("esriFieldTypeString", 64),
    "issue_text": ("esriFieldTypeString", 1024),
}
POINT_FIELDS = (
    "point_id",
    "parcel_group_id",
    "parcel_name",
    "point_role",
    "status_txt",
    "source_txt",
    "row_id",
)
LINE_FIELDS = (
    "line_id",
    "parcel_group_id",
    "parcel_name",
    "start_pt",
    "end_pt",
    "bearing_txt",
    "distance_txt",
    "length_txt",
    "line_type",
    "seg_index",
    "source_txt",
)
POLYGON_FIELDS = (
    "parcel_group_id",
    "parcel_name",
    "parcel_type",
    "validation_status",
    "closure_status",
    "area_sq_m",
    "perimeter_m",
    "review_note",
    "SUID",
    "source_txt",
)
CASE_INDEX_FIELDS = (
    "case_id",
    "workflow_name",
    "assigned_user",
    "assigned_group",
    "output_summary_ref",
    "working_publish_ref",
    "recoverability_state",
    "spatial_unit_id",
    "spatial_unit_api_status",
)
ISSUE_FIELDS = ("issue_type", "issue_text")
WORKING_LINES_LABEL_CLASS_NAME = "COGO Segment"
WORKING_LINES_LABEL_FIELDS = ("bearing_txt", "length_txt", "distance_txt")
WORKING_LINES_LABEL_EXPRESSION = (
    "var len = IIf(IsEmpty($feature.length_txt), $feature.distance_txt, $feature.length_txt); "
    "When(IsEmpty($feature.bearing_txt) && IsEmpty(len), '', "
    "IsEmpty($feature.bearing_txt), len, "
    "IsEmpty(len), $feature.bearing_txt, "
    "$feature.bearing_txt + TextFormatting.NewLine + len)"
)


def build_payload(args: argparse.Namespace) -> dict[str, Any]:
    errors: list[str] = []
    warnings: list[str] = []
    layers = _read_layer_arguments(args)

    if not args.schema_version.strip():
        errors.append("schema_version is required.")

    if not args.target_service_name.strip():
        errors.append("target_service_name is required.")

    if args.mode in {"validate", "cleanup"}:
        for role in REQUIRED_LAYER_ROLES:
            if not layers[role]:
                errors.append(f"{role} layer URL is required for {args.mode}.")

    if args.mode == "cleanup" and args.require_cleanup_scope and not args.cleanup_scope_value.strip():
        errors.append("cleanup_scope_value is required for cleanup.")

    live_metadata: dict[str, Any] = {}
    visualization = _visualization_diagnostics("skipped" if args.dry_run else "not_checked")
    if args.mode == "validate" and args.dry_run is False and not errors:
        live_metadata, visualization = _validate_live_layers(args, layers, errors, warnings)

    generated_layers = layers if any(layers.values()) else _generated_layer_urls(args)
    cleanup_counts = {role: 0 for role in LAYER_ROLES}
    if args.mode == "provision" and args.dry_run is False and not errors:
        generated_layers = _provision_live_layers(args, errors, warnings)
        if not errors:
            visualization = _apply_visualization_defaults(generated_layers, args.token_env_var, errors, warnings)
    elif args.mode == "cleanup" and args.dry_run is False and not errors:
        cleanup_counts = _cleanup_live_layers(args, layers, errors, warnings)

    status = "failed" if errors else _status_for_mode(args.mode, args.dry_run)
    now_utc = _dt.datetime.now(_dt.timezone.utc).isoformat()

    return {
        "schema_version": args.schema_version,
        "mode": args.mode,
        "status": status,
        "dry_run": args.dry_run,
        "operator": getpass.getuser(),
        "timestamp_utc": now_utc,
        "target": {
            "portal_url": args.portal_url,
            "service_root": args.service_root,
            "target_folder": args.target_folder,
            "target_service_name": args.target_service_name,
            "workspace_name": args.workspace_name,
        },
        "schema": {
            "layer_roles": list(LAYER_ROLES),
            "required_layer_roles": list(REQUIRED_LAYER_ROLES),
            "shared_fields": list(SHARED_FIELDS),
        },
        "layers": generated_layers,
        "visualization": visualization,
        "validation": {
            "errors": errors,
            "warnings": warnings,
            "live_metadata": live_metadata,
        },
        "cleanup": {
            "scope_field": args.cleanup_scope_field,
            "scope_value": args.cleanup_scope_value,
            "mode": args.cleanup_mode,
            "affected_counts": cleanup_counts,
        },
        "generated_settings": {
            "enterprise_working_review": {
                "enabled": True,
                "service_root": args.service_root,
                "workspace_name": args.workspace_name or args.target_service_name,
                "transaction_scope_field": args.cleanup_scope_field,
                "layers": generated_layers,
            }
        },
        "messages": _messages_for_mode(args.mode, args.dry_run, errors),
    }


def write_payload(payload: dict[str, Any], output_json: str | None) -> None:
    text = json.dumps(payload, indent=2)
    if output_json:
        output_path = Path(output_json)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(text, encoding="utf-8")

    print(text)


def write_cleanup_audit(payload: dict[str, Any], audit_json: str | None) -> None:
    if payload["mode"] != "cleanup" or not audit_json:
        return

    cleanup = payload["cleanup"]
    audit_payload = {
        "schema_version": payload["schema_version"],
        "operator": payload["operator"],
        "timestamp_utc": payload["timestamp_utc"],
        "scope_field": cleanup["scope_field"],
        "scope_value": cleanup["scope_value"],
        "cleanup_mode": cleanup["mode"],
        "affected_counts": cleanup["affected_counts"],
        "status": payload["status"],
    }
    audit_path = Path(audit_json)
    audit_path.parent.mkdir(parents=True, exist_ok=True)
    audit_path.write_text(json.dumps(audit_payload, indent=2), encoding="utf-8")


def _read_layer_arguments(args: argparse.Namespace) -> dict[str, str]:
    return {
        "points": args.points_layer.strip(),
        "lines": args.lines_layer.strip(),
        "polygons": args.polygons_layer.strip(),
        "case_index": args.case_index_layer.strip(),
        "issues": args.issues_layer.strip(),
    }


def _generated_layer_urls(args: argparse.Namespace) -> dict[str, str]:
    root = args.service_root.rstrip("/") or "https://example.invalid/arcgis/rest"
    service = args.target_service_name.strip() or "sidwell_working_review"
    base = _default_feature_service_url(root, service)
    return {
        "points": f"{base}/0",
        "lines": f"{base}/1",
        "polygons": f"{base}/2",
        "case_index": f"{base}/3",
        "issues": f"{base}/4",
    }


def _validate_live_layers(
    args: argparse.Namespace,
    layers: dict[str, str],
    errors: list[str],
    warnings: list[str],
) -> tuple[dict[str, Any], dict[str, Any]]:
    metadata: dict[str, Any] = {}
    visualization = _visualization_diagnostics("not_checked")
    for role, url in layers.items():
        if not url:
            continue

        try:
            layer_metadata = _fetch_layer_metadata(url, args.token_env_var)
        except Exception as exc:  # noqa: BLE001 - diagnostics must stay non-secret and non-fatal.
            errors.append(f"{role} layer metadata could not be read: {exc}")
            continue

        metadata[role] = _summarize_layer_metadata(layer_metadata)
        _validate_capabilities(role, layer_metadata, errors)
        _validate_geometry(role, layer_metadata, errors, warnings)
        _validate_fields(role, layer_metadata, errors, warnings)
        if role == "lines":
            visualization = _validate_working_lines_visualization(layer_metadata, errors, warnings)

    return metadata, visualization


def _visualization_diagnostics(status: str, message: str = "") -> dict[str, Any]:
    diagnostics: dict[str, Any] = {
        "status": status,
        "lines": {
            "label_class": WORKING_LINES_LABEL_CLASS_NAME,
            "label_expression": WORKING_LINES_LABEL_EXPRESSION,
            "label_fields": list(WORKING_LINES_LABEL_FIELDS),
        },
    }
    if message:
        diagnostics["message"] = message

    return diagnostics


def _working_lines_labeling_info() -> list[dict[str, Any]]:
    return [
        {
            "name": WORKING_LINES_LABEL_CLASS_NAME,
            "where": "1=1",
            "labelExpressionInfo": {"expression": WORKING_LINES_LABEL_EXPRESSION},
            "labelPlacement": "esriServerLinePlacementAboveAlong",
            "useCodedValues": True,
            "minScale": 0,
            "maxScale": 0,
            "symbol": {
                "type": "esriTS",
                "color": [255, 255, 255, 255],
                "haloColor": [75, 75, 75, 255],
                "haloSize": 1,
                "font": {
                    "family": "Arial",
                    "size": 8,
                    "style": "normal",
                    "weight": "bold",
                    "decoration": "none",
                },
            },
        }
    ]


def _apply_visualization_defaults(
    layers: dict[str, str],
    token_env_var: str,
    errors: list[str],
    warnings: list[str],
) -> dict[str, Any]:
    lines_layer = layers.get("lines", "")
    if not lines_layer:
        errors.append("Enterprise default visualization setup failed: lines layer URL was not available.")
        return _visualization_diagnostics(
            "failed",
            "working_lines URL was not available; data publishing remains separate from visualization default setup.",
        )

    result = _apply_working_lines_visualization(lines_layer, token_env_var)
    if result["status"] == "failed":
        errors.append(f"Enterprise default visualization setup failed: {result['message']}")
    elif result["status"] == "applied":
        warnings.append("Enterprise working_lines default COGO labeling was applied.")

    diagnostics = _visualization_diagnostics(result["status"], result.get("message", ""))
    diagnostics["lines"]["service_layer_url"] = lines_layer
    return diagnostics


def _apply_working_lines_visualization(lines_layer_url: str, token_env_var: str) -> dict[str, str]:
    errors: list[str] = []
    form = {"updateDefinition": json.dumps({"drawingInfo": {"labelingInfo": _working_lines_labeling_info()}})}
    for update_definition_url in _update_definition_urls(lines_layer_url):
        try:
            _post_form(update_definition_url, form, token_env_var)
            return {
                "status": "applied",
                "message": "working_lines default COGO labeling was applied.",
            }
        except Exception as exc:  # noqa: BLE001 - try the next supported Enterprise URL shape.
            errors.append(f"{update_definition_url}: {type(exc).__name__}: {exc}")

    return {
        "status": "failed",
        "message": (
            "Could not apply working_lines default COGO labeling. "
            f"{' | '.join(errors)}. "
            "data publishing remains separate from visualization default setup."
        ),
    }


def _update_definition_urls(layer_url: str) -> list[str]:
    layer_url = layer_url.rstrip("/")
    urls = [f"{layer_url}/updateDefinition"]
    marker = "/rest/services/"
    if marker in layer_url:
        root, service_path = layer_url.split(marker, 1)
        parts = service_path.rsplit("/FeatureServer/", 1)
        if len(parts) == 2 and parts[1].isdigit():
            service_name, child_id = parts
            urls.append(f"{root}/rest/admin/services/{service_name}.FeatureServer/{child_id}/updateDefinition")
            urls.append(f"{root}/admin/services/{service_name}.FeatureServer/{child_id}/updateDefinition")
            urls.append(f"{root}/rest/services/{service_name}/FeatureServer/{child_id}/admin/updateDefinition")

    return list(dict.fromkeys(urls))


def _validate_working_lines_visualization(
    metadata: dict[str, Any],
    errors: list[str],
    warnings: list[str],
) -> dict[str, Any]:
    labeling_info = (metadata.get("drawingInfo") or {}).get("labelingInfo")
    if not isinstance(labeling_info, list) or not labeling_info:
        warnings.append("working_lines default visualization labelingInfo is not configured.")
        return _visualization_diagnostics("failed", "working_lines labelingInfo is not configured.")

    expected_expression = _normalize_expression(WORKING_LINES_LABEL_EXPRESSION)
    for label_class in labeling_info:
        if not isinstance(label_class, dict):
            continue

        expression = (label_class.get("labelExpressionInfo") or {}).get("expression", "")
        if _normalize_expression(str(expression)) == expected_expression:
            return _visualization_diagnostics("already_current", "working_lines default COGO labeling is current.")

    message = "working_lines default visualization labelingInfo does not match the expected COGO expression."
    warnings.append(message)
    return _visualization_diagnostics("failed", message)


def _normalize_expression(expression: str) -> str:
    return "".join(expression.split()).lower()


def _fetch_layer_metadata(url: str, token_env_var: str) -> dict[str, Any]:
    query = {"f": "json"}
    token = os.environ.get(token_env_var, "") if token_env_var else ""
    if token:
        query["token"] = token

    separator = "&" if "?" in url else "?"
    request_url = f"{url}{separator}{urlencode(query)}"
    with urlopen(request_url, timeout=30) as response:
        return json.loads(response.read().decode("utf-8"))


def _post_form(url: str, form: dict[str, Any], token_env_var: str, *, raise_on_error: bool = True) -> dict[str, Any]:
    token = os.environ.get(token_env_var, "") if token_env_var else ""
    payload = {"f": "json", **form}
    if token:
        payload["token"] = token

    data = urlencode(payload).encode("utf-8")
    request = Request(url, data=data, method="POST")
    with urlopen(request, timeout=60) as response:
        result = json.loads(response.read().decode("utf-8"))

    if raise_on_error and isinstance(result, dict) and result.get("error"):
        if isinstance(result["error"], dict):
            message = result["error"].get("message") or "ArcGIS Enterprise returned an error."
            details = result["error"].get("details") or []
            if details:
                message = f"{message} Details: {'; '.join(str(detail) for detail in details)}"
        else:
            message = str(result["error"])
        raise RuntimeError(message or "ArcGIS Enterprise returned an error.")

    if raise_on_error and isinstance(result, dict) and result.get("success") is False:
        message = str(result.get("message") or "ArcGIS Enterprise returned success=false.")
        details = result.get("details") or []
        if details:
            message = f"{message} Details: {'; '.join(str(detail) for detail in details)}"
        raise RuntimeError(message)

    if raise_on_error and isinstance(result, dict) and str(result.get("status") or "").lower() == "error":
        message = str(result.get("message") or "ArcGIS Enterprise returned status=error.")
        messages = result.get("messages") or []
        if messages:
            message = f"{message} Messages: {'; '.join(str(detail) for detail in messages)}"
        raise RuntimeError(message)

    return result


def _post_multipart_file(
    url: str,
    form: dict[str, Any],
    file_field: str,
    file_path: Path,
    token_env_var: str,
) -> dict[str, Any]:
    token = os.environ.get(token_env_var, "") if token_env_var else ""
    boundary = f"----SidwellEnterpriseWorking{_dt.datetime.now(_dt.timezone.utc).timestamp():.0f}"
    parts: list[bytes] = []
    payload = {"f": "json", **form}
    if token:
        payload["token"] = token

    for name, value in payload.items():
        parts.extend(
            [
                f"--{boundary}\r\n".encode("utf-8"),
                f'Content-Disposition: form-data; name="{name}"\r\n\r\n'.encode("utf-8"),
                f"{value}\r\n".encode("utf-8"),
            ]
        )

    parts.extend(
        [
            f"--{boundary}\r\n".encode("utf-8"),
            f'Content-Disposition: form-data; name="{file_field}"; filename="{file_path.name}"\r\n'.encode("utf-8"),
            b"Content-Type: application/octet-stream\r\n\r\n",
            file_path.read_bytes(),
            b"\r\n",
            f"--{boundary}--\r\n".encode("utf-8"),
        ]
    )
    request = Request(
        url,
        data=b"".join(parts),
        method="POST",
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
    )
    with urlopen(request, timeout=120) as response:
        result = json.loads(response.read().decode("utf-8"))

    if isinstance(result, dict) and result.get("error"):
        raise RuntimeError(_format_arcgis_error(result["error"]))
    if isinstance(result, dict) and result.get("success") is False:
        raise RuntimeError(str(result.get("message") or "ArcGIS Enterprise returned success=false."))
    if isinstance(result, dict) and str(result.get("status") or "").lower() == "error":
        messages = result.get("messages") or []
        message = "ArcGIS Enterprise returned status=error."
        if messages:
            message = f"{message} Messages: {'; '.join(str(detail) for detail in messages)}"
        raise RuntimeError(message)

    return result


def _provision_live_layers(
    args: argparse.Namespace,
    errors: list[str],
    warnings: list[str],
) -> dict[str, str]:
    token = os.environ.get(args.token_env_var, "") if args.token_env_var else ""
    if not token:
        errors.append(f"Live provisioning requires a portal token in {args.token_env_var}.")
        return {}

    if not args.portal_url.strip():
        errors.append("portal_url is required for live provisioning.")
        return {}

    try:
        return _provision_live_layers_rest(args)
    except Exception as exc:  # noqa: BLE001 - surface admin failure without leaking secrets.
        errors.append(f"Live provisioning failed: {type(exc).__name__}: {exc}")
        warnings.append("No generated layer URLs were written back because provisioning did not complete.")
        return {}


def _provision_live_layers_rest(args: argparse.Namespace) -> dict[str, str]:
    portal_url = args.portal_url.strip().rstrip("/")
    username = _read_portal_username(portal_url, args.token_env_var)
    service_url = _default_feature_service_url(args.service_root.rstrip("/"), args.target_service_name.strip())
    existing_metadata = _fetch_service_metadata_or_none(service_url, args.token_env_var)
    if existing_metadata is None:
        if _resolve_schema_template_path(args) is not None:
            service_url = _publish_schema_template(portal_url, username, args)
        else:
            service_url = _publish_working_feature_collection(portal_url, username, args)
    else:
        _assert_working_service_spatial_reference(existing_metadata)

    return _verified_layer_urls(service_url, args.token_env_var)


def _fetch_service_metadata_or_none(service_url: str, token_env_var: str) -> dict[str, Any] | None:
    try:
        metadata = _fetch_layer_metadata(service_url, token_env_var)
    except Exception:  # noqa: BLE001 - absence or inaccessible target is handled by createService.
        return None

    if isinstance(metadata.get("error"), dict):
        return None

    return metadata


def _create_working_service(portal_url: str, username: str, args: argparse.Namespace) -> str:
    create_parameters = {
        "name": args.target_service_name.strip(),
        "serviceDescription": "Sidwell Enterprise working review layers",
        "description": "Temporary transaction-scoped Enterprise working layers for parcel workflow review.",
        "hasStaticData": False,
        "maxRecordCount": 2000,
        "supportedQueryFormats": "JSON",
        "capabilities": "Query,Create,Update,Delete,Editing,Uploads",
        "allowGeometryUpdates": True,
        "spatialReference": {"wkid": 3448, "latestWkid": 3448},
        "initialExtent": _jamaica_working_extent(),
        "fullExtent": _jamaica_working_extent(),
    }
    create_parameters.update(_feature_service_definition(args))
    result = _post_form(
        f"{portal_url}/sharing/rest/content/users/{username}/createService",
        {
            "createParameters": json.dumps(create_parameters),
            "outputType": "featureService",
        },
        args.token_env_var,
    )
    return _read_service_url(result, args)


def _publish_working_feature_collection(portal_url: str, username: str, args: argparse.Namespace) -> str:
    item_title = f"{args.target_service_name.strip()} feature collection schema"
    add_result = _post_form(
        f"{portal_url}/sharing/rest/content/users/{username}/addItem",
        {
            "title": item_title,
            "type": "Feature Collection",
            "typeKeywords": "Feature Collection,Data,Feature Access",
            "tags": "sidwell,parcel-workflow,enterprise-working-review,schema-template",
            "text": json.dumps(_feature_collection_schema(args)),
            "extent": "550000,550000,900000,800000",
        },
        args.token_env_var,
    )
    item_id = str(add_result.get("id") or add_result.get("itemId") or "").strip()
    if not item_id:
        raise RuntimeError("Feature Collection schema item was not created.")

    service_item_id = ""
    try:
        publish_result = _post_form(
            f"{portal_url}/sharing/rest/content/users/{username}/publish",
            {
                "itemid": item_id,
                "fileType": "featureCollection",
                "outputType": "featureService",
                "publishParameters": json.dumps(
                    {
                        "name": args.target_service_name.strip(),
                        "serviceName": args.target_service_name.strip(),
                        "targetSR": _jad2001_spatial_reference(),
                        "sourceSR": _jad2001_spatial_reference(),
                        "spatialReference": _jad2001_spatial_reference(),
                        "extent": _jamaica_working_extent(),
                    }
                ),
            },
            args.token_env_var,
        )
        service_url = _read_published_service_url(publish_result, args, portal_url, username)
        _rename_published_service_item(portal_url, username, publish_result, args)
        services = publish_result.get("services")
        if isinstance(services, list):
            for service in services:
                if isinstance(service, dict):
                    service_item_id = str(service.get("serviceItemId") or service.get("itemId") or "")
                    job_id = str(service.get("jobId") or "")
                    if service_item_id and job_id:
                        _poll_publish_status(
                            f"{portal_url}/sharing/rest/content/users/{username}/items/{service_item_id}/status",
                            job_id,
                            args.token_env_var,
                        )
                        break

        return service_url
    finally:
        # The source Feature Collection is only a transient schema carrier.
        _post_form(
            f"{portal_url}/sharing/rest/content/users/{username}/items/{item_id}/delete",
            {},
            args.token_env_var,
            raise_on_error=False,
        )


def _publish_schema_template(portal_url: str, username: str, args: argparse.Namespace) -> str:
    template_path = _resolve_schema_template_path(args)
    if template_path is None:
        existing_service_url = _default_feature_service_url(args.service_root.rstrip("/"), args.target_service_name.strip())
        try:
            _verify_service_children_or_raise(existing_service_url, args.token_env_var)
            return existing_service_url
        except Exception as exc:  # noqa: BLE001 - convert empty/invalid existing target into actionable setup guidance.
            raise RuntimeError(
                "Schema-backed provisioning requires a File Geodatabase (.zip) or Service Definition (.sd) template. "
                f"Set --schema-template-path or {SCHEMA_TEMPLATE_ENV_VAR}. "
                f"If the target service already exists, delete or replace any invalid empty service first. Existing target check: {exc}"
            ) from exc

    item_type, file_type = _schema_template_item_types(template_path)
    item_title = f"{args.target_service_name.strip()} schema template"
    item_id = _find_schema_template_item_id(portal_url, username, item_title, item_type, args.token_env_var)
    if item_id:
        update_result = _post_multipart_file(
            f"{portal_url}/sharing/rest/content/users/{username}/items/{item_id}/update",
            {
                "title": item_title,
                "tags": "sidwell,parcel-workflow,enterprise-working-review,schema-template",
            },
            "file",
            template_path,
            args.token_env_var,
        )
        if update_result.get("success") is False:
            raise RuntimeError("Existing schema template item could not be updated.")
    else:
        add_item_url = f"{portal_url}/sharing/rest/content/users/{username}/addItem"
        add_result = _post_multipart_file(
            add_item_url,
            {
                "title": item_title,
                "type": item_type,
                "tags": "sidwell,parcel-workflow,enterprise-working-review,schema-template",
            },
            "file",
            template_path,
            args.token_env_var,
        )
        item_id = str(add_result.get("id") or add_result.get("itemId") or "").strip()
    if not item_id:
        raise RuntimeError("Schema template upload succeeded but no portal item id was returned.")

    publish_url = f"{portal_url}/sharing/rest/content/users/{username}/publish"
    publish_result = _post_form(
        publish_url,
        {
            "itemid": item_id,
            "fileType": file_type,
            "outputType": "featureService",
            "publishParameters": json.dumps(
                {
                    "name": args.target_service_name.strip(),
                    "serviceName": args.target_service_name.strip(),
                    "maxRecordCount": 2000,
                    "layerInfo": {"capabilities": "Query,Create,Update,Delete,Editing"},
                }
            ),
        },
        args.token_env_var,
    )
    service_url = _read_published_service_url(publish_result, args, portal_url, username)
    _rename_published_service_item(portal_url, username, publish_result, args)
    return service_url


def _resolve_schema_template_path(args: argparse.Namespace) -> Path | None:
    candidates = [
        getattr(args, "schema_template_path", ""),
        os.environ.get(SCHEMA_TEMPLATE_ENV_VAR, ""),
        str(Path(__file__).resolve().parent / "templates" / f"{args.schema_version}.zip"),
        str(Path(__file__).resolve().parent / "templates" / "enterprise_working_schema_template.zip"),
        str(Path(__file__).resolve().parent / "templates" / f"{args.schema_version}.sd"),
        str(Path(__file__).resolve().parent / "templates" / "enterprise_working_schema_template.sd"),
    ]
    for candidate in candidates:
        if not str(candidate).strip():
            continue

        path = Path(str(candidate).strip())
        if path.exists():
            return path

    return None


def _schema_template_item_types(path: Path) -> tuple[str, str]:
    suffix = path.suffix.lower()
    if suffix == ".zip":
        return "File Geodatabase", "fileGeodatabase"
    if suffix == ".sd":
        return "Service Definition", "serviceDefinition"

    raise RuntimeError(f"Unsupported schema template extension '{path.suffix}'. Use .zip or .sd.")


def _find_schema_template_item_id(
    portal_url: str,
    username: str,
    title: str,
    item_type: str,
    token_env_var: str,
) -> str:
    queries = [
        f'owner:{username} title:"{title}" type:"{item_type}"',
        f'owner:{username} "{title}"',
    ]
    for query in queries:
        result = _post_form(
            f"{portal_url}/sharing/rest/search",
            {"q": query, "num": 10, "sortField": "modified", "sortOrder": "desc"},
            token_env_var,
        )
        for item in result.get("results") or []:
            if not isinstance(item, dict):
                continue

            if str(item.get("type") or "").lower() != item_type.lower():
                continue

            item_title = str(item.get("title") or "")
            if item_title.lower() == title.lower():
                return str(item.get("id") or "")

    return ""


def _read_published_service_url(
    publish_result: dict[str, Any],
    args: argparse.Namespace,
    portal_url: str,
    username: str,
) -> str:
    service_url = (
        publish_result.get("serviceurl")
        or publish_result.get("serviceUrl")
        or publish_result.get("serviceURL")
        or publish_result.get("url")
    )
    if service_url:
        return str(service_url).rstrip("/")

    services = publish_result.get("services")
    if isinstance(services, list):
        for service in services:
            if isinstance(service, dict):
                if service.get("success") is False and service.get("error"):
                    error_message = _format_arcgis_error(service["error"])
                    if "already exists" in error_message.lower() and args.target_service_name.strip() in error_message:
                        return _default_feature_service_url(args.service_root.rstrip("/"), args.target_service_name.strip())

                    raise RuntimeError(error_message)

                service_url = service.get("serviceurl") or service.get("serviceUrl") or service.get("serviceURL") or service.get("url")
                if service_url:
                    return str(service_url).rstrip("/")

                service_item_id = service.get("serviceItemId") or service.get("itemId") or service.get("itemid") or service.get("id")
                job_id = service.get("jobId")
                if service_item_id and job_id:
                    status_url = f"{portal_url}/sharing/rest/content/users/{username}/items/{service_item_id}/status"
                    _poll_publish_status(status_url, str(job_id), args.token_env_var)
                if service_item_id:
                    return _read_portal_item_url(portal_url, str(service_item_id), args.token_env_var)

    service_item_id = publish_result.get("serviceItemId")
    job_id = publish_result.get("jobId")
    if service_item_id and job_id:
        status_url = str(publish_result.get("statusUrl") or f"{portal_url}/sharing/rest/content/users/{username}/items/{service_item_id}/status")
        _poll_publish_status(status_url, str(job_id), args.token_env_var)
    if service_item_id:
        return _read_portal_item_url(portal_url, str(service_item_id), args.token_env_var)

    item_id = publish_result.get("itemId") or publish_result.get("itemid") or publish_result.get("id")
    if item_id:
        return _read_portal_item_url(portal_url, str(item_id), args.token_env_var)

    raise RuntimeError("Schema template publish did not return a FeatureServer URL.")


def _rename_published_service_item(
    portal_url: str,
    username: str,
    publish_result: dict[str, Any],
    args: argparse.Namespace,
) -> None:
    item_id = _read_published_service_item_id(publish_result)
    if not item_id:
        return

    _post_form(
        f"{portal_url}/sharing/rest/content/users/{username}/items/{item_id}/update",
        {
            "title": args.target_service_name.strip(),
            "name": args.target_service_name.strip(),
        },
        args.token_env_var,
        raise_on_error=False,
    )


def _read_published_service_item_id(publish_result: dict[str, Any]) -> str:
    for key in ("serviceItemId", "serviceItemID"):
        value = str(publish_result.get(key) or "").strip()
        if value:
            return value

    services = publish_result.get("services")
    if isinstance(services, list):
        for service in services:
            if not isinstance(service, dict):
                continue

            for key in ("serviceItemId", "serviceItemID", "itemId", "itemid", "id"):
                value = str(service.get(key) or "").strip()
                if value:
                    return value

    return ""


def _poll_publish_status(status_url: str, job_id: str, token_env_var: str) -> dict[str, Any]:
    last_status: dict[str, Any] = {}
    for _ in range(30):
        last_status = _post_form(status_url, {"jobId": job_id}, token_env_var)
        status = str(last_status.get("status") or "").lower()
        if status in {"completed", "complete", "success", "succeeded"}:
            return last_status
        if status in {"failed", "failure", "error"}:
            messages = last_status.get("statusMessage") or last_status.get("messages") or []
            raise RuntimeError(f"Schema template publish job failed. {messages}")
        time.sleep(2)

    raise RuntimeError(f"Schema template publish job did not complete in time. Last status: {last_status.get('status')}")


def _read_portal_item_url(portal_url: str, item_id: str, token_env_var: str) -> str:
    item = _post_form(f"{portal_url.rstrip('/')}/sharing/rest/content/items/{item_id}", {}, token_env_var)
    item_url = item.get("url")
    if item_url:
        return str(item_url).rstrip("/")

    raise RuntimeError(f"Published service item {item_id} did not expose a FeatureServer URL.")


def _read_portal_username(portal_url: str, token_env_var: str) -> str:
    profile = _post_form(f"{portal_url}/sharing/rest/community/self", {}, token_env_var)
    username = str(profile.get("username") or "").strip()
    if not username:
        raise RuntimeError("Portal token was accepted but the current username could not be read.")

    return username


def _format_arcgis_error(error: Any) -> str:
    if not isinstance(error, dict):
        return str(error)

    message = str(error.get("message") or "ArcGIS Enterprise returned an error.")
    details = error.get("details") or []
    if details:
        message = f"{message} Details: {'; '.join(str(detail) for detail in details)}"
    return message


def _read_service_url(create_result: dict[str, Any], args: argparse.Namespace) -> str:
    service_url = (
        create_result.get("serviceurl")
        or create_result.get("serviceUrl")
        or create_result.get("serviceURL")
        or create_result.get("encodedServiceURL")
    )
    if service_url:
        return str(service_url).rstrip("/")

    return _default_feature_service_url(args.service_root.rstrip("/"), args.target_service_name.strip())


def _default_feature_service_url(service_root: str, service_name: str) -> str:
    root = service_root.rstrip("/")
    if root.lower().endswith("/services"):
        return f"{root}/Hosted/{service_name}/FeatureServer"

    return f"{root}/services/Hosted/{service_name}/FeatureServer"


def _service_has_expected_children(service_url: str, token_env_var: str) -> bool:
    metadata = _fetch_layer_metadata(service_url, token_env_var)
    return len(metadata.get("layers") or []) >= 4 and len(metadata.get("tables") or []) >= 1


def _assert_working_service_spatial_reference(metadata: dict[str, Any]) -> None:
    spatial_reference = metadata.get("spatialReference") or {}
    wkids = {
        str(spatial_reference.get("wkid") or ""),
        str(spatial_reference.get("latestWkid") or ""),
    }
    if "3448" in wkids:
        return

    raise RuntimeError(
        "Existing working_review Feature Service is not in JAD2001 / Jamaica Metric Grid (EPSG:3448). "
        "Delete the existing service and run Provision again so it can be recreated with the correct coordinate system."
    )


def _verify_service_children_or_raise(service_url: str, token_env_var: str) -> None:
    metadata = _fetch_layer_metadata(service_url, token_env_var)
    role_urls = _layer_urls_from_metadata(service_url, metadata)
    _validate_verified_children_fields(role_urls, token_env_var)


def _verified_layer_urls(service_url: str, token_env_var: str) -> dict[str, str]:
    metadata = _fetch_layer_metadata(service_url, token_env_var)
    role_urls = _layer_urls_from_metadata(service_url, metadata)
    _validate_verified_children_fields(role_urls, token_env_var)
    return role_urls


def _layer_urls_from_metadata(service_url: str, metadata: dict[str, Any]) -> dict[str, str]:
    service_url = service_url.rstrip("/")
    layers = metadata.get("layers") or []
    tables = metadata.get("tables") or []
    if not layers and not tables:
        raise RuntimeError(
            "Target service is an empty hosted Feature Service. Delete it and run Provision again so the working "
            "layers and tables can be created in the initial Enterprise createService request."
        )

    children = {}
    for child in [*layers, *tables]:
        if not isinstance(child, dict):
            continue

        name = str(child.get("name") or "").strip().lower()
        child_id = child.get("id")
        if child_id is None:
            continue

        children[name] = f"{service_url}/{child_id}"

    role_names = {
        "points": "working_points",
        "lines": "working_lines",
        "polygons": "working_polygons",
        "case_index": "working_case_index",
        "issues": "working_issues",
    }
    role_urls = {role: children.get(name, "") for role, name in role_names.items()}
    missing_required = [role for role in REQUIRED_LAYER_ROLES if not role_urls[role]]
    if not missing_required:
        return role_urls

    raise RuntimeError(
        "Target service does not expose the expected working children. "
        f"Missing roles: {', '.join(missing_required)}. Found layers={len(layers)}, tables={len(tables)}. "
        "Delete the partial working_review Feature Service and run Provision again so the schema template can recreate "
        "working_points, working_lines, working_polygons, working_issues, and working_case_index together."
    )


def _validate_verified_children_fields(role_urls: dict[str, str], token_env_var: str) -> None:
    errors: list[str] = []
    warnings: list[str] = []
    for role in REQUIRED_LAYER_ROLES:
        url = role_urls.get(role, "")
        if not url:
            continue

        metadata = _fetch_layer_metadata(url, token_env_var)
        _validate_capabilities(role, metadata, errors)
        _validate_geometry(role, metadata, errors, warnings)
        _validate_fields(role, metadata, errors, warnings)

    if errors:
        raise RuntimeError(" ".join(errors))


def _add_feature_service_definition(service_url: str, definition: dict[str, Any], token_env_var: str) -> None:
    errors: list[str] = []
    for add_definition_url in _add_to_definition_urls(service_url):
        try:
            _post_form(
                add_definition_url,
                {"addToDefinition": json.dumps(definition)},
                token_env_var,
            )
            return
        except Exception as exc:  # noqa: BLE001 - try the next supported Enterprise URL shape.
            errors.append(f"{add_definition_url}: {exc}")

    raise RuntimeError("Feature service was created, but layer/table definition could not be added. " + " | ".join(errors))


def _add_to_definition_urls(service_url: str) -> list[str]:
    service_url = service_url.rstrip("/")
    urls = [
        f"{service_url}/addToDefinition",
        f"{service_url}/admin/addToDefinition",
    ]
    marker = "/rest/services/"
    if marker in service_url:
        root, service_path = service_url.split(marker, 1)
        if service_path.endswith("/FeatureServer"):
            service_name = service_path[: -len("/FeatureServer")]
            urls.append(f"{root}/rest/admin/services/{service_name}.FeatureServer/addToDefinition")
            urls.append(f"{root}/admin/services/{service_name}.FeatureServer/addToDefinition")
            urls.append(f"{root}/rest/services/{service_name}/FeatureServer/admin/addToDefinition")

    return list(dict.fromkeys(urls))


def _feature_service_definition(args: argparse.Namespace) -> dict[str, Any]:
    return {
        "layers": [
            _layer_definition(0, "working_points", "esriGeometryPoint", _field_definitions([*SHARED_FIELDS, *POINT_FIELDS])),
            _layer_definition(1, "working_lines", "esriGeometryPolyline", _field_definitions([*SHARED_FIELDS, *LINE_FIELDS])),
            _layer_definition(2, "working_polygons", "esriGeometryPolygon", _field_definitions([*SHARED_FIELDS, *POLYGON_FIELDS])),
            _layer_definition(4, "working_issues", "esriGeometryPoint", _field_definitions([*SHARED_FIELDS, *ISSUE_FIELDS])),
        ],
        "tables": [
            {
                "id": 3,
                "name": "working_case_index",
                "type": "Table",
                "capabilities": "Query,Create,Update,Delete,Editing",
                "objectIdField": "OBJECTID",
                "fields": _field_definitions([*SHARED_FIELDS, *CASE_INDEX_FIELDS]),
            }
        ],
    }


def _feature_collection_schema(args: argparse.Namespace) -> dict[str, Any]:
    return {
        "layers": [
            _feature_collection_child(
                _layer_definition(0, "working_points", "esriGeometryPoint", _field_definitions([*SHARED_FIELDS, *POINT_FIELDS]))
            ),
            _feature_collection_child(
                _layer_definition(1, "working_lines", "esriGeometryPolyline", _field_definitions([*SHARED_FIELDS, *LINE_FIELDS]))
            ),
            _feature_collection_child(
                _layer_definition(2, "working_polygons", "esriGeometryPolygon", _field_definitions([*SHARED_FIELDS, *POLYGON_FIELDS]))
            ),
            _feature_collection_child(
                _layer_definition(4, "working_issues", "esriGeometryPoint", _field_definitions([*SHARED_FIELDS, *ISSUE_FIELDS]))
            ),
            _feature_collection_child(
                {
                    "id": 3,
                    "name": "working_case_index",
                    "type": "Table",
                    "capabilities": "Query,Create,Update,Delete,Editing",
                    "objectIdField": "OBJECTID",
                    "fields": _field_definitions([*SHARED_FIELDS, *CASE_INDEX_FIELDS]),
                }
            ),
        ],
        "spatialReference": _jad2001_spatial_reference(),
        "showLegend": True,
    }


def _feature_collection_child(layer_definition: dict[str, Any]) -> dict[str, Any]:
    geometry_type = layer_definition.get("geometryType")
    feature_set: dict[str, Any] = {"features": []}
    if geometry_type:
        feature_set["geometryType"] = geometry_type
        feature_set["spatialReference"] = _jad2001_spatial_reference()

    return {
        "id": layer_definition["id"],
        "layerDefinition": layer_definition,
        "featureSet": feature_set,
    }


def _jamaica_working_extent() -> dict[str, Any]:
    return {
        "xmin": 550000,
        "ymin": 550000,
        "xmax": 900000,
        "ymax": 800000,
        "spatialReference": _jad2001_spatial_reference(),
    }


def _jad2001_spatial_reference() -> dict[str, int]:
    return {"wkid": 3448, "latestWkid": 3448}


def _layer_definition(layer_id: int, name: str, geometry_type: str, fields: list[dict[str, Any]]) -> dict[str, Any]:
    drawing_tool = {
        "esriGeometryPoint": "esriFeatureEditToolPoint",
        "esriGeometryPolyline": "esriFeatureEditToolLine",
        "esriGeometryPolygon": "esriFeatureEditToolPolygon",
    }[geometry_type]
    definition = {
        "id": layer_id,
        "name": name,
        "type": "Feature Layer",
        "geometryType": geometry_type,
        "capabilities": "Query,Create,Update,Delete,Editing",
        "objectIdField": "OBJECTID",
        "hasZ": False,
        "hasM": False,
        "spatialReference": _jad2001_spatial_reference(),
        "extent": _jamaica_working_extent(),
        "fields": fields,
        "templates": [
            {
                "name": "New Feature",
                "description": "",
                "drawingTool": drawing_tool,
                "prototype": {"attributes": {}},
            }
        ],
    }
    if name == "working_lines":
        definition["drawingInfo"] = {"labelingInfo": _working_lines_labeling_info()}

    return definition


def _field_definitions(field_names: tuple[str, ...] | list[str]) -> list[dict[str, Any]]:
    definitions = [
        {"name": "OBJECTID", "type": "esriFieldTypeOID", "alias": "OBJECTID", "nullable": False, "editable": False},
    ]
    seen = {"objectid"}
    for name in field_names:
        if name.lower() in seen:
            continue

        seen.add(name.lower())
        field_type, length = FIELD_SPECS.get(name, ("esriFieldTypeString", 255))

        definition = {"name": name, "type": field_type, "alias": name, "nullable": True, "editable": True}
        if length is not None:
            definition["length"] = length
        if name == "review_decision":
            definition["domain"] = {
                "type": "codedValue",
                "name": "review_decision_domain",
                "codedValues": [{"name": value.title(), "code": value} for value in REVIEW_DECISION_VALUES],
            }
        definitions.append(definition)

    return definitions


def _cleanup_live_layers(
    args: argparse.Namespace,
    layers: dict[str, str],
    errors: list[str],
    warnings: list[str],
) -> dict[str, int]:
    token = os.environ.get(args.token_env_var, "") if args.token_env_var else ""
    if not token:
        errors.append(f"Live cleanup requires a portal token in {args.token_env_var}.")
        return {role: 0 for role in LAYER_ROLES}

    counts: dict[str, int] = {role: 0 for role in LAYER_ROLES}
    where = f"{args.cleanup_scope_field} = '{_escape_sql_literal(args.cleanup_scope_value)}'"
    for role, url in layers.items():
        if not url:
            continue

        try:
            if args.cleanup_mode == "delete":
                result = _post_form(f"{url.rstrip('/')}/deleteFeatures", {"where": where}, args.token_env_var)
                counts[role] = _count_successful_results(result.get("deleteResults", []))
            else:
                object_ids, object_id_field = _query_object_ids(url, where, args.token_env_var)
                if not object_ids:
                    continue

                features = [
                    {"attributes": {object_id_field: object_id, "is_active": 0, "case_status": "cleanup_removed"}}
                    for object_id in object_ids
                ]
                result = _post_form(
                    f"{url.rstrip('/')}/updateFeatures",
                    {"features": json.dumps(features)},
                    args.token_env_var,
                )
                counts[role] = _count_successful_results(result.get("updateResults", []))
        except Exception as exc:  # noqa: BLE001 - continue collecting role-specific diagnostics.
            errors.append(f"{role} cleanup failed: {exc}")

    if not errors and all(count == 0 for count in counts.values()):
        warnings.append("Cleanup completed but did not find matching rows for the requested scope.")

    return counts


def _query_object_ids(url: str, where: str, token_env_var: str) -> tuple[list[int], str]:
    result = _post_form(
        f"{url.rstrip('/')}/query",
        {"where": where, "returnIdsOnly": "true"},
        token_env_var,
    )
    object_ids = [int(value) for value in result.get("objectIds", [])]
    object_id_field = str(result.get("objectIdFieldName") or "OBJECTID")
    return object_ids, object_id_field


def _count_successful_results(results: Any) -> int:
    if not isinstance(results, list):
        return 0

    return sum(1 for result in results if isinstance(result, dict) and result.get("success") is True)


def _escape_sql_literal(value: str) -> str:
    return value.replace("'", "''")


def _summarize_layer_metadata(metadata: dict[str, Any]) -> dict[str, Any]:
    summary = {
        "type": metadata.get("type", ""),
        "geometryType": metadata.get("geometryType", ""),
        "capabilities": metadata.get("capabilities", ""),
        "field_names": [field.get("name", "") for field in metadata.get("fields", [])],
    }
    labeling_info = (metadata.get("drawingInfo") or {}).get("labelingInfo")
    if isinstance(labeling_info, list):
        summary["label_class_names"] = [
            str(label_class.get("name") or "")
            for label_class in labeling_info
            if isinstance(label_class, dict)
        ]

    return summary


def _validate_capabilities(role: str, metadata: dict[str, Any], errors: list[str]) -> None:
    capabilities = {capability.strip().lower() for capability in str(metadata.get("capabilities", "")).split(",")}
    if "query" not in capabilities:
        errors.append(f"{role} layer must support Query capability.")

    if role in REQUIRED_LAYER_ROLES and not ({"editing", "create", "update", "delete"} & capabilities):
        errors.append(f"{role} layer must expose edit capability for working review updates.")


def _validate_geometry(role: str, metadata: dict[str, Any], errors: list[str], warnings: list[str]) -> None:
    expected = {
        "points": "esriGeometryPoint",
        "lines": "esriGeometryPolyline",
        "polygons": "esriGeometryPolygon",
    }.get(role)
    geometry_type = metadata.get("geometryType", "")
    if expected and geometry_type != expected:
        errors.append(f"{role} layer geometry must be {expected}; found {geometry_type or 'none'}.")
    elif role == "issues" and geometry_type not in {"", "esriGeometryPoint", "esriGeometryPolygon"}:
        warnings.append(f"issues role has unexpected geometry type {geometry_type}.")


def _validate_fields(role: str, metadata: dict[str, Any], errors: list[str], warnings: list[str] | None = None) -> None:
    warnings = warnings if warnings is not None else []
    fields = [field for field in metadata.get("fields", []) if isinstance(field, dict)]
    field_names = {str(field.get("name", "")).lower() for field in fields}
    required_fields = {"transaction_number", *DISPOSITION_FIELDS} if role == "case_index" else set(SHARED_FIELDS)
    if role == "case_index":
        required_fields.update({"spatial_unit_id", "spatial_unit_api_status"})
    if role == "polygons":
        required_fields.add("SUID")
    missing = sorted(field for field in required_fields if field.lower() not in field_names)
    if missing:
        message = (
            f"{role} layer schema upgrade required: missing required fields for Compute disposition support: "
            f"{', '.join(missing)}."
        )
        if role == "issues":
            warnings.append(message)
        else:
            errors.append(message)

    if role == "lines":
        missing_label_fields = sorted(field for field in WORKING_LINES_LABEL_FIELDS if field.lower() not in field_names)
        if missing_label_fields:
            errors.append(
                "lines layer schema upgrade required for default visualization labeling: missing "
                f"{', '.join(missing_label_fields)}."
            )

    if role != "issues":
        _validate_review_decision_domain(role, fields, errors)
        _validate_review_comment_capacity(role, fields, errors)


def _validate_review_decision_domain(role: str, fields: list[dict[str, Any]], errors: list[str]) -> None:
    review_decision_field = _find_field(fields, "review_decision")
    if not review_decision_field:
        return

    domain = review_decision_field.get("domain")
    if not isinstance(domain, dict):
        return

    coded_values = domain.get("codedValues") if isinstance(domain, dict) else None
    values = {
        str(value.get("code") or "").lower()
        for value in coded_values
        if isinstance(value, dict)
    } if isinstance(coded_values, list) else set()
    missing = [value for value in REVIEW_DECISION_VALUES if value not in values]
    if missing:
        errors.append(
            f"{role} layer schema upgrade required: review_decision must support values "
            f"{', '.join(REVIEW_DECISION_VALUES)}."
        )


def _validate_review_comment_capacity(role: str, fields: list[dict[str, Any]], errors: list[str]) -> None:
    review_comment_field = _find_field(fields, "review_comment")
    if not review_comment_field:
        return

    length = review_comment_field.get("length")
    if isinstance(length, int) and length >= 1024:
        return

    errors.append(
        f"{role} layer schema upgrade required: review_comment must allow at least 1024 characters "
        "for rejected/postponed reason text."
    )


def _find_field(fields: list[dict[str, Any]], name: str) -> dict[str, Any] | None:
    for field in fields:
        if str(field.get("name") or "").lower() == name.lower():
            return field

    return None


def _status_for_mode(mode: str, dry_run: bool) -> str:
    if dry_run:
        return {
            "validate": "validated",
            "provision": "provision_ready",
            "cleanup": "cleanup_ready",
        }[mode]

    return {
        "validate": "validated",
        "provision": "provisioned",
        "cleanup": "cleanup_completed",
    }[mode]

def _messages_for_mode(mode: str, dry_run: bool, errors: list[str]) -> list[str]:
    if errors:
        return ["Admin request failed validation. No Enterprise changes were made."]

    if mode == "cleanup":
        return ["Cleanup request is scoped and ready for admin execution."] if dry_run else ["Cleanup completed for the requested scope."]

    if mode == "provision":
        return ["Provisioning request is ready. Dry-run output includes generated layer URLs."] if dry_run else ["Enterprise working layers were provisioned."]

    return ["Enterprise working layer settings validated."]


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Provision or maintain Enterprise generic working layers.")
    parser.add_argument("--mode", choices=("validate", "provision", "cleanup"), required=True)
    parser.add_argument("--portal-url", default="")
    parser.add_argument("--service-root", default="")
    parser.add_argument("--workspace-name", default="sidwell_working_review")
    parser.add_argument("--target-folder", default="Sidwell Working Review")
    parser.add_argument("--target-service-name", default="sidwell_working_review")
    parser.add_argument("--schema-version", default="sidwell_enterprise_working_v1")
    parser.add_argument("--schema-template-path", default="")
    parser.add_argument("--points-layer", default="")
    parser.add_argument("--lines-layer", default="")
    parser.add_argument("--polygons-layer", default="")
    parser.add_argument("--case-index-layer", default="")
    parser.add_argument("--issues-layer", default="")
    parser.add_argument("--cleanup-scope-field", default="transaction_number")
    parser.add_argument("--cleanup-scope-value", default="")
    parser.add_argument("--cleanup-mode", choices=("deactivate", "delete"), default="deactivate")
    parser.add_argument("--require-cleanup-scope", action="store_true", default=False)
    parser.add_argument("--token-env-var", default="ARCGIS_PORTAL_TOKEN")
    parser.add_argument("--dry-run", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--output-json", default="")
    parser.add_argument("--audit-json", default="")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    payload = build_payload(args)
    write_payload(payload, args.output_json)
    write_cleanup_audit(payload, args.audit_json)
    return 1 if payload["status"] == "failed" else 0


if __name__ == "__main__":
    raise SystemExit(main())
