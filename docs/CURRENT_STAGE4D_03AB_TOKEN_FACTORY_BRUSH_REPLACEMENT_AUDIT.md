# Stage 4D-03AB Token Factory Brush Replacement Audit

日期：2026-05-14
结论：**IMPLEMENTED REPRESENTATIVE / PROJECT NOT READY**

4D-03AB 完成 `UNL·T03` 草丛 battlefield replacement focused representative，并从 `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 退役最后一个 token deferred surface。

## Scope

已退役 surface：

- `TOKEN_DEFERRED_BRUSH_BATTLEFIELD_REPLACEMENT`
- source card no: `UNL·T03`
- official anchor: `当你在此处得分时，你可以选择使用被此牌替代的战场来替代此牌`
- surface kind: `battlefield-replacement`

`P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 现在为空。`GetImplementedRuleSurfaces()` 现在保留 Brush replacement、Image copy-token 与 Baron Nest movement static 三个 retired / implemented representatives。

## Runtime Behavior

- Ivern 创建 Brush 时继续使用 `UNL·T03` token identity，并通过 `REPLACES_BATTLEFIELD:<originalBattlefieldObjectId>` 记录原战场 memory。
- 服务端 prompt / `DECLARE_BATTLE` optional choice 暴露 `BRUSH_USE_REPLACED_BATTLEFIELD:<original>`，只在 Brush replacement memory 指向当前模型可解析的 original score battlefield 时出现。
- 在 Brush 据守得分 representative 中，提交该 choice 后，服务端使用 original battlefield identity 解析 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`。
- 成功路径写入 `BATTLEFIELD_REPLACEMENT_APPLIED`，payload 包含 Brush object、replacement original object/cardNo、choice id、effective battlefield 与 surface id。
- 不提交 choice 时不自动替代。

## Guards

已覆盖 rejected no-mutation：

- unknown original battlefield；
- Brush 缺 replacement tag；
- replacement tag 指向非 battlefield object；
- replacement tag 指向 Brush 自身；
- 非 Brush battlefield 提交 Brush replacement choice。

## No-Go

- 未把 Brush 处理为自动替代。
- 未把 Brush 当作单位、ROAM provider、普通 battlefield static 或 activated ability。
- 未修改前端运行时代码。
- 未修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未关闭 P0-002、P0-003、P0-004、P0-005、P1、READY 或任何 full-official 项目状态。

## Residual Risk

本切片只覆盖当前稳定的 Brush score-time representative，即 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`；完整 replacement ordering、所有未来 score 入口、LayerEngine / dependency model 仍未关闭。项目仍 **NOT READY**。
