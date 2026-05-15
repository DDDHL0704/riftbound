# 4D-04O LayerEngine Power Modifier Ordering Baseline Evidence

日期：2026-05-16
结论：**BASELINE GREEN / HANDOFF ONLY / PROJECT NOT READY**

本文件记录 4D-04O-B 实现前基线。该批次只建立 handoff，不改 runtime、不改 tests、不派发 worker、不打开写锁。

## 1. Baseline Gap

当前服务端已经有 ledger-backed power modifier metadata，但还没有显式 application order / timestamp 字段：

- `src/Riftbound.Engine/MatchSession.cs` 的 `PowerModifierLedgerEntry` 暴露 effect/source/minimum/resulting metadata，但无顺序字段。
- `CardObjectState.NormalizePowerModifierLedger` 按 `EffectId` 排序 ledger entries，会丢掉 entry append order 的直接可观察性。
- `BuildContinuousEffectStates` 最后按 `Scope` / `TargetObjectId` / `Layer` / `EffectId` 排序 continuous effects，snapshot view 也不会显式告诉前端或审计层真实应用顺序。
- `EffectId` 末尾虽然包含 ledger count，但这是内部 id 组成，不应要求后续 LayerEngine consumer 解析字符串来恢复顺序。

因此 4D-04O 的下一步不是 full LayerEngine，而是补一层显式顺序 metadata，作为后续 timestamp / dependency / source-ordering 模型的安全地基。

## 2. Commands

Focused ordering baseline:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn|FullyQualifiedName~P79LegendTriggerRengarGivesUnitPlusOneAfterUnitPlayed|FullyQualifiedName~IcevaleArcherAttackPaymentAcceptedAppliesTemporaryPowerMinusOne|FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

Result: **passed 6/6**.

Adjacent LayerEngine / power metadata regression:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Rengar|FullyQualifiedName~Icevale|FullyQualifiedName~Switcheroo|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

Result: **passed 37/37**.

Backend full:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **passed 4447/4447**.

## 3. Boundary

This is A-side baseline evidence only. It does not close:

- P1-001 full continuous effect LayerEngine
- timestamp / dependency graph
- source ordering for all static / replacement / direct breadth
- keyword gain/loss layer ordering
- complete minimum-power ordering
- P1-002 keyword execution breadth
- full-card matrix official coverage
- frontend final validation
- READY
