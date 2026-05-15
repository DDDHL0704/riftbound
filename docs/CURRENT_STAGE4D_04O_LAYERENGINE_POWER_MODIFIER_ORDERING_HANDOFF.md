# 4D-04O LayerEngine Power Modifier Ordering Handoff

日期：2026-05-16
结论：**HANDOFF READY / NOT DISPATCHED / PROJECT NOT READY**

本文件是 A 主控为 P1-001 建立的下一枚窄实现交接单。4D-04L / 4D-04M / 4D-04N 已经让 until-end power modifier ledger 暴露 source / effect / direct-path / requested / applied / minimum / resulting metadata；4D-04O 只补“同层实际应用顺序”metadata，不实现完整 LayerEngine。

## 1. 当前事实

- `PowerModifierLedgerEntry` 当前没有显式 application order / timestamp 字段。`CoreRuleEngine.ApplyPowerModifier` 与 `ApplyDirectUntilEndPowerModifier` 会把新 entry append 到 `UntilEndOfTurnPowerModifiers`，但后续 state normalization 会改变可观察顺序。
- `CardObjectState.NormalizePowerModifierLedger` 目前按 `EffectId` 字典序排序 ledger entries；`MatchSession.BuildContinuousEffectStates` 最后又按 `Scope` / `TargetObjectId` / `Layer` / `EffectId` 排序 continuous effect view。
- `BuildPowerModifierLedgerEffectId` / `BuildDirectPowerModifierLedgerEffectId` 的尾段包含当前 ledger count + 1，但这是 effect id 内部字符串的一部分，不应成为未来 LayerEngine / UI / audit 推断顺序的唯一来源。
- 4D-04L/M/N 的 focused representatives 仍绿色，说明现有 arithmetic、minimum floor、direct mutation metadata 与 turn-end cleanup 可以作为 4D-04O 的保护边界。

## 2. 目标

4D-04O-B 的建议目标是：为 ledger-backed until-end power modifier 增加稳定、显式、可投影的应用顺序 metadata，使后续 LayerEngine 可以审计同一对象、同一 layer 下多个修正的真实应用顺序。

必须保持：

1. 现有 `Power` / `UntilEndOfTurnPowerModifier` arithmetic 不变。
2. 现有 `PowerModifierLedgerEntry.PowerDelta` 仍表示 applied delta。
3. 4D-04M 的 requested / applied / minimum / resulting metadata 不倒退。
4. 4D-04N 的 direct path source / effect / direct-path metadata 不倒退。
5. `END_TURN` 清理后 ledger-backed continuous effects 仍消失。
6. snapshot / `ContinuousEffectState` 可以暴露相同的顺序字段，但不得要求前端本地重算战力。

## 3. 建议写锁

建议只在 A 明确 dispatch 后打开 4D-04O-B 写锁。

允许范围：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/**` 中 focused LayerEngine / power modifier representatives
- 必要时的最小 helper / model 调整

禁止范围：

- 前端运行时代码
- card matrix JSON / fullOfficial 状态
- broad PaymentEngine
- battle lifecycle / task queue 语义重写
- wide equipment runtime / full static aura rewrite
- 完整 LayerEngine 重算框架
- `riftbound-dotnet.sln`

## 4. 验收要求

4D-04O-B 完成后，A 侧验收至少需要确认：

1. Ledger entry、`ContinuousEffectState` 与 snapshot view 暴露一致的显式顺序 metadata，例如 `appliedOrder` 或同等命名。
2. 同一目标上多个 until-end power modifiers 的顺序反映实际 append / mutation 顺序，不依赖 `EffectId` 字典序推断。
3. legacy untracked remainder fallback 保持兼容，且不会伪造 misleading ordered ledger metadata。
4. Rengar / Icevale / Switcheroo / minimum-power representatives 不倒退。
5. `END_TURN` 后顺序 metadata 随 ledger 一起清理。
6. 禁止把本切片扩大成 timestamp dependency graph、keyword gain/loss ordering、equipment aura dependency、full minimum-power ordering 或 full official closure。

## 5. 实现前基线

实现前基线见 `docs/CURRENT_STAGE4D_04O_LAYERENGINE_POWER_MODIFIER_ORDERING_BASELINE_EVIDENCE.md`。

通过命令：

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

Result: **37/37 passed**.

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4447/4447 passed**.

## 6. 暂停点

本 handoff 尚未派发 B worker，也未打开 runtime / test 写锁。当前项目仍 **NOT READY**，P1-001、P1-002、full-card matrix、frontend final validation 与 READY 均未关闭。
