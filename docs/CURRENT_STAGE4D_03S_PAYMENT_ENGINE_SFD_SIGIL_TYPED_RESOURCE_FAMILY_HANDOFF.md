# Stage 4D-03S PaymentEngine SFD Sigil Typed Resource Family Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03S 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03R 已验收 `SFD·222/221` 暴怒之印 / Rage Sigil typed red payment-only resource representative。4D-03S 继续收窄 P0-005，但只补同一 SFD 印记小家族中剩余五张普通颜色印记，不扩展到 OGN 复刻版或其他资源技能家族：

- `SFD·226/221`，卡名 `专注之印` / `Focus Sigil`，官方文本锚点：`{{横置}}：{{反应}}—{{获得}}{{绿色}}，用以支付符能费用。（获得费用资源的技能无法成为其他法术的反应目标。）`
- `SFD·229/221`，卡名 `洞察之印` / `Insight Sigil`，官方文本锚点：`{{横置}}：{{反应}}—{{获得}}{{蓝色}}，用以支付符能费用。（获得费用资源的技能无法成为其他法术的反应目标。）`
- `SFD·231/221`，卡名 `力量之印` / `Power Sigil`，官方文本锚点：`{{横置}}：{{反应}}—{{获得}}{{橙色}}，用以支付符能费用。（获得费用资源的技能无法成为其他法术的反应目标。）`
- `SFD·234/221`，卡名 `不和之印` / `Discord Sigil`，官方文本锚点：`{{横置}}：{{反应}}—{{获得}}{{紫色}}，用以支付符能费用。（获得费用资源的技能无法成为其他法术的反应目标。）`
- `SFD·238/221`，卡名 `团结之印` / `Unity Sigil`，官方文本锚点：`{{横置}}：{{反应}}—{{获得}}{{黄色}}，用以支付符能费用。（获得费用资源的技能无法成为其他法术的反应目标。）`

本切片应复用 4D-03R 已建立的 typed temporary payment ledger、stack-priority reaction timing、base equipment source guard、prompt metadata、command no-mutation 与 payment consumption 口径。成功路径为 controlled face-up ready base equipment source，在合法 reaction/resource payment window 中横置 source，立即生成对应颜色 1 点 payment-only temporary rune resource，用以支付符能费用，不创建普通 stack item，且该资源技能不能成为其他法术的反应目标。

不要同时实现 OGN 版 Sigil、完整 `[A]` / `[C]` resource skill family、Honeyfruit、Hextech Anomaly、Ancient Stele、Gold token、Lux、legend resource skills、coverage matrix full-official 升级或前端运行时代码。

## 2. 当前代码事实

- `CardBehaviorRegistry` 已有 SFD 六张 Sigil 的 0 费装备打出路径；普通入场和显式目标拒绝 fixtures 已在 `rules-evidence-index.md` 标记为 `RULE_AUDITED`。
- 4D-03R 已为 `P4ActivatedAbilityDefinition` 增加 `GeneratedPowerByTrait`，并为 `TemporaryPaymentResourceState` 增加 `GeneratedPowerByTrait` / `RemainingPowerByTrait`。
- `ResolveRageSigilResourceSkill` 已证明 base equipment source、stack-priority reaction window、no-stack resource skill、typed red temporary payment ledger、red/generic rune-cost consumption、wrong-trait rejection 与 cleanup lifecycle 的代表路径。
- `MatchSession` 目前仍有若干 Rage Sigil 专名判断，例如 temporary resource restriction、source requirement display 与 ability display name；4D-03S 应把这些口径参数化为 Sigil definition / trait metadata，避免为每种颜色复制一套分叉。
- `RuneTrait` / `PaymentCostRules.CanPayPowerCost` 已支持红、绿、蓝、橙、紫、黄 typed power；typed remaining power 可按既有语义支付对应颜色符能费用，并可在满足 typed costs 后参与 generic `A` 符能费用。
- 当前 OGN Sigil 只验证普通装备打出和目标拒绝，不能因为本切片自动变成 executable resource skill。

## 3. 建议写入范围

建议 owner：B 服务端规则 / 协议 / 测试实现。

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/PaymentCostRules.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/RageSigilResourceSkillTests.cs`
- 可新增窄域测试文件，例如 `tests/Riftbound.ConformanceTests/SfdSigilResourceSkillTests.cs`

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- OGN Sigil resource skills
- Malzahar、Dragon Soul Sage、Renata、Crimson Rose、Fluft Poro、Shadow 既有实现的非必要重构
- 完整 reaction/counter lifecycle 重写
- 未跟踪文件 `riftbound-dotnet.sln`

不可并行文件：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/PaymentCostRules.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/RageSigilResourceSkillTests.cs`
- 新增 SFD Sigil resource skill 测试文件

## 4. 实现要求

### 4.1 Catalog / Metadata

建议新增五个 executable activated ability definitions：

| Card | Trait | Ability id | Effect kind | Restriction |
| --- | --- | --- | --- | --- |
| `SFD·226/221` | `green` | `FOCUS_SIGIL_REACTION_EXHAUST_GAIN_1_GREEN_POWER` | `FOCUS_SIGIL_REACTION_TYPED_RESOURCE_GAIN_GREEN` | `PAY_RUNE_COSTS_ONLY_TYPED_GREEN_TEMPORARY_LEDGER_4D_03S` |
| `SFD·229/221` | `blue` | `INSIGHT_SIGIL_REACTION_EXHAUST_GAIN_1_BLUE_POWER` | `INSIGHT_SIGIL_REACTION_TYPED_RESOURCE_GAIN_BLUE` | `PAY_RUNE_COSTS_ONLY_TYPED_BLUE_TEMPORARY_LEDGER_4D_03S` |
| `SFD·231/221` | `orange` | `POWER_SIGIL_REACTION_EXHAUST_GAIN_1_ORANGE_POWER` | `POWER_SIGIL_REACTION_TYPED_RESOURCE_GAIN_ORANGE` | `PAY_RUNE_COSTS_ONLY_TYPED_ORANGE_TEMPORARY_LEDGER_4D_03S` |
| `SFD·234/221` | `purple` | `DISCORD_SIGIL_REACTION_EXHAUST_GAIN_1_PURPLE_POWER` | `DISCORD_SIGIL_REACTION_TYPED_RESOURCE_GAIN_PURPLE` | `PAY_RUNE_COSTS_ONLY_TYPED_PURPLE_TEMPORARY_LEDGER_4D_03S` |
| `SFD·238/221` | `yellow` | `UNITY_SIGIL_REACTION_EXHAUST_GAIN_1_YELLOW_POWER` | `UNITY_SIGIL_REACTION_TYPED_RESOURCE_GAIN_YELLOW` | `PAY_RUNE_COSTS_ONLY_TYPED_YELLOW_TEMPORARY_LEDGER_4D_03S` |

每个 definition 应表达 `IsResourceSkill=true`、`ReactionSpeed=true`、`PaymentOnlyResource=true`、`RequiresBaseEquipmentSource=true`、`ExhaustsSourceAsCost=true`、`RequiredTargetCount=0`、`GeneratedPowerByTrait.<trait>=1`。

建议新增共享 helper，例如 `IsSfdSigilTypedResourceAbility`、`TryGetSigilTypedResourceProfile`、`GeneratedPowerByTraitForAbility` 复用，保持 Rage Sigil 与本切片五张 SFD Sigil 使用同一个 source / prompt / command / temporary ledger path。

### 4.2 Prompt / Command

- Source 必须是当前玩家控制、公开、正面、未横置、cardNo 为对应 SFD Sigil、位于 controller base 的 equipment object。
- Prompt 只在服务端已有合法 stack-priority reaction/resource skill priority window 暴露；保持 Dragon Soul Sage / Rage Sigil 的 timing policy，不为本切片重写完整 reaction lifecycle。
- Prompt metadata 应暴露 `resourceSkill=true`、`reactionSpeed=true`、`typedPaymentOnlyResource=true`、`generatedPowerByTrait.<trait>=1`、`resourceRestriction`、`resourceLifecycle=temporary-payment-resource-ledger`、`requiresBaseEquipmentSource=true`、`exhaustsSource=true`、`requiredTargetCount=0`、`stackPolicy=no-ordinary-stack-item`。
- Prompt 不得要求或暴露目标、optional costs、payment resource actions 或普通 stack target choices。
- 命令侧必须通过 `ACTIVATE_ABILITY` 服务端 authoritative source requirement 口径校验；不得让前端本地推断时点、source、颜色或资源用途。
- 成功提交后横置 source，创建对应颜色 1 点 typed temporary payment-only resource ledger，不创建普通 stack item，不进入可反应的法术/技能 stack。
- 事件 payload 应带 abilityId、sourceObjectId、effectKind、resourceId、generatedPowerByTrait、remainingPowerByTrait、paymentOnly=true、resourceRestriction、resourceLifecycle。

### 4.3 Typed Temporary Payment Consumption

- 每个 Sigil 生成的 typed temporary resource 必须可以支付同色 typed rune cost。
- 若既有 `PaymentCostRules.CanPayPowerCost` 允许 typed remaining power 支付 generic `A` cost，应保留该语义并用测试固定。
- 每个 Sigil 生成的 typed temporary resource 不得支付其他颜色 typed-only cost，不得支付 mana-only cost，不得支付 experience 或 non-rune cost。
- Pending `PAY_COST` 与 inline payment windows 的 quote / commit 口径必须继续一致。若本切片只覆盖 pending `PAY_COST` 和既有 representative inline windows，需在实现审计中明确边界。
- Consumption 应扣减 / 清理 typed temporary ledger，并在 `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `COST_PAID` 中记录 consumed typed amount、remaining typed amount、paymentId / paymentWindow。
- Cleanup 沿用 Malzahar / Rage Sigil temporary ledger 生命周期：支付关闭、回合资源清理或同等 cleanup 点清除剩余 resource。

### 4.4 No-Mutation Guards

必须 rejected no-mutation：

- 未知 ability id、错误 cardNo、source 不存在。
- source 非当前玩家控制、隐藏 / face-down、非 equipment、非 base、已横置、未知 cardNo。
- wrong timing：open-main、base neutral state、non-priority player、cleanup/task queue blocking、pending hand choice blocking、非合法 reaction/resource skill priority window。
- command 携带 target、2+ targets、optional costs、payment resource actions、unsupported costs。
- duplicate activation with exhausted source。
- attempted typed temporary resource consumption in wrong-color typed cost、mana-only cost、experience cost、wrong owner payment window、invalid / duplicate / unnecessary temporary resource action。
- OGN `OGN·081/298` / `OGN·120/298` / `OGN·163/298` / `OGN·204/298` / `OGN·245/298` source 使用 SFD ability id 或同名 skill id。

No-mutation 至少断言 tick、zones、cardObjects、source exhausted state、runePool、experience、score、stack、priority/focus、temporary payment resources 与 events 不变。

## 5. 必补测试

Focused tests 至少覆盖：

- Catalog 中五个 SFD Sigil executable definitions 存在，metadata 表达 base equipment source、reaction speed、typed payment-only resource、no target、no stack。
- Prompt 在合法 reaction/resource skill priority state 暴露五种 SFD source requirement；metadata 含对应 generatedPowerByTrait、payment-only restriction、exhaust-as-cost 与 no-stack policy。
- Prompt 在 open-main、wrong priority、source 已横置、wrong controller / wrong cardNo / face-down / non-equipment / battlefield source 时不暴露。
- 成功命令对五种 Sigil 分别横置 source、创建对应颜色 typed temporary payment-only resource、未创建普通 stack item、未改变 runePool mana/power。
- typed temporary resource 可支付同色 pending `PAY_COST`；若设计允许，也可支付 generic `A` pending/inline cost，并清理 ledger。
- typed temporary resource 不能支付其他颜色 typed pending `PAY_COST`、mana-only、experience 或 non-rune cost。
- 生命周期 cleanup 会移除未消费 typed temporary resource。
- missing / extra target、unsupported optional costs、payment resource actions on activation、invalid source/timing/owner 全部 rejected no-mutation。
- OGN Sigil 在本切片仍不可执行 resource skill，即使它们已作为普通 base equipment 入场。
- Existing Rage Sigil tests、SFD/OGN Sigil play fixtures、Malzahar temporary ledger、Dragon Soul Sage reaction resource skill、PaymentEngine / RunePool / ActionPrompt / GameHub adjacent regression 继续通过。

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

- 不要实现 OGN Sigil resource skills。
- 不要把任何 Sigil resource skill 做成普通 stack item。
- 不要让这些 resource skills 成为其他法术/技能的反应目标。
- 不要创建 generic temporary resource 伪装 typed resource；必须保留具体颜色 typed 语义。
- 不要让 typed temporary resource 支付错误颜色、mana-only、experience 或 non-rune costs。
- 不要修改前端运行时代码或 coverage matrix full-official 状态。
- 不要因为 4D-03S 通过而关闭 P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03S 是 4D-03R 后的 Sigil family breadth slice，目标是把已证明的 typed payment-only ledger 从单一 red representative 扩展到剩余五张 SFD 官方颜色印记，同时明确 OGN 复刻版仍 deferred。它可验证服务端 typed resource metadata 参数化、五色 prompt / command / payment / cleanup parity 与 OGN no-executable guard；但不能替代 full PaymentEngine、完整 `[A]` / `[C]` resource skill family、完整 OGN family、LayerEngine、覆盖矩阵 full-official 或最终 READY audit。
