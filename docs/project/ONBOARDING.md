# ONBOARDING

## What this repo is

Sid-jamaica is an ArcGIS Pro add-in for parcel workflow processing with Innola transaction intake, review-first extraction flow, and local case-folder-based execution artifacts.

## First 10 minutes for new model/engineer

1. Read `docs/INDEX.md`.
2. Read `docs/project/AI_PROJECT_CONTEXT.md`.
3. Read `_bmad-output/planning-artifacts/epics.md` (especially current Epic 2 status).
4. Check `docs/project-management/CURRENT_SPRINT.md`.
5. Check `docs/project-management/DECISIONS.md` for active constraints.
6. Run:
   - `PowerShell -ExecutionPolicy Bypass -File .\tools\run_python_tests.ps1`
   - `dotnet build` checks only after environment readiness
   - `PowerShell -ExecutionPolicy Bypass -File .\tools\package_addin.ps1 -Configuration Debug` (requires ArcGIS Pro SDK + Visual Studio MSBuild path)

### Build recovery note (known environment blocker)

Recent workstation runs can fail in readiness or packaging with this error:

`Access to the path 'C:\Users\<user>\AppData\Local\Microsoft SDKs' is denied.`

If that happens:

1. Open an **elevated PowerShell** (Run as Administrator).
2. Fix permissions for the exact path:

```powershell
$path = "C:\Users\$env:USERNAME\AppData\Local\Microsoft SDKs"
takeown /f $path /r /d y
icacls $path /grant "$($env:USERDOMAIN)\$($env:USERNAME):(OI)(CI)F" /T
```

3. Retry readiness:

```powershell
PowerShell -ExecutionPolicy Bypass -File .\tools\run_arcgis_addin_readiness.ps1 -Configuration Debug
```

If the issue persists, capture the full log from:
`%TEMP%\pe-jamaica-readiness-YYYYMMDD-HHMMSS.log` and escalate before rerunning repeatedly.

## Model/handoff handoff checklist

Before switching AI models or handing off to another developer:

- Ensure `CURRENT_SPRINT.md` has:
  - current objective,
  - done vs backlog,
  - risks.
- Ensure `DECISIONS.md` includes the last decision made this sprint.
- Ensure `LESSONS_LEARNED.md` has the most recent sprint learnings.
- Leave code changes committed with a short summary of acceptance state.
- Use the standardized handoff block format (Objective / Next 3 Actions / Files Changed / Risk / Handoff Instruction).

## Operational defaults to keep stable

- ArcGIS Pro lane: 3.6 first, 3.7 optional.
- Case Folder is system of record; do not rely on hidden ArcGIS project state.
- Keep mock mode and live mode supported.
- Keep secrets out of standard logs and case artifacts.
