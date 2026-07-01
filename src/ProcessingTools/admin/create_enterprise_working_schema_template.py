"""Create the Enterprise working review FileGDB schema template.

The generated zip is consumed by provision_enterprise_working_layers.py. It
uses GDAL's OpenFileGDB driver so the template can be rebuilt without ArcPy.
"""

from __future__ import annotations

import argparse
import os
import shutil
import zipfile
from pathlib import Path
from typing import Iterable


SHARED_FIELDS = (
    ("transaction_number", "string", 64),
    ("transaction_id", "string", 64),
    ("task_id", "string", 64),
    ("workflow_stage", "string", 64),
    ("review_state", "string", 64),
    ("case_status", "string", 64),
    ("created_by", "string", 128),
    ("created_utc", "datetime", None),
    ("last_saved_by", "string", 128),
    ("last_saved_utc", "datetime", None),
    ("run_id", "string", 64),
    ("review_decision", "string", 32),
    ("review_decision_by", "string", 128),
    ("review_decision_utc", "datetime", None),
    ("review_comment", "string", 1024),
    ("official_comparison_status", "string", 64),
    ("official_reference_ids", "string", 512),
    ("is_active", "integer", None),
    ("edit_generation", "integer", None),
)

POINT_FIELDS = (
    ("point_id", "string", 64),
    ("parcel_group_id", "string", 64),
    ("parcel_name", "string", 128),
    ("point_role", "string", 64),
    ("status_txt", "string", 64),
    ("source_txt", "string", 1024),
    ("row_id", "string", 64),
)

LINE_FIELDS = (
    ("line_id", "string", 64),
    ("parcel_group_id", "string", 64),
    ("parcel_name", "string", 128),
    ("start_pt", "string", 64),
    ("end_pt", "string", 64),
    ("bearing_txt", "string", 64),
    ("distance_txt", "string", 64),
    ("length_txt", "string", 128),
    ("line_type", "string", 32),
    ("seg_index", "integer", None),
    ("source_txt", "string", 1024),
)

POLYGON_FIELDS = (
    ("parcel_group_id", "string", 64),
    ("parcel_name", "string", 128),
    ("parcel_type", "string", 64),
    ("validation_status", "string", 64),
    ("closure_status", "string", 64),
    ("area_sq_m", "double", None),
    ("perimeter_m", "double", None),
    ("review_note", "string", 512),
    ("source_txt", "string", 1024),
)

CASE_INDEX_FIELDS = (
    ("case_id", "string", 64),
    ("workflow_name", "string", 64),
    ("assigned_user", "string", 128),
    ("assigned_group", "string", 128),
    ("output_summary_ref", "string", 256),
    ("working_publish_ref", "string", 256),
    ("recoverability_state", "string", 64),
)

ISSUE_FIELDS = (
    ("issue_type", "string", 64),
    ("issue_text", "string", 1024),
)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    output_zip = Path(args.output_zip).resolve()
    work_dir = output_zip.parent / "_schema_template_work"
    gdb_path = work_dir / "sidwell_enterprise_working_v1.gdb"

    if work_dir.exists():
        shutil.rmtree(work_dir)
    work_dir.mkdir(parents=True, exist_ok=True)
    output_zip.parent.mkdir(parents=True, exist_ok=True)

    # ArcGIS Pro's GDAL package may otherwise use an AppData raster proxy path
    # that is unavailable to this standalone script.
    proxy_dir = work_dir / "gdal_pam_proxy"
    proxy_dir.mkdir(parents=True, exist_ok=True)
    os.environ.setdefault("GDAL_PAM_PROXY_DIR", str(proxy_dir))

    from osgeo import ogr, osr  # noqa: PLC0415

    driver = ogr.GetDriverByName("OpenFileGDB")
    if driver is None:
        raise RuntimeError("GDAL OpenFileGDB driver is required to build the schema template.")

    datasource = driver.CreateDataSource(str(gdb_path))
    if datasource is None:
        raise RuntimeError(f"Could not create File Geodatabase at {gdb_path}.")

    spatial_ref = osr.SpatialReference()
    spatial_ref.ImportFromEPSG(args.epsg)

    create_layer(datasource, "working_points", ogr.wkbPoint, spatial_ref, [*SHARED_FIELDS, *POINT_FIELDS])
    create_layer(datasource, "working_lines", ogr.wkbLineString, spatial_ref, [*SHARED_FIELDS, *LINE_FIELDS])
    create_layer(datasource, "working_polygons", ogr.wkbPolygon, spatial_ref, [*SHARED_FIELDS, *POLYGON_FIELDS])
    create_layer(datasource, "working_issues", ogr.wkbPoint, spatial_ref, [*SHARED_FIELDS, *ISSUE_FIELDS])
    create_layer(datasource, "working_case_index", ogr.wkbNone, None, [*SHARED_FIELDS, *CASE_INDEX_FIELDS])
    datasource = None

    if output_zip.exists():
        output_zip.unlink()
    with zipfile.ZipFile(output_zip, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(gdb_path.rglob("*")):
            archive.write(path, path.relative_to(work_dir))

    shutil.rmtree(work_dir)
    print(output_zip)
    return 0


def create_layer(datasource, name: str, geometry_type: int, spatial_ref, fields: Iterable[tuple[str, str, int | None]]) -> None:
    layer = datasource.CreateLayer(name, spatial_ref, geom_type=geometry_type)
    if layer is None:
        raise RuntimeError(f"Could not create layer {name}.")

    for field_name, field_type, length in fields:
        field = create_field(field_name, field_type, length)
        if layer.CreateField(field) != 0:
            raise RuntimeError(f"Could not create field {field_name} on {name}.")


def create_field(name: str, field_type: str, length: int | None):
    from osgeo import ogr  # noqa: PLC0415

    field_types = {
        "string": ogr.OFTString,
        "integer": ogr.OFTInteger,
        "double": ogr.OFTReal,
        "datetime": ogr.OFTDateTime,
    }
    definition = ogr.FieldDefn(name, field_types[field_type])
    if length is not None:
        definition.SetWidth(length)
    return definition


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create the Enterprise working review FileGDB schema template.")
    parser.add_argument(
        "--output-zip",
        default=str(Path(__file__).resolve().parent / "templates" / "sidwell_enterprise_working_v1.zip"),
    )
    parser.add_argument("--epsg", type=int, default=3448)
    return parser.parse_args(argv)


if __name__ == "__main__":
    raise SystemExit(main())
