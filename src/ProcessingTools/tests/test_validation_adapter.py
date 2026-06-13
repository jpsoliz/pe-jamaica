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
                            {"row_id": "1", "point_identifier": "P1", "easting": "1000.1", "northing": "2000.2"},
                            {"row_id": "2", "point_identifier": "P2", "easting": "1001.1", "northing": "2001.2"},
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


if __name__ == "__main__":
    unittest.main()
