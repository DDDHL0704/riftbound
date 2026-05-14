# Stage 4D-03Y Battlefield Deferred Catalog Baseline Evidence

日期：2026-05-14
结论：**BASELINE RECORDED / PROJECT NOT READY**

本文记录 4D-03Y 实现前 baseline。该 baseline 只证明当前 HEAD 在交接前回归通过，并固定 4D-03Y 的待清理边界；不代表 `P6BattlefieldEffectCatalog` 旧 deferred 语义已退役。

## 1. Baseline Scope

目标切片：退役 `P6BattlefieldEffectCatalog` 中四个已经被 `BATTLEFIELD_RULE_DOMAIN` 领域实现的旧 deferred battlefield representatives。

当前事实：

- `P6BattlefieldEffectCatalog.GetDeferredSurfaces()` 仍返回 Poro Forge、Dream Tree、Blood Altar、Blackflame Altar 四个旧 surface。
- `BehaviorSpecCatalog` 已把 battlefield specs `57/57` 记为 implemented，manual battlefield specs 为 `0`。
- 四个旧 surface 的 positive battlefield representative tests 已存在并通过。
- `SFD·208/221` battlefield-granted legend attach ability 的手写 `ACTIVATE_ABILITY` 仍应拒绝；正确执行路径由 battlefield rule domain / `LEGEND_ACT` representative 提供。

## 2. Validation Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6BattlefieldEffectCatalog|FullyQualifiedName~P6BattlefieldRuleDomain|FullyQualifiedName~P79BattlefieldForge|FullyQualifiedName~P79BattlefieldFriendlySpellTarget|FullyQualifiedName~P79BattlefieldEphemeralDefender|FullyQualifiedName~P79BattlefieldBattleDestroyed"
```

结果：passed 15/15。

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentEngine|FullyQualifiedName~RunePool|FullyQualifiedName~DeclareBattle"
```

结果：passed 641/641。

Whitespace check:

```sh
git diff --check
```

结果：无输出。

## 3. Baseline Notes

- 本 baseline 发现的是状态语义不一致：runtime / BehaviorSpec 已 implemented，但 P6 catalog 名称和测试仍以 deferred 记录四个旧 surface。
- 本切片不需要前端运行时代码。
- 本切片不修改 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未修改未跟踪文件 `riftbound-dotnet.sln`。

## 4. Verdict

4D-03Y handoff baseline ready. 项目仍 **NOT READY**。
