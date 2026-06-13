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
            self.assertTrue(summary["payload"]["result_gdb_path"].endswith(".gdb"))

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


if __name__ == "__main__":
    unittest.main()
