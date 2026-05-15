# Stage 4D-04L LayerEngine Foundation Audit

日期：2026-05-15
结论：**IMPLEMENTED AND A-VALIDATED / PROJECT NOT READY**

本文件记录 4D-04L-B 的 A 侧验收。该切片只建立 source-aware / effect-aware until-end power modifier metadata foundation，不替换完整 LayerEngine，不修改 frontend，不更新 card matrix JSON，不升级 `fullOfficial`，不关闭 P1-001、P1-002 或 READY。

## 1. Scope

B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 在 4D-04L 写锁内完成：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/SwitcherooGuardTests.cs`

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

1. `PowerModifierLedgerEntry` 为 until-end power modifier 保存 `effectId`、`effectKind`、`sourceObjectId`、`sourceCardNo`、`sourcePath`、`powerDelta`、`basePower` 与 `effectivePower`。
2. `CoreRuleEngine.ApplyPowerModifier` 仍保持现有 `Power` + `UntilEndOfTurnPowerModifier` 算术，只在同一结算中追加 source/effect-aware ledger metadata。
3. turn-end cleanup 与 replay-to-field 等清理路径会同步清空 ledger，避免过期 metadata 残留。
4. `ContinuousEffectState` 与 snapshot `timing.continuousEffects` 保留旧字段，同时对 ledger-backed power modifier 暴露 `effectKind`、`sourceCardNo`、`sourcePath`、`layerEngineStatus=FOUNDATION_ONLY` 与 deferred residual list。
5. Legacy / direct untracked modifier 仍有 remainder fallback，不漏掉现有聚合 `UntilEndOfTurnPowerModifier` view。
6. `SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn` 新增 source/effect metadata assertions，证明至少一个 representative power modifier path 已能被 audit。
7. Existing Ornn、Switcheroo、battle-response assignment、trigger payment power modifier、pending-task zero-power guard 与 turn-end cleanup representatives 保持绿色。

## 3. Verification

A 侧验收命令与结果记录在 `docs/CURRENT_STAGE4D_04L_LAYERENGINE_FOUNDATION_EVIDENCE.md`：

- Focused LayerEngine foundation guard：11/11 passed。
- Adjacent power / layer / equipment regression：141/141 passed。
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
- minimum-power layering。
- full official LayerEngine coverage。
- P1-002 keyword execution breadth。
- card matrix fullOfficial。
- frontend final validation。
- READY / active goal completion。

## 5. Verdict

4D-04L-B is accepted and its runtime / focused-test write lock is closed. This gives current until-end power modifier representatives a source-aware audit foundation, but the server still mutates current `Power` and derives views from aggregate modifier state. Project remains **NOT READY**.
