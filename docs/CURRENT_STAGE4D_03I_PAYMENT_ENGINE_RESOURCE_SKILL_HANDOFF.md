# Stage 4D-03I PaymentEngine Resource Skill Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03I 的 B 侧实现交接范围。A 主控只记录官方候选、当前代码边界、写入范围和验收门槛；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

把一个具体、官方、支付资源技能代表路径接入 shared PaymentEngine 口径，优先覆盖 `[A]` resource skill 缺口。

首选候选为 OGN 玛尔扎哈：

- `OGN·113/298`，卡名 `玛尔扎哈` / `Malzahar`，cardId `31332`，FU `FU-0f7cbe26ce`。
- 官方文本：摧毁一个友方单位或装备，横置；迅捷获得 `A A`，用以支付符能费用；可在己方回合或法术对决中使用；获得费用资源的技能无法成为其他法术的反应目标。
- 现有 design gate：`docs/CURRENT_STAGE4C_BATCH88_MALZAHAR_RESOURCE_SKILL_DESIGN_GATE.md`。
- 现有 preflight fixture：`tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-malzahar-tap-rune-static.fixture.json`，当前只覆盖普通手牌打出，并明确把横置、摧毁友方对象、迅捷资源获得和反应限制路径暂缓。

本切片目标不是补完所有 `[A]` / `[C]` resource skills、swift / spell-duel timing 或完整 reaction prohibition。目标是先建立一个可靠的 open-main representative：服务端 prompt 暴露 Malzahar source 与合法友方单位/装备成本对象，命令侧摧毁该成本对象、横置来源、获得 payment-only `A A` 资源，并记录可审计 payload。

## 2. 当前代码事实

- `P4ActivatedAbilityCatalog` 当前只有 Vi 与 Xerath 的已实现代表能力；`P4DeferredActivatedAbilitySurface` 记录了若干 deferred activated ability surfaces，但没有 Malzahar resource skill definition。
- `CoreRuleEngine.ResolveActivateAbility` 当前只支持 battlefield experience ability、Xerath 分支和 Vi-like no-target paid skill 分支；Malzahar 命令会落入 unsupported ability 或不匹配现有能力。
- `MatchSession.ActivateAbilitySourceRequirements` 目前只会为 `P4ActivatedAbilityCatalog.GetAll()` 中的 Vi / Xerath 等已实现能力生成 source requirements；不会暴露 Malzahar source 或 destroy-cost target choices。
- `CardBehaviorRegistry` 只登记 `OGN·113/298` 普通打出路径：费用 4、0 目标、结算后作为 3-power 单位进入控制者基地。
- 4D-03D 已让 Vi / Xerath `ACTIVATE_ABILITY` 支持 `RECYCLE_RUNE:*` payment resource action；4D-03I 不应回退这些 prompt quote / command commit / audit 语义。
- 4D-03H 已让 SFD Fiora trigger payment resource action 接入 `TRIGGER_PAYMENT` / `PAY_COST`；本切片不应改动该触发支付窗口。

## 3. 建议写入范围

建议 owner：B / Maxwell。

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 必要时补充窄域 `ActivateAbility` / resource skill 测试文件

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- `src/Riftbound.Engine/PaymentCostRules.cs`，除非实现中证明现有 plan / commit 接口无法表达 payment-only resource gain
- 未跟踪文件 `riftbound-dotnet.sln`

## 4. 实现要求

### 4.1 Prompt / snapshot

- `ACTIVATE_ABILITY.sourceRequirements` 应只向控制者暴露当前合法的 Malzahar source。
- source 必须是控制者的公开、正面、场上或基地单位对象，`cardNo = OGN·113/298`，且未横置。
- prompt 应暴露一个成本对象选择槽：友方单位或装备，必须公开、正面、由同一玩家控制，位于可验证公共区域，不得是 Malzahar source 自身。
- prompt metadata 应标明这是 resource skill / payment-only resource gain，而不是普通 stack damage / buff skill。
- 对手 snapshot 不得泄漏隐藏牌、face-down 待命或未知对象身份。

### 4.2 命令校验

- 拒绝 unsupported ability id、错误 source cardNo、非控制者 source、source 非公开正面对象、source 已横置、source 不在合法区域。
- 拒绝缺少、重复或非法 destroy-cost target。
- 拒绝目标为 source 自身、敌方对象、未知对象、隐藏对象、非单位/非装备对象、错误区域对象。
- 第一切片只开放 open-main / neutral-open 代表路径；若 B 暂不实现 spell-duel / swift window，应显式不在 prompt 中开放，并用测试锁住。
- 错误路径必须 rejected 且 no mutation：tick、runePool、zones、cardObjects、stack、pending payments 不变。

### 4.3 成功路径

- source Malzahar 横置。
- destroy-cost object 进入 owner graveyard，`ObjectLocations` 与 `PlayerZones` 一致更新。
- 控制者获得 2 点 payment-only generic power resource，并以事件 payload 区分：
  - `paymentWindow = ACTIVATE_ABILITY`
  - `abilityId`
  - `sourceObjectId`
  - `destroyedCostObjectId`
  - `resourceSkill = true`
  - `paymentOnly = true`
  - `generatedPower = 2`
- 若采用 `RunePool.Power` 临时表达 payment-only `A A`，必须记录 restriction metadata，并在文档和事件中明确该资源当前只作为 4D-03I representative，不等同完整 resource restriction lifecycle。
- 该技能不能作为普通可反应 stack item 打开；若实现为立即结算，应有测试证明不会创建可被普通反应命中的 stack item。

### 4.4 与 PaymentEngine 的关系

- 本切片应尽量复用 4D-03 的 payment reason / audit vocabulary，让后续 `[A]` / `[C]` resource skills 可以接入统一 quote / authorize / commit。
- 不要为了 Malzahar 特化破坏 Vi / Xerath `ACTIVATE_ABILITY` payment resource action、`LEGEND_ACT`、ordinary pending `PAY_COST`、battlefield held score 或 SFD Fiora trigger payment。
- 若本切片只完成 resource generation，而不把生成的 `A A` 立即限定到同一支付步骤消费，则必须把该限制作为 explicit residual risk 保留给后续 4D-03J。

## 5. 必补测试

Focused tests 建议覆盖：

- Malzahar 普通打出 fixture 继续通过，且不因新增能力改变普通 play route。
- open-main prompt 暴露 Malzahar `ACTIVATE_ABILITY` source 和合法友方单位/装备 cost target。
- 成功提交后：source 横置，cost object 进 owner graveyard，控制者获得 2 generic payment-only power，事件 payload 可审计，不创建普通 stack item。
- source 已横置、source 非 Malzahar、source 非控制者、source 非公开正面、source 不在合法区域均 rejected no-mutation。
- 缺少 / 多个 / 敌方 / source 自身 / 隐藏 / 未知 / 非单位装备 cost target 均 rejected no-mutation。
- 当前第一切片若不实现 spell-duel / swift window，必须证明 spell-duel timing 不暴露该 ability。
- 既有 Vi / Xerath `ACTIVATE_ABILITY`、4D-03D payment resource action、4D-03H trigger payment resource action 回归继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Malzahar|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~SpellDuel|FullyQualifiedName~Priority"
```

实现通过后由 A 决定是否再跑 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. No-Go Criteria

- 不要把 Malzahar 的 `A A` 简化为无约束永久符能并宣称 full official；若用 `RunePool.Power` 表达，必须显式记录限制缺口。
- 不要在没有 timing 裁定和测试的情况下开放 spell-duel / swift 使用窗口。
- 不要把资源技能放入普通可反应 stack item，除非同时实现“获得费用资源的技能无法成为其他法术反应目标”的完整规则。
- 不要升级 `FU-0f7cbe26ce` 到 full-official 或修改 coverage matrix。
- 不要实现完整 `[A]` / `[C]` resource skill family、LayerEngine、reaction payment windows 或前端本地推断。
- 不要因为本切片通过而关闭 P0-005 或 active goal。

## 8. A 侧结论

4D-03I 是 P0-005 full PaymentEngine breadth 的下一枚窄切片。它只用 Malzahar 的 `[A A]` resource skill 代表路径验证服务端 resource skill quote / command / audit surface，不关闭完整 PaymentEngine、完整 reaction timing、完整 `[A]` / `[C]` family、1009/811 full-official 证据或最终 READY。
