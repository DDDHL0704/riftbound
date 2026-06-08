# Stage 4D-197B Recovery Trigger Queue Hidden Redaction Required Fields Audit

Date: 2026-06-08

Owner: A_MAIN

## Scope

Stage 4D-197B adds one server-test shard for spectator recovery replay timing hidden-source `triggerQueue[0]` redaction required-field absence validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceRedactionRequiredFieldAbsenceWithoutCountMismatch`

The test builds an authoritative state with one face-down standby source on a battlefield and a redacted spectator replay frame. It removes `sourceObjectId`, `sourceVisibility` and `effectKind` from the keyed `trigger-hidden` payload while keeping the spectator trigger queue length equal to the authoritative trigger queue length of one.

Assertions prove recovery validation emits:

- Required source-object diagnostic.
- Required source-visibility diagnostic.
- Required effect-kind diagnostic.
- Keyed authoritative source-object mismatch diagnostic under trigger id `trigger-hidden`.
- Keyed authoritative source-visibility mismatch diagnostic under trigger id `trigger-hidden`.
- Keyed authoritative effect-kind mismatch diagnostic under trigger id `trigger-hidden`.
- Aggregate source-object disagreement diagnostic.
- Aggregate source-visibility disagreement diagnostic.
- Aggregate effect-kind disagreement diagnostic.
- No trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceRedactionRequiredFieldAbsenceWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1422/1422`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "Recovery"` -> `1427/1427`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7697/7697`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

Code commit:

- `465fd608 test: cover hidden trigger queue redaction required fields`

Push:

- `git push origin main` succeeded after the code commit via SSH.

## Coordination

A_MAIN created no subagent and no new worktree. DOC_MATRIX_CURRENT was clean at HEAD `17bde0c3` when checked before the docs sync. No DOC_MATRIX handoff or unresolved question was pending.

Project remains **NOT READY**. This slice does not close broader P0/P1 runtime/server closure, command/recovery/random determinism, remaining recovery payload breadth, LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, or final readiness.
