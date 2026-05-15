# 4D-04O LayerEngine Power Modifier Ordering Evidence

日期：2026-05-16
结论：**A-VALIDATED / GREEN / PROJECT NOT READY**

## 1. Changed Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/SwitcherooGuardTests.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`

## 2. Evidence Summary

- Ledger-backed until-end power modifiers now carry explicit nullable `AppliedOrder`.
- Continuous effect state and snapshot `timing.continuousEffects[]` project `appliedOrder` only for ordered ledger-backed entries.
- Same-target multiple modifier order is tested with Power Bind Echo and remains `[1, 2]`.
- A deliberately reversed `EffectId` shape test proves normalization does not sort ordered entries by lexicographic effect id.
- Legacy untracked power modifier view remains compatible and does not expose `appliedOrder`.

## 3. A-Side Commands

Focused ordering guard:

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

Result: **passed 39/39**.

Backend full:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **passed 4449/4449**.

Diff hygiene:

```sh
git diff --check
```

Result: **passed**.

## 4. Not Closed

This evidence does not close full LayerEngine, timestamp dependency graph, keyword/equipment layer ordering, card matrix fullOfficial status, frontend final validation, or READY.
