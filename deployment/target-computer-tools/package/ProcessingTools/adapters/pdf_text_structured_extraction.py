"""Structured embedded-text extraction for computation PDFs.

This helper tries the deterministic path first:
embedded PDF text -> parcel/segment parser -> normalized review artifact.

If the PDF has no usable text layer or the parsed result is too weak, the script
does not hard-fail. Instead it emits a fallback envelope so the add-in can move
to the configured OCR/AI/manual chain.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path


A4_WIDTH_MM = 210.0
A4_HEIGHT_MM = 297.0
POINT_TO_MM = 25.4 / 72.0


@dataclass(frozen=True)
class _PdfTextMetricSpan:
    text: str
    bbox: tuple[float, float, float, float]


@dataclass(frozen=True)
class _PdfTextMetricPage:
    width_pt: float
    height_pt: float
    spans: list[_PdfTextMetricSpan]

NUMBER_TOKEN = r"\d[\d ]*(?:\.[\d ]+)?"

SEGMENT_RE = re.compile(
    r"^(?P<from>\d+)\s+"
    r"(?P<bearing>[NS]\d{1,2}[°º]\d{1,2}'\d{1,2}\"?[EW])\s+"
    rf"(?P<distance>{NUMBER_TOKEN})\s+"
    rf"(?P<north>{NUMBER_TOKEN})\s+"
    rf"(?P<east>{NUMBER_TOKEN})\s+"
    r"(?P<to>\d+)$"
)
START_POINT_RE = re.compile(rf"^(?P<north>{NUMBER_TOKEN})\s+(?P<east>{NUMBER_TOKEN})\s+(?P<point>\d+)$")
PARCEL_NAME_RE = re.compile(r"^(?:Parcel\s+\d+|[A-Z][A-Z0-9 /-]{4,}|\d{6,})$")
PARCEL_BLOCK_RE = re.compile(r"^Parcel:\s*(?P<parcel>\d[\dA-Z_-]*)$", re.IGNORECASE)
PARCEL_NAME_LABEL_RE = re.compile(r"^Parcel\s+Name:\s*(?P<parcel>.+?)\s*$", re.IGNORECASE)
SEGMENT_HEADER_RE = re.compile(r"^Segment\s*#\s*(?P<segment>\d+)\s*:\s*(?P<segment_type>.+)?$", re.IGNORECASE)
COURSE_LENGTH_RE = re.compile(
    rf"^(?:Line\s+)?Course\s*:\s*(?P<course>.+?)\s+Length\s*:\s*(?P<length>{NUMBER_TOKEN})m?$",
    re.IGNORECASE,
)
NORTH_EAST_RE = re.compile(
    rf"^North\s*:\s*(?P<north>{NUMBER_TOKEN})m?\s+East\s*:\s*(?P<east>{NUMBER_TOKEN})m?$",
    re.IGNORECASE,
)
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


def _build_document_text_metrics_from_pages(pages: list[_PdfTextMetricPage]) -> dict:
    metric_pages: list[dict] = []
    measured_runs = 0
    uncertain_pages = 0

    for page_number, page in enumerate(pages, start=1):
        has_page_size = page.width_pt > 0 and page.height_pt > 0
        width_mm = round(page.width_pt * POINT_TO_MM, 3) if has_page_size else A4_WIDTH_MM
        height_mm = round(page.height_pt * POINT_TO_MM, 3) if has_page_size else A4_HEIGHT_MM
        scale_y = height_mm / page.height_pt if page.height_pt > 0 else 1.0
        text_runs: list[dict] = []

        for span in page.spans:
            raw_text = " ".join((span.text or "").split())
            if not raw_text:
                continue
            x0, y0, x1, y1 = span.bbox
            height = max(0.0, float(y1) - float(y0)) * scale_y
            if height <= 0:
                continue
            text_runs.append(
                {
                    "text": raw_text[:80],
                    "height_mm": round(height, 3),
                    "bbox": [round(float(x0), 3), round(float(y0), 3), round(float(x1), 3), round(float(y1), 3)],
                }
            )

        measured_runs += len(text_runs)
        if not has_page_size or not text_runs:
            uncertain_pages += 1

        metric_pages.append(
            {
                "page_number": page_number,
                "width_mm": width_mm,
                "height_mm": height_mm,
                "page_standard": "pdf_metadata" if has_page_size else "A4_fallback",
                "page_size_fallback": not has_page_size,
                "raster_only": not text_runs,
                "dpi_unknown": not has_page_size,
                "text_runs": text_runs[:250],
            }
        )

    return {
        "status": "measured" if measured_runs else "not_available",
        "page_standard": "pdf_metadata_or_A4_fallback",
        "page_count": len(metric_pages),
        "measured_text_run_count": measured_runs,
        "uncertain_page_count": uncertain_pages,
        "pages": metric_pages,
    }


def _extract_document_text_metrics(pdf_path: Path) -> dict:
    try:
        import fitz  # type: ignore
    except ImportError:
        return {
            "status": "not_available",
            "reason": "fitz_unavailable",
            "page_standard": "A4_fallback",
            "pages": [],
        }

    document = fitz.open(pdf_path)
    try:
        metric_pages: list[_PdfTextMetricPage] = []
        for page in document:
            rect = getattr(page, "rect", None)
            width_pt = float(getattr(rect, "width", 0.0) or 0.0)
            height_pt = float(getattr(rect, "height", 0.0) or 0.0)
            spans: list[_PdfTextMetricSpan] = []
            text_dict = page.get_text("dict") or {}
            for block in text_dict.get("blocks", []):
                if not isinstance(block, dict):
                    continue
                for line in block.get("lines", []):
                    if not isinstance(line, dict):
                        continue
                    for span in line.get("spans", []):
                        if not isinstance(span, dict):
                            continue
                        bbox = span.get("bbox") or []
                        if len(bbox) != 4:
                            continue
                        spans.append(
                            _PdfTextMetricSpan(
                                str(span.get("text") or ""),
                                (float(bbox[0]), float(bbox[1]), float(bbox[2]), float(bbox[3])),
                            )
                        )
            metric_pages.append(_PdfTextMetricPage(width_pt, height_pt, spans))
        return _build_document_text_metrics_from_pages(metric_pages)
    finally:
        document.close()

def _load_pages(pdf_path: Path) -> list[str]:
    try:
        import fitz  # type: ignore
    except ImportError:
        fitz = None

    if fitz is not None:
        doc = fitz.open(pdf_path)
        try:
            pages = [page.get_text("text") or "" for page in doc]
            if any(page.strip() for page in pages):
                return pages
        finally:
            doc.close()

    try:
        from pypdf import PdfReader  # type: ignore
    except ImportError:
        return []

    reader = PdfReader(str(pdf_path))
    return [page.extract_text() or "" for page in reader.pages]


def _normalize_line(value: str) -> str:
    normalized = " ".join(value.strip().replace("\t", " ").split())
    normalized = re.sub(r"\bCour\s+se\b", "Course", normalized, flags=re.IGNORECASE)
    normalized = re.sub(r"\bCou\s+rse\b", "Course", normalized, flags=re.IGNORECASE)
    normalized = re.sub(r"\bCours\s+e\b", "Course", normalized, flags=re.IGNORECASE)
    normalized = re.sub(r"\bLen\s+gth\b", "Length", normalized, flags=re.IGNORECASE)
    normalized = re.sub(r"\bE\s+ast\b", "East", normalized, flags=re.IGNORECASE)
    return normalized


def _clean_numeric_text(value: str) -> str:
    return value.replace(" ", "")


def _normalize_course_text(value: str) -> str:
    normalized = _normalize_line(value)
    match = re.match(
        r"^(?P<ns>[NS])\s*(?P<deg>\d{1,2})\s*[-°º]\s*(?P<minute>\d{1,2})\s*[-']\s*(?P<second>\d{1,2})\"?\s*(?P<ew>[EW])$",
        normalized,
        re.IGNORECASE,
    )
    if not match:
        return normalized

    return (
        f"{match.group('ns').upper()}"
        f"{int(match.group('deg')):02d}°"
        f"{int(match.group('minute')):02d}'"
        f"{int(match.group('second')):02d}\""
        f"{match.group('ew').upper()}"
    )


def _append_structured_row(
    rows: list[dict],
    parcel_group_id: str,
    parcel_name: str,
    point_order: int,
    segment_no: int,
    point_identifier: str,
    course_from_previous: str | None,
    length_from_previous_m: str | None,
    easting: str,
    northing: str,
    source_page: int,
) -> None:
    rows.append(
        {
            "parcel_group_id": parcel_group_id,
            "parcel_name": parcel_name,
            "point_order": point_order,
            "segment_no": segment_no,
            "point_identifier": point_identifier,
            "from_point": None if point_order == 0 else f"{parcel_name}_P{max(segment_no - 1, 0)}",
            "to_point": point_identifier,
            "course_from_previous": course_from_previous,
            "length_from_previous_m": length_from_previous_m,
            "easting": easting,
            "northing": northing,
            "source_page": source_page,
            "is_boundary_break": False,
            "row_provenance": "embedded_pdf_text",
            "extraction_status": "matched",
        }
    )


def _append_segment_table_start_row(
    rows: list[dict],
    parcel_group_id: str,
    parcel_name: str,
    point_identifier: str,
    easting: str,
    northing: str,
    source_page: int,
) -> None:
    rows.append(
        {
            "parcel_group_id": parcel_group_id,
            "parcel_name": parcel_name,
            "point_order": 1,
            "segment_no": 0,
            "point_identifier": point_identifier,
            "from_point": None,
            "to_point": point_identifier,
            "course_from_previous": None,
            "length_from_previous_m": None,
            "easting": easting,
            "northing": northing,
            "source_page": source_page,
            "is_boundary_break": False,
            "row_provenance": "embedded_pdf_text",
            "extraction_status": "matched",
        }
    )


def _append_segment_table_follow_row(
    rows: list[dict],
    pending_segment: dict,
    parcel_group_id: str,
    parcel_name: str,
    point_order: int,
    easting: str,
    northing: str,
    source_page: int,
) -> None:
    rows.append(
        {
            "parcel_group_id": parcel_group_id,
            "parcel_name": parcel_name,
            "point_order": point_order,
            "segment_no": pending_segment["segment_no"],
            "point_identifier": pending_segment["to_point"],
            "from_point": pending_segment["from_point"],
            "to_point": pending_segment["to_point"],
            "course_from_previous": pending_segment["course_from_previous"],
            "length_from_previous_m": pending_segment["length_from_previous_m"],
            "easting": easting,
            "northing": northing,
            "source_page": source_page,
            "is_boundary_break": False,
            "row_provenance": "embedded_pdf_text",
            "extraction_status": "matched",
        }
    )


def _detect_parcel_name(line: str, current_name: str | None) -> str | None:
    normalized = _normalize_line(line)
    if not normalized:
        return current_name

    parcel_name_match = PARCEL_NAME_LABEL_RE.match(normalized)
    if parcel_name_match:
        parcel_name = parcel_name_match.group("parcel").strip()
        return parcel_name or current_name

    upper = normalized.upper()
    if upper.startswith("PROPERTY NAME:"):
        property_name = normalized.split(":", 1)[1].strip()
        return property_name or current_name

    if PARCEL_NAME_RE.match(normalized):
        return normalized

    return current_name


def _extract_volume_folios(pages: list[str]) -> list[dict]:
    volume_folios: list[dict] = []
    seen: set[tuple[str, str]] = set()
    for page_index, page_text in enumerate(pages, start=1):
        for raw_line in page_text.splitlines():
            line = _normalize_line(raw_line)
            if not line:
                continue
            for pattern in VOLUME_FOLIO_PATTERNS:
                for match in pattern.finditer(line):
                    volume = match.group("volume").strip()
                    folio = match.group("folio").strip()
                    key = (volume.lower(), folio.lower())
                    if key in seen:
                        continue
                    seen.add(key)
                    volume_folios.append(
                        {
                            "volume": volume,
                            "folio": folio,
                            "raw_text": line,
                            "confidence": 0.85,
                            "source_page": page_index,
                            "source_zone": "registration_block",
                            "review_note": f"Recognized using volume/folio aliases: {VOLUME_FOLIO_ALIASES}",
                        }
                    )
    return volume_folios

COMPUTE_SHEET_KEYWORDS = (
    "COMPUTATION SHEET",
    "COMPUTE SHEET",
    "SURVEY COMPUTATION",
    "LINE COURSE",
    "SEGMENT #",
    "FROM PNT",
    "NORTHING EASTING",
)


def _detect_embedded_compute_sheet_pages(pages: list[str]) -> dict:
    page_numbers: list[int] = []
    evidence: list[str] = []
    score = 0

    for page_index, page_text in enumerate(pages, start=1):
        normalized = " ".join((page_text or "").upper().split())
        if not normalized:
            continue

        hits = [keyword for keyword in COMPUTE_SHEET_KEYWORDS if keyword in normalized]
        has_coordinate_pattern = bool(re.search(r"\bNORTH\s*:?\s*\d", normalized)) and bool(re.search(r"\bEAST\s*:?\s*\d", normalized))
        has_bearing_distance = "COURSE" in normalized and "LENGTH" in normalized
        page_score = len(hits) + (2 if has_coordinate_pattern else 0) + (2 if has_bearing_distance else 0)
        if page_score < 3:
            continue

        page_numbers.append(page_index)
        score += page_score
        first_line = next((line.strip() for line in page_text.splitlines() if line.strip()), "compute sheet evidence")
        evidence.append(first_line[:160])

    confidence = 0.0 if not page_numbers else min(0.95, 0.55 + (score * 0.05))
    return {
        "detected": bool(page_numbers),
        "status": "detected" if page_numbers else "not_detected",
        "page_numbers": page_numbers,
        "confidence": round(confidence, 2),
        "evidence": evidence[:5],
    }

def _parse_pages(pages: list[str], transaction_number: str, document_text_metrics: dict | None = None) -> dict:
    rows: list[dict] = []
    parcel_names: list[str] = []
    volume_folios = _extract_volume_folios(pages)
    embedded_compute_sheet = _detect_embedded_compute_sheet_pages(pages)
    document_text_metrics = document_text_metrics or {"status": "not_available", "pages": []}
    current_parcel_name: str | None = None
    current_group: str | None = None
    point_order = 0
    seen_start_points: set[tuple[str, str, str, str]] = set()
    pending_segment_no: int | None = None
    pending_course: str | None = None
    pending_length: str | None = None
    pending_segment_table_row: dict | None = None
    current_group_uses_segment_table = False

    for page_index, page_text in enumerate(pages, start=1):
        for raw_line in page_text.splitlines():
            line = _normalize_line(raw_line)
            if not line:
                continue

            parcel_block_match = PARCEL_BLOCK_RE.match(line)
            if parcel_block_match:
                current_parcel_name = parcel_block_match.group("parcel")
                parcel_names.append(current_parcel_name)
                current_group = f"parcel-{len(parcel_names):03d}"
                point_order = 0
                pending_segment_no = None
                pending_course = None
                pending_length = None
                pending_segment_table_row = None
                current_group_uses_segment_table = False
                continue

            segment_header_match = SEGMENT_HEADER_RE.match(line)
            if segment_header_match and current_group is not None:
                pending_segment_no = int(segment_header_match.group("segment"))
                pending_course = None
                pending_length = None
                continue

            course_length_match = COURSE_LENGTH_RE.match(line)
            if course_length_match and current_group is not None:
                pending_course = _normalize_course_text(course_length_match.group("course"))
                pending_length = _clean_numeric_text(course_length_match.group("length"))
                if pending_segment_no is None:
                    pending_segment_no = point_order + 1
                continue

            north_east_match = NORTH_EAST_RE.match(line)
            if north_east_match and current_group is not None:
                northing = _clean_numeric_text(north_east_match.group("north"))
                easting = _clean_numeric_text(north_east_match.group("east"))
                if pending_segment_no is None:
                    start_key = (
                        current_group,
                        "0",
                        northing,
                        easting,
                    )
                    if start_key in seen_start_points:
                        continue
                    seen_start_points.add(start_key)
                    _append_structured_row(
                        rows=rows,
                        parcel_group_id=current_group,
                        parcel_name=current_parcel_name or current_group,
                        point_order=0,
                        segment_no=0,
                        point_identifier=f"{current_parcel_name or current_group}_P0",
                        course_from_previous=None,
                        length_from_previous_m=None,
                        easting=easting,
                        northing=northing,
                        source_page=page_index,
                    )
                    continue

                point_order += 1
                _append_structured_row(
                    rows=rows,
                    parcel_group_id=current_group,
                    parcel_name=current_parcel_name or current_group,
                    point_order=point_order,
                    segment_no=pending_segment_no,
                    point_identifier=f"{current_parcel_name or current_group}_P{pending_segment_no}",
                    course_from_previous=pending_course,
                    length_from_previous_m=pending_length,
                    easting=easting,
                    northing=northing,
                    source_page=page_index,
                )
                pending_segment_no = None
                pending_course = None
                pending_length = None
                continue

            detected_name = _detect_parcel_name(line, current_parcel_name)
            if detected_name and detected_name != current_parcel_name:
                current_parcel_name = detected_name
                parcel_names.append(detected_name)
                current_group = f"parcel-{len(parcel_names):03d}"
                point_order = 0
                pending_segment_table_row = None
                current_group_uses_segment_table = False

            segment_match = SEGMENT_RE.match(line)
            if segment_match:
                if current_group is None:
                    current_group = "parcel-001"
                    if not parcel_names:
                        parcel_names.append("Parcel 1")
                    current_parcel_name = parcel_names[-1]

                current_group_uses_segment_table = True
                current_segment = {
                    "segment_no": point_order + 1 if point_order > 0 else 1,
                    "from_point": segment_match.group("from"),
                    "to_point": segment_match.group("to"),
                    "course_from_previous": segment_match.group("bearing"),
                    "length_from_previous_m": segment_match.group("distance"),
                }
                current_row_northing = segment_match.group("north")
                current_row_easting = segment_match.group("east")

                if point_order == 0:
                    point_order = 1
                    _append_segment_table_start_row(
                        rows=rows,
                        parcel_group_id=current_group,
                        parcel_name=current_parcel_name or current_group,
                        point_identifier=current_segment["from_point"],
                        easting=current_row_easting,
                        northing=current_row_northing,
                        source_page=page_index,
                    )
                elif pending_segment_table_row is not None:
                    point_order += 1
                    _append_segment_table_follow_row(
                        rows=rows,
                        pending_segment=pending_segment_table_row,
                        parcel_group_id=current_group,
                        parcel_name=current_parcel_name or current_group,
                        point_order=point_order,
                        easting=current_row_easting,
                        northing=current_row_northing,
                        source_page=page_index,
                    )

                pending_segment_table_row = current_segment
                continue

            start_match = START_POINT_RE.match(line)
            if start_match and current_group is not None:
                if current_group_uses_segment_table:
                    start_key = (
                        current_group,
                        start_match.group("point"),
                        start_match.group("north"),
                        start_match.group("east"),
                    )
                    seen_start_points.add(start_key)
                    continue

                start_key = (
                    current_group,
                    start_match.group("point"),
                    start_match.group("north"),
                    start_match.group("east"),
                )
                if start_key in seen_start_points:
                    continue

                seen_start_points.add(start_key)
                rows.insert(
                    len(rows) - point_order,
                    {
                        "parcel_group_id": current_group,
                        "parcel_name": current_parcel_name or current_group,
                        "point_order": 0,
                        "segment_no": 0,
                        "point_identifier": start_match.group("point"),
                        "from_point": None,
                        "to_point": start_match.group("point"),
                        "course_from_previous": None,
                        "length_from_previous_m": None,
                        "easting": start_match.group("east"),
                        "northing": start_match.group("north"),
                        "source_page": page_index,
                        "is_boundary_break": False,
                        "row_provenance": "embedded_pdf_text",
                        "extraction_status": "matched",
                    },
                )

    if not rows:
        return {
            "status": "fallback_requested",
            "text_layer_available": True,
            "parser_status": "parse_confidence_low",
            "fallback_reason": "parse_confidence_low",
            "parsed_parcel_count": 0,
            "parsed_row_count": 0,
            "survey_metadata": {"volume_folio": volume_folios} if volume_folios else {},
            "embedded_compute_sheet": embedded_compute_sheet,
            "document_text_metrics": document_text_metrics,
        }

    normalized_rows = []
    current_group = None
    normalized_point_order = 0
    for row in rows:
        if row["parcel_group_id"] != current_group:
            current_group = row["parcel_group_id"]
            normalized_point_order = 0
        normalized_point_order += 1
        row["point_order"] = normalized_point_order
        normalized_rows.append(row)

    return {
        "status": "success",
        "transaction_number": transaction_number,
        "text_layer_available": True,
        "parser_status": "parsed",
        "parsed_parcel_count": len({row["parcel_group_id"] for row in normalized_rows}),
        "parsed_row_count": len(normalized_rows),
        "parcel_count": len({row["parcel_group_id"] for row in normalized_rows}),
        "row_count": len(normalized_rows),
        "extraction_source": "embedded_text_pdf",
        "survey_metadata": {"volume_folio": volume_folios} if volume_folios else {},
        "embedded_compute_sheet": {**embedded_compute_sheet, "rows": normalized_rows if embedded_compute_sheet.get("detected") else []},
        "document_text_metrics": document_text_metrics,
        "rows": normalized_rows,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-pdf", required=True)
    parser.add_argument("--output-json", required=True)
    parser.add_argument("--transaction-number", required=True)
    args = parser.parse_args(argv)

    source_pdf = Path(args.source_pdf)
    output_json = Path(args.output_json)

    if not source_pdf.exists():
        payload = {
            "status": "fallback_requested",
            "text_layer_available": False,
            "parser_status": "missing_source_pdf",
            "fallback_reason": "missing_source_pdf",
            "parsed_parcel_count": 0,
            "parsed_row_count": 0,
        }
        print(json.dumps(payload))
        return 0

    pages = _load_pages(source_pdf)
    has_text_layer = any(page.strip() for page in pages)
    if not has_text_layer:
        payload = {
            "status": "fallback_requested",
            "text_layer_available": False,
            "parser_status": "no_usable_text_layer",
            "fallback_reason": "no_usable_text_layer",
            "parsed_parcel_count": 0,
            "parsed_row_count": 0,
        }
        print(json.dumps(payload))
        return 0

    document_text_metrics = _extract_document_text_metrics(source_pdf)
    payload = _parse_pages(pages, args.transaction_number, document_text_metrics)
    if payload.get("status") == "success":
        output_json.parent.mkdir(parents=True, exist_ok=True)
        output_json.write_text(json.dumps({**payload, "outputs": {"review_json": str(output_json)}}, indent=2), encoding="utf-8")
        payload["outputs"] = {"review_json": str(output_json)}

    print(json.dumps(payload))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

