# Plan B Attack Damage Trigger Source Identity Catalog Source Evidence

日期：2026-06-26
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing the direct source card-number check from the current Sharpshooter Pirate / 神射海盗 attack-damage trigger representative.

## Runtime Evidence

- `CoreRuleEngine.ResolveSharpshooterPirateAttackDamageTrigger(...)` now selects eligible attacking sources through `IsControlledFaceUpFieldUnitWithEffectKind(...)`.
- The shared predicate requires:
  - `CardObjectTags.UnitCard`
  - `IsFaceDown == false`
  - no `CardObjectTags.Standby`
  - `CardBehaviorRegistry.IsImplementedUnitWithEffectKind(attackerState.CardNo, SharpshooterPirateAttackTriggerSourceEffectKind)`
- The source effect kind is `SHARPSHOOTER_PIRATE_ATTACK_TRIGGER_PLAY_UNIT`, which is the registered catalog behavior row for `OGN·130/298`.
- Existing controller and field-location checks remain in `ResolveSharpshooterPirateAttackDamageTrigger(...)`.
- The emitted runtime damage effect remains `SHARPSHOOTER_PIRATE_ATTACK_DAMAGE_1` for event/replay compatibility.

## Test Evidence

Focused test files:

- `tests/Riftbound.ConformanceTests/TriggerSourceIdentityGuardTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesCatalogTriggerSourceUnitsByEffectKind` accepts `OGN·130/298` with `SHARPSHOOTER_PIRATE_ATTACK_TRIGGER_PLAY_UNIT`.
- `CardBehaviorRegistryRejectsNonMatchingCatalogTriggerSourceUnits` rejects Sharpshooter/Ember cross-effect source identity matches.
- `CoreRuleEngineTriggerSourceSelectionUsesCatalogEffectKindIdentity` blocks reintroducing a direct `attackerState.CardNo` comparison against `SharpshooterPirateCardNo` and verifies the runtime path consumes `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`.
- `P79SharpshooterPirateDamagesEnemyUnitWhenAttackingBattlefield` proves the attacking visible Sharpshooter source emits the trigger and deals 1 damage to the same-battlefield enemy defender.
- `P79SharpshooterPirateSkipsAttackDamageWhenDefending` proves the defensive case does not emit the attack-damage trigger.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result: 15/15 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79SharpshooterPirate" --nologo
```

Result: 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SharpshooterPirate|FullyQualifiedName~DeclareBattle|FullyQualifiedName~BattleDamageAssignment|FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result: 238/238 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8652/8652 passed.

## Non-Closure Statement

This evidence does not close complete combat-trigger timing, complete trigger queue ordering, complete TriggerSpec migration for this trigger family, card matrix full-official state, frontend final validation, or READY.
