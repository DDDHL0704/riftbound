# Stage 4D-198E Recovery Trigger Queue Redaction Consistency Audit

Date: 2026-06-08

Owner: A_MAIN

## Scope

Stage 4D-198E adds one server-test shard for spectator recovery replay timing trigger-queue redaction consistency validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueRedactionConsistencyWithoutCountMismatch`

The test builds an authoritative state with one visible trigger source and one face-down standby hidden trigger source. It starts from the redacted spectator replay frame, keeps the spectator trigger queue length equal to the authoritative trigger queue length of two, then forges the visible trigger as hidden by changing `sourceObjectId` and `effectKind` to `HIDDEN` while also exposing the hidden trigger by changing `sourceObjectId` to the hidden object id and `effectKind` to `AMBUSH_REVEALED`.

Assertions prove recovery validation emits:

- Hidden source-object redaction-required diagnostic.
- Hidden effect-kind redaction-required diagnostic.
- Visible source-object must-not-be-redacted diagnostic.
- Visible effect-kind must-not-be-redacted diagnostic.
- Keyed authoritative source-object mismatch diagnostics for both `trigger-visible` and `trigger-hidden`.
- Keyed authoritative effect-kind mismatch diagnostics for both `trigger-visible` and `trigger-hidden`.
- Aggregate source-object-id disagreement diagnostic.
- Aggregate effect-kind disagreement diagnostic.
- No trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueRedactionConsistencyWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1451/1451`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1456/1456`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7726/7726`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

Code commit:

- `ff20a0b5 test: cover trigger queue redaction consistency`

Push:

- `git push origin main` succeeded after the code commit via SSH.

## Coordination

A_MAIN created no subagent and no new worktree. DOC_MATRIX_CURRENT was clean at HEAD `17bde0c3` when checked before the docs sync at `2026-06-08 20:59 CST`. No DOC_MATRIX handoff or unresolved question was pending.

Project remains **NOT READY**. This slice does not close broader P0/P1 runtime/server closure, command/recovery/random determinism, remaining recovery payload breadth, LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, or final readiness.
