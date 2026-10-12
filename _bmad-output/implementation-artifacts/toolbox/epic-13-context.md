# Epic 13 Context: Transaction Case Toolbox

## Goal

Compose existing examination tools over a versioned case workspace, preserving single/multi-parcel evidence and current/source authority while keeping operational cases untouched.

## Stories

- 13.1: Process and task matrix.
- 13.2: Architecture and migration.
- 13.3: Case context/workspace selection.
- 13.4: Parcel model/reconciliation.
- 13.5: Versioned persistence, including C prototype disk revisions.
- 13.6: Reusable capabilities.
- 13.7: Compute preset.
- 13.8: Compare/corrections.
- 13.9: Revision-consistent handoff.
- 13.10: PLA/RT presets.

## Requirements & Constraints

Preserve full case identity, original raw/manual/reviewed evidence, histories and stale approval semantics. Geometry permission stays distinct from examination forwarding. No selected-parcel acceptance bypass, source PE writes or inferred server authority. Experimental work stays in the isolated branch/worktree. User authorized C.1-C.3 implementation together; later import/schema/host stories remain gated. Documentation work already present is retained.

## Technical Decisions

C is an ordinary-file local NTFS prototype, not FGDB authority. A's accepted byte codec and schema remain unchanged. Strict separate disk metadata, expected-parent concurrency, immutable revision payloads, journal/pointer publication, independently verified historical retry and explicit recovery/abandonment are required. Use handle-contained filesystem operations and exclusive process lock; unsafe capabilities fail closed. No automatic cleanup, merge, live service or power-loss durability claim.

## Cross-Story Dependencies

C.1 supplies pure records/validation/planner; C.2 platform capability proof precedes C.3 integration; C.3 executes save/open/retry/recover/abandon. User authorizes completing all three sequentially, with implementation review/verification at each boundary. Accepted A and in-memory model are reused; D/E/B and production gates remain open.
