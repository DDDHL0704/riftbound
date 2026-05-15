# Stage 4D-04M LayerEngine Minimum-Power Ledger Audit

日期：2026-05-15
结论：**IMPLEMENTED AND A-VALIDATED / PROJECT NOT READY**

本文件记录 4D-04M-B 的 A 侧验收。该切片只补强 minimum-power power modifier ledger metadata，不替换完整 LayerEngine，不修改 frontend，不更新 card matrix JSON，不升级 `fullOfficial`，不关闭 P1-001、P1-002 或 READY。

## 1. Scope

B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 在 4D-04M 写锁内完成：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

未触碰：

- frontend runtime
- card matrix JSON
- broad PaymentEngine
- battle lifecycle / task queue semantics
- wide equipment runtime
- fullOfficial / READY status
- `riftbound-dotnet.sln`

## 2. Accepted Behavior

A 接受该切片，因为当前 diff 满足 handoff：

1. `PowerModifierLedgerEntry.PowerDelta` 继续保持 applied delta 语义，并新增 `RequestedPowerDelta`、`MinimumPower`、`ResultingPower` metadata。
2. `CoreRuleEngine.ApplyPowerModifier` 继续保持现有 `Power` / `UntilEndOfTurnPowerModifier` arithmetic，只在非零 applied delta 时追加 ledger entry。
3. `MinimumPowerAfterModifier > 0` floor path 可以同时区分 requested delta、applied delta、minimum floor 与 resulting power。
4. applied delta 为 0 的 Extortion floor path 不写入 zero ledger，避免在 `ContinuousEffectState` / snapshot view 中产生误导性的非效果条目；事件 payload 仍保留既有 requested/applied/floor 信息。
5. ledger-backed `ContinuousEffectState` 与 snapshot `timing.continuousEffects` 保留旧字段，同时暴露 `requestedPowerDelta`、`appliedPowerDelta`、`minimumPower` 与 `resultingPower`。
6. Blastcone Sprout representative 新增 state、continuous effect 与 snapshot metadata assertions，证明 requested `-2`、applied `-1`、minimum `1`、resulting `1` 的 floor metadata exactness。
7. Switcheroo、minimum-power representatives、power modifier regressions 与 backend full suite 保持绿色。

## 3. Verification

A 侧验收命令与结果记录在 `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_EVIDENCE.md`：

- Focused minimum-power foundation guard：9/9 passed。
- Adjacent power / layer / minimum regression：16/16 passed。
- Backend full test：4447/4447 passed。
- `git diff --check` passed。

## 4. Residuals

仍未关闭：

- P1-001 full LayerEngine。
- timestamp ordering。
- dependency ordering。
- source ordering。
- keyword gain/loss layering。
- multiple equipment/static aura interactions。
- complete minimum-power ordering。
- full official LayerEngine coverage。
- P1-002 keyword execution breadth。
- card matrix fullOfficial。
- frontend final validation。
- READY / active goal completion。

## 5. Verdict

4D-04M-B is accepted and its runtime / focused-test write lock is closed. This gives current minimum-power power modifier representatives explicit requested/applied/minimum/resulting audit metadata, while the server remains on the existing foundation-only power modifier model. Project remains **NOT READY**.
