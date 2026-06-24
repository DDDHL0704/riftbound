# 方案 B / B1：STATIC_AURA 通用化 spike

> 状态：spike 结论与最小实现方案；未改 gameplay 代码。
> 工作区：`/Users/dinghaolin/MyProjects/riftbound-dotnet`
> 依据：`AGENTS.md`、`docs/PLAN_B_engine_auto_resolution.md`、`docs/rules-authority-and-audit.md`、`docs/rules-evidence-index.md`、`data/official/card-catalog.zh-CN.json`。

## 1. 结论

B1 不能只改 `MatchSession`。当前 BehaviorSpec 还没有可执行的静态光环模型，只能把关键字、目标、触发、替代、激活技能、静态文本和基础动作模板暴露给 UI/覆盖矩阵。

因此最小正确路径是：

1. 在 `Riftbound.Contracts` / `Riftbound.CardCatalog` 增加 `StaticAuraSpec` 数据模型。
2. 先让官方文本里的两个已实现 representative 生成结构化 aura spec：
   - `SFD·085/221` / `SFD·085a/221` 奥恩：每有一件友方装备，自身获得 `{{S}}+1`。
   - `OGN·294/298` 崔法利兵营：此处所有单位获得 `{{S}}+1`。
3. 引擎连续效果投影和战斗战力读取从 spec 求值，不再读取卡号常量或 registry 布尔字段。
4. 保留现有快照 shape 的兼容性，避免一次性改动 DevUi 和 recovery payload 协议。

## 2. 已确认的现状

### BehaviorSpec

相关文件：

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.DevUi/src/types/catalog.ts`

当前 `BehaviorSpec` 字段：

- `Cost`
- `Keywords`
- `Targets`
- `Triggers`
- `Replacements`
- `ActivatedAbilities`
- `StaticAbilities`
- `Effects`
- `TemplateIds`

`StaticAbilitySpec` 只有：

```csharp
public sealed record StaticAbilitySpec(
    string Kind,
    string Text,
    string Status,
    string Reason);
```

它是展示/骨架字段，不足以表达 aura 的作用域、层、目标集合、参与对象集合、战力增量和生命周期。

### 当前 STATIC_AURA 硬编码点

相关文件：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- `src/Riftbound.Engine/MatchRecovery.cs`

当前硬编码路径：

- `MatchSession.cs`
  - `ContinuousEffectStaticAuraCards.BattlefieldAllUnitsPowerPlusOneCardNo = "OGN·294/298"`
  - `TryBuildFriendlyEquipmentStaticAuraEffect(...)` 读取 `CardBehaviorDefinition.AddsFriendlyFieldEquipmentCountToSourceUnitPower`
  - `BuildBattlefieldAllUnitsStaticAuraEffects(...)` 直接判断 `OGN·294/298`
  - `BattlefieldStaticAuraParticipantObjectIds(...)` 是战场全体 +1 的专用求集合函数
- `CoreRuleEngine.cs`
  - `ApplyFriendlyEquipmentStaticPowerRecompute(...)` 读取 registry 布尔字段重算奥恩战力
  - `ResolveBattlefieldAllUnitsPowerBonus(...)` 直接判断 `OGN·294/298`
- `CardBehaviorRegistry.cs`
  - `SFD·085/221` / `SFD·085a/221` 通过 `AddsFriendlyFieldEquipmentCountToSourceUnitPower: true` 接入
- `CardEquipmentKeywordRules.cs`
  - `FriendlyEquipmentStaticPowerRepresentativeCardNos` 写死奥恩两个卡号
- `MatchRecovery.cs`
  - 对两类 static aura 的 `effectId`、metadata、`sourceCardNo`、`sourcePath`、`condition`、`lifecycle` 有专用校验

### 当前测试覆盖

已有代表性测试可以保留为回归，但断言会随着 spec-driven 改造调整：

- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`
- `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

现有覆盖已经包括：

- 奥恩按友方公开 field 装备数量动态重算。
- 装备离开/敌方装备/暗牌装备不会泄漏或计入。
- 崔法利兵营只影响同一战场单位。
- source / participant 离场后 continuous effect metadata 消失。
- spectator recovery 对 static aura payload 有大量形状与规范性校验。

这意味着 B1 的风险不是缺测试，而是测试现在锁死了两种硬编码 effect 形状。

## 3. 最小 StaticAuraSpec 设计

建议新增在 `src/Riftbound.Contracts/BehaviorSpecs.cs`：

```csharp
public static class StaticAuraKinds
{
    public const string FriendlyFieldEquipmentCountToSourceUnitPower =
        "FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER";
    public const string BattlefieldAllUnitsPowerPlusOne =
        "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE";
}

public static class StaticAuraScopes
{
    public const string SourceObject = "SOURCE_OBJECT";
    public const string SameBattlefieldUnits = "SAME_BATTLEFIELD_UNITS";
}

public static class StaticAuraParticipantScopes
{
    public const string FriendlyPublicFieldEquipment = "FRIENDLY_PUBLIC_FIELD_EQUIPMENT";
    public const string SameBattlefieldPublicUnits = "SAME_BATTLEFIELD_PUBLIC_UNITS";
}

public sealed record StaticAuraSpec(
    string Kind,
    string Layer,
    string Duration,
    string TargetScope,
    string ParticipantScope,
    int PowerDeltaPerParticipant,
    string Text,
    string Status,
    string Reason);
```

并在 `BehaviorSpec` 上增加：

```csharp
IReadOnlyList<StaticAuraSpec> StaticAuras
```

第一批只支持 `Layer = STATIC_AURA`，不引入 RULE_TEXT keyword grant。B2 再处理授予/移除关键词。

### 两个 representative 的映射

奥恩：

```json
{
  "kind": "FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER",
  "layer": "STATIC_AURA",
  "duration": "WHILE_SOURCE_ON_PUBLIC_FIELD",
  "targetScope": "SOURCE_OBJECT",
  "participantScope": "FRIENDLY_PUBLIC_FIELD_EQUIPMENT",
  "powerDeltaPerParticipant": 1
}
```

崔法利兵营：

```json
{
  "kind": "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE",
  "layer": "STATIC_AURA",
  "duration": "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD",
  "targetScope": "SAME_BATTLEFIELD_UNITS",
  "participantScope": "SAME_BATTLEFIELD_PUBLIC_UNITS",
  "powerDeltaPerParticipant": 1
}
```

## 4. 引擎求值边界

第一批不做完整 LayerEngine 重写，只把现有两条 representative 从卡号/布尔字段迁移到 spec：

1. 新增一个只读 resolver，例如 `StaticAuraSpecResolver`：
   - 输入：`cardNo`
   - 输出：`IReadOnlyList<StaticAuraSpec>`
   - 数据来源：`BehaviorSpecCatalogBuilder` 生成的 spec，或同等只读 catalog API
2. `MatchSession.BuildContinuousEffectStates(...)`：
   - 遍历公开 field 对象。
   - 根据对象 `CardNo` 查 aura specs。
   - 按 `TargetScope` / `ParticipantScope` 求目标与参与对象。
   - 生成现有 `ContinuousEffectState` shape。
3. `CoreRuleEngine`：
   - `ApplyFriendlyEquipmentStaticPowerRecompute(...)` 改成查 `StaticAuraSpec.Kind == FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER`。
   - `ResolveBattlefieldAllUnitsPowerBonus(...)` 改成查战场对象的 `StaticAuraSpec.Kind == BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE`。
4. `MatchRecovery`：
   - 校验从“固定 effect id / 固定 sourceCardNo”调整为“按 authoritative effect payload 的 kind/scope/duration/source/target/spec shape 校验”。
   - 仍保留对现有两个 kind 的严格 metadata 约束，避免 spectator continuous-effect 信息边界放松。

## 5. TDD 任务清单

### Task 1：BehaviorSpec 暴露 StaticAuraSpec

修改：

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs`
- `src/Riftbound.DevUi/src/types/catalog.ts`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

先写测试：

- `BehaviorSpecCatalogParsesFriendlyEquipmentStaticAuraForOrnn`
- `BehaviorSpecCatalogParsesBattlefieldAllUnitsPowerAura`
- 断言 `SFD·085/221`、`SFD·085a/221`、`OGN·294/298` 的 `StaticAuras` 非空且字段精确。

预期先失败：`BehaviorSpec` 无 `StaticAuras`。

### Task 2：引擎 continuous effect 投影改读 spec

修改：

- `src/Riftbound.Engine/MatchSession.cs`
- 可选新增：`src/Riftbound.Engine/StaticAuraSpecRules.cs`
- `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

先写/改测试：

- 当前 `P79BattlefieldStaticPowerAddsOneToBattleParticipants` 仍通过，但断言 source path / reason 迁移到 spec-driven 命名。
- 新增测试：构造相同 card text 的 alternate battlefield card，如果 BehaviorSpec 提供同一 aura spec，不改引擎也能生成 aura。
- 新增测试：奥恩不再依赖 `CardBehaviorRegistry.AddsFriendlyFieldEquipmentCountToSourceUnitPower`。

预期先失败：引擎仍查卡号/registry。

### Task 3：战斗战力计算改读 spec

修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

先写测试：

- 同一战场两个单位在 spec-driven battlefield aura 下战斗伤害各 +1。
- 移除/替换 source 后不再得到 `staticPowerBonus`。

预期先失败：`ResolveBattlefieldAllUnitsPowerBonus(...)` 仍只认 `OGN·294/298`。

### Task 4：动态重算改读 spec

修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`

先写测试：

- 奥恩入场后，友方公开装备结算进入 field，奥恩 power 从 4 到 5。
- 友方公开装备离开 field，奥恩 power 从 5 回到 4。
- 敌方/暗牌/非装备不计入且不泄漏 participant metadata。

预期先失败：重算仍依赖 `AddsFriendlyFieldEquipmentCountToSourceUnitPower`。

### Task 5：删除或降级硬编码清单

修改：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`

目标：

- 删除 `ContinuousEffectStaticAuraCards`，或只保留在 test fixture helper 中。
- 删除 `BattlefieldAllUnitsPowerPlusOneCardNo` 对战斗 bonus 的直接判断。
- 删除 `AddsFriendlyFieldEquipmentCountToSourceUnitPower` 运行时依赖。
- `FriendlyEquipmentStaticPowerRepresentativeCardNos` 不再作为实现开关；如仍用于 coverage profile，必须标注为 representative coverage 而不是 runtime gate。

### Task 6：Recovery validator 泛化

修改：

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

先写测试：

- authoritative payload 的 static aura source card no 不再必须等于 `OGN·294/298`，但必须等于 source object 的公开 card no。
- effect id 允许由 `StaticAuraSpec.Kind` 派生，而不是两种固定字符串。
- spectator view 仍不能包含隐藏 participant/dependency object ids。

预期先失败：validator 仍含固定 `OGN·294/298` 和固定 effect id 分支。

### Task 7：证据与门禁

修改：

- `docs/rules-evidence-index.md`
- 新增 B1 audit/evidence 文档，命名建议：
  - `docs/CURRENT_PLAN_B_B1_STATIC_AURA_SPEC_AUDIT.md`
  - `docs/CURRENT_PLAN_B_B1_STATIC_AURA_SPEC_EVIDENCE.md`

验证命令：

```bash
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~OrnnFriendlyEquipmentStaticPower|FullyQualifiedName~BattlefieldStaticPower|FullyQualifiedName~LayerEngineBattlefieldStaticAura"
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
git diff --check
```

## 6. 第一批建议范围

建议第一批只做 Task 1 + Task 2 的投影层，不碰 combat damage 和 recovery validator 的语义收紧：

- 先让 `BehaviorSpec.StaticAuras` 出来。
- 让 `MatchSession.BuildContinuousEffectStates(...)` 对现有两个 representative 改读 spec。
- 保持 `ContinuousEffectState` payload 字段值兼容现有测试。

这样可以先证明“新增光环类卡只改 BehaviorSpec 数据即可出现在 continuousEffects 投影”，风险最小。

第二批再改 `CoreRuleEngine` 的真实战力计算和动态重算。第三批改 recovery validator 的硬编码 canonicality。

## 7. 需要确认的问题

1. `StaticAuraSpec` 是否允许第一批只支持两种 `Kind`，后续再扩条件子集与 RULE_TEXT keyword grant？
2. 第一批是否接受保留 `ContinuousEffectState.EffectKind` 的旧字符串，以避免 DevUi / recovery 大面积改动？
3. 奥恩当前是通过 registry 代表路径实现。B1 是否要求同批删除 `AddsFriendlyFieldEquipmentCountToSourceUnitPower`，还是允许先让它变成由 spec 派生的兼容字段，下一批删除？
