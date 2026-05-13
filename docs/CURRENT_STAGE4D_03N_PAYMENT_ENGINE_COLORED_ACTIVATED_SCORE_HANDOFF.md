# Stage 4D-03N PaymentEngine Colored Activated Score Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03N 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03M 已用 `SFD·088/221` / `SFD·088a/221` 烈娜塔·戈拉斯克的 `支付{{1}}和{{蓝色}}：抽一张牌` 建立 colored typed-blue ordinary activated draw representative。4D-03N 继续收窄同一官方卡的剩余 colored activated ability：

- `SFD·088/221` / `SFD·088a/221`，卡名 `烈娜塔·戈拉斯克` / `Renata Glasc`。
- 当前 deferred surface：`DEFERRED_PAY_4_BLUE4_EXHAUST_SCORE_1`。
- 官方文本锚点：`支付{{4}}和{{蓝色}}{{蓝色}}{{蓝色}}{{蓝色}}，{{横置}}：获得1分`。
- 同功能卡组：`SFD·088/221` 与异画 `SFD·088a/221` 的官方文本相同，矩阵 `functionalUnitId = FU-3185396ef9`、`functionalRepresentativeNo = SFD·088/221`。
- 现有普通入场证据：`p2-preflight-play-sfd-088-renata-glasc-activated-skill-unit`、`p2-preflight-play-sfd-088-renata-glasc-alt-a-activated-skill-unit` 及对应 target rejected fixtures。
- 现有 activated evidence：4D-03M 已验收两张 collector 的 draw skill alias、typed blue payment、ordinary stack resolution 与 no-mutation guard。

本切片只处理 Renata score skill：支付 4 mana 与 4 blue typed power，横置来源作为费用，创建普通可响应 stack item，结算时获得 1 分。不要同时实现 Crimson Rose ready-unit、Shadow swift stun、Fluft Poro Warhawk token creation、target-bearing activated ability family 或覆盖矩阵 full-official 升级。

## 2. 当前代码事实

- `P4ActivatedAbilityCatalog.GetAll()` 当前执行 Vi、Xerath、Malzahar、Dragon Soul Sage 与 Renata draw 五条 representative ability。
- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 仍登记 `DEFERRED_PAY_4_BLUE4_EXHAUST_SCORE_1`，并标注 `RequiresBattlefieldSource: true`、`IsTargetBearing: false`、`EnemySpellshieldTaxRisk: false`。
- `P4DeferredActivatedAbilityOfficialTextAnchor("renata-glasc-score")` 已用官方文本锚点审计 Renata score skill 仍 deferred。
- `P4ActivatedAbilityCatalog` 已有 `RenataGlascCardNo` / `RenataGlascAltCardNo` 与 draw source alias helper；4D-03N 应复用或扩展同一 alias 口径，不能只让异画落入未知行为。
- `CoreRuleEngine.ResolveActivateAbility` 当前遇到未登记的 Renata score ability id 会 rejected / unsupported，不应推进 tick、支付资源、横置来源、创建 stack item 或加分。
- `MatchSession` 当前只会为 Renata draw 生成 open-main `ACTIVATE_ABILITY.sourceRequirements`；score skill 尚无 prompt surface。
- `PaymentCostRules.PaymentPlan` 已支持 mana cost、typed `powerCostByTrait`、`RECYCLE_RUNE:*` payment resource action 与 no-mutation rollback。4D-03N 必须复用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`，不新增并行费用逻辑。
- 4D-03K 后 Malzahar `TEMP_PAYMENT_RESOURCE:*` 可补 generic rune-power shortfall，但不能支付 typed blue shortfall；Renata score 没有 generic power cost，因此不应暴露或接受 temporary resource。

## 3. 建议写入范围

建议 owner：B 服务端规则 / 协议 / 测试实现。

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/RenataActivatedAbilityTests.cs`
- 必要时更新 `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- Crimson Rose / Shadow / Fluft Poro deferred ability 实现
- 未跟踪文件 `riftbound-dotnet.sln`

不可并行文件：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/RenataActivatedAbilityTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

## 4. 实现要求

### 4.1 Catalog / Prompt

- 将 Renata score skill 从 deferred-only surface 移入 executable activated ability catalog，或增加等价 executable definition；审计说明必须保留：4D-03N 只开放 score representative。
- 支持 `SFD·088/221` 与 `SFD·088a/221` 两个同功能官方 collector。
- prompt 只在服务端确认普通开放主阶段合法时公开 score `ACTIVATE_ABILITY` source requirement：`Phase=MAIN`、`TimingState=NEUTRAL_OPEN`、当前玩家为 active player、stack 为空、无 blocking pending payment / hand choice / task。
- source 必须由当前玩家控制、公开、正面、位于 battlefield，且未横置。官方文本写明“只有我位于战场时，才能使用我的技能”；不要让 base source 激活 score skill。
- score skill 不需要目标，但需要横置 source 作为费用；prompt 不得暴露 target slot，也不得让前端自行推断蓝色费用、横置费用或得分时机。
- metadata 应暴露 `manaCost=4`、`powerCost=0`、`powerCostByTrait.blue=4`、`requiredTargetCount=0`、`requiresBattlefieldSource=true`、`exhaustsSource=true` 与 stack behavior marker。
- 如果 Renata 已横置，prompt 应隐藏 score skill；draw skill 不以横置为费用，仍可按 4D-03M 既有规则暴露。

### 4.2 Payment / Command

- 命令侧必须使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`，其中 `baseManaCost=4`、`powerCostByTrait[blue]=4`、`totalPowerCost=4`、`genericPowerCost=0`。
- source 横置必须作为费用提交的一部分；费用提交失败时 source 必须保持未横置。
- 支持已有 `RECYCLE_RUNE:<objectId>` payment resource action 在蓝色 typed-power shortfall 时补足费用；需要多枚蓝色 rune 时必须全部显式、合法且不可重复。
- wrong trait、unnecessary recycle、duplicate / missing / invalid rune 都必须 rejected no-mutation。
- 若存在 Malzahar temporary payment-only resource，不能用它支付 typed blue shortfall；Renata score skill 没有 generic power cost，因此不应暴露或接受 `TEMP_PAYMENT_RESOURCE:*`。
- 成功提交时应横置 Renata、创建普通 stack item，并进入 `NeutralClosed` / priority window；不得立即加分。
- `ABILITY_ACTIVATED`、`COST_PAID`、`STACK_ITEM_ADDED` 事件必须包含 abilityId、sourceObjectId、effectKind、paymentId/paymentWindow、typed power audit metadata、exhaust-as-cost metadata 与 stackItemId。

### 4.3 Stack Resolution / Score

- 双方 priority 让过后，Renata score stack item 才结算。
- 结算时 controller 获得 1 分，沿用现有 score helper / event / winning score 口径。
- 若得分达到有效胜利分，必须沿用既有 `WinningPlayerId` / match-finished 语义；不要为 Renata 单独写一套胜负分支。
- resolved stack item 不应移动 Renata source、不应抽牌；source 应保持 battlefield / exhausted。
- stack resolution event 应可被审计为 Renata score skill effect，例如 `SCORE_GAINED` 或现有得分事件，并带 source / stack / ability metadata；若现有 score helper 事件字段不足，至少在 focused tests 固定可观察状态变化。

### 4.4 No-Mutation Guards

必须 rejected no-mutation：

- 未知 ability id、错误 cardNo、source 不存在。
- source 非当前玩家控制、隐藏 / face-down、不在 battlefield、已横置。
- source 不是 `SFD·088/221` / `SFD·088a/221` 或 ability / cardNo mismatch。
- 提交任意 targetObjectIds、unsupported optional costs、`TEMP_PAYMENT_RESOURCE:*`、不必要 / wrong-trait / duplicate / invalid `RECYCLE_RUNE:*`。
- wrong timing：non-active player、non-main、neutral closed priority window、spell-duel focus、cleanup/task queue blocking、pending payment / hand choice blocking。
- 资源不足：缺 mana、缺 blue typed power、蓝色 shortfall 但只回收非蓝色 rune。

No-mutation 至少断言 tick、zones、cardObjects、source exhausted state、runePool、main deck / hand、score、stack、priority/focus 与 temporary payment resources 不变。

### 4.5 Regression Boundaries

- 不得破坏 Renata draw prompt / command / stack pass-pass draw behavior。
- 不得破坏 Vi / Xerath `ACTIVATE_ABILITY` payment plan、Spellshield tax、recycle rune payment resource actions。
- 不得破坏 Malzahar resource skill、temporary payment-only resource pending / inline consumption。
- 不得破坏 Dragon Soul Sage reaction resource skill timing。
- 不得修改前端以补服务端 prompt 缺口。

## 5. 必补测试

Focused tests 至少覆盖：

- catalog 不再把 Renata score skill 留在 deferred-only surface；draw skill 仍 executable；Crimson Rose、Shadow、Fluft Poro 等其他 deferred surfaces 不变。
- prompt 在 open-main battlefield ready source 上同时暴露 Renata draw 与 score source requirement，score metadata 含 `manaCost=4`、`powerCost=0`、`powerCostByTrait.blue=4`、`exhaustsSource=true`、无 target slot。
- source 已横置时 score 不暴露，draw 仍按既有 4D-03M 规则可暴露。
- prompt / command 对 `SFD·088/221` 与 `SFD·088a/221` 均可用。
- 成功命令支付 4 mana + 4 blue typed power，横置 source，创建 stack item，未立即加分。
- `RECYCLE_RUNE:*` 蓝色符文可补 typed blue shortfall，多个 shortfall 时要求多个合法蓝色 rune，并在 `COST_PAID` 中记录 payment resource action 与 recycled object ids。
- stack pass-pass 后获得 1 分，source 保持 battlefield / exhausted，不抽牌、不移动。
- 得分达到有效胜利分时走既有胜负语义。
- wrong timing、target submitted、temporary resource submitted、wrong trait recycle、duplicate / invalid / unnecessary recycle、source invalid / exhausted、insufficient mana / blue power 均 rejected no-mutation。
- Renata draw、Vi、Xerath、Malzahar、Dragon Soul Sage existing `ACTIVATE_ABILITY` tests 继续通过。

相邻回归必须覆盖：

- `PaymentEngineUnificationTests`
- `MalzaharResourceSkillTests`
- `ReactionResourceSkillTests`
- Renata draw tests
- payment resource actions、typed `SpendPower`、rune pool、ActionPrompt、GameHub、score / battlefield held score

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~Malzahar|FullyQualifiedName~DragonSoulSage|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Score"
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

- 不要把 4D-03N 扩展成完整 activated ability / resource skill family。
- 不要同时实现 Crimson Rose、Shadow 或 Fluft Poro deferred abilities。
- 不要让前端本地推断蓝色费用、横置费用、得分时机或 stack priority。
- 不要在 base source、横置 source、wrong priority、cleanup、spell-duel 或 neutral-closed priority window 公开或接受 Renata score skill。
- 不要立即加分；score skill 必须先创建普通 stack item，等双方让过后结算。
- 不要让 Malzahar temporary payment-only resource 支付 typed blue cost。
- 不要修改 coverage matrix 或升级 Renata 到 full-official。
- 不要因为 4D-03N 通过而关闭 P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03N 是 4D-03M 后的下一枚 PaymentEngine breadth 切片，只用 Renata Glasc score skill 验证更高 typed-blue cost、exhaust-as-cost、ordinary stack-before-score 与 score resolution representative。它可以继续收窄 P0-005，但不能替代完整 target-bearing activated skills、full resource skill family、LayerEngine、覆盖矩阵 full-official 或最终 READY audit。
