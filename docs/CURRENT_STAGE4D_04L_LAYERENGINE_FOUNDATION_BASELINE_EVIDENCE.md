# Stage 4D-04L LayerEngine Foundation Baseline Evidence

日期：2026-05-15
结论：**BASELINE RECORDED / PROJECT NOT READY**

本文件记录 4D-04L handoff 的 implementation-before baseline。此 baseline 只证明当前 continuous-effect snapshot view、until-end power modifier representatives、Ornn dynamic recompute 和 adjacent power/layer tests 在最新工作树中为绿色，不代表 4D-04L 已实现，不关闭 P1-001、P1-002、full LayerEngine、card matrix、frontend final validation 或 READY。

## 1. Scope

Focused baseline 覆盖：

- `ContinuousEffectState` / `basePower` / `effectivePower` snapshot view。
- pending task queue 不把 undamaged zero effective power 误暴露为 state-based task。
- turn-end cleanup 对 negative base power + positive modifier 的恢复。
- Switcheroo 双目标 until-end power swap representative。
- battle response activation 后 assignment damage pool 使用 effective power representative。
- Ornn entry-time and dynamic friendly-equipment static recompute representatives。

Adjacent baseline 覆盖：

- Continuous-effect / power-modifier named tests。
- Switcheroo regressions。
- Ornn regressions。
- Trigger payment power-modifier regressions。
- Battle damage assignment lifecycle regressions.

This baseline does not cover full timestamp ordering, dependency resolution, complete equipment/static aura layering, keyword gain/loss layering, full PaymentEngine, full battle lifecycle, frontend final validation, card matrix completion, or READY.

## 2. Commands And Results

Focused LayerEngine baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~PendingTaskQueueDoesNotExposeUndamagedZeroPowerFromPowerModifierAsStateBasedTask|FullyQualifiedName~TurnEndCleanupRestoresNegativeBasePowerAfterPositiveModifierExpires|FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn|FullyQualifiedName~NaturalBattleResponseActivationPowerModifierUsesEffectiveAssignmentDamagePool|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests"
```

Result:

```text
Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11
```

Adjacent power / layer / equipment regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~Switcheroo|FullyQualifiedName~Ornn|FullyQualifiedName~TriggerPayment|FullyQualifiedName~BattleDamageAssignmentLifecycleTests"
```

Result:

```text
Passed! - Failed: 0, Passed: 141, Skipped: 0, Total: 141
```

## 3. Baseline Interpretation

- Current layer-facing snapshot/report behavior is green.
- Existing until-end power modifier arithmetic and cleanup representatives are green.
- Existing Ornn static and dynamic recompute representatives are green.
- This is still weaker than a full official LayerEngine because current state mutates `Power` and derives base power from `Power - UntilEndOfTurnPowerModifier`.

## 4. Closure

4D-04L is handoff-ready only. No runtime, test, frontend or matrix change has been made by this baseline batch. Project remains **NOT READY**.
