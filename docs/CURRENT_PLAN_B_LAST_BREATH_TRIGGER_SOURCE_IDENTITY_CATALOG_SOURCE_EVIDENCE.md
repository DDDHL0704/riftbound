# Plan B Last-Breath Trigger Source Identity Catalog Source Evidence

日期：2026-06-26
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing direct source card-number checks from the current last-breath trigger representatives for Kogmaw, Undercover Agent, Honest Broker, and Unsung Hero.

## Runtime Evidence

- `CoreRuleEngine.IsFaceUpNonStandbyUnitWithEffectKind(...)` is the shared source predicate for this slice.
- The predicate requires:
  - `CardObjectTags.UnitCard`
  - `IsFaceDown == false`
  - no `CardObjectTags.Standby`
  - `CardBehaviorRegistry.IsImplementedUnitWithEffectKind(sourceState.CardNo, sourceEffectKind)`
- `ResolveKogmawLastBreathAoePlayerId(...)` now uses source effect kind `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT`.
- The original 2026-06-26 slice routed Undercover Agent, Honest Broker, and Unsung Hero source identity through catalog effect-kind rows; later 2026-06-27 follow-ups migrated their executable shapes to `BehaviorSpec.Triggers`.
- Undercover Agent now resolves through `TriggerSpec.Kind=UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT`.
- Honest Broker now resolves through `TriggerSpec.Kind=HONEST_BROKER_LAST_BREATH_CREATE_GOLD`.
- Unsung Hero now resolves through `TriggerSpec.Kind=UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2`.

2026-06-27 follow-up: Honest Broker create-Gold token shape now also routes through `BehaviorSpec.Triggers`; see `docs/CURRENT_PLAN_B_UNIT_LAST_BREATH_CREATE_DORMANT_GOLD_TRIGGER_SPEC_EVIDENCE.md`.

2026-06-27 follow-up: Undercover Agent last-breath discard/draw source and count shape now also routes through `BehaviorSpec.Triggers`; see `docs/CURRENT_PLAN_B_UNIT_LAST_BREATH_DISCARD_DRAW_TRIGGER_SPEC_EVIDENCE.md`.

2026-06-27 follow-up: Unsung Hero last-breath powerful-draw source, threshold and draw-count shape now also routes through `BehaviorSpec.Triggers`; see `docs/CURRENT_PLAN_B_UNIT_LAST_BREATH_POWERFUL_DRAW_TRIGGER_SPEC_EVIDENCE.md`.

## Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/TriggerSourceIdentityGuardTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesCatalogTriggerSourceUnitsByEffectKind` accepts the four registered last-breath source rows used by this slice.
- `CardBehaviorRegistryRejectsNonMatchingCatalogTriggerSourceUnits` rejects cross-effect source identity matches for these rows.
- `CoreRuleEngineTriggerSourceSelectionUsesCatalogEffectKindIdentity` blocks reintroducing direct `destroyedState.CardNo` comparisons for `KogmawCardNo`, `UndercoverAgentCardNo`, `HonestBrokerCardNo`, and `UnsungHeroCardNo`.
- The same guard verifies the shared helper consumes `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result: 23/23 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Kogmaw|FullyQualifiedName~UndercoverAgent|FullyQualifiedName~HonestBroker|FullyQualifiedName~UnsungHero|FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result: 111/111 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~UndercoverAgentTriggerTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2058/2058 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8660/8660 passed.

## Non-Closure Statement

This evidence does not close complete last-breath trigger timing, complete trigger queue ordering, complete effective-power / LayerEngine powerful checks, card matrix full-official state, frontend final validation, or READY. Honest Broker create-Gold, Undercover Agent discard/draw and Unsung Hero powerful-draw shapes are covered by the 2026-06-27 follow-up evidence.
