# 13.5D/E/B Story Preparation Review

Date: 2026-10-11
Scope: prepare/refine and statically review the next D, E and B stories under the continuing explicit no-coding instruction.
Result: nine specifications complete. Runtime stories/parents remain backlog, authorization false and tasks unchecked; no new tracker keys or feasibility/production gates closed. Exact mapper, schema catalog/profile and activation UPDATE list are future reviewed deliverables, not created/approved by these specifications.

## Ordered Specifications

| Area | Ordered stories | Runtime evidence required |
|---|---|---|
| D copied import | [D.1 freeze/mapping/plan](13-5d-1-define-source-freeze-mapping-and-import-plan.md) -> [D.2 sealed preparation](13-5d-2-define-sealed-import-preparation.md) -> [D.3 execute/retry](13-5d-3-define-import-execution-and-retry.md) | Accepted C, reviewed mapper, immutable source bytes, sealed request and exact C receipt |
| E schema/projection | [E.1 exact catalog/profile](13-5e-1-define-exact-fgdb-catalog-and-geometry-profile.md) -> [E.2 pure projection/read-back](13-5e-2-define-pure-projection-and-readback-verification.md) -> [E.3 hosted adapter](13-5e-3-define-isolated-hosted-schema-adapter.md) | Actual named schema/profile review, manifest coverage, headless and real disposable FGDB read-back |
| B final hosted activation | [B.1 commit/recovery](13-5b-1-define-host-commit-and-recovery-protocol.md) -> [B.2 edits/snapshots](13-5b-2-define-edit-detection-and-supported-snapshot-proofs.md) -> [B.3 activation/rollback](13-5b-3-define-isolated-activation-and-rollback.md) | Accepted C/E and D when importing, real F01/F02/F03, policy/guard review, explicit host/activation authority and F07 before installation |

Parent contracts [D](13-5d-add-read-only-copied-case-import.md), [E](13-5e-define-fgdb-schema-and-projection-mapping.md) and [B](13-5b-add-recoverable-fgdb-commits-and-isolated-activation.md) remain requirement sources. Overall order C -> D/E -> B is preserved. E.1 catalog authoring can occur alongside C/D when separately requested; all runtime/host scopes still require explicit authorization and applicable gates.

## Contract Review Clarifications

A read-only reviewer examined the original parent contracts while the specifications were drafted. Five substantive handoffs were clarified in both parent requirements and children:

1. D.2 pins target store_id with the exact C request before target creation. D.3 distinguishes fresh Create, exact owned target resume and unrepaired initialization_incomplete. If accepted C Create cannot bind the prepared ID, integration is blocked rather than changing the request.
2. D's 1 MiB limit covers the entire serialized plan, including transformations/maps/observations/diagnostics. Mandatory sidecars count toward C asset bounds. Separate artifact provenance can reference the same exact byte asset, but C receives a unique digest inventory and full collision checks. Entire preparation/request budgets are checked before sealing.
3. E separates A domain/model/archive IDs, C immutable source provenance and host authority. Preliminary host-context fields are E.1-owned; row planners never claim a committed host result.
4. B.1 requires a minimal supported, independently read-back previous-state baseline before F01 mutation, including an explicit first empty/synthetic baseline. This is not full F03; B.2 owns complete edit/asset/copy/restore proof.
5. B uses immutable historical snapshot/ancestry/catalog/inventory proof for receipt retries after successors. Current authority is separate from original digest-bound actor/permission audit; forward recovery publication needs fresh authority and complete actual content. Missing publication tail is explicit recovery-required, not falsely committed.

## Final Specification Review And Resolution

A second read-only reviewer checked all nine child specifications against parent guarantees and their dependencies. One issue was found: E.1 originally required B.1 host-contract freeze before E.3 integration, creating a cycle because B.1 required E acceptance.

Corrected E.1 and E parent: E.1 defines/reviews the preliminary host-context contract used by E.2/E.3 synthetic fixtures without a B dependency or commit claim. After E acceptance, B.1 finalizes its protocol against those fields. Incompatible changes trigger E re-review before affected B integration. B.1's accepted-E prerequisite remains intact. No other substantive issues were reported by that reviewer; no unresolved specification finding remains from these reviews. The cycle correction was checked against E.3/B.1 entry gates.

## Acceptance And Ownership Coverage

| Parent requirements | Responsible specification |
|---|---|
| D AC1/3/4/5: frozen source, strict identity, mode/mapping, legacy observations | D.1 |
| D AC2/4/6: sealed request, source loss, incomplete preparation | D.2 |
| D AC2/5/6/7: exact execution/retry, unchanged sources, review/acceptance | D.3 with D.1/D.2 evidence |
| E AC1/3: exact ownership/catalog/profile and conversion rules | E.1 |
| E AC1/2/3/5/6: pure coverage/round-trip and unsupported values | E.2 |
| E AC4/5/6/7: actual hosted schema/geometry and named acceptance | E.3 |
| B AC1/2/3/6: host protocol/F01/permission/retry | B.1 |
| B AC2/4/5: F02 actual edits and F03 supported complete snapshot/restore | B.2 |
| B AC7/8: guard coverage, isolated activation/rollback and proof acceptance | B.3, preserving predecessor evidence |

Reviewed existing A evidence/manifests, copied-case layout/manifest source, accepted identity mapping, architecture acceptance strategy and named decision gates. Checked Esri's [Pro 3.6 field types](https://pro.arcgis.com/en/pro-app/3.6/help/data/geodatabases/overview/arcgis-field-data-types.htm) and [Copy tool](https://pro.arcgis.com/en/pro-app/3.6/tool-reference/data-management/copy.htm). E keeps full Int64 owners lossless despite host-safe numeric limits; Copy documentation is not actual F03 proof. These references support constraints, not installed-host behavior or catalog acceptance.

## Documentation Validation And Limits

Checks cover nine expected files, required sections, explicit no-runtime authorization, unchecked implementation tasks, local references, whitespace and unchanged sprint keys/statuses. Parent 13.5, D/E/B parent story tables, sprint comments, Epic 13 plan and project continuation notes are synchronized. No code/source/test harness changes, source capture, preparation seal, exact catalog generation, GDB fixture, host experiment, installation, commit or deployment performed.

Baseline 158 focused passes and full-harness stop after 207 at known Compare XAML remain historical, not rerun results. Default-PointIds stays an inspection limitation; no normalization/domain-hash fix. D01/D06/D11/F01/F02/F03/F07 and operational 11.2 gates remain open. Exact host commands/mechanisms and activation update files must be enumerated and reviewed during separately authorized execution, with actual pure/host evidence recorded separately.
