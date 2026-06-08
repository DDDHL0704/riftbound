# Stage 4D-196M Recovery Trigger Queue Canonical Values Audit

Date: 2026-06-08

Owner: A_MAIN

## Scope

Stage 4D-196M adds one server-test shard for spectator recovery replay timing `triggerQueue[0]` known-value canonicality validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedKnownValueCanonicalityWithoutCountMismatch`

The test builds an authoritative state with one visible trigger and a redacted spectator replay frame with one trigger. It keeps the trigger id stable and changes `controllerId`, `sourceObjectId`, `sourceVisibility`, `effectKind` and `triggeredByEventKind` to whitespace-padded authoritative values while keeping the spectator trigger queue length equal to the authoritative trigger queue length.

Assertions prove recovery validation emits:

- Surrounding-whitespace canonicality diagnostics for controller id, source object id, source visibility, effect kind and triggered event kind.
- Keyed authoritative mismatch diagnostics for the same five fields under trigger id `trigger-visible`.
- Aggregate trigger-queue field disagreement diagnostics for controller ids, source object ids, source visibilities, effect kinds and triggered event kinds.
- No trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedKnownValueCanonicalityWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1407/1407`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1412/1412`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7682/7682`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

Code commit:

- `f3edaf3e test: cover spectator trigger queue canonical values`

Push:

- `git push origin main` succeeded after the code commit via SSH.

## Coordination

A_MAIN created no subagent and no new worktree. DOC_MATRIX_CURRENT was clean at HEAD `17bde0c3` when checked before the docs sync. No DOC_MATRIX handoff or unresolved question was pending.

Project remains **NOT READY**. This slice does not close broader P0/P1 runtime/server closure, command/recovery/random determinism, remaining recovery payload breadth, LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, or final readiness.
