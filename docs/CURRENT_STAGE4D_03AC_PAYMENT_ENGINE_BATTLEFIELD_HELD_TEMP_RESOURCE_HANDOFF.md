# Stage 4D-03AC PaymentEngine Battlefield Held Temporary Resource Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本切片继续收窄 P0-005 PaymentEngine breadth。目标是把 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 这个直接据守得分支付窗口，从已支持 `RECYCLE_RUNE:*` 扩展到 `TEMP_PAYMENT_RESOURCE:*` prompt quote / command commit / audit parity。

## Current Gap

现状已由 4D-03G / 4D-03K 分别证明：

- battlefield held score 可以用必要的 `RECYCLE_RUNE:*` 支付 4 generic power。
- `PLAY_CARD` / `ACTIVATE_ABILITY` / `ASSEMBLE_EQUIPMENT` inline windows 可以消费 temporary payment-only resource。
- pending `PAY_COST` prompt / snapshot 可以合并 `TEMP_PAYMENT_RESOURCE:*` 候选。

但 `BATTLEFIELD_HELD` 仍是 direct combat resolver path：

- `TryBuildMinimalDeclareBattle` 只把 `RECYCLE_RUNE:*` 识别为 held-score payment resource action。
- `ValidateDeclareBattleBattlefieldHeldScorePaymentResources` 只验证 recycled rune。
- `TryResolveBattlefieldHeldPayPowerScoreTrigger` 只抽取并提交 recycled rune resource actions。
- `DeclareBattleSourceRequirementView` 未给 held-score cost 暴露 `paymentResourceChoices` / `paymentResourcePowerByChoice`。

因此已有 Malzahar / Sigil / Gold temporary payment-only ledger 不能在该 held-score payment window 中被 quote 或消费。

## Scope

实现范围仅限：

- `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`
- payment window: `BATTLEFIELD_HELD`
- cost: 4 generic power
- payment player: scoring battlefield controller / owner fallback, matching existing battlefield held payment controller resolution
- payment resources:
  - existing `RECYCLE_RUNE:*`
  - new `TEMP_PAYMENT_RESOURCE:*`
  - mixed resource actions when both are needed

Expected behavior:

- Prompt metadata for `DECLARE_BATTLE` exposes payment resource choices only when the held-score battlefield representative is relevant and current rune pool cannot already pay the 4 generic power cost.
- `TEMP_PAYMENT_RESOURCE:<id>` is legal only when owned by the payment player, has remaining generic or typed power, includes `RUNE_COST`, and can help pay the held-score generic power cost.
- Command validation accepts necessary temporary resources and rejects stale, duplicate, wrong-owner, zero, wrong-kind, insufficient, and unnecessary resource actions with no mutation.
- Successful resolution emits temporary resource spend / cleanup events, includes temporary ids and payment resource actions in `COST_PAID`, commits the power cost through shared `PaymentCostRules`, then awards score as before.
- Existing Brush replacement held-score path continues to work when the effective battlefield is the original held-score battlefield.

## Suggested Write Scope

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- optionally `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs` if a more focused helper fixture is clearer

Do not modify frontend files or `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Acceptance

Focused tests should cover:

- held-score prompt quotes a temporary payment resource for a 4-power shortfall;
- held-score command consumes a temporary payment resource and scores;
- mixed recycle + temporary resources can pay the same held-score cost when needed;
- wrong owner, zero resource, wrong allowed kind, duplicate action, unnecessary action, invalid id, and insufficient combined resources reject with no mutation;
- score-prevented / already-scored battlefield paths do not consume temporary resources;
- Brush replacement plus held-score still resolves against the original battlefield identity.

Regression must keep existing 4D-03G recycle-rune held-score behavior green.

## No-Go

- Do not add new resource-skill cards.
- Do not enter LayerEngine / timestamp / dependency work.
- Do not let temporary payment resources pay mana-only, experience, score, or non-rune costs.
- Do not leak temporary resource power into unrestricted `RunePool.Power` beyond the payment window.
- Do not close P0-005, P1, READY, or full-official status.
