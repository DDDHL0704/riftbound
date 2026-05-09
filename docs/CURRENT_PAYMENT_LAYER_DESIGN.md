# PaymentEngine / LayerEngine 第一阶段设计草案

本文只记录后续拆分设计，不要求本轮修改 `src/**` 或 `tests/**`。

## 当前落地状态

更新日期：2026-05-09

- 已新增 `src/Riftbound.Engine/PaymentCostRules.cs`，先集中符文/符能/经验支付原语。
- `PaymentCostRules` 已增加 `BuildPaymentId` 与 `BuildCostPaidPayload`，为支付事件提供兼容包络。
- `PLAY_CARD`、`ASSEMBLE_EQUIPMENT`、伏击打出、启动技能、传奇行动、待命埋伏、待命翻开反应的 `COST_PAID` 已带 `paymentId/paymentWindow/remainingMana/remainingPower/remainingPowerByTrait/remainingExperience`；支付资源动作 `RUNE_RECYCLED` / `POWER_GAINED` 已带同一个 `paymentId`。
- `CoreRuleEngine` 现有私有支付方法仍保留为薄包装，调用点暂不大范围迁移。
- 本次只做行为不变的最小抽取；尚未实现真正 `PaymentEngine Quote/Authorize/Commit`，也未新增 `PAY_COST` prompt 或 pending payment state。
- 已验证：Engine build 通过，Payment/费用聚焦测试 195/195 通过，`paymentId` 聚焦 Hub 测试 2/2 通过，命令窗口费用聚焦测试 193/193 通过，后端 full conformance 3308/3308 通过，DevUi build 通过，`git diff --check` 通过。

## 目标

- 把费用计算、可选费用、额外费用、符能/法力/经验支付、减费/加费、支付资源动作从 `CoreRuleEngine` 与 `ActionPromptBuilder` 的重复逻辑里抽出来。
- 把战力基础值、临时修正、持续效果、关键词/文字变更和 snapshot 展示从直接改 `CardObjectState.Power/Tags/UntilEndOfTurnEffects` 的散点逻辑里抽出来。
- 第一阶段优先提供可复用的计划、校验、提示 metadata 和事件 payload，不一次性迁移全部卡牌行为。

## 当前散落点

### Payment 相关

- `CoreRuleEngine.cs` 顶部定义了大量费用 token 和卡牌专用常量：`STANDBY_*`、`ASSEMBLE_*`、`SPEND_MANA:*`、`SPEND_POWER:*`、`SPEND_EXPERIENCE:*`、`RECYCLE_RUNE:*`、摧毁/横置/弃牌/返回装备等额外费用前缀。
- `ResolvePlayCard` 先调用 `TryBuildPlayCardPlan`，随后自行移除手牌、应用回收符文支付资源动作、扣法力/符能/经验、创建 stack item、消费减费效果、发 `COST_PAID`，再逐个执行横置、摧毁、返回、弃牌等额外费用副作用。
- `TryBuildPlayCardPlan` 同时承担目标校验、可选费用解析、额外费用合法性、减费/加费计算、法力/符能/经验资源检查、回收符文是否必要等职责。
- `TryBuildOptionalCostPlan` 负责解析回响、急速活跃、新月禁卫、横置友方单位、摧毁友方单位/强力单位/指定类型单位、返回装备、目标效果额外费用、支付符能伤害、支付经验减费、弃牌减费等多种 token。
- `PayRuneCosts`、`CanPayRuneCosts`、`CanPayPowerCost`、`PayPowerCost`、`NormalizePowerCostByTrait` 是底层扣资源工具，但调用点分散在打牌、技能、传奇行动、待命、战场动作等路径。
- `ApplyRecycleRunePaymentResourceActions` 同时修改 `PlayerZones`、`CardObjects`、`ObjectLocations`、`RunePools`，并发 `RUNE_RECYCLED` 与 `POWER_GAINED`；它既是支付前资源动作，又是状态迁移。
- `PayExperienceCosts` 只做扣经验，`GainExperience` 和“本回合获得经验”标记在 stack 结算阶段另行处理。
- `BuildCostPaidPayload` 目前已包络主要玩家命令窗口；战场触发/替代费用等自动支付旧 `COST_PAID` 路径仍待迁移。这些深层 helper 需要先传入当前 action tick，避免生成 `tick=0` 一类不可审计的 `paymentId`。
- `ResolveCostReductionMana`、战场回响减费、装备减费、狂暴龙怪下一张法术减费、法术战场减费、战场单位加费、法术护盾税分散在 `CoreRuleEngine`，同时在 `MatchSession.ActionPromptBuilder` 有 prompt 版本。
- `ResolveAmbushPlayCard`、`ResolveActivateAbility`、`ResolveLegendAction`、`ResolveStandbyHide`、装备装配、战场支付 4 符能得分等路径绕过 `TryBuildPlayCardPlan`，直接检查资源并扣费。
- `MatchSession.cs` 的 `ActionPromptBuilder` 复制了费用 token、装配 profile、减费计算、支付资源 choices、可选费用 choices 和 prompt metadata 组装。
- `CardBehaviorRegistry.cs` 的 `CardBehaviorDefinition` 同时承载牌面费用、回响费用、减费条件、经验费用、急速活跃费用、额外费用、源单位额外支付、目标效果额外支付等 payment 维度。

### Layer / 持续效果相关

- `CardObjectState` 目前保存的是有效 `Power`，另用 `UntilEndOfTurnPowerModifier` 记录本回合临时总修正；基础战力在 snapshot 中通过 `Power - UntilEndOfTurnPowerModifier` 反推。
- `UntilEndOfTurnEffects` 同时表示对象持续效果、状态文字、破坏替代、伤害预防、移动许可等，`MatchState.UntilEndOfTurnEffects` 又存全局/玩家级持续效果。
- `ApplyPowerModifier` 直接修改 `Power` 并累加 `UntilEndOfTurnPowerModifier`，还顺手触发菲奥娜达到强力后的关键词补全。
- `ApplyBoon` 直接永久增加 `Power` 并添加 `Boon` tag；部分获得增益后的触发也在这里执行。
- `PlaySourceUnitToBase` / `PlaySourceUnitToBattlefield` 计算入场战力、等级经验加成、墓地数量加成、条件战力/关键词、急速活跃和新月禁卫活跃。
- 一些区域迁移函数会手动清除伤害、`UntilEndOfTurnEffects`、`UntilEndOfTurnPowerModifier`，并通过反推方式重置 `Power`。
- `ApplyTurnEndCleanup` 统一清理伤害、对象/全局本回合效果、临时战力修正，但依赖 `UntilEndOfTurnPowerModifier` 的累加值正确。
- `BuildContinuousEffectStates` 和 object snapshot 在 `MatchSession.cs` 派生 `basePower`、`effectivePower`、`continuousEffects`，不是规则计算的源头。
- 战斗、伤害、目标限制、动态伤害和复制 token 等路径多数直接读 `CardObjectState.Power` / `Tags`，未来需要统一读 LayerEngine 的对象视图。

## PaymentEngine 第一阶段接口

第一阶段采用三段式接口：`Quote` 给 prompt，`Authorize` 给命令校验，`Commit` 给资源/支付资源动作落状态。额外费用造成的摧毁、返回、弃牌等对象移除，第一阶段仍由现有 `CoreRuleEngine` 执行，PaymentEngine 只返回已校验的 commitment。

```csharp
public interface IPaymentEngine
{
    PaymentQuote Quote(PaymentQuoteRequest request);

    PaymentAuthorizationResult Authorize(PaymentAuthorizationRequest request);

    PaymentCommitResult Commit(PaymentCommitRequest request);
}
```

### 输入

`PaymentQuoteRequest` / `PaymentAuthorizationRequest` 最小字段：

- `PaymentWindow Window`：`PlayCard`、`Ambush`、`ActivateAbility`、`LegendAction`、`StandbyHide`、`AssembleEquipment`、`BattlefieldAction`。
- `string PlayerId`、`string SourceObjectId`、`string? CardNo`、`string? AbilityId`、`string? EffectKind`。
- `PaymentCostSpec CostSpec`：基础法力、任意符能、指定颜色符能、经验、回响费用、可选费用规格、额外费用规格、是否允许支付资源动作。
- `PaymentContext Context`：当前 `MatchState` 的只读切片，包括 `RunePools`、`PlayerExperience`、`PlayerZones`、`CardObjects`、`ObjectLocations`、`UntilEndOfTurnEffects`、`PlayerCardsPlayedThisTurn`、目标列表、当前阶段/时点。
- `IReadOnlyList<string> SubmittedPaymentTokens`：命令提交的 optional cost / required cost token，包括 `SPEND_*`、`RECYCLE_RUNE:*`、额外费用目标 token。
- `IReadOnlyList<string> TargetObjectIds`：用于法术护盾税、目标额外费用、按目标战力减费、目标合法性补充校验。

`PaymentCostSpec` 不直接引用整张 `CardBehaviorDefinition`，避免 PaymentEngine 继续依赖大而全的卡牌行为 record。第一阶段可由适配器从 `CardBehaviorDefinition` / `LegendAbilityDefinition` / 装配 profile 转出。

### 输出

`PaymentAuthorizationResult`：

- `bool IsValid`。
- `PaymentError? Error`。
- `PaymentPlan? Plan`。

`PaymentPlan` 最小字段：

- `PaymentIdentity Identity`：`paymentId`、`window`、`playerId`、`sourceObjectId`、`cardNo`、`abilityId`。
- `PaymentCostBreakdown Costs`：
  - `baseMana`、`baseAnyPower`、`basePowerByTrait`、`baseExperience`。
  - `optionalExtraMana`、`optionalExtraAnyPower`、`optionalExtraPowerByTrait`、`optionalExperience`。
  - `costReductionMana`、`optionalCostManaReduction`、`battlefieldEchoCostReductionMana`、`battlefieldEquipmentCostReductionMana`、`nextSpellCostReductionMana`、`battlefieldSpellCostReductionMana`。
  - `costIncreaseMana`、`battlefieldHeldUnitCostIncreaseMana`、`spellshieldTaxMana`。
  - `totalMana`、`totalAnyPower`、`totalPowerByTrait`、`totalPower`、`totalExperience`。
- `IReadOnlyList<string> AcceptedPaymentTokens`：保留当前协议 token，兼容前端。
- `IReadOnlyList<PaymentResourceAction> ResourceActions`：例如回收符文支付资源，包含 `actionId`、`objectId`、`trait`、`powerGain`、`required`。
- `IReadOnlyList<AdditionalCostCommitment> AdditionalCosts`：例如横置、摧毁、返回、弃牌、回收废牌堆，包含 `kind`、`targetObjectId`、`reason`。
- `PaymentResolutionFlags Flags`：`effectRepeatCount`、`hasteReadyPaid`、`crescentGuardReadyPaid`、`echoPaid`、`damageAmountFromPaidPower` 等当前 stack item 仍需要读取的结果。
- `PaymentAdjustmentConsumption Consumption`：会被消费的减费 effect ids，例如狂暴龙怪下一张法术减费。

`PaymentCommitResult`：

- `IReadOnlyDictionary<string, RunePool> RunePools`。
- `IReadOnlyDictionary<string, int> PlayerExperience`。
- `IReadOnlyDictionary<string, PlayerZones> PlayerZones`、`CardObjects`、`ObjectLocations`：第一阶段仅允许支付资源动作造成的符文回收迁移；额外费用对象移除仍由调用方处理。
- `IReadOnlyList<GameEvent> Events`：只包含资源支付/支付资源动作事件。
- `PaymentPlan Plan`：原样返回，便于调用方继续处理 stack、额外费用和触发。

### 错误

`PaymentError` 最小字段：

- `PaymentErrorCode Code`。
- `string Message`。
- `string? Token`。
- `string? TargetObjectId`。
- `string? ResourceKind`。
- `IReadOnlyDictionary<string, object?> Details`。
- `string CoreErrorCode`：映射到现有 `ErrorCodes`，第一阶段保持外部错误语义不变。

建议错误码：

- `UnsupportedPaymentWindow`：该 window 暂无规格适配。
- `InvalidPaymentToken`：token 格式不合法或不属于当前费用规格。
- `InvalidPaymentResourceAction`：例如 `RECYCLE_RUNE:*` 指向非法对象。
- `PaymentResourceActionNotRequired`：当前已有资源足够，不允许多回收符文。
- `InvalidAdditionalCostTarget`：额外费用目标不满足友方、单位、强力、装备等条件。
- `MissingRequiredCost`：必需费用 token / 额外费用未提交。
- `InsufficientMana`、`InsufficientAnyPower`、`InsufficientTraitPower`、`InsufficientExperience`。
- `StateVersionMismatch`：后续若引入 `tick` / prompt revision，用于防止旧 prompt 支付。

### 事件 payload

第一阶段保留现有事件名，补齐统一 payload 字段，不要求前端改变裁决原则。

`COST_PAID` 建议 payload：

- `paymentId`、`paymentWindow`、`playerId`、`sourceObjectId`、`cardNo`、`abilityId`。
- `mana`、`power`、`powerByTrait`、`experience`。
- `baseMana`、`basePower`、`basePowerByTrait`、`baseExperience`。
- `costReductionMana`、`optionalCostManaReduction`、`battlefieldEchoCostReductionMana`、`battlefieldEquipmentCostReductionMana`、`nextSpellCostReductionMana`、`battlefieldSpellCostReductionMana`。
- `battlefieldHeldUnitCostIncreaseMana`、`spellshieldTaxMana`、`spellshieldTaxTargetObjectIds`。
- `optionalCosts`、`requiredCosts`、`additionalCosts`、`paymentResourceActions`、`recycledRuneObjectIds`。
- `remainingMana`、`remainingPower`、`remainingPowerByTrait`、`remainingExperience`。

支付资源动作继续发：

- `RUNE_RECYCLED`：`paymentId`、`paymentWindow`、`playerId`、`sourceObjectId`、`cardNo`、`trait`、`power`、`runeDeckCountAfter`。
- `POWER_GAINED`：`paymentId`、`paymentWindow`、`playerId`、`sourceObjectId`、`trait`、`power`、`powerAfter`、`traitPowerAfter`。

经验支付可统一进 `COST_PAID`，但第一阶段为兼容传奇行动也可继续发 `EXPERIENCE_SPENT`，payload 加 `paymentId`、`paymentWindow`、`amount`、`remainingExperience`。

### Prompt metadata

`PaymentQuote` 最小字段：

- `PaymentIdentity Identity`。
- `PaymentCostBreakdown PreviewCosts`：包含基础费用、最低法力、各项可见减费/加费预估。
- `IReadOnlyList<ActionPromptChoiceDto> OptionalCostChoices`。
- `IReadOnlyList<ActionPromptChoiceDto> AdditionalCostChoices`。
- `int RequiredAdditionalCostChoiceCount`。
- `IReadOnlyList<ActionPromptChoiceDto> PaymentResourceChoices`。
- `IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> PaymentResourcePowerByChoice`。
- `IReadOnlyDictionary<string, int> AvailablePowerByTrait`。
- `IReadOnlyDictionary<string, int> AvailablePowerByTraitWithPaymentResources`。
- `IReadOnlyList<string> RequiredPaymentTokens`。
- `IReadOnlyList<PaymentCombinationHint> LegalCombinations`：可选，第一阶段可只用于复杂费用，例如目标效果同时要求法力和指定符能。
- `bool Composable`、`string? UnsupportedReason`。

迁移时，`ActionPromptBuilder` 不再自己复制减费与支付资源规则，而是从 `PaymentQuote` 填充现有 prompt extras 字段。

## LayerEngine 第一阶段接口

第一阶段不追求完整规则层系统，只把当前“有效 Power 直接写回对象”的做法包成统一视图和统一效果申请。

```csharp
public interface ILayerEngine
{
    LayeredObjectView EvaluateObject(LayerEvaluationRequest request);

    LayerApplicationResult Apply(LayerApplicationRequest request);

    LayerCleanupResult Expire(LayerExpirationRequest request);
}
```

### 输入

`LayerEvaluationRequest`：

- `string ObjectId`。
- `CardObjectState ObjectState`。
- `LayerContext Context`：只读 `PlayerZones`、`CardObjects`、`PlayerExperience`、`UntilEndOfTurnEffects`、对象位置、控制者、当前事件/stack context。
- `IReadOnlyList<LayerEffectInstance> Effects`：第一阶段可从 `ObjectState.UntilEndOfTurnPowerModifier`、`ObjectState.UntilEndOfTurnEffects`、全局 `UntilEndOfTurnEffects` 适配生成。

`LayerApplicationRequest`：

- `string SourceObjectId`、`string TargetObjectId`、`string? CardNo`、`string? EffectKind`。
- `LayerEffectSpec Spec`：层、持续时间、数值、关键词/标签、文字变更。
- `StackItemState? StackItem` 或轻量 `LayerSourceContext`。

### 基础 power

`LayeredObjectView` 至少包含：

- `printedPower`：后续若牌库提供印刷值，用于快照；第一阶段可为空或等于 `basePower`。
- `basePower`：当前对象去掉临时战力修正后的值。兼容现状时可用 `Math.Max(0, Power - UntilEndOfTurnPowerModifier)`。
- `effectivePower`：所有已应用 layer 后的最终战力，第一阶段等于当前 `CardObjectState.Power`。
- `damage`、`isExhausted`、`isFaceDown`、`isAttacking`、`isDefending`。
- `tags`：当前有效标签。
- `keywords`：从 tags 中拆出的关键词视图，第一阶段可与 tags 同源。
- `textEffects`：对象/全局文字变更效果，第一阶段只记录，不改变前端裁决。

入场基础战力计算建议由适配器交给 LayerEngine：

- `SourceUnitPower` 优先于已有对象 power。
- 等级经验加成、墓地数量加成、已打出 4 费法术、已获得经验等条件加成转为 `LayerEffectSpec`，不要散落在 `PlaySourceUnitToBase/Battlefield`。
- 永久增益，例如 `Boon` 的 +1，第一阶段仍可落入 `basePower`，但事件和 tag 由 LayerEngine 统一返回。

### 修正

`LayerEffectSpec` 建议字段：

- `effectId`、`sourceObjectId`、`targetObjectId`。
- `LayerKind`：`BasePowerSet`、`PowerModifier`、`KeywordAdd`、`KeywordRemove`、`TextAdd`、`TextRemove`、`StatusEffect`。
- `DurationKind`：`Permanent`、`UntilEndOfTurn`、`UntilSourceLeavesZone`、`UntilObjectLeavesZone`、`WhileConditionTrue`。
- `int? setBasePower`、`int powerDelta`、`int minimumPower`。
- `IReadOnlyList<string> addedTags`、`removedTags`。
- `IReadOnlyList<string> addedKeywords`、`removedKeywords`。
- `string? textEffectId`。
- `string reason`。

`Apply` 返回：

- `CardObjectState TargetState`。
- `LayerEffectInstance Effect`。
- `IReadOnlyList<GameEvent> Events`。
- `LayeredObjectView Before`、`After`。

第一阶段事件名保持：

- `POWER_MODIFIED_UNTIL_END_OF_TURN`。
- `OBJECT_TAG_ADDED`。
- `BOON_GRANTED`。
- `STATUS_EFFECT_APPLIED`。
- 需要时保留 `UNIT_READIED` / `UNIT_EXHAUSTED`，但它们不是 layer 核心。

### 持续时间

第一阶段明确支持：

- `Permanent`：例如增益 `Boon` 的基础战力 +1 和 tag。
- `UntilEndOfTurn`：当前 `UntilEndOfTurnPowerModifier` 与 `UntilEndOfTurnEffects`。
- `UntilSourceLeavesZone`：先只作为 effect metadata，不急于实现动态移除。
- `WhileConditionTrue`：先只作为计算视图 metadata，用于后续战场静态效果/关键词文字；第一阶段不要求替换现有战场逻辑。

`Expire` 负责替代当前回合末散点清理：

- 输入当前对象、全局效果、过期条件。
- 输出清理后的对象、过期 effect ids、过期 power modifier object ids。
- 兼容当前行为：清伤害、清对象本回合效果、将 `Power` 减去 `UntilEndOfTurnPowerModifier`、清零 modifier。

### 关键词 / 文字变更

- 关键词第一阶段仍落在 `Tags`，但 LayerEngine 事件和 snapshot 应能表达来源：例如菲奥娜达到强力获得 `Spellshield`、`Roam`、`Steadfast`。
- 状态文字继续使用 `UntilEndOfTurnEffects` 的 effect id，例如伤害预防、下次受伤摧毁、眩晕、OVERWHELM 等。
- `TextAdd` / `TextRemove` 先不改变规则解析，只作为 `ContinuousEffectState` / snapshot 的字段，为后续“失去/获得文字”保留接口。

### Snapshot 字段

对象 snapshot 建议保持现有字段并新增来源型字段：

- `power`：兼容现有字段，可继续等于 `effectivePower`。
- `printedPower`：可选，第一阶段没有来源时为 `null`。
- `basePower`。
- `effectivePower`。
- `damage`。
- `untilEndOfTurnPowerModifier`。
- `powerModifiers`：数组，每项含 `effectId`、`sourceObjectId`、`powerDelta`、`duration`、`layer`、`reason`。
- `tags`。
- `keywords`：从 tags 派生，便于前端展示但不参与裁决。
- `keywordEffects`：数组，每项含 `effectId`、`sourceObjectId`、`addedKeywords`、`removedKeywords`、`duration`。
- `textEffects`：数组，每项含 `effectId`、`sourceObjectId`、`textEffectId`、`duration`。
- `untilEndOfTurnEffects`：保留兼容。

`continuousEffects` snapshot 建议扩充：

- `effectId`、`scope`、`layer`、`duration`、`sourceObjectId`、`targetObjectId`。
- `powerDelta`、`basePower`、`effectivePower`。
- `addedTags`、`removedTags`、`addedKeywords`、`removedKeywords`。
- `textEffectId`、`reason`。

## 第一阶段迁移步骤

1. 新增 PaymentEngine 类型与 `CardBehaviorDefinition -> PaymentCostSpec` 适配器，不改行为。
2. 用 PaymentEngine 先替换 `ActionPromptBuilder` 的 play-card quote 路径，snapshot/prompt 字段保持原名。
3. 用 PaymentEngine 替换 `TryBuildPlayCardPlan` 的费用解析、减费、资源检查，保留 `PlayCardPlan` 外壳以降低调用方改动。
4. 把回收符文支付资源动作和经验/符能扣除迁入 `Commit`，额外费用对象移除暂留 `CoreRuleEngine`。
5. 再逐步接入 Ambush、ActivateAbility、LegendAction、StandbyHide、AssembleEquipment、BattlefieldAction。
6. 新增 LayerEngine 视图，不先改战斗读取；先让 object snapshot 和 `continuousEffects` 来自 LayerEngine。
7. 用 LayerEngine 包装 `ApplyPowerModifier`、`ApplyBoon`、`ApplyTargetTag`、回合末清理，再逐步替换直接读写 `Power` 的战斗/目标限制路径。

## 不做范围

- 不一次性重构所有卡牌定义，不要求把 `CardBehaviorDefinition` 立刻拆成多个规则组件。
- 不改变前端裁决原则：服务端仍是唯一规则裁决方，前端只消费 prompt、events、snapshot。
- 不在第一阶段实现完整动态 layer 依赖、持续源离场自动失效、复杂文字替换或全卡牌 oracle 文本系统。
- 不改变现有命令 token 协议；`SPEND_*`、`RECYCLE_RUNE:*`、额外费用 target token 先继续兼容。
- 不把区域移动、摧毁、弃牌等通用 zone/lifecycle 行为塞进 PaymentEngine；第一阶段只让 PaymentEngine 产出额外费用 commitment。

## 最高风险

- 最高行为风险是 prompt 与 resolution 当前有两套费用/减费逻辑。拆分时如果只替换一侧，会出现前端显示可支付但服务端拒绝，或服务端允许前端没有提示的路径。
- 最高状态风险是当前 `Power` 同时承担基础值和有效值，临时修正靠 `UntilEndOfTurnPowerModifier` 反推。LayerEngine 接入时若漏掉区域迁移和回合末清理，可能永久化临时战力或错误清掉永久增益。
- 最高集成风险是回收符文支付资源动作同时改资源、区域、对象状态和事件。PaymentEngine 第一阶段必须把这类动作作为原子 commit，否则会出现资源已增加但符文未移回符文牌堆的半状态。
