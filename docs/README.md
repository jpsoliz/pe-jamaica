# Sid-jamaica Project Documentation

Welcome to the Sid-jamaica project workspace.

Use this folder as the primary onboarding and operational documentation source for both AI agents and human developers.

## Start Here

- [docs/INDEX.md](INDEX.md) — quick map of all key project docs
- [docs/toolchain.md](toolchain.md) — build/run prerequisites and packaging workflow
- [docs/project/AI_PROJECT_CONTEXT.md](project/AI_PROJECT_CONTEXT.md) — stable project context and conventions
- [docs/project/ONBOARDING.md](project/ONBOARDING.md) — immediate onboarding checklist for new team/model handoff
- [docs/project-management/CURRENT_SPRINT.md](project-management/CURRENT_SPRINT.md) — current sprint status and next work
- [docs/project-management/DECISIONS.md](project-management/DECISIONS.md)
- [docs/retrospectives/LESSONS_LEARNED.md](retrospectives/LESSONS_LEARNED.md)

## BMAD and Planning Outputs

- Epic plan: [_bmad-output/planning-artifacts/epics.md](../_bmad-output/planning-artifacts/epics.md)
- Architecture: [_bmad-output/planning-artifacts/architecture.md](../_bmad-output/planning-artifacts/architecture.md)
- Story implementations: [_bmad-output/implementation-artifacts/](../_bmad-output/implementation-artifacts/)
- Sprint status: [_bmad-output/implementation-artifacts/sprint-status.yaml](../_bmad-output/implementation-artifacts/sprint-status.yaml)

## Quick norms

- Keep JSON and schema artifacts snake_case (`schema_version`, `transaction_id`, `run_id`).
- ArcGIS Pro workflow decisions live in the add-in code first, not in scripts.
- Preserve current state in the Case Folder; avoid hidden ArcGIS-only dependency for recovery/audit.
