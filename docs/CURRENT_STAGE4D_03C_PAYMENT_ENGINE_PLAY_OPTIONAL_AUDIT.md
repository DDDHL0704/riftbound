# Stage 4D-03C Payment Engine Play Optional/Extra Audit

日期：2026-05-13
结论：**4D-03C FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03C 的 `PLAY_CARD` optional / extra / payment-resource focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_HANDOFF.md` 的最小推进要求，把 Haste / Echo / Spellshield / experience / payment-resource 代表路径进一步接入 shared `PaymentPlan` authorize / commit / audit 口径；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_EVIDENCE.md`
- Focused regression：31/31 passed
- Adjacent regression：363/363 passed
- Backend full：3791/3791 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `TryBuildPlayCardPlan` 的 representative affordability preflight 改为通过 `PaymentCostRules.PaymentPlan` + `AuthorizePayment` 判定 mana / generic power / typed power / experience 是否可支付。
- `ResolvePlayCard` 的 final commit 仍在 `RECYCLE_RUNE:*` payment resource action 落入临时资源池后调用 `TryCommitPayment`，保留既有事务回滚语义。
- `PLAY_CARD` `COST_PAID` payload 继续保留旧兼容键，并通过 plan audit metadata 增补 cost reduction、optional mana reduction、battlefield Echo/equipment/spell reductions、held unit cost increase、Spellshield tax 和 recycled rune object ids。
- 新增/强化 fixture 断言锁住 Haste ready、Echo、Spellshield tax、experience optional cost 与 recycle-rune payment resource 的 plan metadata envelope。
- 本切片未修改 `PaymentCostRules.cs`、`MatchSession.cs`、前端、卡牌矩阵或未跟踪的 `riftbound-dotnet.sln`。

## 3. Remaining No-Ready Items

- P0-005 仍未 full-official resolved：完整 `[A]` / `[C]` 资源技能、Haste / Echo / Spellshield 全窗口、替代 / 额外 / 可选费用全矩阵、费用目标选择、replacement/prevention 交互和全部 prompt quote parity 仍未完成。
- P0-002 / P0-003 / P0-004 的 full-official board / cleanup / spell-duel / battle lifecycle 残余仍未关闭。
- P1 LayerEngine、关键词 full-pass、1009/811 full-official matrix、最终 Chrome / hidden-info / replay-hash audit 仍未收口。

## 4. Next Step

后续 P0-005 应继续从 representative `PLAY_CARD` / non-play paths 扩展到 full PaymentEngine breadth，优先处理 `[A]` / `[C]` resource skills、更多替代 / 额外 / 可选费用与 prompt sourceRequirements / command commit 的 quote parity。
