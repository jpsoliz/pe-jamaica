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
from pathlib import Path


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
    rf"^Course:\s*(?P<course>.+?)\s+Length:\s*(?P<length>{NUMBER_TOKEN})m?$",
    re.IGNORECASE,
)
NORTH_EAST_RE = re.compile(
    rf"^North:\s*(?P<north>{NUMBER_TOKEN})m?\s+East:\s*(?P<east>{NUMBER_TOKEN})m?$",
    re.IGNORECASE,
)


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
    return " ".join(value.strip().replace("\t", " ").split())


def _clean_numeric_text(value: str) -> str:
    return value.replace(" ", "")


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


def _parse_pages(pages: list[str], transaction_number: str) -> dict:
    rows: list[dict] = []
    parcel_names: list[str] = []
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
                pending_course = _normalize_line(course_length_match.group("course"))
                pending_length = _clean_numeric_text(course_length_match.group("length"))
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

    payload = _parse_pages(pages, args.transaction_number)
    if payload.get("status") == "success":
        output_json.parent.mkdir(parents=True, exist_ok=True)
        output_json.write_text(json.dumps({**payload, "outputs": {"review_json": str(output_json)}}, indent=2), encoding="utf-8")
        payload["outputs"] = {"review_json": str(output_json)}

    print(json.dumps(payload))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
