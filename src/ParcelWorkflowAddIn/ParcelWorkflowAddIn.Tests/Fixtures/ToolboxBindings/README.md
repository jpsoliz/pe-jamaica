# Toolbox Binding Fixtures

Status: isolated test artifacts; not an activated policy, published catalog or complete case
Baseline: approved Compute/Compare classification v1 (C15)

## Files

- [Binding catalog](binding-catalog.v1.json): all 42 checklist items and both stages, with requirements, scope/predicate/check declarations, source references, evidence selectors and field mappings where established.
- [Single parcel](single-parcel.bundle.json): one synthetic parcel, owner and volume/folio pair; cross-parcel shared-boundary check N/A, duplicate-edge review remains required.
- [Multi-parcel Cad Map](multi-parcel-cad-map.bundle.json): three adjacent synthetic parcels, reciprocal boundary links, three owners/pairs, a source-area inconsistency and one unavailable mock LTD lookup.
- [Manual geometry recovery](manual-geometry-recovery.bundle.json): zero extracted geometry rows, manual capture history, four reviewed points, a Compare-local owner typo correction and human-confirmed missing north arrow.
- [Executable fixture tests](../../Workflow/ToolboxBindingFixtureTests.cs): structural/link checks, policy resolution and reuse of selected existing pure services.

All names, TRs, evidence, coordinates, documents and system responses are synthetic. Source documents are text snapshots, not real survey-plan PDFs or a tested OCR pipeline. TR-prefixed FGDB names are expectations only; no geodatabase is created. Coordinate labels are not verified georeferencing.

Cad Maps contain a variable number of parcels, not a fixed three-parcel layout. The stored three-parcel bundle is a representative scenario only. Generated tests cover 2, 3, 5, 10, 50 and 100 parcels, with all owners/pairs, reciprocal boundary links and per-parcel geometry validation. Shared-boundary applicability uses parcel_count > 1. These counts demonstrate no three-parcel cap in this local slice; they are not a production performance limit or proof of unlimited plan size.

## Checklist Mapping

18 item groups have fixture declarations:
- M03/M04: automated route and DWG applicability, as captured contracts.
- M09-M18: inventory, point/dimension/closure/orientation/boundary/geographic evidence, manual construction and owners.
- M20: volume/folio source and mock system evidence for every pair.
- M35: global human north-arrow review plus the applicable memorandum evaluator.
- M38: mock legal-query evidence; unavailable response does not erase the required obligation.
- M40: human presence/accuracy/provenance records.
- M41/M42: expected future finding-history and stage-disposition contracts only.

16 groups have fixture_bound stage declarations; M41/M42 are contract_snapshot_only. The other 24 item groups retain unresolved bindings. Required review, report-only effects, unresolved bindings and expected future stage outcomes are separate concepts. No bundle claims full coverage, approval, rejection, valid-geometry publication or remote readiness.

Catalog entries map existing code/evidence, not a finished runtime adapter. Source references identify reuse candidates. Field mappings describe normalized fixture fields; strings mentioning future normalization do not introduce existing runtime properties. Accuracy is not falsely represented as a universal legacy accuracy field.

## Test Projection

The current local policy loader requires all 42 baseline items. The test helper projects concrete declarations to its strict local-v1 format and uses explicitly named unresolved.binding sentinels for unresolved stage bindings. Sentinels retain the declared obligation; they are never executed, treated as evidence, counted as completed mappings or published. Their applicability result is not proof that an unmapped item's full document/profile conditions have been resolved.

Binding status from the sidecar remains authoritative for this fixture slice. Production activation still rejects this incomplete catalog; there is no application compiler, publisher, adapter dispatch or default policy installation here. These tests do not weaken the production boundary.

## Reused Versus Simulated

Replayed through existing pure code:
- ParcelScopedReviewValidationService, using injected in-memory tolerance/orientation/readiness catalogs rather than active settings loaders. Fixture closure thresholds are constructor defaults: 0.3m maximum/0.15m warning and 2500/4000 misclose ratios; orientation expects any with 5/2.5 degree thresholds. Readiness profiles are empty, so this is not full readiness certification.
- ParcelScopedManualPointService: incomplete manual edits remain blocked; completed capture is revalidated.
- PxaMemorandumReviewRuleService with in-memory defaults: reviewed north-arrow presence passes, absent presence yields legacy not_available, and effect remains report_only. Human missing-from-source confirmation is a separate fixture record.

Captured or mock only:
- Printed dimensions/area, spatial and georeference evidence, Python duplicate/shared-boundary diagnostics, participant/volume-folio legal responses and source-to-system comparison inputs.
- No Python/ArcPy, georeference transform, shared physical-edge identity engine, HTTP/LTD queries or actual participant matcher is run.
- Cumulative finding history and pending/forwarding outcomes are expected-only snapshots. The policy resolver does not generate those outcomes.

Raw extracted values and corrected/manual values are retained separately. Mock query inputs are not returned records; an unavailable query has an empty records array. All supplied parcel/participant/pair scopes are expanded. The fixture inventory validator independently rejects omitted scopes, dangling links, missing evidence and invented outage records; the resolver alone cannot discover omitted authoritative entities.

## Verification

Build/test results are recorded in [the fixture delivery note](../../../../../_bmad-output/planning-artifacts/toolbox/toolbox-binding-fixtures.md).

From the isolated worktree:
```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln -t:Rebuild -m:1 /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=obj-bindings/ /p:MSBuildProjectExtensionsPath=obj-bindings/
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner" "settings workspace" "workflow execution settings"
```

Only the isolated test project copies these JSON fixtures to its output. No application settings, project configuration, UI, policy engine implementation, actual case or remote task is changed by this slice.
