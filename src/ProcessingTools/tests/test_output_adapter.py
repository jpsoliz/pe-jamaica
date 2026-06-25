import json
import os
import tempfile
import unittest
from pathlib import Path

from adapters import output_adapter


class OutputAdapterTests(unittest.TestCase):
    def test_output_adapter_writes_output_summary_and_geojson(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_root = temp_path / "output"
            output_summary_path = output_root / "output_summary.json"

            manifest_path.write_text(
                json.dumps(
                    {
                        "transaction_id": "100000206",
                        "payload": {
                            "script_plan": {"source_manifest_hash": "hash-123"},
                        },
                    }
                ),
                encoding="utf-8",
            )
            approved_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000206",
                        "review_hash": "approved-hash",
                        "approved_by": "tester",
                    }
                ),
                encoding="utf-8",
            )
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000206",
                        "review_hash": "approved-hash",
                        "rows": [
                            {"row_id": "1", "point_identifier": "P1", "easting": "1000.0", "northing": "2000.0"},
                            {"row_id": "2", "point_identifier": "P2", "easting": "1010.0", "northing": "2000.0"},
                            {"row_id": "3", "point_identifier": "P3", "easting": "1010.0", "northing": "2010.0"},
                        ],
                    }
                ),
                encoding="utf-8",
            )

            previous = os.environ.get("SIDWELL_OUTPUT_ADAPTER_TEST_MODE")
            os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = "1"
            try:
                exit_code = output_adapter.main(
                    [
                        "--manifest",
                        str(manifest_path),
                        "--approved-review",
                        str(approved_path),
                        "--review-data",
                        str(review_path),
                        "--add-cogo-attributes",
                        "true",
                        "--add-cogo-labels",
                        "true",
                        "--cogo-source-mode",
                        "source_then_computed",
                        "--output-root",
                        str(output_root),
                        "--output-summary",
                        str(output_summary_path),
                        "--operator",
                        "tester",
                    ]
                )
            finally:
                if previous is None:
                    os.environ.pop("SIDWELL_OUTPUT_ADAPTER_TEST_MODE", None)
                else:
                    os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = previous

            self.assertEqual(0, exit_code)
            self.assertTrue(output_summary_path.exists())
            self.assertTrue((output_root / "extracted_geometry.geojson").exists())
            summary = json.loads(output_summary_path.read_text(encoding="utf-8"))
            self.assertEqual("created", summary["payload"]["status"])
            self.assertEqual(3, summary["payload"]["point_count"])
            self.assertEqual(2, summary["payload"]["line_count"])
            self.assertEqual(1, summary["payload"]["polygon_count"])
            self.assertTrue(summary["payload"]["add_cogo_attributes"])
            self.assertTrue(summary["payload"]["add_cogo_labels"])
            self.assertEqual("source_then_computed", summary["payload"]["cogo_source_mode"])
            self.assertEqual("non_fabric", summary["payload"]["map_load_mode"])
            self.assertTrue(summary["payload"]["bearing_txt_populated"])
            self.assertEqual(2, summary["payload"]["bearing_txt_populated_count"])
            self.assertTrue(summary["payload"]["distance_txt_populated"])
            self.assertEqual(2, summary["payload"]["distance_txt_populated_count"])
            self.assertEqual(2, summary["payload"]["computed_cogo_fallback_line_count"])
            self.assertIn("root_line_feature_class_diagnostic", summary["payload"])
            self.assertTrue(summary["payload"]["root_line_bearing_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_distance_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_length_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_distance_m_exists"])
            self.assertEqual(2, summary["payload"]["root_line_feature_class_diagnostic"]["row_count"])
            self.assertTrue(summary["payload"]["result_gdb_path"].endswith(".gdb"))
            self.assertTrue(summary["payload"]["polygon_feature_class_path"].endswith("parcel_polygons"))

            point_rows = json.loads(Path(summary["payload"]["point_feature_class_path"]).read_text(encoding="utf-8"))
            line_rows = json.loads(Path(summary["payload"]["line_feature_class_path"]).read_text(encoding="utf-8"))
            polygon_rows = json.loads(Path(summary["payload"]["polygon_feature_class_path"]).read_text(encoding="utf-8"))

            self.assertEqual("parcel-001", point_rows[0]["parcel_id"])
            self.assertEqual(1, point_rows[0]["point_order"])
            self.assertEqual("100000206", point_rows[0]["transaction_number"])
            self.assertEqual("parcel_workflow_compute", point_rows[0]["workflow_name"])
            self.assertEqual("spatial_units_created", point_rows[0]["workflow_stage"])
            self.assertEqual("approved", point_rows[0]["review_state"])
            self.assertIn("doc_type_id", point_rows[0])
            self.assertIn("source_doc", point_rows[0])
            self.assertIn("parcel_group_id", point_rows[0])
            self.assertIn("point_role", point_rows[0])
            self.assertIn("distance_txt", point_rows[0])
            self.assertEqual("parcel-001", line_rows[0]["parcel_id"])
            self.assertEqual(1, line_rows[0]["segment_order"])
            self.assertEqual("100000206", line_rows[0]["transaction_number"])
            self.assertIn("distance_m", line_rows[0])
            self.assertIn("bearing_txt", line_rows[0])
            self.assertIn("distance_txt", line_rows[0])
            self.assertIn("azimuth_deg", line_rows[0])
            self.assertIn("is_computed_cogo", line_rows[0])
            self.assertIn("from_point_id", line_rows[0])
            self.assertIn("to_point_id", line_rows[0])
            self.assertIn("line_type", line_rows[0])
            self.assertEqual("parcel-001", polygon_rows[0]["parcel_id"])
            self.assertEqual("parcel-001", polygon_rows[0]["parcel_name"])
            self.assertEqual("100000206", polygon_rows[0]["transaction_number"])
            self.assertIn("parcel_group_id", polygon_rows[0])
            self.assertIn("polygon_order", polygon_rows[0])
            self.assertIn("point_count", polygon_rows[0])
            self.assertIn("perimeter_m", polygon_rows[0])
            self.assertIn("area_sq_m", polygon_rows[0])
            self.assertIn("closure_status", polygon_rows[0])

    def test_output_adapter_non_fabric_can_disable_optional_cogo_enrichment(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_root = temp_path / "output"
            output_summary_path = output_root / "output_summary.json"

            manifest_path.write_text(json.dumps({"transaction_id": "100000206", "payload": {}}), encoding="utf-8")
            approved_path.write_text(json.dumps({"transaction_number": "100000206", "review_hash": "approved-hash"}), encoding="utf-8")
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000206",
                        "review_hash": "approved-hash",
                        "rows": [
                            {"row_id": "1", "point_identifier": "P1", "easting": "1000.0", "northing": "2000.0"},
                            {"row_id": "2", "point_identifier": "P2", "easting": "1010.0", "northing": "2000.0", "bearing": "N90°00'00\"E", "length": "10.0"},
                            {"row_id": "3", "point_identifier": "P3", "easting": "1010.0", "northing": "2010.0", "bearing": "N00°00'00\"E", "length": "10.0"},
                        ],
                    }
                ),
                encoding="utf-8",
            )

            previous = os.environ.get("SIDWELL_OUTPUT_ADAPTER_TEST_MODE")
            os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = "1"
            try:
                exit_code = output_adapter.main(
                    [
                        "--manifest",
                        str(manifest_path),
                        "--approved-review",
                        str(approved_path),
                        "--review-data",
                        str(review_path),
                        "--add-cogo-attributes",
                        "false",
                        "--add-cogo-labels",
                        "false",
                        "--output-root",
                        str(output_root),
                        "--output-summary",
                        str(output_summary_path),
                    ]
                )
            finally:
                if previous is None:
                    os.environ.pop("SIDWELL_OUTPUT_ADAPTER_TEST_MODE", None)
                else:
                    os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = previous

            self.assertEqual(0, exit_code)
            summary = json.loads(output_summary_path.read_text(encoding="utf-8"))
            self.assertFalse(summary["payload"]["add_cogo_attributes"])
            self.assertFalse(summary["payload"]["add_cogo_labels"])
            self.assertEqual("non_fabric", summary["payload"]["map_load_mode"])
            self.assertFalse(summary["payload"]["bearing_txt_populated"])
            self.assertEqual(0, summary["payload"]["bearing_txt_populated_count"])
            self.assertFalse(summary["payload"]["distance_txt_populated"])
            self.assertEqual(0, summary["payload"]["distance_txt_populated_count"])
            self.assertEqual(0, summary["payload"]["computed_cogo_fallback_line_count"])
            self.assertFalse(summary["payload"]["root_line_bearing_txt_exists"])
            self.assertFalse(summary["payload"]["root_line_distance_txt_exists"])
            self.assertFalse(summary["payload"]["root_line_length_txt_exists"])
            self.assertFalse(summary["payload"]["root_line_distance_m_exists"])

            line_rows = json.loads(Path(summary["payload"]["line_feature_class_path"]).read_text(encoding="utf-8"))
            self.assertNotIn("bearing_txt", line_rows[0])
            self.assertNotIn("distance_txt", line_rows[0])
            self.assertNotIn("distance_m", line_rows[0])
            self.assertNotIn("azimuth_deg", line_rows[0])
            self.assertNotIn("is_computed_cogo", line_rows[0])

    def test_output_adapter_non_fabric_computes_safe_cogo_fallbacks_when_enabled(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_root = temp_path / "output"
            output_summary_path = output_root / "output_summary.json"

            manifest_path.write_text(json.dumps({"transaction_id": "100000206", "payload": {}}), encoding="utf-8")
            approved_path.write_text(json.dumps({"transaction_number": "100000206", "review_hash": "approved-hash"}), encoding="utf-8")
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000206",
                        "review_hash": "approved-hash",
                        "rows": [
                            {"row_id": "1", "point_identifier": "P1", "easting": "1000.0", "northing": "2000.0"},
                            {"row_id": "2", "point_identifier": "P2", "easting": "1010.0", "northing": "2000.0"},
                            {"row_id": "3", "point_identifier": "P3", "easting": "1010.0", "northing": "2010.0"},
                        ],
                    }
                ),
                encoding="utf-8",
            )

            previous = os.environ.get("SIDWELL_OUTPUT_ADAPTER_TEST_MODE")
            os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = "1"
            try:
                exit_code = output_adapter.main(
                    [
                        "--manifest",
                        str(manifest_path),
                        "--approved-review",
                        str(approved_path),
                        "--review-data",
                        str(review_path),
                        "--add-cogo-attributes",
                        "true",
                        "--cogo-source-mode",
                        "source_then_computed",
                        "--output-root",
                        str(output_root),
                        "--output-summary",
                        str(output_summary_path),
                    ]
                )
            finally:
                if previous is None:
                    os.environ.pop("SIDWELL_OUTPUT_ADAPTER_TEST_MODE", None)
                else:
                    os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = previous

            self.assertEqual(0, exit_code)
            summary = json.loads(output_summary_path.read_text(encoding="utf-8"))
            line_rows = json.loads(Path(summary["payload"]["line_feature_class_path"]).read_text(encoding="utf-8"))
            self.assertEqual(10.0, line_rows[0]["distance_m"])
            self.assertEqual("10", line_rows[0]["distance_txt"])
            self.assertEqual("N90°00'00\"E", line_rows[0]["bearing_txt"])
            self.assertAlmostEqual(90.0, line_rows[0]["azimuth_deg"], places=6)
            self.assertTrue(line_rows[0]["is_computed_cogo"])
            self.assertTrue(summary["payload"]["bearing_txt_populated"])
            self.assertEqual(2, summary["payload"]["bearing_txt_populated_count"])
            self.assertTrue(summary["payload"]["distance_txt_populated"])
            self.assertEqual(2, summary["payload"]["distance_txt_populated_count"])
            self.assertEqual(2, summary["payload"]["computed_cogo_fallback_line_count"])

    def test_output_adapter_rejects_stale_approval_hash(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_root = temp_path / "output"
            output_summary_path = output_root / "output_summary.json"

            manifest_path.write_text(json.dumps({"transaction_id": "100000206", "payload": {}}), encoding="utf-8")
            approved_path.write_text(json.dumps({"review_hash": "approved-hash"}), encoding="utf-8")
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000206",
                        "review_hash": "different-hash",
                        "rows": [{"row_id": "1", "point_identifier": "P1", "easting": "1", "northing": "2"}],
                    }
                ),
                encoding="utf-8",
            )

            previous = os.environ.get("SIDWELL_OUTPUT_ADAPTER_TEST_MODE")
            os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = "1"
            try:
                with self.assertRaises(RuntimeError):
                    output_adapter.main(
                        [
                            "--manifest",
                            str(manifest_path),
                            "--approved-review",
                            str(approved_path),
                            "--review-data",
                            str(review_path),
                            "--output-root",
                            str(output_root),
                            "--output-summary",
                            str(output_summary_path),
                        ]
                    )
            finally:
                if previous is None:
                    os.environ.pop("SIDWELL_OUTPUT_ADAPTER_TEST_MODE", None)
                else:
                    os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = previous

    def test_output_adapter_skips_invalid_polygon_and_still_succeeds(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_root = temp_path / "output"
            output_summary_path = output_root / "output_summary.json"

            manifest_path.write_text(
                json.dumps({"transaction_id": "100000206", "payload": {"script_plan": {"source_manifest_hash": "hash-123"}}}),
                encoding="utf-8",
            )
            approved_path.write_text(
                json.dumps({"transaction_number": "100000206", "review_hash": "approved-hash", "approved_by": "tester"}),
                encoding="utf-8",
            )
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000206",
                        "review_hash": "approved-hash",
                        "rows": [
                            {"row_id": "1", "point_identifier": "P1", "easting": "1000.0", "northing": "2000.0"},
                            {"row_id": "2", "point_identifier": "P2", "easting": "1010.0", "northing": "2000.0"},
                            {"row_id": "3", "point_identifier": "P3", "easting": "1020.0", "northing": "2000.0"},
                        ],
                    }
                ),
                encoding="utf-8",
            )

            previous = os.environ.get("SIDWELL_OUTPUT_ADAPTER_TEST_MODE")
            os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = "1"
            try:
                exit_code = output_adapter.main(
                    [
                        "--manifest",
                        str(manifest_path),
                        "--approved-review",
                        str(approved_path),
                        "--review-data",
                        str(review_path),
                        "--add-cogo-attributes",
                        "true",
                        "--output-root",
                        str(output_root),
                        "--output-summary",
                        str(output_summary_path),
                    ]
                )
            finally:
                if previous is None:
                    os.environ.pop("SIDWELL_OUTPUT_ADAPTER_TEST_MODE", None)
                else:
                    os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = previous

            self.assertEqual(0, exit_code)
            summary = json.loads(output_summary_path.read_text(encoding="utf-8"))
            self.assertEqual(0, summary["payload"]["polygon_count"])
            self.assertEqual([], summary["warnings"])

    def test_output_adapter_parcel_fabric_mode_writes_true_review_paths(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_root = temp_path / "output"
            output_summary_path = output_root / "output_summary.json"

            manifest_path.write_text(
                json.dumps(
                    {
                        "transaction_id": "100000206",
                        "payload": {"script_plan": {"source_manifest_hash": "hash-123"}},
                    }
                ),
                encoding="utf-8",
            )
            approved_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000206",
                        "review_hash": "approved-hash",
                        "approved_by": "tester",
                    }
                ),
                encoding="utf-8",
            )
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000206",
                        "review_hash": "approved-hash",
                        "rows": [
                            {"row_id": "1", "point_identifier": "P1", "easting": "1000.0", "northing": "2000.0"},
                            {"row_id": "2", "point_identifier": "P2", "easting": "1010.0", "northing": "2000.0"},
                            {"row_id": "3", "point_identifier": "P3", "easting": "1010.0", "northing": "2010.0"},
                        ],
                    }
                ),
                encoding="utf-8",
            )

            previous = os.environ.get("SIDWELL_OUTPUT_ADAPTER_TEST_MODE")
            os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = "1"
            try:
                exit_code = output_adapter.main(
                    [
                        "--manifest",
                        str(manifest_path),
                        "--approved-review",
                        str(approved_path),
                        "--review-data",
                        str(review_path),
                        "--review-workspace-mode",
                        "parcel_fabric",
                        "--add-cogo-attributes",
                        "true",
                        "--output-root",
                        str(output_root),
                        "--output-summary",
                        str(output_summary_path),
                    ]
                )
            finally:
                if previous is None:
                    os.environ.pop("SIDWELL_OUTPUT_ADAPTER_TEST_MODE", None)
                else:
                    os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = previous

            self.assertEqual(0, exit_code)
            summary = json.loads(output_summary_path.read_text(encoding="utf-8"))
            self.assertEqual("parcel_fabric", summary["payload"]["review_workspace_mode"])
            self.assertEqual("fabric", summary["payload"]["map_load_mode"])
            self.assertEqual("true", summary["payload"]["parcel_fabric_mode"])
            self.assertTrue(summary["payload"]["review_dataset_path"].endswith("parcel_fabric_dataset"))
            self.assertTrue(summary["payload"]["review_layer_path"].endswith("parcel_fabric_dataset/local_parcel_fabric") or summary["payload"]["review_layer_path"].endswith("parcel_fabric_dataset\\local_parcel_fabric"))
            self.assertTrue(summary["payload"]["review_point_feature_class_path"].endswith("compute_review/points.json") or summary["payload"]["review_point_feature_class_path"].endswith("compute_review\\points.json"))
            self.assertEqual(summary["payload"]["review_layer_path"], summary["payload"]["map_layer_paths"][0])
            self.assertEqual("compute_review", summary["payload"]["parcel_type"])
            self.assertEqual("sidwell-record-100000206", summary["payload"]["parcel_record_name"])
            self.assertEqual(1, summary["payload"]["built_parcel_count"])
            self.assertEqual(2, summary["payload"]["built_line_count"])
            self.assertEqual(3, summary["payload"]["built_point_count"])
            self.assertTrue(summary["payload"]["root_line_bearing_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_distance_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_length_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_distance_m_exists"])
            self.assertEqual(2, summary["payload"]["bearing_txt_populated_count"])
            self.assertEqual(2, summary["payload"]["distance_txt_populated_count"])
            self.assertEqual([], summary["warnings"])

    def test_output_adapter_respects_parcel_group_boundaries_for_segments(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_root = temp_path / "output"
            output_summary_path = output_root / "output_summary.json"

            manifest_path.write_text(
                json.dumps({"transaction_id": "100000206", "payload": {"script_plan": {"source_manifest_hash": "hash-123"}}}),
                encoding="utf-8",
            )
            approved_path.write_text(
                json.dumps({"transaction_number": "100000206", "review_hash": "approved-hash", "approved_by": "tester"}),
                encoding="utf-8",
            )
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000206",
                        "review_hash": "approved-hash",
                        "rows": [
                            {"row_id": "1", "parcel_group_id": "parcel-a", "sequence_in_group": 1, "point_identifier": "A1", "easting": "1000.0", "northing": "2000.0"},
                            {"row_id": "2", "parcel_group_id": "parcel-a", "sequence_in_group": 2, "point_identifier": "A2", "easting": "1010.0", "northing": "2000.0"},
                            {"row_id": "3", "parcel_group_id": "parcel-b", "sequence_in_group": 1, "point_identifier": "B1", "easting": "2000.0", "northing": "3000.0"},
                            {"row_id": "4", "parcel_group_id": "parcel-b", "sequence_in_group": 2, "point_identifier": "B2", "easting": "2010.0", "northing": "3000.0"},
                        ],
                    }
                ),
                encoding="utf-8",
            )

            previous = os.environ.get("SIDWELL_OUTPUT_ADAPTER_TEST_MODE")
            os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = "1"
            try:
                exit_code = output_adapter.main(
                    [
                        "--manifest",
                        str(manifest_path),
                        "--approved-review",
                        str(approved_path),
                        "--review-data",
                        str(review_path),
                        "--add-cogo-attributes",
                        "true",
                        "--output-root",
                        str(output_root),
                        "--output-summary",
                        str(output_summary_path),
                    ]
                )
            finally:
                if previous is None:
                    os.environ.pop("SIDWELL_OUTPUT_ADAPTER_TEST_MODE", None)
                else:
                    os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = previous

            self.assertEqual(0, exit_code)
            summary = json.loads(output_summary_path.read_text(encoding="utf-8"))
            self.assertEqual(4, summary["payload"]["point_count"])
            self.assertEqual(2, summary["payload"]["line_count"])
            self.assertEqual(0, summary["payload"]["polygon_count"])

            geojson = json.loads((output_root / "extracted_geometry.geojson").read_text(encoding="utf-8"))
            line_features = [feature for feature in geojson["features"] if feature["geometry"]["type"] == "LineString"]
            self.assertEqual(2, len(line_features))
            self.assertEqual({"parcel-a", "parcel-b"}, {feature["properties"]["parcel_group_id"] for feature in line_features})

    def test_output_adapter_computes_canonical_non_fabric_schema_values(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_root = temp_path / "output"
            output_summary_path = output_root / "output_summary.json"

            manifest_path.write_text(
                json.dumps({"transaction_id": "100000300", "transaction_type": "Assign Computation Task", "payload": {"script_plan": {"source_manifest_hash": "hash-123"}}}),
                encoding="utf-8",
            )
            approved_path.write_text(
                json.dumps({"transaction_number": "100000300", "review_hash": "approved-hash", "approved_by": "tester"}),
                encoding="utf-8",
            )
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000300",
                        "review_hash": "approved-hash",
                        "rows": [
                            {"row_id": "1", "parcel_group_id": "parcel-a", "point_identifier": "P1", "easting": "1000.0", "northing": "2000.0", "status": "Matched"},
                            {"row_id": "2", "parcel_group_id": "parcel-a", "point_identifier": "P2", "easting": "1010.0", "northing": "2000.0", "status": "Matched"},
                            {"row_id": "3", "parcel_group_id": "parcel-a", "point_identifier": "P3", "easting": "1010.0", "northing": "2010.0", "status": "Matched"},
                        ],
                    }
                ),
                encoding="utf-8",
            )

            previous = os.environ.get("SIDWELL_OUTPUT_ADAPTER_TEST_MODE")
            os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = "1"
            try:
                exit_code = output_adapter.main(
                    [
                        "--manifest",
                        str(manifest_path),
                        "--approved-review",
                        str(approved_path),
                        "--review-data",
                        str(review_path),
                        "--add-cogo-attributes",
                        "true",
                        "--output-root",
                        str(output_root),
                        "--output-summary",
                        str(output_summary_path),
                    ]
                )
            finally:
                if previous is None:
                    os.environ.pop("SIDWELL_OUTPUT_ADAPTER_TEST_MODE", None)
                else:
                    os.environ["SIDWELL_OUTPUT_ADAPTER_TEST_MODE"] = previous

            self.assertEqual(0, exit_code)
            summary = json.loads(output_summary_path.read_text(encoding="utf-8"))
            point_rows = json.loads(Path(summary["payload"]["point_feature_class_path"]).read_text(encoding="utf-8"))
            line_rows = json.loads(Path(summary["payload"]["line_feature_class_path"]).read_text(encoding="utf-8"))
            polygon_rows = json.loads(Path(summary["payload"]["polygon_feature_class_path"]).read_text(encoding="utf-8"))

            self.assertEqual("validated_review", point_rows[0]["source_mode"])
            self.assertEqual("Assign Computation Task", point_rows[0]["transaction_type"])
            self.assertEqual("P1", point_rows[0]["point_id"])
            self.assertEqual("P1", line_rows[0]["from_point_id"])
            self.assertEqual("P2", line_rows[0]["to_point_id"])
            self.assertEqual("line", line_rows[0]["line_type"])
            self.assertAlmostEqual(10.0, line_rows[0]["distance_m"], places=6)
            self.assertEqual("closed", polygon_rows[0]["closure_status"])
            self.assertGreater(polygon_rows[0]["perimeter_m"], 0.0)
            self.assertGreater(polygon_rows[0]["area_sq_m"], 0.0)


if __name__ == "__main__":
    unittest.main()
