# Plan B Unit Last-Breath Powerful Draw TriggerSpec Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `SFD·167/221` 无名英雄 `{{绝念}} — 如果我为{{强力}}单位，则抽两张牌` 的既有代表性结算，从 `CoreRuleEngine` 本地 Unsung Hero 专用常量迁移到官方文本解析出的 `BehaviorSpec.Triggers`。该切片只收窄 Unsung Hero last-breath powerful-draw 的 source / threshold / draw count 来源；不关闭完整 effective-power / LayerEngine 强力矩阵、完整绝念家族、完整 `ORDER_TRIGGERS` / APNAP、Kogmaw TriggerSpec migration、P0 full objective 或 READY。

## 1. Scope

Changed:

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.Engine/UnitDestroyedTriggerSpecRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `docs/CURRENT_PLAN_B_UNIT_LAST_BREATH_POWERFUL_DRAW_TRIGGER_SPEC_AUDIT.md`
- `docs/CURRENT_PLAN_B_UNIT_LAST_BREATH_POWERFUL_DRAW_TRIGGER_SPEC_EVIDENCE.md`
- `docs/CURRENT_PLAN_B_LAST_BREATH_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_LAST_BREATH_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- trigger queue / `ORDER_TRIGGERS` protocol shape
- public effect string `UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2`
- recovery validator compatibility constants
- frontend runtime
- card matrix full-official status

## 2. Official Inputs

- `data/official/card-catalog.zh-CN.json`: `SFD·167/221` contains `{{绝念}} — 如果我为{{强力}}单位，则抽两张牌。（当我被摧毁后，发动此效果。战力达到5或以上时，即为强力单位。）`
- Existing rules evidence treats `强力` as power 5 or more for current representative paths.

## 3. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Unsung Hero last-breath powerful draw text is represented as data | `RuleTextParser` emits `TriggerKinds.UnitLastBreathPowerfulDraw`, `Timing=UNIT_DESTROYED`, `TargetScope=SOURCE_UNIT`, `DrawCount=2`, `RequiredPowerThreshold=5` | Accepted |
| Runtime source selection reads TriggerSpec | `ResolveUnsungHeroLastBreathDrawPlayerId(...)` now uses `UnitDestroyedTriggerSpecRules.TryGetLastBreathPowerfulDrawTrigger(...)` plus source unit / face-up / non-standby checks | Accepted |
| Runtime draw count reads TriggerSpec | immediate and stack last-breath draw paths derive draw count from `TriggerSpec.DrawCount` with compatibility fallback | Accepted |
| Public wire compatibility is preserved | `TriggerKinds.UnitLastBreathPowerfulDraw` keeps the existing effect string `UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2`; existing recovery and trigger tests remain green | Accepted |
| Core no longer owns Unsung local constants | `UnsungHeroCardNo`, `UnsungHeroLastBreathSourceEffectKind`, and `UnsungHeroLastBreathPowerfulDrawEffectKind` are absent from `CoreRuleEngine`; the guard test blocks reintroduction | Accepted |
| Full official breadth | complete effective-power / LayerEngine threshold matrix, complete last-breath family, APNAP ordering and Kogmaw TriggerSpec migration remain residual | Residual, no full-official claim |

## 4. Verification

Initial focused TDD guard failed before implementation because `TriggerKinds.UnitLastBreathPowerfulDraw` did not exist. After implementation:

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

## 5. Residual Risks

- This still uses current `CardObjectState.Power` as the representative powerful check; complete effective-power / LayerEngine threshold integration remains open.
- This does not migrate Kogmaw's battlefield-context AoE last-breath trigger to full TriggerSpec shape.
- This does not implement complete simultaneous last-breath ordering or optional-trigger policy.
- This does not close card matrix full-official, frontend final validation, formal E2E or READY.
