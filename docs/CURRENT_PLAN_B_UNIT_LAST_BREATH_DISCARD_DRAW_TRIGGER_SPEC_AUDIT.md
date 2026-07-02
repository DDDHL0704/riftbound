# Plan B Unit Last-Breath Discard-Draw TriggerSpec Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `OGN·178/298` 卧底特工 `{{绝念}}—弃置两张手牌，然后抽两张牌` 的既有代表性结算，从 `CoreRuleEngine` 本地 Undercover 专用 effect 常量迁移到官方文本解析出的 `BehaviorSpec.Triggers`。该切片只收窄 Undercover Agent last-breath discard/draw 的 source / count 来源；不关闭完整 hand-choice family、完整绝念家族、完整 `ORDER_TRIGGERS` / APNAP、Kogmaw / Unsung Hero TriggerSpec migration、P0 full objective 或 READY。

## 1. Scope

Changed:

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.Engine/UnitDestroyedTriggerSpecRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `docs/CURRENT_PLAN_B_UNIT_LAST_BREATH_DISCARD_DRAW_TRIGGER_SPEC_AUDIT.md`
- `docs/CURRENT_PLAN_B_UNIT_LAST_BREATH_DISCARD_DRAW_TRIGGER_SPEC_EVIDENCE.md`
- `docs/CURRENT_PLAN_B_LAST_BREATH_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_LAST_BREATH_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- prompt protocol shape
- public effect string `UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT`
- recovery validator compatibility constants
- Undercover Agent hand-choice command contract
- frontend runtime
- card matrix full-official status

## 2. Official Inputs

- `data/official/card-catalog.zh-CN.json`: `OGN·178/298` contains `{{绝念}}—弃置两张手牌，然后抽两张牌。（当我被摧毁后，发动此效果。）`
- `CORE-260330` p52-p55 rules 383.3.d-383.3.e: destroyed unit triggers enter the trigger flow.
- `CORE-260330` p62 rule 422.4: when an effect discards cards, the player discards the maximum possible count; prior Undercover hand-choice evidence covers shortfall behavior.

## 3. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Undercover Agent last-breath discard/draw text is represented as data | `RuleTextParser` emits `TriggerKinds.UnitLastBreathDiscardDraw`, `Timing=UNIT_DESTROYED`, `TargetScope=SOURCE_UNIT`, `DiscardCount=2`, `DrawCount=2` | Accepted |
| Runtime source selection reads TriggerSpec | `ResolveUndercoverAgentLastBreathPlayerId(...)` now uses `UnitDestroyedTriggerSpecRules.TryGetTrigger(..., IsLastBreathDiscardDrawTrigger, ...)` and validates source unit / face-up / non-standby boundaries | Accepted |
| Runtime hand-choice counts read TriggerSpec | `ResolveUndercoverAgentLastBreathStackItem(...)` derives required/max discard count and draw count from `TriggerSpec` with compatibility fallback | Accepted |
| Public wire compatibility is preserved | `TriggerKinds.UnitLastBreathDiscardDraw` keeps the existing effect string `UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT`; existing recovery and trigger tests remain green | Accepted |
| Core no longer owns Undercover local effect constant | `UndercoverAgentCardNo` and `UndercoverAgentLastBreathEffectKind` are absent from `CoreRuleEngine`; the guard test blocks reintroduction | Accepted |
| Full official breadth | complete generic hand-choice/discard-draw resolver, complete last-breath family, APNAP ordering and Kogmaw / Unsung Hero TriggerSpec migration remain residual | Residual, no full-official claim |

## 4. Verification

Initial focused TDD guard failed before implementation because `TriggerKinds.UnitLastBreathDiscardDraw` did not exist. After implementation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesUnitLastBreathDiscardDrawTrigger|FullyQualifiedName~UnitLastBreathDiscardDrawTriggerDoesNotUseCoreCardNumberBehavior" --nologo
```

Result: 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UndercoverAgent|FullyQualifiedName~LastBreath|FullyQualifiedName~TriggerSourceIdentityGuard|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2327/2327 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8819/8819 passed.

DevUi build was not rerun because this slice did not change DevUi source or shared TypeScript catalog payload shape.

## 5. Residual Risks

- This does not implement a fully generic pending hand-choice resolver across all discard/draw cards.
- This does not migrate Kogmaw or Unsung Hero to full TriggerSpec shape.
- This does not close complete simultaneous last-breath ordering or optional-trigger policy.
- This does not close card matrix full-official, frontend final validation, formal E2E or READY.
