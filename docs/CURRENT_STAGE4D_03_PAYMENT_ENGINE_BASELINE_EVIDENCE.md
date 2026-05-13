# Stage 4D-03 Payment Engine Baseline Evidence

日期：2026-05-13
结论：**BASELINE GREEN / PROJECT NOT READY**

本文记录 Stage 4D-03 实现前的 payment 基线。它只说明当前 HEAD 的既有代表路径测试为绿色，不关闭 P0-005，不升级 full official，不改变项目 **NOT READY** 结论。

## 1. Scope

4D-03 目标来自 `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`：把 `PLAY_CARD`、`MOVE_UNIT`、`ASSEMBLE_EQUIPMENT`、`ACTIVATE_ABILITY`、`LEGEND_ACT`、battlefield trigger、keyword optional / extra cost 与 rune resource actions 统一到可回滚、可审计、服务端候选驱动的 PaymentEngine。

当前已存在基础能力：

- typed `RunePool`：`Mana`、generic `Power`、`PowerByTrait`。
- `PaymentCostRules`：payment id / event payload、rune cost、typed power、experience helper。
- `PendingPaymentState`：最小 `PAY_COST` window snapshot 和 prompt。
- `PLAY_CARD`：基础费用、typed `SPEND_POWER`、Haste / Echo / Spellshield tax、部分减费/加费、`RECYCLE_RUNE:*` payment resource action。
- `TRIGGER_PAYMENT`：Treasure Pile / Sunken Temple / Vayne / Icevale Archer / Jax 代表性支付或拒付窗口。
- 非出牌代表路径：Long Sword assemble、Vi / Xerath active ability、battlefield held score typed-power payment。
- ActionPrompt / GameHub：代表性 `sourceRequirements`、`paymentResourceChoices`、typed / generic payment resource seeds。

当前仍未证明：

- prompt quote 和 command commit 共享一个 PaymentPlan。
- `RECYCLE_RUNE:*` 与后续 cost commit 是中心化事务。
- 所有非出牌支付窗口都支持相同的 resource action / optional / extra cost semantics。
- `[A]`、支付步骤 `[C]`、所有 Haste/Echo/Spellshield tax、replacement / optional / extra cost 和触发费用拒付进入同一模型。
- trigger payment context 不再依赖 stringly typed reason。
- 所有支付失败统一回滚 hand / stack / runePool / objectLocations / playerExperience / pending window。

## 2. Baseline Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~P6RuneResourceDomainMapsAllRuneEntriesWithoutMakingRunesPlayableCards|FullyQualifiedName~P7TypedPowerPayment|FullyQualifiedName~P7PlayCardRecyclesRune|FullyQualifiedName~P7PlayCardPaymentResource|FullyQualifiedName~P7PlayCardGenericPaymentResource|FullyQualifiedName~P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions|FullyQualifiedName~P7PlayCardRejectsOverRecycledPaymentResourceActions|FullyQualifiedName~P79BattlefieldHeldPaysTypedPowerToGainScore"
```

Result: passed, 51/51.

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RuneResourceDomain|FullyQualifiedName~RecycleRune|FullyQualifiedName~TypedPowerPayment|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~PayCost|FullyQualifiedName~Payment|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed, 240/240.

Backend full baseline inherited from accepted 4D-02 evidence:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result at 4D-02 acceptance: passed, 3786/3786.

## 3. A Interpretation

- The current representative payment paths are green and can be used as regression guardrails for 4D-03.
- The green baseline does not prove P0-005 complete because the gap is architectural: quote / authorize / commit is still distributed across helper methods and command resolvers.
- 4D-03 must add tests that fail on quote/commit drift and transactional rollback before broadening implementation.
- Any implementation that only preserves current helper calls without centralizing at least one shared payment plan / commit path is not acceptable.

## 4. Next Step

Use `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_HANDOFF.md` as Maxwell / B service-side implementation handoff. A will accept the slice only after reviewing diff, focused / adjacent / backend full output and updated audit docs.
