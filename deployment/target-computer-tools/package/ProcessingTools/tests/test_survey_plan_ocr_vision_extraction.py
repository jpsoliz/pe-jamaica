import json
import os
import tempfile
import unittest
from pathlib import Path

from adapters import survey_plan_ocr_vision_extraction


class SurveyPlanOcrVisionExtractionTests(unittest.TestCase):
    def test_mock_vision_response_writes_review_rows_segments_and_metadata(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            source_pdf = temp_path / "DOC_PLAN_492321.pdf"
            output_json = temp_path / "extraction_review_data.json"
            mock_json = temp_path / "mock_vision.json"
            source_pdf.write_bytes(b"%PDF-1.7 image only")
            mock_json.write_text(
                json.dumps(
                    {
                        "coordinate_system": "JAD 2001",
                        "coordinate_system_confidence": 0.96,
                        "north_arrow": {
                            "detected": True,
                            "approximate_page_location": "upper right",
                            "confidence": 0.9,
                        },
                        "survey_metadata": {
                            "parish": "Clarendon",
                            "document_area": "854.807 sq. metres",
                            "survey_date": "September 03, 2024",
                            "instrument": "TOPCON GM-52 #1Y013971",
                            "surveyed_by": "Michael D. Isaacs",
                        },
                        "parties": ["Clayon Smith"],
                        "adjacent_owners": ["Glen Alford Battiste"],
                        "points": [
                            {"point_id": "15", "northing": 670582.156, "easting": 712897.345},
                            {"point_id": "17", "northing": 670563.653, "easting": 712856.553},
                        ],
                        "derived_points": [
                            {
                                "point_id": "18",
                                "northing": 670585.112,
                                "easting": 712864.006,
                                "status": "derived",
                                "confidence": 0.62,
                                "review_note": "Derived from point 15 and segment 18-15.",
                            }
                        ],
                        "segments": [
                            {"from_point": "18", "to_point": "15", "bearing_txt": "S84 56 E", "distance_txt": "33.470"},
                            {"from_point": "15", "to_point": "30", "bearing_txt": "S01 27 E", "distance_txt": "18.343"},
                        ],
                    }
                ),
                encoding="utf-8",
            )
            previous = os.environ.get("SURVEY_PLAN_OCR_VISION_MOCK_JSON")
            os.environ["SURVEY_PLAN_OCR_VISION_MOCK_JSON"] = str(mock_json)
            try:
                exit_code = survey_plan_ocr_vision_extraction.main(
                    [
                        "--source-pdf",
                        str(source_pdf),
                        "--output-json",
                        str(output_json),
                        "--transaction-number",
                        "100000562",
                    ]
                )
            finally:
                if previous is None:
                    os.environ.pop("SURVEY_PLAN_OCR_VISION_MOCK_JSON", None)
                else:
                    os.environ["SURVEY_PLAN_OCR_VISION_MOCK_JSON"] = previous

            self.assertEqual(0, exit_code)
            payload = json.loads(output_json.read_text(encoding="utf-8"))
            self.assertEqual("review_required", payload["status"])
            self.assertEqual("JAD 2001", payload["coordinate_system"]["value"])
            self.assertEqual("Clarendon", payload["survey_metadata"]["parish"]["value"])
            self.assertEqual("854.807 sq. metres", payload["survey_metadata"]["document_area"]["value"])
            self.assertEqual(3, payload["row_count"])
            self.assertEqual("15", payload["rows"][0]["point_identifier"])
            self.assertEqual("15", payload["rows"][0]["point_id"])
            self.assertEqual("712897.345", payload["rows"][0]["easting"])
            self.assertEqual("18", payload["rows"][2]["point_identifier"])
            self.assertEqual("derived", payload["rows"][2]["extraction_status"])
            self.assertEqual("Derived from point 15 and segment 18-15.", payload["rows"][2]["review_note"])
            self.assertEqual(2, payload["segment_row_count"])
            self.assertEqual("33.470", payload["segments"][0]["distance_txt"])
            self.assertEqual("Clayon Smith", payload["parties"][0]["name"])
            self.assertEqual("Glen Alford Battiste", payload["adjacent_owners"][0]["name"])

    def test_provider_unavailable_writes_manual_review_artifact(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            source_pdf = temp_path / "DOC_PLAN_492321.pdf"
            output_json = temp_path / "extraction_review_data.json"
            source_pdf.write_bytes(b"%PDF-1.7 image only")
            previous_mock = os.environ.pop("SURVEY_PLAN_OCR_VISION_MOCK_JSON", None)
            previous_key = os.environ.pop("OPENAI_API_KEY", None)
            try:
                exit_code = survey_plan_ocr_vision_extraction.main(
                    [
                        "--source-pdf",
                        str(source_pdf),
                        "--output-json",
                        str(output_json),
                        "--transaction-number",
                        "100000562",
                    ]
                )
            finally:
                if previous_mock is not None:
                    os.environ["SURVEY_PLAN_OCR_VISION_MOCK_JSON"] = previous_mock
                if previous_key is not None:
                    os.environ["OPENAI_API_KEY"] = previous_key

            self.assertEqual(0, exit_code)
            payload = json.loads(output_json.read_text(encoding="utf-8"))
            self.assertEqual("manual_review_required", payload["status"])
            self.assertEqual(0, payload["row_count"])
            self.assertTrue(payload["fallback_reason"])
            self.assertIn("OCR/vision extraction did not produce usable data.", payload["review_notes"][0])


if __name__ == "__main__":
    unittest.main()
