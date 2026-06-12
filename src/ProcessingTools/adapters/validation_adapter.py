"""Validation adapter for approved review data.

This adapter validates the approved review snapshot and writes a deterministic
``validation_summary.json`` contract for the add-in.
"""

from __future__ import annotations

import argparse
import json
import sys
import uuid
from collections import Counter
from pathlib import Path
from typing import Any


def _read_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def _read_rules_metadata(path: Path | None) -> tuple[str, str]:
    if path is None or not path.exists():
        return ("sidwell_validation_default", "1.0.0")

    rule_profile = "sidwell_validation_default"
    rule_version = "1.0.0"
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or ":" not in line:
            continue
        key, value = [part.strip() for part in line.split(":", 1)]
        value = value.strip("'\"")
        if key == "rule_profile" and value:
            rule_profile = value
        elif key == "rule_version" and value:
            rule_version = value

    return (rule_profile, rule_version)


def _review_hash(document: dict[str, Any]) -> str | None:
    return document.get("review_hash") or document.get("review_data_hash")


def _finding(
    rule_id: str,
    title: str,
    severity: str,
    status: str,
    evidence: str | None = None,
    recommended_action: str | None = None,
) -> dict[str, Any]:
    return {
        "rule_id": rule_id,
        "title": title,
        "severity": severity,
        "status": status,
        "evidence": evidence,
        "recommended_action": recommended_action,
    }


def _is_number(value: Any) -> bool:
    if value is None:
        return False
    try:
        float(str(value).strip())
        return True
    except ValueError:
        return False


def build_summary(
    manifest: dict[str, Any],
    approved_review: dict[str, Any],
    review_data: dict[str, Any],
    operator_id: str | None,
    rules_path: Path | None,
) -> dict[str, Any]:
    rule_profile, rule_version = _read_rules_metadata(rules_path)
    transaction_id = manifest.get("transaction_id") or review_data.get("transaction_number") or ""
    findings: list[dict[str, Any]] = []
    warnings: list[str] = []
    errors: list[str] = []

    approved_hash = _review_hash(approved_review)
    review_hash = _review_hash(review_data)
    if not approved_hash or not review_hash or approved_hash != review_hash:
        findings.append(
            _finding(
                "approved_review_snapshot_current",
                "Approved review snapshot is stale.",
                "critical",
                "failed",
                f"approved={approved_hash or 'missing'}; current={review_hash or 'missing'}",
                "Re-open review data, save current edits, and approve the review again before validation.",
            )
        )
    else:
        findings.append(
            _finding(
                "approved_review_snapshot_current",
                "Approved review snapshot matches the current review data.",
                "passed",
                "passed",
                f"review_hash={review_hash}",
                None,
            )
        )

    rows = review_data.get("rows") or []
    if not rows:
        findings.append(
            _finding(
                "review_rows_present",
                "Approved review contains at least one point row.",
                "high",
                "failed",
                "row_count=0",
                "Run extraction review again and confirm the point rows were generated.",
            )
        )
    else:
        findings.append(
            _finding(
                "review_rows_present",
                "Approved review contains point rows.",
                "passed",
                "passed",
                f"row_count={len(rows)}",
                None,
            )
        )

    unresolved_rows = [
        row.get("row_id") or row.get("point_identifier") or f"row-{index + 1}"
        for index, row in enumerate(rows)
        if bool(row.get("review_unresolved") if "review_unresolved" in row else row.get("unresolved"))
    ]
    if unresolved_rows:
        findings.append(
            _finding(
                "review_rows_resolved",
                "All review rows must be resolved before validation passes.",
                "high",
                "failed",
                ", ".join(str(value) for value in unresolved_rows[:10]),
                "Resolve the remaining review blockers, save the review, and approve it again.",
            )
        )
    else:
        findings.append(
            _finding(
                "review_rows_resolved",
                "All review rows are resolved.",
                "passed",
                "passed",
                None,
                None,
            )
        )

    missing_required = []
    non_numeric = []
    point_ids: list[str] = []
    for index, row in enumerate(rows):
        row_label = row.get("point_identifier") or row.get("point_id") or row.get("row_id") or f"row-{index + 1}"
        point_id = str(row.get("point_identifier") or row.get("point_id") or "").strip()
        easting = row.get("easting")
        northing = row.get("northing")
        if not point_id or easting in (None, "") or northing in (None, ""):
            missing_required.append(str(row_label))
        if point_id:
            point_ids.append(point_id)
        if easting not in (None, "") and northing not in (None, "") and (not _is_number(easting) or not _is_number(northing)):
            non_numeric.append(str(row_label))

    if missing_required:
        findings.append(
            _finding(
                "required_coordinates_present",
                "Every row must contain point id, easting, and northing.",
                "high",
                "failed",
                ", ".join(missing_required[:10]),
                "Complete the missing point identifiers or coordinates in review editing.",
            )
        )
    else:
        findings.append(
            _finding(
                "required_coordinates_present",
                "Every row contains point id, easting, and northing.",
                "passed",
                "passed",
                None,
                None,
            )
        )

    if non_numeric:
        findings.append(
            _finding(
                "coordinates_numeric",
                "All easting and northing values must be numeric.",
                "high",
                "failed",
                ", ".join(non_numeric[:10]),
                "Correct non-numeric coordinate values before output generation.",
            )
        )
    else:
        findings.append(
            _finding(
                "coordinates_numeric",
                "All coordinates are numeric.",
                "passed",
                "passed",
                None,
                None,
            )
        )

    duplicate_ids = sorted([point_id for point_id, count in Counter(point_ids).items() if count > 1])
    if duplicate_ids:
        findings.append(
            _finding(
                "unique_point_identifiers",
                "Point identifiers must be unique in the approved review set.",
                "warning",
                "failed",
                ", ".join(duplicate_ids[:10]),
                "Review duplicated point identifiers and correct them before final outputs.",
            )
        )
        warnings.append("Duplicate point identifiers were detected in the approved review dataset.")
    else:
        findings.append(
            _finding(
                "unique_point_identifiers",
                "Point identifiers are unique.",
                "passed",
                "passed",
                None,
                None,
            )
        )

    source_files = (manifest.get("payload") or {}).get("source_files") or []
    has_dwg = any(str(file_item.get("file_type", "")).lower() == ".dwg" for file_item in source_files)
    if has_dwg:
        findings.append(
            _finding(
                "dwg_context_optional",
                "DWG reference is present for downstream validation context.",
                "info",
                "passed",
                "dwg_reference_detected=true",
                None,
            )
        )
    else:
        findings.append(
            _finding(
                "dwg_context_optional",
                "No DWG reference was supplied for this transaction.",
                "info",
                "passed",
                "dwg_reference_detected=false",
                None,
            )
        )

    blocked = any(
        finding["severity"] in {"critical", "high"} and finding["status"] == "failed"
        for finding in findings
    )

    counts = Counter(finding["severity"] for finding in findings)
    status = "blocked" if blocked else "passed"
    manifest_hash = (manifest.get("payload") or {}).get("script_plan", {}).get("source_manifest_hash") or ""

    return {
        "schema_version": "1.0.0",
        "transaction_id": transaction_id,
        "run_id": f"validation-{uuid.uuid4().hex}",
        "created_at": approved_review.get("approved_at") or review_data.get("review_saved_at") or "",
        "created_by": operator_id or approved_review.get("approved_by"),
        "source_manifest_hash": manifest_hash,
        "payload": {
            "status": status,
            "rule_profile": rule_profile,
            "rule_version": rule_version,
            "finding_counts": {
                "critical": counts.get("critical", 0),
                "high": counts.get("high", 0),
                "warning": counts.get("warning", 0),
                "info": counts.get("info", 0),
                "passed": counts.get("passed", 0),
            },
            "findings": findings,
        },
        "warnings": warnings,
        "errors": errors,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Validate approved review data and write validation_summary.json.")
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--approved-review", required=True)
    parser.add_argument("--review-data", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--operator", default="")
    parser.add_argument("--rules", default="")
    args = parser.parse_args(argv)

    manifest_path = Path(args.manifest)
    approved_review_path = Path(args.approved_review)
    review_data_path = Path(args.review_data)
    output_path = Path(args.output)
    rules_path = Path(args.rules) if args.rules else None

    summary = build_summary(
        _read_json(manifest_path),
        _read_json(approved_review_path),
        _read_json(review_data_path),
        args.operator or None,
        rules_path,
    )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    return 0


def run(input_json_path: str, output_json_path: str) -> None:
    raise NotImplementedError("Use the CLI entry point for validation_adapter.py.")


if __name__ == "__main__":
    sys.exit(main())
