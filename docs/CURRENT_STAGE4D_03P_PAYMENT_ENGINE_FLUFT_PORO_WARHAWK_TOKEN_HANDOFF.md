# Stage 4D-03P PaymentEngine Fluft Poro Warhawk Token Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03P 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03O 已完成 Crimson Rose target-bearing equipment ready-unit representative。4D-03P 继续收窄 P0-005，选取剩余 deferred activated surface 中较小的一条 no-target token skill：

- `UNL-160/219`，卡名 `绵绵魄罗` / `Fluft Poro`。
- 当前 deferred surface：`DEFERRED_TAP_CREATE_TWO_SPELLSHIELD_WARHAWKS`。
- 官方文本锚点：`{{横置}}：打出两名1{{S}}的“战鹰”，它们拥有{{法盾}}。我必须位于战场上才能使用此技能。`
- Token identity：`UNL·T02`，卡名 `战鹰`，`power=1`，`cardCategoryName=指示物单位`，文本含 `{{法盾}}`。
- 现有普通入场证据：`p2-preflight-play-fluft-poro-activated-skill-unit` 与 `p4-play-fluft-poro-target-rejected`，确认 Fluft Poro 从手牌打出后进入控制者基地成为 5 战力、`CARD_TYPE:UNIT|魄罗` 单位对象；横置技能仍 deferred。
- 本切片只处理 activated skill：controlled face-up ready battlefield `UNL-160/219` source 横置作为费用、无目标、创建普通可响应 stack item，双方让过后在 controller base 创建两名 1 power、`UNL·T02`、`CARD_TYPE:UNIT|法盾` Warhawk token。

不要同时实现 Shadow swift stun、Crimson Rose 第一行经验触发、Crumbling Palace 战鹰分支、Moving Perch opponent Warhawk 分支、完整 token-play family、完整 battlefield-only activated family 或 coverage matrix full-official 升级。

## 2. 当前代码事实

- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 仍登记 `DEFERRED_TAP_CREATE_TWO_SPELLSHIELD_WARHAWKS`，并用 official text anchor `fluft-poro-warhawk-token` 审计官方文本仍 deferred。
- `P6TokenFactoryCatalog` 已登记 `UNL·T02` Warhawk token：`Unit("UNL·T02", "战鹰", "战鹰", 1, CardObjectTags.Spellshield, "鸟类")`，创建对象会带 unit 与 Spellshield 代表标签。
- `CoreRuleEngine.CreateWarhawkTokenInControllerBase` 已用于 Muddy Dredger Last Breath，写入 `UNIT_TOKEN_CREATED`，payload 包含 `tokenCardNo=UNL·T02`、`tokenName=战鹰`、`power`、`destinationZone=BASE`、`tokenTags` 与 `reason`。
- `rules-evidence-index.md` 4C-22 已记录 Warhawk token identity：结算创建 Warhawk token 到 controller base，power 1，unit / `法盾` tag 可追踪。
- Featherstorm、Prowling Hunter、Muddy Dredger 等现有 fixture 已覆盖 Warhawk token 到 controller base 的代表路径；完整“打出 token”官方语义、token family taxonomy 与 Spellshield target tax 仍未 full-official。
- 现有 Fluft Poro 普通打出 fixture 只覆盖 source unit 入场和显式目标拒绝；本切片不得改变普通手牌打出路径。

## 3. 建议写入范围

建议 owner：B 服务端规则 / 协议 / 测试实现。

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 可新增窄域测试文件，例如 `tests/Riftbound.ConformanceTests/FluftPoroActivatedAbilityTests.cs`
- 必要时更新 `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- Shadow、Crimson Rose、Crumbling Palace、Moving Perch 或其他 deferred ability 实现
- 未跟踪文件 `riftbound-dotnet.sln`

不可并行文件：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 新增 Fluft Poro 测试文件
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

## 4. 实现要求

### 4.1 Catalog / Prompt

- 将 `UNL-160/219` Fluft Poro Warhawk skill 从 deferred-only surface 移入 executable activated ability catalog，或增加等价 executable definition。
- 建议 ability id：`FLUFT_PORO_EXHAUST_CREATE_TWO_SPELLSHIELD_WARHAWKS`；effect kind：`FLUFT_PORO_ACTIVATED_CREATE_TWO_WARHAWKS`。
- Definition 应表达 `ManaCost=0`、`PowerCost=0`、`ExperienceCost=0`、`RequiredTargetCount=0`、`RequiresBattlefieldSource=true`、`ExhaustsSourceAsCost=true`、`AppliesSpellshieldTargetTax=false`、ordinary stack-before-token semantics。
- Prompt 只在当前玩家 open-main 可行动窗口公开：`Phase=MAIN`、`TimingState=NEUTRAL_OPEN`、active player 为当前玩家、stack 为空、无 blocking pending payment / hand choice / task。
- Source 必须是当前玩家控制、公开、正面、未横置、cardNo=`UNL-160/219`、位于 controller battlefield 的 `CARD_TYPE:UNIT` 对象。不要让基地 source、隐藏、face-down、standby、敌方控制、未知 cardNo、非单位或已横置 source 暴露。
- 该 ability 无目标；prompt 不应暴露 target choices，也不应要求或展示 Spellshield target tax。
- Prompt metadata 应暴露 `manaCost=0`、`powerCost=0`、`experienceCost=0`、`requiredTargetCount=0`、`exhaustsSource=true`、`requiresBattlefieldSource=true`、`tokenCardNo=UNL·T02`、`tokenCount=2`、`tokenPower=1`、`tokenTags` 包含 `CARD_TYPE:UNIT` 和 `法盾`，以及 `stackPolicy=ordinary-stack-item-before-token-create`。

### 4.2 Payment / Command

- 命令侧必须复用 existing activated ability command path 的 timing / source / payload guards。若走 shared `PaymentPlan`，cost 应为 0 mana / 0 power / 0 experience；不得新增并行支付逻辑。
- Source 横置必须作为费用提交的一部分；任何校验失败时 source 必须保持 ready，token 不创建，stack 不改变。
- 成功提交后应横置 source unit、创建普通 stack item，并进入 `NeutralClosed` / priority window；不得立即创建 Warhawk token。
- `ABILITY_ACTIVATED`、source exhausted event、`STACK_ITEM_ADDED` 与任何 0-cost audit payload 必须包含 abilityId、sourceObjectId、effectKind、targetObjectIds 空数组、token metadata 与 stackItemId。
- 该 skill 不接受 targetObjectIds、`RECYCLE_RUNE:*`、`TEMP_PAYMENT_RESOURCE:*` 或其他 optional costs。

### 4.3 Stack Resolution / Token Creation

- 双方 priority 让过后，Fluft Poro stack item 才结算。
- 结算时创建两名 Warhawk token 到 controller base，沿用 `UNL·T02` token factory identity：1 power、owner/controller 为 skill controller、tags 包含 unit 与 Spellshield。
- 每个 token 应写入 `UNIT_TOKEN_CREATED`，payload 包含 `sourceObjectId`、`abilityId` / reason、`tokenObjectId`、`tokenCardNo=UNL·T02`、`tokenName=战鹰`、`power=1`、`destinationZone=BASE`、`tokenTags`。
- Resolved stack item 不应移动 Fluft Poro source、不应改变 source zone；source 应保持 battlefield / exhausted。

### 4.4 No-Mutation Guards

必须 rejected no-mutation：

- 未知 ability id、错误 cardNo、source 不存在。
- source 非当前玩家控制、隐藏 / face-down、非 battlefield unit、位于 base、standby、已横置、未知 cardNo、不是 `UNL-160/219`。
- 提供任意目标、提供多目标、提供 optional costs。
- 提交任意 `RECYCLE_RUNE:*`、`TEMP_PAYMENT_RESOURCE:*`、unsupported payment resources。
- wrong timing：non-active player、non-main、neutral closed priority window、spell-duel focus、cleanup/task queue blocking、pending payment / hand choice blocking。

No-mutation 至少断言 tick、zones、cardObjects、source exhausted state、runePool、experience、score、stack、priority/focus 与 temporary payment resources 不变，且没有 `UNIT_TOKEN_CREATED`。

### 4.5 Regression Boundaries

- 不得破坏 Fluft Poro 普通手牌打出和带目标打出拒绝 fixtures。
- 不得破坏 Warhawk token identity / destination / tag 代表路径。
- 不得破坏 Renata draw / score、Vi、Xerath、Malzahar、Dragon Soul Sage、Crimson Rose existing `ACTIVATE_ABILITY` representative。
- 不得让 frontend 本地推断 battlefield-only source、token identity、token数量或 stack timing。

## 5. 必补测试

Focused tests 至少覆盖：

- catalog 移除 `DEFERRED_TAP_CREATE_TWO_SPELLSHIELD_WARHAWKS`，保留 Shadow deferred surface。
- prompt 在 open-main controlled battlefield ready Fluft Poro unit 上暴露 ability source requirement，metadata 含 no-cost、`requiredTargetCount=0`、`exhaustsSource=true`、`requiresBattlefieldSource=true`、Warhawk token metadata。
- source 在 base、source 已横置、wrong controller / wrong cardNo / face-down source 时 prompt 不暴露。
- 成功命令横置 source、创建普通 stack item，未立即创建 token。
- pass-pass 后创建两名 `UNL·T02` Warhawk token 到 controller base，source 仍在 battlefield 且 exhausted。
- missing source、provided target、unsupported optional costs、temporary resource、recycle rune action、wrong timing、invalid source 全部 rejected no-mutation。
- Fluft ordinary play fixtures、Warhawk token fixtures、Renata / Crimson / Xerath / Malzahar / Dragon Soul Sage activated tests 继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Fluft|FullyQualifiedName~Warhawk|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~Token"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Fluft|FullyQualifiedName~Warhawk|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Token|FullyQualifiedName~Spellshield|FullyQualifiedName~Battlefield"
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

- 不要实现 Shadow swift stun、Crimson Rose 第一行 trigger、Crumbling Palace、Moving Perch 或其他 Warhawk-producing abilities。
- 不要把 4D-03P 扩展成完整 battlefield-only activated family、complete token-play semantics 或 full Spellshield target-tax matrix。
- 不要允许 Fluft Poro 从 controller base 激活；官方文本明确 source 必须位于 battlefield。
- 不要立即创建 token；skill 必须先创建普通 stack item，等双方让过后结算。
- 不要让 Malzahar temporary payment-only resource、recycled rune actions 或 optional costs 参与本切片。
- 不要因为 4D-03P 通过而关闭 P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03P 是 Crimson Rose target-bearing equipment model 之后的 no-target battlefield-only token skill 切片。它可验证 battlefield-only source locality、exhaust-as-cost、ordinary stack-before-token、Warhawk token identity 与 Spellshield tag continuity；但不能替代 full PaymentEngine、完整 activated / resource skill family、完整 token-play official semantics、LayerEngine、覆盖矩阵 full-official 或最终 READY audit。
