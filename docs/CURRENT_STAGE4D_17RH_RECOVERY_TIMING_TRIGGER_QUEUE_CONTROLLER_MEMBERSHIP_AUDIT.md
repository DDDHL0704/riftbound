# Stage 4D-17RH Recovery Timing Trigger Queue Controller Membership Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens recovered player-view and spectator replay-frame timing trigger queue validation so every timing `triggerQueue[]` item with a readable `controllerId` must point at a known player.

Current runtime builder facts:

- Recovered snapshot timing `triggerQueue[]` payloads carry concrete `controllerId` values that should name a player present in the snapshot `players` map.
- Spectator replay timing `triggerQueue[]` payloads carry concrete `controllerId` values that should name an authoritative seated player.
- Same-payload trigger queue validation must continue even when spectator trigger queue count parity fails and authoritative item-by-item comparison is skipped.

## Runtime Change

`MatchRecoveryValidator` now emits explicit recovered/spectator timing diagnostics when a trigger queue item names a controller outside the relevant player set:

```text
snapshot for <playerId> timing trigger queue item controller id <controllerId> is missing from players
spectator replay frame timing trigger queue item controller id <controllerId> is missing from seats
```

Existing missing/null, whitespace, duplicate-id, identity-redaction sentinel, prompt-field and source/effect redaction diagnostics are preserved.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueControllerMembershipDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueControllerMembershipWithCountMismatch`

The recovered test mutates snapshot timing `triggerQueue[]` to use `controllerId: "charlie"` while the snapshot players are `alice` and `bob`. The spectator test adds a trigger queue item with `controllerId: "charlie"` while authoritative seats are `alice` and `bob`, and keeps the spectator trigger queue count mismatched so same-payload membership validation is proven independent of authoritative parity.

## Validation

Passed:

- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter "FullyQualifiedName~RecoveryValidatorRejectsSnapshotTimingTriggerQueueControllerMembershipDrift|FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueControllerMembershipWithCountMismatch"` (`2/2`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "TriggerQueue"` (`79/79`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`557/557`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1138/1138`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` (`6503/6503`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
