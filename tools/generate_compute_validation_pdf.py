from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path


MAX_LINE_LENGTH = 96
MAX_LINES_PER_PAGE = 45


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def read_status(path: Path, *keys: str) -> str:
    if not path.exists():
        return "missing"

    data = load_json(path)
    for key in keys:
        value = data.get(key)
        if isinstance(value, str) and value.strip():
            return value

    return "available"


def preflight_status(path: Path) -> str:
    if not path.exists():
        return "missing"

    data = load_json(path)
    payload = data.get("payload") or {}
    return payload.get("status") or data.get("status") or "available"


def count_preflight(path: Path, key: str) -> int:
    if not path.exists():
        return 0

    data = load_json(path)
    payload = data.get("payload") or {}
    values = payload.get(key) or []
    return len(values) if isinstance(values, list) else 0


def build_lines(case_root: Path) -> list[str]:
    manifest = load_json(case_root / "manifest.json")
    payload = manifest.get("payload") or {}
    innola = payload.get("innola_transaction") or {}
    lifecycle = payload.get("innola_lifecycle") or {}
    working = case_root / "working"
    output = case_root / "output"

    spatial_response_path = working / "spatial_unit_api_response.json"
    spatial_response = load_json(spatial_response_path) if spatial_response_path.exists() else {}
    publish_path = output / "enterprise_working_publish.json"
    publish = load_json(publish_path) if publish_path.exists() else {}
    plan_check_path = working / "plan_check_api_response.json"
    plan_check = load_json(plan_check_path) if plan_check_path.exists() else {}
    audit_path = working / "workflow_lifecycle_audit.json"
    audit = load_json(audit_path) if audit_path.exists() else {}

    generated_at = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    tx_number = innola.get("transaction_number") or manifest.get("transaction_id") or case_root.name

    lines = [
        "Compute Examination Validation Report",
        f"Transaction Number: {tx_number}",
        f"Transaction Id: {innola.get('transaction_id') or ''}",
        f"Task Id: {innola.get('task_id') or ''}",
        f"Task Name: {innola.get('task_name') or ''}",
        f"Generated At UTC: {generated_at}",
        f"Manifest Run Id: {manifest.get('run_id') or ''}",
        "",
        "Stage Summary",
        f"- Supporting Document Check: {payload.get('detected_profile', {}).get('status') or 'available'}",
        f"- Structure Check: {preflight_status(working / 'structure_check_summary.json')} "
        f"(blockers={count_preflight(working / 'structure_check_summary.json', 'blockers')}, "
        f"warnings={count_preflight(working / 'structure_check_summary.json', 'warnings')})",
        f"- Georeference Check: {preflight_status(working / 'georeference_check_summary.json')} "
        f"(blockers={count_preflight(working / 'georeference_check_summary.json', 'blockers')}, "
        f"warnings={count_preflight(working / 'georeference_check_summary.json', 'warnings')})",
        f"- Dimension Check: {preflight_status(working / 'dimension_check_summary.json')} "
        f"(blockers={count_preflight(working / 'dimension_check_summary.json', 'blockers')}, "
        f"warnings={count_preflight(working / 'dimension_check_summary.json', 'warnings')})",
        f"- Validate Points and Lines: {read_status(working / 'approved_review.json', 'status', 'decision')}",
        f"- Create Spatial Units: {read_status(output / 'output_summary.json', 'status')}",
        f"- Final Review: {read_status(working / 'approved_review.json', 'status', 'decision')}",
        f"- Enterprise Working Publish: {publish.get('status') or 'missing'}",
        f"- Spatial Unit API: {spatial_response.get('status') or lifecycle.get('spatial_unit_api_status') or 'available'}",
        f"- Plan Check Writeback: {plan_check.get('status') or 'missing'}",
        f"- Package Upload: {lifecycle.get('working_package_upload_status') or 'missing'}",
        f"- Innola Task Completion: {lifecycle.get('status') or 'missing'}",
        "",
        "Enterprise Publish Counts",
    ]

    for layer in publish.get("published_layers") or []:
        lines.append(f"- {layer.get('layer_role')}: {layer.get('record_count')} row(s)")

    lines.extend(
        [
            "",
            "Spatial Units",
            f"Requested Spatial Units: {spatial_response.get('requested_spatial_unit_count') or ''}",
            f"Saved Spatial Units: {spatial_response.get('saved_object_count') or ''}",
            f"Returned SUIDs: {', '.join(spatial_response.get('returned_suids') or [])}",
            "",
            "Plan Check",
            f"Saved Plan Count: {plan_check.get('saved_plan_count') or ''}",
            f"Updated Count: {plan_check.get('updated_count') or ''}",
            f"Updated Check Types: {', '.join(plan_check.get('updated_check_types') or [])}",
            "",
            "Closeout",
            f"Lifecycle Status: {lifecycle.get('status') or ''}",
            f"Completed By: {lifecycle.get('completed_by') or ''}",
            f"Completed At: {lifecycle.get('completed_at') or ''}",
            f"Working Package: {lifecycle.get('working_package_file_name') or ''}",
            f"Working Package Upload Status: {lifecycle.get('working_package_upload_status') or ''}",
            "",
            "Audit Events",
        ]
    )

    for event in audit.get("events") or []:
        lines.append(f"- {event.get('created_at')}: {event.get('action')} - {event.get('status')}")

    lines.extend(
        [
            "",
            "Artifact References",
            "manifest.json",
            "working/structure_check_summary.json",
            "working/georeference_check_summary.json",
            "working/dimension_check_summary.json",
            "working/approved_review.json",
            "output/output_summary.json",
            "output/enterprise_working_publish.json",
            "working/spatial_unit_api_response.json",
            "working/plan_check_api_response.json",
            "working/workflow_lifecycle_audit.json",
        ]
    )

    return lines


def wrap_lines(lines: list[str]) -> list[str]:
    wrapped: list[str] = []
    for line in lines:
        remaining = line
        if not remaining:
            wrapped.append("")
            continue

        while len(remaining) > MAX_LINE_LENGTH:
            split_at = remaining.rfind(" ", 0, MAX_LINE_LENGTH)
            if split_at <= 0:
                split_at = MAX_LINE_LENGTH
            wrapped.append(remaining[:split_at])
            remaining = remaining[split_at:].lstrip()
        wrapped.append(remaining)

    return wrapped


def pdf_escape(text: str) -> str:
    return text.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")


def content_object(lines: list[str]) -> str:
    stream_lines = ["BT", "/F1 10 Tf", "50 750 Td"]
    for line in lines:
        stream_lines.append(f"({pdf_escape(line)}) Tj")
        stream_lines.append("0 -15 Td")
    stream_lines.append("ET")
    stream = "\n".join(stream_lines) + "\n"
    return f"<< /Length {len(stream.encode('ascii', errors='replace'))} >>\nstream\n{stream}endstream"


def write_pdf(path: Path, lines: list[str]) -> None:
    wrapped = wrap_lines(lines)
    pages = [wrapped[index:index + MAX_LINES_PER_PAGE] for index in range(0, len(wrapped), MAX_LINES_PER_PAGE)]
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
        objects.append(content_object(page_lines))

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


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: generate_compute_validation_pdf.py <case-root> <output-pdf>")
        return 2

    case_root = Path(sys.argv[1])
    output_pdf = Path(sys.argv[2])
    lines = build_lines(case_root)
    write_pdf(output_pdf, lines)
    print(str(output_pdf))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
