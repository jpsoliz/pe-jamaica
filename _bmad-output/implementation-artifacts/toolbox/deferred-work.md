# Toolbox Deferred Work

## Deferred From: Story 13.3 Code Review (2026-10-08)

- Existing CompareWorkspaceXamlTests.CompareWorkspaceExposesTaskLifecycleCommands expects IsFinalizeOperationRunning/FinalizeOperationRunningText bindings, but baseline XAML uses IsLoading/StatusText for its shared indeterminate progress indicator. Reproduced at baseline 5908f30. Review the intended UI contract in the operational test-maintenance track; do not modify the current UI as part of local Toolbox context work. Full-suite green is not claimed by 13.3 completion.
- Existing ManifestSerializer.File.Move replacement intermittently raises UnauthorizedAccessException under the default system temp directory, also reproduced on baseline. Process-only TEMP/TMP redirection into the isolated worktree mitigates this for validation; investigate separately rather than change production persistence in 13.3.
- Future 13.5 persistence must enforce canonical filesystem/reparse-point containment before writes. 13.3 performs lexical context validation only and explicitly makes no symlink/junction-safe write claim.
