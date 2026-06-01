# Stage 4D-17RG Recovery Timing Trigger Queue Identity Redaction Sentinel Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens recovered player-view and spectator replay-frame timing trigger queue validation so timing payloads reject the view-redaction sentinel `HIDDEN` in concrete trigger identity, controller and event fields.

Current runtime builder facts:

- Timing `triggerQueue[]` payloads carry concrete `triggerId`, `controllerId` and `triggeredByEventKind` values for every queued trigger.
- Timing `triggerQueue[]` source/effect redaction is scoped to `sourceObjectId`, `sourceVisibility` and `effectKind`.
- The `HIDDEN` sentinel must not replace machine-readable trigger identity, controller or event values.

## Runtime Change

`MatchRecoveryValidator` now emits explicit recovered/spectator timing diagnostics when a trigger queue item uses `HIDDEN` for concrete identity, controller or event values:

```text
snapshot for <playerId> timing trigger queue item trigger id must not be redacted
snapshot for <playerId> timing trigger queue item controller id must not be redacted
snapshot for <playerId> timing trigger queue item triggered event kind must not be redacted
spectator replay frame timing trigger queue item trigger id must not be redacted
spectator replay frame timing trigger queue item controller id must not be redacted
spectator replay frame timing trigger queue item triggered event kind must not be redacted
```

Existing missing/null, whitespace, duplicate-id, prompt-field and source/effect redaction diagnostics are preserved.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueIdentityRedactionSentinelDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueIdentityRedactionSentinelDrift`

The tests build recovered and spectator timing trigger queue payloads with `triggerId: "HIDDEN"`, `controllerId: "HIDDEN"` and `triggeredByEventKind: "HIDDEN"`, then assert the same-payload diagnostics before authoritative parity can skip or obscure identity redaction drift.

## Validation

Passed:

- `dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsSnapshotTimingTriggerQueueIdentityRedactionSentinelDrift|RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueIdentityRedactionSentinelDrift"` (`2/2`)
- `dotnet test Riftbound.slnx --no-restore --filter "TriggerQueue"` (`77/77`)
- `dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`555/555`)
- `dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1136/1136`)
- `dotnet test Riftbound.slnx --no-restore` (`6501/6501`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
