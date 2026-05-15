# Stage 4D-04N LayerEngine Direct Power Ledger Baseline Evidence

日期：2026-05-16
结论：**BASELINE RECORDED / HANDOFF READY / PROJECT NOT READY**

本文记录 4D-04N direct until-end power mutation ledger handoff 的实现前基线。当前测试只证明代表路径仍可按旧 arithmetic 运行，不证明 direct paths 已有 ledger metadata。

## 1. Focused Direct-Power Baseline

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LuxHighCostSpellQueuesResolvesAndGainsPowerUntilEndOfTurn|FullyQualifiedName~IcevaleArcherAttackPaymentAcceptedAppliesTemporaryPowerMinusOne|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~P79LegendTriggerRengarGivesUnitPlusOneAfterUnitPlayed|FullyQualifiedName~P4ActivateAbilityCommandResolvesViDoublePowerSkillOnStack|FullyQualifiedName~P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden"
```

Result: **Passed 6/6**.

Interpretation: Icevale Archer, Ember Monk, Thousand-Tailed Watcher optional ready power, Rengar +1, Vi double-power and adjacent high-cost trigger representatives remain executable before 4D-04N implementation. These tests currently do not prove ledger-backed metadata for direct mutation paths.

## 2. Adjacent Regression Baseline

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~TriggerPayment|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~Rengar|FullyQualifiedName~ViDoublePower|FullyQualifiedName~EmberMonk|FullyQualifiedName~HasteOptional"
```

Result: **Passed 185/185**.

Interpretation: Existing continuous-effect, power-modifier, trigger-payment, Rengar, Vi, Ember Monk and Haste optional-ready coverage is green before direct ledger implementation.

## 3. Backend Full Baseline

Command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **Passed 4447/4447**.

## 4. Diff Check

Command:

```sh
git diff --check
```

Result: **Passed**.

## 5. Worktree Constraint

`riftbound-dotnet.sln` remains untracked and untouched. This baseline does not open a B write lock and does not modify runtime, tests, frontend, card matrix JSON, fullOfficial / READY status, or the active goal completion state.
