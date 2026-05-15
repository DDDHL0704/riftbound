# 4D-04Q LayerEngine Static Aura Source Lifecycle Handoff

日期：2026-05-16
结论：**HANDOFF READY / NOT DISPATCHED / PROJECT NOT READY**

本文件是 A 主控为 P1-001 建立的下一枚窄实现交接单。4D-04L 到 4D-04P 已把 until-end power modifier 的 source / effect / direct-path / requested / applied / minimum / resulting / order metadata 打通；4D-04Q 不继续扩大 minimum-power 顺序 representative，而是把下一个阻断拆到静态光环 / 装备静态修正的 source 与 lifecycle 可审计基础。

## 1. 当前事实

- `src/Riftbound.Engine/CoreRuleEngine.cs` 的 `ApplyFriendlyEquipmentStaticPowerRecompute` 已在 accepted core command 后，针对 registry 标记 `AddsFriendlyFieldEquipmentCountToSourceUnitPower` 的公开 field 单位窄重算 Ornn：registered source unit power + 当前 controller 友方公开 field equipment count + until-end modifier。
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs` 已覆盖 Ornn 入场、后续装备进入 / 离开、hand / enemy / face-down / dirty-controller / non-equipment exclusion，以及重复 accepted command 不漂移。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 的 `P79BattlefieldStaticPowerAddsOneToBattleParticipants` 已证明 battlefield static power 当前会进入 combat damage payload 的 `staticPowerBonus` / `combatPower`。
- `src/Riftbound.Engine/MatchSession.cs` 现有 `ContinuousEffectState` 只投影 global / object until-end rule text 与 until-end power modifier ledger；Ornn dynamic static recompute 与 battlefield static power 仍不作为 static aura source / lifecycle continuous-effect view 暴露。
- 因此前端 / 审计目前能读到最终 authoritative `power`、`basePower`、`effectivePower` 与 battle damage payload，但不能从 server snapshot 稳定审计“哪个静态来源在何时、对哪个对象、因什么 lifecycle 条件生效 / 失效”。

## 2. 目标

下一枚 B 切片建议只做 P1-001 foundation：为 dynamic static aura / equipment static representative 增加 source / lifecycle 可审计基础，优先绑定 Ornn friendly-equipment static recompute 与 battlefield static power 两个现成代表锚点。

必须保持：

1. 现有 Ornn arithmetic、battlefield static combat power arithmetic 与事件 payload 不变。
2. 服务端仍是唯一规则权威；前端只能显示 authoritative snapshot / event payload，不本地统计装备、重算静态光环或推断 source lifecycle。
3. 如果新增 snapshot / continuous-effect view，它只能标记为 foundation / representative，不得声明完整 LayerEngine。
4. source leaves field、条件不再满足、controller / visibility 变化时，view 必须能消失或更新，不能留下 stale static aura metadata。
5. existing 4D-04L-P until-end modifier ledger / minimum / order metadata 不倒退。
6. 本切片不得扩大为完整 timestamp dependency graph、完整 static replacement engine、所有装备静态修正、full `百炼`、P1-002 keyword breadth 或 full LayerEngine rewrite。

## 3. 建议写锁

建议只在 A 明确 dispatch 后打开 4D-04Q-B 写锁。

允许范围：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 中 focused static aura / battlefield static representative
- 必要时的最小 model / snapshot helper 调整

禁止范围：

- 前端运行时代码
- card matrix JSON / fullOfficial 状态
- broad PaymentEngine
- battle lifecycle / task queue 语义重写
- wide equipment runtime / full `百炼` breadth
- 完整 LayerEngine / timestamp dependency graph rewrite
- `riftbound-dotnet.sln`

## 4. 验收要求

4D-04Q-B 完成后，A 侧验收至少需要确认：

1. Ornn dynamic friendly-equipment static recompute 的 source / target / condition / lifecycle metadata 可由服务端权威视图审计。
2. Battlefield static power representative 的 source / participant / battlefield lifecycle metadata 可由服务端权威视图或等价 audit payload 审计。
3. source 或条件失效后，metadata 不会 stale；existing `power` / `combatPower` arithmetic 不被双计。
4. existing Ornn, battlefield static power, `ContinuousEffectState`, power modifier, minimum-power 与 applied-order representatives 仍保持绿色。
5. 禁止把本切片声明为 P1-001、P1-002、full official 或 READY closure。

## 5. 实现前基线

实现前基线见 `docs/CURRENT_STAGE4D_04Q_LAYERENGINE_STATIC_AURA_SOURCE_LIFECYCLE_BASELINE_EVIDENCE.md`。

通过命令：

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **10/10 passed**.

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~BattlefieldStaticPower|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~LayerEngine|FullyQualifiedName~EquipmentKeyword"
```

Result: **48/48 passed**.

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4450/4450 passed**.

## 6. 暂停点

本 handoff 尚未派发 B worker，也未打开 runtime / test 写锁。当前项目仍 **NOT READY**，P1-001、P1-002、full-card matrix、frontend final validation 与 READY 均未关闭。
