# Stage 4D-03O PaymentEngine Crimson Rose Ready Unit Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03O 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03N 已完成 Renata Glasc no-target colored activated score representative。4D-03O 继续收窄 P0-005，选取第一个 target-bearing activated ability representative：

- `UNL-109/219`，卡名 `猩红玫瑰` / `Crimson Rose` / `Scarlet Rose`。
- 当前 deferred surface：`DEFERRED_EXPERIENCE_EXHAUST_READY_UNIT`。
- 官方文本锚点：`消耗3经验，{{横置}}：让一名单位变为活跃状态`。
- 现有普通入场证据：`p2-preflight-play-scarlet-rose-equipment` 与 `p4-play-scarlet-rose-target-rejected`，确认其从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象。
- 本切片只处理第二行 activated ready-unit skill：支付 3 experience、横置 `UNL-109/219` source equipment、选择一名单位作为 skill target，创建普通可响应 stack item，双方让过后让目标变为活跃状态。

不要同时实现第一行“当你打出一名单位时，你可以选择支付{{1}}来获得1经验”、Shadow swift stun、Fluft Poro Warhawk token creation、完整 equipment activated family、完整 target-bearing skill family 或 coverage matrix full-official 升级。

## 2. 当前代码事实

- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 仍登记 `DEFERRED_EXPERIENCE_EXHAUST_READY_UNIT`，并用 official text anchor `crimson-rose-ready-unit` 审计官方文本仍 deferred。
- `P4ActivatedAbilityCatalog` 当前 executable definitions 均以 unit source 为主；`MatchSession.ActivateAbilityRequirementsForSource` 目前也只扫描可控、公开、正面的 `CARD_TYPE:UNIT` source。4D-03O 需要扩展 source filter，使 `UNL-109/219` 的 controlled face-up base equipment source 可以暴露 activated ability。
- 现有 deferred surface 的 `RequiresBattlefieldSource` 是保守占位；官方 `UNL-109/219` 文本没有 Fluft Poro 那种 battlefield-only 限制，且当前打出 fixture 把 source equipment 放入 controller base。本切片代表路径应以 controlled public base equipment source 为准，不应要求 source 位于 battlefield。
- `PaymentCostRules.PaymentPlan` 已支持 `experienceCost`、`AuthorizePayment`、`TryCommitPayment` 与 `COST_PAID` audit payload。4D-03O 必须复用 shared payment plan，不新增并行经验扣费逻辑。
- Xerath 已有 target-bearing activated skill 与 enemy Spellshield skill target tax representative。Crimson Rose 应复用同一 enemy Spellshield target tax 口径：选择敌方带法盾单位作为 skill target 时，当前玩家必须额外支付 mana tax；选择友方法盾单位时不缴税。
- 现有 generic ready behavior 会写 `UNIT_READIED` 并把 target `IsExhausted=false`；4D-03O 可复用等价 ready-state helper，但 stack item、payment、source exhaust 和 target validation 仍需由 server authoritative command path 固化。

## 3. 建议写入范围

建议 owner：B 服务端规则 / 协议 / 测试实现。

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 可新增窄域测试文件，例如 `tests/Riftbound.ConformanceTests/CrimsonRoseActivatedAbilityTests.cs`
- 必要时更新 `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- Renata、Shadow、Fluft Poro 或其他 deferred ability 实现
- 未跟踪文件 `riftbound-dotnet.sln`

不可并行文件：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 新增 Crimson Rose 测试文件
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

## 4. 实现要求

### 4.1 Catalog / Prompt

- 将 `UNL-109/219` Crimson Rose ready-unit skill 从 deferred-only surface 移入 executable activated ability catalog，或增加等价 executable definition。
- 建议 ability id：`CRIMSON_ROSE_EXPERIENCE3_EXHAUST_READY_UNIT`；effect kind：`CRIMSON_ROSE_ACTIVATED_READY_UNIT`。
- Definition 应表达 `ExperienceCost=3` 或等价 cost metadata、`RequiredTargetCount=1`、`ExhaustsSourceAsCost=true`、`AppliesSpellshieldTargetTax=true`、equipment source support、ordinary stack-before-ready semantics。
- Prompt 只在当前玩家 open-main 可行动窗口公开：`Phase=MAIN`、`TimingState=NEUTRAL_OPEN`、active player 为当前玩家、stack 为空、无 blocking pending payment / hand choice / task。
- Source 必须是当前玩家控制、公开、正面、未横置、cardNo=`UNL-109/219`、位于 controller base 的 `CARD_TYPE:EQUIPMENT` 对象。不要让隐藏、face-down、standby、敌方控制、未知 cardNo、非装备或已横置 source 暴露。
- Target scope 是“一名单位”：使用服务端筛出的公开 base 或 battlefield unit object；不要限制为友方单位。敌方法盾单位需要 mana target tax；友方法盾单位不需要 target tax。
- Prompt metadata 应暴露 `experienceCost=3`、`manaCost=0` 或 target-tax-adjusted quote、`powerCost=0`、`requiredTargetCount=1`、`targetChoicesByIndex`、`exhaustsSource=true`、`appliesSpellshieldTargetTax=true`、`stackPolicy=ordinary-stack-item-before-ready`。
- 玩家 experience 不足 3 时，prompt 应隐藏该 ability 或标记不可组合；命令侧仍必须独立拒绝。

### 4.2 Payment / Command

- 命令侧必须使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`，其中 base skill `experienceCost=3`，基础 mana / power cost 为 0。
- 如果目标是敌方 Spellshield unit，payment plan 的 total mana cost 必须包含 target tax，并在 audit payload 中记录 `spellshieldTaxMana`。
- Source 横置必须作为费用提交的一部分；费用或目标校验失败时 source 必须保持原状态。
- 成功提交后应扣除 3 experience、必要时扣除 target tax mana、横置 source equipment、创建普通 stack item，并进入 `NeutralClosed` / priority window；不得立即 ready target。
- `ABILITY_ACTIVATED`、`EQUIPMENT_EXHAUSTED` 或等价 exhausted event、`COST_PAID`、`STACK_ITEM_ADDED` 必须包含 abilityId、sourceObjectId、targetObjectIds、effectKind、paymentId/paymentWindow、experience cost、remainingExperience、target tax metadata 与 stackItemId。
- 该 skill 不接受 `RECYCLE_RUNE:*`、`TEMP_PAYMENT_RESOURCE:*` 或其他 optional costs；若后续决定允许 mana tax 通过 rune actions 支付，必须先有 prompt quote 与 no-mutation tests。4D-03O 默认不开放这条额外复杂度。

### 4.3 Stack Resolution / Ready

- 双方 priority 让过后，Crimson Rose stack item 才结算。
- 结算时将 target unit `IsExhausted=false`，并写入 `ABILITY_RESOLVED` / `UNIT_READIED` 或等价可审计事件。
- Target 已经活跃时仍应按官方文本尽可能执行；focused tests 至少固定一种 exhausted target success 路径。如实现选择只暴露 exhausted units，必须在 audit 中说明这是本切片保守代表范围，不得宣称 full official。
- Resolved stack item 不应移动 Crimson Rose source、不应改变 source zone；source 应保持 controller base / exhausted。

### 4.4 No-Mutation Guards

必须 rejected no-mutation：

- 未知 ability id、错误 cardNo、source 不存在。
- source 非当前玩家控制、隐藏 / face-down、非 base equipment、已横置、未知 cardNo、不是 `UNL-109/219`。
- 缺目标、多目标、目标不是公开单位、目标不存在、隐藏 / face-down / standby target。
- 敌方法盾目标 mana tax 不足。
- experience 不足 3。
- 提交任意 `RECYCLE_RUNE:*`、`TEMP_PAYMENT_RESOURCE:*`、unsupported optional costs。
- wrong timing：non-active player、non-main、neutral closed priority window、spell-duel focus、cleanup/task queue blocking、pending payment / hand choice blocking。

No-mutation 至少断言 tick、zones、cardObjects、source exhausted state、target exhausted state、runePool、experience、score、stack、priority/focus 与 temporary payment resources 不变。

### 4.5 Regression Boundaries

- 不得破坏 Scarlet Rose 普通装备入场和带目标打出拒绝 fixtures。
- 不得破坏 Renata draw / score、Vi、Xerath、Malzahar、Dragon Soul Sage existing `ACTIVATE_ABILITY` representative。
- 不得破坏 Xerath enemy Spellshield skill target tax。
- 不得让 frontend 本地推断 experience cost、target tax、equipment source 或 ready timing。

## 5. 必补测试

Focused tests 至少覆盖：

- catalog 移除 `DEFERRED_EXPERIENCE_EXHAUST_READY_UNIT`，保留 Fluft Poro 与 Shadow deferred surfaces。
- prompt 在 open-main controlled base ready Crimson Rose equipment 上暴露 ability source requirement，metadata 含 `experienceCost=3`、`exhaustsSource=true`、`RequiredTargetCount=1` 与 target choices。
- experience 不足、source 已横置、source 非 base equipment、wrong controller / wrong cardNo / face-down source 时 prompt 不暴露。
- 成功命令扣除 3 experience、横置 source、创建 stack item，未立即 ready target。
- pass-pass 后 exhausted target 变为 active，source 仍在 base 且 exhausted。
- 敌方法盾 target 需要 mana tax；友方法盾 target 不需要 tax。
- insufficient experience、insufficient target tax mana、missing / too many / invalid target、unsupported optional costs、temporary resource、recycle rune action、wrong timing、invalid source 全部 rejected no-mutation。
- Scarlet Rose ordinary play fixtures、Renata activated tests、Xerath tax tests、Malzahar / Dragon Soul Sage resource skill tests 继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Crimson|FullyQualifiedName~Scarlet|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Crimson|FullyQualifiedName~Scarlet|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Experience|FullyQualifiedName~Spellshield"
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

- 不要实现 Crimson Rose 第一行 unit-play trigger / optional pay 1 gain experience。
- 不要把 4D-03O 扩展成完整 target-bearing activated family、equipment ability family 或 all ready-unit effects。
- 不要同时实现 Shadow swift stun、Fluft Poro Warhawk token creation 或 coverage matrix full-official upgrade。
- 不要要求 Crimson Rose source 位于 battlefield；当前 official text 与 existing fixture 指向 controlled base equipment representative。
- 不要立即 ready target；skill 必须先创建普通 stack item，等双方让过后结算。
- 不要让 Malzahar temporary payment-only resource 或 recycled rune actions 支付本切片费用。
- 不要因为 4D-03O 通过而关闭 P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03O 是 Renata no-target ordinary activated model 之后的第一枚 target-bearing activated equipment skill 切片。它可验证 experience cost、equipment source exhaust-as-cost、server target selection、enemy Spellshield skill target tax 与 ordinary stack-before-ready；但不能替代 full PaymentEngine、完整 activated / resource skill family、LayerEngine、覆盖矩阵 full-official 或最终 READY audit。
