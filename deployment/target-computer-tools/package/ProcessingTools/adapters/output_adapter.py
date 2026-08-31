import argparse
import datetime as dt
import json
import math
import os
import re
import shutil
import sys
from pathlib import Path
from typing import Any

REVIEW_WORKSPACE_MODE_NORMAL = "normal"
REVIEW_WORKSPACE_MODE_PARCEL_FABRIC = "parcel_fabric"
REVIEW_WORKSPACE_MODE_ENTERPRISE = "enterprise_working_layers"
REVIEW_WORKSPACE_MODE_ENTERPRISE_PARCEL_FABRIC = "enterprise_parcel_fabric"
REVIEW_RESULT_OWNER_APPROVED = "approved_review"
REVIEW_RESULT_OWNER_MANUAL = "manual_spatial_review"
COGO_SOURCE_MODE_PREFER_SOURCE = "prefer_source"
COGO_SOURCE_MODE_PREFER_COMPUTED = "prefer_computed"
COGO_SOURCE_MODE_SOURCE_THEN_COMPUTED = "source_then_computed"
ORIENTATION_NORMALIZE_MODE_PRESERVE = "preserve"
ORIENTATION_NORMALIZE_MODE_CLOCKWISE = "clockwise"
ORIENTATION_NORMALIZE_MODE_COUNTERCLOCKWISE = "counterclockwise"
PARCEL_FABRIC_MODE_PILOT = "pilot"
PARCEL_FABRIC_MODE_TRUE = "true"
PARCEL_FABRIC_DATASET_NAME = "parcel_fabric_dataset"
PARCEL_FABRIC_NAME = "local_parcel_fabric"
PARCEL_FABRIC_PARCEL_TYPE_NAME = "compute_review"
PARCEL_FABRIC_RECORD_PREFIX = "sidwell-record"
JAD2001_WKID = 3448
JAD2001_LATEST_WKID = 3448
JAD2001_NAME = "JAD 2001 Jamaica Grid"
PLA_WORKFLOW_PROFILE = "pla_plan_annexation"
PLA_REPORTS_DIRECTORY_NAME = "reports"
PLA_SELECTED_PLAN_OUTPUT_PDF_FILE_NAME = "pla_selected_plan_page.pdf"
PLA_GEOMETRY_OUTPUT_PDF_FILE_NAME = "pla_generated_geometry.pdf"
PDF_MAX_LINE_LENGTH = 96
PDF_MAX_LINES_PER_PAGE = 45
_ARCPY_IMPORT_ERROR: str | None = None


def run(input_json_path, output_json_path):
    raise NotImplementedError("Output adapter is implemented through its CLI entrypoint.")


def _utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def _read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def _is_pla_plan_annexation(manifest: dict[str, Any], review_data: dict[str, Any]) -> bool:
    payload = manifest.get("payload") or {}
    detected_profile = payload.get("detected_profile") or {}
    transaction_type_profile = payload.get("transaction_type_profile") or {}
    candidates = [
        payload.get("workflow_profile"),
        detected_profile.get("profile_code"),
        transaction_type_profile.get("workflow_profile"),
        review_data.get("source_profile"),
        review_data.get("extraction_source"),
    ]

    return any(str(value or "").strip().lower() == PLA_WORKFLOW_PROFILE for value in candidates)


def _pdf_escape(text: str) -> str:
    return text.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")


def _wrap_pdf_lines(lines: list[str]) -> list[str]:
    wrapped: list[str] = []
    for line in lines:
        remaining = line
        if not remaining:
            wrapped.append("")
            continue

        while len(remaining) > PDF_MAX_LINE_LENGTH:
            split_at = remaining.rfind(" ", 0, PDF_MAX_LINE_LENGTH)
            if split_at <= 0:
                split_at = PDF_MAX_LINE_LENGTH
            wrapped.append(remaining[:split_at])
            remaining = remaining[split_at:].lstrip()
        wrapped.append(remaining)

    return wrapped


def _pdf_content_object(lines: list[str]) -> str:
    stream_lines = ["BT", "/F1 10 Tf", "50 750 Td"]
    for line in lines:
        stream_lines.append(f"({_pdf_escape(line)}) Tj")
        stream_lines.append("0 -15 Td")
    stream_lines.append("ET")
    stream = "\n".join(stream_lines) + "\n"
    return f"<< /Length {len(stream.encode('ascii', errors='replace'))} >>\nstream\n{stream}endstream"


def _write_text_pdf(path: Path, lines: list[str]) -> None:
    wrapped = _wrap_pdf_lines(lines)
    pages = [wrapped[index:index + PDF_MAX_LINES_PER_PAGE] for index in range(0, len(wrapped), PDF_MAX_LINES_PER_PAGE)]
    if not pages:
        pages = [[""]]

    objects: list[str] = [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
    ]
    page_numbers: list[int] = []
    for page_lines in pages:
        page_object_number = len(objects) + 1
        content_object_number = len(objects) + 2
        page_numbers.append(page_object_number)
        objects.append(
            f"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
            f"/Resources << /Font << /F1 3 0 R >> >> /Contents {content_object_number} 0 R >>"
        )
        objects.append(_pdf_content_object(page_lines))

    objects[1] = f"<< /Type /Pages /Count {len(page_numbers)} /Kids [{' '.join(f'{number} 0 R' for number in page_numbers)}] >>"

    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("wb") as handle:
        offsets = [0]
        handle.write(b"%PDF-1.4\n")
        for index, obj in enumerate(objects, start=1):
            offsets.append(handle.tell())
            handle.write(f"{index} 0 obj\n".encode("ascii"))
            handle.write(obj.encode("ascii", errors="replace"))
            handle.write(b"\nendobj\n")
        xref_offset = handle.tell()
        handle.write(f"xref\n0 {len(objects) + 1}\n".encode("ascii"))
        handle.write(b"0000000000 65535 f \n")
        for offset in offsets[1:]:
            handle.write(f"{offset:010d} 00000 n \n".encode("ascii"))
        handle.write(
            f"trailer\n<< /Size {len(objects) + 1} /Root 1 0 R >>\nstartxref\n{xref_offset}\n%%EOF\n".encode("ascii")
        )


def _load_case_json(path: Path) -> dict[str, Any]:
    try:
        return _read_json(path) if path.exists() else {}
    except (OSError, json.JSONDecodeError):
        return {}


def _format_number(value: Any, precision: int = 2) -> str:
    try:
        number = float(value)
    except (TypeError, ValueError):
        return ""

    return f"{number:.{precision}f}"


def _resolve_case_artifact_path(case_root: Path, value: Any) -> Path | None:
    text = str(value or "").strip()
    if not text:
        return None

    path = Path(text)
    if path.is_absolute():
        return path

    return case_root / text.replace("/", os.sep)


def _extract_pdf_page(source_pdf: Path, selected_page_number: int, output_pdf: Path) -> bool:
    try:
        from pypdf import PdfReader, PdfWriter  # type: ignore

        reader = PdfReader(str(source_pdf))
        page_index = selected_page_number - 1
        if page_index < 0 or page_index >= len(reader.pages):
            return False

        writer = PdfWriter()
        writer.add_page(reader.pages[page_index])
        output_pdf.parent.mkdir(parents=True, exist_ok=True)
        with output_pdf.open("wb") as handle:
            writer.write(handle)
        return output_pdf.exists() and output_pdf.stat().st_size > 0
    except Exception:
        return False


def _create_pla_selected_plan_output_pdf(output_root: Path, manifest_path: Path) -> Path | None:
    case_root = manifest_path.parent
    selection = _load_case_json(case_root / "working" / "pla_plan_annexation" / "pla_plan_evidence_selection.json")
    selected_page_number = selection.get("selected_page_number")
    try:
        selected_page = int(selected_page_number)
    except (TypeError, ValueError):
        return None

    output_pdf = output_root / PLA_REPORTS_DIRECTORY_NAME / PLA_SELECTED_PLAN_OUTPUT_PDF_FILE_NAME
    source_pdf = _resolve_case_artifact_path(case_root, selection.get("source_relative_path"))
    if source_pdf is not None and source_pdf.exists() and _extract_pdf_page(source_pdf, selected_page, output_pdf):
        return output_pdf

    generated_evidence = _resolve_case_artifact_path(case_root, selection.get("generated_plan_evidence_path"))
    generated_format = str(selection.get("generated_plan_evidence_format") or "").strip().lower()
    if generated_format == "pdf" and generated_evidence is not None and generated_evidence.exists():
        output_pdf.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(generated_evidence, output_pdf)
        return output_pdf

    return None


def _create_pla_geometry_output_pdf(
    output_root: Path,
    manifest_path: Path,
    manifest: dict[str, Any],
    approved_review: dict[str, Any],
    review_data: dict[str, Any],
    points: list[dict[str, Any]],
    segments: list[dict[str, Any]],
    polygons: list[dict[str, Any]],
    operator_id: str | None,
    geojson_path: Path,
    result_gdb_path: Path,
) -> Path:
    case_root = manifest_path.parent
    working_root = case_root / "working"
    selection = _load_case_json(working_root / "pla_plan_annexation" / "pla_plan_evidence_selection.json")
    validation = _load_case_json(working_root / "validation_summary.json")
    spatial_review = _load_case_json(working_root / "spatial_review_approval.json")
    validation_payload = validation.get("payload") or {}
    closure_results = validation_payload.get("closure_results") if isinstance(validation_payload.get("closure_results"), list) else []
    first_closure = closure_results[0] if closure_results else {}
    first_polygon = polygons[0] if polygons else {}

    transaction_number = str(
        review_data.get("transaction_number")
        or approved_review.get("transaction_number")
        or manifest.get("transaction_id")
        or case_root.name
    )
    selected_evidence = selection.get("generated_plan_evidence_path") or selection.get("generated_plan_evidence_relative_path") or ""
    selected_page = selection.get("selected_page_number") or ""
    area_sq_m = _format_number(first_polygon.get("area_sq_m") or first_closure.get("computed_area_sq_m"))
    perimeter_m = _format_number(first_polygon.get("perimeter_m"))

    lines = [
        "PLA Plan Annexation Generated Geometry",
        f"Transaction Number: {transaction_number}",
        f"Generated At UTC: {_utc_now()}",
        f"Generated By: {operator_id or approved_review.get('approved_by') or ''}",
        "",
        "Review Evidence",
        f"- Selected Plan Evidence: {selected_evidence}",
        f"- Selected Source Page: {selected_page}",
        f"- Review Status: {approved_review.get('status') or approved_review.get('decision') or 'approved'}",
        f"- Spatial Review Approved At: {spatial_review.get('approved_at') or ''}",
        "",
        "Generated Geometry",
        f"- Coordinate System: {JAD2001_NAME} (EPSG:{JAD2001_WKID})",
        f"- Points: {len(points)}",
        f"- Lines: {len(segments)}",
        f"- Polygons: {len(polygons)}",
        f"- Area sq m: {area_sq_m}",
        f"- Perimeter m: {perimeter_m}",
        "",
        "Artifacts",
        f"- GeoJSON: {geojson_path}",
        f"- Geodatabase: {result_gdb_path}",
        "",
        "Notes",
        "- This PDF describes the geometry generated from approved bearings and distances.",
        "- Source-plan comparison is approximate visual evidence, not survey-accurate georeferencing.",
    ]

    pdf_path = output_root / PLA_REPORTS_DIRECTORY_NAME / PLA_GEOMETRY_OUTPUT_PDF_FILE_NAME
    _write_text_pdf(pdf_path, lines)
    return pdf_path


def _load_arcpy():
    global _ARCPY_IMPORT_ERROR
    _ARCPY_IMPORT_ERROR = None
    try:
        import arcpy  # type: ignore

        return arcpy
    except Exception as exc:
        _ARCPY_IMPORT_ERROR = f"{type(exc).__name__}: {exc}"
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
        pass

    match = re.search(r"[-+]?\d+(?:\.\d+)?", text)
    if not match:
        return None

    try:
        return float(match.group(0))
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
    if text in {REVIEW_WORKSPACE_MODE_ENTERPRISE, "enterprise-working-layers"}:
        return REVIEW_WORKSPACE_MODE_ENTERPRISE
    if text in {REVIEW_WORKSPACE_MODE_ENTERPRISE_PARCEL_FABRIC, "enterprise-parcel-fabric"}:
        return REVIEW_WORKSPACE_MODE_ENTERPRISE_PARCEL_FABRIC

    return REVIEW_WORKSPACE_MODE_NORMAL


def _normalize_review_result_owner(value: Any) -> str:
    text = "" if value is None else str(value).strip().lower()
    return REVIEW_RESULT_OWNER_MANUAL if text == REVIEW_RESULT_OWNER_MANUAL else REVIEW_RESULT_OWNER_APPROVED


def _normalize_bool_flag(value: Any, default: bool = False) -> bool:
    if value is None:
        return default
    if isinstance(value, bool):
        return value
    return str(value).strip().lower() in {"1", "true", "yes", "y", "on"}


def _normalize_cogo_source_mode(value: Any) -> str:
    text = "" if value is None else str(value).strip().replace("-", "_").replace(" ", "_").lower()
    if text == COGO_SOURCE_MODE_PREFER_SOURCE:
        return COGO_SOURCE_MODE_PREFER_SOURCE
    if text == COGO_SOURCE_MODE_PREFER_COMPUTED:
        return COGO_SOURCE_MODE_PREFER_COMPUTED
    return COGO_SOURCE_MODE_SOURCE_THEN_COMPUTED


def _normalize_orientation_mode(value: Any) -> str:
    text = "" if value is None else str(value).strip().replace("-", "_").replace(" ", "_").lower()
    if text == ORIENTATION_NORMALIZE_MODE_CLOCKWISE:
        return ORIENTATION_NORMALIZE_MODE_CLOCKWISE
    if text == ORIENTATION_NORMALIZE_MODE_COUNTERCLOCKWISE:
        return ORIENTATION_NORMALIZE_MODE_COUNTERCLOCKWISE
    return ORIENTATION_NORMALIZE_MODE_PRESERVE


def _normalize_points(review_data: dict[str, Any]) -> list[dict[str, Any]]:
    normalized: list[dict[str, Any]] = []
    root_doc_type_id = _normalize_text(review_data.get("doc_type_id") or "", 64)
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

        source_bearing = (
            row.get("review_bearing")
            or row.get("bearing")
            or row.get("course")
            or row.get("course_from_previous")
            or ""
        )
        source_distance_m = _parse_coordinate(
            row.get("distance_m")
            or row.get("distance")
            or row.get("length_from_previous_m")
        )
        source_length_txt = (
            row.get("review_length")
            if row.get("review_length") is not None
            else row.get("length")
            or row.get("length_from_previous_m")
            or ""
        )

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
                "point_id": _normalize_text(point_id, 64),
                "easting": easting,
                "northing": northing,
                "parcel_name": _normalize_text(row.get("review_parcel_name") or row.get("parcel_name") or "", 128),
                "bearing": _normalize_text(source_bearing, 64),
                "distance_m": source_distance_m,
                "radius_m": _parse_coordinate(row.get("radius_m") or row.get("radius")),
                "arc_length_m": _parse_coordinate(row.get("arc_length_m") or row.get("arc_length")),
                "doc_type_id": _normalize_text(row.get("doc_type_id") or root_doc_type_id, 64),
                "traverse_id": _normalize_text(row.get("review_traverse_id") or row.get("traverse_id") or "", 64),
                "point_role": _normalize_text(row.get("review_point_role") or row.get("point_role") or "", 32),
                "from_segment": _parse_int(row.get("review_from_segment") or row.get("from_segment") or row.get("segment_no")),
                "source_doc": _normalize_text(
                    row.get("source_document_name")
                    or row.get("source_document")
                    or row.get("source_file")
                    or "",
                    256,
                ),
                "length": _normalize_text(source_length_txt, 128),
                "distance_txt": _normalize_text(source_length_txt, 64),
                "status": _normalize_text(row.get("review_extraction_status") or row.get("status") or "", 64),
                "status_txt": _normalize_text(row.get("review_extraction_status") or row.get("status") or "", 64),
                "is_manual": _parse_bool(row.get("is_manual")) or str(row.get("row_id") or "").startswith("manual-"),
                "is_edited": _parse_bool(row.get("review_is_edited") if row.get("review_is_edited") is not None else row.get("is_edited")),
                "length_txt": _normalize_text(source_length_txt, 64),
                "source_evidence": _normalize_text(row.get("review_source_evidence") or row.get("source_evidence") or "", 1024),
                "source_txt": _normalize_text(row.get("review_source_evidence") or row.get("source_evidence") or "", 1024),
            }
        )

    return normalized


def _field_text_value(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, dict):
        for key in ("value", "normalized_value", "raw_value", "text", "name"):
            text = _field_text_value(value.get(key))
            if text:
                return text
        return ""
    if isinstance(value, list):
        for item in value:
            text = _field_text_value(item)
            if text:
                return text
        return ""
    text = str(value).strip()
    if text.lower() in {"none", "null", "not provided", "not present", "missing", "present", "true", "false"}:
        return ""
    return text


def _resolve_property_name(review_data: dict[str, Any]) -> str:
    survey_metadata = review_data.get("survey_metadata") if isinstance(review_data.get("survey_metadata"), dict) else {}
    for candidate in (
        survey_metadata.get("property_name"),
        review_data.get("property_name"),
        review_data.get("surveyed_property_names"),
        survey_metadata.get("surveyed_property_name"),
    ):
        value = _field_text_value(candidate)
        if value:
            return _normalize_text(value, 128)
    return ""


def _apply_property_name_to_polygons(polygons: list[dict[str, Any]], property_name: str) -> None:
    normalized = _normalize_text(property_name, 128)
    if not normalized:
        return
    for polygon in polygons:
        polygon["property_name"] = normalized
        polygon["propertyName"] = normalized


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


def _parcel_id_for_group(group_id: str, group_index: int) -> str:
    text = str(group_id or "").strip()
    lowered = text.lower()
    if lowered.startswith("parcel-"):
        suffix = text[7:]
        if suffix.isdigit():
            return f"parcel-{int(suffix):03d}"
    if text and not text.lower().startswith("traverse:"):
        return text
    return f"parcel-{group_index:03d}"


def _apply_group_parcel_metadata(point_groups: list[dict[str, Any]]) -> list[dict[str, Any]]:
    enriched_groups: list[dict[str, Any]] = []
    for group_index, group in enumerate(point_groups, start=1):
        group_id = str(group.get("group_id") or f"parcel-{group_index}").strip()
        parcel_id = _parcel_id_for_group(group_id, group_index)
        points = group.get("points") or []
        parcel_name = ""
        enriched_points: list[dict[str, Any]] = []

        for point_index, point in enumerate(points, start=1):
            updated = dict(point)
            updated["parcel_id"] = parcel_id
            updated["point_order"] = point_index
            updated["point_id"] = updated.get("point_identifier") or updated.get("point_id") or f"{parcel_id}_P{point_index}"
            if updated.get("parcel_name"):
                parcel_name = str(updated["parcel_name"])
            enriched_points.append(updated)

        if not parcel_name:
            parcel_name = parcel_id

        for updated in enriched_points:
            if not updated.get("parcel_name"):
                updated["parcel_name"] = parcel_name

        enriched_groups.append(
            {
                "group_id": group_id,
                "parcel_id": parcel_id,
                "parcel_name": parcel_name,
                "points": enriched_points,
            }
        )

    return enriched_groups


def _polyline_segments(point_groups: list[dict[str, Any]]) -> list[dict[str, Any]]:
    def append_segment(
        group_id: str,
        parcel_id: str,
        segment_order: int,
        start: dict[str, Any],
        end: dict[str, Any],
        *,
        is_closure: bool = False,
    ) -> None:
        nonlocal segment_index
        computed_distance_m = _distance_between(
            float(start["easting"]),
            float(start["northing"]),
            float(end["easting"]),
            float(end["northing"]),
        )
        distance_m = end.get("distance_m")
        distance_txt = _normalize_text(end.get("distance_txt") or end.get("length") or "", 64)
        is_manual = bool(start.get("is_manual")) or bool(end.get("is_manual"))
        is_edited = bool(start.get("is_edited")) or bool(end.get("is_edited"))
        segments.append(
            {
                "line_id": f"{parcel_id}_L{segment_order}",
                "segment_index": segment_index,
                "segment_order": segment_order,
                "parcel_id": parcel_id,
                "parcel_group_id": group_id,
                "traverse_id": start.get("traverse_id") or end.get("traverse_id") or "",
                "line_type": "curve" if end.get("radius_m") or end.get("arc_length_m") else ("closure" if is_closure else "line"),
                "start_point": start["point_identifier"],
                "end_point": end["point_identifier"],
                "from_point_id": start["point_identifier"],
                "to_point_id": end["point_identifier"],
                "start": (start["easting"], start["northing"]),
                "end": (end["easting"], end["northing"]),
                "bearing": end.get("bearing") or "",
                "bearing_txt": end.get("bearing") or "",
                "length": end.get("length") or "",
                "length_txt": end.get("length") or "",
                "distance_txt": distance_txt,
                "distance_m": distance_m if distance_m is not None else computed_distance_m,
                "radius_m": end.get("radius_m"),
                "arc_length_m": end.get("arc_length_m"),
                "delta_angle_txt": _normalize_text(end.get("delta_angle_txt") or "", 64),
                "chord_bearing_txt": _normalize_text(end.get("chord_bearing_txt") or "", 64),
                "chord_distance_m": _parse_coordinate(end.get("chord_distance_m")),
                "doc_type_id": end.get("doc_type_id") or "",
                "source_doc": end.get("source_doc") or "",
                "row_id": end.get("row_id") or "",
                "status": end.get("status") or "",
                "status_txt": end.get("status") or "",
                "source_evidence": end.get("source_evidence") or "",
                "source_txt": end.get("source_evidence") or "",
                "is_boundary_break": bool(end.get("is_boundary_break")),
                "is_manual": is_manual,
                "is_edited": is_edited,
            }
        )
        segment_index += 1

    def points_match(first: dict[str, Any], second: dict[str, Any]) -> bool:
        first_id = (first.get("point_identifier") or "").strip()
        second_id = (second.get("point_identifier") or "").strip()
        if first_id and second_id and first_id == second_id:
            return True
        return (
            math.isclose(float(first["easting"]), float(second["easting"]), abs_tol=1e-9)
            and math.isclose(float(first["northing"]), float(second["northing"]), abs_tol=1e-9)
        )

    segments: list[dict[str, Any]] = []
    segment_index = 1
    for group in point_groups:
        group_id = group.get("group_id") or ""
        parcel_id = group.get("parcel_id") or group_id
        points = group.get("points") or []
        for index in range(len(points) - 1):
            start = points[index]
            end = points[index + 1]
            append_segment(group_id, parcel_id, index + 1, start, end)
        if len(points) >= 3 and not points_match(points[0], points[-1]):
            append_segment(group_id, parcel_id, len(points), points[-1], points[0], is_closure=True)
    return segments


def _included_reviewed_boundary_raw_segments(review_data: dict[str, Any]) -> list[tuple[int, dict[str, Any]]]:
    raw_segments = review_data.get("segments")
    if not isinstance(raw_segments, list):
        return []

    included: list[tuple[int, dict[str, Any]]] = []
    for index, raw_segment in enumerate(raw_segments, start=1):
        if not isinstance(raw_segment, dict):
            continue
        include_value = raw_segment.get("review_include_in_boundary")
        if include_value is None:
            include_value = raw_segment.get("include_in_boundary")
        if include_value is not None and not _parse_bool(include_value):
            continue
        included.append((index, raw_segment))

    return sorted(
        included,
        key=lambda item: _parse_int(item[1].get("review_sequence") or item[1].get("segment_no") or item[1].get("sequence")) or item[0],
    )


def _point_lookup_by_identifier(point_groups: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    point_by_id: dict[str, dict[str, Any]] = {}
    for group in point_groups:
        for point in group.get("points") or []:
            point_id = str(point.get("point_identifier") or point.get("point_id") or "").strip()
            if point_id:
                point_by_id[point_id.lower()] = point
    return point_by_id


def _parse_bearing_azimuth_deg(value: Any) -> float | None:
    text = str(value or "").strip().upper()
    if not text:
        return None

    text = (
        text.replace("°", " ")
        .replace("º", " ")
        .replace("'", " ")
        .replace("′", " ")
        .replace("’", " ")
        .replace('"', " ")
        .replace("″", " ")
        .replace("”", " ")
    )
    text = re.sub(r"\s+", " ", text).strip()
    match = re.match(r"^([NS])\s*([0-9]+(?:\.[0-9]+)?)(?:\s+([0-9]+(?:\.[0-9]+)?))?(?:\s+([0-9]+(?:\.[0-9]+)?))?\s*([EW])$", text)
    if not match:
        return None

    ns, degrees_text, minutes_text, seconds_text, ew = match.groups()
    degrees = float(degrees_text)
    minutes = float(minutes_text or 0.0)
    seconds = float(seconds_text or 0.0)
    angle = degrees + (minutes / 60.0) + (seconds / 3600.0)
    if angle < 0.0 or angle > 90.0:
        return None
    if ns == "N" and ew == "E":
        return angle
    if ns == "S" and ew == "E":
        return 180.0 - angle
    if ns == "S" and ew == "W":
        return 180.0 + angle
    return 360.0 - angle


def _endpoint_from_bearing_distance(start: tuple[float, float], bearing_txt: str, distance_m: float) -> tuple[float, float] | None:
    azimuth_deg = _parse_bearing_azimuth_deg(bearing_txt)
    if azimuth_deg is None:
        return None
    azimuth_rad = math.radians(azimuth_deg)
    return (
        float(start[0]) + math.sin(azimuth_rad) * distance_m,
        float(start[1]) + math.cos(azimuth_rad) * distance_m,
    )


def _should_rebuild_reviewed_output_from_bearings(review_data: dict[str, Any]) -> bool:
    solver = review_data.get("boundary_solver")
    if not isinstance(solver, dict):
        return False
    status = str(solver.get("status") or "").strip().lower()
    geometry_source = str(solver.get("geometry_source") or "").strip().lower()
    if status not in {"passed", "warning"} or geometry_source != "reviewed_boundary_segments":
        return False
    findings = " ".join(str(finding or "") for finding in (solver.get("findings") or [])).lower()
    return "unscaled reviewed boundary was kept" in findings or "anchored to printed reference point" in findings


def _reviewed_boundary_construction_from_solver(
    review_data: dict[str, Any],
    point_groups: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    if not _should_rebuild_reviewed_output_from_bearings(review_data):
        return [], []

    included_segments = _included_reviewed_boundary_raw_segments(review_data)
    if len(included_segments) < 3:
        return [], []

    point_by_id = _point_lookup_by_identifier(point_groups)
    first_index, first_raw_segment = included_segments[0]
    anchor_id = _normalize_text(first_raw_segment.get("review_from_point") or first_raw_segment.get("from_point"), 64)
    anchor_point = point_by_id.get(anchor_id.lower())
    if anchor_point is None:
        return [], []

    parcel_id = anchor_point.get("parcel_id") or anchor_point.get("parcel_group_id") or "parcel-001"
    parcel_group_id = anchor_point.get("parcel_group_id") or parcel_id
    anchor_coord = (float(anchor_point["easting"]), float(anchor_point["northing"]))
    current_coord = anchor_coord
    constructed_points: list[dict[str, Any]] = []
    constructed_segments: list[dict[str, Any]] = []
    constructed_by_id: dict[str, dict[str, Any]] = {}

    def add_point(point_id: str, coord: tuple[float, float], sequence: int, source_point: dict[str, Any] | None, status: str) -> dict[str, Any]:
        point = dict(source_point or {})
        point.update(
            {
                "point_identifier": point_id,
                "point_id": point_id,
                "easting": float(coord[0]),
                "northing": float(coord[1]),
                "parcel_id": parcel_id,
                "parcel_group_id": parcel_group_id,
                "sequence": sequence,
                "seq": sequence,
                "point_order": sequence,
                "status": status,
                "source_evidence": "Output coordinate reconstructed from reviewed bearing/distance boundary.",
            }
        )
        constructed_by_id[point_id.lower()] = point
        constructed_points.append(point)
        return point

    add_point(anchor_id, anchor_coord, 1, anchor_point, str(anchor_point.get("status") or "0.95"))

    for segment_position, (raw_index, raw_segment) in enumerate(included_segments, start=1):
        from_point_id = _normalize_text(raw_segment.get("review_from_point") or raw_segment.get("from_point"), 64)
        to_point_id = _normalize_text(raw_segment.get("review_to_point") or raw_segment.get("to_point"), 64)
        if not from_point_id or not to_point_id:
            return [], []

        bearing_txt = _normalize_text(raw_segment.get("review_bearing_txt") or raw_segment.get("bearing_txt") or raw_segment.get("bearing"), 64)
        distance_txt = _normalize_text(
            raw_segment.get("review_distance_txt")
            or raw_segment.get("review_length_txt")
            or raw_segment.get("distance_txt")
            or raw_segment.get("length_txt")
            or raw_segment.get("distance")
            or raw_segment.get("length")
            or "",
            64,
        )
        distance_m = _parse_coordinate(distance_txt)
        if not bearing_txt or distance_m is None:
            return [], []

        end_coord = _endpoint_from_bearing_distance(current_coord, bearing_txt, distance_m)
        if end_coord is None:
            return [], []
        is_closure = to_point_id.lower() == anchor_id.lower() and segment_position == len(included_segments)
        if is_closure:
            end_coord = anchor_coord
            end_point = constructed_by_id[anchor_id.lower()]
        else:
            end_point = add_point(
                to_point_id,
                end_coord,
                len(constructed_points) + 1,
                point_by_id.get(to_point_id.lower()),
                "derived_from_reviewed_segments",
            )

        sequence = _parse_int(raw_segment.get("review_sequence") or raw_segment.get("segment_no") or raw_segment.get("sequence")) or raw_index
        status_txt = _normalize_text(raw_segment.get("review_status") or raw_segment.get("status") or "", 64)
        source_evidence = _normalize_text(raw_segment.get("source_evidence") or raw_segment.get("review_notes") or "", 1024)
        constructed_segments.append(
            {
                "line_id": f"{parcel_id}_L{sequence}",
                "segment_index": len(constructed_segments) + 1,
                "segment_order": sequence,
                "parcel_id": parcel_id,
                "parcel_group_id": parcel_group_id,
                "traverse_id": anchor_point.get("traverse_id") or "",
                "line_type": "line",
                "start_point": from_point_id,
                "end_point": to_point_id,
                "from_point_id": from_point_id,
                "to_point_id": to_point_id,
                "start": current_coord,
                "end": end_coord,
                "bearing": bearing_txt,
                "bearing_txt": bearing_txt,
                "length": distance_txt,
                "length_txt": distance_txt,
                "distance_txt": distance_txt,
                "distance_m": distance_m,
                "radius_m": None,
                "arc_length_m": None,
                "delta_angle_txt": "",
                "chord_bearing_txt": "",
                "chord_distance_m": None,
                "doc_type_id": anchor_point.get("doc_type_id") or end_point.get("doc_type_id") or "",
                "source_doc": anchor_point.get("source_doc") or end_point.get("source_doc") or "",
                "row_id": _normalize_text(raw_segment.get("segment_id") or f"segment-{sequence}", 64),
                "status": status_txt,
                "status_txt": status_txt,
                "source_evidence": source_evidence,
                "source_txt": source_evidence,
                "is_boundary_break": False,
                "is_manual": bool(end_point.get("is_manual")),
                "is_edited": True,
            }
        )
        current_coord = end_coord

    return constructed_points, constructed_segments


def _reviewed_boundary_segments(review_data: dict[str, Any], point_groups: list[dict[str, Any]]) -> list[dict[str, Any]]:
    included_segments = _included_reviewed_boundary_raw_segments(review_data)
    if not included_segments:
        return []

    point_by_id = _point_lookup_by_identifier(point_groups)
    segments: list[dict[str, Any]] = []
    for index, raw_segment in included_segments:
        from_point_id = _normalize_text(raw_segment.get("review_from_point") or raw_segment.get("from_point"), 64)
        to_point_id = _normalize_text(raw_segment.get("review_to_point") or raw_segment.get("to_point"), 64)
        start = point_by_id.get(from_point_id.lower())
        end = point_by_id.get(to_point_id.lower())
        if start is None or end is None:
            continue

        sequence = _parse_int(raw_segment.get("review_sequence") or raw_segment.get("segment_no") or raw_segment.get("sequence")) or index
        parcel_id = start.get("parcel_id") or end.get("parcel_id") or start.get("parcel_group_id") or "parcel-001"
        parcel_group_id = start.get("parcel_group_id") or end.get("parcel_group_id") or parcel_id
        bearing_txt = _normalize_text(raw_segment.get("review_bearing_txt") or raw_segment.get("bearing_txt") or raw_segment.get("bearing"), 64)
        distance_txt = _normalize_text(
            raw_segment.get("review_distance_txt")
            or raw_segment.get("review_length_txt")
            or raw_segment.get("distance_txt")
            or raw_segment.get("length_txt")
            or raw_segment.get("distance")
            or raw_segment.get("length")
            or "",
            64,
        )
        distance_m = _parse_coordinate(distance_txt)
        if distance_m is None:
            distance_m = _distance_between(
                float(start["easting"]),
                float(start["northing"]),
                float(end["easting"]),
                float(end["northing"]),
            )
        status_txt = _normalize_text(raw_segment.get("review_status") or raw_segment.get("status") or "", 64)
        source_evidence = _normalize_text(raw_segment.get("source_evidence") or raw_segment.get("review_notes") or "", 1024)

        segments.append(
            {
                "line_id": f"{parcel_id}_L{sequence}",
                "segment_index": len(segments) + 1,
                "segment_order": sequence,
                "parcel_id": parcel_id,
                "parcel_group_id": parcel_group_id,
                "traverse_id": start.get("traverse_id") or end.get("traverse_id") or "",
                "line_type": "line",
                "start_point": from_point_id,
                "end_point": to_point_id,
                "from_point_id": from_point_id,
                "to_point_id": to_point_id,
                "start": (start["easting"], start["northing"]),
                "end": (end["easting"], end["northing"]),
                "bearing": bearing_txt,
                "bearing_txt": bearing_txt,
                "length": distance_txt,
                "length_txt": distance_txt,
                "distance_txt": distance_txt,
                "distance_m": distance_m,
                "radius_m": None,
                "arc_length_m": None,
                "delta_angle_txt": "",
                "chord_bearing_txt": "",
                "chord_distance_m": None,
                "doc_type_id": start.get("doc_type_id") or end.get("doc_type_id") or "",
                "source_doc": start.get("source_doc") or end.get("source_doc") or "",
                "row_id": _normalize_text(raw_segment.get("segment_id") or f"segment-{sequence}", 64),
                "status": status_txt,
                "status_txt": status_txt,
                "source_evidence": source_evidence,
                "source_txt": source_evidence,
                "is_boundary_break": False,
                "is_manual": bool(start.get("is_manual")) or bool(end.get("is_manual")),
                "is_edited": True,
            }
        )

    return sorted(segments, key=lambda segment: int(segment.get("segment_order") or 0))


def _is_pxa_survey_plan_review(review_data: dict[str, Any]) -> bool:
    source_values = [
        review_data.get("extraction_source"),
        review_data.get("extractor_id"),
        review_data.get("active_extractor_id"),
        review_data.get("source_profile"),
        review_data.get("primary_source_role"),
    ]
    text = " ".join(str(value or "") for value in source_values).lower()
    return "survey_plan" in text or "ocr_vision" in text


def _rounded_coord_key(coord: Any) -> tuple[float, float]:
    return (round(float(coord[0]), 6), round(float(coord[1]), 6))


def _dedupe_spatial_points_for_output(points: list[dict[str, Any]]) -> list[dict[str, Any]]:
    deduped: list[dict[str, Any]] = []
    seen: set[tuple[Any, ...]] = set()
    for point in points:
        point_identifier = str(point.get("point_identifier") or point.get("point_id") or "").strip().lower()
        coord_key = (round(float(point["easting"]), 6), round(float(point["northing"]), 6))
        key = ("id_coord", point_identifier, coord_key) if point_identifier else ("coord", coord_key)
        if key in seen:
            continue
        seen.add(key)
        deduped.append(point)
    return deduped


def _segment_output_key(segment: dict[str, Any]) -> tuple[Any, ...]:
    start_key = _rounded_coord_key(segment["start"])
    end_key = _rounded_coord_key(segment["end"])
    edge_key = tuple(sorted((start_key, end_key)))
    line_type = str(segment.get("line_type") or "line").strip().lower()
    if line_type == "curve":
        return (
            line_type,
            edge_key,
            round(float(segment.get("radius_m") or 0.0), 6),
            round(float(segment.get("arc_length_m") or 0.0), 6),
            str(segment.get("delta_angle_txt") or "").strip().lower(),
            str(segment.get("chord_bearing_txt") or "").strip().lower(),
        )
    return ("straight", edge_key)


def _segment_output_score(segment: dict[str, Any]) -> tuple[int, int, int, int]:
    has_bearing = 1 if str(segment.get("bearing_txt") or segment.get("bearing") or "").strip() else 0
    has_distance = 1 if str(segment.get("distance_txt") or segment.get("length_txt") or segment.get("length") or "").strip() else 0
    is_boundary = 0 if str(segment.get("line_type") or "").strip().lower() == "closure" else 1
    has_source = 1 if str(segment.get("source_evidence") or segment.get("source_txt") or "").strip() else 0
    return (has_bearing, has_distance, is_boundary, has_source)


def _dedupe_spatial_segments_for_output(segments: list[dict[str, Any]]) -> list[dict[str, Any]]:
    selected: dict[tuple[Any, ...], dict[str, Any]] = {}
    order: list[tuple[Any, ...]] = []
    for segment in segments:
        key = _segment_output_key(segment)
        if key not in selected:
            selected[key] = segment
            order.append(key)
            continue
        if _segment_output_score(segment) > _segment_output_score(selected[key]):
            selected[key] = segment

    deduped: list[dict[str, Any]] = []
    for index, key in enumerate(order, start=1):
        updated = dict(selected[key])
        updated["segment_index"] = index
        deduped.append(updated)
    return deduped


def _polygon_points(points: list[dict[str, Any]]) -> list[tuple[float, float]]:
    if len(points) < 3:
        return []

    coords = [(float(point["easting"]), float(point["northing"])) for point in points]
    return _polygon_ring_from_coords(coords)


def _polygon_ring_from_coords(coords: list[tuple[float, float]]) -> list[tuple[float, float]]:
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


def _ring_orientation_name(coords: list[tuple[float, float]]) -> str:
    if len(coords) < 4:
        return "unknown"
    signed_area = _ring_area(coords)
    if math.isclose(signed_area, 0.0, abs_tol=1e-9):
        return "degenerate"
    return "counterclockwise" if signed_area > 0 else "clockwise"


def _normalize_ring_orientation(
    ring: list[tuple[float, float]],
    normalize_orientation_mode: str,
) -> list[tuple[float, float]]:
    mode = _normalize_orientation_mode(normalize_orientation_mode)
    if mode == ORIENTATION_NORMALIZE_MODE_PRESERVE or len(ring) < 4:
        return ring

    orientation = _ring_orientation_name(ring)
    if orientation in {"unknown", "degenerate"} or orientation == mode:
        return ring

    reversed_ring = list(reversed(ring[:-1]))
    if reversed_ring and reversed_ring[0] != reversed_ring[-1]:
        reversed_ring.append(reversed_ring[0])
    return reversed_ring


def _polygon_rings_from_segments(
    segments: list[dict[str, Any]],
    normalize_orientation_mode: str = ORIENTATION_NORMALIZE_MODE_PRESERVE,
) -> list[dict[str, Any]]:
    if not segments:
        return []

    grouped: dict[str, list[dict[str, Any]]] = {}
    for segment in segments:
        group_key = str(segment.get("parcel_group_id") or segment.get("parcel_id") or "parcel-001").strip()
        grouped.setdefault(group_key, []).append(segment)

    polygons: list[dict[str, Any]] = []
    for index, (group_key, group_segments) in enumerate(grouped.items(), start=1):
        ordered = sorted(group_segments, key=lambda segment: int(segment.get("segment_order") or segment.get("segment_index") or 0))
        if len(ordered) < 3:
            continue

        coords: list[tuple[float, float]] = []
        first = ordered[0]
        start = first.get("start")
        if not start:
            continue
        coords.append((float(start[0]), float(start[1])))
        for segment in ordered:
            end = segment.get("end")
            if not end:
                coords = []
                break
            coords.append((float(end[0]), float(end[1])))

        ring = _polygon_ring_from_coords(coords)
        if not ring:
            continue
        ring = _normalize_ring_orientation(ring, normalize_orientation_mode)

        first_segment = ordered[0]
        parcel_id = first_segment.get("parcel_id") or group_key or f"parcel-{index:03d}"
        polygons.append(
            {
                "polygon_index": index,
                "polygon_order": index,
                "parcel_id": parcel_id,
                "parcel_name": first_segment.get("parcel_name") or parcel_id,
                "name": first_segment.get("parcel_name") or parcel_id,
                "parcel_group_id": group_key or parcel_id,
                "coordinates": ring,
                "point_count": max(0, len(ring) - 1),
                "perimeter_m": _ring_perimeter(ring),
                "area_sq_m": abs(_ring_area(ring)),
                "closure_status": "closed",
                "doc_type_id": first_segment.get("doc_type_id") or "",
                "source_doc": first_segment.get("source_doc") or "",
                "status": first_segment.get("status") or "",
                "status_txt": first_segment.get("status_txt") or first_segment.get("status") or "",
                "source_evidence": first_segment.get("source_evidence") or "Reviewed boundary segments",
                "source_txt": first_segment.get("source_txt") or first_segment.get("source_evidence") or "Reviewed boundary segments",
                "is_manual": any(bool(segment.get("is_manual")) for segment in ordered),
                "is_edited": True,
                "geometry_source": "reviewed_boundary_segments",
            }
        )

    return polygons


def _polygon_rings(
    point_groups: list[dict[str, Any]],
    normalize_orientation_mode: str = ORIENTATION_NORMALIZE_MODE_PRESERVE,
) -> list[dict[str, Any]]:
    polygons: list[dict[str, Any]] = []
    for index, group in enumerate(point_groups, start=1):
        group_points = group.get("points") or []
        coords = _polygon_points(group_points)
        if not coords:
            continue
        coords = _normalize_ring_orientation(coords, normalize_orientation_mode)
        first_point = group_points[0] if group_points else {}
        polygons.append(
            {
                "polygon_index": index,
                "polygon_order": index,
                "parcel_id": group.get("parcel_id") or f"parcel-{index:03d}",
                "parcel_name": group.get("parcel_name") or group.get("parcel_id") or f"parcel-{index:03d}",
                "name": group.get("parcel_name") or group.get("parcel_id") or f"parcel-{index:03d}",
                "parcel_group_id": group.get("group_id") or f"parcel-{index}",
                "coordinates": coords,
                "point_count": len(group_points),
                "perimeter_m": _ring_perimeter(coords),
                "area_sq_m": abs(_ring_area(coords)),
                "closure_status": "closed",
                "doc_type_id": first_point.get("doc_type_id") or "",
                "source_doc": first_point.get("source_doc") or "",
                "status": first_point.get("status") or "",
                "status_txt": first_point.get("status") or "",
                "source_evidence": first_point.get("source_evidence") or "",
                "source_txt": first_point.get("source_evidence") or "",
                "is_manual": any(bool(point.get("is_manual")) for point in group_points),
                "is_edited": any(bool(point.get("is_edited")) for point in group_points),
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


def _ring_perimeter(coords: list[tuple[float, float]]) -> float:
    if len(coords) < 2:
        return 0.0

    perimeter = 0.0
    for index in range(len(coords) - 1):
        x1, y1 = coords[index]
        x2, y2 = coords[index + 1]
        perimeter += _distance_between(x1, y1, x2, y2)
    return perimeter


def _distance_between(x1: float, y1: float, x2: float, y2: float) -> float:
    return math.hypot(x2 - x1, y2 - y1)


def _compute_azimuth_deg(x1: float, y1: float, x2: float, y2: float) -> float | None:
    dx = x2 - x1
    dy = y2 - y1
    if math.isclose(dx, 0.0, abs_tol=1e-12) and math.isclose(dy, 0.0, abs_tol=1e-12):
        return None

    azimuth = math.degrees(math.atan2(dx, dy))
    return (azimuth + 360.0) % 360.0


def _degrees_to_dms(value: float) -> tuple[int, int, int]:
    total_seconds = int(round(abs(value) * 3600.0))
    degrees = total_seconds // 3600
    minutes = (total_seconds % 3600) // 60
    seconds = total_seconds % 60

    if seconds == 60:
        seconds = 0
        minutes += 1
    if minutes == 60:
        minutes = 0
        degrees += 1

    return degrees, minutes, seconds


def _azimuth_to_bearing_text(azimuth_deg: float | None) -> str:
    if azimuth_deg is None:
        return ""

    azimuth = azimuth_deg % 360.0
    if azimuth <= 90.0:
        prefix, suffix, angle = "N", "E", azimuth
    elif azimuth <= 180.0:
        prefix, suffix, angle = "S", "E", 180.0 - azimuth
    elif azimuth <= 270.0:
        prefix, suffix, angle = "S", "W", azimuth - 180.0
    else:
        prefix, suffix, angle = "N", "W", 360.0 - azimuth

    degrees, minutes, seconds = _degrees_to_dms(angle)
    return f"{prefix}{degrees}\u00b0{minutes:02d}'{seconds:02d}\"{suffix}"


def _format_distance_text(distance_m: float | None) -> str:
    if distance_m is None:
        return ""

    formatted = f"{distance_m:.3f}".rstrip("0").rstrip(".")
    return formatted


def _pick_cogo_value(source_value: Any, computed_value: Any, mode: str) -> Any:
    source_present = source_value is not None and str(source_value).strip() != ""
    computed_present = computed_value is not None and str(computed_value).strip() != ""

    if mode == COGO_SOURCE_MODE_PREFER_COMPUTED:
        if computed_present:
            return computed_value
        return source_value

    if source_present:
        return source_value
    if computed_present:
        return computed_value
    return source_value


def _copy_without_keys(row: dict[str, Any], keys: set[str]) -> dict[str, Any]:
    return {key: value for key, value in row.items() if key not in keys}


def _prepare_optional_output_cogo(
    points: list[dict[str, Any]],
    segments: list[dict[str, Any]],
    polygons: list[dict[str, Any]],
    review_workspace_mode: str,
    add_cogo_attributes: bool,
    cogo_source_mode: str,
) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    if not add_cogo_attributes:
        point_keys = {"length_txt", "distance_txt"}
        line_keys = {
            "bearing_txt",
            "distance_m",
            "distance_txt",
            "length_txt",
            "radius_m",
            "arc_length_m",
            "delta_angle_txt",
            "chord_bearing_txt",
            "chord_distance_m",
            "azimuth_deg",
            "is_computed_cogo",
        }
        return (
            [_copy_without_keys(point, point_keys) for point in points],
            [_copy_without_keys(segment, line_keys) for segment in segments],
            polygons,
        )

    enriched_points = [dict(point) for point in points]
    enriched_segments: list[dict[str, Any]] = []
    for segment in segments:
        updated = dict(segment)
        source_distance_m = _parse_coordinate(segment.get("distance_m"))
        computed_distance_m = _distance_between(
            float(segment["start"][0]),
            float(segment["start"][1]),
            float(segment["end"][0]),
            float(segment["end"][1]),
        )
        distance_m = _pick_cogo_value(source_distance_m, computed_distance_m, cogo_source_mode)
        source_distance_txt = _normalize_text(segment.get("distance_txt") or segment.get("length_txt") or segment.get("length") or "", 64)
        computed_distance_txt = _format_distance_text(_parse_coordinate(distance_m))
        source_bearing_txt = _normalize_text(segment.get("bearing_txt") or segment.get("bearing") or "", 64)
        azimuth_deg = None
        computed_bearing_txt = ""
        if str(segment.get("line_type") or "").strip().lower() != "curve":
            azimuth_deg = _compute_azimuth_deg(
                float(segment["start"][0]),
                float(segment["start"][1]),
                float(segment["end"][0]),
                float(segment["end"][1]),
            )
            computed_bearing_txt = _azimuth_to_bearing_text(azimuth_deg)

        bearing_txt = _pick_cogo_value(source_bearing_txt, computed_bearing_txt, cogo_source_mode)
        distance_txt = _pick_cogo_value(source_distance_txt, computed_distance_txt, cogo_source_mode)
        used_computed = (
            (not source_distance_m and distance_m is not None)
            or (not source_distance_txt and str(distance_txt or "").strip() != "")
            or (not source_bearing_txt and str(bearing_txt or "").strip() != "")
        )

        updated["distance_m"] = _parse_coordinate(distance_m)
        updated["distance_txt"] = _normalize_text(distance_txt or "", 64)
        updated["length_txt"] = _normalize_text(updated.get("length_txt") or updated.get("length") or updated.get("distance_txt") or "", 128)
        updated["bearing_txt"] = _normalize_text(bearing_txt or "", 64)
        updated["azimuth_deg"] = azimuth_deg
        updated["is_computed_cogo"] = bool(used_computed)
        enriched_segments.append(updated)

    return enriched_points, enriched_segments, polygons


def _derive_output_metadata(
    manifest: dict[str, Any],
    approved_review: dict[str, Any],
    review_data: dict[str, Any],
    review_result_owner: str,
    transaction_number: str,
) -> dict[str, str]:
    transaction_id = _normalize_text(
        manifest.get("transaction_id")
        or approved_review.get("transaction_id")
        or review_data.get("transaction_id")
        or transaction_number,
        64,
    )
    transaction_type = _normalize_text(
        approved_review.get("transaction_type")
        or review_data.get("transaction_type")
        or manifest.get("transaction_type")
        or "",
        128,
    )
    source_mode = _normalize_text(
        review_data.get("source_mode")
        or review_data.get("extraction_mode")
        or review_data.get("doc_type_family")
        or ("manual_review" if review_result_owner == REVIEW_RESULT_OWNER_MANUAL else "validated_review"),
        64,
    )
    return {
        "transaction_number": _normalize_text(transaction_number, 64),
        "transaction_id": transaction_id,
        "workflow_name": "parcel_workflow_compute",
        "workflow_stage": "spatial_units_created",
        "transaction_type": transaction_type,
        "review_state": "manual_edit" if review_result_owner == REVIEW_RESULT_OWNER_MANUAL else "approved",
        "source_mode": source_mode,
    }


def _build_geojson(points: list[dict[str, Any]], segments: list[dict[str, Any]], polygons: list[dict[str, Any]]) -> dict[str, Any]:
    features: list[dict[str, Any]] = []

    for point in points:
        features.append(
            {
                "type": "Feature",
                "geometry": {"type": "Point", "coordinates": [point["easting"], point["northing"]]},
                "properties": {
                    "row_id": point["row_id"],
                    "parcel_id": point.get("parcel_id") or "",
                    "parcel_group_id": point.get("parcel_group_id") or "",
                    "parcel_name": point.get("parcel_name") or "",
                    "traverse_id": point.get("traverse_id") or "",
                    "point_order": point.get("point_order"),
                    "sequence_in_group": point.get("sequence_in_group"),
                    "is_boundary_break": point.get("is_boundary_break") or False,
                    "group_confidence": point.get("group_confidence") or "",
                "point_identifier": point["point_identifier"],
                    "point_id": point.get("point_id") or point["point_identifier"],
                    "point_role": point.get("point_role") or "",
                    "from_segment": point.get("from_segment"),
                    "doc_type_id": point.get("doc_type_id") or "",
                    "source_doc": point.get("source_doc") or "",
                    "status": point["status"],
                    "length": point["length"],
                    "distance_txt": point.get("distance_txt") or "",
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
                    "line_id": segment.get("line_id") or "",
                    "segment_index": segment["segment_index"],
                    "segment_order": segment.get("segment_order"),
                    "parcel_id": segment.get("parcel_id") or "",
                    "parcel_group_id": segment.get("parcel_group_id") or "",
                    "traverse_id": segment.get("traverse_id") or "",
                    "start_point": segment["start_point"],
                    "end_point": segment["end_point"],
                    "from_point_id": segment.get("from_point_id") or segment["start_point"],
                    "to_point_id": segment.get("to_point_id") or segment["end_point"],
                    "line_type": segment.get("line_type") or "",
                    "bearing": segment.get("bearing") or "",
                    "length": segment["length"],
                    "distance_txt": segment.get("distance_txt") or "",
                    "distance_m": segment.get("distance_m"),
                    "radius_m": segment.get("radius_m"),
                    "arc_length_m": segment.get("arc_length_m"),
                    "delta_angle_txt": segment.get("delta_angle_txt") or "",
                    "chord_bearing_txt": segment.get("chord_bearing_txt") or "",
                    "chord_distance_m": segment.get("chord_distance_m"),
                    "doc_type_id": segment.get("doc_type_id") or "",
                    "source_doc": segment.get("source_doc") or "",
                },
            }
        )

    for polygon in polygons:
        features.append(
            {
                "type": "Feature",
                "geometry": {"type": "Polygon", "coordinates": [[list(coord) for coord in polygon["coordinates"]]]},
                "properties": {
                    "parcel_id": polygon.get("parcel_id") or f"parcel-{polygon['polygon_index']:03d}",
                    "parcel_name": polygon.get("parcel_name") or f"parcel-{polygon['polygon_index']:03d}",
                    "name": polygon.get("parcel_name") or f"parcel_polygon_{polygon['polygon_index']}",
                    "property_name": polygon.get("property_name") or polygon.get("propertyName") or "",
                    "propertyName": polygon.get("propertyName") or polygon.get("property_name") or "",
                    "parcel_group_id": polygon.get("parcel_group_id") or "",
                    "polygon_order": polygon.get("polygon_order"),
                    "point_count": polygon.get("point_count"),
                    "perimeter_m": polygon.get("perimeter_m"),
                    "area_sq_m": polygon.get("area_sq_m"),
                    "closure_status": polygon.get("closure_status") or "",
                    "doc_type_id": polygon.get("doc_type_id") or "",
                    "source_doc": polygon.get("source_doc") or "",
                },
            }
        )

    return {
        "type": "FeatureCollection",
        "name": "extracted_geometry_jad2001",
        "crs": {
            "type": "name",
            "properties": {
                "name": f"EPSG:{JAD2001_WKID}",
                "wkid": JAD2001_WKID,
                "latestWkid": JAD2001_LATEST_WKID,
                "coordinateSystem": JAD2001_NAME,
            },
        },
        "spatialReference": {"wkid": JAD2001_WKID, "latestWkid": JAD2001_LATEST_WKID},
        "features": features,
    }


def _count_populated_value(value: Any) -> bool:
    if value is None:
        return False
    if isinstance(value, str):
        return bool(value.strip())
    return True


def _build_field_population_record(field_name: str, exists: bool, populated_count: int) -> dict[str, Any]:
    return {
        "field_name": field_name,
        "exists": bool(exists),
        "populated_count": int(populated_count),
    }


def _inspect_json_feature_rows(feature_class_path: Path, field_names: list[str]) -> dict[str, Any]:
    if not feature_class_path.exists():
        return {
            "feature_class_path": str(feature_class_path),
            "exists": False,
            "row_count": 0,
            "fields": [_build_field_population_record(field_name, False, 0) for field_name in field_names],
        }

    rows = _read_json(feature_class_path)
    if not isinstance(rows, list):
        rows = []

    available_fields: set[str] = set()
    for row in rows:
        if isinstance(row, dict):
            available_fields.update(row.keys())

    field_records = []
    for field_name in field_names:
        populated_count = 0
        if field_name in available_fields:
            for row in rows:
                if isinstance(row, dict) and _count_populated_value(row.get(field_name)):
                    populated_count += 1

        field_records.append(_build_field_population_record(field_name, field_name in available_fields, populated_count))

    return {
        "feature_class_path": str(feature_class_path),
        "exists": True,
        "row_count": len(rows),
        "fields": field_records,
    }


def _inspect_arcpy_feature_class(arcpy: Any, feature_class_path: str | None, field_names: list[str]) -> dict[str, Any]:
    if not feature_class_path:
        return {
            "feature_class_path": feature_class_path,
            "exists": False,
            "row_count": 0,
            "fields": [_build_field_population_record(field_name, False, 0) for field_name in field_names],
        }

    if not arcpy.Exists(feature_class_path):
        return {
            "feature_class_path": feature_class_path,
            "exists": False,
            "row_count": 0,
            "fields": [_build_field_population_record(field_name, False, 0) for field_name in field_names],
        }

    available_fields = {field.name for field in arcpy.ListFields(feature_class_path)}
    row_count = int(arcpy.management.GetCount(feature_class_path)[0])

    present_field_names = [field_name for field_name in field_names if field_name in available_fields]
    populated_counts = {field_name: 0 for field_name in present_field_names}
    if present_field_names:
        with arcpy.da.SearchCursor(feature_class_path, present_field_names) as cursor:
            for row in cursor:
                for index, field_name in enumerate(present_field_names):
                    if _count_populated_value(row[index]):
                        populated_counts[field_name] += 1

    return {
        "feature_class_path": feature_class_path,
        "exists": True,
        "row_count": row_count,
        "fields": [
            _build_field_population_record(field_name, field_name in available_fields, populated_counts.get(field_name, 0))
            for field_name in field_names
        ],
    }


def _inspect_feature_class_diagnostics(
    arcpy: Any,
    feature_class_path: str | None,
    field_names: list[str],
) -> dict[str, Any]:
    if not feature_class_path:
        return {
            "feature_class_path": feature_class_path,
            "exists": False,
            "row_count": 0,
            "fields": [_build_field_population_record(field_name, False, 0) for field_name in field_names],
        }

    path = Path(feature_class_path)
    if path.exists() and path.is_file():
        return _inspect_json_feature_rows(path, field_names)

    if arcpy is not None:
        return _inspect_arcpy_feature_class(arcpy, feature_class_path, field_names)

    return {
        "feature_class_path": feature_class_path,
        "exists": False,
        "row_count": 0,
        "fields": [_build_field_population_record(field_name, False, 0) for field_name in field_names],
    }


def _field_record_value(diagnostic: dict[str, Any] | None, field_name: str, property_name: str) -> Any:
    if not diagnostic:
        return None

    for field_record in diagnostic.get("fields") or []:
        if str(field_record.get("field_name") or "").strip().lower() == field_name.strip().lower():
            return field_record.get(property_name)

    return None


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


def _parse_allowed_cad_layers(value: str | None) -> set[str]:
    if not value:
        return set()

    allowed: set[str] = set()
    for token in str(value).replace(";", ",").replace("|", ",").split(","):
        normalized = token.strip().lower()
        if normalized:
            allowed.add(normalized)
    return allowed


def _is_allowed_cad_layer(layer_name: Any, allowed_layers: set[str]) -> bool:
    if not allowed_layers:
        return True

    normalized = str(layer_name or "").strip().lower()
    return normalized in allowed_layers


def _record_name(transaction_number: str) -> str:
    return f"{PARCEL_FABRIC_RECORD_PREFIX}-{transaction_number}"


def _arcade_string_literal(value: str) -> str:
    escaped = (value or "").replace("\\", "\\\\").replace("'", "\\'")
    return f"'{escaped}'"


def _append_features(arcpy, source: str, target: str) -> None:
    arcpy.management.Append([source], target, "NO_TEST")


_FABRIC_LINE_COGO_FIELDS: tuple[tuple[str, str, int | None], ...] = (
    ("bearing_txt", "TEXT", 64),
    ("distance_txt", "TEXT", 64),
    ("length_txt", "TEXT", 128),
    ("distance_m", "DOUBLE", None),
)


def _dataset_field_names(arcpy, dataset: str) -> set[str]:
    return {field.name.lower() for field in arcpy.ListFields(dataset)}


def _ensure_fabric_line_cogo_fields(arcpy, target_line_fc: str) -> None:
    existing = _dataset_field_names(arcpy, target_line_fc)
    for field_name, field_type, field_length in _FABRIC_LINE_COGO_FIELDS:
        if field_name.lower() in existing:
            continue
        if field_length is None:
            arcpy.management.AddField(target_line_fc, field_name, field_type)
        else:
            arcpy.management.AddField(target_line_fc, field_name, field_type, field_length=field_length)


def _copy_cogo_fields_to_fabric_lines(arcpy, source_line_fc: str | None, target_line_fc: str | None, warnings: list[str] | None = None) -> None:
    if not source_line_fc or not target_line_fc:
        return

    required_source_fields = [field[0] for field in _FABRIC_LINE_COGO_FIELDS]
    source_fields = _dataset_field_names(arcpy, source_line_fc)
    missing_source_fields = [field_name for field_name in required_source_fields if field_name.lower() not in source_fields]
    if missing_source_fields:
        if warnings is not None:
            warnings.append(
                "Parcel Fabric line COGO enrichment skipped because source parcel_lines is missing "
                + ", ".join(missing_source_fields)
                + "."
            )
        return

    _ensure_fabric_line_cogo_fields(arcpy, target_line_fc)

    with arcpy.da.SearchCursor(source_line_fc, required_source_fields) as cursor:
        values = [tuple(row) for row in cursor]
    if not values:
        return

    copied_count = 0
    with arcpy.da.UpdateCursor(target_line_fc, required_source_fields) as cursor:
        for index, row in enumerate(cursor):
            if index >= len(values):
                break
            updated = list(row)
            for field_index, value in enumerate(values[index]):
                updated[field_index] = value
            cursor.updateRow(updated)
            copied_count += 1

    if warnings is not None and copied_count != len(values):
        warnings.append(
            "Parcel Fabric line COGO enrichment copied "
            f"{copied_count} of {len(values)} source line row(s); source and fabric line counts differ."
        )


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
    warnings: list[str] | None = None,
) -> tuple[dict[str, str | None], dict[str, Any]]:
    fabric_dataset = target_gdb / PARCEL_FABRIC_DATASET_NAME
    if arcpy.Exists(str(fabric_dataset)):
        arcpy.management.Delete(str(fabric_dataset))

    spatial_reference = None
    if root_paths.get("polygon_fc"):
        polygon_description = arcpy.Describe(root_paths["polygon_fc"])
        spatial_reference = getattr(polygon_description, "spatialReference", None)
    elif root_paths.get("point_fc"):
        point_description = arcpy.Describe(root_paths["point_fc"])
        spatial_reference = getattr(point_description, "spatialReference", None)
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

    record_name = _record_name(transaction_number)
    if root_paths.get("polygon_fc"):
        print("Parcel fabric step: copying approved polygon into parcel type polygons.")
        _append_features(arcpy, root_paths["polygon_fc"], parcel_polygon_fc)

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
            record_name if root_paths.get("polygon_fc") else None,
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

    _copy_cogo_fields_to_fabric_lines(arcpy, root_paths.get("line_fc"), parcel_line_fc, warnings)

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
            "parcel_record_name": record_name if root_paths.get("polygon_fc") else None,
            "parcel_record_id": None,
            "parcel_type": PARCEL_FABRIC_PARCEL_TYPE_NAME,
            "built_parcel_count": _count_rows(arcpy, parcel_polygon_fc),
            "built_line_count": _count_rows(arcpy, parcel_line_fc),
            "built_point_count": _count_rows(arcpy, parcel_points_fc),
        },
    )


def _load_structured_supplemental_points(
    normalized_points_path: Path | None,
    fallback_points: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    if normalized_points_path is not None and normalized_points_path.exists():
        try:
            document = _read_json(normalized_points_path)
            normalized = _normalize_points(document)
            if normalized:
                return normalized
        except Exception:
            pass

    return [point for point in fallback_points if point.get("easting") is not None and point.get("northing") is not None]


def _create_structured_points_layer_with_arcpy(
    arcpy,
    target_gdb: Path,
    feature_class_path: Path,
    spatial_reference,
    structured_points: list[dict[str, Any]],
    output_metadata: dict[str, str],
) -> str:
    arcpy.management.CreateFeatureclass(str(target_gdb), feature_class_path.name, "POINT", spatial_reference=spatial_reference)
    arcpy.management.AddField(str(feature_class_path), "transaction_number", "TEXT", field_length=64)
    arcpy.management.AddField(str(feature_class_path), "transaction_id", "TEXT", field_length=64)
    arcpy.management.AddField(str(feature_class_path), "point_id", "TEXT", field_length=64)
    arcpy.management.AddField(str(feature_class_path), "parcel_id", "TEXT", field_length=64)
    arcpy.management.AddField(str(feature_class_path), "parcel_group_id", "TEXT", field_length=64)
    arcpy.management.AddField(str(feature_class_path), "easting", "DOUBLE")
    arcpy.management.AddField(str(feature_class_path), "northing", "DOUBLE")
    arcpy.management.AddField(str(feature_class_path), "status_txt", "TEXT", field_length=64)
    arcpy.management.AddField(str(feature_class_path), "source_doc", "TEXT", field_length=256)

    with arcpy.da.InsertCursor(
        str(feature_class_path),
        ["SHAPE@XY", "transaction_number", "transaction_id", "point_id", "parcel_id", "parcel_group_id", "easting", "northing", "status_txt", "source_doc"],
    ) as cursor:
        for index, point in enumerate(structured_points, start=1):
            easting = point.get("easting")
            northing = point.get("northing")
            if easting is None or northing is None:
                continue

            cursor.insertRow(
                [
                    (easting, northing),
                    _normalize_text(output_metadata.get("transaction_number") or "", 64),
                    _normalize_text(output_metadata.get("transaction_id") or "", 64),
                    _normalize_text(point.get("point_id") or point.get("point_identifier") or f"PT-{index:03d}", 64),
                    _normalize_text(point.get("parcel_id") or "", 64),
                    _normalize_text(point.get("parcel_group_id") or point.get("traverse_id") or "", 64),
                    easting,
                    northing,
                    _normalize_text(point.get("status") or point.get("status_txt") or "", 64),
                    _normalize_text(point.get("source_doc") or "", 256),
                ]
            )

    return str(feature_class_path)


def _import_dwg_reference_with_arcpy(
    arcpy,
    target_gdb: Path,
    dataset_name: str,
    dwg_source_path: Path | None,
    spatial_reference,
    allowed_layers: set[str],
    warnings: list[str],
) -> str | None:
    if dwg_source_path is None or not dwg_source_path.exists():
        warnings.append("AutoCAD survey source import was requested, but no DWG source file was available.")
        return None

    target_dataset = target_gdb / dataset_name
    try:
        if arcpy.Exists(str(target_dataset)):
            arcpy.management.Delete(str(target_dataset))

        arcpy.conversion.CADToGeodatabase(
            str(dwg_source_path),
            str(target_gdb),
            dataset_name,
            "1000",
            spatial_reference,
        )

        if arcpy.Exists(str(target_dataset)):
            _filter_cad_reference_layers(arcpy, target_dataset, allowed_layers, warnings)
        return str(target_dataset) if arcpy.Exists(str(target_dataset)) else None
    except Exception as exc:
        warnings.append(f"autocad survey source import requested, but DWG import failed: {exc}")
        return None


def _filter_cad_reference_layers(arcpy, target_dataset: Path, allowed_layers: set[str], warnings: list[str]) -> None:
    if not allowed_layers:
        return

    previous_workspace = arcpy.env.workspace
    deleted_rows = 0
    scanned_feature_classes = 0
    try:
        arcpy.env.workspace = str(target_dataset)
        for feature_class in arcpy.ListFeatureClasses() or []:
            feature_class_path = str(target_dataset / feature_class)
            layer_field = _first_matching_field(arcpy, feature_class_path, ["Layer", "LayerName", "cad_layer"])
            if layer_field is None:
                warnings.append(f"DWG reference layer filter skipped {feature_class}: no CAD layer name field was available.")
                continue

            scanned_feature_classes += 1
            with arcpy.da.UpdateCursor(feature_class_path, [layer_field]) as cursor:
                for row in cursor:
                    if not _is_allowed_cad_layer(row[0], allowed_layers):
                        cursor.deleteRow()
                        deleted_rows += 1
    finally:
        arcpy.env.workspace = previous_workspace

    warnings.append(
        "DWG reference layer filter applied: "
        f"allowed_layers={','.join(sorted(allowed_layers))}; "
        f"feature_classes_scanned={scanned_feature_classes}; "
        f"rows_removed={deleted_rows}."
    )


def _create_outputs_with_arcpy(
    arcpy,
    target_gdb: Path,
    template_gdb: Path | None,
    points: list[dict[str, Any]],
    segments: list[dict[str, Any]],
    polygons: list[dict[str, Any]],
    output_metadata: dict[str, str],
    add_optional_cogo_fields: bool,
    review_workspace_mode: str,
    transaction_number: str,
    import_structured_points: bool,
    import_dwg_reference: bool,
    normalized_points_path: Path | None,
    dwg_source_path: Path | None,
    dwg_allowed_layers: set[str],
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
    polygon_fc = target_gdb / "parcel_polygons"
    structured_points_fc = target_gdb / "survey_point_layer"
    cad_reference_name = "survey_cad_reference"

    for dataset_path in (point_fc, line_fc, polygon_fc, structured_points_fc):
        if arcpy.Exists(str(dataset_path)):
            arcpy.management.Delete(str(dataset_path))

    def add_shared_fields(dataset: Path) -> None:
        arcpy.management.AddField(str(dataset), "transaction_number", "TEXT", field_length=64)
        arcpy.management.AddField(str(dataset), "transaction_id", "TEXT", field_length=64)
        arcpy.management.AddField(str(dataset), "workflow_name", "TEXT", field_length=64)
        arcpy.management.AddField(str(dataset), "workflow_stage", "TEXT", field_length=64)
        arcpy.management.AddField(str(dataset), "transaction_type", "TEXT", field_length=128)
        arcpy.management.AddField(str(dataset), "review_state", "TEXT", field_length=64)
        arcpy.management.AddField(str(dataset), "source_mode", "TEXT", field_length=64)

    shared_values = [
        output_metadata.get("transaction_number") or "",
        output_metadata.get("transaction_id") or "",
        output_metadata.get("workflow_name") or "",
        output_metadata.get("workflow_stage") or "",
        output_metadata.get("transaction_type") or "",
        output_metadata.get("review_state") or "",
        output_metadata.get("source_mode") or "",
    ]

    arcpy.management.CreateFeatureclass(str(target_gdb), point_fc.name, "POINT", spatial_reference=spatial_reference)
    add_shared_fields(point_fc)
    arcpy.management.AddField(str(point_fc), "point_id", "TEXT", field_length=64)
    arcpy.management.AddField(str(point_fc), "parcel_id", "TEXT", field_length=64)
    arcpy.management.AddField(str(point_fc), "parcel_group_id", "TEXT", field_length=64)
    arcpy.management.AddField(str(point_fc), "parcel_grp", "TEXT", field_length=64)
    arcpy.management.AddField(str(point_fc), "parcel_name", "TEXT", field_length=128)
    arcpy.management.AddField(str(point_fc), "traverse_id", "TEXT", field_length=64)
    arcpy.management.AddField(str(point_fc), "sequence_in_group", "LONG")
    arcpy.management.AddField(str(point_fc), "point_ord", "LONG")
    arcpy.management.AddField(str(point_fc), "point_order", "LONG")
    arcpy.management.AddField(str(point_fc), "point_role", "TEXT", field_length=32)
    arcpy.management.AddField(str(point_fc), "from_segment", "LONG")
    arcpy.management.AddField(str(point_fc), "group_confidence", "TEXT", field_length=32)
    arcpy.management.AddField(str(point_fc), "easting", "DOUBLE")
    arcpy.management.AddField(str(point_fc), "northing", "DOUBLE")
    arcpy.management.AddField(str(point_fc), "status_txt", "TEXT", field_length=64)
    if add_optional_cogo_fields:
        arcpy.management.AddField(str(point_fc), "length_txt", "TEXT", field_length=64)
        arcpy.management.AddField(str(point_fc), "distance_txt", "TEXT", field_length=64)
    arcpy.management.AddField(str(point_fc), "doc_type_id", "TEXT", field_length=64)
    arcpy.management.AddField(str(point_fc), "source_doc", "TEXT", field_length=256)
    arcpy.management.AddField(str(point_fc), "is_manual", "SHORT")
    arcpy.management.AddField(str(point_fc), "is_edited", "SHORT")
    arcpy.management.AddField(str(point_fc), "source_txt", "TEXT", field_length=1024)
    arcpy.management.AddField(str(point_fc), "row_id", "TEXT", field_length=64)

    point_cursor_fields = ["SHAPE@XY", "transaction_number", "transaction_id", "workflow_name", "workflow_stage", "transaction_type", "review_state", "source_mode", "point_id", "parcel_id", "parcel_group_id", "parcel_grp", "parcel_name", "traverse_id", "sequence_in_group", "point_ord", "point_order", "point_role", "from_segment", "group_confidence", "easting", "northing", "status_txt"]
    if add_optional_cogo_fields:
        point_cursor_fields.extend(["length_txt", "distance_txt"])
    point_cursor_fields.extend(["doc_type_id", "source_doc", "is_manual", "is_edited", "source_txt", "row_id"])

    with arcpy.da.InsertCursor(str(point_fc), point_cursor_fields) as cursor:
        for point in points:
            is_manual = 1 if point.get("is_manual") else 0
            is_edited = 1 if point.get("is_edited") else 0
            row = [
                (point["easting"], point["northing"]),
                *shared_values,
                _normalize_text(point.get("point_id") or point["point_identifier"], 64),
                _normalize_text(point.get("parcel_id") or "", 64),
                _normalize_text(point.get("parcel_group_id") or point.get("traverse_id") or "", 64),
                _normalize_text(point.get("parcel_group_id") or point.get("traverse_id") or "", 64),
                _normalize_text(point.get("parcel_name") or "", 128),
                _normalize_text(point.get("traverse_id") or "", 64),
                point.get("sequence_in_group"),
                point.get("point_order"),
                point.get("point_order"),
                _normalize_text(point.get("point_role") or "", 32),
                point.get("from_segment"),
                _normalize_text(point.get("group_confidence") or "", 32),
                point["easting"],
                point["northing"],
                _normalize_text(point["status"], 64),
            ]
            if add_optional_cogo_fields:
                row.extend(
                    [
                        _normalize_text(point.get("length_txt") or point.get("length") or "", 64),
                        _normalize_text(point.get("distance_txt") or point.get("length") or "", 64),
                    ]
                )
            row.extend(
                [
                    _normalize_text(point.get("doc_type_id") or "", 64),
                    _normalize_text(point.get("source_doc") or "", 256),
                    is_manual,
                    is_edited,
                    _normalize_text(point["source_evidence"], 1024),
                    _normalize_text(point["row_id"], 64),
                ]
            )
            cursor.insertRow(row)

    created_line_fc: str | None = None
    created_polygon_fc: str | None = None

    if segments:
        arcpy.management.CreateFeatureclass(str(target_gdb), line_fc.name, "POLYLINE", spatial_reference=spatial_reference)
        add_shared_fields(line_fc)
        arcpy.management.AddField(str(line_fc), "line_id", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "parcel_id", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "parcel_group_id", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "traverse_id", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "from_point_id", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "to_point_id", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "start_pt", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "end_pt", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "parcel_grp", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "line_type", "TEXT", field_length=32)
        if add_optional_cogo_fields:
            arcpy.management.AddField(str(line_fc), "bearing_txt", "TEXT", field_length=64)
            arcpy.management.AddField(str(line_fc), "distance_m", "DOUBLE")
            arcpy.management.AddField(str(line_fc), "distance_txt", "TEXT", field_length=64)
            arcpy.management.AddField(str(line_fc), "length_txt", "TEXT", field_length=128)
            arcpy.management.AddField(str(line_fc), "radius_m", "DOUBLE")
            arcpy.management.AddField(str(line_fc), "arc_length_m", "DOUBLE")
            arcpy.management.AddField(str(line_fc), "delta_angle_txt", "TEXT", field_length=64)
            arcpy.management.AddField(str(line_fc), "chord_bearing_txt", "TEXT", field_length=64)
            arcpy.management.AddField(str(line_fc), "chord_distance_m", "DOUBLE")
            arcpy.management.AddField(str(line_fc), "azimuth_deg", "DOUBLE")
            arcpy.management.AddField(str(line_fc), "is_computed_cogo", "SHORT")
        arcpy.management.AddField(str(line_fc), "seg_index", "LONG")
        arcpy.management.AddField(str(line_fc), "seg_order", "LONG")
        arcpy.management.AddField(str(line_fc), "segment_index", "LONG")
        arcpy.management.AddField(str(line_fc), "segment_order", "LONG")
        arcpy.management.AddField(str(line_fc), "doc_type_id", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "source_doc", "TEXT", field_length=256)
        arcpy.management.AddField(str(line_fc), "is_boundary_break", "SHORT")
        arcpy.management.AddField(str(line_fc), "is_boundary", "SHORT")
        arcpy.management.AddField(str(line_fc), "is_manual", "SHORT")
        arcpy.management.AddField(str(line_fc), "is_edited", "SHORT")
        arcpy.management.AddField(str(line_fc), "status_txt", "TEXT", field_length=64)
        arcpy.management.AddField(str(line_fc), "source_txt", "TEXT", field_length=1024)
        arcpy.management.AddField(str(line_fc), "row_id", "TEXT", field_length=64)

        line_cursor_fields = ["SHAPE@", "transaction_number", "transaction_id", "workflow_name", "workflow_stage", "transaction_type", "review_state", "source_mode", "line_id", "parcel_id", "parcel_group_id", "traverse_id", "from_point_id", "to_point_id", "start_pt", "end_pt", "parcel_grp", "line_type"]
        if add_optional_cogo_fields:
            line_cursor_fields.extend(["bearing_txt", "distance_m", "distance_txt", "length_txt", "radius_m", "arc_length_m", "delta_angle_txt", "chord_bearing_txt", "chord_distance_m", "azimuth_deg", "is_computed_cogo"])
        line_cursor_fields.extend(["seg_index", "seg_order", "segment_index", "segment_order", "doc_type_id", "source_doc", "is_boundary_break", "is_boundary", "is_manual", "is_edited", "status_txt", "source_txt", "row_id"])

        with arcpy.da.InsertCursor(str(line_fc), line_cursor_fields) as cursor:
            for segment in segments:
                array = arcpy.Array([arcpy.Point(*segment["start"]), arcpy.Point(*segment["end"])])
                is_boundary_break = 1 if segment.get("is_boundary_break") else 0
                is_manual = 1 if segment.get("is_manual") else 0
                is_edited = 1 if segment.get("is_edited") else 0
                row = [
                    arcpy.Polyline(array, spatial_reference),
                    *shared_values,
                    _normalize_text(segment.get("line_id") or "", 64),
                    _normalize_text(segment.get("parcel_id") or "", 64),
                    _normalize_text(segment.get("parcel_group_id") or "", 64),
                    _normalize_text(segment.get("traverse_id") or "", 64),
                    _normalize_text(segment.get("from_point_id") or segment["start_point"], 64),
                    _normalize_text(segment.get("to_point_id") or segment["end_point"], 64),
                    _normalize_text(segment["start_point"], 64),
                    _normalize_text(segment["end_point"], 64),
                    _normalize_text(segment.get("parcel_group_id") or "", 64),
                    _normalize_text(segment.get("line_type") or "line", 32),
                ]
                if add_optional_cogo_fields:
                    row.extend(
                        [
                            _normalize_text(segment.get("bearing_txt") or segment.get("bearing") or "", 64),
                            segment.get("distance_m"),
                            _normalize_text(segment.get("distance_txt") or "", 64),
                            _normalize_text(segment.get("length_txt") or segment["length"], 128),
                            segment.get("radius_m"),
                            segment.get("arc_length_m"),
                            _normalize_text(segment.get("delta_angle_txt") or "", 64),
                            _normalize_text(segment.get("chord_bearing_txt") or "", 64),
                            segment.get("chord_distance_m"),
                            segment.get("azimuth_deg"),
                            1 if segment.get("is_computed_cogo") else 0,
                        ]
                    )
                row.extend(
                    [
                        segment["segment_index"],
                        segment.get("segment_order"),
                        segment["segment_index"],
                        segment.get("segment_order"),
                        _normalize_text(segment.get("doc_type_id") or "", 64),
                        _normalize_text(segment.get("source_doc") or "", 256),
                        is_boundary_break,
                        is_boundary_break,
                        is_manual,
                        is_edited,
                        _normalize_text(segment.get("status") or "", 64),
                        _normalize_text(segment.get("source_evidence") or "", 1024),
                        _normalize_text(segment.get("row_id") or "", 64),
                    ]
                )
                cursor.insertRow(row)
        created_line_fc = str(line_fc)

    if polygons:
        try:
            arcpy.management.CreateFeatureclass(str(target_gdb), polygon_fc.name, "POLYGON", spatial_reference=spatial_reference)
            add_shared_fields(polygon_fc)
            arcpy.management.AddField(str(polygon_fc), "parcel_id", "TEXT", field_length=64)
            arcpy.management.AddField(str(polygon_fc), "parcel_name", "TEXT", field_length=128)
            arcpy.management.AddField(str(polygon_fc), "name", "TEXT", field_length=128)
            arcpy.management.AddField(str(polygon_fc), "property_name", "TEXT", field_length=128)
            arcpy.management.AddField(str(polygon_fc), "propertyName", "TEXT", field_length=128)
            arcpy.management.AddField(str(polygon_fc), "parcel_group_id", "TEXT", field_length=64)
            arcpy.management.AddField(str(polygon_fc), "parcel_grp", "TEXT", field_length=64)
            arcpy.management.AddField(str(polygon_fc), "polygon_order", "LONG")
            arcpy.management.AddField(str(polygon_fc), "point_cnt", "LONG")
            arcpy.management.AddField(str(polygon_fc), "point_count", "LONG")
            arcpy.management.AddField(str(polygon_fc), "perimeter_m", "DOUBLE")
            arcpy.management.AddField(str(polygon_fc), "area_sq_m", "DOUBLE")
            arcpy.management.AddField(str(polygon_fc), "closure_status", "TEXT", field_length=64)
            arcpy.management.AddField(str(polygon_fc), "doc_type_id", "TEXT", field_length=64)
            arcpy.management.AddField(str(polygon_fc), "source_doc", "TEXT", field_length=256)
            arcpy.management.AddField(str(polygon_fc), "is_manual", "SHORT")
            arcpy.management.AddField(str(polygon_fc), "is_edited", "SHORT")
            arcpy.management.AddField(str(polygon_fc), "status_txt", "TEXT", field_length=64)
            arcpy.management.AddField(str(polygon_fc), "source_txt", "TEXT", field_length=1024)

            with arcpy.da.InsertCursor(str(polygon_fc), ["SHAPE@", "transaction_number", "transaction_id", "workflow_name", "workflow_stage", "transaction_type", "review_state", "source_mode", "parcel_id", "parcel_name", "name", "property_name", "propertyName", "parcel_group_id", "parcel_grp", "polygon_order", "point_cnt", "point_count", "perimeter_m", "area_sq_m", "closure_status", "doc_type_id", "source_doc", "is_manual", "is_edited", "status_txt", "source_txt"]) as cursor:
                for polygon in polygons:
                    array = arcpy.Array([arcpy.Point(*coord) for coord in polygon["coordinates"]])
                    polygon_geometry = arcpy.Polygon(array, spatial_reference)
                    if getattr(polygon_geometry, "isEmpty", False):
                        continue
                    cursor.insertRow(
                        [
                            polygon_geometry,
                            *shared_values,
                            _normalize_text(polygon.get("parcel_id") or f"parcel-{polygon['polygon_index']:03d}", 64),
                            _normalize_text(polygon.get("parcel_name") or f"parcel-{polygon['polygon_index']:03d}", 128),
                            _normalize_text(polygon.get("name") or polygon.get("parcel_name") or f"parcel-{polygon['polygon_index']:03d}", 128),
                            _normalize_text(polygon.get("property_name") or polygon.get("propertyName") or "", 128),
                            _normalize_text(polygon.get("propertyName") or polygon.get("property_name") or "", 128),
                            _normalize_text(polygon.get("parcel_group_id") or "", 64),
                            _normalize_text(polygon.get("parcel_group_id") or "", 64),
                            polygon.get("polygon_order"),
                            polygon.get("point_count"),
                            polygon.get("point_count"),
                            polygon.get("perimeter_m"),
                            polygon.get("area_sq_m"),
                            _normalize_text(polygon.get("closure_status") or "", 64),
                            _normalize_text(polygon.get("doc_type_id") or "", 64),
                            _normalize_text(polygon.get("source_doc") or "", 256),
                            1 if polygon.get("is_manual") else 0,
                            1 if polygon.get("is_edited") else 0,
                            _normalize_text(polygon.get("status") or "", 64),
                            _normalize_text(polygon.get("source_evidence") or "", 1024),
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
        "structured_points_fc": None,
        "cad_reference_path": None,
        "supplemental_layer_paths": [],
    }

    if import_structured_points:
        structured_points_rows = _load_structured_supplemental_points(normalized_points_path, points)
        if structured_points_rows:
            root_paths["structured_points_fc"] = _create_structured_points_layer_with_arcpy(
                arcpy,
                target_gdb,
                structured_points_fc,
                spatial_reference,
                structured_points_rows,
                output_metadata,
            )
            root_paths["supplemental_layer_paths"].append(root_paths["structured_points_fc"])
        else:
            warnings.append("structured survey points import requested, but no usable structured point rows were available.")

    if import_dwg_reference:
        cad_reference_path = _import_dwg_reference_with_arcpy(
            arcpy,
            target_gdb,
            cad_reference_name,
            dwg_source_path,
            spatial_reference,
            dwg_allowed_layers,
            warnings,
        )
        root_paths["cad_reference_path"] = cad_reference_path
        if cad_reference_path:
            root_paths["supplemental_layer_paths"].append(cad_reference_path)

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
            warnings,
        )

    return (root_paths, review_paths | review_metadata, warnings)


def _create_outputs_filesystem_fallback(
    target_gdb: Path,
    points: list[dict[str, Any]],
    segments: list[dict[str, Any]],
    polygons: list[dict[str, Any]],
    output_metadata: dict[str, str],
    review_workspace_mode: str,
    transaction_number: str,
    import_structured_points: bool,
    import_dwg_reference: bool,
) -> tuple[dict[str, str | None], dict[str, str | None], list[str]]:
    target_gdb.mkdir(parents=True, exist_ok=True)
    (target_gdb / "_sidwell_test_mode.txt").write_text("filesystem fallback", encoding="utf-8")

    point_fc = target_gdb / "parcel_points"
    line_fc = target_gdb / "parcel_lines"
    polygon_fc = target_gdb / "parcel_polygons"

    enriched_points = [{**output_metadata, **point} for point in points]
    enriched_segments = [{**output_metadata, **segment} for segment in segments]
    enriched_polygons = [{**output_metadata, **polygon} for polygon in polygons]

    point_fc.write_text(json.dumps(enriched_points, indent=2), encoding="utf-8")
    if segments:
        line_fc.write_text(json.dumps(enriched_segments, indent=2), encoding="utf-8")
    if polygons:
        polygon_fc.write_text(json.dumps(enriched_polygons, indent=2), encoding="utf-8")

    root_paths = {
        "point_fc": str(point_fc),
        "line_fc": str(line_fc) if segments else None,
        "polygon_fc": str(polygon_fc) if polygons else None,
        "structured_points_fc": None,
        "cad_reference_path": None,
        "supplemental_layer_paths": [],
    }
    if import_structured_points:
        survey_point_layer = target_gdb / "survey_point_layer.json"
        survey_point_layer.write_text(json.dumps(points, indent=2), encoding="utf-8")
        root_paths["structured_points_fc"] = str(survey_point_layer)
        root_paths["supplemental_layer_paths"].append(str(survey_point_layer))
    if import_dwg_reference:
        cad_reference_path = target_gdb / "survey_cad_reference"
        cad_reference_path.mkdir(parents=True, exist_ok=True)
        root_paths["cad_reference_path"] = str(cad_reference_path)
        root_paths["supplemental_layer_paths"].append(str(cad_reference_path))
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
    review_result_owner: str,
    add_cogo_attributes: bool,
    add_cogo_labels: bool,
    cogo_source_mode: str,
    payload_bearing_txt_populated_count: int,
    payload_distance_txt_populated_count: int,
    payload_computed_cogo_fallback_line_count: int,
    root_line_feature_class_diagnostic: dict[str, Any] | None,
    review_line_feature_class_diagnostic: dict[str, Any] | None,
    parcel_fabric_mode: str | None,
    parcel_fabric_dataset_path: str | None,
    parcel_fabric_layer_path: str | None,
    parcel_record_name: str | None,
    parcel_record_id: str | None,
    parcel_type: str | None,
    built_parcel_count: int,
    built_line_count: int,
    built_point_count: int,
    property_name: str | None,
    generated_artifact_paths: list[str] | None = None,
) -> dict[str, Any]:
    artifact_paths = [str(geojson_path)]
    if review_paths.get("review_dataset"):
        artifact_paths.append(review_paths["review_dataset"])
    artifact_paths.extend(generated_artifact_paths or [])

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
    active_layer_paths.extend(layer_paths.get("supplemental_layer_paths") or [])

    root_bearing_txt_exists = bool(_field_record_value(root_line_feature_class_diagnostic, "bearing_txt", "exists"))
    root_distance_txt_exists = bool(_field_record_value(root_line_feature_class_diagnostic, "distance_txt", "exists"))
    root_length_txt_exists = bool(_field_record_value(root_line_feature_class_diagnostic, "length_txt", "exists"))
    root_distance_m_exists = bool(_field_record_value(root_line_feature_class_diagnostic, "distance_m", "exists"))
    bearing_txt_populated_count = int(_field_record_value(root_line_feature_class_diagnostic, "bearing_txt", "populated_count") or 0)
    distance_txt_populated_count = int(_field_record_value(root_line_feature_class_diagnostic, "distance_txt", "populated_count") or 0)
    length_txt_populated_count = int(_field_record_value(root_line_feature_class_diagnostic, "length_txt", "populated_count") or 0)
    distance_m_populated_count = int(_field_record_value(root_line_feature_class_diagnostic, "distance_m", "populated_count") or 0)
    map_load_mode = "fabric" if review_workspace_mode == REVIEW_WORKSPACE_MODE_PARCEL_FABRIC else "non_fabric"

    if payload_bearing_txt_populated_count > 0 and (not root_bearing_txt_exists or bearing_txt_populated_count <= 0):
        warnings.append("COGO diagnostic mismatch: payload reported populated bearing text, but root parcel_lines does not expose populated bearing_txt values.")
    if payload_distance_txt_populated_count > 0 and (not root_distance_txt_exists or distance_txt_populated_count <= 0):
        warnings.append("COGO diagnostic mismatch: payload reported populated distance text, but root parcel_lines does not expose populated distance_txt values.")

    payload = {
        "status": "created",
        "review_workspace_mode": review_workspace_mode,
        "map_load_mode": map_load_mode,
        "coordinate_system": JAD2001_NAME,
        "spatial_reference": {"wkid": JAD2001_WKID, "latestWkid": JAD2001_LATEST_WKID},
        "output_epsg": JAD2001_WKID,
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
        "property_name": property_name or None,
        "propertyName": property_name or None,
        "built_parcel_count": built_parcel_count,
        "built_line_count": built_line_count,
        "built_point_count": built_point_count,
        "point_count": len(points),
        "line_count": len(segments),
        "polygon_count": len(polygons),
        "template_project_path": template_project_path or None,
        "template_gdb_path": template_gdb_path or None,
        "review_result_owner": review_result_owner,
        "add_cogo_attributes": add_cogo_attributes,
        "add_cogo_labels": add_cogo_labels,
        "cogo_source_mode": cogo_source_mode,
        "payload_bearing_txt_populated_count": payload_bearing_txt_populated_count,
        "payload_distance_txt_populated_count": payload_distance_txt_populated_count,
        "payload_computed_cogo_fallback_line_count": payload_computed_cogo_fallback_line_count,
        "bearing_txt_populated": bearing_txt_populated_count > 0,
        "bearing_txt_populated_count": bearing_txt_populated_count,
        "distance_txt_populated": distance_txt_populated_count > 0,
        "distance_txt_populated_count": distance_txt_populated_count,
        "computed_cogo_fallback_line_count": payload_computed_cogo_fallback_line_count,
        "root_line_feature_class_diagnostic": root_line_feature_class_diagnostic,
        "review_line_feature_class_diagnostic": review_line_feature_class_diagnostic,
        "root_line_bearing_txt_exists": root_bearing_txt_exists,
        "root_line_distance_txt_exists": root_distance_txt_exists,
        "root_line_length_txt_exists": root_length_txt_exists,
        "root_line_distance_m_exists": root_distance_m_exists,
        "root_line_length_txt_populated_count": length_txt_populated_count,
        "root_line_distance_m_populated_count": distance_m_populated_count,
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
    parser.add_argument("--approved-review")
    parser.add_argument("--review-data", required=True)
    parser.add_argument("--review-workspace-mode", default=REVIEW_WORKSPACE_MODE_NORMAL)
    parser.add_argument("--add-cogo-attributes", default="false")
    parser.add_argument("--add-cogo-labels", default="false")
    parser.add_argument("--cogo-source-mode", default=COGO_SOURCE_MODE_SOURCE_THEN_COMPUTED)
    parser.add_argument("--normalize-orientation-mode", default=ORIENTATION_NORMALIZE_MODE_PRESERVE)
    parser.add_argument("--import-structured-points", default="false")
    parser.add_argument("--import-dwg-reference", default="false")
    parser.add_argument("--normalized-points", default="")
    parser.add_argument("--dwg-source", default="")
    parser.add_argument("--dwg-allowed-layers", default="")
    parser.add_argument("--review-source-route", default=REVIEW_RESULT_OWNER_APPROVED)
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--output-summary", required=True)
    parser.add_argument("--operator")
    parser.add_argument("--template-project")
    parser.add_argument("--template-gdb")
    args = parser.parse_args(argv)

    manifest_path = Path(args.manifest)
    approved_review_path = Path(args.approved_review) if args.approved_review else None
    review_data_path = Path(args.review_data)
    review_workspace_mode = _normalize_review_workspace_mode(args.review_workspace_mode)
    add_cogo_attributes = _normalize_bool_flag(args.add_cogo_attributes, False)
    add_cogo_labels = _normalize_bool_flag(args.add_cogo_labels, False)
    cogo_source_mode = _normalize_cogo_source_mode(args.cogo_source_mode)
    normalize_orientation_mode = _normalize_orientation_mode(args.normalize_orientation_mode)
    import_structured_points = _normalize_bool_flag(args.import_structured_points, False)
    import_dwg_reference = _normalize_bool_flag(args.import_dwg_reference, False)
    normalized_points_path = Path(args.normalized_points) if args.normalized_points else None
    dwg_source_path = Path(args.dwg_source) if args.dwg_source else None
    dwg_allowed_layers = _parse_allowed_cad_layers(args.dwg_allowed_layers)
    review_result_owner = _normalize_review_result_owner(args.review_source_route)
    output_root = Path(args.output_root)
    output_summary_path = Path(args.output_summary)
    template_gdb_path = Path(args.template_gdb) if args.template_gdb else None

    manifest = _read_json(manifest_path)
    approved_review = _read_json(approved_review_path) if approved_review_path and approved_review_path.exists() else {}
    review_data = _read_json(review_data_path)

    if review_result_owner != REVIEW_RESULT_OWNER_MANUAL:
        if approved_review_path is None or not approved_review_path.exists():
            raise RuntimeError("Approved review data is required for output generation.")

        approved_hash = approved_review.get("review_hash")
        review_hash = review_data.get("review_hash")
        if approved_hash and review_hash and str(approved_hash).strip().lower() != str(review_hash).strip().lower():
            raise RuntimeError("Approved review hash does not match current review data.")

    points = _normalize_points(review_data)
    property_name = _resolve_property_name(review_data)
    if not points and review_result_owner != REVIEW_RESULT_OWNER_MANUAL:
        raise RuntimeError("Approved review data does not contain any usable point rows for output generation.")

    point_groups = _apply_group_parcel_metadata(_grouped_point_sequences(points))
    points = [point for group in point_groups for point in (group.get("points") or [])]
    reviewed_segments: list[dict[str, Any]] = []
    if _is_pxa_survey_plan_review(review_data) or _should_rebuild_reviewed_output_from_bearings(review_data):
        constructed_points, constructed_segments = _reviewed_boundary_construction_from_solver(review_data, point_groups)
        if constructed_points and constructed_segments:
            points = constructed_points
            point_groups = _apply_group_parcel_metadata(_grouped_point_sequences(points))
            reviewed_segments = constructed_segments
        else:
            reviewed_segments = _reviewed_boundary_segments(review_data, point_groups)
    segments = reviewed_segments or _polyline_segments(point_groups)
    polygons = _polygon_rings_from_segments(reviewed_segments, normalize_orientation_mode) if reviewed_segments else _polygon_rings(point_groups, normalize_orientation_mode)
    _apply_property_name_to_polygons(polygons, property_name)
    points, segments, polygons = _prepare_optional_output_cogo(
        points,
        segments,
        polygons,
        review_workspace_mode,
        add_cogo_attributes,
        cogo_source_mode,
    )
    output_points = _dedupe_spatial_points_for_output(points)
    output_segments = _dedupe_spatial_segments_for_output(segments)
    transaction_number = review_data.get("transaction_number") or approved_review.get("transaction_number") or manifest.get("transaction_id") or "transaction"
    output_metadata = _derive_output_metadata(
        manifest,
        approved_review,
        review_data,
        review_result_owner,
        str(transaction_number),
    )
    result_gdb_path = output_root / f"{transaction_number}_parcel_output.gdb"
    geojson_path = output_root / "extracted_geometry.geojson"
    output_root.mkdir(parents=True, exist_ok=True)

    arcpy = _load_arcpy()
    if arcpy is not None:
        layer_paths, review_paths, warnings = _create_outputs_with_arcpy(
            arcpy,
            result_gdb_path,
            template_gdb_path,
            output_points,
            output_segments,
            polygons,
            output_metadata,
            add_cogo_attributes,
            review_workspace_mode,
            str(transaction_number),
            import_structured_points,
            import_dwg_reference,
            normalized_points_path,
            dwg_source_path,
            dwg_allowed_layers,
        )
    elif os.environ.get("SIDWELL_OUTPUT_ADAPTER_TEST_MODE", "").strip() == "1":
        layer_paths, review_paths, warnings = _create_outputs_filesystem_fallback(
            result_gdb_path,
            output_points,
            output_segments,
            polygons,
            output_metadata,
            review_workspace_mode,
            str(transaction_number),
            import_structured_points,
            import_dwg_reference,
        )
    else:
        detail = f" {_ARCPY_IMPORT_ERROR}" if _ARCPY_IMPORT_ERROR else ""
        raise RuntimeError(f"ArcPy is not available for output generation.{detail}")

    effective_polygons = polygons if layer_paths.get("polygon_fc") else []
    _write_json(geojson_path, _build_geojson(output_points, output_segments, effective_polygons))
    payload_bearing_txt_populated_count = sum(1 for segment in output_segments if str(segment.get("bearing_txt") or "").strip())
    payload_distance_txt_populated_count = sum(1 for segment in output_segments if str(segment.get("distance_txt") or "").strip())
    payload_computed_cogo_fallback_line_count = sum(1 for segment in output_segments if bool(segment.get("is_computed_cogo")))
    diagnostic_fields = ["bearing_txt", "distance_txt", "length_txt", "distance_m"]
    root_line_feature_class_diagnostic = _inspect_feature_class_diagnostics(arcpy, layer_paths.get("line_fc"), diagnostic_fields)
    review_line_feature_class_diagnostic = _inspect_feature_class_diagnostics(arcpy, review_paths.get("review_line_fc"), diagnostic_fields)
    generated_artifact_paths: list[str] = []
    if _is_pla_plan_annexation(manifest, review_data):
        selected_plan_output = _create_pla_selected_plan_output_pdf(output_root, manifest_path)
        if selected_plan_output is None:
            warnings.append("PLA selected plan page output PDF was not generated; Finalize will remain blocked until the selected source page can be extracted.")
        else:
            generated_artifact_paths.append(str(selected_plan_output))
            generated_artifact_paths.append(
                str(
                    _create_pla_geometry_output_pdf(
                        output_root,
                        manifest_path,
                        manifest,
                        approved_review,
                        review_data,
                        output_points,
                        output_segments,
                        effective_polygons,
                        args.operator,
                        geojson_path,
                        result_gdb_path,
                    )
                )
            )

    summary = _build_summary(
        manifest,
        approved_review,
        output_summary_path,
        result_gdb_path,
        geojson_path,
        layer_paths,
        review_paths,
        output_points,
        output_segments,
        effective_polygons,
        args.operator,
        args.template_project,
        args.template_gdb,
        warnings,
        review_workspace_mode,
        review_result_owner,
        add_cogo_attributes,
        add_cogo_labels,
        cogo_source_mode,
        payload_bearing_txt_populated_count,
        payload_distance_txt_populated_count,
        payload_computed_cogo_fallback_line_count,
        root_line_feature_class_diagnostic,
        review_line_feature_class_diagnostic,
        review_paths.get("parcel_fabric_mode"),
        review_paths.get("parcel_fabric_dataset_path"),
        review_paths.get("parcel_fabric_layer_path"),
        review_paths.get("parcel_record_name"),
        review_paths.get("parcel_record_id"),
        review_paths.get("parcel_type"),
        int(review_paths.get("built_parcel_count") or 0),
        int(review_paths.get("built_line_count") or 0),
        int(review_paths.get("built_point_count") or 0),
        property_name,
        generated_artifact_paths,
    )
    _write_json(output_summary_path, summary)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"Output generation failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
