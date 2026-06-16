import argparse
import datetime as dt
import json
import math
import os
import shutil
import sys
from pathlib import Path
from typing import Any

REVIEW_WORKSPACE_MODE_NORMAL = "normal"
REVIEW_WORKSPACE_MODE_PARCEL_FABRIC = "parcel_fabric"
PARCEL_FABRIC_MODE_PILOT = "pilot"
PARCEL_FABRIC_MODE_TRUE = "true"
PARCEL_FABRIC_DATASET_NAME = "parcel_fabric_dataset"
PARCEL_FABRIC_NAME = "local_parcel_fabric"
PARCEL_FABRIC_PARCEL_TYPE_NAME = "compute_review"
PARCEL_FABRIC_RECORD_PREFIX = "sidwell-record"


def run(input_json_path, output_json_path):
    raise NotImplementedError("Output adapter is implemented through its CLI entrypoint.")


def _utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def _read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def _load_arcpy():
    try:
        import arcpy  # type: ignore

        return arcpy
    except Exception:
        return None


def _review_rows(review_data: dict[str, Any]) -> list[dict[str, Any]]:
    rows = review_data.get("rows")
    return rows if isinstance(rows, list) else []


def _parse_coordinate(value: Any) -> float | None:
    if value is None:
        return None

    text = str(value).strip().replace(",", "")
    if not text:
        return None

    try:
        return float(text)
    except ValueError:
        return None


def _normalize_text(value: Any, limit: int) -> str:
    text = "" if value is None else str(value).strip()
    text = " ".join(text.split())
    if len(text) <= limit:
        return text

    if limit <= 3:
        return text[:limit]

    return text[: limit - 3] + "..."


def _normalize_review_workspace_mode(value: Any) -> str:
    text = "" if value is None else str(value).strip().replace(" ", "_").lower()
    if text in {REVIEW_WORKSPACE_MODE_PARCEL_FABRIC, "parcel-fabric", "parcelfabric"}:
        return REVIEW_WORKSPACE_MODE_PARCEL_FABRIC

    return REVIEW_WORKSPACE_MODE_NORMAL


def _normalize_points(review_data: dict[str, Any]) -> list[dict[str, Any]]:
    normalized: list[dict[str, Any]] = []
    for index, row in enumerate(_review_rows(review_data), start=1):
        if bool(row.get("review_unresolved")):
            continue

        point_id = (
            row.get("review_point_identifier")
            or row.get("point_identifier")
            or row.get("point_id")
            or row.get("point_no")
            or row.get("point_number")
            or f"P-{index:03d}"
        )
        easting = _parse_coordinate(row.get("review_easting") or row.get("easting"))
        northing = _parse_coordinate(row.get("review_northing") or row.get("northing"))
        if easting is None or northing is None:
            continue

        normalized.append(
            {
                "row_id": _normalize_text(row.get("row_id") or f"row-{index:03d}", 64),
                "parcel_group_id": _normalize_text(
                    row.get("review_parcel_group_id") or row.get("parcel_group_id") or "",
                    64,
                ),
                "traverse_id": _normalize_text(
                    row.get("review_traverse_id") or row.get("traverse_id") or "",
                    64,
                ),
                "sequence_in_group": _parse_int(row.get("review_sequence_in_group") or row.get("sequence_in_group")),
                "is_boundary_break": _parse_bool(row.get("review_is_boundary_break") if row.get("review_is_boundary_break") is not None else row.get("is_boundary_break")),
                "group_confidence": _normalize_text(
                    row.get("review_group_confidence") or row.get("group_confidence") or "",
                    32,
                ),
                "point_identifier": _normalize_text(point_id, 64),
                "easting": easting,
                "northing": northing,
                "length": "" if row.get("review_length") is None else _normalize_text(row.get("review_length") or row.get("length") or "", 128),
                "status": _normalize_text(row.get("review_extraction_status") or row.get("status") or "", 64),
                "source_evidence": _normalize_text(row.get("review_source_evidence") or row.get("source_evidence") or "", 1024),
            }
        )

    return normalized


def _parse_int(value: Any) -> int | None:
    if value is None:
        return None

    try:
        return int(str(value).strip())
    except (TypeError, ValueError):
        return None


def _parse_bool(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    if value is None:
        return False
    return str(value).strip().lower() in {"1", "true", "yes", "y"}


def _normalized_group_key(point: dict[str, Any], fallback_index: int) -> str:
    parcel_group_id = str(point.get("parcel_group_id") or "").strip()
    traverse_id = str(point.get("traverse_id") or "").strip()
    if parcel_group_id:
        return parcel_group_id
    if traverse_id:
        return f"traverse:{traverse_id}"
    return f"parcel-{fallback_index}"


def _grouped_point_sequences(points: list[dict[str, Any]]) -> list[dict[str, Any]]:
    if not points:
        return []

    groups: list[dict[str, Any]] = []
    current_points: list[dict[str, Any]] = []
    current_group_key: str | None = None
    implied_group_index = 1

    for point in points:
        explicit_group_key = str(point.get("parcel_group_id") or point.get("traverse_id") or "").strip() or None
        boundary_break = bool(point.get("is_boundary_break"))

        if current_points and (boundary_break or (explicit_group_key and explicit_group_key != current_group_key)):
            groups.append({"group_id": current_group_key or _normalized_group_key(current_points[0], implied_group_index), "points": current_points})
            implied_group_index += 1
            current_points = []
            current_group_key = None

        if not current_points:
            current_group_key = explicit_group_key or _normalized_group_key(point, implied_group_index)

        current_points.append(point)

    if current_points:
        groups.append({"group_id": current_group_key or _normalized_group_key(current_points[0], implied_group_index), "points": current_points})

    return groups


def _polyline_segments(point_groups: list[dict[str, Any]]) -> list[dict[str, Any]]:
    segments: list[dict[str, Any]] = []
    segment_index = 1
    for group in point_groups:
        group_id = group.get("group_id") or ""
        points = group.get("points") or []
        for index in range(len(points) - 1):
            start = points[index]
            end = points[index + 1]
            segments.append(
                {
                    "segment_index": segment_index,
                    "parcel_group_id": group_id,
                    "start_point": start["point_identifier"],
                    "end_point": end["point_identifier"],
                    "start": (start["easting"], start["northing"]),
                    "end": (end["easting"], end["northing"]),
                    "length": end.get("length") or "",
                }
            )
            segment_index += 1
    return segments


def _polygon_points(points: list[dict[str, Any]]) -> list[tuple[float, float]]:
    if len(points) < 3:
        return []

    coords = [(float(point["easting"]), float(point["northing"])) for point in points]
    cleaned = _dedupe_consecutive_points(coords)
    if len(cleaned) < 3:
        return []

    if cleaned[0] != cleaned[-1]:
        cleaned.append(cleaned[0])

    unique_vertices = {coord for coord in cleaned[:-1]}
    if len(unique_vertices) < 3:
        return []

    if math.isclose(abs(_ring_area(cleaned)), 0.0, abs_tol=1e-9):
        return []

    return cleaned


def _polygon_rings(point_groups: list[dict[str, Any]]) -> list[dict[str, Any]]:
    polygons: list[dict[str, Any]] = []
    for index, group in enumerate(point_groups, start=1):
        group_points = group.get("points") or []
        coords = _polygon_points(group_points)
        if not coords:
            continue
        polygons.append(
            {
                "polygon_index": index,
                "parcel_group_id": group.get("group_id") or f"parcel-{index}",
                "coordinates": coords,
            }
        )
    return polygons


def _dedupe_consecutive_points(coords: list[tuple[float, float]]) -> list[tuple[float, float]]:
    deduped: list[tuple[float, float]] = []
    for coord in coords:
        if not deduped or deduped[-1] != coord:
            deduped.append(coord)
    return deduped


def _ring_area(coords: list[tuple[float, float]]) -> float:
    if len(coords) < 4:
        return 0.0

    area = 0.0
    for index in range(len(coords) - 1):
        x1, y1 = coords[index]
        x2, y2 = coords[index + 1]
        area += (x1 * y2) - (x2 * y1)
    return area / 2.0


def _build_geojson(points: list[dict[str, Any]], segments: list[dict[str, Any]], polygons: list[dict[str, Any]]) -> dict[str, Any]:
    features: list[dict[str, Any]] = []

    for point in points:
        features.append(
            {
                "type": "Feature",
                "geometry": {"type": "Point", "coordinates": [point["easting"], point["northing"]]},
                "properties": {
                    "row_id": point["row_id"],
                    "parcel_group_id": point.get("parcel_group_id") or "",
                    "traverse_id": point.get("traverse_id") or "",
                    "sequence_in_group": point.get("sequence_in_group"),
                    "is_boundary_break": point.get("is_boundary_break") or False,
                    "group_confidence": point.get("group_confidence") or "",
                    "point_identifier": point["point_identifier"],
                    "status": point["status"],
                    "length": point["length"],
                    "source_evidence": point["source_evidence"],
                },
            }
        )

    for segment in segments:
        features.append(
            {
                "type": "Feature",
                "geometry": {"type": "LineString", "coordinates": [list(segment["start"]), list(segment["end"])]},
                "properties": {
                    "segment_index": segment["segment_index"],
                    "parcel_group_id": segment.get("parcel_group_id") or "",
                    "start_point": segment["start_point"],
                    "end_point": segment["end_point"],
                    "length": segment["length"],
                },
            }
        )

    for polygon in polygons:
        features.append(
            {
                "type": "Feature",
                "geometry": {"type": "Polygon", "coordinates": [[list(coord) for coord in polygon["coordinates"]]]},
                "properties": {
                    "name": f"parcel_polygon_{polygon['polygon_index']}",
                    "parcel_group_id": polygon.get("parcel_group_id") or "",
                },
            }
        )

    return {"type": "FeatureCollection", "features": features}


def _ensure_empty(path: Path) -> None:
    if path.is_dir():
        shutil.rmtree(path, ignore_errors=True)
    elif path.exists():
        path.unlink()


def _copy_template_gdb(template_gdb: Path, target_gdb: Path) -> None:
    if target_gdb.exists():
        shutil.rmtree(target_gdb, ignore_errors=True)
    shutil.copytree(template_gdb, target_gdb)


def _coerce_path(value: Any) -> str | None:
    if value is None:
        return None

    if isinstance(value, (list, tuple)):
        for item in value:
            text = _coerce_path(item)
            if text:
                return text
        return None

    text = str(value).strip()
    return text or None


def _existing_feature_classes(arcpy, dataset_path: Path) -> dict[str, list[str]]:
    previous_workspace = arcpy.env.workspace
    try:
        arcpy.env.workspace = str(dataset_path)
        feature_classes = arcpy.ListFeatureClasses() or []
        classified: dict[str, list[str]] = {"POINT": [], "POLYLINE": [], "POLYGON": []}
        for feature_class in feature_classes:
            try:
                shape_type = str(arcpy.Describe(feature_class).shapeType or "").upper()
            except Exception:
                continue
            if shape_type in classified:
                classified[shape_type].append(feature_class)
        return classified
    finally:
        arcpy.env.workspace = previous_workspace


def _feature_class_delta(before: dict[str, list[str]], after: dict[str, list[str]], shape_type: str) -> list[str]:
    previous = set(before.get(shape_type, []))
    return [name for name in after.get(shape_type, []) if name not in previous]


def _first_matching_field(arcpy, dataset: str, candidates: list[str]) -> str | None:
    candidate_lookup = {candidate.lower(): candidate for candidate in candidates}
    for field in arcpy.ListFields(dataset):
        key = field.name.lower()
        if key in candidate_lookup:
            return field.name
    return None


def _record_name(transaction_number: str) -> str:
    return f"{PARCEL_FABRIC_RECORD_PREFIX}-{transaction_number}"


def _arcade_string_literal(value: str) -> str:
    escaped = (value or "").replace("\\", "\\\\").replace("'", "\\'")
    return f"'{escaped}'"


def _append_features(arcpy, source: str, target: str) -> None:
    arcpy.management.Append([source], target, "NO_TEST")


def _count_rows(arcpy, dataset_path: str | None) -> int:
    if not dataset_path:
        return 0

    try:
        return int(arcpy.management.GetCount(dataset_path)[0])
    except Exception:
        return 0


def _create_true_parcel_fabric_with_arcpy(
    arcpy,
    target_gdb: Path,
    root_paths: dict[str, str | None],
    transaction_number: str,
) -> tuple[dict[str, str | None], dict[str, Any]]:
    if not root_paths.get("polygon_fc"):
        raise RuntimeError("Parcel Fabric mode requires a polygon candidate generated from approved review data.")

    fabric_dataset = target_gdb / PARCEL_FABRIC_DATASET_NAME
    if arcpy.Exists(str(fabric_dataset)):
        arcpy.management.Delete(str(fabric_dataset))

    polygon_description = arcpy.Describe(root_paths["polygon_fc"])
    spatial_reference = getattr(polygon_description, "spatialReference", None)
    if spatial_reference is None:
        raise RuntimeError("Could not determine spatial reference for Parcel Fabric output generation.")

    print(f"Parcel fabric step: creating feature dataset '{fabric_dataset.name}'.")
    arcpy.management.CreateFeatureDataset(str(target_gdb), fabric_dataset.name, spatial_reference)

    print(f"Parcel fabric step: creating parcel fabric '{PARCEL_FABRIC_NAME}'.")
    fabric_path = _coerce_path(arcpy.parcel.CreateParcelFabric(str(fabric_dataset), PARCEL_FABRIC_NAME))
    if not fabric_path:
        fabric_path = str(fabric_dataset / PARCEL_FABRIC_NAME)

    if not arcpy.Exists(fabric_path):
        raise RuntimeError("CreateParcelFabric completed, but the parcel fabric dataset could not be resolved.")

    before_types = _existing_feature_classes(arcpy, fabric_dataset)
    print(f"Parcel fabric step: adding parcel type '{PARCEL_FABRIC_PARCEL_TYPE_NAME}'.")
    arcpy.parcel.AddParcelType(
        fabric_path,
        PARCEL_FABRIC_PARCEL_TYPE_NAME,
        "TOPOLOGY_POLYGON",
        "NOT_STRATA_PARCELS",
    )
    after_types = _existing_feature_classes(arcpy, fabric_dataset)

    polygon_type_names = _feature_class_delta(before_types, after_types, "POLYGON")
    line_type_names = _feature_class_delta(before_types, after_types, "POLYLINE")
    if not polygon_type_names:
        raise RuntimeError("AddParcelType completed, but no parcel type polygon feature class was found.")

    parcel_polygon_fc = str(fabric_dataset / polygon_type_names[0])
    parcel_line_fc = str(fabric_dataset / line_type_names[0]) if line_type_names else None

    print("Parcel fabric step: copying approved polygon into parcel type polygons.")
    _append_features(arcpy, root_paths["polygon_fc"], parcel_polygon_fc)

    record_name = _record_name(transaction_number)
    record_expression = _arcade_string_literal(record_name)
    print(f"Parcel fabric step: creating parcel record '{record_name}'.")
    arcpy.parcel.CreateParcelRecords(
        parcel_polygon_fc,
        None,
        record_expression,
        "EXPRESSION",
    )

    print(f"Parcel fabric step: building parcel fabric for record '{record_name}'.")
    arcpy.parcel.BuildParcelFabric(fabric_path, None, record_name)

    parcel_points_fc = None
    point_feature_classes = _existing_feature_classes(arcpy, fabric_dataset).get("POINT", [])
    if point_feature_classes:
        parcel_points_fc = str(fabric_dataset / point_feature_classes[0])

    if root_paths.get("point_fc"):
        print("Parcel fabric step: importing approved points into parcel fabric points.")
        arcpy.parcel.ImportParcelFabricPoints(
            root_paths["point_fc"],
            fabric_path,
            "PROXIMITY",
            "1 Meters",
            "ALL",
            record_name,
            None,
            None,
            "UPDATE_AND_CREATE",
            parcel_points_fc,
            None,
        )

    if not parcel_points_fc:
        point_feature_classes = _existing_feature_classes(arcpy, fabric_dataset).get("POINT", [])
        if point_feature_classes:
            parcel_points_fc = str(fabric_dataset / point_feature_classes[0])

    print("Parcel fabric step: validating parcel fabric.")
    arcpy.parcel.ValidateParcelFabric(fabric_path, None)

    return (
        {
            "review_dataset": str(fabric_dataset),
            "review_layer": fabric_path,
            "review_point_fc": parcel_points_fc,
            "review_line_fc": parcel_line_fc,
            "review_polygon_fc": parcel_polygon_fc,
        },
        {
            "parcel_fabric_mode": PARCEL_FABRIC_MODE_TRUE,
            "parcel_fabric_dataset_path": str(fabric_dataset),
            "parcel_fabric_layer_path": fabric_path,
            "parcel_record_name": record_name,
            "parcel_record_id": None,
            "parcel_type": PARCEL_FABRIC_PARCEL_TYPE_NAME,
            "built_parcel_count": _count_rows(arcpy, parcel_polygon_fc),
            "built_line_count": _count_rows(arcpy, parcel_line_fc),
            "built_point_count": _count_rows(arcpy, parcel_points_fc),
        },
    )


def _create_outputs_with_arcpy(
    arcpy,
    target_gdb: Path,
    template_gdb: Path | None,
    points: list[dict[str, Any]],
    segments: list[dict[str, Any]],
    polygons: list[dict[str, Any]],
    review_workspace_mode: str,
    transaction_number: str,
) -> tuple[dict[str, str | None], dict[str, str | None], list[str]]:
    output_dir = target_gdb.parent
    output_dir.mkdir(parents=True, exist_ok=True)
    warnings: list[str] = []

    if template_gdb is not None and template_gdb.exists() and template_gdb.suffix.lower() == ".gdb":
        _copy_template_gdb(template_gdb, target_gdb)
    else:
        if target_gdb.exists():
            shutil.rmtree(target_gdb, ignore_errors=True)
        arcpy.management.CreateFileGDB(str(output_dir), target_gdb.name)

    spatial_reference = arcpy.SpatialReference(3448)
    point_fc = target_gdb / "parcel_points"
    line_fc = target_gdb / "parcel_lines"
    polygon_fc = target_gdb / "parcel_polygon"

    for dataset_path in (point_fc, line_fc, polygon_fc):
        if arcpy.Exists(str(dataset_path)):
            arcpy.management.Delete(str(dataset_path))

    arcpy.management.CreateFeatureclass(str(target_gdb), point_fc.name, "POINT", spatial_reference=spatial_reference)
    arcpy.management.AddField(str(point_fc), "point_id", "TEXT", field_length=64)
    arcpy.management.AddField(str(point_fc), "parcel_grp", "TEXT", field_length=64)
    arcpy.management.AddField(str(point_fc), "status_txt", "TEXT", field_length=64)
    arcpy.management.AddField(str(point_fc), "length_txt", "TEXT", field_length=64)
    arcpy.management.AddField(str(point_fc), "source_txt", "TEXT", field_length=1024)
    arcpy.management.AddField(str(point_fc), "row_id", "TEXT", field_length=64)

    with arcpy.da.InsertCursor(str(point_fc), ["SHAPE@XY", "point_id", "parcel_grp", "status_txt", "length_txt", "source_txt", "row_id"]) as cursor:
        for point in points:
            cursor.insertRow(
                [
                    (point["easting"], point["northing"]),
                    _normalize_text(point["point_identifier"], 64),
                    _normalize_text(point.get("parcel_group_id") or point.get("traverse_id") or "", 64),
                    _normalize_text(point["status"], 64),
                    _normalize_text(point["length"], 128),
                    _normalize_text(point["source_evidence"], 1024),
                    _normalize_text(point["row_id"], 64),
                ]
            )

    created_line_fc: str | None = None
    created_polygon_fc: str | None = None

    if segments:
        arcpy.management.CreateFeatureclass(str(target_gdb), line_fc.name, "POLYLINE", spatial_reference=spatial_reference)
        arcpy.management.AddField(str(line_fc), "start_pt", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "end_pt", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "parcel_grp", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "length_txt", "TEXT", field_length=128)
        arcpy.management.AddField(str(line_fc), "seg_index", "LONG")

        with arcpy.da.InsertCursor(str(line_fc), ["SHAPE@", "start_pt", "end_pt", "parcel_grp", "length_txt", "seg_index"]) as cursor:
            for segment in segments:
                array = arcpy.Array([arcpy.Point(*segment["start"]), arcpy.Point(*segment["end"])])
                cursor.insertRow(
                    [
                        arcpy.Polyline(array, spatial_reference),
                        _normalize_text(segment["start_point"], 64),
                        _normalize_text(segment["end_point"], 64),
                        _normalize_text(segment.get("parcel_group_id") or "", 64),
                        _normalize_text(segment["length"], 128),
                        segment["segment_index"],
                    ]
                )
        created_line_fc = str(line_fc)

    if polygons:
        try:
            arcpy.management.CreateFeatureclass(str(target_gdb), polygon_fc.name, "POLYGON", spatial_reference=spatial_reference)
            arcpy.management.AddField(str(polygon_fc), "name", "TEXT", field_length=64)
            arcpy.management.AddField(str(polygon_fc), "parcel_grp", "TEXT", field_length=64)

            with arcpy.da.InsertCursor(str(polygon_fc), ["SHAPE@", "name", "parcel_grp"]) as cursor:
                for polygon in polygons:
                    array = arcpy.Array([arcpy.Point(*coord) for coord in polygon["coordinates"]])
                    polygon_geometry = arcpy.Polygon(array, spatial_reference)
                    if getattr(polygon_geometry, "isEmpty", False):
                        continue
                    cursor.insertRow(
                        [
                            polygon_geometry,
                            _normalize_text(f"parcel_polygon_{polygon['polygon_index']}", 64),
                            _normalize_text(polygon.get("parcel_group_id") or "", 64),
                        ]
                    )

            if _count_rows(arcpy, str(polygon_fc)) <= 0:
                raise RuntimeError("ArcPy did not create any valid polygon features from grouped review geometry.")
            created_polygon_fc = str(polygon_fc)
        except Exception as exc:
            warnings.append(f"polygon_generation_skipped: {exc}")
            if arcpy.Exists(str(polygon_fc)):
                arcpy.management.Delete(str(polygon_fc))

    root_paths = {
        "point_fc": str(point_fc),
        "line_fc": created_line_fc,
        "polygon_fc": created_polygon_fc,
    }
    review_paths = {
        "review_dataset": None,
        "review_layer": None,
        "review_point_fc": None,
        "review_line_fc": None,
        "review_polygon_fc": None,
    }
    review_metadata: dict[str, Any] = {
        "parcel_fabric_mode": None,
        "parcel_fabric_dataset_path": None,
        "parcel_fabric_layer_path": None,
        "parcel_record_name": None,
        "parcel_record_id": None,
        "parcel_type": None,
        "built_parcel_count": 0,
        "built_line_count": 0,
        "built_point_count": 0,
    }

    if review_workspace_mode == REVIEW_WORKSPACE_MODE_PARCEL_FABRIC:
        review_paths, review_metadata = _create_true_parcel_fabric_with_arcpy(
            arcpy,
            target_gdb,
            root_paths,
            transaction_number,
        )

    return (root_paths, review_paths | review_metadata, warnings)


def _create_outputs_filesystem_fallback(
    target_gdb: Path,
    points: list[dict[str, Any]],
    segments: list[dict[str, Any]],
    polygons: list[dict[str, Any]],
    review_workspace_mode: str,
    transaction_number: str,
) -> tuple[dict[str, str | None], dict[str, str | None], list[str]]:
    target_gdb.mkdir(parents=True, exist_ok=True)
    (target_gdb / "_sidwell_test_mode.txt").write_text("filesystem fallback", encoding="utf-8")

    point_fc = target_gdb / "parcel_points"
    line_fc = target_gdb / "parcel_lines"
    polygon_fc = target_gdb / "parcel_polygon"

    point_fc.write_text(json.dumps(points, indent=2), encoding="utf-8")
    if segments:
        line_fc.write_text(json.dumps(segments, indent=2), encoding="utf-8")
    if polygons:
        polygon_fc.write_text(json.dumps(polygons, indent=2), encoding="utf-8")

    root_paths = {
        "point_fc": str(point_fc),
        "line_fc": str(line_fc) if segments else None,
        "polygon_fc": str(polygon_fc) if polygons else None,
    }
    warnings: list[str] = []
    review_paths = {
        "review_dataset": None,
        "review_layer": None,
        "review_point_fc": None,
        "review_line_fc": None,
        "review_polygon_fc": None,
        "parcel_fabric_mode": None,
        "parcel_fabric_dataset_path": None,
        "parcel_fabric_layer_path": None,
        "parcel_record_name": None,
        "parcel_record_id": None,
        "parcel_type": None,
        "built_parcel_count": 0,
        "built_line_count": 0,
        "built_point_count": 0,
    }

    if review_workspace_mode == REVIEW_WORKSPACE_MODE_PARCEL_FABRIC:
        review_dataset = target_gdb / PARCEL_FABRIC_DATASET_NAME
        fabric_layer = review_dataset / PARCEL_FABRIC_NAME
        parcel_type_dir = fabric_layer / PARCEL_FABRIC_PARCEL_TYPE_NAME
        review_dataset.mkdir(parents=True, exist_ok=True)
        parcel_type_dir.mkdir(parents=True, exist_ok=True)

        review_point_fc = parcel_type_dir / "points.json"
        review_line_fc = parcel_type_dir / "lines.json"
        review_polygon_fc = parcel_type_dir / "polygons.json"

        if root_paths.get("point_fc"):
            shutil.copyfile(root_paths["point_fc"], review_point_fc)
        if root_paths.get("line_fc"):
            shutil.copyfile(root_paths["line_fc"], review_line_fc)
        if root_paths.get("polygon_fc"):
            shutil.copyfile(root_paths["polygon_fc"], review_polygon_fc)

        (fabric_layer / "records.json").write_text(
            json.dumps(
                {
                    "record_name": _record_name(transaction_number),
                    "parcel_type": PARCEL_FABRIC_PARCEL_TYPE_NAME,
                },
                indent=2,
            ),
            encoding="utf-8",
        )

        review_paths.update(
            {
                "review_dataset": str(review_dataset),
                "review_layer": str(fabric_layer),
                "review_point_fc": str(review_point_fc) if root_paths.get("point_fc") else None,
                "review_line_fc": str(review_line_fc) if root_paths.get("line_fc") else None,
                "review_polygon_fc": str(review_polygon_fc) if root_paths.get("polygon_fc") else None,
                "parcel_fabric_mode": PARCEL_FABRIC_MODE_TRUE,
                "parcel_fabric_dataset_path": str(review_dataset),
                "parcel_fabric_layer_path": str(fabric_layer),
                "parcel_record_name": _record_name(transaction_number),
                "parcel_record_id": None,
                "parcel_type": PARCEL_FABRIC_PARCEL_TYPE_NAME,
                "built_parcel_count": len(polygons),
                "built_line_count": len(segments),
                "built_point_count": len(points),
            }
        )

    return (root_paths, review_paths, warnings)


def _build_summary(
    manifest: dict[str, Any],
    approved_review: dict[str, Any],
    output_summary_path: Path,
    result_gdb_path: Path,
    geojson_path: Path,
    layer_paths: dict[str, str | None],
    review_paths: dict[str, str | None],
    points: list[dict[str, Any]],
    segments: list[dict[str, Any]],
    polygons: list[dict[str, Any]],
    operator_id: str | None,
    template_project_path: str | None,
    template_gdb_path: str | None,
    warnings: list[str],
    review_workspace_mode: str,
    parcel_fabric_mode: str | None,
    parcel_fabric_dataset_path: str | None,
    parcel_fabric_layer_path: str | None,
    parcel_record_name: str | None,
    parcel_record_id: str | None,
    parcel_type: str | None,
    built_parcel_count: int,
    built_line_count: int,
    built_point_count: int,
) -> dict[str, Any]:
    artifact_paths = [str(geojson_path)]
    if review_paths.get("review_dataset"):
        artifact_paths.append(review_paths["review_dataset"])

    active_layer_paths = (
        [
            review_paths.get("review_layer"),
            review_paths.get("review_point_fc"),
            review_paths.get("review_line_fc"),
            review_paths.get("review_polygon_fc"),
        ]
        if review_workspace_mode == REVIEW_WORKSPACE_MODE_PARCEL_FABRIC and review_paths.get("review_dataset")
        else [
            layer_paths.get("point_fc"),
            layer_paths.get("line_fc"),
            layer_paths.get("polygon_fc"),
        ]
    )

    payload = {
        "status": "created",
        "review_workspace_mode": review_workspace_mode,
        "result_gdb_path": str(result_gdb_path),
        "artifact_paths": artifact_paths,
        "map_layer_paths": [path for path in active_layer_paths if path],
        "point_feature_class_path": layer_paths.get("point_fc"),
        "line_feature_class_path": layer_paths.get("line_fc"),
        "polygon_feature_class_path": layer_paths.get("polygon_fc"),
        "review_dataset_path": review_paths.get("review_dataset"),
        "review_layer_path": review_paths.get("review_layer"),
        "review_point_feature_class_path": review_paths.get("review_point_fc"),
        "review_line_feature_class_path": review_paths.get("review_line_fc"),
        "review_polygon_feature_class_path": review_paths.get("review_polygon_fc"),
        "parcel_fabric_mode": parcel_fabric_mode,
        "parcel_fabric_dataset_path": parcel_fabric_dataset_path,
        "parcel_fabric_layer_path": parcel_fabric_layer_path,
        "parcel_record_name": parcel_record_name,
        "parcel_record_id": parcel_record_id,
        "parcel_type": parcel_type,
        "built_parcel_count": built_parcel_count,
        "built_line_count": built_line_count,
        "built_point_count": built_point_count,
        "point_count": len(points),
        "line_count": len(segments),
        "polygon_count": len(polygons),
        "template_project_path": template_project_path or None,
        "template_gdb_path": template_gdb_path or None,
    }

    return {
        "schema_version": "1.0.0",
        "transaction_id": manifest.get("transaction_id") or approved_review.get("transaction_number") or "",
        "run_id": f"output-{dt.datetime.now(dt.timezone.utc).strftime('%Y%m%d%H%M%S')}",
        "created_at": _utc_now(),
        "created_by": operator_id or approved_review.get("approved_by"),
        "source_manifest_hash": ((manifest.get("payload") or {}).get("script_plan") or {}).get("source_manifest_hash", ""),
        "payload": payload,
        "warnings": warnings,
        "errors": [],
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate transaction output geodatabase from approved review data.")
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--approved-review", required=True)
    parser.add_argument("--review-data", required=True)
    parser.add_argument("--review-workspace-mode", default=REVIEW_WORKSPACE_MODE_NORMAL)
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--output-summary", required=True)
    parser.add_argument("--operator")
    parser.add_argument("--template-project")
    parser.add_argument("--template-gdb")
    args = parser.parse_args(argv)

    manifest_path = Path(args.manifest)
    approved_review_path = Path(args.approved_review)
    review_data_path = Path(args.review_data)
    review_workspace_mode = _normalize_review_workspace_mode(args.review_workspace_mode)
    output_root = Path(args.output_root)
    output_summary_path = Path(args.output_summary)
    template_gdb_path = Path(args.template_gdb) if args.template_gdb else None

    manifest = _read_json(manifest_path)
    approved_review = _read_json(approved_review_path)
    review_data = _read_json(review_data_path)

    approved_hash = approved_review.get("review_hash")
    review_hash = review_data.get("review_hash")
    if approved_hash and review_hash and str(approved_hash).strip().lower() != str(review_hash).strip().lower():
        raise RuntimeError("Approved review hash does not match current review data.")

    points = _normalize_points(review_data)
    if not points:
        raise RuntimeError("Approved review data does not contain any usable point rows for output generation.")

    point_groups = _grouped_point_sequences(points)
    segments = _polyline_segments(point_groups)
    polygons = _polygon_rings(point_groups)
    transaction_number = review_data.get("transaction_number") or approved_review.get("transaction_number") or manifest.get("transaction_id") or "transaction"
    result_gdb_path = output_root / f"{transaction_number}_parcel_output.gdb"
    geojson_path = output_root / "extracted_geometry.geojson"
    output_root.mkdir(parents=True, exist_ok=True)

    arcpy = _load_arcpy()
    if arcpy is not None:
        layer_paths, review_paths, warnings = _create_outputs_with_arcpy(
            arcpy,
            result_gdb_path,
            template_gdb_path,
            points,
            segments,
            polygons,
            review_workspace_mode,
            str(transaction_number),
        )
    elif os.environ.get("SIDWELL_OUTPUT_ADAPTER_TEST_MODE", "").strip() == "1":
        layer_paths, review_paths, warnings = _create_outputs_filesystem_fallback(
            result_gdb_path,
            points,
            segments,
            polygons,
            review_workspace_mode,
            str(transaction_number),
        )
    else:
        raise RuntimeError("ArcPy is not available for output generation.")

    effective_polygons = polygons if layer_paths.get("polygon_fc") else []
    _write_json(geojson_path, _build_geojson(points, segments, effective_polygons))
    summary = _build_summary(
        manifest,
        approved_review,
        output_summary_path,
        result_gdb_path,
        geojson_path,
        layer_paths,
        review_paths,
        points,
        segments,
        effective_polygons,
        args.operator,
        args.template_project,
        args.template_gdb,
        warnings,
        review_workspace_mode,
        review_paths.get("parcel_fabric_mode"),
        review_paths.get("parcel_fabric_dataset_path"),
        review_paths.get("parcel_fabric_layer_path"),
        review_paths.get("parcel_record_name"),
        review_paths.get("parcel_record_id"),
        review_paths.get("parcel_type"),
        int(review_paths.get("built_parcel_count") or 0),
        int(review_paths.get("built_line_count") or 0),
        int(review_paths.get("built_point_count") or 0),
    )
    _write_json(output_summary_path, summary)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"Output generation failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
