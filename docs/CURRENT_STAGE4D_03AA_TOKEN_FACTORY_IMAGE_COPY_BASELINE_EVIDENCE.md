# Stage 4D-03AA Token Factory Image Copy Baseline Evidence

日期：2026-05-14
结论：**BASELINE RECORDED / PROJECT NOT READY**

本文记录 4D-03AA 实现前 baseline。该 baseline 只证明当前 HEAD 在交接前回归通过，并固定 4D-03AA 的待实现边界；不代表 Image copy-token 已完成。

## 1. Baseline Scope

目标切片：实现 `UNL·T06` 映像 token copy representative，并从 token deferred catalog 中只移除 `TOKEN_DEFERRED_IMAGE_COPY_SOURCE_REQUIRED`。

当前事实：

- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 仍返回 2 项：Image copy-token 与 Brush battlefield replacement。
- `UNL·T06` 当前已作为 token identity 存在，`RequiresCopySource=true`，但 Image copy-token surface 仍 deferred。
- `UNL-200/219` 镜花水月当前只复制目标 `Power` / tags 到基地 Image，尚未写入 copied target `CardNo` 或完整 copied-card audit metadata。
- LeBlanc battlefield-result Image trigger 已复制目标 `CardNo` / `Power` / tags，并写入 `copiedCardNo`；这是 4D-03AA 的 runtime 参照。
- Brush battlefield replacement 仍是真 deferred，不属于 4D-03AA 退役范围。

## 2. Validation Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6TokenFactoryCatalog|FullyQualifiedName~GoldTokenDeferredResourceSurfaces|FullyQualifiedName~MirrorImage|FullyQualifiedName~P79LegendTriggerLeblanc"
```

结果：passed 11/11。

Whitespace check:

```sh
git diff --check
```

结果：无输出。

## 3. Baseline Notes

- 本 baseline 证明现有 token catalog / Mirror Image / LeBlanc Image 相邻路径绿色，但 Image copy-token 仍未从 deferred catalog 退役。
- 本切片预计不需要前端运行时代码。
- 本切片不修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未修改未跟踪文件 `riftbound-dotnet.sln`。

## 4. Verdict

4D-03AA handoff baseline ready. 项目仍 **NOT READY**。
