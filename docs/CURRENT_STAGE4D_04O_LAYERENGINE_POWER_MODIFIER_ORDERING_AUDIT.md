# 4D-04O LayerEngine Power Modifier Ordering Audit

日期：2026-05-16
结论：**IMPLEMENTED AND A-VALIDATED / WRITELOCK CLOSED / PROJECT NOT READY**

本文件记录 4D-04O-B 的 A 侧验收。该切片只为 ledger-backed until-end power modifier 增加显式 application order metadata，仍不是完整 LayerEngine。

## 1. 范围

4D-04O-B 允许范围：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- focused LayerEngine / power modifier conformance tests

本批未触碰：

- 前端运行时代码
- card matrix JSON / fullOfficial 状态
- broad PaymentEngine
- battle lifecycle / task queue 语义重写
- wide equipment runtime
- 完整 LayerEngine rewrite
- `riftbound-dotnet.sln`

## 2. 实现事实

- `PowerModifierLedgerEntry` 新增 nullable `AppliedOrder`，并在 JSON 输出中省略 null。
- `ContinuousEffectState` 新增 nullable `AppliedOrder`，ledger-backed power modifier 会把 order 投影到 continuous effect view。
- `ResolutionResult.BuildSnapshots` 的 `timing.continuousEffects[]` 现在会在存在 order 时暴露 `appliedOrder`。
- `CoreRuleEngine.ApplyPowerModifier` 与 `ApplyDirectUntilEndPowerModifier` 在 applied delta 非零时用 append sequence 生成下一枚 `AppliedOrder`，并把同一个值写入 ledger entry 与 effect id 尾段。
- `CardObjectState.NormalizePowerModifierLedger` 在 entries 带 `AppliedOrder` 时按该字段排序，避免同目标同层 view 被 `EffectId` 字典序改写。
- legacy untracked remainder fallback 仍不带 `AppliedOrder`，不会伪造 ordered ledger metadata。
- `NormalizeCardObject` 现在保留 `UntilEndOfTurnPowerModifiers`，避免状态 normalize 时丢失 ledger metadata。

## 3. 测试覆盖

新增 / 扩展的代表断言覆盖：

- Blastcone minimum-power representative：state ledger、`ContinuousEffectState`、snapshot 均暴露 `appliedOrder=1`。
- Rengar direct trigger representative：state ledger、continuous effect、snapshot 均暴露 `appliedOrder=1`，并保留 end-turn cleanup。
- Icevale trigger payment representative：direct path metadata 与 `appliedOrder=1` 同时存在。
- Switcheroo representative：两个目标各自的 single modifier 均有 per-target `appliedOrder=1`。
- Power Bind Echo representative：同一目标两枚 modifiers 的 order 为 `[1, 2]`，并在 state / continuous effects / snapshot 中一致。
- Shape test：构造 `EffectId` 字典序与 append order 相反的 ledger，确认 normalized state、continuous effects、snapshot 仍按 `AppliedOrder` 输出；legacy untracked power modifier 不输出 `appliedOrder`。

## 4. A 侧验收命令

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn|FullyQualifiedName~P79LegendTriggerRengarGivesUnitPlusOneAfterUnitPlayed|FullyQualifiedName~IcevaleArcherAttackPaymentAcceptedAppliesTemporaryPowerMinusOne|FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

Result: **6/6 passed**.

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Rengar|FullyQualifiedName~Icevale|FullyQualifiedName~Switcheroo|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

Result: **39/39 passed**.

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4449/4449 passed**.

```sh
git diff --check
```

Result: **passed**.

## 5. 残留

4D-04O-B 不关闭以下事项：

- 完整 LayerEngine
- timestamp / dependency graph
- source-ordering breadth
- keyword gain/loss ordering
- multiple equipment/static aura interactions
- complete minimum-power ordering
- P1-002 keyword execution breadth
- full-card matrix official coverage
- frontend final validation
- READY

当前项目仍 **NOT READY**。
