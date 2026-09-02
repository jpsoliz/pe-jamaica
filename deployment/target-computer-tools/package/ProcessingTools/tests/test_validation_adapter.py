import json
import tempfile
import unittest
from datetime import datetime
from pathlib import Path

from adapters import validation_adapter


class ValidationAdapterTests(unittest.TestCase):
    def test_validation_adapter_writes_passed_summary(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_path = temp_path / "validation_summary.json"
            rules_path = temp_path / "rules.yaml"
            source_root = temp_path / "source"
            source_root.mkdir()
            (source_root / "computation.pdf").write_text("source", encoding="utf-8")

            manifest_path.write_text(
                json.dumps(
                    {
                        "transaction_id": "100000206",
                        "payload": {
                            "script_plan": {"source_manifest_hash": "hash-123"},
                            "source_files": [{"file_type": ".pdf"}, {"file_type": ".pdf"}],
                        },
                    }
                ),
                encoding="utf-8",
            )
            approved_path.write_text(
                json.dumps({"review_hash": "hash-review", "approved_at": "2026-06-12T00:00:00Z", "approved_by": "tester"}),
                encoding="utf-8",
            )
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000206",
                        "review_hash": "hash-review",
                        "rows": [
                            {"row_id": "1", "parcel_group_id": "parcel-001", "sequence_in_group": 1, "point_identifier": "P1", "easting": "1000.0", "northing": "2000.0"},
                            {"row_id": "2", "parcel_group_id": "parcel-001", "sequence_in_group": 2, "point_identifier": "P2", "easting": "1010.0", "northing": "2000.0"},
                            {"row_id": "3", "parcel_group_id": "parcel-001", "sequence_in_group": 3, "point_identifier": "P3", "easting": "1000.0", "northing": "2000.0"},
                        ],
                    }
                ),
                encoding="utf-8",
            )
            rules_path.write_text("rule_profile: sidwell_validation_v1\nrule_version: 1.0.0\n", encoding="utf-8")

            exit_code = validation_adapter.main(
                [
                    "--manifest",
                    str(manifest_path),
                    "--approved-review",
                    str(approved_path),
                    "--review-data",
                    str(review_path),
                    "--source-root",
                    str(source_root),
                    "--output",
                    str(output_path),
                    "--operator",
                    "tester",
                    "--rules",
                    str(rules_path),
                ]
            )

            self.assertEqual(0, exit_code)
            summary = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("passed", summary["payload"]["status"])
            self.assertEqual("sidwell_validation_v1", summary["payload"]["rule_profile"])
            self.assertEqual(0, summary["payload"]["finding_counts"]["high"])
            self.assertIn("source_inputs_available", {item["rule_id"] for item in summary["payload"]["findings"]})
            self.assertIn("closure_results", summary["payload"])
            self.assertEqual(1, summary["payload"]["closure_summary"]["passed"])
            datetime.fromisoformat(summary["created_at"].replace("Z", "+00:00"))

    def test_validation_adapter_writes_blocked_summary_for_stale_review(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_path = temp_path / "validation_summary.json"

            manifest_path.write_text(json.dumps({"transaction_id": "100000206", "payload": {}}), encoding="utf-8")
            approved_path.write_text(json.dumps({"review_hash": "approved-hash"}), encoding="utf-8")
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000206",
                        "review_hash": "current-hash",
                        "rows": [],
                    }
                ),
                encoding="utf-8",
            )

            exit_code = validation_adapter.main(
                [
                    "--manifest",
                    str(manifest_path),
                    "--approved-review",
                    str(approved_path),
                    "--review-data",
                    str(review_path),
                    "--output",
                    str(output_path),
                ]
            )

            self.assertEqual(0, exit_code)
            summary = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("blocked", summary["payload"]["status"])
            self.assertGreater(summary["payload"]["finding_counts"]["critical"], 0)

    def test_validation_adapter_blocks_on_closure_profile(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_path = temp_path / "validation_summary.json"
            rules_path = temp_path / "rules.yaml"

            manifest_path.write_text(
                json.dumps(
                    {
                        "transaction_id": "100000236",
                        "payload": {"source_files": [{"file_type": ".pdf"}]},
                    }
                ),
                encoding="utf-8",
            )
            approved_path.write_text(json.dumps({"review_hash": "hash-review"}), encoding="utf-8")
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000236",
                        "review_hash": "hash-review",
                        "rows": [
                            {
                                "row_id": "1",
                                "parcel_group_id": "parcel-001",
                                "sequence_in_group": 1,
                                "point_identifier": "P1",
                                "easting": "1000",
                                "northing": "1000",
                            },
                            {
                                "row_id": "2",
                                "parcel_group_id": "parcel-001",
                                "sequence_in_group": 2,
                                "point_identifier": "P2",
                                "easting": "1100",
                                "northing": "1000",
                            },
                            {
                                "row_id": "3",
                                "parcel_group_id": "parcel-001",
                                "sequence_in_group": 3,
                                "point_identifier": "P3",
                                "easting": "1110",
                                "northing": "1010",
                            },
                        ],
                    }
                ),
                encoding="utf-8",
            )
            rules_path.write_text(
                "\n".join(
                    [
                        "rule_profile: sidwell_validation_v1",
                        "rule_version: 1.0.0",
                        "closure_tolerance_defaults:",
                        "  rule_id: closure_default_standard",
                        "  parcel_type: standard_closed",
                        "  enabled: true",
                        "  severity: blocker",
                        "  allow_open_boundary: false",
                        "  max_closure_distance_m: 0.3",
                        "  min_misclose_ratio_denominator: 2500",
                        "  warning_closure_distance_m: 0.15",
                        "  warning_misclose_ratio_denominator: 4000",
                        "closure_tolerance_profiles:",
                        "  - rule_id: closure_standard_plan_exam",
                        "    parcel_type: standard_closed",
                        "    enabled: true",
                        "    severity: blocker",
                        "    allow_open_boundary: false",
                        "    max_closure_distance_m: 0.3",
                        "    min_misclose_ratio_denominator: 2500",
                        "    warning_closure_distance_m: 0.15",
                        "    warning_misclose_ratio_denominator: 4000",
                    ]
                ),
                encoding="utf-8",
            )

            exit_code = validation_adapter.main(
                [
                    "--manifest",
                    str(manifest_path),
                    "--approved-review",
                    str(approved_path),
                    "--review-data",
                    str(review_path),
                    "--output",
                    str(output_path),
                    "--rules",
                    str(rules_path),
                ]
            )

            self.assertEqual(0, exit_code)
            summary = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("blocked", summary["payload"]["status"])
            self.assertEqual(1, summary["payload"]["closure_summary"]["blocker"])
            self.assertGreaterEqual(summary["payload"]["finding_counts"]["high"], 1)

    def test_validation_adapter_treats_structured_traverse_rows_as_implicit_closed_ring(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_path = temp_path / "validation_summary.json"
            rules_path = temp_path / "rules.yaml"

            manifest_path.write_text(
                json.dumps(
                    {
                        "transaction_id": "100000379",
                        "payload": {"source_files": [{"file_type": ".pdf"}]},
                    }
                ),
                encoding="utf-8",
            )
            approved_path.write_text(json.dumps({"review_hash": "hash-review"}), encoding="utf-8")
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000379",
                        "review_hash": "hash-review",
                        "rows": [
                            {
                                "row_id": "1",
                                "parcel_group_id": "parcel-001",
                                "sequence_in_group": 1,
                                "point_identifier": "338",
                                "to_point": "338",
                                "easting": "680920.044",
                                "northing": "639209.180",
                            },
                            {
                                "row_id": "2",
                                "parcel_group_id": "parcel-001",
                                "sequence_in_group": 2,
                                "point_identifier": "339",
                                "from_point": "338",
                                "to_point": "339",
                                "easting": "680912.604",
                                "northing": "639210.742",
                            },
                            {
                                "row_id": "3",
                                "parcel_group_id": "parcel-001",
                                "sequence_in_group": 3,
                                "point_identifier": "340",
                                "from_point": "339",
                                "to_point": "340",
                                "easting": "680912.453",
                                "northing": "639208.761",
                            },
                            {
                                "row_id": "4",
                                "parcel_group_id": "parcel-001",
                                "sequence_in_group": 4,
                                "point_identifier": "337",
                                "from_point": "340",
                                "to_point": "337",
                                "easting": "680921.968",
                                "northing": "639216.482",
                            },
                        ],
                    }
                ),
                encoding="utf-8",
            )
            rules_path.write_text(
                "\n".join(
                    [
                        "rule_profile: sidwell_validation_v1",
                        "rule_version: 1.0.0",
                        "closure_tolerance_defaults:",
                        "  rule_id: closure_default_standard",
                        "  parcel_type: standard_closed",
                        "  enabled: true",
                        "  severity: blocker",
                        "  allow_open_boundary: false",
                        "  max_closure_distance_m: 0.3",
                        "  min_misclose_ratio_denominator: 2500",
                        "  warning_closure_distance_m: 0.15",
                        "  warning_misclose_ratio_denominator: 4000",
                    ]
                ),
                encoding="utf-8",
            )

            exit_code = validation_adapter.main(
                [
                    "--manifest",
                    str(manifest_path),
                    "--approved-review",
                    str(approved_path),
                    "--review-data",
                    str(review_path),
                    "--output",
                    str(output_path),
                    "--rules",
                    str(rules_path),
                ]
            )

            self.assertEqual(0, exit_code)
            summary = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("passed", summary["payload"]["status"])
            self.assertEqual(1, summary["payload"]["closure_summary"]["passed"])
            self.assertTrue(summary["payload"]["closure_results"][0]["implicit_closure_used"])
            self.assertEqual(0.0, summary["payload"]["closure_results"][0]["closure_distance_m"])

    def test_validation_adapter_writes_readiness_results_for_sequence_gap(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_path = temp_path / "validation_summary.json"
            rules_path = temp_path / "rules.yaml"

            manifest_path.write_text(
                json.dumps(
                    {
                        "transaction_id": "100000379",
                        "payload": {"source_files": [{"file_type": ".pdf"}]},
                    }
                ),
                encoding="utf-8",
            )
            approved_path.write_text(json.dumps({"review_hash": "hash-review"}), encoding="utf-8")
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000379",
                        "review_hash": "hash-review",
                        "rows": [
                            {"row_id": "1", "parcel_group_id": "parcel-001", "sequence_in_group": 1, "point_identifier": "P1", "easting": "1000", "northing": "1000"},
                            {"row_id": "2", "parcel_group_id": "parcel-001", "sequence_in_group": 3, "point_identifier": "P2", "easting": "1010", "northing": "1000"},
                            {"row_id": "3", "parcel_group_id": "parcel-001", "sequence_in_group": 4, "point_identifier": "P3", "easting": "1000", "northing": "1000"},
                        ],
                    }
                ),
                encoding="utf-8",
            )
            rules_path.write_text(
                "\n".join(
                    [
                        "rule_profile: sidwell_validation_v1",
                        "rule_version: 1.0.0",
                        "parcel_construction_readiness_defaults:",
                        "  parcel_type: standard_closed",
                        "  enabled: true",
                        "  severity: blocker",
                        "  min_segment_count: 3",
                        "parcel_construction_readiness_profiles:",
                        "  - rule_id: readiness_boundary_completeness",
                        "    title: Boundary completeness",
                        "    category: boundary_completeness",
                        "    parcel_type: standard_closed",
                        "    enabled: true",
                        "    severity: blocker",
                        "  - rule_id: readiness_minimum_segment_count",
                        "    title: Minimum segment count",
                        "    category: minimum_segment_count",
                        "    parcel_type: standard_closed",
                        "    enabled: true",
                        "    severity: blocker",
                        "    min_segment_count: 3",
                    ]
                ),
                encoding="utf-8",
            )

            exit_code = validation_adapter.main(
                [
                    "--manifest",
                    str(manifest_path),
                    "--approved-review",
                    str(approved_path),
                    "--review-data",
                    str(review_path),
                    "--output",
                    str(output_path),
                    "--rules",
                    str(rules_path),
                ]
            )

            self.assertEqual(0, exit_code)
            summary = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("blocked", summary["payload"]["status"])
            self.assertIn("readiness_summary", summary["payload"])
            self.assertEqual(1, summary["payload"]["readiness_summary"]["blocker"])
            self.assertTrue(
                any(
                    result["category"] == "boundary_completeness" and result["status"] == "blocker"
                    for result in summary["payload"]["readiness_results"]
                )
            )

    def test_validation_adapter_uses_passed_pxa_boundary_solver_for_closure(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_path = temp_path / "validation_summary.json"
            rules_path = temp_path / "rules.yaml"

            manifest_path.write_text(
                json.dumps(
                    {
                        "transaction_id": "100000562",
                        "payload": {"source_files": [{"file_type": ".pdf"}]},
                    }
                ),
                encoding="utf-8",
            )
            approved_path.write_text(json.dumps({"review_hash": "hash-review"}), encoding="utf-8")
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000562",
                        "review_hash": "hash-review",
                        "validation_profile": "single_parcel_survey_plan_v1",
                        "boundary_solver": {
                            "status": "passed",
                            "geometry_source": "reviewed_boundary_segments",
                            "closure_distance_m": 0,
                            "computed_area_sq_m": 854.8521118164062,
                            "document_area_sq_m": 854.807,
                            "area_delta_percent": 0.005277427115855817,
                        },
                        "rows": [
                            {"row_id": "15", "parcel_group_id": "parcel-001", "sequence_in_group": 2, "point_identifier": "15", "easting": "712897.345", "northing": "670582.156"},
                            {"row_id": "17", "parcel_group_id": "parcel-001", "sequence_in_group": 5, "point_identifier": "17", "easting": "712856.553", "northing": "670563.653"},
                            {"row_id": "16", "parcel_group_id": "parcel-001", "sequence_in_group": 4, "point_identifier": "16", "easting": "712897.659", "northing": "670558.591"},
                            {"row_id": "18", "parcel_group_id": "parcel-001", "sequence_in_group": 1, "point_identifier": "18", "easting": "712864.006", "northing": "670585.112"},
                            {"row_id": "derived-3", "parcel_group_id": "parcel-001", "sequence_in_group": 3, "point_identifier": "3", "easting": "712897.809", "northing": "670563.819"},
                        ],
                    }
                ),
                encoding="utf-8",
            )
            rules_path.write_text(
                "\n".join(
                    [
                        "rule_profile: sidwell_validation_v1",
                        "rule_version: 1.0.0",
                        "closure_tolerance_defaults:",
                        "  rule_id: closure_standard_plan_exam",
                        "  parcel_type: standard_closed",
                        "  enabled: true",
                        "  severity: blocker",
                        "  allow_open_boundary: false",
                        "  max_closure_distance_m: 0.3",
                        "  min_misclose_ratio_denominator: 2500",
                    ]
                ),
                encoding="utf-8",
            )

            exit_code = validation_adapter.main(
                [
                    "--manifest",
                    str(manifest_path),
                    "--approved-review",
                    str(approved_path),
                    "--review-data",
                    str(review_path),
                    "--output",
                    str(output_path),
                    "--rules",
                    str(rules_path),
                ]
            )

            self.assertEqual(0, exit_code)
            summary = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("passed", summary["payload"]["status"])
            self.assertEqual(0, summary["payload"]["closure_summary"]["blocker"])
            self.assertEqual(1, summary["payload"]["closure_summary"]["passed"])
            self.assertEqual("reviewed_boundary_segments", summary["payload"]["closure_results"][0]["geometry_source"])
            self.assertIn("superseded", summary["payload"]["closure_results"][0]["message"])

    def test_validation_adapter_blocks_pxa_boundary_solver_area_mismatch(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_path = temp_path / "validation_summary.json"
            rules_path = temp_path / "rules.yaml"

            manifest_path.write_text(
                json.dumps({"transaction_id": "100000862", "payload": {"source_files": [{"file_type": ".pdf"}]}}),
                encoding="utf-8",
            )
            approved_path.write_text(json.dumps({"review_hash": "hash-review"}), encoding="utf-8")
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000862",
                        "review_hash": "hash-review",
                        "validation_profile": "single_parcel_survey_plan_v1",
                        "boundary_solver": {
                            "status": "warning",
                            "geometry_source": "reviewed_boundary_segments",
                            "closure_distance_m": 0,
                            "computed_area_sq_m": 6487.991424560547,
                            "document_area_sq_m": 1874.639,
                            "area_delta_percent": 246.09284371873983,
                            "findings": [
                                "Reviewed boundary was rebuilt from bearings/distances and fitted to printed reference points 22 and 26.",
                                "Reference-fit scale factor is 1.86037.",
                            ],
                        },
                        "rows": [
                            {"row_id": "22", "parcel_group_id": "parcel-001", "sequence_in_group": 1, "point_identifier": "22", "easting": "782997.243", "northing": "643901.728"},
                            {"row_id": "26", "parcel_group_id": "parcel-001", "sequence_in_group": 2, "point_identifier": "26", "easting": "782940.146", "northing": "643944.147"},
                        ],
                    }
                ),
                encoding="utf-8",
            )
            rules_path.write_text(
                "\n".join(
                    [
                        "rule_profile: sidwell_validation_v1",
                        "rule_version: 1.0.0",
                        "closure_tolerance_defaults:",
                        "  rule_id: closure_standard_plan_exam",
                        "  parcel_type: standard_closed",
                        "  enabled: true",
                        "  severity: blocker",
                        "  allow_open_boundary: false",
                        "  max_closure_distance_m: 0.3",
                        "  min_misclose_ratio_denominator: 2500",
                        "  max_area_delta_percent: 5.0",
                    ]
                ),
                encoding="utf-8",
            )

            exit_code = validation_adapter.main(
                [
                    "--manifest",
                    str(manifest_path),
                    "--approved-review",
                    str(approved_path),
                    "--review-data",
                    str(review_path),
                    "--output",
                    str(output_path),
                    "--rules",
                    str(rules_path),
                ]
            )

            self.assertEqual(0, exit_code)
            summary = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("blocked", summary["payload"]["status"])
            self.assertEqual(1, summary["payload"]["closure_summary"]["blocker"])
            self.assertIn("area differs", summary["payload"]["closure_results"][0]["message"])

    def test_validation_adapter_uses_warning_pxa_boundary_solver_for_closure(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_path = temp_path / "validation_summary.json"
            rules_path = temp_path / "rules.yaml"

            manifest_path.write_text(
                json.dumps({"transaction_id": "100000854", "payload": {"source_files": [{"file_type": ".pdf"}]}}),
                encoding="utf-8",
            )
            approved_path.write_text(json.dumps({"review_hash": "hash-review"}), encoding="utf-8")
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000854",
                        "review_hash": "hash-review",
                        "validation_profile": "single_parcel_survey_plan_v1",
                        "boundary_solver": {
                            "status": "warning",
                            "geometry_source": "reviewed_boundary_segments",
                            "closure_distance_m": 0,
                            "findings": ["Point B was recalculated from the reviewed boundary path."],
                        },
                        "rows": [
                            {"row_id": "A", "parcel_group_id": "parcel-001", "sequence_in_group": 1, "point_identifier": "A", "easting": "0", "northing": "0"},
                            {"row_id": "B", "parcel_group_id": "parcel-001", "sequence_in_group": 2, "point_identifier": "B", "easting": "5", "northing": "5"},
                            {"row_id": "1", "parcel_group_id": "parcel-001", "sequence_in_group": 2, "point_identifier": "1", "easting": "10", "northing": "0"},
                        ],
                    }
                ),
                encoding="utf-8",
            )
            rules_path.write_text(
                "\n".join(
                    [
                        "rule_profile: sidwell_validation_v1",
                        "rule_version: 1.0.0",
                        "closure_tolerance_defaults:",
                        "  rule_id: closure_standard_plan_exam",
                        "  parcel_type: standard_closed",
                        "  enabled: true",
                        "  severity: blocker",
                        "  allow_open_boundary: false",
                        "  max_closure_distance_m: 0.3",
                        "  min_misclose_ratio_denominator: 2500",
                    ]
                ),
                encoding="utf-8",
            )

            exit_code = validation_adapter.main(
                [
                    "--manifest",
                    str(manifest_path),
                    "--approved-review",
                    str(approved_path),
                    "--review-data",
                    str(review_path),
                    "--output",
                    str(output_path),
                    "--rules",
                    str(rules_path),
                ]
            )

            self.assertEqual(0, exit_code)
            summary = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("passed", summary["payload"]["status"])
            self.assertEqual("reviewed_boundary_segments", summary["payload"]["closure_results"][0]["geometry_source"])

    def test_parish_validation_passes_when_points_are_inside_configured_boundary(self):
        review_data = {
            "spatial_reference_wkid": 3448,
            "survey_metadata": {"parish": {"value": "Clarendon"}},
            "rows": [
                {"point_identifier": "P1", "easting": "100", "northing": "100"},
                {"point_identifier": "P2", "easting": "120", "northing": "110"},
                {"point_identifier": "P3", "easting": "110", "northing": "120"},
            ],
        }
        settings = {
            "working_map": {
                "reference_layers": [
                    {
                        "name": "JM Parishes",
                        "url": "https://example.local/FeatureServer/0",
                        "parish_name_field": "PARISH",
                        "use_for_validation": True,
                    }
                ]
            },
            "parish_validation": {
                "boundaries": [{"name": "CLARENDON", "spatial_reference": {"wkid": 3448}, "xmin": 0, "ymin": 0, "xmax": 200, "ymax": 200}],
            },
        }

        results = validation_adapter._compute_parish_validation_results(review_data, settings)

        self.assertEqual("passed", results[0]["status"])
        self.assertEqual("georeference.parish_point_within_boundary", results[0]["rule_id"])
        self.assertEqual("passed", results[1]["status"])
        self.assertEqual("spatial_units.parish_polygon_within_boundary", results[1]["rule_id"])

    def test_parish_validation_queries_feature_service_boundary_when_local_boundary_is_missing(self):
        review_data = {
            "spatial_reference_wkid": 3448,
            "survey_metadata": {"parish": {"value": "Clarendon"}},
            "rows": [
                {"point_identifier": "P1", "easting": "100", "northing": "100"},
                {"point_identifier": "P2", "easting": "120", "northing": "100"},
                {"point_identifier": "P3", "easting": "120", "northing": "120"},
            ],
        }
        settings = {
            "working_map": {
                "reference_layers": [
                    {
                        "name": "JM Parishes",
                        "source_type": "feature_service_url",
                        "url": "https://services6.arcgis.com/3R3y1KXaPJ9BFnsU/ArcGIS/rest/services/Jamaica_Parishes_SDC_Communities/FeatureServer/0",
                        "parish_name_field": "PARISH",
                        "use_for_validation": True,
                    }
                ]
            }
        }

        original_query = validation_adapter._query_arcgis_parish_boundary
        try:
            validation_adapter._query_arcgis_parish_boundary = lambda layer, parish, out_wkid=None: (
                {
                    "name": parish,
                    "source": "feature_service_query",
                    "spatial_reference": {"wkid": 3448},
                    "rings": [[[0, 0], [200, 0], [200, 200], [0, 200], [0, 0]]],
                },
                None,
            )
            results = validation_adapter._compute_parish_validation_results(review_data, settings)
        finally:
            validation_adapter._query_arcgis_parish_boundary = original_query

        self.assertEqual("passed", results[0]["status"])
        self.assertEqual("passed", results[1]["status"])
        self.assertIn("boundary_source=feature_service_query", results[1]["evidence"])

    def test_parish_validation_requests_feature_service_geometry_in_review_spatial_reference(self):
        review_data = {
            "spatial_reference_wkid": 4326,
            "survey_metadata": {"parish": {"value": "Clarendon"}},
            "rows": [{"point_identifier": "P1", "easting": "-77.3", "northing": "18.1"}],
        }
        settings = {
            "working_map": {
                "reference_layers": [
                    {
                        "name": "JM Parishes",
                        "source_type": "feature_service_url",
                        "url": "https://services6.arcgis.com/3R3y1KXaPJ9BFnsU/ArcGIS/rest/services/Jamaica_Parishes_SDC_Communities/FeatureServer/0",
                        "parish_name_field": "PARISH",
                        "use_for_validation": True,
                    }
                ]
            }
        }
        requested_wkids = []
        original_query = validation_adapter._query_arcgis_parish_boundary
        try:
            def fake_query(layer, parish, out_wkid=None):
                requested_wkids.append(out_wkid)
                return (
                    {
                        "name": parish,
                        "source": "feature_service_query",
                        "spatial_reference": {"wkid": out_wkid},
                        "rings": [[[-78, 17], [-76, 17], [-76, 19], [-78, 19], [-78, 17]]],
                    },
                    None,
                )

            validation_adapter._query_arcgis_parish_boundary = fake_query
            results = validation_adapter._compute_parish_validation_results(review_data, settings)
        finally:
            validation_adapter._query_arcgis_parish_boundary = original_query

        self.assertEqual([4326], requested_wkids)
        self.assertEqual("passed", results[0]["status"])

    def test_feature_service_query_uses_normalized_parish_matching_after_broad_query(self):
        calls = []

        class FakeResponse:
            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc, tb):
                return False

            def read(self):
                return json.dumps(
                    {
                        "spatialReference": {"wkid": 102095, "latestWkid": 3448},
                        "features": [
                            {
                                "attributes": {"PARISH": "SAINT CATHERINE"},
                                "geometry": {"rings": [[[0, 0], [1, 0], [1, 1], [0, 1], [0, 0]]]},
                            }
                        ],
                    }
                ).encode("utf-8")

        original_urlopen = validation_adapter.urlopen
        try:
            def fake_urlopen(url, timeout):
                calls.append(url)
                return FakeResponse()

            validation_adapter.urlopen = fake_urlopen
            boundary, reason = validation_adapter._query_arcgis_parish_boundary(
                {"url": "https://example.local/FeatureServer/0", "parish_name_field": "PARISH"},
                "Saint Catherine",
                3448,
            )
        finally:
            validation_adapter.urlopen = original_urlopen

        self.assertIsNone(reason)
        self.assertEqual("SAINT CATHERINE", boundary["name"])
        self.assertIn("where=1%3D1", calls[0])
        self.assertIn("outSR=3448", calls[0])

    def test_spatial_reference_prefers_latest_wkid_alias(self):
        self.assertEqual(3448, validation_adapter._spatial_reference_wkid({"wkid": 102095, "latestWkid": 3448}))

    def test_ring_holes_are_not_treated_as_valid_parish_area(self):
        boundary = {
            "rings": [
                [[0, 0], [10, 0], [10, 10], [0, 10], [0, 0]],
                [[4, 4], [6, 4], [6, 6], [4, 6], [4, 4]],
            ]
        }

        self.assertTrue(validation_adapter._point_inside_boundary(("outer", 2, 2), boundary, 0))
        self.assertFalse(validation_adapter._point_inside_boundary(("hole", 5, 5), boundary, 0))

    def test_parish_validation_reports_overlap_when_polygon_edges_cross_without_inside_vertices(self):
        review_data = {
            "spatial_reference_wkid": 3448,
            "survey_metadata": {"parish": {"value": "Clarendon"}},
            "rows": [
                {"point_identifier": "P1", "easting": "-10", "northing": "5"},
                {"point_identifier": "P2", "easting": "20", "northing": "5"},
                {"point_identifier": "P3", "easting": "20", "northing": "6"},
                {"point_identifier": "P4", "easting": "-10", "northing": "6"},
            ],
        }
        settings = {
            "working_map": {
                "reference_layers": [{"name": "JM Parishes", "parish_name_field": "PARISH", "use_for_validation": True}]
            },
            "parish_validation": {
                "boundaries": [{"name": "Clarendon", "spatial_reference": {"wkid": 3448}, "xmin": 0, "ymin": 0, "xmax": 10, "ymax": 10}]
            },
        }

        results = validation_adapter._compute_parish_validation_results(review_data, settings)

        self.assertEqual("blocker", results[0]["status"])
        self.assertEqual("needs_review", results[1]["status"])

    def test_parish_validation_needs_review_when_spatial_reference_projection_is_required(self):
        review_data = {
            "spatial_reference_wkid": 4326,
            "survey_metadata": {"parish": {"value": "Clarendon"}},
            "rows": [{"point_identifier": "P1", "easting": "100", "northing": "100"}],
        }
        settings = {
            "working_map": {
                "reference_layers": [{"name": "JM Parishes", "parish_name_field": "PARISH", "use_for_validation": True}]
            },
            "parish_validation": {
                "boundaries": [{"name": "Clarendon", "spatial_reference": {"wkid": 3448}, "xmin": 0, "ymin": 0, "xmax": 200, "ymax": 200}]
            },
        }

        results = validation_adapter._compute_parish_validation_results(review_data, settings)

        self.assertEqual("needs_review", results[0]["status"])
        self.assertIn("projection=not_available", results[0]["evidence"])

    def test_parish_validation_reports_overlap_when_polygon_partially_intersects_boundary(self):
        review_data = {
            "spatial_reference_wkid": 3448,
            "survey_metadata": {"parish": {"value": "Clarendon"}},
            "rows": [
                {"point_identifier": "P1", "easting": "50", "northing": "50"},
                {"point_identifier": "P2", "easting": "250", "northing": "50"},
                {"point_identifier": "P3", "easting": "250", "northing": "250"},
            ],
        }
        settings = {
            "working_map": {
                "reference_layers": [{"name": "JM Parishes", "parish_name_field": "PARISH", "use_for_validation": True}]
            },
            "parish_validation": {
                "boundaries": [{"name": "Clarendon", "spatial_reference": {"wkid": 3448}, "xmin": 0, "ymin": 0, "xmax": 200, "ymax": 200}]
            },
        }

        results = validation_adapter._compute_parish_validation_results(review_data, settings)

        self.assertEqual("blocker", results[0]["status"])
        self.assertEqual("needs_review", results[1]["status"])
        self.assertIn("intersects", results[1]["message"])

    def test_parish_validation_returns_not_available_when_layer_is_missing(self):
        review_data = {"survey_metadata": {"parish": {"value": "Clarendon"}}, "rows": []}

        results = validation_adapter._compute_parish_validation_results(review_data, {"working_map": {"reference_layers": []}})

        self.assertEqual(1, len(results))
        self.assertEqual("not_available", results[0]["status"])
        self.assertIn("No working map reference layer", results[0]["message"])

    def test_plan_compute_sheet_comparison_reports_unavailable_when_sheet_rows_are_missing(self):
        results = validation_adapter._compute_plan_compute_sheet_consistency_results(
            {"rows": [{"point_identifier": "P1", "easting": "100.00", "northing": "200.00"}]},
            {"plan_compute_sheet_consistency": {}},
        )

        self.assertEqual(2, len(results))
        self.assertEqual("pxa.embedded_compute_sheet_detected", results[0]["rule_id"])
        self.assertEqual("not_available", results[0]["status"])
        self.assertEqual("pxa.plan_compute_sheet_consistency", results[1]["rule_id"])
        self.assertEqual("not_available", results[1]["status"])
        self.assertIn("plan_points=1", results[1]["evidence"])
        self.assertIn("sheet_points=0", results[1]["evidence"])
    def test_plan_compute_sheet_comparison_blocks_coordinate_mismatch(self):
        review_data = {
            "rows": [{"point_identifier": "P1", "easting": "100.00", "northing": "200.00"}],
            "embedded_compute_sheet": {
                "status": "detected",
                "page_numbers": [2],
                "rows": [{"point_identifier": "P1", "easting": "101.50", "northing": "200.00"}],
            },
        }

        results = validation_adapter._compute_plan_compute_sheet_consistency_results(
            review_data,
            {"plan_compute_sheet_consistency": {"coordinate_tolerance_m": 0.1}},
        )

        self.assertEqual("blocker", results[0]["status"])
        self.assertIn("P1", results[0]["mismatches"][0])

    def test_printed_text_size_supports_minimum_and_unavailable_metrics(self):
        failing = validation_adapter._compute_printed_text_size_result(
            {"document_text_metrics": {"pages": [{"page_number": 1, "width_mm": 210, "height_mm": 297, "page_standard": "pdf_metadata", "text_runs": [{"height_mm": 1.8, "text": "small"}]}]}},
            {"printed_text_size": {"threshold_mm": 2.0, "comparison": "minimum"}},
        )
        unavailable = validation_adapter._compute_printed_text_size_result({}, {"printed_text_size": {}})

        self.assertEqual("blocker", failing["status"])
        self.assertIn("ordinary_plan_text_excludes_titles_subtitles", failing["evidence"])
        self.assertEqual("not_available", unavailable["status"])

    def test_printed_text_size_excludes_title_and_subtitle_runs(self):
        result = validation_adapter._compute_printed_text_size_result(
            {
                "document_text_metrics": {
                    "pages": [
                        {
                            "page_number": 1,
                            "width_mm": 420,
                            "height_mm": 297,
                            "page_standard": "pdf_metadata",
                            "text_runs": [
                                {"height_mm": 12.0, "text": "SURVEY PLAN", "bbox": [10, 10, 180, 42]},
                                {"height_mm": 6.5, "text": "ROXBURY PLANTATION", "bbox": [10, 45, 200, 66]},
                                {"height_mm": 2.2, "text": "Boundary bearing label", "bbox": [80, 120, 160, 128]},
                                {"height_mm": 2.1, "text": "Coordinate table value", "bbox": [80, 140, 170, 148]},
                            ],
                        }
                    ]
                }
            },
            {"printed_text_size": {"threshold_mm": 2.0, "comparison": "minimum"}},
        )

        self.assertEqual("passed", result["status"])
        self.assertIn("observed_mm=2.100", result["evidence"])
        self.assertIn("excluded_title_subtitle_runs=2", result["evidence"])
    def test_plan_compute_sheet_comparison_checks_distance_bearing_and_missing_points(self):
        review_data = {
            "rows": [
                {
                    "point_identifier": "P1",
                    "parcel_group_id": "parcel-001",
                    "from_point": "P0",
                    "to_point": "P1",
                    "easting": "100.00",
                    "northing": "200.00",
                    "distance_m": "10.00",
                    "bearing_degrees": "45.00",
                },
                {"point_identifier": "P2", "easting": "110.00", "northing": "200.00"},
            ],
            "area_m2": "100.0",
            "embedded_compute_sheet": {
                "status": "detected",
                "page_numbers": [3],
                "area_m2": "101.0",
                "rows": [
                    {
                        "point_identifier": "P1",
                        "parcel_group_id": "parcel-002",
                        "from_point": "P0",
                        "to_point": "P1",
                        "easting": "100.00",
                        "northing": "200.00",
                        "distance_m": "10.50",
                        "bearing_degrees": "45.50",
                    },
                    {"point_identifier": "P3", "easting": "120.00", "northing": "200.00"},
                ],
            },
        }

        results = validation_adapter._compute_plan_compute_sheet_consistency_results(
            review_data,
            {
                "plan_compute_sheet_consistency": {
                    "coordinate_tolerance_m": 0.1,
                    "distance_tolerance_m": 0.1,
                    "bearing_tolerance_degrees": 0.1,
                    "area_tolerance_percent": 0.5,
                }
            },
        )

        self.assertEqual("blocker", results[0]["status"])
        self.assertTrue(any("missing from compute sheet" in mismatch for mismatch in results[0]["mismatches"]))
        self.assertTrue(any("extra compute-sheet points" in mismatch for mismatch in results[0]["mismatches"]))
        self.assertTrue(any("distance_m" in mismatch for mismatch in results[0]["mismatches"]))
        self.assertTrue(any("bearing_degrees" in mismatch for mismatch in results[0]["mismatches"]))
        self.assertTrue(any("parcel_group_id" in mismatch for mismatch in results[0]["mismatches"]))
        self.assertTrue(any("area_m2" in mismatch for mismatch in results[0]["mismatches"]))

    def test_named_rule_sections_read_top_level_validation_defaults(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            rules_path = Path(temp_dir) / "rules.yaml"
            rules_path.write_text(
                "\n".join(
                    [
                        "rule_profile: sidwell_validation_v1",
                        "parish_validation:",
                        "  tolerance_m: 1.5",
                        "plan_compute_sheet_consistency:",
                        "  coordinate_tolerance_m: 0.25",
                        "  bearing_tolerance_degrees: 0.02",
                        "printed_text_size:",
                        "  threshold_mm: 2.0",
                        "  comparison: minimum",
                    ]
                ),
                encoding="utf-8",
            )

            sections = validation_adapter._read_named_rule_sections(
                rules_path,
                {"parish_validation", "plan_compute_sheet_consistency", "printed_text_size"},
            )

            self.assertEqual(1.5, sections["parish_validation"]["tolerance_m"])
            self.assertEqual(0.25, sections["plan_compute_sheet_consistency"]["coordinate_tolerance_m"])
            self.assertEqual("minimum", sections["printed_text_size"]["comparison"])
if __name__ == "__main__":
    unittest.main()
