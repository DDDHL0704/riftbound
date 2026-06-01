# Stage 4D-17RQ Recovery Authoritative Pending Hand Choice Redaction Sentinel Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint continues the authoritative pending-workflow redaction-boundary work from Stage 4D-17RP.

Current runtime facts:

- Authoritative `PendingHandChoiceState` is internal recovery state, not recovered/spectator view payload text.
- The `HIDDEN` sentinel is reserved for view redaction and must not be persisted as authoritative pending-hand-choice data.
- Stage 4D-17RP covered pending-payment identity, player, window, reason and action-list values.
- This slice covers pending-hand-choice identity, player, window, reason, source/effect metadata and legal object ids.

## Runtime Change

`MatchRecoveryValidator` now rejects `HIDDEN` in authoritative pending-hand-choice fields:

- choice id;
- player id;
- choice window;
- reason;
- source object id;
- effect kind;
- legal object ids.

The existing authoritative string-list validation redaction-sentinel option now also covers pending-hand-choice legal object ids. Existing duplicate, blank, whitespace, count and object-reference behavior is preserved.

Concrete authoritative pending-hand-choice redaction drift now emits:

```text
authoritative state pending hand choice id must not be redacted
authoritative state pending hand choice <choiceId> player must not be redacted
authoritative state pending hand choice <choiceId> window must not be redacted
authoritative state pending hand choice <choiceId> reason must not be redacted
authoritative state pending hand choice <choiceId> source object must not be redacted
authoritative state pending hand choice <choiceId> effect kind must not be redacted
authoritative state pending hand choice <choiceId> legal object must not be redacted
```

## Tests

Added coverage:

- `RecoveryValidatorRejectsAuthoritativeStatePendingHandChoiceRedactionSentinelDrift`

The test creates an authoritative pending hand choice whose identity, player, window, reason, source object, effect kind and legal object id all use `HIDDEN`. Validation now emits explicit redaction-sentinel diagnostics for those pending-hand-choice fields.

## Validation

Passed:

- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsAuthoritativeStatePendingHandChoiceRedactionSentinelDrift"` (`1/1`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "PendingHandChoice|PendingPayment"` (`23/23`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`569/569`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1150/1150`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` (`6515/6515`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
