import unittest

from adapters import pdf_text_structured_extraction


class PdfTextStructuredExtractionTests(unittest.TestCase):
    def test_parcel_name_label_takes_precedence_for_segment_table_blocks(self):
        pages = [
            "\n".join(
                [
                    "LAMP BLOCK 0 9 SHEET 002",
                    "Parcel Name: 110900201",
                    "North: 644211.6910m East: 670076.2940m",
                    "Segment #1 : Line",
                    "Course: N4° 07' 50\"E Length: 10.107m",
                    "North: 644221.7717m East: 670077.0220m",
                    "Segment #2 : Line",
                    "Course: N10° 06' 54\"W Length: 45.552m",
                    "North: 644266.6157m East: 670069.0220m",
                ]
            )
        ]

        result = pdf_text_structured_extraction._parse_pages(pages, "100000400")

        self.assertEqual("success", result["status"])
        rows = result["rows"]
        self.assertEqual("110900201", rows[0]["parcel_name"])
        self.assertEqual("110900201_P0", rows[0]["point_identifier"])
        self.assertEqual("110900201_P1", rows[1]["point_identifier"])
        self.assertEqual("110900201_P2", rows[2]["point_identifier"])

    def test_segment_table_rows_assign_coordinates_to_from_point_and_shift_to_point_metadata(self):
        pages = [
            "\n".join(
                [
                    "110402901",
                    "From PNT Bearing Distance Northing Easting To Pnt",
                    "338 N78°08'35\"W 7.60 639209.180 680920.044 339",
                    "339 S4°21'32\"W 1.99 639210.742 680912.604 340",
                    "340 N88°01'14\"W 19.11 639208.761 680912.453 326",
                    "639209.180 680920.044 338",
                ]
            )
        ]

        result = pdf_text_structured_extraction._parse_pages(pages, "100000379")

        self.assertEqual("success", result["status"])
        rows = result["rows"]
        self.assertEqual(3, len(rows))

        self.assertEqual("338", rows[0]["point_identifier"])
        self.assertEqual("639209.180", rows[0]["northing"])
        self.assertEqual("680920.044", rows[0]["easting"])
        self.assertIsNone(rows[0]["course_from_previous"])

        self.assertEqual("339", rows[1]["point_identifier"])
        self.assertEqual("338", rows[1]["from_point"])
        self.assertEqual("339", rows[1]["to_point"])
        self.assertEqual("N78°08'35\"W", rows[1]["course_from_previous"])
        self.assertEqual("7.60", rows[1]["length_from_previous_m"])
        self.assertEqual("639210.742", rows[1]["northing"])
        self.assertEqual("680912.604", rows[1]["easting"])

        self.assertEqual("340", rows[2]["point_identifier"])
        self.assertEqual("339", rows[2]["from_point"])
        self.assertEqual("340", rows[2]["to_point"])
        self.assertEqual("S4°21'32\"W", rows[2]["course_from_previous"])
        self.assertEqual("1.99", rows[2]["length_from_previous_m"])
        self.assertEqual("639208.761", rows[2]["northing"])
        self.assertEqual("680912.453", rows[2]["easting"])

    def test_line_course_blocks_parse_bull_savannah_computation_sheet_text(self):
        pages = [
            "\n".join(
                [
                    "Parcel name: 113201101",
                    "North: 636791.9524 East : 686979.3920",
                    "Line Course: N 84 -02-29 W Length: 21.468",
                    "North: 636794.1810 East : 686958.0400",
                    "Line Cour se: N 72 -55-59 W Length: 22.005",
                    "North: 636800.6392 East : 686937.0040",
                ]
            )
        ]

        result = pdf_text_structured_extraction._parse_pages(pages, "100000492")

        self.assertEqual("success", result["status"])
        rows = result["rows"]
        self.assertEqual(3, len(rows))
        self.assertEqual("113201101_P0", rows[0]["point_identifier"])
        self.assertEqual("686979.3920", rows[0]["easting"])
        self.assertEqual("636791.9524", rows[0]["northing"])
        self.assertEqual("113201101_P1", rows[1]["point_identifier"])
        self.assertEqual("N84°02'29\"W", rows[1]["course_from_previous"])
        self.assertEqual("21.468", rows[1]["length_from_previous_m"])
        self.assertEqual("113201101_P2", rows[2]["point_identifier"])
        self.assertEqual("N72°55'59\"W", rows[2]["course_from_previous"])
        self.assertEqual("22.005", rows[2]["length_from_previous_m"])

    def test_volume_folio_aliases_are_extracted_to_review_metadata(self):
        pages = [
            "\n".join(
                [
                    "Volume/Folio 1238/856",
                    "Parcel Name: 110900201",
                    "North: 644211.6910m East: 670076.2940m",
                    "Line Course: N 04 -07-50 E Length: 10.107",
                    "North: 644221.7717m East: 670077.0220m",
                ]
            )
        ]

        result = pdf_text_structured_extraction._parse_pages(pages, "100000400")

        self.assertEqual("success", result["status"])
        volume_folio = result["survey_metadata"]["volume_folio"][0]
        self.assertEqual("1238", volume_folio["volume"])
        self.assertEqual("856", volume_folio["folio"])
        self.assertIn("Volume/Folio", volume_folio["raw_text"])

    def test_document_text_metrics_uses_page_dimensions_and_span_heights(self):
        page = pdf_text_structured_extraction._PdfTextMetricPage(
            width_pt=612,
            height_pt=792,
            spans=[
                pdf_text_structured_extraction._PdfTextMetricSpan("Lot 12", (72, 72, 110, 78)),
                pdf_text_structured_extraction._PdfTextMetricSpan("Boundary", (72, 90, 128, 99)),
            ],
        )

        result = pdf_text_structured_extraction._build_document_text_metrics_from_pages([page])

        self.assertEqual("measured", result["status"])
        self.assertAlmostEqual(215.9, result["pages"][0]["width_mm"], places=1)
        self.assertAlmostEqual(279.4, result["pages"][0]["height_mm"], places=1)
        self.assertEqual(2, len(result["pages"][0]["text_runs"]))
        self.assertGreater(result["pages"][0]["text_runs"][1]["height_mm"], result["pages"][0]["text_runs"][0]["height_mm"])

    def test_document_text_metrics_falls_back_to_a4_for_missing_page_size(self):
        page = pdf_text_structured_extraction._PdfTextMetricPage(
            width_pt=0,
            height_pt=0,
            spans=[pdf_text_structured_extraction._PdfTextMetricSpan("small", (0, 0, 20, 5))],
        )

        result = pdf_text_structured_extraction._build_document_text_metrics_from_pages([page])

        self.assertEqual("measured", result["status"])
        self.assertTrue(result["pages"][0]["page_size_fallback"])
        self.assertTrue(result["pages"][0]["dpi_unknown"])
        self.assertEqual(210.0, result["pages"][0]["width_mm"])
        self.assertEqual(297.0, result["pages"][0]["height_mm"])
    def test_embedded_compute_sheet_detection_records_pages_and_evidence(self):
        pages = [
            "SURVEY PLAN\nDiagram details only",
            "COMPUTATION SHEET\nParcel Name: 110900201\nLine Course: N 04 -07-50 E Length: 10.107\nNorth: 644221.7717 East: 670077.0220",
        ]

        detection = pdf_text_structured_extraction._detect_embedded_compute_sheet_pages(pages)

        self.assertTrue(detection["detected"])
        self.assertEqual([2], detection["page_numbers"])
        self.assertGreaterEqual(detection["confidence"], 0.75)
        self.assertIn("COMPUTATION SHEET", detection["evidence"][0])

if __name__ == "__main__":
    unittest.main()

