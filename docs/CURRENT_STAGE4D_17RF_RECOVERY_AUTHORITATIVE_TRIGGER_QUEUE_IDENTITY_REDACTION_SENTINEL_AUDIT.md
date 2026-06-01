# Stage 4D-17RF Recovery Authoritative Trigger Queue Identity Redaction Sentinel Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens authoritative-state trigger queue validation so the server recovery validator rejects the view-redaction sentinel `HIDDEN` in concrete trigger identity, controller and event fields.

Current runtime builder facts:

- Authoritative trigger queue state carries concrete `triggerId`, `controllerId` and `triggeredByEventKind` values for every queued trigger.
- Recovered player-view and spectator timing payloads may redact visible source/effect fields with `HIDDEN`.
- The `HIDDEN` sentinel is a view payload redaction value, not an authoritative trigger identity, controller or event value.

## Runtime Change

`MatchRecoveryValidator` now emits explicit authoritative-state diagnostics when a trigger queue item uses `HIDDEN` for concrete authoritative identity, controller or event values:

```text
authoritative state trigger queue item id must not be redacted
authoritative state trigger queue item <triggerId> controller player must not be redacted
authoritative state trigger queue item <triggerId> triggered event kind must not be redacted
```

Existing null, empty, whitespace, duplicate-id and seated-player diagnostics are preserved.

## Tests

Added coverage:

- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueIdentityRedactionSentinelDrift`

The test builds an authoritative trigger queue item with `triggerId: "HIDDEN"`, `controllerId: "HIDDEN"` and `triggeredByEventKind: "HIDDEN"`, then asserts all three redaction-sentinel diagnostics before authoritative-state recovery consumers can accept view-redacted trigger identity or event state.

## Validation

Passed:

- `dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsAuthoritativeStateTriggerQueueIdentityRedactionSentinelDrift"` (`1/1`)
- `dotnet test Riftbound.slnx --no-restore --filter "TriggerQueue"` (`75/75`)
- `dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`553/553`)
- `dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1134/1134`)
- `dotnet test Riftbound.slnx --no-restore` (`6499/6499`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
