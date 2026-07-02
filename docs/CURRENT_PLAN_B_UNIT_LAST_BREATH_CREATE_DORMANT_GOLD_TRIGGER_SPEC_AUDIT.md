# Plan B Unit Last-Breath Create Dormant Gold TriggerSpec Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `SFD·155/221` 诚实掮客 `{{绝念}} — 打出一个休眠的“金币”装备指示物` 的既有代表性结算，从 `CoreRuleEngine` 本地 `HonestBrokerCardNo` / `HonestBrokerLastBreathCreateGoldBehavior` 迁移到官方文本解析出的 `BehaviorSpec.Triggers`。该切片只收窄 Honest Broker last-breath create-Gold 的 source / token shape 来源，不关闭完整绝念家族、完整 `ORDER_TRIGGERS` / APNAP、完整 token factory/cardNo selection、P0 full objective 或 READY。

## 1. Scope

Changed:

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.Engine/UnitDestroyedTriggerSpecRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `docs/CURRENT_PLAN_B_UNIT_LAST_BREATH_CREATE_DORMANT_GOLD_TRIGGER_SPEC_AUDIT.md`
- `docs/CURRENT_PLAN_B_UNIT_LAST_BREATH_CREATE_DORMANT_GOLD_TRIGGER_SPEC_EVIDENCE.md`
- `docs/CURRENT_PLAN_B_LAST_BREATH_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_LAST_BREATH_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- trigger queue / `ORDER_TRIGGERS` protocol shape
- public effect string `HONEST_BROKER_LAST_BREATH_CREATE_GOLD`
- recovery validator source-card constants
- full token factory / token card-number assignment
- card matrix full-official status

## 2. Official Inputs

- `data/official/card-catalog.zh-CN.json`: `SFD·155/221` contains “{{绝念}} — 打出一个休眠的“金币”装备指示物。（当我被摧毁后，发动此效果。）”
- `data/official/card-catalog.zh-CN.json`: official Gold token entries identify “金币” as an equipment token with reaction text.

## 3. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Honest Broker last-breath Gold text is represented as data | `RuleTextParser` emits `TriggerKinds.UnitLastBreathCreateDormantGold`, `Timing=UNIT_DESTROYED`, `TargetScope=SOURCE_UNIT`, `CreatedTokenCount=1`, `CreatedTokenName=金币`, `CreatedTokenDestination=OWNER_BASE`, `CreatedTokenExhausted=true`, `CreatedTokenKeywords=[反应]` | Accepted |
| Runtime token creation reads `TriggerSpec` | `CoreRuleEngine` now resolves ordered and immediate Honest Broker trigger paths through `UnitDestroyedTriggerSpecRules.TryGetTrigger(..., IsLastBreathCreateDormantGoldTrigger, ...)` and `CreateBaseEquipmentTokensFromTrigger(...)` | Accepted |
| Core no longer owns Honest Broker local token behavior | `HonestBrokerCardNo`, `HonestBrokerLastBreathCreateGoldEffectKind`, and `HonestBrokerLastBreathCreateGoldBehavior` were removed from `CoreRuleEngine`; the guard test blocks reintroduction | Accepted |
| Existing trigger queue behavior remains intact | Honest Broker trigger queue / ordering / stack resolution representatives remain green | Accepted |
| Gold token tags now match the parsed token identity | runtime created token tags now include `CARD_TYPE:EQUIPMENT`, `金币`, and `反应`; existing representative assertions were updated accordingly | Accepted |
| Full official breadth | complete last-breath family, simultaneous trigger policy, token factory cardNo selection, and full Gold token resource lifecycle remain residual | Residual, no full-official claim |

## 4. Verification

Initial focused TDD guard failed before implementation because `TriggerKinds.UnitLastBreathCreateDormantGold` did not exist. After implementation:

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

## 5. Residual Risks

- This does not migrate Kogmaw, Undercover Agent, or Unsung Hero representative effect payloads to full `TriggerSpec` shape.
- This does not implement complete simultaneous last-breath ordering beyond the existing representative queue.
- This does not assign a concrete Gold token `cardNo`; existing token object identity shape is preserved except for the now-official `金币` / `反应` tags.
- This does not close complete Gold token resource lifecycle, complete token factory taxonomy, full official last-breath breadth, card matrix full-official, frontend final validation, formal E2E or READY.
