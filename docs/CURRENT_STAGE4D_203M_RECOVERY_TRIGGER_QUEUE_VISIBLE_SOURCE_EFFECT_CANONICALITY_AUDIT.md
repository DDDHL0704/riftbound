# Stage 4D-203M Recovery Trigger Queue Visible Source Effect Canonicality Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203M adds one server-test shard for spectator recovery replay timing trigger queue visible-source `sourceObjectId` plus `effectKind` canonicality validation without a trigger queue count mismatch while aggregate source-object/effect-kind comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceEffectCanonicalityWithoutCountMismatch`

The test builds one authoritative visible trigger queue item keyed as `trigger-visible`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only the spectator `sourceObjectId` and `effectKind` fields to their canonical values with surrounding whitespace.

Assertions prove recovery validation emits:

- Source-object-id and effect-kind surrounding-whitespace diagnostics.
- Keyed authoritative source-object-id and effect-kind mismatch diagnostics for `trigger-visible`.
- Aggregate same-count trigger queue source-object-id and effect-kind disagreement diagnostics.

Assertions also prove this same-count trigger queue visible-source effect canonicality path does not emit:

- Source-visibility aggregate disagreement diagnostics.
- Trigger queue count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceEffectCanonicalityWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1589/1589`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1594/1594`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7864/7864`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `414099f9`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 02:56 CST before docs sync.
- Project remains **NOT READY**.
