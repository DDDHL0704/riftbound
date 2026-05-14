# Stage 4D-03X Legend Action Deferred Catalog Baseline Evidence

日期：2026-05-14
结论：**BASELINE RECORDED / PROJECT NOT READY**

本文记录 4D-03X 实现前 baseline。该 baseline 只证明当前 HEAD 在交接前回归通过，并固定 4D-03X 的待清理边界；不代表 `P6LegendAbilityCatalog` 旧 deferred 语义已退役。

## 1. Baseline Scope

目标切片：退役 `P6LegendAbilityCatalog` 中五个已经被 `LEGEND_ACT` 领域实现的旧 deferred legend ability representatives。

当前事实：

- `P6LegendAbilityCatalog.GetDeferredSurfaces()` 仍返回 Yasuo、Lee Sin、Diana、Poppy、Viktor 五个旧 surface。
- `BehaviorSpecCatalog` 已把 legend specs `106/106` 记为 implemented，manual legend specs 为 `0`。
- 五个旧 surface 的 positive `LEGEND_ACT` representative tests 已存在并通过。
- 手写 `ACTIVATE_ABILITY` 提交这些 ability id 仍应拒绝；正确命令路径是 `LEGEND_ACT`。

## 2. Validation Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6LegendAbilityCatalog|FullyQualifiedName~P6LegendRuleDomainSurfacesReportManualBoundaryCoverage|FullyQualifiedName~P79LegendAct|FullyQualifiedName~LegendAction"
```

结果：passed 54/54。

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LegendAct|FullyQualifiedName~LegendAction|FullyQualifiedName~Diana|FullyQualifiedName~Yasuo|FullyQualifiedName~LeeSin|FullyQualifiedName~Poppy|FullyQualifiedName~Viktor|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentEngine|FullyQualifiedName~RunePool"
```

结果：passed 279/279。

## 3. Baseline Notes

- 本 baseline 发现的是状态语义不一致：runtime / BehaviorSpec 已 implemented，但 P6 catalog 名称和测试仍以 deferred 记录五个旧 surface。
- 本切片不需要前端运行时代码。
- 本切片不修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未修改未跟踪文件 `riftbound-dotnet.sln`。

## 4. Verdict

4D-03X handoff baseline ready. 项目仍 **NOT READY**。
