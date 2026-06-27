# Plan B Unit Last-Breath Powerful Draw TriggerSpec Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records evidence for routing the existing Unsung Hero last-breath powerful-draw representative through `BehaviorSpec.Triggers` instead of `CoreRuleEngine` local Unsung constants.

## 1. Official Rule Evidence

- Official catalog entry `SFD·167/221`: `{{绝念}} — 如果我为{{强力}}单位，则抽两张牌。（当我被摧毁后，发动此效果。战力达到5或以上时，即为强力单位。）`
- Existing Plan B evidence uses power 5 or more as the current representative `强力` threshold.

No official data file was edited.

## 2. Runtime Evidence

- `RuleTextParser` parses Unsung Hero last-breath powerful draw text into `TriggerSpec` with:
  - `Kind=UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2` via `TriggerKinds.UnitLastBreathPowerfulDraw`
  - `Timing=UNIT_DESTROYED`
  - `TargetScope=SOURCE_UNIT`
  - `DrawCount=2`
  - `RequiredPowerThreshold=5`
- `UnitDestroyedTriggerSpecRules.TryGetLastBreathPowerfulDrawTrigger(...)` exposes the parsed trigger shape to the shared engine.
- `ResolveUnsungHeroLastBreathDrawPlayerId(...)` now accepts a destroyed source through the parsed TriggerSpec plus the shared visible unit boundary.
- Immediate and stack resolution paths read draw count from TriggerSpec while preserving the existing trigger queue and event shape.
- `CoreRuleEngine` no longer defines `UnsungHeroCardNo`, `UnsungHeroLastBreathSourceEffectKind`, or `UnsungHeroLastBreathPowerfulDrawEffectKind`.
- The public effect string remains `UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2`, so existing recovery and replay validators remain compatible.

## 3. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

Coverage:

- `BehaviorSpecCatalogParsesUnitLastBreathPowerfulDrawTrigger` proves the official Unsung Hero entry produces the expected `TriggerSpec` row.
- `UnitLastBreathPowerfulDrawTriggerDoesNotUseCoreCardNumberBehavior` blocks reintroducing the old Core local card-number / effect constant branch.
- Existing Unsung Hero tests prove powerful and below-powerful trigger behavior remains green.
- Existing RealTriggerQueue and MatchRecovery representatives prove trigger queue / stack / spectator validation compatibility remains green.

## 4. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesUnitLastBreathPowerfulDrawTrigger|FullyQualifiedName~UnitLastBreathPowerfulDrawTriggerDoesNotUseCoreCardNumberBehavior" --nologo
```

Result: 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnsungHero|FullyQualifiedName~LastBreath|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~TriggerSourceIdentityGuard|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~ConformanceFixtureRunner" --nologo
```

Result: 5475/5475 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8821/8821 passed.

DevUi build was not rerun because this slice did not change DevUi source or shared TypeScript catalog payload shape.

## 5. Non-Closure Statement

This evidence does not close complete effective-power / LayerEngine powerful checks, complete last-breath trigger timing, complete trigger queue ordering, complete AoE damage matrix, card matrix full-official state, frontend final validation, or READY.
