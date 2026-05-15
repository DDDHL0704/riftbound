# Stage 4D-04N LayerEngine Direct Power Ledger Evidence

日期：2026-05-16
结论：**A-VALIDATED / PROJECT NOT READY**

本文件记录 4D-04N-B implementation-after evidence。该 evidence 只证明 selected direct until-end power mutation ledger metadata foundation 通过验收，不代表 full LayerEngine、P1-001、P1-002、frontend final validation、card matrix 或 READY 完成。

## 1. Diff Scope

Accepted files:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

A-side dispatch / audit docs updated separately:

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_04N_LAYERENGINE_DIRECT_POWER_LEDGER_AUDIT.md`
- this file

`riftbound-dotnet.sln` remains untracked and untouched.

## 2. Focused Direct-Power Guard

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LuxHighCostSpellQueuesResolvesAndGainsPowerUntilEndOfTurn|FullyQualifiedName~IcevaleArcherAttackPaymentAcceptedAppliesTemporaryPowerMinusOne|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~P79LegendTriggerRengarGivesUnitPlusOneAfterUnitPlayed|FullyQualifiedName~P4ActivateAbilityCommandResolvesViDoublePowerSkillOnStack|FullyQualifiedName~P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden"
```

Result:

```text
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

## 3. Adjacent Power / Layer / Trigger Regression

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~TriggerPayment|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~Rengar|FullyQualifiedName~ViDoublePower|FullyQualifiedName~EmberMonk|FullyQualifiedName~HasteOptional"
```

Result:

```text
Passed! - Failed: 0, Passed: 185, Skipped: 0, Total: 185
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

- Selected direct until-end power mutation representatives now create ledger-backed metadata instead of relying only on legacy untracked remainder projection.
- Icevale Archer proves trigger-payment direct `-1` state, continuous effect and snapshot metadata.
- Rengar proves direct `+1` state/snapshot metadata and end-turn ledger cleanup.
- Ember Monk, conquest +8, battlefield moved +1, optional ready power and Vi double power now share the same direct helper.
- Existing public `power`, `basePower`, `effectivePower`, `powerDelta` and `continuousEffects` compatibility remains green.
- Full timestamp/dependency/source ordering, keyword gain/loss, multi-equipment/static aura layering, complete minimum-power ordering and unselected direct/static/replacement breadth remain deferred.

Project remains **NOT READY**.
