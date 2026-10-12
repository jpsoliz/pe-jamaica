---
project_name: 'Sid-jamaica'
user_name: 'JotaPe'
date: '2026-09-12'
sections_completed: ['technology_stack', 'language_specific_rules', 'framework_specific_rules', 'testing_rules', 'code_quality_style_rules', 'development_workflow_rules', 'critical_dont_miss_rules']
existing_patterns_found: 18
status: 'complete'
rule_count: 81
optimized_for_llm: true
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

- ArcGIS Pro add-in implemented in C# with WPF, DAML, and ArcGIS Pro SDK references.
- Main add-in project: `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/ParcelWorkflowAddIn.csproj`.
- Target framework: `net8.0-windows`; platform: `x64`; `UseWPF=true`; nullable reference types enabled.
- Configured SDK lane in `WorkflowSettings.json`: ArcGIS Pro SDK `3.6`, Visual Studio `2022 17.13+`, `net8.0-windows`.
- ArcGIS Pro SDK assemblies are referenced from `C:\Program Files\ArcGIS\Pro\bin`; do not replace them with NuGet packages.
- ArcGIS Pro SDK packaging target is `C:\Program Files\ArcGIS\Pro\bin\Esri.ProApp.SDK.Desktop.targets`; build fails intentionally if it is missing.
- Python/ArcPy processing lives under `src/ProcessingTools`; C# should call adapter/tool seams instead of rewriting ArcPy processing in the add-in.
- Validate Points orientation and bearing checks are shared contract concepts: Python validation emits deterministic diagnostics, C# surfaces them, and output generation consumes the same truth.
- ArcGIS Python executable is configured in `WorkflowSettings.json`; current path points to `C:\JPFiles\Dropbox\Sidwell\Development\AI-Survey\python-envs\arcgispro-survey-ai\python.exe`.
- Deployment Python environment is cloned from ArcGIS Pro `arcgispro-py3`; conda requirements are intentionally empty unless a package is proven compatible with ArcGIS Pro pins.
- Pip dependencies include OpenAI/PDF tooling such as `openai==1.109.1`, `pdfplumber==0.11.9`, `pypdfium2==5.8.0`, and `pytest==8.2.1`.
- Runtime configuration is JSON-backed in `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Settings/WorkflowSettings.json`.
- Parcel Search source configuration is stored under `compare_enterprise_cadaster`; the Settings UI summary is read-only and the Advanced JSON remains the source of truth for URLs, sublayers, field mappings, display names, popup fields, and parish lookup.
- Test project is a lightweight executable harness at `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests`, not xUnit/NUnit.

## Critical Implementation Rules

### Language-Specific Rules

- C# nullable reference types are enabled; preserve null checks and explicit fallback behavior instead of silencing warnings casually.
- Prefer small service classes, records, and explicit result objects over large UI code-behind changes.
- Keep ArcGIS Pro SDK calls out of ordinary ViewModel logic; use service seams that can be mocked or safely no-op outside ArcGIS Pro.
- Use `async`/`await` for long-running work and preserve cancellation tokens where existing services expose them.
- Do not block the WPF UI thread with service calls, filesystem scans, Python execution, HTTP calls, or ArcGIS map work.
- Treat settings as strongly loaded documents through existing settings services; do not parse `WorkflowSettings.json` ad hoc in UI handlers.
- Preserve existing `StringComparer.OrdinalIgnoreCase` and case-insensitive matching patterns for IDs, source names, statuses, and field names.
- JSON contract and settings fields use lowercase `snake_case`; C# public properties remain PascalCase.
- Python/ArcPy code should stay behind adapter/tool boundaries and write contract-compliant JSON artifacts rather than calling UI logic or relying on hidden ArcGIS Pro project state.
- Do not rewrite existing Python processing logic into C# unless a story explicitly changes the processing boundary.

### Framework-Specific Rules

- ArcGIS Pro is the host application; do not introduce standalone desktop, web, Electron, or generic GIS app patterns for core workflow features.
- Register ArcGIS Pro commands, tools, and dock panes through `Config.daml`; keep captions/tooltips aligned with existing Sidwell/Cadastre Tool language.
- ArcGIS map, layer, selection, symbology, project, and geodatabase operations must run through ArcGIS Pro SDK-safe seams and `QueuedTask.Run` where required.
- WPF UI should remain compact and ArcGIS Pro-adjacent: tabs, grids, dense controls, direct labels, and restrained styling.
- Do not put processing, HTTP, or ArcGIS map manipulation directly in WPF event handlers; route through services/ViewModels.
- The active ArcGIS Pro map is the companion surface. Do not embed a custom map preview in the dockpane unless a story explicitly requires it.
- Settings belong in the existing Settings workspace and `SettingsWorkspaceService` / `SettingsWorkspaceDocument` flow.
- Parcel Search settings belong in the dedicated Settings > Parcel Search tab. Keep Settings > Map Layers focused on basemap/reference layer planning.
- Working map/reference-layer behavior belongs near `Workflow/Maps` and existing map preparation services.
- Compare/cadaster query behavior should reuse existing `Compare` settings and query seams before creating new service families.
- Compare Neighbor Search controls may share `compare_enterprise_cadaster` settings, but keep their UX clearly separated from Parcel Search source mappings because the spatial mode/buffer are not map-layer reference settings.
- Enterprise working-layer behavior must stay distinct from final authoritative promotion; do not imply external CADINDEX or authoritative sync unless a story explicitly implements it.

### Testing Rules

- Use the existing executable test harness in `ParcelWorkflowAddIn.Tests`; add focused test classes there rather than introducing xUnit/NUnit unless a story explicitly changes the test framework.
- Run C# checks with `dotnet build src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.sln /p:UseSharedCompilation=false` and `dotnet run --project src\ParcelWorkflowAddIn\ParcelWorkflowAddIn.Tests\ParcelWorkflowAddIn.Tests.csproj --no-build`.
- Prefer unit-testable service seams for settings, query planning, path resolution, contract serialization, redaction, and workflow state.
- ArcGIS Pro SDK map operations should be isolated behind interfaces or services so most logic can be tested without launching ArcGIS Pro.
- For code that requires ArcGIS Pro runtime, document manual smoke coverage and keep automated tests around the pure planning/mapping logic.
- Settings changes need round-trip tests that prove unrelated JSON settings are preserved and existing loaders still understand saved values.
- Query/service work needs tests for success, no-record, timeout/failure diagnostics, redaction, and disabled/incomplete configuration.
- Parcel Search tests should cover per-source criteria mapping. A selected source should only query a criterion when that source has an explicit field mapping or a documented migration/default for that criterion.
- PXA memorandum review rules are compute rule catalog entries. Tests should cover extraction normalization, not-applicable behavior, Settings/catalog visibility, persistence, UI labels, disposition gating, and report inclusion when these rules change.
- PXA memorandum evidence values are first-class review data: table/narrative section type, scale-bar text evidence, split instrument-check date/result values, and explicit negative attendance text such as `No one appeared` must remain visible in the Memorandum tab, saved rule JSON, review hash, and report path.
- PXA memorandum detection must prefer visible/extracted text evidence over stale boolean flags: if OCR or embedded text contains `MEMORANDUM`, the memorandum rules should evaluate, and scale labels such as `SCALE : 1cm To 10m R.F 1/1000` should remain visible evidence instead of collapsing to only `Present`.
- PXA review loading must recover memorandum applicability from root-level OCR/text fields and memorandum-sourced metadata, not only from `document_sections.memorandum`; real case artifacts may put visible text evidence outside the normalized section object.
- PLA Plan Annexation is a distinct Innola transaction profile from PE/PXA. It uses required source type `st_plan_annexation_pdf`; do not route it through PE computation-sheet extraction or hardcode it as PXA.
- PXA single-parcel survey-plan intake must recognize both Innola transaction type names `Plan Examination by Area` and `Plan Examination v2` as equivalent aliases for the same `pxa_single_parcel_survey_plan` behavior.
- PLA plan PDFs may be image-only, multi-page documents with title information before the plan page. The examiner must select the plan evidence page/area; first implementation may use `selection_type = "full_page"` while preserving schema room for later rectangular crop metadata.
- PLA selected plan evidence should be generated as PDF when practical, with PNG fallback, and kept as a case-folder artifact until the final PRO step saves/attaches generated output documents back to Innola.
- PLA geometry extraction should reuse OCR/vision and reviewed segment/boundary solver patterns where possible. If no usable georeference exists, generate form-valid local-origin geometry such as `(0,0)` and record that it is unreferenced instead of blocking solely for missing geographic placement.
- PLA source-plan versus generated-geometry matching is visual similarity/overlay evidence only; do not label it as survey-accurate georeferencing or authoritative parcel fabric promotion.
- PLA_B is separate from PLA_A: it recovers current transaction geometry from Enterprise `working_review` by stripped PE transaction number and loads the related PE output GDB without using PLA_A extraction, survey-diagram crop, or finalize UX. PE output GDB loading must include standalone feature classes, feature-dataset feature classes, and root raster datasets; `mgeo_overlay_[trnumber]` is a raster dataset in tested GDBs and must be loaded with 70% transparency.
- Supporting Documents crop/attach from Story 2.23F is a shared viewer capability for copied case-folder PDF/PNG/JPG/TIFF documents: users draw a crop, choose DPI defaulting to 300, save PNG first to `working/pla_b/survey_diagram_selection.png`, then attach to the Current TR only using configured source/document type `st_plan_annex_image`; crop export must use controlled source coordinates, not WebView/screen screenshots.
- RT Examination is stage-driven: any selected transaction at task/stage `In RT Examination` can launch the RT workspace regardless of main transaction type. Supporting documents load from the parent/main transaction, while RT review/writeback targets only the current/main RT transaction Plan; linked/originating PE data is read-only source context.
- RT Examination `Save` and `Save & Close` have intentionally different side effects. `Save` only confirms and writes current RT Plan `neighbors` updates; it must preserve `checkList` and never write SpatialUnit data. `Save & Close` confirms, performs the same neighbor save first, updates/appends exactly one current RT PlanCheck row using a valid Innola `plan_check_type_*` key with `passed = true`, completes the selected Innola workflow transition, refreshes, cleans RT map/content state, and closes the form.
- RT Examination `Save & Close` is branch-driven at the post-examination gateway. The user must choose an RT result before completion: `Yes, proceed next` targets `Review Completed RT Examination`; `No, prepare pre-check log sheet` targets `Prepare Plan Pre-Check Log Sheet`. Pass the selected target stage plus gateway-label aliases as the desired Innola workflow transition, record the selected branch in RT artifacts, and fail closed if an explicitly requested transition cannot be matched.
- RT Examination Plan children must use Innola-generated child object templates when the current Plan arrays are empty. Create missing `Neighbor` rows with `{ "@c": "Neighbor", "id": null }` and missing `PlanCheck` rows with `{ "@c": "PlanCheck", "id": null }` through `/api/v4/rest/data/objects/create`; do not fabricate local child IDs or copy child rows from the `original` linked/PE snapshot for current RT writeback.
- RT Examination Spatial Units are read-only parcel context. Populate the tab from originating PE SpatialUnits or Enterprise `working_review` polygon rows and initially show `parcel_name`, `area_sqr`, `suid`, and `created_utc`; geometry, boundary fields, and SpatialUnit attributes must not be saved by RT Examination.
- RT Examination parent attachment preload is tolerant of individual parent attachment failures. Keep successfully copied parent documents, record failed attachments as manifest warnings, and fail only when no usable source document is available; non-RT transaction loading remains stricter and should still fail/clean up on partial attachment failure.
- Compare title-image lookup is local-only: title sources found from computed participant Volume/Folio rows are downloaded into the case `source` folder as `Repo_[Volume]_[Folio].pdf`, refreshed in the Compare document selector, and must not be registered/uploaded back to Innola unless a later story explicitly changes that boundary.
- Workflow/state-changing stories must test restart/reopen, stale artifacts, duplicate IDs, missing/corrupt artifacts, and explicit failure messages when practical.
- Python adapter changes should include Python tests where pure Python logic changes, and must keep emitted JSON artifacts contract-compatible.
- Orientation and bearing-consistency changes need tests for clockwise detection, counterclockwise detection, indeterminate geometry, missing/unparsable bearing text, and tolerance-driven mismatch outcomes.
- Output-orientation normalization tests must prove saved reviewer point order remains unchanged while generated downstream geometry can be reversed by policy.
- Do not mark a story complete only because the happy path passes; include at least one negative or edge case for each new service boundary.

### Code Quality & Style Rules

- Keep files organized by domain folders already present: `CaseFolders`, `Compare`, `Contracts`, `Enterprise`, `Innola`, `Preflight`, `Settings`, `Workflow`, and focused UI files.
- Use PascalCase for C# types/properties/methods and lowercase `snake_case` for JSON fields and persisted artifact names.
- Prefer explicit record/result types for service outcomes instead of loose tuples or stringly typed status blobs.
- Preserve existing non-secret diagnostic style: clear user message plus technical diagnostic where needed, with secret redaction.
- Add comments only for non-obvious ArcGIS, workflow, or contract decisions; avoid comments that merely restate code.
- Keep WPF XAML compact and functional. Use tabs, grids, expanders, data grids, checkboxes, combo boxes, and masked password controls consistently with the Settings window.
- Do not add broad refactors while implementing a story; extend the nearest existing service/pattern unless the story explicitly calls for restructuring.
- Preserve settings fallback behavior: invalid/missing optional settings should generally produce warnings and safe defaults, not crashes.
- Keep source-specific labels precise. For example, `Fiscal_Cadastre` may be shown as `Cadastral`, but the internal mapping must remain explicit.
- Parcel Search result display uses one reusable feature class with filtered child layers under `Parcel Search Results`; do not create separate stored result feature classes per source unless a story explicitly changes that design.
- Parcel Search labels and popup/display fields must be source-safe and field-map-safe: resolve configured field names to actual returned service fields, stamp normalized result fields, and log diagnostics for configured field, actual field, and produced label values.
- Any persisted or logged value that may contain tokens, passwords, raw authorization responses, API keys, or connection strings must be redacted.

### Development Workflow Rules

- BMad story files live in `_bmad-output/implementation-artifacts`; update the relevant story file with Dev Agent Record, completion notes, file list, and verification results when implementing.
- Do not update `sprint-status.yaml` for ad hoc stories unless the story key already exists there or the user asks to add it to sprint tracking.
- Use `dotnet build` before packaging; use `tools/package_addin.ps1 -Configuration Release` for release packaging when requested.
- Installer/deployment assets live under `installer`, `deployment`, `docs/deployment`, and `tools`; do not mix packaging changes into feature stories unless needed.
- `WorkflowSettings.json`, rule JSON files, and deployment manifests are runtime contracts. Preserve unrelated settings when saving or editing.
- Existing generated/deployment package folders may contain copied files; avoid editing deployment copies unless the story is specifically about packaging or target-computer tools.
- Use existing BMad story history as implementation context; recent completed/review stories often document why services exist and what tests caught.
- When changing ArcGIS Pro SDK behavior, verify build locally and document any manual ArcGIS Pro smoke test that cannot be automated.
- Never persist Innola access tokens or passwords. Innola credentials stay session-only unless a story explicitly introduces secure credential storage.
- Innola reads/writes must obtain the session through `InnolaSessionManager.EnsureCurrentSessionAsync` or a service wrapper that calls it; do not pass stale `CurrentSession` values into long-running workflow operations.
- Keep `project-context.md` current when a story establishes a new durable rule or changes a core architecture boundary.
- Current testing/presentation branch `wipRC9` should stay stable. Commit planning/story docs there if needed, then create PE/PXA refactor code work on a separate branch such as `refactor/pe-unified-plan-examination`; do not merge to `master` until the current implementation is validated as the release baseline.
- PE/PXA unification is tracked under Epic 11. Story `11-1` is a docs-only inventory/decision lock; Story `11-2` is the first code-changing contract story and should only change the review contract/model/persistence/tests, not route planning, OCR/vision extraction, UI redesign, Enterprise publish, or Innola finalize behavior.
- Latest tracked implementation context: Story 8.8 RT Examination is complete through add-in `1.1.468`. Recent fixes recovered partial RT case folders missing `manifest.json`, made RT parent attachment preload tolerant after at least one source document loads, added explicit `Save & Close` branch selection, and corrected RT PlanCheck completion writeback to preserve/use valid Innola `plan_check_type_*` values instead of sending literal `checkType = approved`.

### Critical Don't-Miss Rules

- Do not bypass the review-before-output workflow. Extraction review approval, validation gates, and output creation must remain explicit and auditable.
- Do not let PE/PXA refactor stories change presentation-tested behavior casually. The unified Plan Examination contract must preserve current PXA single-parcel behavior, PE multi-parcel review behavior, approved-review validation, output generation, Enterprise `working_review` publish, and Innola finalize boundaries unless a later story explicitly changes one boundary with tests.
- Do not treat ArcGIS Enterprise working layers as final authoritative sync. Working review, Enterprise working layers, Enterprise Parcel Fabric, and final promotion are separate concepts.
- Do not hardcode Case Folder paths, settings paths, source layer URLs, field names, or Python executable paths when existing settings services can resolve them.
- Do not create hidden state that is required for recovery. Case Folder artifacts and configured Enterprise working layers must be enough to resume or diagnose work.
- Do not duplicate cadaster source configuration. Reuse or extend `compare_enterprise_cadaster` and working map reference-layer settings where appropriate.
- Do not treat the Parcel Search summary grid as a separate editable settings store. Changes must persist through `compare_enterprise_cadaster` JSON and then refresh the summary from that JSON.
- Do not query Survey for DP/R/LandVal/volume/folio/name criteria unless the Survey source explicitly maps those criteria. Survey is primarily PE-based unless configuration proves otherwise.
- Do not hide per-source query failures completely. Show concise safe diagnostics in the Search pane and write fuller safe query details to the parcel-search log so bad FeatureServer URLs, bad layers, or missing mappings can be diagnosed.
- Do not create duplicate ArcGIS maps, reference layers, or result layers when existing map preparation/reuse patterns apply.
- Do not run ArcGIS Pro SDK map/layer operations off the required ArcGIS Pro thread.
- Do not let broad searches, service paging, or Python/geoprocessing runs freeze the dockpane.
- Do not log raw HTTP authorization failures, bearer tokens, API keys, passwords, or certificate details beyond safe diagnostics.
- Do not assume Fiscal Cadastre is Legal Cadastre. Fiscal/Cadastral context and Legal ownership authority must remain labeled separately.
- Do not assume source layers share identical schemas. Field mappings are per source and missing fields must produce clear warnings.
- Do not let Fabric Maintenance final-candidate discovery depend on identity/status SQL before spatial evidence is found. The examiner must see Legal/Cadastral parcels that spatially intersect the working parcel; PID, active-status, and configured key checks are review evidence, not a prefilter that can hide overlaps.
- Do not remove local transaction GDB behavior when adding per-user or Enterprise working functionality; these solve different workflow needs.
- Do not mark workflow completion, Innola completion, or sync readiness unless the configured gate actually passed.
- Do not infer parcel orientation from the first bearing. Orientation is computed from the full ordered closed ring and may be `clockwise`, `counterclockwise`, or `indeterminate`.
- Do not fabricate bearing-consistency passes when bearing text is absent or unparsable. Record skipped/not-applicable diagnostics and keep the examiner-facing result explicit.
- Do not mutate saved reviewer-entered point order to satisfy downstream orientation preference. Optional normalization belongs only in output geometry generation and diagnostics.
- Do not rewrite deployment/package copies as the source of truth; modify source files and regenerate packages when needed.
- Do not use decorative or marketing-style UI for operational dockpanes. Users need dense, predictable, technical controls.

---

## Usage Guidelines

**For AI Agents:**

- Read this file before implementing code in this project.
- Follow all rules exactly as documented.
- When in doubt, prefer the more restrictive option.
- Update this file when a story creates a durable new implementation rule.

**For Humans:**

- Keep this file lean and focused on agent needs.
- Update it when technology stack, architecture boundaries, or workflow patterns change.
- Remove rules that become obvious or obsolete.
- Prefer project-specific rules over generic engineering advice.

## Toolbox Continuation - 2026-10-09

User is pausing and intends to return in approximately six hours. No scheduled execution or automatic continuation requested.

### Workspace And Status

- Continue only in `D:/Code/BMad-Method/dev/pe-jamaica/temp/toolbox-worktree`, branch `refactor/toolbox-case-workspace`. Preserve the Toolbox checkpoint and subsequent uncommitted work; do not reset, deploy, merge or mirror it into the operational checkout.
- User authorized a source-control checkpoint on this branch, with message `feat(toolbox): checkpoint through 13.4B implementation; pending review`. Use `git log -1` to identify the checkpoint; committing does not accept 13.4B or authorize deployment.
- Operational checkout remains on `wipRC9`, baseline `5908f30617cdc49e09873635a30343da8aff9ad6`; tracked diff was empty at handoff.
- 13.1/13.2 accepted, 13.3 finished, 13.4A signed off for the isolated in-memory contract. D07 P01-P05 approved as `toolbox_d07_reconciliation_v1`.
- 13.4B implemented in isolation and in **review**, not accepted. Parent 13.4 remains **in-progress** in the separate Toolbox tracker; no child sprint key added. 13.5 has not started.
- Next action: review 13.4B implementation/evidence against the approved D07 rules, fix confirmed findings, then obtain acceptance before proceeding to 13.5. Do not infer acceptance from the completed implementation tasks.

### Read First On Resume

- [13.4B story](implementation-artifacts/toolbox/13-4b-add-rerun-reconciliation-shared-boundaries-and-lineage.md).
- [13.4B implementation evidence](implementation-artifacts/toolbox/13-4b-implementation-evidence.md).
- [13.4A evidence and baseline limitations](implementation-artifacts/toolbox/13-4a-implementation-evidence.md).
- [Separate Toolbox tracker](implementation-artifacts/toolbox/sprint-status.yaml).
- [Decision register](planning-artifacts/toolbox/toolbox-decision-register.md). Its latest follow-up supersedes historical preparation/execution-pending notes.

### Delivered And Preserved

- New services: `ToolboxReconciliation.cs`, `ToolboxBoundaryDependencies.cs`, `ToolboxParcelLineage.cs` under `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Model/`.
- Narrow updates: `ToolboxParcelModel.cs`, `ToolboxExamination.cs`; new `ToolboxReconciliationTests.cs` and harness registrations. Canonical owner, policy loader/resolver, original binding fixtures and A test bodies reused unchanged.
- Reruns require complete case/source/base/policy/digest binding and exhaustive examiner choices. Manual values, original snapshots, raw suggestions and histories remain separate; empty extraction does not delete current entities. Ordinary same-key `ReAdapt` remains strict.
- Shared physical boundaries retain independent segments and declared points. Actual dependency fingerprints and old/new graph union invalidate consuming checks/decisions, including excluded/retired neighbors. Adjacency and boundary removal require explicit evidence; unknown coverage or consistency blocks readiness.
- Split/merge results receive fresh parcel identities and explicit inclusions, with retained parents and auditable finding/manual applicability. Original finding scopes/classification/history are not reassigned or automatically resolved.
- Old human validation and finding dispositions cannot be renewed merely by refreshing check hashes. Changed inputs need current review/disposition; forwarding with source defects follows the normal next stage and does not approve defective geometry.
- Compute requires explicit local geometry permission; Compare remains geometry-read-only with tabular corrections permitted. All included parcels require current dispositions; selection cannot hide unresolved candidates. Cad Maps have no three-parcel limit.
- Future Toolbox FGDB name is `TR-<transaction-number>.gdb`; legacy numeric naming stays unchanged. B creates no FGDB or durable journal. Canonical JSON remains the persistence owner until the separately approved persistence work.

### Verification At Handoff

- Final build: zero warnings/errors.
- Focused harness: **125 passed**, preserved 105 plus 20 B regression groups; includes variable counts 1, 2, 3, 5, 10, 50 and 100, manual recovery, lineage, shared-point changes, stale proof and A fingerprint compatibility.
- Final full-suite retry: **146 passed before fail-fast**, then `UnauthorizedAccessException` at `ManifestSerializer.Write:28`, called by `CompareWorkspaceViewModelTests.SaveAndRestoreDraftPreservesNotesAndEvidence:94` through helpers 1508/1478. Later tests did not execute. Full suite did not pass; existing Manifest access and Compare XAML limitations remain documented.
- Local self-review only; no independent peer/GIS certification, host smoke test, FGDB round trip, live TR/Innola transition or deployment claimed. Source hashes, authority and geometry semantics are trusted adapter inputs in synthetic fixtures, not production security/topology proof.
- Source/document whitespace and relative links checked. Generated `obj-toolbox-13-4b` directories in both projects removed and verified absent; built test binaries remain available. No build/test session remains running.

Verification commands, from the isolated worktree:

```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln /p:BaseIntermediateOutputPath=obj-toolbox-13-4b/ /p:UseSharedCompilation=false /m:1
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner" "settings workspace" "workflow execution settings" "review persistence" "review approval"
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll
```

SDK builds and harness runs needed outside-sandbox access. Preserve separate build/focused/full results and exact fail-fast limitations; do not fix unrelated legacy Manifest/XAML behavior as part of B review.

## Toolbox Review Corrections - 2026-10-11

- All eight 13.4B review findings are fixed in isolation; ten regression groups added. Final build has zero warnings/errors and 135 focused tests pass. Full suite passed 207 tests then stopped at the existing Compare XAML progress-indicator assertion; later tests did not execute.
- JotaPe explicitly accepted 13.4B for its isolated in-memory scope on 2026-10-11; B and parent 13.4 are done for M2. See `implementation-artifacts/toolbox/13-4b-review-fixes-2026-10-11.md` for current evidence, superseding historical continuation status.
- Parent `implementation-artifacts/toolbox/13-5-add-versioned-toolbox-workspace-persistence.md` remains backlog for its gated scope. Child `implementation-artifacts/toolbox/13-5a-add-storage-contract-and-hydration-foundation.md` is implemented/in review: no-writer headless byte codec and validated A/B hydration. Final clean build, 158 focused passes (135 preserved + 23 new); full207 then known Compare XAML failure. See `implementation-artifacts/toolbox/13-5a-implementation-evidence.md`. No extra tracker key; run the exact child. Disk/import/schema work must be refined after A acceptance; production schema/ownership and FGDB recovery/edit/snapshot/deployment gates remain. No cutover or operational 11.2 closure.
- Dependency checks must declare their geometry/nongeometry input class, agree with review records, and bind known scopes. Logical decision nodes need valid parcel bindings. Every decision consuming a linked parcel must consume the link's supporting evidence; source hashes and linked traces participate in stale-proof checks.
- Preserve accepted disposition audit history (operation/digest/revisions, actor/reason/evidence/choice, UTC acceptance time), same-case archived correction identity and append-only applicability during future persistence. Replay must not duplicate audit records.
- B fingerprint versions were updated; A's v2 and geometry-only canonical contracts remain compatible. Existing operational review writer/hash/approval, UI, source adapters, settings and remote boundaries remain unchanged.
- Storage hydration must never replay review/reconciliation actions or refresh proofs. V1 byte codec locks field names/types/nullability and enum values; hashes are transport/representation IDs, not authenticity, geometry verification or committed revision proof. Trusted policy pins and fresh permissions remain external.
- Prototype capture rejects unsupported dictionary comparers, missing A historical evidence and pending proposals. Preserve default vs empty boundary PointIds; the existing generic domain hash cannot evaluate default arrays, so inspection is not approval. Do not silently normalize them during persistence or alter accepted hashes without a separate reviewed domain change.

Last Updated: 2026-10-11

## Toolbox 13.5A Scoped Acceptance - 2026-10-11

- JotaPe explicitly accepted 13.5A for isolated no-writer storage-contract/byte-codec/hydration; child done. This supersedes historical in-review descriptions above. No separate tracker key; child status and parent tracker comments are synchronized.
- Preserve 158 focused passes, full-harness stop after 207 at known Compare XAML failure, and default-PointIds domain-hash limitation. No full-suite green, disk durability, FGDB/host proof, production cutover or operational 11.2 closure implied.
- Next refine remaining M3 disk/import/schema foundation and ownership/path/writer gates; acceptance does not authorize its implementation or a commit/deployment.

## Toolbox Remaining Persistence Refinement - 2026-10-11

- Remaining M3 scope is refined, not implemented: `implementation-artifacts/toolbox/13-5c-add-isolated-prototype-disk-revisions.md`, `13-5d-add-read-only-copied-case-import.md`, `13-5e-define-fgdb-schema-and-projection-mapping.md`, `13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md`. All backlog pending their named contract/dependency gates; parent backlog, no additional tracker keys.
- Order A -> C -> D/E -> B; E's catalog review can run alongside C. B retains final actual FGDB commit/edit/snapshot proofs and isolated ownership activation, never reuse its ID for disk-only work. Next review C's scoped path/writer/conflict contract and obtain separate implementation authorization.
- C uses `toolbox/prototype-store/` and its own disk contract around unchanged A `prototype_bundle` bytes. Ordinary-file commit receipts are not domain approval/accepted geometry/FGDB authority. Never expand locked A v1 field/type/nullability/enums to active-store metadata silently.
- D requires frozen sanitized source bytes, explicit source/target identity and reviewed mapping. Legacy top-level transaction_id can mean transaction number; stale copied_path must not read the original root. Legacy approval/task flags remain audit observations, not current acceptance/permission. Missing known Toolbox maps/history cannot be repaired with new-import identity allocation.
- E exact schema/geometry profile and D01/D11 ownership review precede host writes; C/D precede affected storage/import integration. F01/F02/F03 and F07 before installation remain B gates. No operational writer/source/settings/registration/remote changes or production/11.2 closure authorized by refinement.

## Toolbox 13.5C Contract Corrections - 2026-10-11

- JotaPe requested application of the ten findings in `implementation-artifacts/toolbox/13-5c-contract-review-2026-10-11.md`; all R1-R10 are incorporated in the 13.5C story. This supersedes the earlier next-contract-review instruction: the next step is separate runtime implementation authorization for corrected C, then its capability/tests/acceptance evidence. Child/parent remain backlog.
- C now separates Create/Open, serializes observations with the existing writer lock, seals complete staged directories, defines journal transitions and historical publication proof, fixes hash/asset rules and traversal budgets, and separates transport integrity from read-only policy inspection. Relocation fixtures require a source-locked complete copy and explicit destination lock preparation; clones have no coordinated writer guarantee.
- Document corrections do not demonstrate Windows replacement/race safety or authorize runtime code, commits or deployment. Preserve the adapter operation-table capability gate, accepted A byte/DTO/hydration semantics, 158 predecessor focused passes and known full-suite/default-PointIds limitations. Order remains C -> D/E -> B with existing production/FGDB/operational gates.

## Toolbox 13.5C Story Preparation - 2026-10-11

- JotaPe explicitly requested preparation/review of the three implementation stories with no coding. Specifications: `implementation-artifacts/toolbox/13-5c-1-define-disk-contract-and-recovery-logic.md`, `13-5c-2-define-windows-containment-locking-and-replacement-adapter.md`, `13-5c-3-define-revision-save-open-retry-recover-and-abandon.md`. Parent C remains the contract source; no new tracker keys. See `13-5c-story-preparation-review-2026-10-11.md` for coverage and remaining implementation evidence.
- C.1 owns strict records/hashes/limits/pure recovery planning; C.2 owns contained native filesystem primitives and actual Windows capability proof; C.3 owns semantic Create/Open/Save/retry/Recover/Abandon composition. Implementation order C.1 -> C.2 -> C.3; later code depends on scoped predecessor acceptance. Preserve overall C -> D/E -> B order.
- Preparation/review completion means specifications are complete, not implemented features, acceptance or authorization. All code stories remain backlog, runtime tasks unchecked, implementation_authorized false. No-coding instruction stays active until the user authorizes runtime work; no commit/deployment or production/operational gate closure implied.

## Toolbox D/E/B Story Preparation - 2026-10-11

- JotaPe requested the next D, E and B specification refinements under the continuing no-coding scope. Child stories are `13-5d-1/2/3-define-*`, `13-5e-1/2/3-define-*` and `13-5b-1/2/3-define-*` under implementation-artifacts/toolbox/. Their parent files link exact paths. See `13-5deb-story-preparation-review-2026-10-11.md` for reviewed handoffs. No new tracker keys/status changes.
- Order: D.1 source freeze/mapper/plan -> D.2 sealed pinned preparation -> D.3 execute/retry; E.1 exact schema/profile catalog -> E.2 pure projection/read-back -> E.3 disposable hosted adapter; B.1 host commit/F01 with minimal verified baseline -> B.2 actual edit/full snapshot F02/F03 -> B.3 isolated activation/rollback. Overall C -> D/E -> B remains; E catalog authoring may proceed alongside C/D when separately requested.
- D preparation pins target store_id before target creation, validates full plan/request budgets including sidecars and uniquely stores identical source bytes with separate provenance refs. E separates A domain/model, C provenance and host authority namespaces; exact integer owners cannot be silently rounded to FGDB-safe numeric ranges. B distinguishes narrow first-commit snapshot bootstrap from full F03, historical immutable receipt proof from active geometry, and frozen permission audit from fresh execution authority.
- Specifications are complete, not runtime implementation/acceptance. Exact legacy mapper and E catalog/profile are future reviewed deliverables; B.3 exact operational UPDATE list must be enumerated/read before authorized activation work. Preserve explicit authorization fields, baseline158/known207-stop/default-PointIds limits and D01/D06/D11/F01/F02/F03/F07 gates. No source capture, GDB writes, coding, installation, commit or deployment by preparation.

## Toolbox 13.6 Capability Story Preparation - 2026-10-11

- JotaPe requested 13.6 specification preparation under the continuing no-coding instruction. New parent `implementation-artifacts/toolbox/13-6-register-reusable-toolbox-capabilities.md` and children `13-6-1-define-capability-contracts-and-static-registry.md`, `13-6-2-define-capability-eligibility-scope-and-staleness.md`, `13-6-3-define-headless-runner-and-reviewed-adapter-bindings.md`; see `13-6-story-preparation-review-2026-10-11.md` for review evidence. Existing 13.6 backlog entry retained; no child keys or runtime authorization.
- .1 owns immutable descriptor contracts/static registry/T01-T18 coverage; .2 pure reasoned eligibility and exact scope/revision/policy/staleness admission; .3 explicit headless lifecycle/result verification and reviewed injected local bindings. Source-presence/registration is not authority or acceptance. Split mixed-effect T03/T12/T14/T15/T17 operations; server/provider/upload/Enterprise/Innola effects remain disabled for 13.9/later reviewed adapters.
- Preserve actual Compute-only explicit GeometryAccess, read-only linked PE, RemoteActionAccess denial and Compute/Compare-only policy resolver. Do not widen PLA/RT permissions/resolver or reopen accepted C15 classification. D04/D12 detailed unresolved mappings remain reasons to block affected bindings, not defaults. Context lexical root/revision IDs are not contained storage/accepted-host proof.
- Pure definition/eligibility future code can use accepted in-memory fixtures once explicitly authorized; I/O/host/local mutation requires accepted affected 13.5 owner contracts. Runner stages proposals and publishes only through one accepted owner with expected-parent/current-authority checks; no A run/proposal persistence extension or durable idempotency inferred from run IDs. 13.7/13.8/13.10 own presets/UI and 13.9 real handoff effects. Preserve baseline158/known207-stop/default-PointIds limits and all production/host gates; no coding, tests, commit or deployment by preparation.

## Toolbox 13.7 Compute Story Preparation - 2026-10-11

- JotaPe requested 13.7 under continuing documentation-only/no-coding scope. Parent `implementation-artifacts/toolbox/13-7-compose-compute-toolbox-preset.md` and children `13-7-1-define-compute-routes-and-prerequisite-planning.md`, `13-7-2-define-compute-review-and-manual-correction-workspace.md`, `13-7-3-define-compute-save-reopen-and-compare-readiness.md`; see `13-7-story-preparation-review-2026-10-11.md`. Existing 13.7 backlog key retained, child keys absent, runtime authorization false.
- .1 owns explicit automatic/assisted/manual composition and reviewed candidate/provider seams; .2 compact WPF source/parcel/point/segment/tabular/finding review with typed command proposals; .3 accepted-owner save/reopen and exact all-included examination disposition/local Compare-input record. .2 uses a mock/interface for .3 commands before .3 implementation; no cyclic durable-save acceptance requirement.
- Preserve C15 M01-M42 applicability, independent source-presence/accuracy, cumulative findings and C11 same-examiner forwarding of genuine source defects. Correct capture mismatches or use reviewed manual recovery; stage forwarding is not geometry approval/final Compare decision. Unknown D04/D12 detailed effects/deferrals block affected gates, no default supervisor or first-defect stop. Shared-edge/selected-out/excluded/retired consumers remain covered.
- Optional extraction provider requires separately reviewed effect/binding and explicit provider authorization before relaxing 13.6 denial; never enables Innola/Enterprise/attachment/transition. Manual labels/solver-derived rows are not Toolbox IDs; use owned proposals/accepted reconciliation, never live mutating solver calls. No silent A run/proposal/coverage DTO expansion: .3 durable mapping must be reviewed or requested save blocked. Actual geometry save needs accepted 13.5 host owner, UI integration needs exact UPDATE list/read/isolation/F07 when installed. Preserve baseline158/known207-stop/default-PointIds limitations; no coding/UI/provider/host tests, commit or deployment by preparation.

## Toolbox 13.8 Compare Story Preparation - 2026-10-11

- JotaPe requested 13.8 under continuing documentation-only/no-coding scope. Parent `implementation-artifacts/toolbox/13-8-compose-read-only-compare-and-correction-flow.md` with children `13-8-1-define-revision-bound-geometry-and-comparison-evidence.md`, `13-8-2-define-compare-workspace-tabular-correction-and-partial-review.md`, `13-8-3-define-compare-decisions-and-compute-correction-loop.md`; see `13-8-story-preparation-review-2026-10-11.md`. Existing 13.8 backlog key retained, no child keys, runtime authorization false.
- .1 owns exact immutable local-first input/reference geometry and scoped external-read evidence; .2 compact WPF evidence/value review, whitelisted current-case tabular command and owner-backed partial Save/Reopen; .3 complete local examination Approved/Rejected choice and controlled saved-return/new-revision Compute correction loop. .2's .3 command interface is mockable, not a dependency cycle. Return/select preset never changes actual Innola task or grants geometry authority.
- Geometry/reference/source PE remains immutable in Compare, enforced at command/owner boundary; geometry-driving/ambiguous tabular fields return to Compute. External queries need separately reviewed read-effect/bounded source binding and explicit permission; 13.6 server-write/lifecycle/RemoteActionAccess denial stays. Legal ownership versus fiscal/taxpayer/Enterprise labels, multiple parties/pairs and no-record/mismatch/ambiguity/outage/source absence remain distinct.
- Required unavailable/incomplete evidence is pending with scoped reason and preserved partial work, not automatic approved/rejected/N/A. Genuine forwarded source defects may be inspectable without valid geometry; final rejection need not falsely require accepted geometry, while approval cannot certify invalid geometry. Legacy blocked/saved_progress/approved JSON markers are not new complete decisions. New comparison/outcome/coverage storage must be admitted by reviewed owner extension/sidecar or Save blocked; no A schema/enum widening, dual writer or dropped evidence.
- Preserve fresh-policy/context reopen, current all-included completeness, raw/manual/source review/finding histories and dependency-based invalidation after tabular or Compute geometry revision. Do not reuse legacy CompareWorkspaceViewModel Finalize/upload/Complete/Suspend/Cancel commands; 13.9 owns lifecycle/attachments/publish. Exact query/field/storage/UI UPDATE mappings remain future gates. Preserve baseline158/known207-stop/default-PointIds and existing owner/host/production gates; no code/query/UI/host/save experiments, commit or deployment by preparation.

## Toolbox 13.9 Handoff Story Preparation - 2026-10-11

- JotaPe requested 13.9 under continuing no-coding scope. Parent `implementation-artifacts/toolbox/13-9-add-revision-consistent-toolbox-handoff.md`, architecture-ID children `13-9a-define-revision-snapshot-package-and-restore-v2.md`, `13-9b-define-enterprise-projection-and-verified-receipts.md`, `13-9c-define-handoff-ledger-attachments-and-task-transition.md`; review `13-9-story-preparation-review-2026-10-11.md`. Existing13.9 backlog key/no child keys, runtime/sandbox authorization false.
- A owns F03-backed exact snapshot/package/report and bounded safe fresh-root restore with D10 review. B owns exact accepted-geometry projection and actual D05/D06/F05 remote exclusion/read-back with independent owner-held journal/receipt. C consumes A/required B receipts and owns frozen ordered plan/ledger/attachments plus F04/F06 task/response-loss reconciliation. Default A -> B -> C, optional projection only by reviewed policy; no B/C acceptance cycle.
- Durable pinned intent before effect, exact confirmed steps never resent, running/missing response ambiguous until actual read-back resolves truth. Fresh credentials/authority separate from original digest-bound audit. Local UUID/success bool/marker never proves remote idempotency/full content/transition; unsupported protection/dedup/readback blocks mutation/retry. No whole-plan replay or stale rollback over newer data.
- Preserve source-defect/rejected evidence without accepted-geometry claim and pending Compare without final completion. Any geometry publish needs current independently accepted all-included geometry. Fresh actual task/instance/assignment/allowed transition required; fallback/name/preset/first transition cannot authorize. Local RemoteActionAccess denial stays; only a reviewed handoff authority seam may permit specified effects. Restore task provenance grants no saved permission, v1 remains baseline and v2 never automatically overwrites roots or promises old-build compatibility.
- Exact limits/policies/service capabilities/endpoints/UPDATE files remain future deliverables; D02/D05/D06/D10/F03-F06/F07 where applicable stay open. Isolated capability proof effects need separate bounded runner authorization before normal adapters are enabled. No code/snapshot/restore/projection/upload/task calls/tests, installation, commit or deployment by preparation; preserve baseline158/known207-stop/default-PointIds and operational11.2/production gates.

## Toolbox 13.10 PLA/RT Story Preparation - 2026-10-11

- JotaPe requested 13.10 under continuing no-coding scope. Parent `implementation-artifacts/toolbox/13-10-compose-plan-annexation-and-rt-toolbox-presets.md`, children `13-10a-define-pla-a-evidence-and-construction-preset.md`, `13-10b-define-pla-b-read-only-pe-reference-and-annex-evidence-preset.md`, `13-10c-define-rt-current-plan-save-and-branch-completion-preset.md`; see `13-10-story-preparation-review-2026-10-11.md`. Existing13.10 backlog key/no child keys; no runtime/read/host/sandbox authority.
- A uses distinct st_plan_annexation_pdf/explicit selected page, owned candidate/manual review, permitted local-origin/unreferenced geometry and visual-only similarity. Current Compute-only access/Compute-Compare resolver cannot authorize PLA_A until separately reviewed scoped profile/access adapter; never impersonate Compute or silently widen A schemas/hashes. B keeps full originating PE identity/revision/GDB feature/raster reference read-only, handles root mgeo_overlay raster display at70% transparency and standalone current-document crop/attach separately, never PLA_A engine or PE rebuild/write.
- C stage eligibility is In RT Examination regardless of main type; unique verified current/main RT Plan is writable target and PE/SpatialUnits read-only. Keep parent-document partial preload tolerance only for RT. Separate Local Save (owner draft), RT Save (current neighbors only, preserve checkList/no PlanCheck/SpatialUnit/transition) and Save & Close (neighbors, exactly one valid plan_check_type_* passed=true process checkpoint, selected actual transition, then confirmed refresh/cleanup/close). Both chosen branches retain exact target/gateway aliases; no missing/default choice, copied PE child or fabricated ID.
- Reuse13.9 ledger for child/template/neighbor/check/upload/task effects with fresh actual authority/version protection and F06 ambiguity/read-back; no second task ledger or wholesale legacy SaveAsync/finalize wrapper. Confirmed partial effects never resent, and cleanup failure after confirmed completion is local recovery, not remote retry. Unknown role/current Plan ambiguity blocks writes. Exact profiles/reference/field/template/branch/storage/UI UPDATE sets and host/sandbox proofs remain gates; baseline158/known207-stop/default-PointIds/operational11.2 limits retained. No code/effects/tests/installation/commit/deployment by preparation.
- 13.10 review correction pins shared Supporting Documents crop to source-coordinate PDF/PNG/JPG/TIFF export (no screenshots), DPI chosen by examiner/default300, PNG saved first at working/pla_b/survey_diagram_selection.png and configured st_plan_annex_image attached only to Current TR. Owner retains immutable revision bytes for prepared uploads; later crop at display path cannot alter13.9 frozen intent. This is specification correction, not crop implementation or attachment authority.

## Toolbox 13.5C Runtime Authorization - 2026-10-11

- JotaPe explicitly requested completion of C.1 disk protocol/recovery, C.2 Windows adapter and C.3 revision operations. This supersedes earlier no-coding statements for those three C slices only; D/E/B and 13.6-13.10 remain preparation-only. Preserve accepted A and all operational, UI, FGDB, remote and production gates. No commit or deployment is part of this run.
- Implementation is confined to new prototype persistence/test files and scoped Program.cs registration/worker dispatch. Physical filesystem verification discovered the worktree D volume is exFAT; disk tests explicitly use unique disposable Windows Temp roots on fixed NTFS via TOOLBOX_DISK_TEST_ROOT. Unsupported storage is refused, not silently weakened.
- Existing-target replacement requires a verified rooted KTM/TxF writer capability and kernel transaction locks through the closed-target handoff. TxF is an optional/deprecated Windows capability; missing support must fail closed. It covers only one metadata replacement, never a multi-file store transaction or hardware power-loss guarantee. See the C.2 operation table and final C runtime evidence for actual validation/review status.
- C.1-C.3 technical implementation, runtime verification and independent review are complete/review-ready. Final stable build: zero warnings/errors. Focused: 197 groups (158 preserved + 39 new); native interruption: 154 scenarios (112 Save/25 Create/17 Abandon). Full harness: 207 passes then unchanged Compare XAML assertion. Default-PointIds inspection limits and A/domain semantics remain unchanged. Final JotaPe scoped C acceptance remains pending before affected D/E integration; parent13.5 remains in-progress, without new child keys.
