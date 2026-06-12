# CURRENT_SPRINT

**Last Updated:** 2026-06-12 (handoff checkpoint)  
**Sprint Owner:** Sidwell / ArcGIS Pro Parcel Workflow Add-in Team  
**Current Sprint Theme:** Epic 2 (continued stabilization)

## 1) Objective

Keep the live + mock transaction workflow stable while preparing Epic 2.12 to wire `CreateParcelFromFile.py` through the script-plan path.

## 2) Done / Backlog

- **Done**
  - Transaction load, login/session gating, rule profile resolution, manifest persistence, and preflight planning are in place.
  - Live queue behavior and certificate-backed authentication decisions are stabilized.
- **Backlog**
  - Story 2.12: implement execution adapter for extraction plan steps.
  - Story 2.13: extract/inspect artifact-driven stage transitions (Extraction Review / Validation / Output).
  - Story 2.14: remove script-side hardcoded secrets and finalize execution configuration UX.

## 3) Next Actions

1. Build and verify Story 2.12: run `CreateParcelFromFile.py --review-data` from adapter once preflight passes and write `working/extraction_review_data.json`.
2. Keep preflight gate semantics unchanged; extraction actions should only be enabled from valid states.
3. Record execution evidence: manifest `script_plan`, generated INI, and artifact existence in case folder.

## 4) Active Risk

- ArcGIS Python execution path and external INI handling are the two main risk points: wrong env path, stale/unsafe credentials, or missing generated review inputs can block progress.
