# Plan B LeBlanc Ephemeral Static AbilitySpec Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Official Evidence

- Official catalog row `UNL-090/219` LeBlanc / 乐芙兰 says: `你在我所处战场的{{瞬息}}效果不会触发。`
- Official catalog row `UNL-090a/219` LeBlanc / 乐芙兰 says: `你在我所处战场的{{瞬息}}效果不会触发。`
- No official data file was edited.

## Runtime Evidence

- `RuleTextParser` recognizes LeBlanc's static text and emits `StaticAbilitySpec.Kind=SAME_BATTLEFIELD_EPHEMERAL_TURN_START_SUPPRESSION`.
- `TargetFilter=TAG:瞬息` records the affected lifecycle keyword family in the spec data.
- `BehaviorSpecCatalogBuilder` marks the parsed static ability implemented for the existing LeBlanc behavior rows.
- `CardStaticAbilitySpecRules` consumes the same catalog-backed `BehaviorSpec` map at runtime.
- `CoreRuleEngine` suppresses turn-start Ephemeral cleanup only when the source object remains:
  - on the same battlefield
  - public and face up
  - a unit
  - not standby
  - controlled by the turn player
- `CoreRuleEngine` no longer consumes the LeBlanc play-row effect kinds to recognize this static ability.

## Test Evidence

- `CardCatalogBaselineTests.BehaviorSpecCatalogParsesLeblancEphemeralSuppressionStaticAbility` proves both LeBlanc printings parse to the new static ability kind with `TargetFilter=TAG:瞬息`.
- `CardCatalogBaselineTests.LeblancEphemeralStaticSuppressionDoesNotUseDuplicatedCardNumberAllowList` now blocks the old LeBlanc cardNo helper, direct alt cardNo branch, Core-owned LeBlanc effect-kind constants, and the old `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` selector route.
- `ConformanceFixtureRunnerTests.CoreRuleEngineSuppressesEphemeralTurnStartAtLeblancBattlefield` continues to prove same-battlefield suppression while other Ephemeral objects still clean up normally.
- Adjacent `MatchRecovery` coverage remains green, preserving hidden-info and snapshot recovery boundaries.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~BehaviorSpecCatalogParsesLeblancEphemeralSuppressionStaticAbility|FullyQualifiedName~LeblancEphemeralStaticSuppressionDoesNotUseDuplicatedCardNumberAllowList|FullyQualifiedName~CoreRuleEngineSuppressesEphemeralTurnStartAtLeblancBattlefield"
```

Result: `4/4` passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~Leblanc|FullyQualifiedName~Ephemeral|FullyQualifiedName~StaticAbility|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery"
```

Result: `2387/2387` passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: `9038/9038` passed.

## Non-Closure

This evidence does not close complete Ephemeral replacement / cleanup breadth, complete LeBlanc official behavior, simultaneous lifecycle ordering, complete hidden-info matrix, full official card-matrix readiness, frontend final validation, formal E2E, P0/P1, or READY.
