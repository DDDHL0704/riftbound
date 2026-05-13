# Stage 4D-03K PaymentEngine Temporary Resource Inline Audit

日期：2026-05-14
结论：**4D-03K FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03K 的 Malzahar temporary payment-only resource inline consumption focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_HANDOFF.md` 的最小推进要求，把 4D-03J 已建立的 `TEMP_PAYMENT_RESOURCE:*` ledger 从 pending `PAY_COST` 扩展到 `PLAY_CARD`、`ACTIVATE_ABILITY` 与 `ASSEMBLE_EQUIPMENT` representative inline payment windows；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_EVIDENCE.md`
- Focused regression：344/344 passed
- Adjacent regression：539/539 passed
- Backend full：3860/3860 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `ActionPromptBuilder` now quotes `TEMP_PAYMENT_RESOURCE:*` in `PLAY_CARD`, `ACTIVATE_ABILITY` and any-power `ASSEMBLE_EQUIPMENT` source metadata only when a current-player temporary resource can legally cover a generic rune-power shortfall.
- Per-choice prompt metadata includes power contribution, `paymentOnly=true`, source temporary resource id, allowed payment kinds and the Malzahar resource restriction. Typed-power-only assemble shortfalls do not expose temporary resource choices.
- `CoreRuleEngine.TryExtractInlinePaymentResourceActions` separates behavior optional costs from payment resource actions and accepts both `RECYCLE_RUNE:*` and `TEMP_PAYMENT_RESOURCE:*` with stale, wrong-owner, zero-power, wrong-kind and duplicate guards.
- Inline payment commits apply legal recycle rune actions and temporary resource contribution before shared `PaymentPlan` authorization / commit, then remove the consumed temporary ledger entry and emit `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `TEMPORARY_PAYMENT_RESOURCE_CLEARED`.
- `COST_PAID` payloads keep behavior optional costs separate from `paymentResourceActions`; assemble recycle assertions were updated to match this server-authoritative audit shape.
- The current representative lifecycle clears selected temporary resources at inline payment close, including any unused remainder, matching the 4D-03K handoff’s permitted cleanup stance.
- A reclaimed the write lock after Raman stalled on the implementation draft, reviewed the draft, fixed quote / audit gaps, added focused tests, reran required regressions and completed this audit. Raman did not commit.

## 3. Remaining No-Ready Items

- P0-005 remains open: this slice covers Malzahar temporary generic rune resource representatives, not the complete PaymentEngine or full `[A]` / `[C]` resource skill family.
- Other payment windows such as `HIDE_CARD`, `LEGEND_ACT`, battlefield held score and trigger payment were not migrated to temporary resource inline consumption in this slice.
- Reaction / counter full target-filter model, complete replacement / optional / extra cost quote parity, P1 LayerEngine / keyword pass, 1009/811 matrix completion and final Browser / hidden-info / replay-hash audit remain open.

## 4. Next Step

Continue P0-005 breadth. Highest-value next candidates are full resource skill family expansion, `LEGEND_ACT` / non-play temporary resource parity where official rules require it, and broader PaymentEngine quote / command / audit consistency for remaining optional and replacement costs.
