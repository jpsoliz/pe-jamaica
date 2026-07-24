"""Validation adapter for approved review data.

This adapter validates the approved review snapshot and writes a deterministic
``validation_summary.json`` contract for the add-in.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
import uuid
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def _read_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def _utc_now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


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


def _parse_scalar(raw_value: str) -> Any:
    value = raw_value.strip().strip("'\"")
    lowered = value.lower()
    if lowered == "true":
        return True
    if lowered == "false":
        return False
    try:
        if "." in value:
            return float(value)
        return int(value)
    except ValueError:
        return value


def _read_closure_profiles(path: Path | None) -> dict[str, Any]:
    result: dict[str, Any] = {
        "default": {
            "rule_id": "closure_default_standard",
            "title": "Standard parcel closure tolerance",
            "description": "Default closure tolerance for closed parcel traverses.",
            "enabled": True,
            "severity": "blocker",
            "parcel_type": "standard_closed",
            "allow_open_boundary": False,
            "max_closure_distance_m": 0.3,
            "min_misclose_ratio_denominator": 2500,
            "warning_closure_distance_m": 0.15,
            "warning_misclose_ratio_denominator": 4000,
            "evaluation_basis": "distance_and_ratio",
            "fallback": True,
        },
        "profiles": {},
    }
    if path is None or not path.exists():
        return result

    lines = path.read_text(encoding="utf-8").splitlines()
    in_default = False
    in_profiles = False
    current_profile: dict[str, Any] | None = None
    skip_nested_indent: int | None = None

    for raw_line in lines:
        stripped = raw_line.strip()
        if not stripped or stripped.startswith("#"):
            continue

        indent = len(raw_line) - len(raw_line.lstrip(" "))
        if indent == 0:
            in_default = stripped.startswith("closure_tolerance_defaults:")
            in_profiles = stripped.startswith("closure_tolerance_profiles:")
            current_profile = None
            skip_nested_indent = None
            continue

        if skip_nested_indent is not None and indent > skip_nested_indent:
            continue
        if skip_nested_indent is not None and indent <= skip_nested_indent:
            skip_nested_indent = None

        if in_default:
            if ":" not in stripped:
                continue
            key, raw_value = [part.strip() for part in stripped.split(":", 1)]
            if not raw_value:
                skip_nested_indent = indent
                continue
            result["default"][key] = _parse_scalar(raw_value)
            continue

        if in_profiles:
            if stripped.startswith("- "):
                current_profile = {}
                result["profiles"][f"profile_{len(result['profiles']) + 1}"] = current_profile
                stripped = stripped[2:].strip()
                if not stripped:
                    continue
            if current_profile is None or ":" not in stripped:
                continue
            key, raw_value = [part.strip() for part in stripped.split(":", 1)]
            if not raw_value:
                skip_nested_indent = indent
                continue
            current_profile[key] = _parse_scalar(raw_value)
            parcel_type = str(current_profile.get("parcel_type") or "").strip()
            if parcel_type:
                result["profiles"][parcel_type] = current_profile

    unnamed = [key for key in list(result["profiles"].keys()) if key.startswith("profile_")]
    for key in unnamed:
        profile = result["profiles"].pop(key)
        parcel_type = str(profile.get("parcel_type") or key).strip()
        result["profiles"][parcel_type] = profile

    return result


def _read_readiness_profiles(path: Path | None) -> dict[str, Any]:
    result: dict[str, Any] = {
        "default": {
            "parcel_type": "standard_closed",
            "enabled": True,
            "severity": "blocker",
            "min_segment_count": 3,
            "require_contiguous_sequence": True,
            "require_referenced_points": True,
            "require_chain_consistency": True,
            "detect_duplicate_edges": True,
        },
        "profiles": {},
    }
    if path is None or not path.exists():
        return result

    lines = path.read_text(encoding="utf-8").splitlines()
    in_default = False
    in_profiles = False
    current_profile: dict[str, Any] | None = None
    skip_nested_indent: int | None = None

    for raw_line in lines:
        stripped = raw_line.strip()
        if not stripped or stripped.startswith("#"):
            continue

        indent = len(raw_line) - len(raw_line.lstrip(" "))
        if indent == 0:
            in_default = stripped.startswith("parcel_construction_readiness_defaults:")
            in_profiles = stripped.startswith("parcel_construction_readiness_profiles:")
            current_profile = None
            skip_nested_indent = None
            continue

        if skip_nested_indent is not None and indent > skip_nested_indent:
            continue
        if skip_nested_indent is not None and indent <= skip_nested_indent:
            skip_nested_indent = None

        if in_default:
            if ":" not in stripped:
                continue
            key, raw_value = [part.strip() for part in stripped.split(":", 1)]
            if not raw_value:
                skip_nested_indent = indent
                continue
            result["default"][key] = _parse_scalar(raw_value)
            continue

        if in_profiles:
            if stripped.startswith("- "):
                current_profile = {}
                result["profiles"][f"profile_{len(result['profiles']) + 1}"] = current_profile
                stripped = stripped[2:].strip()
                if not stripped:
                    continue
            if current_profile is None or ":" not in stripped:
                continue
            key, raw_value = [part.strip() for part in stripped.split(":", 1)]
            if not raw_value:
                skip_nested_indent = indent
                continue
            current_profile[key] = _parse_scalar(raw_value)
            profile_key = f"{str(current_profile.get('parcel_type') or '').strip()}::{str(current_profile.get('category') or '').strip()}"
            if profile_key != "::":
                result["profiles"][profile_key] = current_profile

    unnamed = [key for key in list(result["profiles"].keys()) if key.startswith("profile_")]
    for key in unnamed:
        profile = result["profiles"].pop(key)
        profile_key = f"{str(profile.get('parcel_type') or 'standard_closed').strip()}::{str(profile.get('category') or key).strip()}"
        result["profiles"][profile_key] = profile

    return result


def _load_settings(path: Path | None) -> dict[str, Any]:
    if path is None or not path.exists():
        return {}
    try:
        return _read_json(path)
    except (json.JSONDecodeError, OSError):
        return {}


def _apply_profile_overrides(rule_config: dict[str, Any], settings: dict[str, Any]) -> dict[str, Any]:
    overrides = settings.get("closure_tolerance_profile_overrides")
    if not isinstance(overrides, dict):
        return rule_config

    merged = {
        "default": dict(rule_config.get("default") or {}),
        "profiles": {key: dict(value) for key, value in (rule_config.get("profiles") or {}).items()},
    }

    default_parcel_type = overrides.get("default_parcel_type")
    if isinstance(default_parcel_type, str) and default_parcel_type.strip():
        merged["default"]["parcel_type"] = default_parcel_type.strip()

    profile_overrides = overrides.get("profiles")
    if not isinstance(profile_overrides, dict):
        return merged

    for parcel_type, override_values in profile_overrides.items():
        if not isinstance(override_values, dict):
            continue
        target = merged["profiles"].setdefault(parcel_type, {"parcel_type": parcel_type})
        for key, value in override_values.items():
            target[key] = value

    return merged


def _apply_readiness_profile_overrides(rule_config: dict[str, Any], settings: dict[str, Any]) -> dict[str, Any]:
    overrides = settings.get("parcel_construction_readiness_profile_overrides")
    if not isinstance(overrides, dict):
        return rule_config

    merged = {
        "default": dict(rule_config.get("default") or {}),
        "profiles": {key: dict(value) for key, value in (rule_config.get("profiles") or {}).items()},
    }

    default_parcel_type = overrides.get("default_parcel_type")
    if isinstance(default_parcel_type, str) and default_parcel_type.strip():
        merged["default"]["parcel_type"] = default_parcel_type.strip()

    default_profile = overrides.get("default_profile")
    if isinstance(default_profile, dict):
        for key, value in default_profile.items():
            merged["default"][key] = value

    profile_overrides = overrides.get("profiles")
    if not isinstance(profile_overrides, dict):
        return merged

    for profile_key, override_values in profile_overrides.items():
        if not isinstance(override_values, dict):
            continue
        target = merged["profiles"].setdefault(profile_key, {})
        for key, value in override_values.items():
            target[key] = value

    return merged


def _normalize_point_id(value: Any) -> str:
    return str(value or "").strip()


def _extract_segment_ref(row: dict[str, Any], *names: str) -> str:
    for name in names:
        value = row.get(name)
        text = str(value or "").strip()
        if text:
            return text
    return ""


def _readiness_status(enabled: bool, severity: str, has_issue: bool) -> str:
    if not enabled:
        return "skipped"
    if not has_issue:
        return "passed"
    return "blocker" if severity in {"critical", "high", "blocker"} else "warning"


def _compute_readiness_results(
    review_data: dict[str, Any],
    source_files: list[dict[str, Any]],
    rule_config: dict[str, Any],
) -> list[dict[str, Any]]:
    rows = review_data.get("rows") or []
    grouped: dict[str, list[dict[str, Any]]] = {}
    for index, row in enumerate(rows):
        if not isinstance(row, dict):
            continue
        grouped.setdefault(_normalize_parcel_group(row, index), []).append(row)

    default_profile = dict(rule_config.get("default") or {})
    profile_map = rule_config.get("profiles") or {}
    all_edges: dict[tuple[str, str], list[str]] = {}
    parcel_edges: dict[str, list[tuple[str, str]]] = {}

    for group_id, group_rows in grouped.items():
        ordered_rows = sorted(group_rows, key=lambda row: _sequence_value(row, group_rows.index(row)))
        edge_keys: list[tuple[str, str]] = []
        previous_point = ""
        for row in ordered_rows:
            current_point = _normalize_point_id(row.get("point_identifier") or row.get("point_id"))
            from_point = _extract_segment_ref(row, "from_point", "from_pt", "start_pt")
            to_point = _extract_segment_ref(row, "to_point", "to_pt", "end_pt")
            edge_start = from_point or previous_point
            edge_end = to_point or current_point
            if edge_start and edge_end:
                key = tuple(sorted((edge_start, edge_end)))
                edge_keys.append(key)
                all_edges.setdefault(key, []).append(group_id)
            previous_point = current_point or previous_point
        parcel_edges[group_id] = edge_keys

    results: list[dict[str, Any]] = []
    categories = [
        "minimum_segment_count",
        "boundary_completeness",
        "line_without_point_support",
        "orphan_line_detection",
        "shared_edge_consistency",
    ]

    for group_id, group_rows in grouped.items():
        ordered_rows = sorted(group_rows, key=lambda row: _sequence_value(row, group_rows.index(row)))
        parcel_type = _infer_parcel_type(ordered_rows, source_files, str(default_profile.get("parcel_type") or "standard_closed"))
        parcel_name = next(
            (
                str(row.get("review_parcel_name") or row.get("parcel_name") or "").strip()
                for row in ordered_rows
                if str(row.get("review_parcel_name") or row.get("parcel_name") or "").strip()
            ),
            group_id,
        )
        parcel_point_ids = {
            _normalize_point_id(row.get("point_identifier") or row.get("point_id"))
            for row in ordered_rows
            if _normalize_point_id(row.get("point_identifier") or row.get("point_id"))
        }
        valid_sequences = sorted(
            {
                _sequence_value(row, index)
                for index, row in enumerate(ordered_rows)
                if _sequence_value(row, index) > 0
            }
        )
        missing_sequences: list[int] = []
        if valid_sequences:
            expected = set(range(1, max(valid_sequences) + 1))
            missing_sequences = sorted(expected.difference(valid_sequences))

        previous_point = ""
        referenced_point_misses: list[str] = []
        orphan_segments: list[str] = []
        segment_ids: list[str] = []
        for index, row in enumerate(ordered_rows):
            segment_id = str(
                row.get("segment_no")
                or row.get("segment_index")
                or row.get("seq")
                or row.get("sequence_in_group")
                or index + 1
            ).strip()
            current_point = _normalize_point_id(row.get("point_identifier") or row.get("point_id"))
            from_point = _extract_segment_ref(row, "from_point", "from_pt", "start_pt")
            to_point = _extract_segment_ref(row, "to_point", "to_pt", "end_pt")
            if from_point and from_point not in parcel_point_ids:
                referenced_point_misses.append(from_point)
                segment_ids.append(segment_id)
            if to_point and to_point not in parcel_point_ids:
                referenced_point_misses.append(to_point)
                segment_ids.append(segment_id)
            if previous_point and from_point and from_point != previous_point:
                orphan_segments.append(segment_id)
            if current_point and to_point and current_point != to_point:
                orphan_segments.append(segment_id)
            previous_point = current_point or previous_point

        duplicate_edges = [
            edge for edge in parcel_edges.get(group_id, [])
            if sum(1 for candidate in parcel_edges.get(group_id, []) if candidate == edge) > 1
        ]
        cross_parcel_shared = [
            edge for edge in parcel_edges.get(group_id, [])
            if len(set(all_edges.get(edge, []))) > 2
        ]
        shared_conflict_count = len({*duplicate_edges, *cross_parcel_shared})

        for category in categories:
            profile = dict(default_profile)
            profile.update(profile_map.get(f"{parcel_type}::{category}") or {})
            enabled = bool(profile.get("enabled", True))
            severity = str(profile.get("severity") or "blocker")
            rule_id = str(profile.get("rule_id") or f"readiness_{category}")
            title = str(profile.get("title") or category.replace("_", " ").title())
            min_segment_count = int(profile.get("min_segment_count") or default_profile.get("min_segment_count") or 0)
            boundary_gap_count = 0
            orphan_line_count = 0
            affected_point_ids: list[str] = []
            affected_segment_ids: list[str] = []
            has_issue = False
            message = f"Parcel {group_id} passed {title.lower()}."
            skip_reason = None

            if not enabled:
                status = "skipped"
                skip_reason = "Rule disabled in readiness profile."
                message = f"{title} is disabled for parcel type {parcel_type}."
            elif category == "minimum_segment_count":
                has_issue = len(ordered_rows) < min_segment_count
                status = _readiness_status(True, severity, has_issue)
                if has_issue:
                    message = f"Parcel {group_id} only has {len(ordered_rows)} segment row(s); at least {min_segment_count} are required."
                else:
                    message = f"Parcel {group_id} meets the minimum segment count."
            elif category == "boundary_completeness":
                if not bool(profile.get("require_contiguous_sequence", True)):
                    status = "skipped"
                    skip_reason = "Boundary completeness behavior is disabled in readiness profile."
                    message = f"{title} is disabled for parcel type {parcel_type}."
                else:
                    boundary_gap_count = len(missing_sequences)
                    has_issue = boundary_gap_count > 0
                    status = _readiness_status(True, severity, has_issue)
                    if has_issue:
                        affected_segment_ids = [str(value) for value in missing_sequences]
                        message = f"Parcel {group_id} has {boundary_gap_count} missing sequence value(s): {', '.join(affected_segment_ids[:10])}."
                    else:
                        message = f"Parcel {group_id} has a contiguous parcel sequence."
            elif category == "line_without_point_support":
                if not bool(profile.get("require_referenced_points", True)):
                    status = "skipped"
                    skip_reason = "Referenced-point support behavior is disabled in readiness profile."
                    message = f"{title} is disabled for parcel type {parcel_type}."
                else:
                    affected_point_ids = sorted(set(referenced_point_misses))
                    affected_segment_ids = sorted(set(segment_ids))
                    has_issue = len(affected_point_ids) > 0
                    status = _readiness_status(True, severity, has_issue)
                    if has_issue:
                        message = f"Parcel {group_id} references point id(s) that are not present in the parcel set: {', '.join(affected_point_ids[:10])}."
                    else:
                        message = f"Parcel {group_id} line references are supported by parcel points."
            elif category == "orphan_line_detection":
                if not bool(profile.get("require_chain_consistency", True)):
                    status = "skipped"
                    skip_reason = "Chain-consistency behavior is disabled in readiness profile."
                    message = f"{title} is disabled for parcel type {parcel_type}."
                else:
                    orphan_line_count = len(set(orphan_segments))
                    affected_segment_ids = sorted(set(orphan_segments))
                    has_issue = orphan_line_count > 0
                    status = _readiness_status(True, severity, has_issue)
                    if has_issue:
                        message = f"Parcel {group_id} has {orphan_line_count} segment(s) that do not follow the parcel chain cleanly."
                    else:
                        message = f"Parcel {group_id} line chain follows the parcel sequence."
            else:
                if not bool(profile.get("detect_duplicate_edges", True)):
                    status = "skipped"
                    skip_reason = "Shared-edge conflict detection is disabled in readiness profile."
                    message = f"{title} is disabled for parcel type {parcel_type}."
                else:
                    has_issue = shared_conflict_count > 0
                    status = _readiness_status(True, severity, has_issue)
                    if has_issue:
                        message = f"Parcel {group_id} has {shared_conflict_count} shared-edge or duplicate-edge conflict(s) to review."
                    else:
                        message = f"Parcel {group_id} did not produce shared-edge conflicts."

            results.append(
                {
                    "parcel_group_id": group_id,
                    "parcel_name": parcel_name,
                    "parcel_type": parcel_type,
                    "rule_id": rule_id,
                    "title": title,
                    "category": category,
                    "severity": severity,
                    "status": status,
                    "evaluation_status": "skipped" if status == "skipped" else ("failed" if has_issue else "passed"),
                    "message": message,
                    "affected_point_ids": affected_point_ids,
                    "affected_segment_ids": affected_segment_ids,
                    "boundary_gap_count": boundary_gap_count,
                    "shared_edge_conflict_count": shared_conflict_count if category == "shared_edge_consistency" else 0,
                    "orphan_line_count": orphan_line_count,
                    "rule_disabled": not enabled,
                    "rule_skip_reason": skip_reason,
                }
            )

    return results


def _normalize_parcel_group(row: dict[str, Any], index: int) -> str:
    value = (
        row.get("review_parcel_group_id")
        or row.get("parcel_group_id")
        or row.get("review_traverse_id")
        or row.get("traverse_id")
        or row.get("review_parcel_name")
        or row.get("parcel_name")
        or f"parcel-{index + 1}"
    )
    text = str(value).strip()
    return text or f"parcel-{index + 1}"


def _sequence_value(row: dict[str, Any], fallback_index: int) -> int:
    candidates = (
        row.get("review_sequence_in_group"),
        row.get("sequence_in_group"),
        row.get("seq"),
        row.get("segment_index"),
    )
    for candidate in candidates:
        try:
            if candidate is None or str(candidate).strip() == "":
                continue
            return int(str(candidate).strip())
        except ValueError:
            continue
    return fallback_index + 1


def _infer_parcel_type(rows: list[dict[str, Any]], source_files: list[dict[str, Any]], default_parcel_type: str) -> str:
    explicit_types = [
        str(row.get("parcel_type") or row.get("review_parcel_type") or "").strip()
        for row in rows
        if str(row.get("parcel_type") or row.get("review_parcel_type") or "").strip()
    ]
    if explicit_types:
        return explicit_types[0]

    if any(bool(row.get("review_is_boundary_break") if "review_is_boundary_break" in row else row.get("is_boundary_break")) for row in rows):
        return "open_boundary"

    if any(str(file_item.get("file_type", "")).lower() in {".txt", ".csv"} for file_item in source_files):
        return "imported_coordinates"

    return default_parcel_type or "standard_closed"


def _extract_point_identifier(row: dict[str, Any]) -> str:
    return _normalize_point_id(row.get("point_identifier") or row.get("point_id") or row.get("to_point"))


def _has_implicit_closing_segment(rows: list[dict[str, Any]]) -> bool:
    if len(rows) < 3:
        return False

    first_point = _extract_point_identifier(rows[0])
    if not first_point:
        return False

    if any(bool(row.get("review_is_boundary_break") if "review_is_boundary_break" in row else row.get("is_boundary_break")) for row in rows):
        return False

    first_to_point = _extract_segment_ref(rows[0], "to_point", "to_pt", "end_pt")
    if first_to_point and first_to_point != first_point:
        return False

    previous_point = first_point
    chain_rows = 0
    for row in rows[1:]:
        current_point = _extract_point_identifier(row)
        from_point = _extract_segment_ref(row, "from_point", "from_pt", "start_pt")
        to_point = _extract_segment_ref(row, "to_point", "to_pt", "end_pt")
        if not current_point or not from_point or not to_point:
            return False
        if from_point != previous_point or to_point != current_point:
            return False
        previous_point = current_point
        chain_rows += 1

    return chain_rows == len(rows) - 1


def _compute_closure_results(
    review_data: dict[str, Any],
    source_files: list[dict[str, Any]],
    rule_config: dict[str, Any],
) -> list[dict[str, Any]]:
    rows = review_data.get("rows") or []
    grouped: dict[str, list[dict[str, Any]]] = {}
    for index, row in enumerate(rows):
        if not isinstance(row, dict):
            continue
        grouped.setdefault(_normalize_parcel_group(row, index), []).append(row)

    default_profile = dict(rule_config.get("default") or {})
    profiles = rule_config.get("profiles") or {}
    results: list[dict[str, Any]] = []

    for group_id, group_rows in grouped.items():
        ordered_rows = sorted(group_rows, key=lambda row: _sequence_value(row, group_rows.index(row)))
        parcel_type = _infer_parcel_type(ordered_rows, source_files, str(default_profile.get("parcel_type") or "standard_closed"))
        profile = dict(default_profile)
        profile.update(profiles.get(parcel_type) or {})

        parcel_name = next(
            (
                str(row.get("review_parcel_name") or row.get("parcel_name") or "").strip()
                for row in ordered_rows
                if str(row.get("review_parcel_name") or row.get("parcel_name") or "").strip()
            ),
            group_id,
        )
        profile_enabled = bool(profile.get("enabled", True))
        allow_open_boundary = bool(profile.get("allow_open_boundary", False))
        severity = str(profile.get("severity") or "blocker")
        boundary_solver = review_data.get("boundary_solver") if isinstance(review_data.get("boundary_solver"), dict) else {}
        solver_status = str(boundary_solver.get("status") or "").strip().lower()
        solver_passed = (
            solver_status in {"passed", "warning"}
            and str(boundary_solver.get("geometry_source") or "").strip().lower() == "reviewed_boundary_segments"
        )

        coordinates: list[tuple[float, float]] = []
        coordinate_rows = 0
        for row in ordered_rows:
            easting = row.get("easting")
            northing = row.get("northing")
            if not _is_number(easting) or not _is_number(northing):
                continue
            coordinate_rows += 1
            coordinates.append((float(str(easting).strip()), float(str(northing).strip())))

        if solver_passed:
            closure_distance = boundary_solver.get("closure_distance_m")
            results.append(
                {
                    "parcel_group_id": group_id,
                    "parcel_name": parcel_name,
                    "parcel_type": parcel_type,
                    "profile_rule_id": profile.get("rule_id") or default_profile.get("rule_id") or "closure_default_standard",
                    "profile_title": profile.get("title") or "Closure tolerance profile",
                    "severity": severity,
                    "status": "pass",
                    "evaluation_status": "passed",
                    "message": "PXA reviewed boundary segment solver passed; point-row closure was superseded.",
                    "allow_open_boundary": allow_open_boundary,
                    "coordinate_row_count": coordinate_rows,
                    "implicit_closure_used": True,
                    "closure_distance_m": round(float(closure_distance), 6) if _is_number(closure_distance) else 0.0,
                    "misclose_ratio_denominator": None,
                    "max_closure_distance_m": profile.get("max_closure_distance_m"),
                    "warning_closure_distance_m": profile.get("warning_closure_distance_m"),
                    "min_misclose_ratio_denominator": profile.get("min_misclose_ratio_denominator"),
                    "warning_misclose_ratio_denominator": profile.get("warning_misclose_ratio_denominator"),
                    "geometry_source": boundary_solver.get("geometry_source"),
                    "computed_area_sq_m": boundary_solver.get("computed_area_sq_m"),
                    "document_area_sq_m": boundary_solver.get("document_area_sq_m"),
                    "area_delta_percent": boundary_solver.get("area_delta_percent"),
                }
            )
            continue

        closure_distance = None
        misclose_ratio_denominator = None
        status = "pass"
        message = "Closure validation passed."
        evaluation_status = "passed"

        if not profile_enabled:
            status = "pass"
            message = "Closure validation profile is disabled."
            evaluation_status = "passed"
        elif len(coordinates) < 2:
            status = "warning"
            message = "Not enough numeric coordinate rows are available to compute parcel closure."
            evaluation_status = "failed"
        else:
            start_x, start_y = coordinates[0]
            end_x, end_y = coordinates[-1]
            implicit_closure_used = _has_implicit_closing_segment(ordered_rows)

            if implicit_closure_used:
                dx = 0.0
                dy = 0.0
                closure_distance = 0.0
            else:
                dx = end_x - start_x
                dy = end_y - start_y
                closure_distance = math.hypot(dx, dy)

            total_length = 0.0
            for previous, current in zip(coordinates, coordinates[1:]):
                total_length += math.hypot(current[0] - previous[0], current[1] - previous[1])

            if implicit_closure_used and len(coordinates) >= 2:
                total_length += math.hypot(start_x - end_x, start_y - end_y)

            if closure_distance and closure_distance > 0 and total_length > 0:
                misclose_ratio_denominator = total_length / closure_distance

            max_distance = float(profile.get("max_closure_distance_m") or 0.0)
            warn_distance = float(profile.get("warning_closure_distance_m") or max_distance)
            min_ratio = float(profile.get("min_misclose_ratio_denominator") or 0.0)
            warn_ratio = float(profile.get("warning_misclose_ratio_denominator") or min_ratio)

            is_distance_block = closure_distance is not None and max_distance > 0 and closure_distance > max_distance
            is_distance_warning = closure_distance is not None and warn_distance > 0 and closure_distance > warn_distance
            is_ratio_block = (
                misclose_ratio_denominator is not None and min_ratio > 0 and misclose_ratio_denominator < min_ratio
            )
            is_ratio_warning = (
                misclose_ratio_denominator is not None and warn_ratio > 0 and misclose_ratio_denominator < warn_ratio
            )

            if allow_open_boundary:
                if is_distance_warning or is_ratio_warning:
                    status = "warning"
                    evaluation_status = "failed"
                    message = "Open-boundary parcel remains outside the configured informational closure tolerance."
                else:
                    message = "Open-boundary parcel is within the configured informational closure tolerance."
            else:
                if is_distance_block or is_ratio_block:
                    status = "blocker" if severity in {"critical", "high", "blocker"} else "warning"
                    evaluation_status = "failed"
                    message = "Closed parcel closure exceeds the configured blocking tolerance."
                elif is_distance_warning or is_ratio_warning:
                    status = "warning"
                    evaluation_status = "failed"
                    message = "Closed parcel closure exceeds the configured warning tolerance."

        results.append(
            {
                "parcel_group_id": group_id,
                "parcel_name": parcel_name,
                "parcel_type": parcel_type,
                "profile_rule_id": profile.get("rule_id") or default_profile.get("rule_id") or "closure_default_standard",
                "profile_title": profile.get("title") or "Closure tolerance profile",
                "severity": severity,
                "status": status,
                "evaluation_status": evaluation_status,
                "message": message,
                "allow_open_boundary": allow_open_boundary,
                "coordinate_row_count": coordinate_rows,
                "implicit_closure_used": implicit_closure_used if profile_enabled and len(coordinates) >= 2 else False,
                "closure_distance_m": round(closure_distance, 6) if closure_distance is not None else None,
                "misclose_ratio_denominator": round(misclose_ratio_denominator, 2) if misclose_ratio_denominator is not None else None,
                "max_closure_distance_m": profile.get("max_closure_distance_m"),
                "warning_closure_distance_m": profile.get("warning_closure_distance_m"),
                "min_misclose_ratio_denominator": profile.get("min_misclose_ratio_denominator"),
                "warning_misclose_ratio_denominator": profile.get("warning_misclose_ratio_denominator"),
            }
        )

    return results


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
    source_root: Path | None,
    dwg_context_path: Path | None,
    settings_path: Path | None,
) -> dict[str, Any]:
    rule_profile, rule_version = _read_rules_metadata(rules_path)
    settings = _load_settings(settings_path)
    closure_rule_config = _apply_profile_overrides(_read_closure_profiles(rules_path), settings)
    readiness_rule_config = _apply_readiness_profile_overrides(_read_readiness_profiles(rules_path), settings)
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
    available_source_files = []
    if source_root is not None and source_root.exists():
        available_source_files = sorted(path.name for path in source_root.iterdir() if path.is_file())

    if available_source_files:
        findings.append(
            _finding(
                "source_inputs_available",
                "Source input files are available to validation.",
                "passed",
                "passed",
                ", ".join(available_source_files[:10]),
                None,
            )
        )
    else:
        findings.append(
            _finding(
                "source_inputs_available",
                "Validation could not confirm any local source input files.",
                "warning",
                "failed",
                str(source_root) if source_root is not None else "source_root_unset",
                "Verify transaction attachments were copied into the case source folder before validation.",
            )
        )
        warnings.append("Validation did not find local source files under the case source folder.")

    has_dwg = any(str(file_item.get("file_type", "")).lower() == ".dwg" for file_item in source_files)
    if has_dwg:
        dwg_context_loaded = False
        dwg_context_evidence = "dwg_reference_detected=true"
        if dwg_context_path is not None and dwg_context_path.exists():
            try:
                dwg_context_document = _read_json(dwg_context_path)
                dwg_context_loaded = True
                dwg_context_evidence = (
                    f"dwg_reference_detected=true; "
                    f"context_keys={','.join(sorted(dwg_context_document.keys())[:10]) or 'none'}"
                )
            except json.JSONDecodeError:
                warnings.append("DWG context file exists but could not be parsed; validation continued without parsed DWG context.")
                dwg_context_evidence = "dwg_reference_detected=true; context_parse=failed"

        findings.append(
            _finding(
                "dwg_context_optional",
                "DWG reference is present for downstream validation context.",
                "info",
                "passed",
                dwg_context_evidence if dwg_context_loaded else f"{dwg_context_evidence}; context_loaded=false",
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

    closure_results = _compute_closure_results(review_data, source_files, closure_rule_config)
    readiness_results = _compute_readiness_results(review_data, source_files, readiness_rule_config)
    closure_blockers = 0
    closure_warnings = 0
    closure_passed = 0
    readiness_blockers = 0
    readiness_warnings = 0
    readiness_passed = 0
    readiness_skipped = 0
    for closure_result in closure_results:
        status = closure_result["status"]
        if status == "blocker":
            closure_blockers += 1
            findings.append(
                _finding(
                    str(closure_result["profile_rule_id"]),
                    f"{closure_result['parcel_name']}: closure check",
                    "high",
                    "failed",
                    (
                        f"parcel_group={closure_result['parcel_group_id']}; "
                        f"closure_distance_m={closure_result['closure_distance_m']}; "
                        f"misclose_ratio_denominator={closure_result['misclose_ratio_denominator']}"
                    ),
                    "Adjust the parcel point sequence or coordinates until closure falls within the blocking tolerance.",
                )
            )
        elif status == "warning":
            closure_warnings += 1
            findings.append(
                _finding(
                    str(closure_result["profile_rule_id"]),
                    f"{closure_result['parcel_name']}: closure check",
                    "warning",
                    "failed",
                    (
                        f"parcel_group={closure_result['parcel_group_id']}; "
                        f"closure_distance_m={closure_result['closure_distance_m']}; "
                        f"misclose_ratio_denominator={closure_result['misclose_ratio_denominator']}"
                    ),
                    "Review parcel closure before final approval.",
                )
            )
            warnings.append(f"{closure_result['parcel_name']} remains outside the configured closure warning tolerance.")
        else:
            closure_passed += 1
            findings.append(
                _finding(
                    str(closure_result["profile_rule_id"]),
                    f"{closure_result['parcel_name']}: closure check",
                    "passed",
                    "passed",
                    (
                        f"parcel_group={closure_result['parcel_group_id']}; "
                        f"closure_distance_m={closure_result['closure_distance_m']}; "
                        f"misclose_ratio_denominator={closure_result['misclose_ratio_denominator']}"
                    ),
                    None,
                )
            )

    for readiness_result in readiness_results:
        status = readiness_result["status"]
        if status == "blocker":
            readiness_blockers += 1
            findings.append(
                _finding(
                    str(readiness_result["rule_id"]),
                    f"{readiness_result['parcel_name']}: {readiness_result['title']}",
                    "high",
                    "failed",
                    (
                        f"parcel_group={readiness_result['parcel_group_id']}; "
                        f"category={readiness_result['category']}; "
                        f"boundary_gap_count={readiness_result['boundary_gap_count']}; "
                        f"shared_edge_conflict_count={readiness_result['shared_edge_conflict_count']}; "
                        f"orphan_line_count={readiness_result['orphan_line_count']}"
                    ),
                    "Resolve the parcel construction readiness issue before Create Spatial Units proceeds.",
                )
            )
        elif status == "warning":
            readiness_warnings += 1
            findings.append(
                _finding(
                    str(readiness_result["rule_id"]),
                    f"{readiness_result['parcel_name']}: {readiness_result['title']}",
                    "warning",
                    "failed",
                    (
                        f"parcel_group={readiness_result['parcel_group_id']}; "
                        f"category={readiness_result['category']}; "
                        f"boundary_gap_count={readiness_result['boundary_gap_count']}; "
                        f"shared_edge_conflict_count={readiness_result['shared_edge_conflict_count']}; "
                        f"orphan_line_count={readiness_result['orphan_line_count']}"
                    ),
                    "Review the parcel construction readiness warning before final approval.",
                )
            )
        elif status == "skipped":
            readiness_skipped += 1
            findings.append(
                _finding(
                    str(readiness_result["rule_id"]),
                    f"{readiness_result['parcel_name']}: {readiness_result['title']}",
                    "info",
                    "passed",
                    readiness_result["rule_skip_reason"] or "Rule skipped.",
                    None,
                )
            )
        else:
            readiness_passed += 1
            findings.append(
                _finding(
                    str(readiness_result["rule_id"]),
                    f"{readiness_result['parcel_name']}: {readiness_result['title']}",
                    "passed",
                    "passed",
                    (
                        f"parcel_group={readiness_result['parcel_group_id']}; "
                        f"category={readiness_result['category']}"
                    ),
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
        "created_at": _utc_now_iso(),
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
            "closure_summary": {
                "blocker": closure_blockers,
                "warning": closure_warnings,
                "passed": closure_passed,
            },
            "closure_results": closure_results,
            "readiness_summary": {
                "blocker": readiness_blockers,
                "warning": readiness_warnings,
                "passed": readiness_passed,
                "skipped": readiness_skipped,
            },
            "readiness_results": readiness_results,
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
    parser.add_argument("--source-root", default="")
    parser.add_argument("--dwg-context", default="")
    parser.add_argument("--output", required=True)
    parser.add_argument("--operator", default="")
    parser.add_argument("--rules", default="")
    parser.add_argument("--settings", default="")
    args = parser.parse_args(argv)

    manifest_path = Path(args.manifest)
    approved_review_path = Path(args.approved_review)
    review_data_path = Path(args.review_data)
    source_root = Path(args.source_root) if args.source_root else None
    dwg_context_path = Path(args.dwg_context) if args.dwg_context else None
    output_path = Path(args.output)
    rules_path = Path(args.rules) if args.rules else None
    settings_path = Path(args.settings) if args.settings else None

    summary = build_summary(
        _read_json(manifest_path),
        _read_json(approved_review_path),
        _read_json(review_data_path),
        args.operator or None,
        rules_path,
        source_root,
        dwg_context_path,
        settings_path,
    )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    return 0


def run(input_json_path: str, output_json_path: str) -> None:
    raise NotImplementedError("Use the CLI entry point for validation_adapter.py.")


if __name__ == "__main__":
    sys.exit(main())
