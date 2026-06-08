# Stage 4D-196K Recovery Trigger Queue Required Fields Audit

Date: 2026-06-08

Owner: A_MAIN

## Scope

Stage 4D-196K adds one server-test shard for spectator recovery replay timing `triggerQueue[0]` required-field validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedRequiredFieldAbsenceWithoutCountMismatch`

The test builds an authoritative state with one visible trigger and a redacted spectator replay frame with one trigger. It removes `sourceObjectId` and `sourceVisibility`, changes `effectKind` to a non-string value, and changes `triggeredByEventKind` to a non-string value while keeping the spectator trigger queue length equal to the authoritative trigger queue length.

Assertions prove recovery validation emits:

- Required-field diagnostics for source object id, source visibility, effect kind and triggered event kind.
- Keyed authoritative mismatch diagnostics for the same four fields under trigger id `trigger-visible`.
- Aggregate trigger-queue field disagreement diagnostics for source object ids, source visibilities, effect kinds and triggered event kinds.
- No trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedRequiredFieldAbsenceWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1405/1405`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1410/1410`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7680/7680`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

Code commit:

- `1b72193b test: cover spectator trigger queue required fields`

Push:

- `git push origin main` succeeded after the code commit via SSH.

## Coordination

A_MAIN created no subagent and no new worktree. DOC_MATRIX_CURRENT was clean at HEAD `17bde0c3` when checked before the docs sync. No DOC_MATRIX handoff or unresolved question was pending.

Project remains **NOT READY**. This slice does not close broader P0/P1 runtime/server closure, command/recovery/random determinism, remaining recovery payload breadth, LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, or final readiness.
