# Stage 4D-03Q PaymentEngine Shadow Swift Stun Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03Q 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03P 已完成 Fluft Poro battlefield-only no-target Warhawk token representative。4D-03Q 继续收窄 P0-005，选取当前剩余 deferred activated surface 中的 Shadow swift target-bearing stun skill：

- `UNL-194/219`，卡名 `黑影` / `Shadow`。
- 当前 deferred surface：`DEFERRED_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER`。
- 官方文本锚点：`{{迅捷>}} 支付{{1}}和{{A}}，{{横置}}：{{眩晕}}一名进攻此处的敌方单位`。
- 现有普通入场证据：`p2-preflight-play-shadow-base-unit-static`，确认 Shadow 从手牌打出后进入控制者基地成为 3 战力、未休眠、无额外标签的 `CARD_TYPE:UNIT` 单位对象；战场目的地、活跃战场进场和激活眩晕技能仍 deferred。
- 本切片只处理 activated skill：controlled face-up ready battlefield `UNL-194/219` source，在 representative swift combat response / battle-response priority window 中，支付 1 mana + 1 generic power，横置 source，选择同一战场正在进攻的敌方单位，创建普通可响应 stack item，双方让过后给目标应用 `STUNNED`。

不要同时实现完整 battle response lifecycle、完整 swift family、完整 `[A]` / `[C]` resource skill family、Crimson Rose 第一行触发、完整 target-bearing activated ability family、coverage matrix full-official 升级或前端运行时代码。

## 2. 当前代码事实

- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 目前只剩 Shadow swift stun skill 作为 P4 activated ability deferred surface。
- `ConformanceFixtureRunnerTests.P4DeferredActivatedAbilityOfficialTextAnchor` 已记录 Shadow 官方文本锚点。
- `p2-preflight-play-shadow-base-unit-static.fixture.json` 当前只覆盖 Shadow 普通手牌打出，fixture 明确说明 activated stun skill deferred。
- `CardTargetScopes.EnemyAttackingUnit` 已存在，`IsTargetObjectInScope` 会校验敌方 field object 且 `IsAttackingBattlefieldObject`。
- `ObjectLocations[objectId].BattlefieldObjectId`、`IsObjectLocatedAtBattlefield` 与 `BattleState` 可用于校验 Shadow source 与 target 是否位于同一 battlefield。
- `ResolveStatusEffectIds` / stack resolution 已有 `STUNNED` status effect application pattern。
- `SpellshieldTaxManaForTarget` 已支持 target-bearing enemy field target 的 Spellshield target tax；Shadow 属于 `EnemySpellshieldTaxRisk=true` 的目标技能。
- `MatchSession.CanPromptActivateAbilityInCurrentWindow` 当前没有 Shadow swift combat-response timing policy；本切片需要新增 focused representative policy，但不得重写完整 P0-004 battle lifecycle。

## 3. 建议写入范围

建议 owner：B 服务端规则 / 协议 / 测试实现。

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 可新增窄域测试文件，例如 `tests/Riftbound.ConformanceTests/ShadowActivatedAbilityTests.cs`
- 必要时更新 `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- Fluft Poro、Crimson Rose、Renata、Malzahar、Dragon Soul Sage 既有实现的非必要重构
- 完整 `DECLARE_BATTLE` / `START_BATTLE` lifecycle 重写
- 未跟踪文件 `riftbound-dotnet.sln`

不可并行文件：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 新增 Shadow 测试文件
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

## 4. 实现要求

### 4.1 Catalog / Prompt

- 将 Shadow 从 deferred-only surface 移入 executable activated ability catalog，或增加等价 executable definition。
- 建议 ability id：`SHADOW_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER`。
- 建议 effect kind：`SHADOW_ACTIVATED_STUN_ATTACKER`。
- Definition 应表达 `ManaCost=1`、`PowerCost=1`、`ExperienceCost=0`、`RequiredTargetCount=1`、`RequiresBattlefieldSource=true`、`ExhaustsSourceAsCost=true`、`IsTargetBearing=true`、`AppliesSpellshieldTargetTax=true`、ordinary stack-before-stun semantics。
- Prompt 只在 focused representative swift combat response / battle-response priority window 暴露：当前 `PriorityPlayerId == playerId`，无 blocking pending payment / hand choice / cleanup task，存在 active battle 或可由当前 state 推导出的 active attacker context。
- 本切片不要求完整 P0-004 battle lifecycle。若现有 `DECLARE_BATTLE` 仍立即结算，测试可以构造 `IsAttacking=true` 的 battlefield state 来验证 prompt / command / resolution。
- Source 必须是当前玩家控制、公开、正面、未横置、cardNo=`UNL-194/219`、位于 battlefield 的 `CARD_TYPE:UNIT` 对象。
- Target 必须是 exactly one enemy public battlefield unit，`IsAttacking=true`，且与 Shadow source 位于同一 battlefield。不得暴露防守者、非进攻敌方单位、友方单位、其他战场进攻者、基地单位、面朝下 / standby / unknown identity 对象。
- Prompt metadata 应暴露 `manaCost=1`、`powerCost=1`、`experienceCost=0`、`requiredTargetCount=1`、`exhaustsSource=true`、`requiresBattlefieldSource=true`、`targetScope=enemy-attacking-unit-at-this-battlefield`、`spellshieldTaxManaForTarget`、`stackPolicy=ordinary-stack-item-before-stun`。

### 4.2 Payment / Command

- 命令侧必须复用 `ACTIVATE_ABILITY` shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` 口径；不得新增并行支付逻辑。
- Cost 为 1 mana + 1 generic power，加上敌方法盾目标的 Spellshield target tax mana。
- 允许必要 `RECYCLE_RUNE:*` payment resource action 补足 generic power shortfall，沿用 Vi / Xerath activated ability resource-action 规则。
- 若现有 `TEMP_PAYMENT_RESOURCE:*` 已被同一 activated ability inline payment 口径允许支付 generic power，可按既有规则接受；若不适用于 Shadow timing，必须 rejected no-mutation，并在测试中锁定。
- Source 横置必须作为费用提交的一部分。任何校验失败时 source 必须保持 ready，target 不被 stun，stack / runePool / objectLocations / events 不改变。
- 成功提交后应横置 source、扣除费用、创建普通 stack item，并进入 priority window；不得立即应用 `STUNNED`。
- `ABILITY_ACTIVATED`、source exhausted event、`COST_PAID`、`STACK_ITEM_ADDED` 必须包含 abilityId、sourceObjectId、targetObjectId、battlefieldObjectId、effectKind、spellshieldTax mana 与 payment-resource audit metadata。

### 4.3 Stack Resolution / Stun

- 双方 priority 让过后，Shadow stack item 才结算。
- 结算前应重新验证 target 仍是敌方、公开、在 battlefield、同一 battle/battlefield context 中仍是合法 attacking target；若 target 已离开、变友方、变为非进攻或不再位于同一 battlefield，stack item 应按既有 fizzled / no-effect 口径清除，不得误伤其他对象。
- 合法结算时给 target 应用 `STUNNED` status effect，事件 payload 包含 sourceObjectId、targetObjectId、abilityId、effectKind、battlefieldObjectId 与 duration / until-end-turn metadata。
- Resolved stack item 不应移动 Shadow source、不应改变 source zone；source 应保持 battlefield / exhausted。

### 4.4 No-Mutation Guards

必须 rejected no-mutation：

- 未知 ability id、错误 cardNo、source 不存在。
- source 非当前玩家控制、隐藏 / face-down、非 battlefield unit、位于 base、standby、已横置、未知 cardNo、不是 `UNL-194/219`。
- missing target、0 target、2+ targets。
- target 是友方、非单位、基地对象、非进攻对象、防守者、其他战场 attacker、face-down / standby / unknown identity。
- wrong timing：open-main、base neutral state、non-priority player、spell-duel focus without battle response context、cleanup/task queue blocking、pending payment / hand choice blocking、after target no longer attacking。
- mana / power / Spellshield tax 不足。
- invalid / duplicate / unnecessary `RECYCLE_RUNE:*` 或 unsupported `TEMP_PAYMENT_RESOURCE:*`。
- unsupported optional costs。

No-mutation 至少断言 tick、zones、cardObjects、source exhausted state、target status effects、runePool、experience、score、stack、priority/focus、temporary payment resources 与 events 不变。

### 4.5 Regression Boundaries

- 不得破坏 Shadow 普通手牌打出和带目标打出拒绝 fixture。
- 不得破坏 Fluft Poro、Crimson Rose、Renata、Vi、Xerath、Malzahar、Dragon Soul Sage existing `ACTIVATE_ABILITY` representative。
- 不得把 Shadow prompt 暴露到 open-main 或基地 source 上。
- 若 Shadow 是最后一个 deferred activated ability surface，实现后需要更新 P4 deferred surface audit 测试，使空 deferred list 被视为 P4 activated surface 清零，而不是继续要求至少一个 deferred surface。
- 不得让 frontend 本地推断 swift timing、attacking target、same-battlefield locality、Spellshield target tax 或 stun resolution。

## 5. 必补测试

Focused tests 至少覆盖：

- catalog 移除 `DEFERRED_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER`。如果 deferred list 归零，更新 audit assertion 以声明 P4 activated ability deferred surface closed。
- prompt 在 representative swift / battle-response priority state 暴露 Shadow source requirement，metadata 含 1 mana、1 power、required target count 1、source exhaust、battlefield source、target scope、Spellshield target tax 与 stack policy。
- prompt 在 open-main、base source、exhausted source、wrong controller / wrong cardNo / face-down source、无进攻敌方目标、wrong battlefield target 时不暴露。
- 成功命令支付 1 mana + 1 generic power，必要时支持合法 power resource action，横置 source，创建普通 stack item，未立即 stun。
- pass-pass 后给目标应用 `STUNNED`，target 仍在 battlefield，source 仍在 battlefield 且 exhausted。
- missing target、too many targets、friendly target、non-attacking target、defender target、wrong battlefield attacker、source base、source exhausted、wrong timing、wrong priority、insufficient mana/power/tax、invalid/unnecessary payment resources 全部 rejected no-mutation。
- resolution stale target no-effect：target 离场、移动、变控制者或不再 attacking 后，stack 结算不应用 `STUNNED`。
- Shadow ordinary play fixture、P4 deferred official text anchor、activated ability regression、PaymentEngine regression 与 battle/spell-duel adjacent tests 继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Shadow|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~Swift|FullyQualifiedName~Stun"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Shadow|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Swift|FullyQualifiedName~Stun|FullyQualifiedName~SpellDuel|FullyQualifiedName~Battlefield|FullyQualifiedName~DeclareBattle"
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

- 不要实现完整 battle response lifecycle 或重写 `DECLARE_BATTLE` 立即结算代表路径。
- 不要把 Shadow 扩展成完整 swift / reaction / counter family。
- 不要把 target-bearing activated ability family 一次性泛化到所有卡。
- 不要允许 Shadow 从 controller base 激活。
- 不要立即 stun；skill 必须先创建普通 stack item，等双方让过后结算。
- 不要让非同战场 attacker、defender 或普通敌方单位成为合法目标。
- 不要因为 4D-03Q 通过而关闭 P0-004、P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03Q 是 Fluft Poro token skill 之后的 last known P4 activated ability deferred-surface focused slice。它可验证 swift timing representative、battlefield source locality、same-battlefield attacking enemy target、1 mana + 1 generic power payment、Spellshield target tax、exhaust-as-cost、ordinary stack-before-stun 与 stale resolution no-effect；但不能替代 full battle lifecycle、full PaymentEngine、完整 activated / resource skill family、LayerEngine、覆盖矩阵 full-official 或最终 READY audit。
