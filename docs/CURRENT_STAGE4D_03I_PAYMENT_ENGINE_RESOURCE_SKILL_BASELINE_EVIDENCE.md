# Stage 4D-03I PaymentEngine Resource Skill Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03I 实现前的 A 侧基线。当前仓库尚未实现 OGN 玛尔扎哈 `[A A]` resource skill；本基线只证明既有 Malzahar ordinary play、ActivateAbility、PaymentEngine、ActionPrompt、GameHub 与相邻路径在实现前绿色，可作为后续回归护栏。

## 1. Worktree

实现前 `git status --short --branch`：

```text
## main
?? riftbound-dotnet.sln
```

该文件为既有未跟踪文件，4D-03I handoff / baseline 不读取、不修改、不暂存。

## 2. 官方候选确认

候选：`OGN·113/298` 玛尔扎哈 / Malzahar，cardId `31332`，FU `FU-0f7cbe26ce`。

现有证据入口：

- Design gate：`docs/CURRENT_STAGE4C_BATCH88_MALZAHAR_RESOURCE_SKILL_DESIGN_GATE.md`
- Preflight fixture：`tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-malzahar-tap-rune-static.fixture.json`
- Matrix skeleton references：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

官方文本要点：

- 摧毁一个友方单位或装备。
- 横置 Malzahar。
- 迅捷获得 `A A`，用以支付符能费用。
- 可在己方回合或法术对决中使用。
- 获得费用资源的技能无法成为其他法术的反应目标。

## 3. 当前代码边界

- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 只登记 `OGN·113/298` 普通打出路径。
- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs` 当前已实现能力只包含 Vi / Xerath 代表路径，没有 Malzahar resource skill definition。
- `CoreRuleEngine.ResolveActivateAbility` 当前不会处理 Malzahar resource skill。
- `MatchSession.ActivateAbilitySourceRequirements` 当前不会为 Malzahar 暴露 ability source 或 destroy-cost target choices。
- 现有 `p2-preflight-play-ogn-malzahar-tap-rune-static.fixture.json` 明确说明 tap resource skill deferred。

## 4. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Malzahar|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill"
```

第一次并行执行时与另一个 `dotnet test` 撞到 `obj/Debug/net10.0/ref/Riftbound.Contracts.dll` 文件锁，A 已按顺序重跑；该锁冲突不作为测试失败。

顺序重跑结果：

```text
Passed!  - Failed:     0, Passed:    83, Skipped:     0, Total:    83
```

覆盖含义：

- Malzahar ordinary play preflight 代表路径绿色。
- 既有 Vi / Xerath `ACTIVATE_ABILITY`、PaymentEngineUnification 与 resource skill / deferred audit 相邻路径绿色。
- 该命令不证明 Malzahar `[A A]` resource skill 已实现。

## 5. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~SpellDuel|FullyQualifiedName~Priority"
```

结果：

```text
Passed!  - Failed:     0, Passed:   312, Skipped:     0, Total:   312
```

覆盖含义：

- ActivateAbility、PaymentEngine foundation、ActionPrompt、GameHub、PaymentResource、SpendPower、RunePool、SpellDuel 与 Priority 相邻路径在实现前绿色。
- 该命令是后续 4D-03I 实现后必须保持通过的回归护栏。

## 6. A 侧结论

4D-03I 可以进入 B 侧实现交接。实现前风险集中在 Malzahar resource skill 的 timing / reaction prohibition / payment-only resource restriction 语义：若 B 无法在本切片可靠区分 open-main representative 与完整 swift / spell-duel / reaction model，应只开放 open-main 代表路径，并把其余能力保留为 explicit residual risk。
