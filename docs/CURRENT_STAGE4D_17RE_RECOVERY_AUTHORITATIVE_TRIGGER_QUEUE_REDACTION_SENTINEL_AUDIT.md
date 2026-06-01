# Stage 4D-17RE Recovery Authoritative Trigger Queue Redaction Sentinel Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens authoritative-state trigger queue validation so the server recovery validator rejects authoritative trigger queue items that carry the view-redaction sentinel `HIDDEN` in concrete authoritative fields.

Current runtime builder facts:

- Authoritative trigger queue state carries concrete `sourceObjectId` and `effectKind` values for every queued trigger.
- Recovered player-view and spectator timing payloads may redact visible trigger source/effect fields with `HIDDEN`.
- The `HIDDEN` sentinel is a view payload redaction value, not an authoritative-state value.

## Runtime Change

`MatchRecoveryValidator` now emits explicit authoritative-state diagnostics when a trigger queue item uses `HIDDEN` for concrete authoritative source object or effect kind values:

```text
authoritative state trigger queue item <triggerId> source object must not be redacted
authoritative state trigger queue item <triggerId> effect kind must not be redacted
```

Existing null, empty and whitespace diagnostics are preserved.

## Tests

Added coverage:

- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueRedactionSentinelDrift`

The test builds an authoritative trigger queue item with `sourceObjectId: "HIDDEN"` and `effectKind: "HIDDEN"`, then asserts both redaction-sentinel diagnostics before authoritative-state recovery consumers can accept view-redacted trigger queue state.

## Validation

Passed:

- `dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsAuthoritativeStateTriggerQueueRedactionSentinelDrift"` (`1/1`)
- `dotnet test Riftbound.slnx --no-restore --filter "TriggerQueue"` (`74/74`)
- `dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`552/552`)
- `dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1133/1133`)
- `dotnet test Riftbound.slnx --no-restore` (`6498/6498`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

One earlier parallel `TriggerQueue` run failed with an MSBuild file-lock artifact while another test process was building; the serial `TriggerQueue` rerun passed `74/74`.

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
