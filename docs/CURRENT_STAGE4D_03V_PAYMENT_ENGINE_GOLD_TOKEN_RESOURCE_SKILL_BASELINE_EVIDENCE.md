# Stage 4D-03V PaymentEngine Gold Token Resource Skill Baseline Evidence

日期：2026-05-14
结论：**IMPLEMENTATION BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03V Gold token resource skill slice 的实现前基线。该基线只说明当前相邻路径绿色，可作为 B 侧实现回归护栏；它不代表 Gold token resource / reaction ability 已实现，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. Scope

目标实现切片：

- `UNL·T05` 金币：reaction-speed base equipment token skill，摧毁此牌并横置以获得 1 点 generic temporary payment-only rune resource。
- `SFD·T03` 金币：同一能力的 SFD wording / printing。

## 2. Current Facts

- `P6TokenFactoryCatalog` 已绑定 `UNL·T05` / `SFD·T03` Gold equipment token identity。
- 两个 Gold activated resource surfaces 当前仍在 P6 deferred list 中，这是 4D-03V 的实现范围。
- 现有 Gold creation sources、trigger payment、temporary payment resource、Sigil typed resources、conversion equipment、ActionPrompt / GameHub adjacent paths 均作为回归护栏。
- 本切片不实现 Renata Gold extra mana bonus；现有 `RENATA_GOLD_EXTRA_1_MANA` marker 只作为 no-go / follow-up 边界。
- 本基线保留未跟踪 `riftbound-dotnet.sln`，A 未读取、未修改、未暂存该文件。

## 3. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Token|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：

```text
Passed! - Failed: 0, Passed: 264, Skipped: 0, Total: 264
```

## 4. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Token|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel|FullyQualifiedName~PayCost|FullyQualifiedName~Equipment"
```

结果：

```text
Passed! - Failed: 0, Passed: 758, Skipped: 0, Total: 758
```

## 5. Acceptance Gate

B 侧实现完成后至少重新运行第 3 / 4 节命令，并由 A 复核 diff。若实现触及 shared payment plan / temporary resource / prompt path，A 应追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 6. Verdict

4D-03V implementation baseline accepted. 当前 Gold token identity / creation, activated ability, resource skill, temporary payment resource, payment, RunePool, ActionPrompt, GameHub, reaction, priority, spell-duel, PayCost 与 equipment adjacent 路径绿色，可进入 B 侧 focused implementation；项目仍 **NOT READY**。
