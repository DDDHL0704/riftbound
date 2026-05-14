# Stage 4D-03AB Token Factory Brush Replacement Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03AB 的 B 侧服务端实现交接范围。A 主控只记录当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-002、P0-003、P0-004、P0-005、P1 或项目 **NOT READY** 结论。

## 1. 目标

4D-03AB 是一个 focused token battlefield replacement slice：实现官方 `UNL·T03` 草丛 token battlefield replacement representative，并从 `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 中退役最后一个 token surface。

目标 surface：

- `TOKEN_DEFERRED_BRUSH_BATTLEFIELD_REPLACEMENT`
- source card no: `UNL·T03`
- official anchor: `当你在此处得分时，你可以选择使用被此牌替代的战场来替代此牌`
- surface kind: `battlefield-replacement`

本切片只允许退役 Brush deferred surface，前提是服务端真正实现了“Brush 记住原战场，并在 Brush 处得分时可选择使用原战场替代 Brush”的当前对象模型 representative。若实现时发现当前战场得分模型无法表达“在此处得分”，应返回 blocker，不要用 catalog-only 退役冒充完成。

## 2. 当前代码事实

- `src/Riftbound.Engine/P6TokenFactoryCatalog.cs` 已定义 `UNL·T03` 草丛为 official battlefield token identity。
- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 当前只剩 `TOKEN_DEFERRED_BRUSH_BATTLEFIELD_REPLACEMENT`。
- `CoreRuleEngine.TryResolveIvernLegendBrushTrigger` 已在 Ivern 征服 / 据守触发时创建 `UNL·T03` battlefield token，并写入 tags：
  - `CARD_TYPE:BATTLEFIELD`
  - `草丛`
  - `REPLACES_BATTLEFIELD:<battlefieldId>`
- 现有 Ivern tests 只证明 Brush token 被创建、带 replacement memory tag、不是单位；并未证明 score-time replacement lifecycle。
- `TryGetBattlefieldCardObject` 已支持 concrete battlefield object id；Baron Nest 4D-03Z 已证明 battlefield token 可以作为 concrete battlefield destination / object 参与服务端规则。
- 多个 battlefield held / conquered trigger helpers 以 `battlefieldId` 解析 battlefield card object，再根据 `battlefieldState.CardNo` 判断触发；Brush replacement 若要影响得分或战场 card identity，必须在这些入口前有明确的 effective battlefield resolution / optional choice。

## 3. 建议实现口径

- 新增常量：
  - `BrushTokenCardNo = "UNL·T03"` 或复用现有 `BrushBattlefieldTokenCardNo`
  - `BrushReplacementSurfaceId = "TOKEN_DEFERRED_BRUSH_BATTLEFIELD_REPLACEMENT"`
  - optional choice / audit marker 例如 `BRUSH_USE_REPLACED_BATTLEFIELD`
- 解析 Brush replacement memory：
  - 只接受当前 battlefield object `CardNo == UNL·T03` 且 tags 中恰有一个 `REPLACES_BATTLEFIELD:<originalBattlefieldObjectId>`。
  - original battlefield object 必须存在、是 battlefield card object、有已知 `CardNo`，并且不应是同一个 Brush object。
  - replacement memory 缺失、重复、指向未知对象、指向非 battlefield、指向另一个 Brush 循环时必须 no-op 或 rejected no-mutation，不能退役 surface。
- 可选选择：
  - 官方文本是“可以选择”，不要自动替代。
  - 建议通过 server-authored prompt metadata / `DECLARE_BATTLE.optionalCosts` 或现有 choice 机制暴露 `BRUSH_USE_REPLACED_BATTLEFIELD:<originalBattlefieldObjectId>`。
  - 手写提交该 choice 时，必须确认当前 battle / score battlefield 正是该 Brush，并确认 original id 与 tag memory 一致。
- 代表性 runtime：
  - 当玩家在 Brush battlefield 处触发当前模型中已有的得分路径时，若提交 Brush replacement choice，则该次 score / battlefield-card effect resolution 使用 original battlefield `CardNo` / object id 作为 effective battlefield source，并写 audit event，例如 `BATTLEFIELD_REPLACEMENT_APPLIED`。
  - 至少覆盖一个已有可测试 score path，例如 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 或当前代码中最稳定的 battlefield score representative。
  - 事件 payload 应同时保留 `brushBattlefieldObjectId`、`replacementBattlefieldObjectId`、`replacementBattlefieldCardNo`、`replacementChoice`，避免前端本地推断。
  - 不应删除 Brush token 或 original battlefield object，除非当前 battlefield lifecycle 已有明确置换/清理语义；本 focused slice 优先实现 score-time effective battlefield representative。
- catalog path：
  - `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 变为空。
  - `GetImplementedRuleSurfaces()` 增加 Brush retired representative，继续保留 Image 和 Baron Nest retired representatives。

## 4. 建议写入范围

允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`（若需要给 `DECLARE_BATTLE` prompt 暴露 Brush replacement choice）
- `src/Riftbound.Engine/P6TokenFactoryCatalog.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`
- 需要时可新增 focused battlefield replacement test file，但优先复用现有 conformance tests。
- 本切片完成后的 audit / evidence docs 和顶层状态文档。

不建议写入：

- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 未跟踪文件 `riftbound-dotnet.sln`。
- 完整 battlefield lifecycle、LayerEngine、全局 replacement ordering、非 Brush battlefield card rewrites。

## 5. 必补测试

- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 为空；Brush 不再 deferred。
- `GetImplementedRuleSurfaces()` 包含 Brush / Image / Baron Nest 三个 token retired representatives，并锁住 Brush official anchor、`battlefield-replacement` kind、0 target、non-activated flag、battlefield token identity。
- Ivern 创建 Brush 时继续记录 `REPLACES_BATTLEFIELD:<original>`，且不把 Brush 当作单位。
- score-time replacement representative：
  - Brush battlefield object 有 valid replacement memory，玩家提交 Brush replacement choice，score / battlefield effect 使用 original battlefield identity。
  - 事件包含 Brush 与 replacement original 的 audit payload。
  - 不提交 choice 时不自动替代。
- rejected / no-mutation：
  - 手写 Brush replacement choice 指向未知 original。
  - Brush 缺 replacement tag。
  - replacement tag 指向非 battlefield object。
  - replacement tag 指向自身或循环 Brush。
  - 非 Brush battlefield 提交 Brush replacement choice。
- 既有 Ivern、battlefield held / declare battle、board task queue、P6 token catalog regression 继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6TokenFactoryCatalog|FullyQualifiedName~GoldTokenDeferredResourceSurfaces|FullyQualifiedName~P79LegendTriggerIvern|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~DeclareBattle|FullyQualifiedName~BoardTaskQueue"
```

若改动 MatchSession prompt，追加：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Battlefield"
```

若退役最后一个 token deferred surface 或改动 score / declare battle shared path，A 验收时追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. No-Go Criteria

- 不要 catalog-only 退役 Brush；必须有 score-time replacement runtime representative。
- 不要把 Brush replacement 处理成自动替代；官方文本是可选。
- 不要丢失 original battlefield memory 或要求前端本地推断 replacement target。
- 不要把 Brush 当作单位、普通 battlefield static、ROAM provider 或 activated ability。
- 不要修改 coverage matrix full-official 状态。
- 不要声明 battlefield replacement / LayerEngine / token factory full official。
- 不要关闭 P0-002、P0-003、P0-004、P0-005、P1、READY 或 active goal。

## 8. A 侧结论

4D-03AB 用来处理最后一个 token deferred surface：`UNL·T03` 草丛 battlefield replacement。它必须实现可验证的 score-time effective battlefield representative 才能退役；项目仍 **NOT READY**。
