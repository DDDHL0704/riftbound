# Plan B Unit Last-Breath Create Dormant Gold TriggerSpec Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records evidence for routing the existing Honest Broker last-breath create-Gold representative through `BehaviorSpec.Triggers` instead of a `CoreRuleEngine` local card-number / behavior object branch.

## 1. Official Rule Evidence

- Official catalog entry `SFD·155/221`: “{{绝念}} — 打出一个休眠的“金币”装备指示物。（当我被摧毁后，发动此效果。）”
- Official Gold token entries in `data/official/card-catalog.zh-CN.json` identify “金币” as an equipment token with reaction resource text.

No official data file was edited.

## 2. Runtime Evidence

- `RuleTextParser` parses Honest Broker last-breath Gold text into `TriggerSpec` with:
  - `Kind=HONEST_BROKER_LAST_BREATH_CREATE_GOLD` via `TriggerKinds.UnitLastBreathCreateDormantGold`
  - `Timing=UNIT_DESTROYED`
  - `TargetScope=SOURCE_UNIT`
  - `CreatedTokenCount=1`
  - `CreatedTokenName=金币`
  - `CreatedTokenDestination=OWNER_BASE`
  - `CreatedTokenExhausted=true`
  - `CreatedTokenKeywords=[反应]`
- `UnitDestroyedTriggerSpecRules.TryGetTrigger(..., IsLastBreathCreateDormantGoldTrigger, ...)` exposes the parsed trigger shape to the shared engine.
- `CoreRuleEngine` ordered-trigger stack resolution and single-trigger immediate resolution both call `CreateBaseEquipmentTokensFromTrigger(...)`.
- `CoreRuleEngine` no longer defines `HonestBrokerCardNo`, `HonestBrokerLastBreathCreateGoldEffectKind`, or `HonestBrokerLastBreathCreateGoldBehavior`.
- Runtime-created Honest Broker Gold tokens now carry `[CARD_TYPE:EQUIPMENT, 反应, 金币]` tags while preserving the existing exhausted base-equipment token object shape and public event kind.

## 3. Test Evidence

Focused test files:

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

Coverage:

- `BehaviorSpecCatalogParsesUnitLastBreathCreateDormantGoldTrigger` proves the official Honest Broker entry produces the expected `TriggerSpec` row.
- `UnitLastBreathCreateDormantGoldTriggerDoesNotUseCoreCardNumberBehavior` blocks reintroducing the old Core local card-number / behavior branch.
- Existing Honest Broker trigger queue tests prove APNAP ordering, ordered trigger stack resolution and token creation remain green.
- Fixture runner coverage proves the preflight Honest Broker fixture now observes the spec-defined Gold token tags.

## 4. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesUnitLastBreathCreateDormantGoldTrigger|FullyQualifiedName~UnitLastBreathCreateDormantGoldTriggerDoesNotUseCoreCardNumberBehavior" --nologo
```

Result: 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~HonestBroker|FullyQualifiedName~LastBreath|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~TriggerSourceIdentityGuard|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~ConformanceFixtureRunner" --nologo
```

Result: 5471/5471 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8817/8817 passed.

DevUi build was not rerun because this slice did not change DevUi source or shared catalog TypeScript payload shape.

## 5. Non-Closure Statement

This evidence does not close complete last-breath trigger timing, complete trigger queue ordering, complete Gold token resource lifecycle, token factory cardNo assignment, card matrix full-official state, frontend final validation, formal E2E or READY.
