# Stage 4D-04M LayerEngine Minimum-Power Ledger Baseline Evidence

日期：2026-05-15
结论：**BASELINE RECORDED / PROJECT NOT READY**

本文件记录 4D-04M handoff 的 implementation-before baseline。此 baseline 只证明当前 minimum-power floor representatives、4D-04L source-aware ledger foundation 和 adjacent power/layer/minimum tests 在最新工作树中为绿色，不代表 4D-04M 已实现，不关闭 P1-001、P1-002、full LayerEngine、card matrix、frontend final validation 或 READY。

## 1. Scope

Focused baseline 覆盖：

- Blastcone Sprout floor representative。
- Siphon Energy split all-battlefield floor representative。
- Thousand-Tailed Watcher all enemy units / HASTE_READY floor representative。
- Smoke Bomb floor through stack and turn-end expiry representative。
- Extortion floor + draw representative。
- 4D-04L `ContinuousEffectState` / source-aware power modifier ledger guard。

Adjacent baseline 覆盖：

- PowerModifier / UntilEndOfTurnPowerModifier named tests。
- Continuous-effect snapshot/report tests。
- Minimum-power named representatives.
- Blastcone / SiphonEnergy / ThousandTailed / SmokeBomb / Extortion fixture paths.

This baseline does not cover complete timestamp ordering, dependency resolution, complete equipment/static aura layering, keyword gain/loss layering, complete minimum-power ordering across layers, full PaymentEngine, full battle lifecycle, frontend final validation, card matrix completion, or READY.

## 2. Commands And Results

Focused minimum-power foundation baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~CoreRuleEnginePlaysSiphonEnergyBattlefieldPowerSplit|FullyQualifiedName~CoreRuleEnginePlaysThousandTailedWatcherAllEnemyUnitsMinus3|FullyQualifiedName~CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack|FullyQualifiedName~CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn|FullyQualifiedName~CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn"
```

Result:

```text
Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9
```

Adjacent power / layer / minimum regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~SiphonEnergy|FullyQualifiedName~ThousandTailed|FullyQualifiedName~SmokeBomb|FullyQualifiedName~Extortion|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~UntilEndOfTurnPowerModifier"
```

Result:

```text
Passed! - Failed: 0, Passed: 16, Skipped: 0, Total: 16
```

Diff hygiene:

```sh
git diff --check
```

Result:

```text
passed
```

## 3. Baseline Interpretation

- Current minimum-power floor arithmetic representatives are green.
- Current 4D-04L source/effect metadata guard is green.
- Existing event payloads already expose requested delta, applied delta, minimum power and resulting power, but ledger-backed continuous-effect metadata does not yet preserve all of those values.
- This is still weaker than full official LayerEngine because current state mutates `Power`, derives base power from aggregate modifier state, and has no timestamp/dependency/minimum-power layer ordering model.

## 4. Closure

4D-04M is handoff-ready only. No runtime, test, frontend or matrix change has been made by this baseline batch. Project remains **NOT READY**.
