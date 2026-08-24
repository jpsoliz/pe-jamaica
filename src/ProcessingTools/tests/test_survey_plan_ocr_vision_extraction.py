import json
import os
import tempfile
import unittest
from pathlib import Path

from adapters import survey_plan_ocr_vision_extraction


class SurveyPlanOcrVisionExtractionTests(unittest.TestCase):
    def test_prompt_warns_not_to_invent_sequential_reference_labels(self):
        prompt = survey_plan_ocr_vision_extraction._prompt("single_parcel_survey_plan_vision_v1")

        self.assertIn("Do not invent sequential labels from printed reference labels", prompt)
        self.assertIn("if the plan has reference points A and B but an unlabeled boundary vertex follows A", prompt)
        self.assertIn("use a temporary generated label in the opposite style", prompt)
        self.assertIn("region-first MEMORANDUM table extraction", prompt)
        self.assertIn("semantic_state", prompt)
        self.assertIn("grounds_of_objection", prompt)
        self.assertIn("document_type", prompt)
        self.assertIn("scale_bar", prompt)

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
                            "volume_folio": ["Vol./Fol. 1238/856"],
                        },
                        "parties": ["Occ. Clayon Smith"],
                        "adjacent_owners": [{"owner": "Glen Alford Battiste", "role": "Occ."}],
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
            self.assertEqual("Occupant", payload["parties"][0]["role"])
            self.assertEqual("Glen Alford Battiste", payload["adjacent_owners"][0]["name"])
            self.assertEqual("Occupant", payload["adjacent_owners"][0]["role"])
            self.assertEqual("1238", payload["survey_metadata"]["volume_folio"][0]["volume"])
            self.assertEqual("856", payload["survey_metadata"]["volume_folio"][0]["folio"])

    def test_memorandum_response_writes_document_section_and_memorandum_fields(self):
        raw = {
            "document_text": "MEMORANDUM\nSurveyed for Roxine Campbell",
            "survey_metadata": {
                "parish": "Clarendon",
                "instrument": "TOPCON GM-52",
                "instrument_check_date": "2024-09-04",
                "instrument_check_result": "Checked and found in order",
                "gps_instrument_number": "GPS-7",
                "gps_serial_number": "SN-12345",
            },
            "north_arrow": {"detected": True, "approximate_page_location": "upper right", "confidence": 0.9},
            "scale_bar": {"detected": False, "approximate_page_location": "not visible", "confidence": 0.2},
            "surveyed_for_names": [{"name": "Roxine Campbell", "source_zone": "memorandum"}],
            "surveyed_property_names": [{"value": "Lot 12 Bellevue", "source_zone": "memorandum"}],
            "property_name_near_parcel_diagram": {
                "present": True,
                "value": "Lot 12 Bellevue",
                "source_zone": "parcel_diagram",
                "confidence": 0.72,
            },
            "notice_served_on": ["Austin Singh", "Maria Brown"],
            "appeared_parties": [
                {"name": "Austin Singh", "appearance_mode": "personal"},
                {"name": "Maria Brown", "appearance_mode": "representative", "representative": "Kevon Jarrett"},
            ],
        }

        payload = survey_plan_ocr_vision_extraction._normalize_extraction(raw, "100000562", "DOC_PLAN_492321.pdf")

        memorandum = payload["document_sections"]["memorandum"]
        self.assertTrue(memorandum["detected"])
        self.assertEqual("MEMORANDUM", memorandum["matched_text"])
        self.assertEqual("Roxine Campbell", payload["surveyed_for_names"][0]["name"])
        self.assertEqual("Lot 12 Bellevue", payload["surveyed_property_names"][0]["value"])
        self.assertTrue(payload["property_name_near_parcel_diagram"]["present"])
        self.assertEqual("TOPCON GM-52", payload["survey_metadata"]["instrument"]["value"])
        self.assertEqual("2024-09-04", payload["survey_metadata"]["instrument_check_date"]["value"])
        self.assertEqual("Checked and found in order", payload["survey_metadata"]["instrument_check_result"]["value"])
        self.assertEqual("GPS-7", payload["survey_metadata"]["gps_instrument_number"]["value"])
        self.assertEqual("SN-12345", payload["survey_metadata"]["gps_serial_number"]["value"])
        self.assertFalse(payload["scale_bar"]["present"])
        self.assertEqual("Austin Singh", payload["notice_served_on"][0]["name"])
        self.assertEqual("representative", payload["appeared_parties"][1]["appearance_mode"])

    def test_memorandum_table_layout_parses_combined_instrument_and_no_appearance(self):
        raw = {
            "document_text": (
                "MEMORANDUM PARISH OF ST. ANN The name of the party at whose instance the survey was made "
                "The names of those who appeared either personally or by their representatives No one appeared "
                "SCALE One Millimetre = 0.5 Metre or 1:500"
            ),
            "survey_metadata": {
                "instrument": "FOIF RTS 102R8 S/N: A13183",
                "instrument_check": "04/10/2024 - Satisfactory",
            },
            "appeared_parties": ["No one appeared"],
        }

        payload = survey_plan_ocr_vision_extraction._normalize_extraction(raw, "100000562", "DOC_PLAN_492321.pdf")

        memorandum = payload["document_sections"]["memorandum"]
        self.assertTrue(memorandum["detected"])
        self.assertEqual("table", memorandum["section_type"])
        self.assertTrue(payload["scale_bar"]["present"])
        self.assertEqual("04/10/2024", payload["survey_metadata"]["instrument_check_date"]["value"])
        self.assertEqual("Satisfactory", payload["survey_metadata"]["instrument_check_result"]["value"])
        self.assertEqual("No one appeared", payload["appeared_parties"][0]["name"])
        self.assertEqual("none", payload["appeared_parties"][0]["appearance_mode"])
        self.assertEqual("NO_ONE_APPEARED", payload["appeared_parties"][0]["semantic_state"])

    def test_memorandum_semantic_states_and_deterministic_field_parsing(self):
        raw = {
            "document_text": "MEMORANDUM",
            "survey_metadata": {
                "parish": "St. Ann",
                "area": "3203.710 Sq. Metres",
                "survey_date": "June 5, 2024",
                "grounds_of_objection": "None",
                "surveyor_decision_grounds": "Instructions and marks on ground",
                "instrument": "FOIF RTS 102R8 S/N: A13183",
                "instrument_check": "04/10/2024 - Satisfactory",
                "surveyed_by": {
                    "name": "Craig A. Francis",
                    "title": "Commissioned Land Surveyor",
                    "organization": "Precision Surveying Services Ltd.",
                },
            },
            "surveyed_for_names": [{"name": "Mario Smith"}],
            "surveyed_property_names": [{"value": "Part of SYMS RUN"}],
            "notice_served_on": [{"name": "The C.E.O of St. Ann Municipal Corporation"}],
            "appeared_parties": ["No one appeared"],
        }

        payload = survey_plan_ocr_vision_extraction._normalize_extraction(raw, "100000562", "DOC_PLAN_490449_s.pdf")
        fixture_dir = Path(__file__).parent / "fixtures" / "jamaica" / "plan_examination"
        expected = json.loads((fixture_dir / "DOC_PLAN_490449_s.expected.json").read_text())

        metadata = payload["survey_metadata"]
        self.assertTrue((fixture_dir / "DOC_PLAN_490449_s.pdf").exists())
        self.assertEqual(expected["source"]["file_name"], payload["primary_source_file"])
        self.assertEqual(expected["source"]["document_type"], payload["document_sections"]["memorandum"]["matched_text"])
        self.assertEqual("VALUE", metadata["parish"]["semantic_state"])
        self.assertEqual("3203.710 Sq. Metres", metadata["document_area"]["raw_value"])
        self.assertEqual(3203.71, metadata["document_area"]["numeric_value"])
        self.assertEqual("SQUARE_METRES", metadata["document_area"]["unit"])
        self.assertEqual(expected["survey_metadata"]["document_area"]["unit"], metadata["document_area"]["unit"])
        self.assertEqual("NONE", metadata["grounds_of_objection"]["semantic_state"])
        self.assertEqual("VALUE", metadata["surveyor_decision_grounds"]["semantic_state"])
        self.assertEqual("04/10/2024", metadata["instrument_check_date"]["value"])
        self.assertEqual("Satisfactory", metadata["instrument_check_result"]["value"])
        self.assertEqual("Craig A. Francis", metadata["surveyed_by"]["value"])
        self.assertEqual("Commissioned Land Surveyor", metadata["surveyed_by"]["title"])
        self.assertEqual("Precision Surveying Services Ltd.", metadata["surveyed_by"]["organization"])
        self.assertEqual("VALUE", payload["surveyed_for_names"][0]["semantic_state"])
        self.assertEqual("VALUE", payload["surveyed_property_names"][0]["semantic_state"])
        self.assertEqual("NO_ONE_APPEARED", payload["appeared_parties"][0]["semantic_state"])
        self.assertNotIn("jad2001_point_coordinates", payload)

    def test_semantic_state_distinguishes_blank_missing_na_and_illegible(self):
        raw = {
            "document_text": "MEMORANDUM",
            "survey_metadata": {
                "parish": {"value": "", "source_zone": "memorandum"},
                "document_area": {"raw_value": "about three thousand square metres", "source_zone": "memorandum"},
                "grounds_of_objection": "N/A",
                "instrument_check_result": {"raw_text": "####", "semantic_state": "ILLEGIBLE"},
                "surveyed_by": {"raw_value": "Craig A. Francis", "confidence": 0.0},
            },
            "appeared_parties": [],
        }

        payload = survey_plan_ocr_vision_extraction._normalize_extraction(raw, "100000562", "DOC_PLAN_490449_s.pdf")

        self.assertEqual("NOT_STATED", payload["survey_metadata"]["parish"]["semantic_state"])
        self.assertEqual("needs_review", payload["survey_metadata"]["document_area"]["review_status"])
        self.assertNotIn("unit", payload["survey_metadata"]["document_area"])
        self.assertEqual("N_A", payload["survey_metadata"]["grounds_of_objection"]["semantic_state"])
        self.assertEqual("ILLEGIBLE", payload["survey_metadata"]["instrument_check_result"]["semantic_state"])
        self.assertEqual("Craig A. Francis", payload["survey_metadata"]["surveyed_by"]["value"])
        self.assertEqual(0.0, payload["survey_metadata"]["surveyed_by"]["confidence"])

    def test_instrument_check_date_does_not_fall_back_to_plan_check_date(self):
        raw = {
            "document_text": "MEMORANDUM",
            "survey_metadata": {
                "instrument": "FOIF RTS 102R8 S/N: A13183",
                "plan_check_date": "June 5, 2024",
            },
        }

        payload = survey_plan_ocr_vision_extraction._normalize_extraction(raw, "100000562", "DOC_PLAN_490449_s.pdf")

        self.assertEqual("NOT_FOUND", payload["survey_metadata"]["instrument_check_date"]["semantic_state"])

    def test_visible_memorandum_and_rf_scale_text_override_false_detection_flag(self):
        raw = {
            "document_text": "metres 20 10 0 10 20 30 40 50 60 70 80 90 100 metres SCALE : 1cm To 10m R.F 1/1000 MEMORANDUM",
            "memorandum": {"detected": False},
            "scale_bar": {"detected": False},
        }

        payload = survey_plan_ocr_vision_extraction._normalize_extraction(raw, "100000562", "DOC_PLAN_492321.pdf")

        self.assertTrue(payload["document_sections"]["memorandum"]["detected"])
        self.assertEqual("MEMORANDUM", payload["document_sections"]["memorandum"]["matched_text"])
        self.assertTrue(payload["scale_bar"]["present"])
        self.assertIn("R.F 1/1000", payload["scale_bar"]["value"])
        self.assertEqual("scale_bar_text", payload["scale_bar"]["ApproximatePageLocation"])

    def test_non_memorandum_response_marks_memorandum_not_detected(self):
        payload = survey_plan_ocr_vision_extraction._normalize_extraction(
            {"document_text": "SURVEY PLAN", "survey_metadata": {"parish": "Clarendon"}},
            "100000562",
            "DOC_PLAN_492321.pdf",
        )

        self.assertFalse(payload["document_sections"]["memorandum"]["detected"])
        self.assertEqual("not_applicable", payload["document_sections"]["memorandum"]["status"])

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
