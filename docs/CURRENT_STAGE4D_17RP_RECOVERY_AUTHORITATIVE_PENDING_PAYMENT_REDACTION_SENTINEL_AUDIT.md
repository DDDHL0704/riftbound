# Stage 4D-17RP Recovery Authoritative Pending Payment Redaction Sentinel Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint continues the authoritative recovery redaction-boundary work after Stage 4D-17RO.

Current runtime facts:

- Authoritative `PendingPaymentState` is internal recovery state, not recovered/spectator view payload text.
- The `HIDDEN` sentinel is reserved for view redaction and must not be persisted as authoritative pending-payment data.
- Prior slices covered authoritative trigger-queue and stack redaction boundaries.
- This slice covers pending-payment identity, player, window, reason and action-list values.

## Runtime Change

`MatchRecoveryValidator` now rejects `HIDDEN` in authoritative pending-payment fields:

- payment id;
- player id;
- payment window;
- reason;
- legal payment choice ids;
- payment resource action ids.

The existing authoritative string-list validation redaction-sentinel option now also covers pending-payment legal payment choices and payment resource actions. Existing duplicate, blank and whitespace behavior is preserved.

Concrete authoritative pending-payment redaction drift now emits:

```text
authoritative state pending payment id must not be redacted
authoritative state pending payment <paymentId> player must not be redacted
authoritative state pending payment <paymentId> window must not be redacted
authoritative state pending payment <paymentId> reason must not be redacted
authoritative state pending payment <paymentId> legal payment choice must not be redacted
authoritative state pending payment <paymentId> payment resource action must not be redacted
```

## Tests

Added coverage:

- `RecoveryValidatorRejectsAuthoritativeStatePendingPaymentRedactionSentinelDrift`

The test creates an authoritative pending payment whose identity, player, window, reason, legal payment choice and payment resource action all use `HIDDEN`. Validation now emits explicit redaction-sentinel diagnostics for those pending-payment fields.

## Validation

Passed:

- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsAuthoritativeStatePendingPaymentRedactionSentinelDrift"` (`1/1`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "PendingPayment|PaymentResource"` (`100/100`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`568/568`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1149/1149`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` (`6514/6514`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
