# Toolbox Deferred Work

## Deferred From: Story 13.3 Code Review (2026-10-08)

- Existing CompareWorkspaceXamlTests.CompareWorkspaceExposesTaskLifecycleCommands expects IsFinalizeOperationRunning/FinalizeOperationRunningText bindings, but baseline XAML uses IsLoading/StatusText for its shared indeterminate progress indicator. Reproduced at baseline 5908f30. Review the intended UI contract in the operational test-maintenance track; do not modify the current UI as part of local Toolbox context work. Full-suite green is not claimed by 13.3 completion.
- Existing ManifestSerializer.File.Move replacement intermittently raises UnauthorizedAccessException under the default system temp directory, also reproduced on baseline. Process-only TEMP/TMP redirection into the isolated worktree mitigates this for validation; investigate separately rather than change production persistence in 13.3.
- Future 13.5 persistence must enforce canonical filesystem/reparse-point containment before writes. 13.3 performs lexical context validation only and explicitly makes no symlink/junction-safe write claim.

## Observed During: Story 13.5A Verification (2026-10-11)

- The existing domain `ToolboxSemanticHash.Of` default JSON serializer throws for a default `ImmutableArray<string>` in boundary PointIds. The byte codec preserves default/null vs explicit empty and proves both original/restored domain hash calls retain this limitation. Resolve default-array domain evaluation in a separate reviewed change before approval/export uses such state; do not silently normalize historical input or change accepted hashes in storage hydration. See [13.5A evidence](13-5a-implementation-evidence.md).
- Final 13.5A build clean, 158 focused passes; full harness again stops after 207 passes at the unchanged Compare XAML assertion above. Appended storage tests execute in the focused lane; full-suite green and later-test execution are not claimed.
