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


if __name__ == "__main__":
    unittest.main()
