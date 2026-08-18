---
project_name: 'Sid-jamaica'
user_name: 'JotaPe'
date: '2026-08-17'
sections_completed: ['technology_stack', 'language_specific_rules', 'framework_specific_rules', 'testing_rules', 'code_quality_style_rules', 'development_workflow_rules', 'critical_dont_miss_rules']
existing_patterns_found: 18
status: 'complete'
rule_count: 73
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
- Workflow/state-changing stories must test restart/reopen, stale artifacts, duplicate IDs, missing/corrupt artifacts, and explicit failure messages when practical.
- Python adapter changes should include Python tests where pure Python logic changes, and must keep emitted JSON artifacts contract-compatible.
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
- Keep `project-context.md` current when a story establishes a new durable rule or changes a core architecture boundary.

### Critical Don't-Miss Rules

- Do not bypass the review-before-output workflow. Extraction review approval, validation gates, and output creation must remain explicit and auditable.
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
- Do not remove local transaction GDB behavior when adding per-user or Enterprise working functionality; these solve different workflow needs.
- Do not mark workflow completion, Innola completion, or sync readiness unless the configured gate actually passed.
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

Last Updated: 2026-08-17
