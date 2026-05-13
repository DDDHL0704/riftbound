# Stage 4D-03O PaymentEngine Crimson Rose Ready Unit Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-03O 实现前的 A 侧测试基线。当前 HEAD 已完成 4D-03N Renata Glasc colored activated score representative，但尚未实现 `UNL-109/219` 猩红玫瑰 activated ready-unit skill。本基线只证明实现前 Crimson / activated ability / payment 相邻路径绿色，可作为后续 B 侧实现验收对照。

## 1. Baseline Scope

当前已存在行为：

- Scarlet Rose `UNL-109/219` 普通手牌打出、0 目标入栈、结算后成为 controller base 的 `CARD_TYPE:EQUIPMENT` 装备对象。
- Scarlet Rose 普通打出携带显式目标会 rejected no-mutation。
- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 仍记录 `DEFERRED_EXPERIENCE_EXHAUST_READY_UNIT`，并用官方文本锚点 `消耗3经验，{{横置}}：让一名单位变为活跃状态` 审计。
- Vi、Xerath、Malzahar、Dragon Soul Sage、Renata draw 与 Renata score executable `ACTIVATE_ABILITY` representatives 当前绿色。
- PaymentEngine shared `PaymentPlan` 已覆盖 mana、generic / typed power、experience、payment resource actions 与 audit payload；Xerath 已覆盖 target-bearing activated skill 的 enemy Spellshield target tax representative。

当前未实现行为：

- Crimson Rose ready-unit `ACTIVATE_ABILITY` prompt source requirement。
- Controlled base equipment source 支持。
- `experienceCost=3` activated ability payment quote / command commit。
- Source equipment exhaust-as-cost rollback。
- Target-bearing ready-unit stack item 与 pass-pass `UNIT_READIED` resolution。
- Enemy Spellshield target tax for Crimson Rose skill target。
- Rejection surface for temporary resources / recycle rune actions / invalid target / insufficient experience.

## 2. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Crimson|FullyQualifiedName~Scarlet|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

结果：

```text
Passed! - Failed: 0, Passed: 143, Skipped: 0, Total: 143
```

覆盖含义：

- Scarlet Rose ordinary equipment play / target rejected representative 当前绿色。
- Existing activated ability catalog / deferred surface audit 当前绿色。
- Vi、Xerath、Malzahar、Dragon Soul Sage、Renata draw、Renata score 与 `PaymentEngineUnificationTests` 当前绿色。
- 该基线不证明 Crimson Rose activated ready-unit skill 已实现；它只固定实现前状态。

## 3. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Crimson|FullyQualifiedName~Scarlet|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Experience|FullyQualifiedName~Spellshield"
```

结果：

```text
Passed! - Failed: 0, Passed: 370, Skipped: 0, Total: 370
```

覆盖含义：

- Activated ability、payment resource、typed spend power、rune pool、ActionPrompt、GameHub、experience 与 Spellshield 相邻路径当前绿色。
- 该基线适合作为 4D-03O 实现后的 regression 对照。

## 4. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件；本基线未读取、未修改、未暂存。

## 5. Verdict

4D-03O 可以进入 B 侧实现交接。实现前风险集中在 equipment source prompt support、experience-cost payment plan、source exhaust-as-cost rollback、target-bearing skill target choices、enemy Spellshield target tax、ordinary stack-before-ready resolution、existing Scarlet Rose play fixtures 与 Renata / Xerath / Malzahar / Dragon Soul Sage regressions；若 B 只能完成 prompt 或 command 的部分路径，必须在审计中保留 explicit residual risk，不能关闭 P0-005。
