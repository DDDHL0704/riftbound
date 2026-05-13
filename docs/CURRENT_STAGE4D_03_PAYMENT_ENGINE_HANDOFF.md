# Stage 4D-03 Payment Engine Handoff

日期：2026-05-13
结论：**NOT READY**

本文是 Stage 4D-03 的服务端实现交接规格。它只定义 P0-005 的下一片可交付范围，不授权前端补洞、不升级卡牌矩阵、不宣称完整 PaymentEngine、P1 或 READY 已关闭。

前置状态：4D-02 spell duel / battle focused slice 已验收，见 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_AUDIT.md`。4D-03 的实现前基线见 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_BASELINE_EVIDENCE.md`。

## 1. 目标

4D-03 要把现有分散的费用校验、prompt 候选和扣费动作推进到一个更稳定的 PaymentEngine foundation：

- `PLAY_CARD`、`TRIGGER_PAYMENT` / `PAY_COST`、`ASSEMBLE_EQUIPMENT`、`ACTIVATE_ABILITY`、`LEGEND_ACT`、`MOVE_UNIT`、keyword optional/extra cost 与 rune resource actions 使用同一套 quote / authorize / commit 语义。
- prompt 的 `sourceRequirements`、`paymentChoices`、`paymentResourceChoices` 与命令侧实际可支付性必须来自同一支付计划，不允许 UI 或 prompt 构造器复制规则判断。
- 支付失败必须保持 hand、stack、runePool、objectLocations、playerExperience、pending payment window 等权威状态不变。
- `COST_PAID` / `PAYMENT_WINDOW_OPENED` 事件保留稳定 `paymentId`、`paymentWindow`、cost、resource action、remaining pool 和 reason metadata，便于 audit / replay。
- 支持普通法力、任意符能、指定特性符能、额外/可选费用、减费/加费、触发费用拒付、回收符文支付资源动作和代表性经验费用。

本切片不要求一次完成所有官方费用矩阵。最小可接受推进是建立统一 payment plan / commit 入口，并用 automated tests 证明至少 `PLAY_CARD`、`TRIGGER_PAYMENT`、一个非出牌资源费用窗口和 payment resource action 共享同一模型。

## 2. 当前代码面

已存在基础面：

- `src/Riftbound.Engine/PaymentCostRules.cs`
  - `BuildPaymentId`
  - `BuildCostPaidPayload`
  - `PayRuneCosts`
  - `CanPayRuneCosts`
  - `CanPayPowerCost`
  - `PayPowerCost`
  - `PayExperienceCosts`
- `src/Riftbound.Engine/MatchSession.cs`
  - `RunePool` 已有 `Mana`、generic `Power` 和 `PowerByTrait`。
  - `PendingPaymentState` 暴露 `PaymentId`、`PaymentWindow`、cost、`LegalPaymentChoiceIds` 和 `Reason`。
  - pending payment snapshot 暴露 `timing.pendingPayment.cost` 与 `paymentChoices`。
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `ResolvePlayCard` 先从 `TryBuildPlayCardPlan` 计算费用，再移除手牌、结算 `RECYCLE_RUNE:*`、扣 `RunePool`、扣经验并发出 `COST_PAID`。
  - `TryBuildPlayCardPlan` 已计算基础费用、可选费用、typed power、Haste、Echo、Spellshield tax、装备减费、战场加费、下一法术减费、支付资源动作和经验费用。
  - `TRIGGER_PAYMENT` 的多个 opened window 仍逐个构造 `PendingPaymentState`，用 string reason 解析上下文。
  - `ResolvePayCost` 已能处理代表性 pending payment 支付、拒付、错误 player、stale id、非法 choice、支付不足 no-mutation。
  - `ASSEMBLE_EQUIPMENT`、代表性 `ACTIVATE_ABILITY`、`LEGEND_ACT` 和 battlefield held score 目前分别调用 `PayRuneCosts` / `CanPayRuneCosts`。
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
  - 覆盖 Treasure Pile / Sunken Temple / Vayne / Icevale Archer / Jax 代表性触发费用支付、拒付、非法选择、stale id、支付不足和 no-mutation。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - 覆盖 typed `SPEND_POWER`、`RECYCLE_RUNE:*` payment resource、wrong-trait rejection、mixed generic trait resource、Haste payment resource、Vi / Xerath / Long Sword / battlefield held score typed-power representative paths。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`、`GameHubJoinTests.cs`
  - 覆盖 `PAY_COST` contract、prompt metadata、ActionPrompt/GameHub payment resource candidates。

Current limitation: these surfaces prove representative payment paths, but payment remains a helper + many call sites. Prompt quote and command commit can still drift, trigger payment context is stringly typed, non-play payment windows do not yet get the same payment resource / optional cost handling, and rollback invariants are not centralized.

## 3. Write Lock

Exclusive implementation write scope:

- `src/Riftbound.Engine/PaymentCostRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`

Allowed new files:

- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- Narrow fixtures under `tests/Riftbound.ConformanceTests/Fixtures/` if needed.

Allowed existing test touch if required by implementation:

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

Blocked parallel writes:

- Do not modify frontend UI in this slice.
- Do not modify card matrix status in this slice.
- Do not modify 4D-01 / 4D-02 board task and spell-duel/battle lifecycle behavior except where payment legality is directly involved.
- Do not use UI-side assumptions to choose resources, infer costs, or decide whether a command is payable.
- Do not stage or touch unrelated solution files.

## 4. Required Implementation Shape

Minimum acceptable design:

- Introduce a central payment plan shape, whether named `PaymentPlan` or equivalent, that can represent:
  - `paymentId`
  - `paymentWindow`
  - player id
  - base mana
  - total mana
  - generic power
  - `powerByTrait`
  - experience
  - optional / extra / replacement cost labels
  - payment resource action ids
  - legal payment choice ids
  - audit reason / source metadata
- Provide a single quote / authorize / commit path for rune pool and experience payments. It may live in `PaymentCostRules` for this slice, but command resolvers should stop open-coding equivalent checks where touched.
- Keep payment resource actions transactional. Applying `RECYCLE_RUNE:*` and then failing the remaining cost must leave source state unchanged.
- Preserve exact no-mutation behavior for invalid `PAY_COST`, stale id, wrong player, duplicate/unknown choice, pay-and-decline, insufficient resources and malformed payload.
- Preserve `COST_PAID` payload compatibility while deriving fields from the central plan.
- Ensure `ActionPrompt` metadata and command-side validation share the same source requirements for at least the touched `PLAY_CARD` resource-action path.
- Add tests that would fail if quote and commit diverge.

## 5. Focused Acceptance Tests

4D-03 first slice is not accepted until all of the following are covered by automated tests:

- `PLAY_CARD` typed power quote and commit use the same plan: red requirement accepts red, rejects blue, and no-mutation preserves hand / stack / runePool / objectLocations.
- `RECYCLE_RUNE:*` payment resource action is transactional: wrong trait, over-recycle, duplicate resource, non-required resource and post-resource insufficient cost all no-mutate.
- `TRIGGER_PAYMENT` / `PAY_COST` uses the same payment model for pay, decline, stale id, wrong player, malformed choices, duplicate choices, pay-and-decline and insufficient resources.
- At least one non-play payment path, such as `ASSEMBLE_EQUIPMENT`, `ACTIVATE_ABILITY`, `LEGEND_ACT` or battlefield held score, pays through the shared commit helper and keeps typed-power behavior.
- `sourceRequirements.paymentResourceChoices` / `optionalCostChoices` match command-side payability for the touched `PLAY_CARD` scenario.
- `COST_PAID` and `PAYMENT_WINDOW_OPENED` still expose stable audit metadata: `paymentId`, `paymentWindow`, cost, remaining pool and reason/source metadata.

Suggested focused filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~P6RuneResourceDomainMapsAllRuneEntriesWithoutMakingRunesPlayableCards|FullyQualifiedName~P7TypedPowerPayment|FullyQualifiedName~P7PlayCardRecyclesRune|FullyQualifiedName~P7PlayCardPaymentResource|FullyQualifiedName~P7PlayCardGenericPaymentResource|FullyQualifiedName~P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions|FullyQualifiedName~P7PlayCardRejectsOverRecycledPaymentResourceActions|FullyQualifiedName~P79BattlefieldHeldPaysTypedPowerToGainScore"
```

Suggested adjacent filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RuneResourceDomain|FullyQualifiedName~RecycleRune|FullyQualifiedName~TypedPowerPayment|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~PayCost|FullyQualifiedName~Payment|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Final per-slice gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## 6. No-Go Criteria

- Do not mark P0-005 resolved unless focused, adjacent and backend full tests pass and `docs/CURRENT_SERVER_RULE_AUDIT.md` is updated with exact evidence.
- Do not claim full official PaymentEngine if `[A]` / `[C]` resource skills, all Haste/Echo/Spellshield branches, replacement / optional / extra costs and all non-play windows are not unified.
- Do not claim prompt authority if `sourceRequirements` can still diverge from command-side payability for touched paths.
- Do not claim P0-002/P0-003/P0-004 resolved; board / cleanup / spell-duel / battle full-official lifecycle remains separate.
- Do not update `IMPLEMENTED_TESTED` / full-official matrix fields from this handoff alone.
- Do not call active goal complete.

## 7. Handoff Summary

Next implementing agent should start by adding failing tests for quote/commit drift and transactional resource-action rollback, then introduce the narrowest central payment plan / commit helper. Prefer migrating `PLAY_CARD` and one representative non-play path first, then route existing `TRIGGER_PAYMENT` / `PAY_COST` through the same validation and audit envelope. Return diff, focused / adjacent / backend full output and updated audit docs to A for review.
