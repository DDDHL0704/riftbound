# Stage 4D-195Q Recovery Temporary Payment Allowed Payment Kind Value Audit

Date: 2026-06-08 11:24 CST

Owner: A_MAIN

Main code commit: `7fbfb7f0` (`test: cover spectator temp payment kind value drift`)

Runtime changed: no. This batch added server recovery validation test coverage only.

## Scope

This slice covers spectator replay timing `temporaryPaymentResources[0].allowedPaymentKinds` value validation without relying on a temporary payment resource count mismatch or unrelated field drift.

The new `MatchRecoveryTests` case mutates the single redacted spectator replay temporary payment resource so `allowedPaymentKinds` is `[RUNE_COST, " RUNE_COST ", WRONG_PAYMENT_KIND, ""]` while the authoritative state still has one temporary payment resource with allowed payment kind `RUNE_COST`.

## Assertions

- Recovery validation emits `spectator replay frame timing temporary payment resource item allowed payment kind RUNE_COST has surrounding whitespace`.
- Recovery validation emits `spectator replay frame timing temporary payment resource item allowed payment kind RUNE_COST is duplicated`.
- Recovery validation emits `spectator replay frame timing temporary payment resource item allowed payment kind is required`.
- Recovery validation emits `spectator replay frame timing temporary payment resource item allowed payment kinds do not match authoritative state temporary payment resource allowed payment kinds for resource id temp-payment-resource-1`.
- Recovery validation emits `spectator replay frame timing temporary payment resource allowed payment kinds disagree with authoritative state temporary payment resource allowed payment kinds`.
- Recovery validation does not emit a `spectator replay frame timing temporary payment resource count` diagnostic.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceAllowedPaymentKindListValuePayload"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1385/1385`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1390/1390`.
- Backend full conformance project: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7660/7660`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

## Coordination

No subagent and no new worktree were created. DOC_MATRIX_CURRENT was observed clean on branch `codex/stage4d-matrix-docs-current` at `17bde0c3`. Push after the code commit succeeded via SSH.

Project remains **NOT READY**. Frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, final readiness status, broader command/recovery/random determinism and remaining recovery payload breadth are still open.
