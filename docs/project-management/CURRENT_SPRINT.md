# CURRENT_SPRINT

**Last Updated:** 2026-10-11

**Sprint Owner:** Sidwell / ArcGIS Pro Parcel Workflow Add-in Team

**Current Focus:** Epic 13 Transaction Case Toolbox, developed separately from the operational add-in

## Tracking And Branches

This file is a navigation and current-focus summary. Story statuses belong to the trackers below; it is not a third tracker.

| Track | Branch / Workspace | Status Source | Delivery Context |
|---|---|---|---|
| Operational add-in | `wipRC9`, main checkout | [Operational sprint tracker](../../_bmad-output/implementation-artifacts/sprint-status.yaml) | [Sprint reconciliation](../../_bmad-output/implementation-artifacts/sprint-reconciliation-2026-10-08.md) |
| Transaction Case Toolbox | `refactor/toolbox-case-workspace`, `temp/toolbox-worktree` | [Active branch Toolbox tracker](../../temp/toolbox-worktree/_bmad-output/implementation-artifacts/toolbox/sprint-status.yaml) | [Active branch Epic 13 plan](../../temp/toolbox-worktree/_bmad-output/planning-artifacts/toolbox/epic-13-toolbox-delivery-plan.md) |

When running BMAD workflows for Toolbox, explicitly pass `_bmad-output/implementation-artifacts/toolbox/sprint-status.yaml` and the exact Toolbox story path. The default tracker is operational. Toolbox completion does not update operational statuses or imply a merge, installation, or deployment.

Run Toolbox workflows from `temp/toolbox-worktree` so those relative paths resolve to the active branch. Main-checkout Toolbox files are older planning snapshots. As of checkpoint `0a6188f`, the isolated branch has progressed through 13.4A and 13.4B implementation; this distinction corrects the earlier main-checkout-only summary. Links into `temp/toolbox-worktree` assume the existing local worktree remains present.

## Toolbox Progress

| Item | Recorded Status | Scope |
|---|---|---|
| 13.1 | Done | Process definition, task matrix, scenarios, and decisions |
| 13.2 | Done | Architecture, contracts, isolation, and migration plan |
| 13.3 | Done for its scoped deliverable | Local case context and workspace selection; no production UI or remote actions |
| Policy follow-up | Local implementation verified; no production integration | Compute/Compare policy loader and applicability resolver; does not complete 13.4A |
| 13.4A | Accepted/done for isolated scope | Stable in-memory identities/memberships, inclusion, human review, findings and local acceptance |
| 13.4B | Accepted/done for isolated in-memory scope | Explicit acceptance 2026-10-11; eight findings fixed; clean build and 135 focused passes |
| Parent 13.4 | Done for M2 | A+B accepted; no persistence or production GIS acceptance implied |
| 13.5 | Prepared; backlog | Detailed persistence parent; schema/ownership review and FGDB recovery/edit proof gates remain |
| 13.5A | Review; isolated implementation complete | No-writer byte codec and lossless hydration; clean build, 23 new groups, 158 focused passes; tracked through parent |
| 13.6-13.10 | Backlog | Reusable tools, presets, handoff, and annexation/RT composition |

The policy follow-up is documented in [its implementation record](../../_bmad-output/implementation-artifacts/toolbox/toolbox-policy-contract-review-and-implementation.md). Final 2026-10-11 correction verification: build passed with zero warnings/errors; 135 focused tests passed, including ten new regression groups and legacy review/approval compatibility. Full harness passed 207 tests before the known Compare XAML assertion; later tests did not execute. Full-suite green is not claimed.

## Where To Resume

The 11.2/13.4A ownership and local identity dependency is already resolved in the active branch through its accepted mapped amendment. See [verified ownership and identity mapping](../../temp/toolbox-worktree/_bmad-output/planning-artifacts/toolbox/toolbox-contract-ownership-and-identity-mapping.md) and [13.4A evidence](../../temp/toolbox-worktree/_bmad-output/implementation-artifacts/toolbox/13-4a-implementation-evidence.md). Original operational 11.2 remains open; this is an isolated adaptation, not operational contract migration.

1. Use the prepared [13.5 persistence story](../../temp/toolbox-worktree/_bmad-output/implementation-artifacts/toolbox/13-5-add-versioned-toolbox-workspace-persistence.md). B and parent 13.4 are accepted for isolated M2; [correction/acceptance evidence](../../temp/toolbox-worktree/_bmad-output/implementation-artifacts/toolbox/13-4b-review-fixes-2026-10-11.md) retains the verification limits.
2. Review/accept [13.5A's isolated implementation](../../temp/toolbox-worktree/_bmad-output/implementation-artifacts/toolbox/13-5a-add-storage-contract-and-hydration-foundation.md) using its [verification evidence](../../temp/toolbox-worktree/_bmad-output/implementation-artifacts/toolbox/13-5a-implementation-evidence.md): final clean build and 158 focused passes. Full harness again stopped after 207 passes at the known Compare XAML assertion; new appended groups ran in the focused lane, not after that failure.
3. After A acceptance, refine remaining disk/import/schema work and resolve ownership/path/writer gates. Prove FGDB recovery, edit detection and consistent snapshots before B authority activation. A cannot complete the full parent or authorize a production switch. Default boundary point arrays retain the documented pre-existing domain-hash limitation.

Production applicability/effect bindings, persistence, and tool/preset integration remain future work. The active [decision register](../../temp/toolbox-worktree/_bmad-output/planning-artifacts/toolbox/toolbox-decision-register.md) retains the domain and service gates.

## Validation Gaps And Deferred Work

- Full-harness failures were reproduced on the operational baseline: a Compare UI progress-binding assertion and intermittent manifest replacement access denial under the default temporary directory.
- Focused Toolbox verification does not establish live ArcGIS, FGDB persistence, publication, or Innola handoff acceptance.
- Future persistence must verify filesystem/reparse-point containment before writes; the 13.3 context checks are lexical only.

Details: [Active branch deferred work](../../temp/toolbox-worktree/_bmad-output/implementation-artifacts/toolbox/deferred-work.md). The operational tracker was last updated on 2026-09-13 and may be stale; use the reconciliation record and story evidence when assessing it. This summary does not reclassify its entries.

## Separate Product Discovery

The [Standalone Document Intelligence V1 specification](../../_bmad-output/planning-artifacts/standalone-document-intelligence/standalone-document-intelligence-v1-specification.md) is a separate product proposal covering text/titling and spatial extraction, independent of ESRI/Innola. It is not currently part of either sprint and does not change Epic 13 scope.
