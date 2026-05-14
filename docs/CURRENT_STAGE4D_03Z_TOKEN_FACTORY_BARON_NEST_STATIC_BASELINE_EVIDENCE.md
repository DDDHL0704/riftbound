# Stage 4D-03Z Token Factory Baron Nest Static Baseline Evidence

日期：2026-05-14
结论：**BASELINE RECORDED / PROJECT NOT READY**

本文记录 4D-03Z 实现前 baseline。该 baseline 只证明当前 HEAD 在交接前回归通过，并固定 4D-03Z 的待实现边界；不代表 Baron Nest movement static 已完成。

## 1. Baseline Scope

目标切片：实现 `UNL·T01` 男爵巢穴 token battlefield static `单位可从任意位置移动到此处` 的服务端代表路径，并从 token deferred catalog 中只移除 `TOKEN_DEFERRED_BARON_NEST_MOVE_STATIC`。

当前事实：

- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 仍返回 3 项：Image copy、Brush battlefield replacement、Baron Nest movement static。
- `UNL·T01` 当前只作为 token battlefield identity 存在，属于 `TOKEN_FACTORY_DOMAIN`。
- 已有 base-to-concrete battlefield movement 与 precise `ROAM` battlefield-to-battlefield movement infrastructure。
- 现有 `OGN·297/298` battlefield static roam path 与 `UNL·T01` 不同：前者通过 `ROAM` optional cost 授予战场间游走，后者应是 destination-specific no-ROAM movement to Baron Nest。
- Image copy-token 和 Brush battlefield replacement 仍是真 deferred，不属于 4D-03Z 退役范围。

## 2. Validation Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6TokenFactoryCatalog|FullyQualifiedName~GoldTokenDeferredResourceSurfaces|FullyQualifiedName~P79BattlefieldStaticRoam|FullyQualifiedName~P4MoveUnitCommand|FullyQualifiedName~P5MoveUnitCommand|FullyQualifiedName~PreciseRoam|FullyQualifiedName~BoardTaskQueue"
```

结果：passed 66/66。

Whitespace check:

```sh
git diff --check
```

结果：无输出。

## 3. Baseline Notes

- 本 baseline 证明现有 token catalog / movement / prompt 相邻路径绿色，但 Baron Nest no-ROAM battlefield-to-battlefield static 尚未实现。
- 本切片不需要前端运行时代码。
- 本切片不修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未修改未跟踪文件 `riftbound-dotnet.sln`。

## 4. Verdict

4D-03Z handoff baseline ready. 项目仍 **NOT READY**。
