# TR 100000628 Spatial Offset Investigation

## Summary

The visible mismatch is not caused by the output layer missing JAD 2001 projection metadata. The output GeoJSON/output summary declare JAD 2001 Jamaica Grid / EPSG:3448.

The mismatch is caused by inconsistent control coordinates between the M-Geo georeferenced image and the extracted/reviewed/output geometry, plus the boundary solver intentionally choosing the unscaled bearing/distance boundary instead of forcing all printed reference coordinates.

## Evidence

- Output projection is JAD 2001 / EPSG:3448:
  - `output/output_summary.json` payload `coordinate_system = JAD 2001 Jamaica Grid`
  - `output/output_summary.json` payload `spatial_reference.wkid = 3448`
  - `output/output_summary.json` payload `output_epsg = 3448`
  - `output/extracted_geometry.geojson` CRS name `EPSG:3448`
- M-Geo overlay was georeferenced using:
  - mapPoint1: `E=643439.361`, `N=697377.140`
  - mapPoint2: `E=643510.227`, `N=697395.905`
- Extracted/reviewed/output point 1 is:
  - review: `E=643439.3610`, `N=697337.1400`
  - output: `E=643439.361`, `N=697337.140`
- Therefore M-Geo mapPoint1 northing is `+40.000 m` north of extracted/reviewed/output point 1.
- Extracted/reviewed point 44 is:
  - review: `E=643510.2270`, `N=697395.9050`
- Output point 44 is:
  - output: `E=643509.8070257261`, `N=697356.70297677`
  - status: `derived_from_reviewed_segments`
- Therefore output point 44 is about `39.204 m` away from printed/reviewed point 44 because the solver rebuilt the boundary from bearings/distances and did not force the printed point 44 coordinate.

## Code Path

- `SurveyPlanBoundarySolver.BuildUnscaledReferenceFindings` emits:
  - reviewed boundary rebuilt from bearings/distances and anchored to printed reference point 1
  - printed reference point 44 differs from reconstruction
  - reference-fit scale factor would be `1.259186`
  - reference-fit rotation would be `24.147 degrees`
  - unscaled reviewed boundary was kept
  - printed reference point 44 differs by `39.204 m`
- `output_adapter._should_rebuild_reviewed_output_from_bearings` allows rebuild when solver status is `passed` or `warning` and findings include unscaled boundary/anchored reference text.
- `validation_adapter` treats solver status `warning` with geometry source `reviewed_boundary_segments` as usable, so validation can pass even when there is a control-coordinate conflict that is visually obvious against the georeferenced image.

## Conclusion

This is a coordinate/control conflict, not a projection failure.

The first manual check should be the printed STN. 1 northing on the plan:

- If STN. 1 is really `697377.140`, the extraction/review/output value `697337.140` is wrong by 40 m and should be corrected before rerunning validation/output.
- If STN. 1 is really `697337.140`, the M-Geo overlay was georeferenced with the wrong mapPoint1 northing.
- In either case, point 44 remains a conflict with the bearing/distance reconstruction unless one of the printed reference coordinates or reviewed dimensions is corrected.

## Recommended Fix

Add a validation guard that compares M-Geo control coordinates against approved/reviewed reference coordinates before output generation. If any control point differs by more than a small tolerance, block or require explicit user confirmation.

Also promote the current solver finding "printed reference point differs from unscaled reviewed boundary by N m" to a visible validation warning/blocker when the point is a printed control coordinate, because the current pass state can produce layers that are valid by dimensions but visibly displaced on the map.
