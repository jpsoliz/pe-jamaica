import json
import os
import tempfile
import unittest
from pathlib import Path

from adapters import output_adapter


class OutputAdapterTests(unittest.TestCase):
    def test_output_adapter_parses_and_matches_dwg_layer_allowlist(self):
        allowed = output_adapter._parse_allowed_cad_layers("Boundary|Parcel Lines; CONTROL,")

        self.assertEqual({"boundary", "parcel lines", "control"}, allowed)
        self.assertTrue(output_adapter._is_allowed_cad_layer("BOUNDARY", allowed))
        self.assertTrue(output_adapter._is_allowed_cad_layer(" Parcel Lines ", allowed))
        self.assertFalse(output_adapter._is_allowed_cad_layer("Road Centerline", allowed))
        self.assertTrue(output_adapter._is_allowed_cad_layer("Road Centerline", set()))

    def test_output_adapter_copies_cogo_fields_to_fabric_lines(self):
        class FakeField:
            def __init__(self, name):
                self.name = name

        class FakeCursor:
            def __init__(self, rows, fields, update=False):
                self.rows = rows
                self.fields = fields
                self.update = update
                self.index = -1

            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc, tb):
                return False

            def __iter__(self):
                return self

            def __next__(self):
                self.index += 1
                if self.index >= len(self.rows):
                    raise StopIteration
                return [self.rows[self.index].get(field) for field in self.fields]

            def updateRow(self, values):
                for field, value in zip(self.fields, values):
                    self.rows[self.index][field] = value

        class FakeManagement:
            def __init__(self, arcpy):
                self.arcpy = arcpy

            def AddField(self, dataset, field_name, field_type, field_length=None):
                self.arcpy.datasets[dataset]["fields"].append(field_name)
                for row in self.arcpy.datasets[dataset]["rows"]:
                    row[field_name] = None

        class FakeDa:
            def __init__(self, arcpy):
                self.arcpy = arcpy

            def SearchCursor(self, dataset, fields):
                return FakeCursor(self.arcpy.datasets[dataset]["rows"], fields)

            def UpdateCursor(self, dataset, fields):
                return FakeCursor(self.arcpy.datasets[dataset]["rows"], fields, update=True)

        class FakeArcpy:
            def __init__(self):
                self.datasets = {
                    "source": {
                        "fields": ["bearing_txt", "distance_txt", "length_txt", "distance_m"],
                        "rows": [
                            {"bearing_txt": "N1", "distance_txt": "10.00", "length_txt": "10.00", "distance_m": 10.0},
                            {"bearing_txt": "N2", "distance_txt": "20.00", "length_txt": "20.00", "distance_m": 20.0},
                        ],
                    },
                    "target": {
                        "fields": [],
                        "rows": [{}, {}],
                    },
                }
                self.management = FakeManagement(self)
                self.da = FakeDa(self)

            def ListFields(self, dataset):
                return [FakeField(name) for name in self.datasets[dataset]["fields"]]

        fake = FakeArcpy()
        warnings = []

        output_adapter._copy_cogo_fields_to_fabric_lines(fake, "source", "target", warnings)

        self.assertEqual([], warnings)
        self.assertEqual("N1", fake.datasets["target"]["rows"][0]["bearing_txt"])
        self.assertEqual("20.00", fake.datasets["target"]["rows"][1]["length_txt"])
        self.assertEqual(20.0, fake.datasets["target"]["rows"][1]["distance_m"])

    def test_output_adapter_prefers_reviewed_pxa_segments_for_lines(self):
        review_data = {
            "segments": [
                {
                    "segment_id": "seg-1",
                    "review_sequence": 1,
                    "review_from_point": "18",
                    "review_to_point": "15",
                    "review_bearing_txt": "S84°56'E",
                    "review_distance_txt": "33.470",
                    "review_include_in_boundary": True,
                    "review_status": "accepted",
                }
            ]
        }
        point_groups = [
            {
                "group_id": "parcel-001",
                "parcel_id": "parcel-001",
                "points": [
                    {
                        "point_identifier": "15",
                        "easting": 712897.345,
                        "northing": 670582.156,
                        "parcel_id": "parcel-001",
                        "parcel_group_id": "parcel-001",
                    },
                    {
                        "point_identifier": "18",
                        "easting": 712864.006,
                        "northing": 670585.112,
                        "parcel_id": "parcel-001",
                        "parcel_group_id": "parcel-001",
                    },
                ],
            }
        ]

        segments = output_adapter._reviewed_boundary_segments(review_data, point_groups)

        self.assertEqual(1, len(segments))
        self.assertEqual("18", segments[0]["from_point_id"])
        self.assertEqual("15", segments[0]["to_point_id"])
        self.assertEqual("S84°56'E", segments[0]["bearing_txt"])
        self.assertEqual("33.470", segments[0]["distance_txt"])

    def test_output_adapter_uses_reviewed_pxa_segments_for_polygon_ring(self):
        review_data = {
            "segments": [
                {"segment_id": "seg-1", "review_sequence": 1, "review_from_point": "18", "review_to_point": "15", "review_bearing_txt": "S84°56'E", "review_distance_txt": "33.470"},
                {"segment_id": "seg-2", "review_sequence": 2, "review_from_point": "15", "review_to_point": "3", "review_bearing_txt": "S01°27'E", "review_distance_txt": "18.343"},
                {"segment_id": "seg-3", "review_sequence": 3, "review_from_point": "3", "review_to_point": "16", "review_bearing_txt": "S01°39'W", "review_distance_txt": "5.230"},
                {"segment_id": "seg-4", "review_sequence": 4, "review_from_point": "16", "review_to_point": "17", "review_bearing_txt": "N82°59'W", "review_distance_txt": "41.415"},
                {"segment_id": "seg-5", "review_sequence": 5, "review_from_point": "17", "review_to_point": "18", "review_bearing_txt": "N19°09'E", "review_distance_txt": "22.715"},
            ]
        }
        point_groups = [
            {
                "group_id": "parcel-001",
                "parcel_id": "parcel-001",
                "points": [
                    {"point_identifier": "15", "easting": 712897.345, "northing": 670582.156, "parcel_id": "parcel-001", "parcel_group_id": "parcel-001"},
                    {"point_identifier": "17", "easting": 712856.553, "northing": 670563.653, "parcel_id": "parcel-001", "parcel_group_id": "parcel-001"},
                    {"point_identifier": "16", "easting": 712897.659, "northing": 670558.591, "parcel_id": "parcel-001", "parcel_group_id": "parcel-001"},
                    {"point_identifier": "18", "easting": 712864.006, "northing": 670585.112, "parcel_id": "parcel-001", "parcel_group_id": "parcel-001"},
                    {"point_identifier": "3", "easting": 712897.809, "northing": 670563.819, "parcel_id": "parcel-001", "parcel_group_id": "parcel-001"},
                ],
            }
        ]

        segments = output_adapter._reviewed_boundary_segments(review_data, point_groups)
        polygons = output_adapter._polygon_rings_from_segments(segments)

        self.assertEqual(5, len(segments))
        self.assertEqual(1, len(polygons))
        self.assertEqual("reviewed_boundary_segments", polygons[0]["geometry_source"])
        self.assertAlmostEqual(854.8, polygons[0]["area_sq_m"], delta=1.0)
        self.assertEqual((712864.006, 670585.112), polygons[0]["coordinates"][0])

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
            self.assertEqual(3, summary["payload"]["line_count"])
            self.assertEqual(1, summary["payload"]["polygon_count"])
            self.assertTrue(summary["payload"]["add_cogo_attributes"])
            self.assertTrue(summary["payload"]["add_cogo_labels"])
            self.assertEqual("source_then_computed", summary["payload"]["cogo_source_mode"])
            self.assertEqual("non_fabric", summary["payload"]["map_load_mode"])
            self.assertTrue(summary["payload"]["bearing_txt_populated"])
            self.assertEqual(3, summary["payload"]["bearing_txt_populated_count"])
            self.assertTrue(summary["payload"]["distance_txt_populated"])
            self.assertEqual(3, summary["payload"]["distance_txt_populated_count"])
            self.assertEqual(3, summary["payload"]["computed_cogo_fallback_line_count"])
            self.assertIn("root_line_feature_class_diagnostic", summary["payload"])
            self.assertTrue(summary["payload"]["root_line_bearing_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_distance_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_length_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_distance_m_exists"])
            self.assertEqual(3, summary["payload"]["root_line_feature_class_diagnostic"]["row_count"])
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

    def test_output_adapter_dedupes_shared_edges_and_points_for_review_outputs(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_root = temp_path / "output"
            output_summary_path = output_root / "output_summary.json"

            manifest_path.write_text(json.dumps({"transaction_id": "100000416", "payload": {}}), encoding="utf-8")
            approved_path.write_text(json.dumps({"transaction_number": "100000416", "review_hash": "approved-hash"}), encoding="utf-8")
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000416",
                        "review_hash": "approved-hash",
                        "rows": [
                            {"row_id": "1", "parcel_group_id": "parcel-a", "point_identifier": "P1", "easting": "0", "northing": "0"},
                            {"row_id": "2", "parcel_group_id": "parcel-a", "point_identifier": "P2", "easting": "10", "northing": "0", "length": "10"},
                            {"row_id": "3", "parcel_group_id": "parcel-a", "point_identifier": "P3", "easting": "10", "northing": "10", "length": "10"},
                            {"row_id": "4", "parcel_group_id": "parcel-a", "point_identifier": "P4", "easting": "0", "northing": "10", "length": "10"},
                            {"row_id": "5", "parcel_group_id": "parcel-b", "point_identifier": "P2", "easting": "10", "northing": "0"},
                            {"row_id": "6", "parcel_group_id": "parcel-b", "point_identifier": "P5", "easting": "20", "northing": "0", "length": "10"},
                            {"row_id": "7", "parcel_group_id": "parcel-b", "point_identifier": "P6", "easting": "20", "northing": "10", "length": "10"},
                            {"row_id": "8", "parcel_group_id": "parcel-b", "point_identifier": "P3", "easting": "10", "northing": "10", "length": "10"},
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
            self.assertEqual(6, summary["payload"]["point_count"])
            self.assertEqual(7, summary["payload"]["line_count"])
            self.assertEqual(2, summary["payload"]["polygon_count"])

            point_rows = json.loads(Path(summary["payload"]["point_feature_class_path"]).read_text(encoding="utf-8"))
            line_rows = json.loads(Path(summary["payload"]["line_feature_class_path"]).read_text(encoding="utf-8"))
            self.assertEqual(6, len(point_rows))
            self.assertEqual(7, len(line_rows))

            point_keys = {(row["point_identifier"], row["easting"], row["northing"]) for row in point_rows}
            self.assertEqual(6, len(point_keys))
            edge_keys = {
                tuple(sorted(((row["start"][0], row["start"][1]), (row["end"][0], row["end"][1]))))
                for row in line_rows
            }
            self.assertEqual(7, len(edge_keys))

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
            self.assertEqual(3, summary["payload"]["bearing_txt_populated_count"])
            self.assertTrue(summary["payload"]["distance_txt_populated"])
            self.assertEqual(3, summary["payload"]["distance_txt_populated_count"])
            self.assertEqual(3, summary["payload"]["computed_cogo_fallback_line_count"])

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
            self.assertEqual(3, summary["payload"]["built_line_count"])
            self.assertEqual(3, summary["payload"]["built_point_count"])
            self.assertTrue(summary["payload"]["root_line_bearing_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_distance_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_length_txt_exists"])
            self.assertTrue(summary["payload"]["root_line_distance_m_exists"])
            self.assertEqual(3, summary["payload"]["bearing_txt_populated_count"])
            self.assertEqual(3, summary["payload"]["distance_txt_populated_count"])
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

    def test_output_adapter_adds_closing_segment_for_closed_parcel_review(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            manifest_path = temp_path / "manifest.json"
            approved_path = temp_path / "approved_review.json"
            review_path = temp_path / "extraction_review_data.json"
            output_root = temp_path / "output"
            output_summary_path = output_root / "output_summary.json"

            manifest_path.write_text(
                json.dumps({"transaction_id": "100000401", "payload": {"script_plan": {"source_manifest_hash": "hash-123"}}}),
                encoding="utf-8",
            )
            approved_path.write_text(
                json.dumps({"transaction_number": "100000401", "review_hash": "approved-hash", "approved_by": "tester"}),
                encoding="utf-8",
            )
            review_path.write_text(
                json.dumps(
                    {
                        "transaction_number": "100000401",
                        "review_hash": "approved-hash",
                        "rows": [
                            {"row_id": "1", "parcel_group_id": "parcel-a", "sequence_in_group": 1, "point_identifier": "P1", "easting": "1000.0", "northing": "2000.0"},
                            {"row_id": "2", "parcel_group_id": "parcel-a", "sequence_in_group": 2, "point_identifier": "P2", "easting": "1010.0", "northing": "2000.0"},
                            {"row_id": "3", "parcel_group_id": "parcel-a", "sequence_in_group": 3, "point_identifier": "P3", "easting": "1010.0", "northing": "2010.0"},
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
            self.assertEqual(3, summary["payload"]["line_count"])

            line_rows = json.loads(Path(summary["payload"]["line_feature_class_path"]).read_text(encoding="utf-8"))
            self.assertEqual(3, len(line_rows))
            self.assertEqual("P3", line_rows[-1]["from_point_id"])
            self.assertEqual("P1", line_rows[-1]["to_point_id"])
            self.assertEqual("closure", line_rows[-1]["line_type"])

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
