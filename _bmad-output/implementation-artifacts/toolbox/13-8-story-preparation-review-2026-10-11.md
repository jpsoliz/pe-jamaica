# 13.8 Compare Story Preparation Review

Date: 2026-10-11
Scope: parent and three child specifications under continuing documentation-only/no-coding instruction.
Result: parent and three child specifications prepared and statically reviewed; separate read-only reviewer found no substantive contradictions or missing completion handoffs. Runtime authorization false, code tasks unchecked, parent/children backlog, no new tracker keys.

## Specifications And Handoffs

| Story | Owns | Runtime boundary |
|---|---|---|
| [13.8 parent](13-8-compose-read-only-compare-and-correction-flow.md) | Compare authority/process/owner contract | Requirement source, not deployed workflow |
| [.1 input/evidence](13-8-1-define-revision-bound-geometry-and-comparison-evidence.md) | Immutable local-first geometry/reference and source-qualified query results | Accepted owner/Compute/capability seams and separately authorized reads |
| [.2 workspace/partial review](13-8-2-define-compare-workspace-tabular-correction-and-partial-review.md) | WPF review, field-allowlisted current-case corrections, partial Save/Reopen | .1 and affected owner/policy mapping; .3 command interface only |
| [.3 decisions/Compute loop](13-8-3-define-compare-decisions-and-compute-correction-loop.md) | Full-case final decision and controlled saved correction return/recomparison | .1/.2/Compute accepted plus exact decision/coverage mapping |

.2 can use an interface/mock for later .3 commands; partial-save implementation belongs to .2 and does not wait for final decision code. Geometry correction uses 13.7's accepted owner/permission seams; local switch is not server return/transition. 13.9 retains attachments/publish/package/server handoff.

## Source-Based Review

Inspected existing CompareReviewDecision records/persistence/readiness, CompareWorkingGeometryService, CompareEvidenceModels, query service interfaces/statuses and viewmodel command call anchors, together with approved checklist C11-C15 and 13.6/13.7 specifications.

- Existing geometry loading plans transaction-scoped Enterprise layers, not a verified immutable Toolbox host revision. New input must qualify owner/revision; no newest file/Enterprise fallback.
- Existing query evidence distinguishes ready/no-record/mismatch/ambiguous/unavailable/unsupported and legal/fiscal sources. Preserve these semantics, explicit multiple parties/pairs and actual supported source field/mode mapping.
- Legacy decision values approved/blocked/returned_to_compute/saved_progress and ordinary JSON readiness are not a complete revision-qualified Toolbox final/pending model. Exact owner outcome mapping is required; no blocked -> rejected or progress -> complete conversion.
- CompareWorkspaceViewModel includes save/report generation, uploads and task completion/suspend/cancel. Full call-path read is required before reuse; local Toolbox review must not invoke these lifecycle wrappers implicitly.

## Requirement Coverage

| Confirmed obligation | Specification coverage |
|---|---|
| Read-only current/reference geometry and exact saved input | Parent AC1/3; .1 AC1-2; .2 AC2 |
| Local current-case tabular correction, raw/manual review preserved | Parent AC3-4; .2 AC2-3 |
| Partial required outage/evidence review preserved with pending reason | Parent AC2/5/6; .1 AC5; .2 AC4-5; .3 AC1 |
| Legal versus fiscal provenance, multiple party/pair scopes | Parent contract/AC2; .1 AC3; .2 AC1 |
| Source-defect inspection/complete rejection distinct from geometry approval | Parent boundary/AC6; .1 AC1; .3 AC2 |
| Exact stale-input/query/decision invalidation and historical retention | Parent AC4/7; .1 AC5; .2 AC3/6; .3 AC5-6 |
| No A schema/enum widening or dual writer for pending/outcome state | Parent AC5/6; .2 AC5; .3 AC3 |
| Saved local return to fresh-authorized Compute and new-revision recompare | Parent AC7; .3 AC4-5 |
| No implicit server lifecycle or handoff effect | Parent boundary; .1/.2 scope; .3 AC7 |

## Review Disposition

A separate read-only reviewer checked all four specifications against 13.6/13.7 and approved C11-C15 semantics. No substantive contradictions or missing completion handoffs were reported. Checks included reference geometry versus tabular field allowlist, required pending comparison/final rejection separation, external-read authority versus write denial, noncyclic .2/.3 command seam, durable coverage/outcome admission, actual task neutrality and stale evidence after Compute return. This review does not pass runtime/UI/host/policy/owner acceptance gates.

## Documentation Checks And Remaining Evidence

Check required parent/child sections, local links/whitespace, false authorization, unchecked runtime tasks and unchanged sprint keys/statuses; synchronize Epic13/context. Exact external-read effect/target/mode/bounds, field taxonomy/current tabular permission, durable comparison/outcome/coverage mapping, WPF UPDATE targets and host/query fixtures remain future reviewed deliverables. Static specifications cannot prove actual read-only host enforcement or Save/Reopen/recomparison.

No C#/XAML/harness changes, external query, source read, map load/edit, owner save, decision/server effect, runtime build/tests, installation, commit or deployment by preparation. Baseline158/known full207-stop at Compare XAML/default-PointIds remain historical limitations. Prototype receipts cannot certify host acceptance; scoped Compare acceptance cannot close 13.9/production/operational11.2.
