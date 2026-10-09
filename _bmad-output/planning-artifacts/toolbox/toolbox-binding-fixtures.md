# Toolbox Binding Catalog And Fixture Delivery

Date: 2026-10-08
Status: created and focused verification passed in the isolated Toolbox worktree; no production integration
Scope: test bindings/fixtures; supports 13.4A without claiming story completion

Cad Map clarification: parcel count is variable. The stored three-parcel bundle is a representative fixture, not a product constraint. Added generated coverage for 2, 3, 5, 10, 50 and 100 parcels; full source extraction/FGDB/UI/performance capability for large plans remains future integration work.

## Deliverables

[Fixture guide](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Fixtures/ToolboxBindings/README.md) explains exact scope, field mapping and test-only projection limits.

| Artifact | Scenario |
|---|---|
| [Binding catalog](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Fixtures/ToolboxBindings/binding-catalog.v1.json) | All 42 approved checklist items retained; 16 fixture-bound groups, 2 expected-contract-only groups, 24 unresolved groups |
| [Single parcel bundle](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Fixtures/ToolboxBindings/single-parcel.bundle.json) | One parcel/owner/pair; duplicate review required while cross-parcel sharing is N/A |
| [Cad Map bundle](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Fixtures/ToolboxBindings/multi-parcel-cad-map.bundle.json) | Three parcels, shared links, all owners/pairs; retained source-area finding plus unavailable required LTD evidence |
| [Manual recovery bundle](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Fixtures/ToolboxBindings/manual-geometry-recovery.bundle.json) | Zero extraction, manual capture, original/corrected owner values, Compare-local correction and recorded missing north arrow |
| [Focused tests](../../../src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxBindingFixtureTests.cs) | Ten harness groups validating bindings, variable Cad Map counts, scope/evidence integrity, service replay and negative cases |

These are local fixtures, not a complete production policy. Unresolved items retain requirements; no inferred N/A or clean case status. M41/M42 snapshots are not an implemented cumulative-finding/completeness engine. No new product approval or publication authority is inferred.

## Isolation

Only the isolated test project is changed: fixture JSON, guide, test class, test registrations and JSON content-copy declaration in its csproj. Existing 13.3/context and policy code is preserved. No operational source/settings changes, no application call sites, no add-in deployment, no FGDB files or remote writes.

Source-backed service tests inject in-memory configuration; mock LTD records are explicitly separated from query keys. Source evidence is synthetic text with page/zone links, not real PDF extraction. Field mappings and deferred bindings need domain/runtime review before production activation.

## Verification

Verified 2026-10-08:
- Clean solution Rebuild succeeded with 0 warnings/errors, using isolated obj-bindings intermediates after the recurring missing WPF generated-file failure on the ordinary intermediate path.
- 64 focused harness tests passed after the Cad Map clarification: 10 fixture tests, 19 policy tests, 8 Toolbox context tests, 14 workspace planner tests, 8 settings workspace tests and 5 workflow execution settings tests. Generated Cad Maps with 2, 3, 5, 10, 50 and 100 parcels pass fixture scope/evidence validation, policy resolution and per-parcel closure/orientation service replay. These are tested sample sizes, not a maximum or an end-to-end large-plan performance certification.
- Four JSON files parse and their deployed test-output copies match the source fixture files. All guide/delivery links resolve.
- Catalog covers all 42 approved IDs/defaults. Sixteen groups have fixture-bound declarations, two have expected-only contract snapshots and 24 remain unresolved. Their requirements are retained.
- Existing validation/manual/memorandum code is exercised with isolated in-memory configuration. Mock queries and expected finding/stage snapshots are not presented as executed production adapters.
- Operational tracked diff remains empty. This slice changes no application source, settings, DAML, policy engine or UI.

The full harness was not rerun for this fixture slice. Known full-harness failures from 13.3/policy work remain outside scope; focused passes do not claim full-suite green.

## Next

Use the fixtures to implement 13.4A scoped findings/completeness, after its canonical identity/dependency gates are satisfied. Bind the remaining 24 groups and replace mock/contract-only adapters deliberately; keep policy publication, persistence and remote handoff separate.
