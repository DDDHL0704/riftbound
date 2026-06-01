# Stage 4D-17QY Recovery Timing Continuous-Effect Static-Aura Source/Target Audit

Date: 2026-06-01

Owner: A_MAIN

## Scope

This checkpoint tightens recovery-frame validation for known valid-scope object `STATIC_AURA` continuous-effect payloads.

Current runtime builder facts:

- Friendly-equipment object static auras are emitted by `MatchState.ContinuousEffects` / `CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute` with `scope=OBJECT`, `duration=WHILE_SOURCE_ON_PUBLIC_FIELD`, and `targetObjectId == sourceObjectId`.
- Battlefield all-units static auras are emitted with `scope=BATTLEFIELD`, a battlefield source object, and a participant target object. They are intentionally outside the object source-target identity check.

## Runtime Change

`MatchRecoveryValidator` now rejects recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item payloads when all of these are true:

- `layer` is `STATIC_AURA`.
- `scope` is `OBJECT`.
- `duration` is `WHILE_SOURCE_ON_PUBLIC_FIELD`.
- Both readable `targetObjectId` and readable `sourceObjectId` are present.
- `targetObjectId` and `sourceObjectId` differ.

The diagnostic is explicit:

```text
... object static aura target object id <target> must match source object id <source>
```

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingContinuousEffectStaticAuraObjectSourceTargetConsistencyDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraObjectSourceTargetConsistencyDrift`

The spectator test keeps the same count-mismatch guard used by adjacent recovery slices: authoritative continuous effects are empty, the spectator frame carries one malformed object static aura, and same-payload validation still emits the source-target diagnostic alongside the count mismatch.

## Validation

Passed:

- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter StaticAuraObjectSourceTarget --no-restore` (`2/2`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter ContinuousEffectStaticAura --no-restore` (`34/34`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests --no-restore` (`542/542`)
- `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests" --no-restore` (`1123/1123`)
- `dotnet test Riftbound.slnx --no-restore` (`6488/6488`)
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Open

This is a P1-004 recovery/replay determinism slice only. It does not close broader command/recovery/random determinism, remaining recovered/spectator nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
