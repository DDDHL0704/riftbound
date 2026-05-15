# 4D-04P LayerEngine Minimum Power Ordering Handoff

日期：2026-05-16
结论：**HANDOFF READY / NOT DISPATCHED / PROJECT NOT READY**

本文件是 A 主控为 P1-001 建立的下一枚窄实现交接单。4D-04M 已让 minimum-power modifier 暴露 requested / applied / minimum / resulting metadata；4D-04O 已让 ledger-backed power modifier 暴露 explicit applied order。4D-04P 只把这两层基础收敛到“最小战力与同目标应用顺序”的 representative，不实现完整 LayerEngine。

## 1. 当前事实

- `CoreRuleEngine.ApplyPowerModifier` 仍直接基于当前 `CardObjectState.Power` 计算 `rawResultingPower`，再用 `MinimumPowerAfterModifier` floor 得到 `resultingPower`。
- 非零 `appliedPowerDelta` 会追加 `PowerModifierLedgerEntry`，其中 `PowerDelta` 是实际 applied delta，`RequestedPowerDelta` 保留原请求，`MinimumPower` / `ResultingPower` / `AppliedOrder` 会进入 state、`ContinuousEffectState` 与 snapshot view。
- applied delta 为 0 时不会追加 ledger entry；Extortion representative 已证明 floor 到 0 的路径不会产生 misleading zero ledger 或 fake order。
- Blastcone Sprout representative 已覆盖单个 minimum floor：requested -2，applied -1，minimum 1，resulting 1，appliedOrder 1。
- Power Bind Echo representative 已覆盖同目标双 power modifier order `[1, 2]`，但不包含 minimum floor 参与的同目标 sequential interaction。
- 4D-04O-B 后 P1-001 仍明确保留 `complete minimum-power ordering` residual；现有 evidence 只能证明 minimum metadata 与 ordering metadata 分别成立，尚未证明两者在同目标多修正、floor interaction 与 cleanup 中作为一个序列一起可审计。

## 2. 目标

4D-04P-B 的建议目标是：补一个 narrow representative 或 verifier foundation，证明同一目标上的 minimum floor modifier 与后续/前序 power modifier 在 ledger、continuous effect 和 snapshot view 中保持顺序、数值和 cleanup 一致。

必须保持：

1. 现有 `Power` / `UntilEndOfTurnPowerModifier` arithmetic 不变。
2. `PowerModifierLedgerEntry.PowerDelta` 继续表示 applied delta，不改成 requested delta。
3. requested / applied / minimum / resulting metadata 与 `AppliedOrder` 在 state、`ContinuousEffectState`、snapshot 三处一致。
4. floor 到 applied delta 0 的路径仍不产生 zero ledger entry 或 fake order。
5. `END_TURN` 清理后 ledger-backed minimum / order metadata 一起消失。
6. legacy untracked remainder fallback 仍不伪造 `appliedOrder`。
7. 本切片不得扩大为 timestamp dependency graph、equipment/static aura dependency、keyword gain/loss ordering、full minimum-power ordering 或 full LayerEngine rewrite。

## 3. 建议写锁

建议只在 A 明确 dispatch 后打开 4D-04P-B 写锁。

允许范围：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/**` 中 focused minimum-power / power modifier ordering representatives
- 必要时的最小 fixture / helper / model 调整

禁止范围：

- 前端运行时代码
- card matrix JSON / fullOfficial 状态
- broad PaymentEngine
- battle lifecycle / task queue 语义重写
- wide equipment runtime / static aura rewrite
- 完整 LayerEngine 重算框架
- `riftbound-dotnet.sln`

## 4. 验收要求

4D-04P-B 完成后，A 侧验收至少需要确认：

1. 一个同目标 sequence representative 覆盖 minimum floor 与另一个 power modifier 的组合顺序。
2. state ledger、`ContinuousEffectState`、snapshot view 中的 `requestedPowerDelta`、`appliedPowerDelta`、`minimumPower`、`resultingPower`、`basePower` / `effectivePower` 与 `appliedOrder` 一致。
3. 如果某一步 floor 到 applied delta 0，该步不会产生 misleading zero ledger entry，也不会消耗可观察 applied order。
4. 现有 Blastcone / Extortion / Smoke Bomb / Power Bind Echo / reversed `EffectId` shape / legacy fallback 均不倒退。
5. `END_TURN` cleanup 后 minimum-power order metadata 随 ledger 一起清理。
6. 禁止把本切片声明为 P1-001、P1-002、full official 或 READY closure。

## 5. 实现前基线

实现前基线见 `docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_BASELINE_EVIDENCE.md`。

通过命令：

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~PowerModifierAppliedOrderFollowsPowerBindEchoAppendSequence|FullyQualifiedName~CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack|FullyQualifiedName~CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack|FullyQualifiedName~CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~ContinuousEffectPowerModifierAppliedOrderSurvivesEffectIdNormalization"
```

Result: **7/7 passed**.

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~Extortion|FullyQualifiedName~SmokeBomb|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

Result: **15/15 passed**.

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4449/4449 passed**.

## 6. 暂停点

本 handoff 尚未派发 B worker，也未打开 runtime / test 写锁。当前项目仍 **NOT READY**，P1-001、P1-002、full-card matrix、frontend final validation 与 READY 均未关闭。
