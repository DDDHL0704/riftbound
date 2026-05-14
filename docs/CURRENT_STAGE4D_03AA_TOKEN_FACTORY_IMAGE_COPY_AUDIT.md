# Stage 4D-03AA Token Factory Image Copy Audit

日期：2026-05-14
结论：**IMPLEMENTED REPRESENTATIVE / PROJECT NOT READY**

4D-03AA 完成 `UNL·T06` 映像 copy-token focused representative，并从 `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 退役 `TOKEN_DEFERRED_IMAGE_COPY_SOURCE_REQUIRED`。Brush battlefield replacement 仍保持 deferred。

## Scope

已退役 surface：

- `TOKEN_DEFERRED_IMAGE_COPY_SOURCE_REQUIRED`
- source card no: `UNL·T06`
- official anchor: `当我被打出时，变为某张卡牌的复制体`
- surface kind: `copy-token`

`P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 现在只剩 `TOKEN_DEFERRED_BRUSH_BATTLEFIELD_REPLACEMENT`。`GetImplementedRuleSurfaces()` 现在保留 Image copy-token 与 Baron Nest movement static 两个 retired / implemented representatives。

## Runtime Behavior

- `UNL-200/219` 镜花水月创建的 Image token 复制目标单位的 `CardNo`、`Power`、tags，并追加 `瞬息` / `映像`。
- `UNIT_TOKEN_CREATED` payload 写入 `copiedTargetObjectId`、`copiedCardNo`、`tokenFactoryCardNo=UNL·T06`，并保留 copied card identity。
- Image copy target 必须是场上正面单位且有服务端已知 `CardNo`；缺 `CardNo`、非单位、face-down、非场上对象或 dirty control target 均 rejected no-mutation。
- LeBlanc Image trigger 保持现有 copy `CardNo` / `Power` / tags 行为，并补 `tokenFactoryCardNo=UNL·T06`。
- Image creation 不触发被复制牌的 on-play effects。

## No-Go

- 未实现完整 LayerEngine / timestamp / dependency copy model。
- 未退役 Brush battlefield replacement。
- 未修改前端运行时代码。
- 未修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未关闭 P0-002、P0-003、P0-004、P0-005、P1、READY 或任何 full-official 项目状态。

## Residual Risk

本切片只覆盖当前对象模型可表达的 Image copy representative；隐藏面、非单位复杂复制、持续效果依赖和完整 layer 语义仍 deferred。项目仍 **NOT READY**。
