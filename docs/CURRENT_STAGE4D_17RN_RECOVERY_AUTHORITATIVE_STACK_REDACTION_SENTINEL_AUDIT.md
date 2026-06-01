# Stage 4D-17RN Recovery Authoritative Stack Redaction Sentinel Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens authoritative-state stack item validation for view-redaction sentinel leakage.

Current runtime facts:

- Authoritative `StackItemState` entries are internal recovery state, not recovered/spectator view payloads.
- The `HIDDEN` sentinel is reserved for view redaction, such as recovered/spectator trigger source visibility.
- Authoritative trigger queue validation already rejects `HIDDEN` in concrete identity, controller, source, effect and event fields.
- Authoritative stack items must likewise preserve concrete machine-readable identity, controller, source/effect and target object references when those fields are present.

## Runtime Change

`MatchRecoveryValidator` now rejects `HIDDEN` in authoritative stack item concrete fields:

- stack item id;
- controller player id;
- source object id;
- effect kind;
- target object ids.

The string-list validation helper now has an opt-in redaction-sentinel check used for stack target object ids only in this slice. Existing duplicate and whitespace behavior is preserved.

Concrete authoritative stack item redaction drift now emits:

```text
authoritative state stack item id must not be redacted
authoritative state stack item <stackItemId> controller player must not be redacted
authoritative state stack item <stackItemId> source object must not be redacted
authoritative state stack item <stackItemId> effect kind must not be redacted
authoritative state stack item <stackItemId> target object must not be redacted
```

## Tests

Added coverage:

- `RecoveryValidatorRejectsAuthoritativeStateStackItemRedactionSentinelDrift`

The test creates an authoritative stack item whose id, controller, source object, effect kind and target object id all use `HIDDEN`. Validation now emits explicit redaction-sentinel diagnostics instead of relying only on later missing-seat or missing-object-registry checks.

## Validation

Passed:

- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsAuthoritativeStateStackItemRedactionSentinelDrift"` (`1/1`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "StackAndTrigger|TriggerQueue"` (`88/88`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`566/566`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1147/1147`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` (`6512/6512`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
