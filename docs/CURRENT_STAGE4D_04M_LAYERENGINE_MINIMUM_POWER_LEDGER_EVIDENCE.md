# Stage 4D-04M LayerEngine Minimum-Power Ledger Evidence

日期：2026-05-15
结论：**A-VALIDATED / PROJECT NOT READY**

本文件记录 4D-04M-B implementation-after evidence。该 evidence 只证明 minimum-power power modifier ledger metadata foundation 通过验收，不代表 full LayerEngine、P1-001、P1-002、frontend final validation、card matrix 或 READY 完成。

## 1. Diff Scope

Accepted files:

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

A-side dispatch / audit docs updated separately:

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_AUDIT.md`
- this file

`riftbound-dotnet.sln` remains untracked and untouched.

## 2. Focused Minimum-Power Foundation Guard

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~CoreRuleEnginePlaysSiphonEnergyBattlefieldPowerSplit|FullyQualifiedName~CoreRuleEnginePlaysThousandTailedWatcherAllEnemyUnitsMinus3|FullyQualifiedName~CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack|FullyQualifiedName~CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn|FullyQualifiedName~CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn"
```

Result:

```text
Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9
```

## 3. Adjacent Power / Layer / Minimum Regression

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~SiphonEnergy|FullyQualifiedName~ThousandTailed|FullyQualifiedName~SmokeBomb|FullyQualifiedName~Extortion|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~UntilEndOfTurnPowerModifier"
```

Result:

```text
Passed! - Failed: 0, Passed: 16, Skipped: 0, Total: 16
```

## 4. Backend Full Test

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed! - Failed: 0, Passed: 4447, Skipped: 0, Total: 4447
```

## 5. Diff Hygiene

Command:

```sh
git diff --check
```

Result:

```text
passed
```

## 6. Interpretation

- Current minimum-power power modifier representatives now have ledger-backed requested/applied/minimum/resulting metadata.
- Existing public `power`, `basePower`, `effectivePower`, `powerDelta` and `continuousEffects` compatibility remains green.
- Extortion applied-zero floor path remains no-mutation compatible and does not create a misleading zero-delta continuous effect view.
- The snapshot view keeps `FOUNDATION_ONLY` and deferred LayerEngine residuals visible.
- Full timestamp/dependency/source ordering, keyword gain/loss, multi-equipment/static aura layering and complete minimum-power ordering remain deferred.

Project remains **NOT READY**.
