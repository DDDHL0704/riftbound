# Stage 4D-03R PaymentEngine Rage Sigil Typed Resource Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03R 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03Q 已完成 Shadow swift combat-response stun representative，并清空当前已知 P4 activated ability deferred surface。4D-03R 继续收窄 P0-005，选取符能支付资源技能中的小型装备来源 representative：

- `SFD·222/221`，卡名 `暴怒之印` / `Rage Sigil`。
- 官方文本锚点：`{{横置}}：{{反应}}—{{获得}}{{红色}}，用以支付符能费用。（获得费用资源的技能无法成为其他法术的反应目标。）`
- 现有普通入场证据：`p2-preflight-play-rage-sigil-equipment`，确认 Rage Sigil 从手牌打出支付 0 费、0 目标、进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象。
- 现有 guard 证据：`p4-play-rage-sigil-target-rejected`，确认 Rage Sigil 作为 0 目标装备打出时携带显式目标会被拒绝；横置获得红色符能技能仍 deferred。
- 本切片只处理 SFD 版 Rage Sigil activated resource skill：controlled face-up ready base equipment `SFD·222/221` source，在合法 reaction/resource payment window 中横置 source，立即生成 1 点红色 payment-only temporary rune resource，用以支付符能费用，不创建普通 stack item，且该资源技能不能成为其他法术的反应目标。

不要同时实现完整 Sigil 家族、OGN 版 Sigil、Honeyfruit、Hextech Anomaly、Ancient Stele、Gold token、Lux、legend resource skills、完整 `[A]` / `[C]` resource skill family、coverage matrix full-official 升级或前端运行时代码。

## 2. 当前代码事实

- `CardBehaviorRegistry` 已有 `SFD·222/221` 0 费装备打出路径，Rage Sigil 可作为 controller base equipment 对象存在。
- `P4ActivatedAbilityDefinition` 当前支持 `IsResourceSkill`、`PaymentOnlyResource`、`GeneratedPower`、`GeneratedMana`、`ResourceRestriction`、`ReactionSpeed`、`RequiresBaseEquipmentSource`，但尚无 typed generated power 字段。
- `TemporaryPaymentResourceState` 当前只记录 `GeneratedPower` / `RemainingPower` generic temporary resource，尚无 `GeneratedPowerByTrait` / `RemainingPowerByTrait` 或等价 typed ledger。
- `RunePool` 与 `PaymentCostRules.CanPayPowerCost` 已支持 `PowerByTrait`；typed power 可以先支付对应特性需求，也可在对应特性费用支付后参与 generic power cost。
- `TemporaryPaymentResourceChoicesForGenericPower`、`TemporaryPaymentResourcePowerForGenericPower`、`TemporaryPaymentResourcePowerByChoice` 和 `TryApplyTemporaryPaymentResourcesToPendingPayment` 当前只把 temporary resource 作为 generic power 注入，不支持 red-only temporary payment resource。
- Malzahar resource skill 已有 temporary payment-only ledger lifecycle；Dragon Soul Sage 已有 reaction-speed immediate resource skill；本切片应尽量复用这些 prompt / command / cleanup / audit patterns，而不是新增并行支付系统。

## 3. 建议写入范围

建议 owner：B 服务端规则 / 协议 / 测试实现。

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/PaymentCostRules.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 可新增窄域测试文件，例如 `tests/Riftbound.ConformanceTests/RageSigilResourceSkillTests.cs`

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- OGN Sigil 或所有颜色 Sigil family
- Malzahar、Dragon Soul Sage、Renata、Crimson Rose、Fluft Poro、Shadow 既有实现的非必要重构
- 完整 reaction/counter lifecycle 重写
- 未跟踪文件 `riftbound-dotnet.sln`

不可并行文件：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/PaymentCostRules.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 新增 Rage Sigil 测试文件

## 4. 实现要求

### 4.1 Catalog / Prompt

- 新增或扩展 executable activated ability definition。
- 建议 ability id：`RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER`。
- 建议 effect kind：`RAGE_SIGIL_REACTION_TYPED_RESOURCE_GAIN_RED`。
- 建议 resource restriction：`PAY_RUNE_COSTS_ONLY_TYPED_RED_TEMPORARY_LEDGER_4D_03R`。
- Definition 应表达 `IsResourceSkill=true`、`ReactionSpeed=true`、`PaymentOnlyResource=true`、`RequiresBaseEquipmentSource=true`、`ExhaustsSourceAsCost=true`、`RequiredTargetCount=0`、`GeneratedPowerByTrait.red=1` 或等价 typed temporary resource metadata。
- Source 必须是当前玩家控制、公开、正面、未横置、cardNo=`SFD·222/221`、位于 controller base 的 equipment object。
- Prompt 只在服务端已有合法 reaction/resource skill priority window 暴露。若复用 Dragon Soul Sage 的 reaction-speed policy，应保持同一 priority/focus 约束；不得为了本切片重写完整 reaction lifecycle。
- Prompt metadata 应暴露 `resourceSkill=true`、`reactionSpeed=true`、`typedPaymentOnlyResource=true`、`generatedPowerByTrait.red=1`、`resourceRestriction`、`resourceLifecycle=temporary-payment-resource-ledger`、`requiresBaseEquipmentSource=true`、`exhaustsSource=true`、`requiredTargetCount=0`、`stackPolicy=no-ordinary-stack-item`。
- Prompt 不得要求或暴露目标、optional costs、payment resource actions 或普通 stack target choices。

### 4.2 Command / Resource Creation

- 命令侧必须通过 `ACTIVATE_ABILITY` 服务端 authoritative source requirement 口径校验；不得让前端本地推断时点、source、颜色或资源用途。
- 成功提交后横置 Rage Sigil source，创建 1 点 red typed temporary payment-only resource ledger，不创建普通 stack item，不进入可反应的法术/技能 stack。
- 资源 ledger 必须在 authoritative snapshot / prompt 中可见给 owner 和 spectator，至少包含 resource id、owner、sourceObjectId、abilityId、paymentWindow / lifecycle、generated red power、remaining red power、allowed payment kind、resourceRestriction、createdTick。
- 事件建议包含 `ABILITY_ACTIVATED`、source exhausted event、typed temporary resource created / power gained event；payload 应带 abilityId、sourceObjectId、effectKind、resourceId、generatedPowerByTrait.red=1、remainingPowerByTrait.red=1、paymentOnly=true、resourceRestriction。
- 所有校验失败必须 rejected no-mutation：source 保持 ready，temporary resources、runePool、stack、events、tick 不改变。

### 4.3 Typed Temporary Payment Consumption

- Generated red resource 必须可以在后续支付窗口中支付 red typed rune cost。
- 若现有 `PaymentCostRules.CanPayPowerCost` 允许 typed remaining power 支付 generic power after typed costs，则本切片可让 red temporary resource 支付 generic `A` cost；必须用测试固定该语义。
- Generated red resource 不得支付 blue / green / orange / purple / yellow typed-only cost，不得支付 mana-only cost，不得支付 experience 或 non-rune cost。
- Pending `PAY_COST` 与 inline payment windows 若都暴露 temporary resource choices，应保证 prompt quote 与 command commit 口径一致；若本切片只先覆盖 pending `PAY_COST` + selected inline representative，必须在 handoff audit 中明确边界。
- Consumption 应在同一 transaction 中扣减 / 清理 typed temporary ledger，并在 `COST_PAID` / resource consumption events 中记录 consumed typed red amount、remaining typed amount、paymentId / paymentWindow。
- Cleanup 应沿用 Malzahar temporary ledger 生命周期：支付关闭、回合资源清理或同等 cleanup 点清除剩余 resource。必须有显式测试证明不会跨越生命周期泄漏。

### 4.4 No-Mutation Guards

必须 rejected no-mutation：

- 未知 ability id、错误 cardNo、source 不存在。
- source 非当前玩家控制、隐藏 / face-down、非 equipment、非 base、已横置、未知 cardNo、不是 `SFD·222/221`。
- wrong timing：open-main、base neutral state、non-priority player、cleanup/task queue blocking、pending hand choice blocking、非合法 reaction/resource skill priority window。
- command 携带 target、2+ targets、optional costs、payment resource actions、unsupported costs。
- duplicate activation with exhausted source。
- attempted red temporary resource consumption in blue typed-only cost、mana-only cost、experience cost、wrong owner payment window、invalid / duplicate / unnecessary temporary resource action。

No-mutation 至少断言 tick、zones、cardObjects、source exhausted state、runePool、experience、score、stack、priority/focus、temporary payment resources 与 events 不变。

### 4.5 Regression Boundaries

- 不得破坏 `p2-preflight-play-rage-sigil-equipment` 和 `p4-play-rage-sigil-target-rejected`。
- 不得把 OGN `OGN·040/298` Rage Sigil 或其他 Sigil 自动变为 executable。
- 不得破坏 Malzahar temporary resource、Dragon Soul Sage reaction resource skill、Renata typed-blue costs、Shadow swift stun、Vi/Xerath activated payment resources 或 existing `RECYCLE_RUNE:*` quote / commit 口径。
- 不得让 frontend 读取卡面文本、颜色或 `temporaryPaymentResources` 自行推断可提交 command；前端后续只能展示服务端 ActionPrompt / snapshot 已公开的 choices。

## 5. 必补测试

Focused tests 至少覆盖：

- Catalog 中 Rage Sigil executable definition 存在，metadata 表达 base equipment source、reaction speed、typed red payment-only resource、no target、no stack。
- Prompt 在合法 reaction/resource skill priority state 暴露 Rage Sigil source requirement，metadata 含 generated red power、payment-only restriction、exhaust-as-cost 与 no-stack policy。
- Prompt 在 open-main、wrong priority、base source 但已横置、wrong controller / wrong cardNo / face-down / non-equipment / battlefield source 时不暴露。
- 成功命令横置 source、创建 red typed temporary payment-only resource、未创建普通 stack item、未改变 runePool mana/power。
- red temporary resource 可支付 red typed pending `PAY_COST`；若设计允许，也可支付 generic `A` pending/inline cost，并清理 ledger。
- red temporary resource 不能支付 blue typed pending `PAY_COST`、mana-only、experience 或 non-rune cost。
- 生命周期 cleanup 会移除未消费 red temporary resource。
- missing / extra target、unsupported optional costs、payment resource actions on activation、invalid source/timing/owner 全部 rejected no-mutation。
- existing Rage Sigil play fixtures、Malzahar temporary ledger、Dragon Soul Sage reaction resource skill、PaymentEngine / RunePool / ActionPrompt / GameHub adjacent regression 继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel"
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

- 不要实现完整 Sigil family 或 OGN / multicolor variants。
- 不要把 Rage Sigil resource skill 做成普通 stack item。
- 不要让该 resource skill 成为其他法术/技能的反应目标。
- 不要创建 generic temporary resource 伪装 red resource；必须保留 typed red 语义。
- 不要让 red temporary resource 支付 blue typed-only、mana-only、experience 或 non-rune costs。
- 不要修改前端运行时代码或 coverage matrix full-official 状态。
- 不要因为 4D-03R 通过而关闭 P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03R 是 Shadow 后的 PaymentEngine breadth slice，目标不是再清 deferred surface，而是补齐 typed payment-only resource skill representative。它可验证 base equipment source、reaction resource timing、typed red temporary payment ledger、prompt quote / command commit / cleanup 与 wrong-trait no-mutation；但不能替代 full PaymentEngine、完整 `[A]` / `[C]` resource skill family、完整 Sigil family、LayerEngine、覆盖矩阵 full-official 或最终 READY audit。
