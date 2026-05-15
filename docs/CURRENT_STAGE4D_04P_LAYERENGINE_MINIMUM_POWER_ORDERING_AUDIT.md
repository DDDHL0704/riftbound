# 4D-04P LayerEngine Minimum Power Ordering Audit

日期：2026-05-16
结论：**IMPLEMENTED AND A-VALIDATED / WRITELOCK CLOSED / PROJECT NOT READY**

本文件记录 4D-04P-B 的 A 侧验收。该切片只补同目标 minimum floor 与 applied order 的 representative evidence，未修改 runtime，仍不是完整 LayerEngine。

## 1. 范围

4D-04P-B 允许范围：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/**` 中 focused minimum-power / power modifier ordering representatives
- 必要时的最小 fixture / helper / model

B 实际只触碰：

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-minimum-power-ordering-sequence.fixture.json`

本批未触碰：

- runtime implementation
- 前端运行时代码
- card matrix JSON / fullOfficial 状态
- broad PaymentEngine
- battle lifecycle / task queue 语义重写
- wide equipment runtime / static aura rewrite
- 完整 LayerEngine rewrite
- `riftbound-dotnet.sln`

## 2. 实现事实

- 新增 `p2-preflight-play-minimum-power-ordering-sequence` fixture：Smoke Bomb 先把同一目标从 3 floor 到 1，Extortion 在 floor 上 applied delta 为 0，Power Bind 后续对同一目标 +1。
- 新增 `PowerModifierMinimumPowerAppliedOrderSkipsZeroFloorSequence`，验证同目标 visible ledger entries 只保留 Smoke Bomb 与 Power Bind。
- State ledger 中 Smoke Bomb 与 Power Bind 的 `PowerDelta` 为 `[-2, 1]`，`RequestedPowerDelta` 为 `[-4, 1]`，`MinimumPower` 为 `[1, 0]`，`ResultingPower` 为 `[1, 2]`，`BasePower` 为 `[3, 3]`，`EffectivePower` 为 `[1, 2]`，`AppliedOrder` 为 `[1, 2]`。
- `ContinuousEffectState` 与 snapshot `timing.continuousEffects[]` 暴露与 state ledger 一致的 requested/applied/minimum/resulting/base/effective/order metadata。
- Extortion zero-applied floor path 仍只产生 event payload，不产生 ledger-backed `POWER_MODIFIER` continuous effect，不消耗或伪造 visible `appliedOrder`。
- `CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn` 补强 cleanup assertion：回合结束后 state ledger 为空，`ContinuousEffects` 与 snapshot 不再暴露该 target 的 power modifier view。

## 3. 测试覆盖

新增 / 扩展的代表断言覆盖：

- 同目标 minimum floor + later visible modifier sequence。
- zero-applied floor path 不生成 misleading zero ledger / order。
- state ledger、continuous effect view 与 snapshot view 的 metadata 一致性。
- Smoke Bomb end-turn cleanup 同步清理 ledger/effect/snapshot view。
- 既有 Blastcone、Power Bind Echo、Extortion、Smoke Bomb、continuous effect shape 与 legacy fallback regression 保持绿色。

## 4. A 侧验收命令

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifierMinimumPowerAppliedOrderSkipsZeroFloorSequence|FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~PowerModifierAppliedOrderFollowsPowerBindEchoAppendSequence|FullyQualifiedName~CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack|FullyQualifiedName~CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack|FullyQualifiedName~CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~ContinuousEffectPowerModifierAppliedOrderSurvivesEffectIdNormalization"
```

Result: **8/8 passed**.

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~Extortion|FullyQualifiedName~SmokeBomb|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

Result: **16/16 passed**.

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4450/4450 passed**.

```sh
git diff --check
```

Result: **passed**.

## 5. 残留

4D-04P-B 不关闭以下事项：

- 完整 LayerEngine
- timestamp / dependency graph
- source-ordering breadth
- keyword gain/loss ordering
- multiple equipment/static aura interactions
- complete minimum-power ordering beyond this representative
- P1-002 keyword execution breadth
- full-card matrix official coverage
- frontend final validation
- READY

当前项目仍 **NOT READY**。
