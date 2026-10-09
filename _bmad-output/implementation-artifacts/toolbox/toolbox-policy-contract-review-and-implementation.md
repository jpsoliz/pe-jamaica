# Toolbox Policy Contract Review And Local Engine

Date: 2026-10-08
Status: local engine implemented and focused verification passed in isolated worktree; no production integration
Scope: local Compute/Compare policy loader and applicability resolver only
Track: Toolbox settings follow-up; does not complete 13.4A or change sprint status

## Contract Review Findings

| Priority / ID | Finding | Resolution |
|---|---|---|
| High / P01 | Natural-language applicability notes and empty bindings cannot safely serve as an executable policy. | Separate strict local-v1 contract; reject the planning seed. Bounded typed predicates, explicit checks/scopes/effects and exact baseline required. |
| High / P02 | A file's own published flag cannot establish trusted policy identity or publication authority. | Caller must supply policy ID/version/SHA-256 pin. Local v1 accepts only isolated_test policies; draft/published states rejected. Production publication remains out of scope. |
| High / P03 | Absence or unavailable LTD service evidence could be treated as false applicability and erase required work. | Allowlisted applicability facts only; source-value presence/service availability prohibited. Unknown facts return unresolved, never N/A, including exists. |
| High / P04 | Scope identity alone could combine evidence from separate cases. | Require case ID, revision and input fingerprint per scope; reject mixed-case inventories, duplicate scoped IDs and invalid case-scope identities. |
| Medium / P05 | A broad override mechanism could silently downgrade approved requirements or select a last-match preset. | Local v1 rejects all non-empty preset overrides. Future override support requires separately reviewed precedence/approval semantics. |
| Medium / P06 | Parent-item applicability could hide applicable child checks or additional parcels/participants/pairs. | Resolve explicit subchecks for every supplied matching scope. Missing scope inventory produces an unresolved placeholder; tests cover all three parcels, owners/pairs and M14 child checks. |

All findings above are addressed for this isolated local engine. Remaining production-domain work is not inferred complete: exact applicability/adapter/effect bindings, complete authoritative scope inventory, human fact provenance, deferral rules, publication authority, case persistence and final decision/remote action validation.

## Implemented Code Boundary

Implementation exists only under temp/toolbox-worktree:
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Policy/ToolboxReviewPolicy.cs
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Policy/ToolboxReviewPolicyLoader.cs
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Toolbox/Policy/ToolboxReviewPolicyResolver.cs
- src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/Workflow/ToolboxPolicyTests.cs
- Test registrations in the existing isolated Program.cs, preserving 13.3 registrations.

The operational checkout has no source/settings/project/DAML changes. No new settings tab, application call site, adapter execution, credentials, policy publisher, FGDB write or remote request exists. No test-framework dependency added. Pure C# plus System.Text.Json/immutable collections follows the existing executable harness.

## Local V1 Executable Contract

schema_version = toolbox_review_policy_local_v1. This is intentionally distinct from the 0.1.0 planning seed.

Required root fields:
- policy_id, policy_version: nonblank exact identities matching the external pin.
- activation_status: isolated_test only.
- classification_baseline: compute-compare-v1.
- supported_stages: exactly Compute and Compare, once each.
- preset_overrides: empty array.
- items: exactly M01-M42, each once, with display_name and stages.

Each stage contains requirement and checks. Requirements must match C15 defaults; arbitrary promotions/downgrades require a future approved baseline/contract, not an examiner edit. Stage-level N/A has no independent checks. Required/optional entries have at least one explicit check.

Check fields: check_id, scope, applicability, evaluator_id, evaluator_version, effect. IDs are unique per item/stage. Scopes: case/document/parcel/participant/boundary/identifier_pair. Effects: info/report_only/requires_disposition. Evaluator names are declarations only: this engine does not validate external registration or execute an evaluator.

Predicates: always/never; all/any/not with args; equals/in/exists/gt/gte/lt/lte with fact and typed value where needed. Maximum predicate depth 8, compound children 16, in values 32; policy maximum 256 KiB, JSON maximum depth 32. Strict property sets reject missing/unknown fields and duplicate JSON properties, including case-only duplicates. Numeric facts use decimal; parcel_count must be a nonnegative integer.

Allowlisted facts: route.mode, document.memorandum_applicable, document.dwg_in_use, document.computation_sheet_applicable, geometry.recipe, geometry.closed_ring_applicable, scope.parcel_count, comparison.address_applicable, comparison.landval_applicable and comparison.property_name_applicable. These are applicability facts, not raw extraction presence/confidence or API/service availability. Callers must supply trusted applicability evidence; this slice does not grant an examiner permission to manufacture a fact or define a preset policy.

exists means a typed applicability fact is known. Missing fact returns unresolved rather than false; a declared Boolean false is still a known fact. String comparisons use ordinal ignore-case, numeric comparisons use decimal, logical predicates use three-valued logic.

## Resolver Semantics And Limitations

Resolve takes an immutable loaded policy, selected local Compute/Compare preset and an explicit scope inventory. It returns each item/subcheck's declared/effective requirement, applicability, reason, evaluator declaration/effect and policy/case/scope/revision/input provenance. Results bind policy hash and sorted fact inputs into a deterministic resolution fingerprint. Unknown applicability has a null effective requirement, not a fake N/A. Input enumeration order does not change output.

Every supplied matching scope is expanded, not only the active/first parcel or party. Missing scope kinds remain unresolved. The caller must provide the complete verified included-parcel/participant/pair inventory; a resolver cannot detect a silently omitted parcel without authoritative inventory input. This engine is not a case-completeness or approval service.

Declared N/A denotes an independent action in this stage, not erasure of inherited findings. M41 remains required in Compare. Report-only effect remains separate from required human review. Manual/automated/PDF applicability is expressed through explicit scope facts; disabled tools are not a downgrade input.

Results do not include passed/approved/rejected/geometry-valid/remote-allowed flags. Missing-from-source, extraction mismatch, mixed error, source-defect forwarding and LTD pending semantics remain C11-C14 obligations for the future finding/completeness engine. Unavailable LTD evidence cannot disappear from applicability, but this resolver itself does not produce a pending stage decision.

No case manifest writer or automatic migration implemented. Immutable snapshots and fingerprints support future pinning/staleness checks; they do not persist or approve a migrated case. Compare geometry permissions stay in the existing 13.3 local context, unchanged.

## Verification

Verified on 2026-10-08:
- Clean single-worker Rebuild succeeded with 0 warnings and 0 errors.
- 54 focused harness tests passed: 19 new policy tests, 8 existing Toolbox context tests, 14 workspace planner tests, 8 settings workspace tests and 5 workflow execution settings tests.
- Full harness is not green. Default system TEMP initially hit the previously documented ManifestSerializer File.Move access denial. Process-only TEMP/TMP redirection into the isolated worktree then reached the known CompareWorkspaceXamlTests.CompareWorkspaceExposesTaskLifecycleCommands failure at line 83: expected Finalize footer progress binding. Both issues were previously reproduced on baseline in 13.3; neither affected source is modified here.
- Operational git diff contains no tracked changes. Isolated tracked diff is only the harness registrations, retaining all 13.3 entries. New source is limited to the three policy files and one focused test class.
- Source search confirms loader/resolver have no production call sites. Existing active settings, project configuration, DAML and UI remain unchanged.

Synthetic fixture policies are explicitly isolated_test, not domain-approved production catalogs. No live ArcGIS interaction, Innola/LTD queries or FGDB smoke test is claimed.

Reproduction from temp/toolbox-worktree:
```powershell
dotnet build src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.sln -t:Rebuild -m:1 /p:UseSharedCompilation=false
dotnet src/ParcelWorkflowAddIn/ParcelWorkflowAddIn.Tests/bin/Debug/net8.0-windows/ParcelWorkflowAddIn.Tests.dll "toolbox" "workspace planner" "settings workspace" "workflow execution settings"
```

Full-harness failure history remains in [13.3](13-3-add-toolbox-case-context-and-workspace-selection.md); focused passes are not proof the full suite is green. Windows/ArcGIS SDK metadata requires build access in this environment; missing incremental WPF generated files are handled with clean Rebuild, not source/configuration changes.

## Next Boundary

Bind approved applicability/evidence/effect policies using domain-reviewed fixtures, then integrate scoped outcomes and pinning with 13.4A/13.5/13.6-13.8. Keep production settings activation and publication separate. No existing story or domain gate is closed solely by this local engine.

Related: [settings design](../../planning-artifacts/toolbox/toolbox-review-policy-settings-design.md), [approved matrix](../../planning-artifacts/toolbox/toolbox-compute-compare-checklist-matrix.md#proposed-stage-classification), [decision register](../../planning-artifacts/toolbox/toolbox-decision-register.md).
