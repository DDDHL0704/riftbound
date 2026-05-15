# 4D-04P LayerEngine Minimum Power Ordering Evidence

日期：2026-05-16
结论：**A-VALIDATED / GREEN / PROJECT NOT READY**

## 1. Changed Files

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-minimum-power-ordering-sequence.fixture.json`

## 2. Evidence Summary

- Added a focused fixture where Smoke Bomb floors a target from 3 to 1, Extortion applies zero visible delta at the floor, and Power Bind later gives the same target +1.
- State ledger, `ContinuousEffectState`, and snapshot view all expose matching requested/applied/minimum/resulting/base/effective/order metadata for the two visible modifiers.
- Extortion zero-applied floor path remains event-visible but does not create a misleading zero-delta ledger entry or consume visible `appliedOrder`.
- Smoke Bomb end-turn cleanup now explicitly asserts that state ledger, `ContinuousEffects`, and snapshot continuous effects no longer expose the expired power modifier.
- No runtime code was changed.

## 3. A-Side Commands

Focused minimum-power ordering guard:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifierMinimumPowerAppliedOrderSkipsZeroFloorSequence|FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~PowerModifierAppliedOrderFollowsPowerBindEchoAppendSequence|FullyQualifiedName~CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack|FullyQualifiedName~CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack|FullyQualifiedName~CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~ContinuousEffectPowerModifierAppliedOrderSurvivesEffectIdNormalization"
```

Result: **passed 8/8**.

Adjacent minimum / ordering / continuous-effect regression:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~Extortion|FullyQualifiedName~SmokeBomb|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

Result: **passed 16/16**.

Backend full:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **passed 4450/4450**.

Diff hygiene:

```sh
git diff --check
```

Result: **passed**.

## 4. Not Closed

This evidence does not close full LayerEngine, timestamp dependency graph, keyword/equipment layer ordering, complete minimum-power ordering beyond this representative, card matrix fullOfficial status, frontend final validation, or READY.
