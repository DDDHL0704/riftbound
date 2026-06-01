# Stage 4D-17RJ Recovery Timing Trigger Queue Empty Object Registry Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens recovered player-view and spectator replay-frame timing trigger queue validation so a visible timing `triggerQueue[]` source object is rejected even when the relevant object registry is empty.

Current runtime builder facts:

- Recovered snapshot timing `triggerQueue[]` payloads may expose visible source objects that must be present in recovered snapshot object payloads.
- Spectator replay timing `triggerQueue[]` payloads may expose visible source objects that must be present in the authoritative object registry.
- An empty known object set is still a known object set; a visible `sourceObjectId` cannot be accepted just because there are no objects to compare against.
- Hidden trigger sources continue to use `sourceVisibility: "HIDDEN"` with redacted `sourceObjectId` / `effectKind`.

## Runtime Change

`MatchRecoveryValidator` no longer skips visible source-object membership validation when the known object id set is empty. Visible trigger queue source objects now emit the existing missing-object diagnostics against empty recovered/spectator registries:

```text
snapshot for <playerId> timing trigger queue item visible source object id <sourceObjectId> is missing from objects
spectator replay frame timing trigger queue item visible source object id <sourceObjectId> is missing from object registry
```

Existing missing/null, whitespace, duplicate-id, identity-redaction sentinel, controller membership, prompt-field and source/effect redaction diagnostics are preserved. Source membership remains scoped to `sourceVisibility: "VISIBLE"`.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueVisibleSourceObjectWithEmptyObjectRegistry`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceObjectWithEmptyObjectRegistry`

The recovered test uses the default recovered snapshot with no object payloads and mutates timing `triggerQueue[]` to claim `sourceObjectId: "source-1"` with `sourceVisibility: "VISIBLE"`. The spectator test uses an authoritative state with no object registry entries, adds a mismatched spectator trigger queue item that claims `source-1`, and keeps the spectator trigger queue count mismatched so same-payload source membership validation is proven independent of authoritative parity.

## Validation

Passed:

- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "RecoveryValidatorRejectsSnapshotTimingTriggerQueueVisibleSourceObjectWithEmptyObjectRegistry|RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceObjectWithEmptyObjectRegistry"` (`2/2`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "TriggerQueue"` (`83/83`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests"` (`561/561`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` (`1142/1142`)
- `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` (`6507/6507`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
