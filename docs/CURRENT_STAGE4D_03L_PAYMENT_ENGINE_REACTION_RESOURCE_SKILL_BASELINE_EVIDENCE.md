# Stage 4D-03L PaymentEngine Reaction Resource Skill Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-03L 实现前的 A 侧测试基线。当前 HEAD 已完成 4D-03K temporary resource inline representative，但尚未实现 `UNL-093/219` 龙魂贤者 reaction-speed resource skill。本基线只证明实现前 Dragon Soul Sage / activated ability / reaction / payment 相邻路径绿色，可作为后续 B 侧实现验收对照。

## 1. Baseline Scope

当前已存在行为：

- Dragon Soul Sage 普通手牌打出、0 目标入栈、结算后成为单位对象的 representative fixture 通过。
- Dragon Soul Sage 普通打出携带显式目标会 rejected no-mutation。
- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 仍记录 `DEFERRED_TAP_REACTION_GAIN_1_MANA`，并用官方文本锚点 `{{反应>}} {{横置}}：{{获得}}{{1}}` 审计。
- Vi、Xerath、Malzahar 三条 executable activated ability representative 当前绿色。
- 4D-03K 后 Malzahar temporary payment resource pending / inline consumption 当前绿色。

当前未实现行为：

- Dragon Soul Sage reaction-speed `ACTIVATE_ABILITY` prompt source requirement。
- Dragon Soul Sage command success path：横置 source、获得 1 点资源、即时 audit、无普通 stack item。
- reaction / priority timing guard 与 no-mutation 矩阵。
- generated resource 的 reset / cleanup lifecycle 证据。

## 2. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~DragonSoulSage|FullyQualifiedName~Malzahar|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

结果：

```text
Passed! - Failed: 0, Passed: 126, Skipped: 0, Total: 126
```

覆盖含义：

- Dragon Soul Sage 当前普通入场 / deferred activated surface 代表路径绿色。
- Malzahar resource skill lifecycle 与 temporary payment resource 代表路径绿色。
- `ACTIVATE_ABILITY` 与 `PaymentEngineUnificationTests` 当前绿色。
- 该基线不证明 Dragon Soul Sage reaction resource skill 已实现；它只固定实现前状态。

## 3. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~DragonSoulSage|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~SpellDuel|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：

```text
Passed! - Failed: 0, Passed: 374, Skipped: 0, Total: 374
```

覆盖含义：

- Activated ability、reaction / priority / spell-duel、payment resource、rune pool、ActionPrompt 与 GameHub 相邻路径当前绿色。
- 该基线适合作为 4D-03L 实现后的 regression 对照。

## 4. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件；本基线未读取、未修改、未暂存。

## 5. Verdict

4D-03L 可以进入 B 侧实现交接。实现前风险集中在 reaction-speed resource skill timing、source legality、resource generation lifecycle、ordinary stack / reaction target prohibition 与现有 Vi / Xerath / Malzahar activated ability 回归边界；若 B 只能完成 prompt 或 command 的部分路径，必须在审计中保留 explicit residual risk，不能关闭 P0-005。
