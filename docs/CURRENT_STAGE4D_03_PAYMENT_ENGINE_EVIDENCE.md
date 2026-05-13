# Stage 4D-03 Payment Engine Evidence

日期：2026-05-13
结论：**FOCUSED FOUNDATION ACCEPTED / PROJECT NOT READY**

本文记录 Stage 4D-03 第一片服务端 PaymentEngine foundation 证据。该证据接受 focused foundation，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Code Changes

- `src/Riftbound.Engine/PaymentCostRules.cs`
  - 新增 `PaymentPlan`，统一记录 payment id/window、player、mana、generic power、typed power、experience、optional / extra / payment resource labels、legal choices 与 audit metadata。
  - 新增 `AuthorizePayment` 与 `TryCommitPayment`，集中处理 rune pool / typed power / experience 的授权和扣费。
  - 新增 plan-driven `BuildCostPaidPayload`，在保持既有 payload 兼容字段的同时补充 plan audit metadata。
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `ResolvePayCost` 与当前触发支付代表路径改为通过 `PaymentPlan` / `TryCommitPayment`。
  - `ResolvePlayCard` 先构建 payment plan，再在副本上应用 `RECYCLE_RUNE:*`，最后统一 commit rune pool / experience。
  - `ResolveAssembleEquipment` 作为非出牌代表路径使用 shared payment authorization / commit helper。
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
  - 强化代表性触发支付 `COST_PAID` payload 的 payment plan metadata 断言。
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
  - 新增 payment plan helper、transactional resource-action rollback、play-card audit metadata 与 assemble audit metadata focused tests。

## 2. Covered Acceptance Points

- `PaymentPlan` commit 能同时扣 mana、typed power、generic power 和 experience，并输出 remaining pool / experience audit payload。
- wrong-trait typed power commit 拒绝且不修改输入 rune pool。
- `PLAY_CARD` 中 `RECYCLE_RUNE:*` 资源动作在后续 typed cost 不足时事务回滚，hand/base/runeDeck/runePool/objectLocations/stack 保持原始状态。
- `PLAY_CARD` 的 `COST_PAID` 事件带 `paymentWindow`、source object、base/total mana、generic/total power、experience、optional cost metadata。
- `ASSEMBLE_EQUIPMENT` 作为非出牌代表路径通过同一 plan audit envelope 输出 `COST_PAID` metadata。
- 既有 trigger payment pay / decline / stale / malformed / duplicate / insufficient regression 继续通过。

## 3. Verification

Focused regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~P6RuneResourceDomainMapsAllRuneEntriesWithoutMakingRunesPlayableCards|FullyQualifiedName~P7TypedPowerPayment|FullyQualifiedName~P7PlayCardRecyclesRune|FullyQualifiedName~P7PlayCardPaymentResource|FullyQualifiedName~P7PlayCardGenericPaymentResource|FullyQualifiedName~P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions|FullyQualifiedName~P7PlayCardRejectsOverRecycledPaymentResourceActions|FullyQualifiedName~P79BattlefieldHeldPaysTypedPowerToGainScore"
```

Result: passed, 56/56.

Adjacent regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RuneResourceDomain|FullyQualifiedName~RecycleRune|FullyQualifiedName~TypedPowerPayment|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~PayCost|FullyQualifiedName~Payment|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed, 245/245.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 3791/3791.

Whitespace:

```sh
git diff --check
```

Result: no output after implementation review.

## 4. Remaining Scope

This evidence is intentionally a foundation slice. The following remain open:

- full `[A]` / payment-step `[C]` resource skill model;
- all Haste / Echo / Spellshield tax branches through a single quote/commit pipeline;
- replacement / optional / extra cost breadth and all non-play windows;
- prompt `sourceRequirements` / payment choice quote parity for every payment path;
- trigger payment context de-stringification;
- LayerEngine, keyword full-pass and full-card official matrix.
