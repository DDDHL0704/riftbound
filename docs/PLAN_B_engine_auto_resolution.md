# 方案 B：引擎自动结算攻坚计划

> 状态：执行中（B1 已有多批 BehaviorSpec 静态光环切片落地）
> 目标：推进「自动处理结算效果」的真实引擎缺口，复用现有基础设施，不重造、不打单卡补丁。
> 红线依据：`AGENTS.md`（通用机制改共享引擎）、官方 PDF/FAQ + `docs/rules-evidence-index.md`。

2026-07-01 执行校正：`BehaviorSpec.StaticAuras`、`StaticAuraSpecRules` 与多条静态光环执行/投影路径已经落地。最新小步把 battlefield all-units / filtered-units 的 POWER 与 RULE_TEXT keyword 光环、same-battlefield other-friendly normal / filtered POWER 光环、public-field friendly / other-friendly / friendly-filtered POWER 光环、source-object / source-object-filtered POWER 光环、以及 source battle-state POWER 光环路由合并为 `StaticAuraSpecRules.GetStaticAuras(...)` + shared scope/participant-scope predicates；验证见 `docs/CURRENT_PLAN_B_B1_STATIC_AURA_SPEC_AUDIT.md` 与 `docs/CURRENT_PLAN_B_B1_STATIC_AURA_SPEC_EVIDENCE.md`。本计划中的“硬编码待通用化”条目保留为历史问题说明；当前后续工作应从剩余未合并的 aura families / full keyword breadth / LayerEngine ordering breadth 继续推进。

## 1. 现状校正（重要）

引擎不是空架子，已实现的核心：

- `CoreRuleEngine`（`src/Riftbound.Engine/CoreRuleEngine.cs`，43K 行）、`MatchSession.cs`（24K 行）
- 结算栈 + 触发队列：`NormalizeStackItems`（`MatchSession.cs:2230`）、`NormalizeTriggerQueue`（2250）、快照投影（3295 / 3394）
- 4 个核心 prompt 全实现：`PAY_COST`（13677）、`ORDER_TRIGGERS`（13796）、`ASSIGN_COMBAT_DAMAGE`（13717）、`CHOOSE_HAND_CARDS`（13633）；契约 `Protocol.cs:307-380`
- 基础 PaymentEngine：`PaymentCostRules.cs`（法力/能力值/符文/体验）
- **连续效果层引擎已存在**：
  - `ContinuousEffectLayers`（`MatchSession.cs:324`）：`POWER_MODIFIER` / `RULE_TEXT` / `STATIC_AURA`
  - `PowerModifierLedgerEntry`（377）、`ApplyPowerModifier` 来源路径（405）
  - `UntilEndOfTurnPowerModifier` 账本（463-521）
  - 投影 `ContinuousEffects`（1007）、`BuildContinuousEffectStates`（1760-1849）

> 校正：审计里 `NEEDS_ENGINE_SUPPORT = 4270` 是 skeleton 矩阵逐行记账，**虚高**。真实口径：**284/811 功能单元（35%）未支持**。

## 2. 真实缺口与核心抓手

最高杠杆的发现：**静态光环目前是按卡号硬编码的**，违反「通用机制不打单卡补丁」。

- `ContinuousEffectStaticAuraCards`（`MatchSession.cs:331`）= 内部写死的卡号清单
- `BuildBattlefieldAllUnitsStaticAuraEffects`（1938）写死 `BattlefieldAllUnitsPowerPlusOneCardNo`（1949）
- `TryBuildFriendlyEquipmentStaticAuraEffect`（1884）也是特定形态硬编码

→ **核心抓手：把静态光环 / 连续效果从「卡号硬编码」改造成「BehaviorSpec 数据驱动的通用层」。** 一次改造，把 N 张卡的单卡补丁折叠成一条数据驱动层。

### 缺口家族（按影响面）

| 优先级 | 家族 | 受影响功能单元 | 现状 | 该做 |
|---|---|---|---|---|
| **P0** | 连续效果广度 | 266 | 层引擎在，覆盖窄 | 更多单位卡 buff/debuff 接 `ApplyPowerModifier` 账本 |
| **P0** | STATIC_AURA 通用化 | （上者子集） | 卡号硬编码 | 从 BehaviorSpec 读光环定义，动态重算（在场加成/离场撤销） |
| **P0** | 授予/移除关键词（RULE_TEXT 层） | 多 | 框架在 | 通用「给予/移除关键词」层 |
| **P1** | targeting-stack-timing | 444 | 栈+触发在 | 补目标合法性 + 时序窗口覆盖广度 |
| **P1** | battle / spell-duel 生命周期 | 287 | prompt 在 | 补战斗流程编排 |
| **P1** | cleanup-replacement-duration | 282 | 部分 | 清理队列 / 替换效果链 |
| **P2** | control-zone-movement | 286 | 部分 | 控制权切换 / 单位移动 |
| **P2** | payment 完整矩阵 | 360 | 基础 | 替代 / 额外 / 可选成本 |

## 3. 攻坚主轴（单条主线，逐家族 + conformance）

### 阶段 B1：STATIC_AURA 通用化（最高杠杆，先做）

目标：用 BehaviorSpec 描述光环，引擎统一应用，删除卡号硬编码。

1. **定位数据源**：确认 BehaviorSpec 里是否已有光环/连续效果字段（`src/Riftbound.CardCatalog` 解析、`docs/conformance-fixture-format.md`）。缺字段则先补协议/Spec。
2. **抽象光环模型**：作用域（友方全体 / 战场全体 / 装备宿主 / 条件子集）、层（POWER_MODIFIER / RULE_TEXT）、时长、来源。
3. **改造 `BuildContinuousEffectStates`**（`MatchSession.cs:1760-1849`）：
   - 把 `BuildBattlefieldAllUnitsStaticAuraEffects`（1938）、`TryBuildFriendlyEquipmentStaticAuraEffect`（1884）从「卡号判断」改为「读 Spec 光环定义」。
   - 删除 `ContinuousEffectStaticAuraCards`（331）硬编码清单。
4. **动态重算**：光环源进出战场 / 控制权变化时，依赖单位的 `EffectivePower` 即时重算（参与者集合 `BattlefieldStaticAuraParticipantObjectIds` 1992 改为通用作用域求值）。
5. **conformance**：为「光环加成 / 源离场撤销 / 多光环叠加 / 与 until-end-of-turn 叠加」加 fixture，对官方 PDF/FAQ 裁决。

### 阶段 B2：授予/移除关键词（RULE_TEXT 层通用化）
- 同模式：BehaviorSpec 描述「授予关键词」，引擎在 RULE_TEXT 层统一加/撤。
- 覆盖典型关键词（依 `docs/rules-evidence-index.md` 选高频）。

### 阶段 B3：targeting-stack-timing 广度
- 不新建框架，补已有栈/触发的目标合法性与时序窗口覆盖。
- 按功能单元批量推进，每批配 conformance。

> B2/B3 在 B1 落地、回归绿后再开。

## 4. 每批次的工作流（硬性）

1. 查官方依据：对应 PDF/FAQ 条目 + `docs/rules-authority-and-audit.md` + `docs/rules-evidence-index.md`，补/更新 evidence。
2. 通用机制改共享引擎，**禁止单卡补丁**。
3. 先写聚焦 conformance 测试（`tests/Riftbound.ConformanceTests`），再实现。
4. 跑聚焦回归 → 相邻回归 → 必要时全量后端测试。
5. 保持隐藏信息边界：快照不泄漏对手手牌/牌库序/暗牌身份（`MatchRecovery.cs` 的 spectator continuous-effect 校验 13722+ 必须仍绿）。
6. 小步提交推 main。

## 5. 关键文件索引

| 主题 | 文件 : 行号 |
|---|---|
| 层常量 | `MatchSession.cs:324-331` |
| 力量修正账本 | `MatchSession.cs:377-543` |
| 连续效果投影 | `MatchSession.cs:1007 / 1760-1849` |
| 战场光环（硬编码，待通用化） | `MatchSession.cs:1938-1992` |
| 装备光环（硬编码，待通用化） | `MatchSession.cs:1884-1937` |
| 栈 / 触发队列 | `MatchSession.cs:2230 / 2250 / 3295 / 3394` |
| 4 prompt 元数据 | `MatchSession.cs:13633 / 13677 / 13717 / 13796` |
| 支付规则 | `PaymentCostRules.cs:1-453` |
| prompt 契约 | `Protocol.cs:307-380` |
| 快照连续效果校验 | `MatchRecovery.cs:2312 / 3917-4114 / 13722+` |
| BehaviorSpec / 卡数据 | `src/Riftbound.CardCatalog`、`docs/conformance-fixture-format.md` |
| 覆盖矩阵（缺口口径） | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` |

## 6. 验收标准

- 静态光环不再依赖任何卡号硬编码清单（`ContinuousEffectStaticAuraCards` 删除或仅作 fixture）。
- 新增光环类卡只改 BehaviorSpec 数据即可生效，引擎零改动。
- conformance 覆盖：光环加成、源离场撤销、叠加、与限时修正叠加，全部对官方裁决通过。
- 后端全量测试绿（含现有 5226 基线不回归）。
- 隐藏信息边界 spectator 校验不回归。

## 7. 风险

- BehaviorSpec 可能缺光环字段 → 需先补协议/Catalog，blast radius 比预期大；先做小范围 spike 确认。
- 动态重算时机（进出场 / 控制权变化 / 清理步）需对齐官方时序，避免重复或漏算。
- 删硬编码会牵动现有依赖这些卡的回归 fixture，需同步迁移。
