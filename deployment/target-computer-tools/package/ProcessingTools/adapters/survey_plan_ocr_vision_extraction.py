"""OCR/vision extraction for scanned single-parcel survey plan PDFs.

The add-in calls this helper for PXA survey plans when the PDF has no usable
embedded text layer. The helper renders PDF pages to images, asks the configured
vision provider for structured JSON, and writes the normalized review artifact
used by Georeference, Dimension, and Validate Points and Lines.
"""

from __future__ import annotations

import argparse
import base64
import json
import os
import re
import sys
import tempfile
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any


SCHEMA_VERSION = "2.18.0"
SOURCE_PROFILE = "scanned_single_parcel_survey_plan_pdf"
EXTRACTOR_ID = "survey_plan_ocr_vision"


def _field(
    name: str,
    value: Any,
    confidence: Any = None,
    zone: str = "",
    status: str | None = None,
    note: str | None = None,
    page: int = 1,
) -> dict[str, Any]:
    text = "" if value is None else str(value).strip()
    numeric_confidence = _coerce_float(confidence)
    if numeric_confidence is None:
        numeric_confidence = 0.85 if text else 0.0
    field_status = status or ("extracted" if text else "not_extracted")
    return {
        "field": name,
        "value": text or None,
        "confidence": numeric_confidence,
        "source_page": page,
        "source_zone": zone,
        "status": field_status,
        "review_note": note
        or ("Field was extracted from survey-plan OCR/vision." if text else "Field was not confidently extracted."),
    }


def _normalize_extraction(raw: dict[str, Any], transaction_number: str, source_file: str) -> dict[str, Any]:
    metadata = raw.get("survey_metadata") if isinstance(raw.get("survey_metadata"), dict) else {}
    coordinate_system = raw.get("coordinate_system") or metadata.get("coordinate_system")
    north_arrow_raw = raw.get("north_arrow") if isinstance(raw.get("north_arrow"), dict) else {}
    raw_points = _as_list(raw.get("points")) + _as_list(raw.get("derived_points"))
    points = [_normalize_point(point, index + 1) for index, point in enumerate(_dedupe_points(raw_points))]
    segments = [_normalize_segment(segment, index + 1) for index, segment in enumerate(_as_list(raw.get("segments")))]
    parties = [_normalize_named_item(item) for item in _as_list(raw.get("parties") or raw.get("owners"))]
    representatives = [_normalize_named_item(item) for item in _as_list(raw.get("representatives"))]
    adjacent_owners = [_normalize_named_item(item) for item in _as_list(raw.get("adjacent_owners"))]

    survey_metadata = {
        "parish": _field("parish", metadata.get("parish") or raw.get("parish"), metadata.get("parish_confidence"), "memorandum"),
        "document_area": _field(
            "document_area",
            metadata.get("document_area") or metadata.get("area") or raw.get("document_area") or raw.get("area"),
            metadata.get("area_confidence") or metadata.get("document_area_confidence"),
            "memorandum",
        ),
        "survey_date": _field("survey_date", metadata.get("survey_date") or raw.get("survey_date"), metadata.get("survey_date_confidence"), "signature_block"),
        "instrument": _field("instrument", metadata.get("instrument") or raw.get("instrument"), metadata.get("instrument_confidence"), "instrument_block"),
        "surveyed_by": _field("surveyed_by", metadata.get("surveyed_by") or raw.get("surveyed_by"), metadata.get("surveyed_by_confidence"), "signature_block"),
        "plan_check_date": _field("plan_check_date", metadata.get("plan_check_date") or raw.get("plan_check_date"), metadata.get("plan_check_date_confidence"), "stamp"),
        "file_reference": _field("file_reference", metadata.get("file_reference") or raw.get("file_reference"), metadata.get("file_reference_confidence"), "plan_header"),
    }

    review_notes: list[str] = []
    review_notes.extend(str(note) for note in _as_list(raw.get("review_notes")) if str(note).strip())
    if not points:
        review_notes.append("No coordinate table rows were confidently extracted; manual point review is required.")
    if not segments:
        review_notes.append("No bearing/distance segment rows were confidently extracted; manual line review is required.")
    if not coordinate_system:
        review_notes.append("Coordinate system was not confidently extracted.")

    status = "review_required" if points or segments or any(field["value"] for field in survey_metadata.values()) else "manual_review_required"
    return {
        "schema_version": SCHEMA_VERSION,
        "transaction_number": transaction_number,
        "source_profile": SOURCE_PROFILE,
        "parcel_count_hint": _coerce_int(raw.get("parcel_count_hint")) or 1,
        "extraction_source": EXTRACTOR_ID,
        "extractor_id": EXTRACTOR_ID,
        "active_extractor_id": EXTRACTOR_ID,
        "provider_used": raw.get("provider_used") or "openai",
        "primary_source_role": "survey_plan_pdf",
        "primary_source_file": source_file,
        "status": status,
        "fallback_reason": None if status == "review_required" else "low_confidence_or_no_vision_rows",
        "coordinate_system": _field("coordinate_system", coordinate_system, raw.get("coordinate_system_confidence"), "plan_header"),
        "north_arrow": {
            "Feature": "north_arrow",
            "Detected": bool(north_arrow_raw.get("detected") or north_arrow_raw.get("Detected")),
            "ApproximatePageLocation": north_arrow_raw.get("approximate_page_location") or north_arrow_raw.get("ApproximatePageLocation"),
            "Confidence": _coerce_float(north_arrow_raw.get("confidence") or north_arrow_raw.get("Confidence")) or 0.0,
            "ReviewNote": north_arrow_raw.get("review_note") or north_arrow_raw.get("ReviewNote") or "North arrow OCR/vision result.",
        },
        "survey_metadata": survey_metadata,
        "parties": parties,
        "representatives": representatives,
        "adjacent_owners": adjacent_owners,
        "field_confidence": {
            "coordinate_system": _coerce_float(raw.get("coordinate_system_confidence")) or (0.85 if coordinate_system else 0.0),
            "parish": survey_metadata["parish"]["confidence"],
            "document_area": survey_metadata["document_area"]["confidence"],
            "survey_date": survey_metadata["survey_date"]["confidence"],
            "instrument": survey_metadata["instrument"]["confidence"],
            "surveyed_by": survey_metadata["surveyed_by"]["confidence"],
        },
        "review_notes": review_notes,
        "row_count": len(points),
        "segment_row_count": len(segments),
        "rows": points,
        "segments": segments,
        "outputs": {},
    }


def _normalize_point(point: Any, sequence: int) -> dict[str, Any]:
    node = point if isinstance(point, dict) else {"point_id": point}
    point_id = str(node.get("point_id") or node.get("point_no") or node.get("point_number") or node.get("id") or sequence).strip()
    parcel_group = str(node.get("parcel_group_id") or node.get("parcel") or "parcel-001").strip()
    parcel_name = str(node.get("parcel_name") or node.get("pid") or node.get("lot_number") or "survey-plan-parcel").strip()
    return {
        "parcel_group_id": parcel_group,
        "parcel_name": parcel_name,
        "point_order": _coerce_int(node.get("point_order") or node.get("sequence")) or sequence,
        "point_identifier": point_id,
        "point_id": point_id,
        "easting": _format_number(node.get("easting") or node.get("east") or node.get("x")),
        "northing": _format_number(node.get("northing") or node.get("north") or node.get("y")),
        "source_page": _coerce_int(node.get("source_page")) or 1,
        "source_zone": node.get("source_zone") or "coordinate_table",
        "confidence": _coerce_float(node.get("confidence")) or 0.85,
        "row_provenance": "survey_plan_ocr_vision",
        "extraction_status": node.get("status") or "matched",
        "review_note": node.get("review_note") or node.get("note"),
    }


def _normalize_segment(segment: Any, sequence: int) -> dict[str, Any]:
    node = segment if isinstance(segment, dict) else {}
    distance = node.get("distance_txt") or node.get("distance") or node.get("length") or node.get("length_m")
    bearing = node.get("bearing_txt") or node.get("bearing") or node.get("course")
    return {
        "segment_no": _coerce_int(node.get("segment_no") or node.get("sequence")) or sequence,
        "from_point": _string_or_none(node.get("from_point") or node.get("from")),
        "to_point": _string_or_none(node.get("to_point") or node.get("to")),
        "bearing_txt": _string_or_none(bearing),
        "distance_txt": _string_or_none(distance),
        "length_txt": _string_or_none(distance),
        "length_m": _coerce_float(distance),
        "source_page": _coerce_int(node.get("source_page")) or 1,
        "source_zone": node.get("source_zone") or "plan_sketch",
        "confidence": _coerce_float(node.get("confidence")) or 0.85,
        "row_provenance": "survey_plan_ocr_vision",
        "extraction_status": node.get("status") or "matched",
        "review_note": node.get("review_note") or node.get("note"),
    }


def _normalize_named_item(item: Any) -> dict[str, Any]:
    if isinstance(item, dict):
        return {
            "name": _string_or_none(item.get("name") or item.get("party") or item.get("owner")),
            "role": _string_or_none(item.get("role")),
            "confidence": _coerce_float(item.get("confidence")) or 0.75,
            "source_page": _coerce_int(item.get("source_page")) or 1,
            "source_zone": item.get("source_zone") or "memorandum",
        }
    return {
        "name": str(item).strip(),
        "role": None,
        "confidence": 0.75,
        "source_page": 1,
        "source_zone": "memorandum",
    }


def _render_pdf_pages(pdf_path: Path, max_pages: int) -> list[Path]:
    temp_dir = Path(tempfile.mkdtemp(prefix="survey_plan_vision_"))
    try:
        return _render_pdf_pages_with_fitz(pdf_path, max_pages, temp_dir)
    except ImportError:
        return _render_pdf_pages_with_pypdfium2(pdf_path, max_pages, temp_dir)
    except Exception as exc:
        raise RuntimeError(f"PyMuPDF/fitz could not render '{pdf_path}': {exc}") from exc


def _render_pdf_pages_with_fitz(pdf_path: Path, max_pages: int, temp_dir: Path) -> list[Path]:
    import fitz  # type: ignore

    output_paths: list[Path] = []
    document = fitz.open(pdf_path)
    try:
        for page_index in range(min(max_pages, len(document))):
            page = document[page_index]
            matrix = fitz.Matrix(2.0, 2.0)
            pixmap = page.get_pixmap(matrix=matrix, alpha=False)
            output_path = temp_dir / f"page_{page_index + 1}.png"
            pixmap.save(output_path)
            output_paths.append(output_path)
    finally:
        document.close()
    return output_paths


def _render_pdf_pages_with_pypdfium2(pdf_path: Path, max_pages: int, temp_dir: Path) -> list[Path]:
    try:
        import pypdfium2 as pdfium  # type: ignore
    except ImportError as exc:
        raise RuntimeError("PDF rendering requires PyMuPDF/fitz or pypdfium2 in the configured Python environment.") from exc

    output_paths: list[Path] = []
    try:
        document = pdfium.PdfDocument(str(pdf_path))
    except Exception as exc:
        raise RuntimeError(f"pypdfium2 could not open '{pdf_path}': {exc}") from exc

    try:
        page_count = len(document)
        for page_index in range(min(max_pages, page_count)):
            try:
                page = document[page_index]
            except Exception as exc:
                raise RuntimeError(f"pypdfium2 could not read page {page_index + 1} from '{pdf_path}': {exc}") from exc

            try:
                bitmap = page.render(scale=2)
                image = bitmap.to_pil()
                output_path = temp_dir / f"page_{page_index + 1}.png"
                image.save(output_path)
                output_paths.append(output_path)
            except Exception as exc:
                raise RuntimeError(f"pypdfium2 could not render page {page_index + 1} from '{pdf_path}': {exc}") from exc
            finally:
                close = getattr(page, "close", None)
                if callable(close):
                    close()
    finally:
        close_document = getattr(document, "close", None)
        if callable(close_document):
            close_document()
    return output_paths


def _call_openai_vision(image_paths: list[Path], model: str, profile: str) -> dict[str, Any]:
    api_key = os.environ.get("OPENAI_API_KEY", "").strip()
    if not api_key:
        raise RuntimeError("OPENAI_API_KEY is not configured for survey-plan OCR/vision extraction.")

    content: list[dict[str, Any]] = [{"type": "text", "text": _prompt(profile)}]
    for image_path in image_paths:
        image_b64 = base64.b64encode(image_path.read_bytes()).decode("ascii")
        content.append({"type": "image_url", "image_url": {"url": f"data:image/png;base64,{image_b64}"}})

    payload = {
        "model": model,
        "messages": [{"role": "user", "content": content}],
        "response_format": {"type": "json_object"},
        "temperature": 0,
        "max_completion_tokens": 4000,
    }
    request = urllib.request.Request(
        "https://api.openai.com/v1/chat/completions",
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            response_payload = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"OpenAI vision request failed with HTTP {exc.code}: {body[:500]}") from exc

    text = response_payload["choices"][0]["message"]["content"]
    return _parse_json_text(text)


def _prompt(profile: str) -> str:
    return (
        "Extract structured cadastral survey plan data from this Jamaica survey plan image. "
        "Return only JSON with keys: coordinate_system, coordinate_system_confidence, "
        "north_arrow {detected, approximate_page_location, confidence, review_note}, "
        "survey_metadata {parish, document_area, survey_date, instrument, surveyed_by, "
        "plan_check_date, file_reference}, parties, representatives, adjacent_owners, "
        "points [{point_id,northing,easting,confidence,source_page,source_zone,status,review_note}], "
        "derived_points [{point_id,northing,easting,confidence,source_page,source_zone,status,review_note}], "
        "segments [{from_point,to_point,bearing_txt,distance_txt,confidence,source_page,source_zone,status,review_note}], "
        "review_notes. Capture every visible boundary point and every visible boundary segment around the parcel. "
        "Use point labels only when the label is visibly attached to the boundary point, course table, or coordinate table entry "
        "for that exact point. Do not invent sequential labels from printed reference labels: if the plan has reference points "
        "A and B but an unlabeled boundary vertex follows A, do not call that vertex B unless B is visibly the same vertex. "
        "When an intermediate boundary vertex is unlabeled but is needed to keep the segment chain continuous, use a temporary "
        "generated label in the opposite style from the visible labels (lettered plans use 1, 2, 3; numbered plans use A, B, C), "
        "set status to review_required, and add review_note 'Generated temporary point label; confirm against visible plan labels.' "
        "If boundary labels are visible on the map, use those visible labels exactly and do not generate replacements. "
        "For bearings, preserve the complete quadrant bearing exactly when readable, including quadrant letters, "
        "degrees, minutes, seconds when present, and final direction, for example S84°56'E or N19°09'E. "
        "Do not return partial bearings such as S84 or N82; use null with a review note if the full bearing is unreadable. "
        "If a boundary point coordinate is not printed but can be calculated from printed anchored coordinates plus "
        "visible bearings and distances, include it in derived_points with status 'derived', confidence at or below 0.65, "
        "and a review_note explaining the derivation. Use null when uncertain. Do not invent values. "
        f"Extraction profile: {profile}."
    )


def _parse_json_text(value: str) -> dict[str, Any]:
    text = value.strip()
    fenced = re.search(r"```(?:json)?\s*(?P<body>.*?)```", text, flags=re.IGNORECASE | re.DOTALL)
    if fenced:
        text = fenced.group("body").strip()
    parsed = json.loads(text)
    if not isinstance(parsed, dict):
        raise RuntimeError("Vision provider returned JSON that was not an object.")
    return parsed


def _load_mock_response() -> dict[str, Any] | None:
    mock_path = os.environ.get("SURVEY_PLAN_OCR_VISION_MOCK_JSON", "").strip()
    if not mock_path:
        return None
    with open(mock_path, "r", encoding="utf-8") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise RuntimeError("SURVEY_PLAN_OCR_VISION_MOCK_JSON must point to a JSON object.")
    return payload


def _fallback_payload(transaction_number: str, source_file: str, reason: str) -> dict[str, Any]:
    payload = _normalize_extraction({}, transaction_number, source_file)
    payload["fallback_reason"] = reason
    payload["review_notes"].insert(0, "OCR/vision extraction did not produce usable data.")
    return payload


def _write_outputs(output_json: Path, review_payload: dict[str, Any], parser_status: str) -> dict[str, Any]:
    output_json.parent.mkdir(parents=True, exist_ok=True)
    review_payload["outputs"] = {"review_json": str(output_json)}
    output_json.write_text(json.dumps(review_payload, indent=2), encoding="utf-8")
    return {
        "status": "success",
        "text_layer_available": False,
        "parser_status": parser_status,
        "fallback_reason": review_payload.get("fallback_reason"),
        "parsed_parcel_count": review_payload.get("parcel_count_hint", 1),
        "parsed_row_count": review_payload.get("row_count", 0),
        "outputs": {"review_json": str(output_json)},
    }


def _as_list(value: Any) -> list[Any]:
    if value is None:
        return []
    if isinstance(value, list):
        return value
    return [value]


def _dedupe_points(points: list[Any]) -> list[Any]:
    seen: set[str] = set()
    deduped: list[Any] = []
    for point in points:
        if isinstance(point, dict):
            point_id = str(point.get("point_id") or point.get("point_no") or point.get("point_number") or point.get("id") or "").strip()
        else:
            point_id = str(point).strip()
        key = point_id.lower()
        if key and key in seen:
            continue
        if key:
            seen.add(key)
        deduped.append(point)
    return deduped


def _coerce_float(value: Any) -> float | None:
    if value is None:
        return None
    try:
        return float(str(value).replace(",", "").replace("m", "").strip())
    except ValueError:
        return None


def _coerce_int(value: Any) -> int | None:
    if value is None:
        return None
    try:
        return int(float(str(value).strip()))
    except ValueError:
        return None


def _format_number(value: Any) -> str | None:
    if value is None:
        return None
    text = str(value).strip().replace(",", "")
    return text or None


def _string_or_none(value: Any) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    return text or None


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Extract scanned survey plan data with OCR/vision.")
    parser.add_argument("--source-pdf", required=True)
    parser.add_argument("--output-json", required=True)
    parser.add_argument("--transaction-number", required=True)
    parser.add_argument("--model", default=os.environ.get("OPENAI_MODEL", "gpt-4.1-mini"))
    parser.add_argument("--profile", default=os.environ.get("OPENAI_EXTRACTION_PROFILE", "balanced"))
    parser.add_argument("--max-pages", type=int, default=2)
    args = parser.parse_args(argv)

    source_pdf = Path(args.source_pdf)
    output_json = Path(args.output_json)
    parser_status = "ocr_vision_parsed"
    try:
        raw = _load_mock_response()
        if raw is None:
            image_paths = _render_pdf_pages(source_pdf, max(1, args.max_pages))
            raw = _call_openai_vision(image_paths, args.model, args.profile)
        review_payload = _normalize_extraction(raw, args.transaction_number, source_pdf.name)
    except Exception as exc:  # Keep workflow reviewable even when the provider is unavailable.
        parser_status = "ocr_vision_unavailable"
        review_payload = _fallback_payload(args.transaction_number, source_pdf.name, str(exc))

    envelope = _write_outputs(output_json, review_payload, parser_status)
    print(json.dumps(envelope))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
