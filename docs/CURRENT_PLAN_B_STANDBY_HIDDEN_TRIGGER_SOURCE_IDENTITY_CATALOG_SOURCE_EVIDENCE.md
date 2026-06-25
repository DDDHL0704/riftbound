# Plan B Standby Hidden Trigger Source Identity Catalog Source Evidence

日期：2026-06-26
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing the direct source card-number check from the current Ember Monk / 余火修士 standby-hidden trigger representative.

## Runtime Evidence

- `CoreRuleEngine.ResolveEmberMonkStandbyHiddenPowerTrigger(...)` now selects eligible sources through `IsControlledFaceUpFieldUnitWithEffectKind(...)`.
- The shared predicate requires:
  - `CardObjectTags.UnitCard`
  - `IsFaceDown == false`
  - no `CardObjectTags.Standby`
  - `CardBehaviorRegistry.IsImplementedUnitWithEffectKind(sourceState.CardNo, EmberMonkStandbyTriggerSourceEffectKind)`
- The source effect kind is `EMBER_MONK_STANDBY_TRIGGER_PLAY_UNIT`, which is the registered catalog behavior row for `OGN·167/298`.
- Existing controller and field-location checks remain in `ResolveEmberMonkStandbyHiddenPowerTrigger(...)`.
- The emitted runtime effect remains `EMBER_MONK_FACE_DOWN_STANDBY_POWER_2` for event/replay compatibility.

## Test Evidence

Focused test files:

- `tests/Riftbound.ConformanceTests/TriggerSourceIdentityGuardTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesCatalogTriggerSourceUnitsByEffectKind` accepts `OGN·167/298` with `EMBER_MONK_STANDBY_TRIGGER_PLAY_UNIT`.
- `CardBehaviorRegistryRejectsNonMatchingCatalogTriggerSourceUnits` rejects non-Ember and cross-effect source identity matches.
- `CoreRuleEngineTriggerSourceSelectionUsesCatalogEffectKindIdentity` blocks reintroducing a direct `sourceState.CardNo` comparison against `EmberMonkCardNo` and verifies the runtime path consumes `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`.
- `P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden` proves the visible friendly Ember Monk source gains +2 until end of turn when its controller hides a standby card, while face-down, standby, and opposing Ember Monk objects do not receive the modifier.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result: 12/12 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden" --nologo
```

Result: 1/1 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EmberMonk|FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result: 14/14 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8649/8649 passed.

## Non-Closure Statement

This evidence does not close complete standby-hidden trigger timing, complete trigger queue ordering, complete TriggerSpec migration for this trigger family, card matrix full-official state, frontend final validation, or READY.
