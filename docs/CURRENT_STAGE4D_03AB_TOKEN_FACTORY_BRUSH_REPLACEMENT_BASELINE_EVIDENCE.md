# Stage 4D-03AB Token Factory Brush Replacement Baseline Evidence

日期：2026-05-14
结论：**BASELINE RECORDED / PROJECT NOT READY**

本文记录 4D-03AB 实现前 baseline。该 baseline 只证明当前 HEAD 在交接前回归通过，并固定 4D-03AB 的待实现边界；不代表 Brush battlefield replacement 已完成。

## 1. Baseline Scope

目标切片：实现 `UNL·T03` 草丛 token battlefield replacement representative，并从 token deferred catalog 中移除最后一个 deferred surface `TOKEN_DEFERRED_BRUSH_BATTLEFIELD_REPLACEMENT`。

当前事实：

- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 仍返回 1 项：Brush battlefield replacement。
- `UNL·T03` 当前已作为 token battlefield identity 存在。
- Ivern battlefield-result trigger 已能创建 Brush token，并记录 `REPLACES_BATTLEFIELD:<battlefieldId>` memory tag。
- 当前代码尚未实现 score-time optional replacement：在 Brush 处得分时选择使用被替代战场替代 Brush。
- Image copy-token 与 Baron Nest movement static 已由 4D-03AA / 4D-03Z 退役为 implemented representatives。

## 2. Validation Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6TokenFactoryCatalog|FullyQualifiedName~GoldTokenDeferredResourceSurfaces|FullyQualifiedName~P79LegendTriggerIvern|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~DeclareBattle|FullyQualifiedName~BoardTaskQueue"
```

结果：passed 141/141。

Whitespace check:

```sh
git diff --check
```

结果：无输出。

## 3. Baseline Notes

- 本 baseline 证明现有 token catalog / Ivern Brush creation / battlefield held / declare battle / board task queue 相邻路径绿色，但 Brush score-time replacement 尚未实现。
- 本切片预计不需要前端运行时代码；若需要 UI 暴露，也应通过服务端 `ActionPrompt` metadata。
- 本切片不修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未修改未跟踪文件 `riftbound-dotnet.sln`。

## 4. Verdict

4D-03AB handoff baseline ready. 项目仍 **NOT READY**。
