# Stage 4D-196X Recovery Trigger Queue Hidden Identity Shape Audit

Date: 2026-06-08

Owner: A_MAIN

## Scope

Stage 4D-196X adds one server-test shard for spectator recovery replay timing hidden-source `triggerQueue[0]` identity required-field shape validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceIdentityRequiredFieldShapeWithoutCountMismatch`

The test builds an authoritative state with one face-down standby source on a battlefield and a redacted spectator replay frame. It changes `controllerId` and `triggeredByEventKind` on the keyed `trigger-hidden` payload to array-shaped values while keeping the spectator trigger queue length equal to the authoritative trigger queue length of one.

Assertions prove recovery validation emits:

- Required controller-id diagnostic.
- Required triggered-event-kind diagnostic.
- Keyed authoritative controller-id mismatch diagnostic under trigger id `trigger-hidden`.
- Keyed authoritative triggered-event-kind mismatch diagnostic under trigger id `trigger-hidden`.
- Aggregate controller-id disagreement diagnostic.
- Aggregate triggered-event-kind disagreement diagnostic.
- No trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceIdentityRequiredFieldShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1418/1418`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1423/1423`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7693/7693`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

Code commit:

- `14a55572 test: cover hidden trigger queue identity shape`

Push:

- `git push origin main` succeeded after the code commit via SSH.

## Coordination

A_MAIN created no subagent and no new worktree. DOC_MATRIX_CURRENT was clean at HEAD `17bde0c3` when checked before the docs sync. No DOC_MATRIX handoff or unresolved question was pending.

Project remains **NOT READY**. This slice does not close broader P0/P1 runtime/server closure, command/recovery/random determinism, remaining recovery payload breadth, LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, or final readiness.
