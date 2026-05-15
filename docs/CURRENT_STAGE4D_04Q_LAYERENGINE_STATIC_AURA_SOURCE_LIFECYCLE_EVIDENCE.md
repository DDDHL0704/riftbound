# Stage 4D-04Q LayerEngine Static Aura Source Lifecycle Evidence

日期：2026-05-16
结论：**A-SIDE EVIDENCE RECORDED / PROJECT NOT READY**

本文件保存 4D-04Q-B 的验收证据。它证明 static aura source lifecycle foundation 已有服务端视图和自动化覆盖；它不证明完整 LayerEngine、完整 timestamp dependency graph、P1-001、P1-002、frontend final validation、card matrix full-official 或 READY。

## 1. Implementation Summary

- B-Implementation / Euclid `019e2caf-92c5-7502-8db3-b091e443ad3c` 完成实现，A 侧复核并验收。
- `MatchSession.cs` 新增 `ContinuousEffectLayers.StaticAura` 和 `ContinuousEffectStaticAuraCards.BattlefieldAllUnitsPowerPlusOneCardNo`。
- `ContinuousEffectState` 新增 nullable `Condition`、`Lifecycle`、`ParticipantObjectIds`，snapshot `timing.continuousEffects[]` 会在存在时暴露 `condition`、`lifecycle`、`participantObjectIds`。
- `BuildContinuousEffectStates` 现在会派生 Ornn friendly-equipment static aura 和 battlefield all-units +1 static aura foundation view。
- Ornn view 使用 authoritative current state 的 source / target / friendly public field equipment participants，不改变既有 power arithmetic。
- Battlefield view 使用 current battlefield source and participants，不改变既有 `staticPowerBonus` / `combatPower` payload arithmetic。

## 2. Test Coverage Added

- Ornn 入场时有两个友方公开 field equipment：state continuous effect 暴露 source/target、participants、condition、lifecycle、powerDelta 2、base 4、effective 6。
- Ornn 无友方公开 field equipment：static aura metadata 仍可审计 source/lifecycle，participant list 为空，powerDelta 0。
- Ornn 后续装备进入/离开或 exclusion 变化后，snapshot `timing.continuousEffects[]` 的 static aura metadata 与 authoritative power 一致。
- Ornn source 离开 public field 后，不再保留 stale static aura metadata。
- Battlefield `OGN·294/298` all-units +1 representative 在战斗前暴露两个 participant 的 battlefield static aura metadata，snapshot 也可读取。
- 战斗结算导致 participants 入墓后，不再保留 stale battlefield static aura metadata。

## 3. Commands

Focused static-aura / LayerEngine-view guard:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **11/11 passed**.

Adjacent static / continuous-effect / equipment regression:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~BattlefieldStaticPower|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~LayerEngine|FullyQualifiedName~EquipmentKeyword"
```

Result: **49/49 passed**.

Backend full:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4451/4451 passed**.

Patch hygiene:

```sh
git diff --check
```

Result: **passed**.

## 4. Not Proved

- complete continuous-effect LayerEngine
- timestamp / dependency graph
- all equipment static modifiers
- full `百炼` breadth
- complete battlefield-location dependency model
- frontend final validation
- card matrix full-official coverage
- READY / active goal completion
