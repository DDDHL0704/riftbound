# Stage 4D-03G Payment Engine Battlefield Held Resource Handoff

日期：2026-05-13
状态：**HANDOFF READY / PROJECT NOT READY**

本文是 Stage 4D-03G 的服务端实现交接。目标是在 4D-03F 之后继续收窄 P0-005：让已接入 shared `PaymentPlan` 的 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 据守得分窗口，也能使用 `RECYCLE_RUNE:*` payment resource action 补足 4 点泛化符能费用。

## 1. Owner And Write Lock

- Owner：B / Maxwell 服务端实现。
- A 主控职责：派单、验收、复跑测试、文档收口；不默认亲自写功能代码。
- 写入范围：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`，仅当 held-score prompt / snapshot 需要公开 payment resource metadata 时
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`，仅当需要新增 shared helper regression
  - `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`，仅当需要覆盖 Hub prompt metadata
- 不改前端 UI、不改卡牌矩阵、不碰未跟踪的 `riftbound-dotnet.sln`。

## 2. Scope

4D-03G 聚焦 battlefield held score resource action，不改变当前 mana-only `TRIGGER_PAYMENT` 代表路径。

必须覆盖：

- `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 当前 power 不足、但基地中存在可回收基础符文时，服务端能够公开或接受合法 `RECYCLE_RUNE:<objectId>` payment resource action。
- command commit 必须在同一支付事务中先应用合法 `RECYCLE_RUNE:*`，生成既有 `RUNE_RECYCLED` / `POWER_GAINED` events，再通过 shared `PaymentPlan` / `TryCommitPayment` 扣除最终 4 点 power。
- `COST_PAID` payload 保留现有兼容键，并补齐或保持 plan-driven metadata：`paymentWindow`、`reason = BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`、`paymentResourceActions`、`recycledRuneObjectIds`、remaining pool metadata。
- 当前 power 已足够时，夹带不必要 `RECYCLE_RUNE:*` 必须 rejected 且 no-mutation。
- 无效、不属于当前玩家、非基础符文、非基地符文或重复资源动作必须 rejected 且 no-mutation。
- 既有得分 once-per-turn、third-turn delay / prevent、typed-power 支付和旧泛化 power 支付行为保持兼容。

## 3. Implementation Notes

- 4D-03B 已把 battlefield held score 支付接入 shared `PaymentPlan` / `TryCommitPayment`，当前缺口是 resource action quote / commit。
- 4D-03D / 4D-03F 已有可复用 helper：
  - `TryExtractRecycleRunePaymentResourceActions`
  - `AreRecycleRunePaymentResourceActionsRequired`
  - `ApplyRecycleRunePaymentResourceActions`
  - prompt metadata 中 payment resource contribution / available-power-with-resources 的表达。
- 优先复用 shared helper，避免为 held-score 单独复制回收、扣费和审计逻辑。
- 若 held-score 当前不是 pending `PAY_COST` 窗口，仍要保持 command guard 清晰：资源动作只作为该据守得分支付的一部分被允许，不能变成普通 open-main `RECYCLE_RUNE` 的替代入口。

## 4. No-Go

- 不把 Vayne / Icevale / Jax / Treasure Pile / Sunken Temple 等 mana-only `TRIGGER_PAYMENT` 改成 power-cost trigger。
- 不实现新的 concrete card runtime 或新的 trigger queue 模式选择。
- 不关闭完整 P0-005。
- 不实现完整 `[A]` / `[C]` resource skills、Legend resource action、Haste / Echo / Spellshield 全窗口资源动作。
- 不改完整 PAY_COST DTO 长期契约、卡牌矩阵、LayerEngine 或 READY 结论。

## 5. Acceptance Gates

Focused filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldPaysPowerToGainScore|FullyQualifiedName~P79BattlefieldHeldPaysTypedPowerToGainScore|FullyQualifiedName~P79BattlefieldScoreDelayPreventsHeldScorePaymentBeforeThirdTurn|FullyQualifiedName~PaymentEngineUnificationTests"
```

Adjacent filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Final gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Baseline Evidence

实现前基线见 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_BASELINE_EVIDENCE.md`。
