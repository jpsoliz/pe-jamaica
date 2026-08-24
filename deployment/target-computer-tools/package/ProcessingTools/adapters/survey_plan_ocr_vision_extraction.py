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
SEMANTIC_STATES = {
    "VALUE",
    "NONE",
    "N_A",
    "NOT_STATED",
    "NOT_FOUND",
    "ILLEGIBLE",
    "NO_ONE_APPEARED",
    "UNKNOWN",
}
VOLUME_FOLIO_ALIASES = "Vol., Volume, Folio, Fol., Vol/Fol, Volume/Folio, Vol./Fol."
VOLUME_FOLIO_PATTERNS = [
    re.compile(
        r"\b(?:Vol(?:ume)?\.?\s*/\s*Fol(?:io)?\.?|Vol\.\s*/\s*Fol\.?)\s*[:#]?\s*"
        r"(?P<volume>[A-Z0-9][A-Z0-9/-]*)\s*/\s*(?P<folio>[A-Z0-9][A-Z0-9/-]*)\b",
        re.IGNORECASE,
    ),
    re.compile(
        r"\bVol(?:ume)?\.?\s+Fol(?:io)?\.?\s*[:#]?\s*"
        r"(?P<volume>[A-Z0-9][A-Z0-9/-]*)\s*/\s*(?P<folio>[A-Z0-9][A-Z0-9/-]*)\b",
        re.IGNORECASE,
    ),
    re.compile(
        r"\bVol(?:ume)?\.?\s*[:#]?\s*(?P<volume>[A-Z0-9][A-Z0-9/-]*)\s+"
        r"(?:Fol(?:io)?\.?)\s*[:#]?\s*(?P<folio>[A-Z0-9][A-Z0-9/-]*)\b",
        re.IGNORECASE,
    ),
]


def _field(
    name: str,
    value: Any,
    confidence: Any = None,
    zone: str = "",
    status: str | None = None,
    note: str | None = None,
    page: int = 1,
) -> dict[str, Any]:
    node = value if isinstance(value, dict) else {}
    text = _extract_field_text(value)
    raw_value = _extract_raw_value(value, text)
    semantic_state = _resolve_semantic_state(value, text)
    confidence_source = confidence if confidence is not None else node.get("confidence")
    if confidence_source is None:
        confidence_source = node.get("Confidence")
    numeric_confidence = _coerce_float(confidence_source)
    if numeric_confidence is None:
        numeric_confidence = 0.85 if text else 0.0
    field_status = status or ("extracted" if semantic_state in {"VALUE", "NONE", "N_A", "NO_ONE_APPEARED"} else "not_extracted")
    field = {
        "field": name,
        "value": text or None,
        "raw_value": raw_value,
        "normalized_value": text or None,
        "semantic_state": semantic_state,
        "confidence": numeric_confidence,
        "source_page": _coerce_int(node.get("source_page") or node.get("page")) or page,
        "source_zone": node.get("source_zone") or node.get("zone") or zone,
        "status": field_status,
        "review_status": node.get("review_status") or field_status,
        "review_note": note
        or node.get("review_note")
        or node.get("review_notes")
        or ("Field was extracted from survey-plan OCR/vision." if text else "Field was not confidently extracted."),
    }
    if name == "document_area":
        area = _parse_area_value(raw_value or text)
        if area:
            field["numeric_value"] = area["value"]
            field["unit"] = area["unit"]
        elif semantic_state == "VALUE":
            field["review_status"] = "needs_review"
            field["review_note"] = "Area text was captured but numeric value/unit could not be parsed deterministically."
    if name == "surveyed_by" and isinstance(value, dict):
        field["title"] = _string_or_none(value.get("title") or value.get("surveyor_title"))
        field["organization"] = _string_or_none(value.get("organization") or value.get("company") or value.get("surveyor_organization"))
    candidates = node.get("candidates")
    if isinstance(candidates, list):
        field["candidates"] = candidates
        if len(candidates) > 1 and not node.get("review_status"):
            field["review_status"] = "needs_review"
    return field


def _normalize_extraction(raw: dict[str, Any], transaction_number: str, source_file: str) -> dict[str, Any]:
    metadata = raw.get("survey_metadata") if isinstance(raw.get("survey_metadata"), dict) else {}
    memorandum = _normalize_memorandum_section(raw)
    coordinate_system = raw.get("coordinate_system") or metadata.get("coordinate_system")
    north_arrow_raw = raw.get("north_arrow") if isinstance(raw.get("north_arrow"), dict) else {}
    scale_bar_raw = raw.get("scale_bar") if isinstance(raw.get("scale_bar"), dict) else {}
    scale_bar_text_detected = _has_scale_bar_text(raw)
    scale_bar_text = _extract_scale_bar_text(raw) if scale_bar_text_detected else None
    scale_bar_detected = bool(scale_bar_raw.get("detected") or scale_bar_raw.get("Detected") or scale_bar_raw.get("present"))
    instrument_check_parts = _split_instrument_check(
        metadata.get("instrument_check")
        or raw.get("instrument_check")
        or metadata.get("date_of_last_instr_check_result")
        or raw.get("date_of_last_instr_check_result")
    )
    raw_points = _as_list(raw.get("points")) + _as_list(raw.get("derived_points"))
    points = [_normalize_point(point, index + 1) for index, point in enumerate(_dedupe_points(raw_points))]
    segments = [_normalize_segment(segment, index + 1) for index, segment in enumerate(_as_list(raw.get("segments")))]
    parties = [_normalize_named_item(item) for item in _as_list(raw.get("parties") or raw.get("owners"))]
    representatives = [_normalize_named_item(item) for item in _as_list(raw.get("representatives"))]
    adjacent_owners = [_normalize_named_item(item) for item in _as_list(raw.get("adjacent_owners"))]
    volume_folios = [
        item
        for item in (
            _normalize_volume_folio_item(item)
            for item in _as_list(
                metadata.get("volume_folio")
                or metadata.get("volume_folios")
                or raw.get("volume_folio")
                or raw.get("volume_folios")
            )
        )
        if item
    ]

    survey_metadata = {
        "parish": _field("parish", _first_present(metadata, raw, "parish"), metadata.get("parish_confidence"), "memorandum"),
        "document_area": _field(
            "document_area",
            _first_present(metadata, raw, "document_area", "area"),
            _first_present(metadata, {}, "area_confidence", "document_area_confidence"),
            "memorandum",
        ),
        "survey_date": _field("survey_date", _first_present(metadata, raw, "survey_date"), metadata.get("survey_date_confidence"), "signature_block"),
        "survey_method": _field("survey_method", _first_present(metadata, raw, "survey_method"), metadata.get("survey_method_confidence"), "memorandum"),
        "grounds_of_objection": _field(
            "grounds_of_objection",
            _first_present(metadata, raw, "grounds_of_objection", "grounds_of_objections"),
            metadata.get("grounds_of_objection_confidence"),
            "memorandum",
        ),
        "surveyor_decision_grounds": _field(
            "surveyor_decision_grounds",
            _first_present(metadata, raw, "surveyor_decision_grounds", "grounds_of_surveyor_decision"),
            metadata.get("surveyor_decision_grounds_confidence"),
            "memorandum",
        ),
        "instrument": _field("instrument", _first_present(metadata, raw, "instrument"), metadata.get("instrument_confidence"), "instrument_block"),
        "instrument_check_date": _field(
            "instrument_check_date",
            _first_present(metadata, raw, "instrument_check_date") if _first_present(metadata, raw, "instrument_check_date") is not None else instrument_check_parts.get("date"),
            metadata.get("instrument_check_date_confidence"),
            "instrument_block",
        ),
        "instrument_check_result": _field(
            "instrument_check_result",
            metadata.get("instrument_check_result") or raw.get("instrument_check_result") or instrument_check_parts.get("result"),
            metadata.get("instrument_check_result_confidence"),
            "instrument_block",
        ),
        "gps_instrument_number": _field(
            "gps_instrument_number",
            metadata.get("gps_instrument_number") or raw.get("gps_instrument_number"),
            metadata.get("gps_instrument_number_confidence"),
            "instrument_block",
        ),
        "gps_serial_number": _field(
            "gps_serial_number",
            metadata.get("gps_serial_number") or raw.get("gps_serial_number") or metadata.get("gps_serial") or raw.get("gps_serial"),
            metadata.get("gps_serial_number_confidence") or metadata.get("gps_serial_confidence"),
            "instrument_block",
        ),
        "surveyed_by": _field("surveyed_by", _first_present(metadata, raw, "surveyed_by"), metadata.get("surveyed_by_confidence"), "signature_block"),
        "plan_check_date": _field("plan_check_date", _first_present(metadata, raw, "plan_check_date"), metadata.get("plan_check_date_confidence"), "stamp"),
        "file_reference": _field("file_reference", _first_present(metadata, raw, "file_reference"), metadata.get("file_reference_confidence"), "plan_header"),
        "volume_folio": volume_folios,
    }

    review_notes: list[str] = []
    review_notes.extend(str(note) for note in _as_list(raw.get("review_notes")) if str(note).strip())
    if not points:
        review_notes.append("No coordinate table rows were confidently extracted; manual point review is required.")
    if not segments:
        review_notes.append("No bearing/distance segment rows were confidently extracted; manual line review is required.")
    if not coordinate_system:
        review_notes.append("Coordinate system was not confidently extracted.")

    status = "review_required" if points or segments or any(_metadata_has_value(field) for field in survey_metadata.values()) else "manual_review_required"
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
        "document_sections": {
            "memorandum": memorandum,
        },
        "north_arrow": {
            "Feature": "north_arrow",
            "Detected": bool(north_arrow_raw.get("detected") or north_arrow_raw.get("Detected")),
            "present": bool(north_arrow_raw.get("detected") or north_arrow_raw.get("Detected")),
            "ApproximatePageLocation": north_arrow_raw.get("approximate_page_location") or north_arrow_raw.get("ApproximatePageLocation"),
            "Confidence": _coerce_float(north_arrow_raw.get("confidence") or north_arrow_raw.get("Confidence")) or 0.0,
            "ReviewNote": north_arrow_raw.get("review_note") or north_arrow_raw.get("ReviewNote") or "North arrow OCR/vision result.",
        },
        "scale_bar": {
            "Feature": "scale_bar",
            "Detected": scale_bar_detected or scale_bar_text_detected,
            "present": scale_bar_detected or scale_bar_text_detected,
            "value": scale_bar_raw.get("value") or scale_bar_raw.get("text") or scale_bar_text,
            "raw_text": scale_bar_raw.get("raw_text") or scale_bar_text,
            "ApproximatePageLocation": scale_bar_raw.get("approximate_page_location") or scale_bar_raw.get("ApproximatePageLocation") or ("scale_bar_text" if scale_bar_text_detected else None),
            "Confidence": _coerce_float(scale_bar_raw.get("confidence") or scale_bar_raw.get("Confidence")) or (0.7 if scale_bar_text_detected else 0.0),
            "ReviewNote": scale_bar_raw.get("review_note")
            or scale_bar_raw.get("ReviewNote")
            or ("Scale bar text detected from memorandum/plan text." if scale_bar_text_detected else "Scale bar OCR/vision result."),
        },
        "survey_metadata": survey_metadata,
        "surveyed_for_names": [_normalize_memorandum_name(item, "surveyed_for") for item in _as_list(raw.get("surveyed_for_names") or raw.get("surveyed_for") or raw.get("party_surveyed_for"))],
        "surveyed_property_names": [_normalize_memorandum_value(item, "surveyed_property_name") for item in _as_list(raw.get("surveyed_property_names") or raw.get("surveyed_property_name") or raw.get("property_name"))],
        "property_name_near_parcel_diagram": _normalize_presence_evidence(raw.get("property_name_near_parcel_diagram"), "property_name_near_parcel_diagram", "parcel_diagram"),
        "notice_served_on": [_normalize_memorandum_name(item, "notice_served_on") for item in _as_list(raw.get("notice_served_on") or raw.get("notices_served_on"))],
        "interested_parties": [_normalize_memorandum_name(item, "interested_party") for item in _as_list(raw.get("interested_parties") or raw.get("parties_interested") or raw.get("parties_served_with_notices"))],
        "appeared_parties": [_normalize_appeared_party(item) for item in _as_list(raw.get("appeared_parties") or raw.get("parties_who_appeared"))],
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
        name = _string_or_none(item.get("name") or item.get("party") or item.get("owner") or item.get("occupant"))
        role = _normalize_role(item.get("role") or item.get("type") or ("Occupant" if item.get("occupant") else None))
        return {
            "name": name,
            "role": role,
            "confidence": _coerce_float(item.get("confidence")) or 0.75,
            "source_page": _coerce_int(item.get("source_page")) or 1,
            "source_zone": item.get("source_zone") or "memorandum",
        }
    text = str(item).strip()
    occupant_match = re.match(r"^Occ\.?\s*[:\-]?\s*(?P<name>.+)$", text, flags=re.IGNORECASE)
    return {
        "name": occupant_match.group("name").strip() if occupant_match else text,
        "role": "Occupant" if occupant_match else None,
        "confidence": 0.75,
        "source_page": 1,
        "source_zone": "memorandum",
    }


def _normalize_memorandum_section(raw: dict[str, Any]) -> dict[str, Any]:
    explicit = raw.get("memorandum")
    source_text = _collect_document_text(raw)
    if isinstance(explicit, dict):
        explicit_detected = bool(explicit.get("detected") or explicit.get("present"))
        matched_text = _string_or_none(explicit.get("matched_text") or explicit.get("text") or ("MEMORANDUM" if explicit_detected else None))
        detected = explicit_detected or "memorandum" in " ".join([source_text, matched_text or ""]).lower()
        if detected and not matched_text:
            matched_text = "MEMORANDUM"
        source_page = _coerce_int(explicit.get("source_page")) or 1
        source_zone = explicit.get("source_zone") or "memorandum"
        confidence = _coerce_float(explicit.get("confidence")) or (0.9 if detected else 0.0)
        section_type = _string_or_none(explicit.get("section_type") or explicit.get("layout_type")) or _infer_memorandum_section_type(" ".join([source_text, matched_text or ""]))
    else:
        detected = "memorandum" in source_text.lower()
        matched_text = "MEMORANDUM" if detected else None
        source_page = 1
        source_zone = "memorandum" if detected else ""
        confidence = 0.9 if detected else 0.0
        section_type = _infer_memorandum_section_type(source_text) if detected else "unknown"

    return {
        "detected": detected,
        "present": detected,
        "matched_text": matched_text,
        "section_type": section_type,
        "source_page": source_page,
        "source_zone": source_zone,
        "confidence": confidence,
        "status": "detected" if detected else "not_applicable",
        "review_status": "extracted" if detected else "not_applicable",
    }


def _collect_document_text(raw: dict[str, Any]) -> str:
    return " ".join(
        str(value)
        for value in (
            raw.get("document_text"),
            raw.get("raw_text"),
            raw.get("title"),
            raw.get("heading"),
        )
        if value is not None
    )


def _infer_memorandum_section_type(source_text: str) -> str:
    lowered = source_text.lower()
    table_markers = [
        "the name of the party at whose instance",
        "surveyed for",
        "the name of the property surveyed",
        "notices were served on",
        "served with notices",
        "those who appeared",
        "make and no. of instrument",
        "date of last instr",
        "result of instruments check",
    ]
    narrative_markers = [
        "represents",
        "registered at",
        "notice was served",
        "notices were served",
        "present at the survey",
    ]
    if any(marker in lowered for marker in table_markers):
        return "table"
    if any(marker in lowered for marker in narrative_markers):
        return "narrative"
    return "unknown"


def _split_instrument_check(value: Any) -> dict[str, str]:
    text = _string_or_none(value)
    if not text:
        return {}
    date_pattern = (
        r"(?P<date>"
        r"\d{1,2}/\d{1,2}/\d{2,4}"
        r"|(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)[a-z]*\.?\s+\d{1,2},?\s+\d{4}"
        r")"
    )
    match = re.search(date_pattern, text, flags=re.IGNORECASE)
    if not match:
        return {"result": text}
    date_value = match.group("date").strip()
    result_text = (text[: match.start()] + " " + text[match.end() :]).strip(" -()")
    return {
        "date": date_value,
        "result": result_text.strip() if result_text else "",
    }


def _extract_field_text(value: Any) -> str:
    if isinstance(value, dict):
        for key in ("review_value", "normalized_value", "value", "name", "text", "raw_value"):
            text = _string_or_none(value.get(key))
            if text:
                return text
        return ""
    return "" if value is None else str(value).strip()


def _extract_raw_value(value: Any, fallback: str) -> str | None:
    if isinstance(value, dict):
        for key in ("raw_value", "raw_text", "source_text", "evidence"):
            text = _string_or_none(value.get(key))
            if text is not None:
                return text
    return fallback or None


def _first_present(primary: dict[str, Any], secondary: dict[str, Any], *keys: str) -> Any:
    for key in keys:
        if key in primary:
            return primary.get(key)
    for key in keys:
        if key in secondary:
            return secondary.get(key)
    return None


def _resolve_semantic_state(value: Any, text: str) -> str:
    if value is None:
        return "NOT_FOUND"
    if isinstance(value, dict):
        explicit = _string_or_none(value.get("semantic_state") or value.get("state"))
        if explicit:
            normalized = explicit.strip().upper()
            if normalized in SEMANTIC_STATES:
                return normalized
        if value.get("illegible") is True:
            return "ILLEGIBLE"
        if not text and any(key in value for key in ("value", "name", "text", "raw_text", "raw_value")):
            return "NOT_STATED"
    lowered = text.strip().lower()
    if not lowered:
        return "NOT_STATED"
    if lowered in {"none", "nil", "no objections", "no objection"}:
        return "NONE"
    if _is_no_appearance_text(text):
        return "NO_ONE_APPEARED"
    if re.fullmatch(r"n\s*/?\s*a|not applicable", lowered, flags=re.IGNORECASE):
        return "N_A"
    if lowered in {"illegible", "unreadable"}:
        return "ILLEGIBLE"
    return "VALUE"


def _parse_area_value(value: Any) -> dict[str, Any]:
    text = _string_or_none(value)
    if not text:
        return {}
    match = re.search(
        r"(?P<value>\d{1,3}(?:,\d{3})*(?:\.\d+)?|\d+(?:\.\d+)?)\s*(?P<unit>sq\.?\s*m(?:etres|eters)?|square\s+metres?|m2|m²|hectares?|ha|acres?)",
        text,
        flags=re.IGNORECASE,
    )
    if not match:
        return {}
    numeric_value = float(match.group("value").replace(",", ""))
    unit_text = match.group("unit").lower().replace(".", "").strip()
    if unit_text in {"m2", "m²"} or "metre" in unit_text or "meter" in unit_text or unit_text.startswith("sq m"):
        canonical_unit = "SQUARE_METRES"
    elif unit_text in {"ha", "hectare", "hectares"}:
        canonical_unit = "HECTARES"
    elif unit_text in {"acre", "acres"}:
        canonical_unit = "ACRES"
    else:
        canonical_unit = unit_text.upper().replace(" ", "_")
    return {"value": numeric_value, "unit": canonical_unit}


def _has_scale_bar_text(raw: dict[str, Any]) -> bool:
    text = _collect_document_text(raw)
    if not text:
        return False
    lowered = text.lower()
    return bool(
        re.search(r"\bscale\b", lowered)
        and (
            re.search(r"\b1\s*:\s*\d{3,6}\b", lowered)
            or re.search(r"\b1\s*/\s*\d{3,6}\b", lowered)
            or re.search(r"\b1\s*cm\s*(?:=|to)\s*\d+(?:\.\d+)?\s*m", lowered)
            or re.search(r"\bone\s+millimetre\s*=\s*", lowered)
            or re.search(r"\bmetres?\b", lowered)
        )
    )


def _extract_scale_bar_text(raw: dict[str, Any]) -> str | None:
    text = _collect_document_text(raw)
    if not text:
        return None
    match = re.search(
        r"(?P<scale>\bscale\b.{0,80}(?:\b1\s*:\s*\d{3,6}\b|\b1\s*/\s*\d{3,6}\b|\b1\s*cm\s*(?:=|to)\s*\d+(?:\.\d+)?\s*m).{0,40})",
        text,
        flags=re.IGNORECASE,
    )
    if match:
        return re.sub(r"\s+", " ", match.group("scale")).strip()
    match = re.search(r"(?P<scale>.{0,40}\bscale\b.{0,80})", text, flags=re.IGNORECASE)
    return re.sub(r"\s+", " ", match.group("scale")).strip() if match else None


def _normalize_memorandum_name(item: Any, role: str) -> dict[str, Any]:
    if isinstance(item, dict):
        name = _string_or_none(item.get("name") or item.get("value") or item.get("party"))
        semantic_state = _resolve_semantic_state(item, name or "")
        return {
            "name": name,
            "raw_value": _extract_raw_value(item, name or ""),
            "semantic_state": semantic_state,
            "role": item.get("role") or role,
            "confidence": _coerce_float(item.get("confidence")) or (0.75 if name else 0.0),
            "source_page": _coerce_int(item.get("source_page")) or 1,
            "source_zone": item.get("source_zone") or "memorandum",
            "review_status": item.get("review_status") or ("extracted" if name else "not_available"),
            "review_notes": item.get("review_notes") or item.get("review_note"),
        }

    name = _string_or_none(item)
    semantic_state = _resolve_semantic_state(item, name or "")
    return {
        "name": name,
        "raw_value": name,
        "semantic_state": semantic_state,
        "role": role,
        "confidence": 0.75 if name else 0.0,
        "source_page": 1,
        "source_zone": "memorandum",
        "review_status": "extracted" if name else "not_available",
        "review_notes": None,
    }


def _normalize_memorandum_value(item: Any, field: str) -> dict[str, Any]:
    if isinstance(item, dict):
        value = _string_or_none(item.get("value") or item.get("name") or item.get("text"))
        semantic_state = _resolve_semantic_state(item, value or "")
        return {
            "field": field,
            "value": value,
            "raw_value": _extract_raw_value(item, value or ""),
            "semantic_state": semantic_state,
            "confidence": _coerce_float(item.get("confidence")) or (0.75 if value else 0.0),
            "source_page": _coerce_int(item.get("source_page")) or 1,
            "source_zone": item.get("source_zone") or "memorandum",
            "review_status": item.get("review_status") or ("extracted" if value else "not_available"),
            "review_notes": item.get("review_notes") or item.get("review_note"),
        }

    value = _string_or_none(item)
    semantic_state = _resolve_semantic_state(item, value or "")
    return {
        "field": field,
        "value": value,
        "raw_value": value,
        "semantic_state": semantic_state,
        "confidence": 0.75 if value else 0.0,
        "source_page": 1,
        "source_zone": "memorandum",
        "review_status": "extracted" if value else "not_available",
        "review_notes": None,
    }


def _normalize_presence_evidence(item: Any, field: str, fallback_zone: str) -> dict[str, Any]:
    node = item if isinstance(item, dict) else {"present": item} if item is not None else {}
    present_value = node.get("present")
    if present_value is None:
        present_value = node.get("detected") or node.get("Detected")
    value = _string_or_none(node.get("value") or node.get("text"))
    present = bool(present_value)
    semantic_state = _resolve_semantic_state(node, value or ("Present" if present else ""))
    return {
        "field": field,
        "value": value,
        "raw_value": _extract_raw_value(node, value or ("Present" if present else "")),
        "semantic_state": semantic_state,
        "present": present,
        "confidence": _coerce_float(node.get("confidence")) or (0.75 if present or value else 0.0),
        "source_page": _coerce_int(node.get("source_page")) or 1,
        "source_zone": node.get("source_zone") or fallback_zone,
        "review_status": node.get("review_status") or ("extracted" if present or value else "not_available"),
        "review_notes": node.get("review_notes") or node.get("review_note"),
    }


def _normalize_appeared_party(item: Any) -> dict[str, Any]:
    node = item if isinstance(item, dict) else {"name": item}
    name = _string_or_none(node.get("name") or node.get("party") or node.get("value"))
    explicit_no_appearance = _is_no_appearance_text(name)
    mode = _string_or_none(node.get("appearance_mode") or node.get("mode") or node.get("appearance"))
    if explicit_no_appearance:
        mode = "none"
    mode = mode or "unknown"
    return {
        "name": name,
        "raw_value": _extract_raw_value(node, name or ""),
        "semantic_state": "NO_ONE_APPEARED" if explicit_no_appearance else _resolve_semantic_state(node, name or ""),
        "appearance_mode": mode.strip().lower(),
        "representative": _string_or_none(node.get("representative") or node.get("representative_name")),
        "confidence": _coerce_float(node.get("confidence")) or (0.8 if explicit_no_appearance else 0.75 if name else 0.0),
        "source_page": _coerce_int(node.get("source_page")) or 1,
        "source_zone": node.get("source_zone") or "memorandum",
        "review_status": node.get("review_status") or ("extracted" if name else "not_available"),
        "review_notes": node.get("review_notes") or node.get("review_note"),
    }


def _is_no_appearance_text(value: Any) -> bool:
    text = _string_or_none(value)
    if not text:
        return False
    normalized = re.sub(r"[^a-z]+", " ", text.lower()).strip()
    return normalized in {"no one appeared", "none appeared", "no one", "none"} or normalized.startswith("no one appeared ")


def _normalize_volume_folio_item(item: Any) -> dict[str, Any] | None:
    if isinstance(item, dict):
        volume = _string_or_none(item.get("volume") or item.get("vol") or item.get("Volume") or item.get("Vol."))
        folio = _string_or_none(item.get("folio") or item.get("fol") or item.get("Folio") or item.get("Fol."))
        raw_text = _string_or_none(item.get("raw_text") or item.get("value") or item.get("text"))
        if (not volume or not folio) and raw_text:
            parsed = _parse_volume_folio_text(raw_text)
            volume = volume or parsed.get("volume")
            folio = folio or parsed.get("folio")
        if not volume and not folio and not raw_text:
            return None
        return {
            "volume": volume,
            "folio": folio,
            "raw_text": raw_text or _join_volume_folio(volume, folio),
            "confidence": _coerce_float(item.get("confidence")) or 0.75,
            "source_page": _coerce_int(item.get("source_page")) or 1,
            "source_zone": item.get("source_zone") or "registration_block",
            "review_note": item.get("review_note") or f"Recognized using volume/folio aliases: {VOLUME_FOLIO_ALIASES}",
        }

    text = str(item).strip()
    parsed = _parse_volume_folio_text(text)
    if not parsed:
        return None
    return {
        "volume": parsed.get("volume"),
        "folio": parsed.get("folio"),
        "raw_text": text,
        "confidence": 0.75,
        "source_page": 1,
        "source_zone": "registration_block",
        "review_note": f"Recognized using volume/folio aliases: {VOLUME_FOLIO_ALIASES}",
    }


def _parse_volume_folio_text(text: str) -> dict[str, str]:
    for pattern in VOLUME_FOLIO_PATTERNS:
        match = pattern.search(text)
        if match:
            return {
                "volume": match.group("volume").strip(),
                "folio": match.group("folio").strip(),
            }
    return {}


def _join_volume_folio(volume: str | None, folio: str | None) -> str | None:
    if volume and folio:
        return f"Vol/Fol {volume}/{folio}"
    return volume or folio


def _normalize_role(value: Any) -> str | None:
    role = _string_or_none(value)
    if not role:
        return None
    compact = role.rstrip(".").strip().lower()
    if compact in {"occ", "occupant"}:
        return "Occupant"
    return role


def _metadata_has_value(field: Any) -> bool:
    if isinstance(field, list):
        return bool(field)
    if isinstance(field, dict):
        return bool(field.get("value"))
    return bool(field)


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
        "Return only JSON with keys: document_type, coordinate_system, coordinate_system_confidence, "
        "north_arrow {detected, approximate_page_location, confidence, review_note}, "
        "scale_bar {detected, text, approximate_page_location, confidence, review_note}, "
        "survey_metadata {parish, document_area, survey_date, survey_method, grounds_of_objection, "
        "surveyor_decision_grounds, instrument, instrument_check_date, instrument_check_result, surveyed_by, "
        "plan_check_date, file_reference, volume_folio [{volume,folio,raw_text,confidence,source_page,source_zone,review_note}]}, "
        "surveyed_for_names, surveyed_property_names, notice_served_on, interested_parties, appeared_parties, "
        "parties, representatives, adjacent_owners, "
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
        f"For Volume/Folio, recognize these labels and abbreviations: {VOLUME_FOLIO_ALIASES}. "
        "Return each detected pair as survey_metadata.volume_folio. Treat Occ. or Occ as Occupant in party or owner roles. "
        "For region-first MEMORANDUM table extraction, detect the memorandum region/table before reading labels and values. "
        "For memorandum fields return raw_value, normalized_value, source_page, source_zone, confidence, and semantic_state. "
        "Allowed semantic_state values are VALUE, NONE, N_A, NOT_STATED, NOT_FOUND, ILLEGIBLE, NO_ONE_APPEARED, UNKNOWN. "
        "Do not collapse blank cells, missing labels, illegible OCR, explicit None, explicit N/A, or No one appeared into the same null value. "
        "Parse area into numeric value and unit when readable. Keep memorandum instrument/check evidence distinct from GPS or remarks text. "
        "Preserve row boundaries for interested parties and appeared parties, and preserve surveyor certification name, title, and organization. "
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
