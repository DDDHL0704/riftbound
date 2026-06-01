# Stage 4D-17RO Recovery Authoritative Stack Metadata Redaction Sentinel Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint continues the authoritative stack redaction-boundary work from Stage 4D-17RN.

Current runtime facts:

- Authoritative `StackItemState` metadata is internal recovery state, not recovered/spectator view payload text.
- The `HIDDEN` sentinel is reserved for view redaction and must not be persisted as authoritative stack metadata.
- Stage 4D-17RN covered stack identity, controller, source, effect and target object fields.
- This slice covers the remaining stack metadata fields that can carry concrete machine-readable values.

## Runtime Change

`MatchRecoveryValidator` now rejects `HIDDEN` in authoritative stack item metadata fields:

- card number;
- destination;
- timing context;
- optional costs.

The existing string-list validation redaction-sentinel option now also covers stack optional costs. Existing duplicate, blank and whitespace behavior is preserved.

Concrete authoritative stack metadata redaction drift now emits:

```text
authoritative state stack item <stackItemId> card no must not be redacted
authoritative state stack item <stackItemId> destination must not be redacted
authoritative state stack item <stackItemId> timing context must not be redacted
authoritative state stack item <stackItemId> optional cost must not be redacted
```

## Tests

Added coverage:

- `RecoveryValidatorRejectsAuthoritativeStateStackItemMetadataRedactionSentinelDrift`

The test creates an authoritative stack item whose card number, destination, timing context and optional cost all use `HIDDEN`. Validation now emits explicit redaction-sentinel diagnostics for those metadata fields.

## Validation

Passed:

- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsAuthoritativeStateStackItemMetadataRedactionSentinelDrift"` (`1/1`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "StackAndTrigger|TriggerQueue"` (`88/88`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`567/567`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1148/1148`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` (`6513/6513`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
