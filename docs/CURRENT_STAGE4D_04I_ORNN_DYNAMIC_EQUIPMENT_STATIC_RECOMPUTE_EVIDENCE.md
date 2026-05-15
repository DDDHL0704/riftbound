# Stage 4D-04I Ornn Dynamic Equipment Static Recompute Evidence

日期：2026-05-15
结论：**A-SIDE EVIDENCE RECORDED / PROJECT NOT READY**

本文件保存 4D-04I-B 的验收证据。它证明 Ornn dynamic friendly-equipment static recompute representative 已有服务端实现和自动化覆盖；它不证明完整 LayerEngine、完整 `百炼`、full-card matrix、frontend final validation 或 READY。

## 1. Implementation Summary

- B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 完成实现，A 侧复核并验收。
- `CoreRuleEngine.ResolveAsync` 的 accepted core-command result 会进入窄 `ApplyFriendlyEquipmentStaticPowerRecompute`。
- 该 recompute 只处理已在公开 field 且 registry 标记 `AddsFriendlyFieldEquipmentCountToSourceUnitPower` 的单位。
- Ornn 以 registered source unit power 为稳定 base，重新加上当前 controller 友方公开 field equipment count 与已有 until-end power modifier。
- Result state 改变时会重建 authoritative snapshots / prompts。

## 2. Test Coverage Added

- Ornn 已在 field，之后友方公开 equipment 从手牌打出并结算进 field：power 4 -> 5。
- Ornn 由两个合法 equipment 的状态降到一个合法 equipment 后：power 6 -> 5。
- 重复 accepted command 不会把 4D-04H entry-time bonus 再累加。
- Hand equipment、enemy equipment、face-down equipment、dirty-controller equipment 与 non-equipment objects 不计入 dynamic recompute。
- Snapshot `power` / `basePower` / `effectivePower` 与当前 authoritative state 一致。

## 3. Commands

Focused / keyword / LayerEngine-view guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **9/9 passed**.

Adjacent equipment / payment regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

Result: **117/117 passed**.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **4446/4446 passed**.

Patch hygiene:

```sh
git diff --check
```

Result: **passed**.

## 4. Not Proved

- complete continuous-effect LayerEngine
- full `百炼` breadth
- every equipment static modifier
- owner/controller breadth
- attach lifecycle breadth
- frontend final validation
- card matrix full-official coverage
- READY / active goal completion
