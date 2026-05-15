# Stage 4D-04N LayerEngine Direct Power Ledger Audit

日期：2026-05-16
结论：**IMPLEMENTED AND A-VALIDATED / PROJECT NOT READY**

本文件记录 4D-04N-B 的 A 侧验收。该切片只补强 direct until-end power mutation representatives 的 ledger metadata，不替换完整 LayerEngine，不修改 frontend，不更新 card matrix JSON，不升级 `fullOfficial`，不关闭 P1-001、P1-002 或 READY。

## 1. Scope

B-Implementation / Godel `019e2c69-aa6d-7701-9525-6a79a50fa210` 在 4D-04N 写锁内完成：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
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

1. `ApplyDirectUntilEndPowerModifier` 复用现有 `PowerModifierLedgerEntry` / `ContinuousEffectState` projection，只在 direct path 的 applied delta 非零时追加 ledger entry。
2. Icevale Archer、Ember Monk、conquest +8、Rengar、battlefield moved +1、optional ready power、Vi double power direct paths 现在都通过 direct helper 维护 `Power`、`UntilEndOfTurnPowerModifier` 与 ledger metadata。
3. direct ledger metadata 保留 `sourceObjectId`、`sourceCardNo`、`effectKind`、`sourcePath`、requested / applied / minimum / resulting、base / effective power。
4. applied delta 为 0 的 direct path 不创建 zero-delta ledger entry，避免 stale / no-op path 在 snapshot 中产生 misleading continuous effect。
5. Existing `MatchSession` projection 未改动；ledger-backed direct entries 通过现有 `timing.continuousEffects` 暴露，legacy untracked remainder fallback 仍可覆盖未迁移路径。
6. Icevale Archer trigger-payment representative 验证 state ledger、`ContinuousEffectState` 与 snapshot metadata。
7. Rengar unit-play trigger representative 验证 state / snapshot metadata，并在 `END_TURN` 后确认 ledger 与 `POWER_MODIFIER` continuous effect 一起清理。
8. Existing 4D-04L / 4D-04M `ApplyPowerModifier` source/effect/minimum metadata guards、adjacent trigger/payment regressions 与 backend full suite 保持绿色。

## 3. Verification

A 侧验收命令与结果记录在 `docs/CURRENT_STAGE4D_04N_LAYERENGINE_DIRECT_POWER_LEDGER_EVIDENCE.md`：

- Focused direct-power guard：6/6 passed。
- Adjacent power / layer / trigger regression：185/185 passed。
- Backend full test：4447/4447 passed。
- `git diff --check` passed。

## 4. Residuals

仍未关闭：

- P1-001 full LayerEngine。
- timestamp / dependency / source ordering。
- keyword gain/loss layering。
- multiple equipment / static aura interactions。
- complete minimum-power ordering。
- unselected direct / static / replacement effect breadth。
- P1-002 keyword execution breadth。
- card matrix fullOfficial。
- frontend final validation。
- READY / active goal completion。

## 5. Verdict

4D-04N-B is accepted and its runtime / focused-test write lock is closed. This gives selected direct until-end power mutation representatives ledger-backed source/effect/direct-path metadata while the server remains on the existing foundation-only power modifier model. Project remains **NOT READY**.
