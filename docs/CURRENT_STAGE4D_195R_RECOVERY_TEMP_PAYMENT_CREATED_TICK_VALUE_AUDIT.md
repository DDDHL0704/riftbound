# Stage 4D-195R Recovery Temporary Payment Created Tick Value Audit

Date: 2026-06-08 11:34 CST

Owner: A_MAIN

Main code commit: `32c8fe02` (`test: cover spectator temp payment created tick value`)

Runtime changed: no. This batch added server recovery validation test coverage only.

## Scope

This slice covers spectator replay timing `temporaryPaymentResources[0].createdTick` negative-value validation without relying on a temporary payment resource count mismatch or unrelated field drift.

The new `MatchRecoveryTests` case mutates the single redacted spectator replay temporary payment resource so `createdTick` is `-1` while the authoritative state still has one temporary payment resource created at tick 2.

## Assertions

- Recovery validation emits `spectator replay frame timing temporary payment resource item created tick -1 cannot be negative`.
- Recovery validation emits `spectator replay frame timing temporary payment resource item created tick -1 does not match authoritative state temporary payment resource created tick 2 for resource id temp-payment-resource-1`.
- Recovery validation emits `spectator replay frame timing temporary payment resource created ticks disagree with authoritative state temporary payment resource created ticks`.
- Recovery validation does not emit a `spectator replay frame timing temporary payment resource count` diagnostic.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceCreatedTickValuePayload"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1386/1386`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1391/1391`.
- Backend full conformance project: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7661/7661`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

## Coordination

No subagent and no new worktree were created. DOC_MATRIX_CURRENT was observed clean on branch `codex/stage4d-matrix-docs-current` at `17bde0c3`. Push after the code commit succeeded via SSH.

Project remains **NOT READY**. Frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, final readiness status, broader command/recovery/random determinism and remaining recovery payload breadth are still open.
