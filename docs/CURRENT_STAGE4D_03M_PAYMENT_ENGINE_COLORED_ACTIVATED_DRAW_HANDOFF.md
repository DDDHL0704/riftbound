# Stage 4D-03M PaymentEngine Colored Activated Draw Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03M 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03L 已用 `UNL-093/219` 龙魂贤者建立 reaction-speed no-target resource skill representative。4D-03M 继续收窄 P0-005 PaymentEngine breadth，选择一个更小的 colored-cost ordinary activated ability representative：

- `SFD·088/221` / `SFD·088a/221`，卡名 `烈娜塔·戈拉斯克` / `Renata Glasc`。
- 当前 deferred surface：`DEFERRED_PAY_1_BLUE_DRAW_1`。
- 官方文本锚点：`支付{{1}}和{{蓝色}}：抽一张牌`。
- 同功能卡组：`SFD·088/221` 与异画 `SFD·088a/221` 的官方文本相同，矩阵 `functionalUnitId = FU-3185396ef9`、`functionalRepresentativeNo = SFD·088/221`。
- 现有普通入场证据：`p2-preflight-play-sfd-088-renata-glasc-activated-skill-unit`、`p2-preflight-play-sfd-088-renata-glasc-alt-a-activated-skill-unit` 及对应 target rejected fixtures。

本切片只处理 Renata draw skill：支付 1 mana 与 1 blue typed power，创建普通可响应 stack item，结算时抽 1 张牌。不要同时实现 Renata score skill、Crimson Rose ready-unit、Shadow swift stun、Fluft Poro Warhawk token creation 或完整 activated ability family。

## 2. 当前代码事实

- `P4ActivatedAbilityCatalog.GetAll()` 当前执行 Vi、Xerath、Malzahar 与 Dragon Soul Sage 四条 representative ability。
- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 仍登记 `DEFERRED_PAY_1_BLUE_DRAW_1`，并标注 `RequiresBattlefieldSource: true`、`IsTargetBearing: false`、`EnemySpellshieldTaxRisk: false`。
- `P4DeferredActivatedAbilityOfficialTextAnchor("renata-glasc-draw")` 已用官方文本锚点审计 Renata draw skill 仍 deferred。
- `CoreRuleEngine.ResolveActivateAbility` 当前遇到未登记的 Renata draw ability id 会 rejected / unsupported，不应推进 tick、支付资源、创建 stack item 或抽牌。
- `MatchSession` 不会为 Renata 生成 draw skill 的 `ACTIVATE_ABILITY.sourceRequirements`。
- `PaymentCostRules.PaymentPlan` 已支持 `powerCostByTrait`，普通 pending `PAY_COST` 已覆盖 typed power + `RECYCLE_RUNE:*` payment resource action；4D-03M 应复用该模型，不新增并行费用逻辑。

## 3. 建议写入范围

建议 owner：B 服务端规则 / 协议 / 测试实现。

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 必要时新增窄域 `ColoredActivatedAbilityTests.cs` 或 `RenataActivatedAbilityTests.cs`

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- Renata score skill
- Crimson Rose / Shadow / Fluft Poro deferred ability 实现
- 未跟踪文件 `riftbound-dotnet.sln`

不可并行文件：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

## 4. 实现要求

### 4.1 Catalog / Prompt

- 将 Renata draw skill 从 deferred-only surface 移入 executable activated ability catalog，或增加等价 executable definition；审计说明必须保留：4D-03M 只开放 draw representative。
- 支持 `SFD·088/221` 与 `SFD·088a/221` 两个同功能官方 collector。若实现沿用单 `SourceCardNo` 字段，必须以 helper / alias list 固定两者等价，不得只让异画落入未知行为。
- prompt 只在服务端确认普通开放主阶段合法时公开 `ACTIVATE_ABILITY` source requirement：`Phase=MAIN`、`TimingState=NEUTRAL_OPEN`、当前玩家为 active player、stack 为空、无 blocking pending payment / hand choice / task。
- source 必须由当前玩家控制、公开、正面、位于 battlefield。官方文本写明“只有我位于战场时，才能使用我的技能”；不要让 base source 激活 draw skill。
- draw skill 不需要目标、不横置 source；prompt 不得暴露 target slot，也不得让前端自行推断蓝色费用可支付性。
- metadata 应暴露 `manaCost=1`、`powerCost=0`、`powerCostByTrait.blue=1`、`requiredTargetCount=0`、`requiresBattlefieldSource=true`、`exhaustsSource=false` 与 stack behavior marker。

### 4.2 Payment / Command

- 命令侧必须使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`，其中 `baseManaCost=1`、`powerCostByTrait[blue]=1`、`totalPowerCost=1`。
- 支持已有 `RECYCLE_RUNE:<objectId>` payment resource action 在蓝色 typed-power shortfall 时补足费用；wrong trait、unnecessary recycle、duplicate / missing / invalid rune 都必须 rejected no-mutation。
- 若存在 Malzahar temporary payment-only resource，不能用它支付 typed blue shortfall；本切片只允许它服务 generic rune-power shortfall。Renata draw skill 没有 generic power cost，因此不应暴露或接受 `TEMP_PAYMENT_RESOURCE:*`。
- 成功提交时应创建普通 stack item，并进入 `NeutralClosed` / priority window；不得立即抽牌。
- `ABILITY_ACTIVATED`、`COST_PAID`、`STACK_ITEM_ADDED` 事件必须包含 abilityId、sourceObjectId、effectKind、paymentId/paymentWindow、typed power audit metadata 与 stackItemId。

### 4.3 Stack Resolution / Draw

- 双方 priority 让过后，Renata draw stack item 才结算。
- 结算时从 controller main deck 抽 1 张到 hand，沿用现有 draw helper、隐藏信息与 win/loss / empty deck 口径。
- resolved stack item 不应移动 Renata source、不应横置 source、不应改变 score。
- stack resolution event 应可被审计为 Renata draw skill effect，例如 `CARD_DRAWN` 或现有抽牌事件，并带 source / stack / ability metadata；若现有 draw helper 事件字段不足，至少在 focused tests 固定可观察状态变化。

### 4.4 No-Mutation Guards

必须 rejected no-mutation：

- 未知 ability id、错误 cardNo、source 不存在。
- source 非当前玩家控制、隐藏 / face-down、不在 battlefield。
- source 不是 `SFD·088/221` / `SFD·088a/221` 或 ability / cardNo mismatch。
- 提交任意 targetObjectIds、unsupported optional costs、`TEMP_PAYMENT_RESOURCE:*`、不必要 / wrong-trait / duplicate / invalid `RECYCLE_RUNE:*`。
- wrong timing：non-active player、non-main、neutral closed priority window、spell-duel focus、cleanup/task queue blocking、pending payment / hand choice blocking。
- 资源不足：缺 mana、缺 blue typed power、蓝色 shortfall 但只回收非蓝色 rune。

No-mutation 至少断言 tick、zones、cardObjects、runePool、main deck / hand、stack、priority/focus 与 temporary payment resources 不变。

### 4.5 Regression Boundaries

- 不得破坏 Vi / Xerath `ACTIVATE_ABILITY` payment plan、Spellshield tax、recycle rune payment resource actions。
- 不得破坏 Malzahar resource skill、temporary payment-only resource pending / inline consumption。
- 不得破坏 Dragon Soul Sage reaction resource skill timing。
- 不得修改前端以补服务端 prompt 缺口。

## 5. 必补测试

Focused tests 至少覆盖：

- catalog 不再把 Renata draw skill 留在 deferred-only surface；score skill 仍 deferred。
- prompt 在 open-main battlefield source 上暴露 Renata draw source requirement，含 typed blue cost metadata，无 target slot、无 exhaust marker。
- prompt / command 对 `SFD·088/221` 与 `SFD·088a/221` 均可用。
- 成功命令支付 1 mana + 1 blue typed power，创建 stack item，未立即抽牌。
- `RECYCLE_RUNE:*` 蓝色符文可补 typed blue shortfall，并在 `COST_PAID` 中记录 payment resource action 与 recycled object id。
- stack pass-pass 后抽 1 张，source 不横置、不移动、不加分。
- wrong timing、target submitted、temporary resource submitted、wrong trait recycle、source invalid、insufficient mana/blue power 均 rejected no-mutation。
- Vi / Xerath / Malzahar / Dragon Soul Sage existing `ACTIVATE_ABILITY` tests 继续通过。

相邻回归必须覆盖：

- `PaymentEngineUnificationTests`
- `MalzaharResourceSkillTests`
- `ReactionResourceSkillTests`
- payment resource actions、typed `SpendPower`、rune pool、ActionPrompt、GameHub

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~Malzahar|FullyQualifiedName~DragonSoulSage|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
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

- 不要把 4D-03M 扩展成完整 activated ability / resource skill family。
- 不要同时实现 Renata score、Crimson Rose、Shadow 或 Fluft Poro deferred abilities。
- 不要让前端本地推断蓝色费用、抽牌时机或 stack priority。
- 不要在 base source、wrong priority、cleanup、spell-duel 或 neutral-closed priority window 公开或接受 Renata draw skill。
- 不要立即抽牌；draw skill 必须先创建普通 stack item，等双方让过后结算。
- 不要让 Malzahar temporary payment-only resource 支付 typed blue cost。
- 不要修改 coverage matrix 或升级 Renata 到 full-official。
- 不要因为 4D-03M 通过而关闭 P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03M 是 4D-03L 后的下一枚 PaymentEngine breadth 切片，只用 Renata Glasc draw skill 验证 colored typed-power activated ability、ordinary stack resolution 与 prompt / command quote parity representative。它可以继续收窄 P0-005，但不能替代完整 score skill、target-bearing activated skills、full resource skill family、LayerEngine 或最终 READY audit。
