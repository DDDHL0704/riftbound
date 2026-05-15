# 4D-04Q LayerEngine Static Aura Source Lifecycle Baseline Evidence

日期：2026-05-16
结论：**A-SIDE BASELINE RECORDED / PROJECT NOT READY**

本文件保存 4D-04Q handoff 的实现前基线。它证明当前 Ornn dynamic static recompute、battlefield static power、continuous-effect power modifier view 与 equipment keyword profile guard 都保持绿色；它不证明 static aura source / lifecycle metadata 已实现，也不证明完整 LayerEngine、P1-001、P1-002、frontend final validation 或 READY。

## 1. Baseline Gap

- Ornn 已有 authoritative recompute：`ApplyFriendlyEquipmentStaticPowerRecompute` 会在 accepted core command 后，用 registered source unit power + 当前友方公开 field equipment count + until-end modifier 重算 Ornn。
- Battlefield static power 已有 combat payload evidence：`P79BattlefieldStaticPowerAddsOneToBattleParticipants` 断言 `staticPowerBonus` 与 `combatPower`。
- 当前 `ContinuousEffectState` 只覆盖 until-end rule text 与 until-end power modifier ledger；dynamic static aura / equipment static source lifecycle 仍不是稳定 server snapshot view。
- 因此下一 B 切片需要在不改变 arithmetic 的前提下，补 source / lifecycle audit foundation，而不是重写完整 LayerEngine。

## 2. Commands

Focused static-aura / LayerEngine-view guard:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **10/10 passed**.

Adjacent static / continuous-effect / equipment regression:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~BattlefieldStaticPower|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~LayerEngine|FullyQualifiedName~EquipmentKeyword"
```

Result: **48/48 passed**.

Backend full:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4450/4450 passed**.

## 3. Not Proved

- static aura source / lifecycle metadata implementation
- complete continuous-effect LayerEngine
- timestamp / dependency graph
- all equipment static modifiers
- full `百炼` breadth
- owner/controller breadth and attach lifecycle breadth beyond existing representatives
- frontend final validation
- card matrix full-official coverage
- READY / active goal completion
