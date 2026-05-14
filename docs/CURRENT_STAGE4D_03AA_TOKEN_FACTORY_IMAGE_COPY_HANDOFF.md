# Stage 4D-03AA Token Factory Image Copy Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03AA 的 B 侧服务端实现交接范围。A 主控只记录当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-002、P0-003、P0-004、P0-005、P1 或项目 **NOT READY** 结论。

## 1. 目标

4D-03AA 是一个 focused token copy slice：实现官方 `UNL·T06` 映像 token copy representative，并从 `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 中只退役该一个 surface。

目标 surface：

- `TOKEN_DEFERRED_IMAGE_COPY_SOURCE_REQUIRED`
- source card no: `UNL·T06`
- official anchor: `当我被打出时，变为某张卡牌的复制体`
- surface kind: `copy-token`

本切片只覆盖当前对象模型已能表达的 copy representative：Image 被打出后复制目标单位的 `CardNo`、`Power`、对象 tags，并按来源效果额外获得 `瞬息` / `映像` 等 token 标记；创建 Image 不应触发被复制牌的打出效果。完整 layer / timestamp copy model、复制隐藏面、复制非单位复杂规则文本、持续效果依赖和完整 P1 LayerEngine 仍保持 deferred。

## 2. 当前代码事实

- `src/Riftbound.Engine/P6TokenFactoryCatalog.cs` 已定义 `UNL·T06` 映像为 official unit token identity，`RequiresCopySource=true`，tags 含 `COPY_SOURCE_REQUIRED`。
- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 当前有 2 项：Image copy-token 与 Brush battlefield replacement。
- `CardBehaviorRegistry` 已有 `UNL-200/219` 镜花水月 `MIRROR_IMAGE_CREATE_COPY_EPHEMERAL_BASE`，当前通过 `CreatedBaseUnitTokenCopiesFirstTarget` 复制目标 `Power` / tags，但创建出的 token 没有 `CardNo` / `copiedCardNo` 审计。
- `CoreRuleEngine.TryResolveLeblancLegendImageTrigger` 已创建 battlefield Image，复制目标 `CardNo` / `Power` / tags，并在事件 payload 中写入 `copiedCardNo`；这是本切片的可复用语义参照。
- 既有相邻代表测试：
  - `CoreRuleEnginePlaysMirrorImageCreatesEphemeralCopyInBase`
  - `CoreRuleEngineRejectsMirrorImageAgainstHandCard`
  - `P79LegendTriggerLeblancCreatesActiveImageOnConquer`
  - `P79LegendTriggerLeblancCreatesActiveImageOnHold`
  - `P6TokenFactoryCatalogAuditsDeferredRuleSurfacesAgainstOfficialText`

## 3. 建议实现口径

- 在 token creation helper 中明确识别 Image copy-token path，而不是让所有 copied base unit tokens 都获得 Image 专属语义。
- 建议新增常量：
  - `ImageTokenCardNo = "UNL·T06"`
  - `ImageCopySurfaceId = "TOKEN_DEFERRED_IMAGE_COPY_SOURCE_REQUIRED"`
- `UNL-200/219` 镜花水月成功结算后：
  - 创建的 Image token 位于控制者基地、活跃、controller / owner 为施放者。
  - `CardObjectState.CardNo` 应为被复制目标的 `CardNo`，若目标缺少 cardNo 则应拒绝或保持 no-mutation，不能创建半复制对象。
  - `Power` 与对象 tags 从复制目标读取，并追加 `瞬息`；若本地模型需要保留 token family，可追加 `映像`，但不要移除被复制目标已有 unit/tag 信息。
  - `UNIT_TOKEN_CREATED` payload 应至少含 `copiedTargetObjectId`、`copiedCardNo`，并保留可审计的 Image token identity marker，例如 `tokenFactoryCardNo = "UNL·T06"`；若保留旧 `tokenCardNo` 字段，需避免把 token identity 与 copied card identity 混淆。
  - 不触发被复制牌的 `PLAY_CARD` / on-play 行为，不创建额外 stack item、token、draw、score 或 trigger side effect。
- LeBlanc Image path：
  - 保持现有 copy cardNo / power / tags / event 行为。
  - 补齐与 Mirror Image 一致的 token factory marker（如采用），并覆盖不触发 copied card on-play effect。
- catalog path：
  - `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 从 2 降为 1，只保留 Brush battlefield replacement。
  - `GetImplementedRuleSurfaces()` 增加 Image retired representative，继续保留 Baron Nest retired representative。
  - 不要退役 Brush battlefield replacement。

## 4. 建议写入范围

允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`（仅当需要补 Image token card no metadata）
- `src/Riftbound.Engine/P6TokenFactoryCatalog.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-mirror-image-copy-ephemeral-base.fixture.json`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`
- 本切片完成后的 audit / evidence docs 和顶层状态文档。

不建议写入：

- 前端运行时代码。
- `src/Riftbound.Engine/MatchSession.cs`，除非实现确实需要给 prompt metadata 增加 Image copy 审计字段；镜花水月已有目标提示路径。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 未跟踪文件 `riftbound-dotnet.sln`。
- Brush replacement、LayerEngine、通用连续效果 copy model 或非 Image token 逻辑。

## 5. 必补测试

- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 只剩 Brush；Image 不再 deferred。
- `GetImplementedRuleSurfaces()` 包含 Image 与 Baron Nest retired representatives，并锁住 Image official anchor、`copy-token` kind、1 target、non-activated flag、`RequiresCopySource=true`。
- 镜花水月成功路径：
  - Image token 进入控制者基地且活跃。
  - token `CardNo` 等于 copied target `CardNo`。
  - token `Power` / tags 复制目标并追加 `瞬息`。
  - event payload 包含 copied target / copied card audit metadata 与 Image token identity marker。
- LeBlanc 成功路径继续复制 cardNo / power / tags，并在 event payload 中保持 copied target / copied card audit metadata。
- 对缺少 `CardNo`、非单位、手牌 / 非场上对象、脏控制权 copy source 的命令 rejected no-mutation。
- 复制带 on-play 行为的单位时，不触发被复制牌的打出效果，不额外创建 stack item / token / draw / score。
- 既有 Mirror Image、LeBlanc、P6 token catalog 与 Gold token deferred-resource regression 继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6TokenFactoryCatalog|FullyQualifiedName~GoldTokenDeferredResourceSurfaces|FullyQualifiedName~MirrorImage|FullyQualifiedName~P79LegendTriggerLeblanc"
```

若改动触碰通用 token creation helper，追加：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~UNIT_TOKEN_CREATED|FullyQualifiedName~Ephemeral|FullyQualifiedName~Token|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

若改动影响 catalog global counts、fixture expected payload 或通用 stack resolution，A 验收时追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. No-Go Criteria

- 不要把 Image token cardNo 固定为 `UNL·T06` 后丢失 copied card identity；官方语义是变为复制体。
- 不要让 Image creation 触发被复制牌的打出效果。
- 不要退役 Brush battlefield replacement deferred surface。
- 不要借本切片实现完整 layer / timestamp / dependency copy model。
- 不要修改 coverage matrix full-official 状态。
- 不要声明 token factory / copy / LayerEngine full official。
- 不要关闭 P0-002、P0-003、P0-004、P0-005、P1、READY 或 active goal。

## 8. A 侧结论

4D-03AA 用来把 `UNL·T06` 映像 copy-token 从 P6 deferred list 推进为当前对象模型可审计的服务端代表路径。它是 focused token-copy slice，不是 READY 切片；项目仍 **NOT READY**。
