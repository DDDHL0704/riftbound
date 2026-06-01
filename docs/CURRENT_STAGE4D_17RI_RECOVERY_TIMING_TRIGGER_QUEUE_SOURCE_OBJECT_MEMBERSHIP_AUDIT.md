# Stage 4D-17RI Recovery Timing Trigger Queue Source Object Membership Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens recovered player-view and spectator replay-frame timing trigger queue validation so every visible timing `triggerQueue[]` source object must point at a known object.

Current runtime builder facts:

- Recovered snapshot timing `triggerQueue[]` payloads may expose visible source objects that should be present in the recovered snapshot object payloads.
- Spectator replay timing `triggerQueue[]` payloads may expose visible source objects that should be present in the authoritative object registry.
- Hidden trigger sources continue to use `sourceVisibility: "HIDDEN"` with redacted `sourceObjectId` / `effectKind`.
- Same-payload trigger queue validation must continue even when spectator trigger queue count parity fails and authoritative item-by-item comparison is skipped.

## Runtime Change

`MatchRecoveryValidator` now emits explicit recovered/spectator timing diagnostics when a trigger queue item claims a visible source object outside the relevant object set:

```text
snapshot for <playerId> timing trigger queue item visible source object id <sourceObjectId> is missing from objects
spectator replay frame timing trigger queue item visible source object id <sourceObjectId> is missing from object registry
```

Existing missing/null, whitespace, duplicate-id, identity-redaction sentinel, controller membership, prompt-field and source/effect redaction diagnostics are preserved. Source membership is checked only for `sourceVisibility: "VISIBLE"` and only when a known object set is available.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueVisibleSourceObjectMembershipDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceObjectMembershipWithCountMismatch`

The recovered test seeds a snapshot object set with `source-1`, then mutates timing `triggerQueue[]` to claim `sourceObjectId: "missing-source"` with `sourceVisibility: "VISIBLE"`. The spectator test seeds authoritative object registry state with `source-1`, adds a mismatched spectator trigger queue item that claims `missing-source`, and keeps the spectator trigger queue count mismatched so same-payload source membership validation is proven independent of authoritative parity.

## Validation

Passed:

- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter "FullyQualifiedName~RecoveryValidatorRejectsSnapshotTimingTriggerQueueVisibleSourceObjectMembershipDrift|FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceObjectMembershipWithCountMismatch"` (`2/2`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "TriggerQueue"` (`81/81`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`559/559`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1140/1140`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` (`6505/6505`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
