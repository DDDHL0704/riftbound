# Stage 4D-03L PaymentEngine Reaction Resource Skill Evidence

日期：2026-05-14
结论：**FOCUSED SLICE GREEN / PROJECT NOT READY**

本文记录 4D-03L Dragon Soul Sage reaction resource skill focused slice 的实现证据。该证据接受 4D-03L focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Implementation Evidence

实现范围：

- `P4ActivatedAbilityCatalog`：
  - 新增 Dragon Soul Sage executable ability constants and definition。
  - `ReactionSpeed=true`、`GeneratedMana=1`、`IsResourceSkill=true`、`RequiresBattlefieldSource=true`。
  - 从 deferred-only surface 移除 `UNL-093/219`。
- `MatchSession`：
  - stack-priority reaction prompt 可公开 `ACTIVATE_ABILITY`。
  - Dragon Soul Sage source requirement metadata 包含 `resourceSkill`、`reactionSpeed`、`generatedMana`、`timingPolicy`、`reactionPolicy` 与 `resourceLifecycle`。
- `CoreRuleEngine`：
  - 新增 Dragon Soul Sage command resolver。
  - 成功路径横置 source、`RunePool.Mana +1`、不创建普通 stack item。
  - wrong timing/source/payload rejected no-mutation。
- `ReactionResourceSkillTests`：
  - prompt surface。
  - open-main 不暴露。
  - 成功无 stack item、获得 mana。
  - end-turn cleanup 清理 generated mana。
  - invalid timing/source/payload no-mutation。
- `ConformanceFixtureRunnerTests`：
  - executable ability catalog 断言 Dragon Soul Sage。
  - deferred surface audit 不再要求 Dragon Soul Sage 仍 deferred。

## 2. Focused Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~DragonSoulSage|FullyQualifiedName~Malzahar|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

结果：

```text
Passed! - Failed: 0, Passed: 140, Skipped: 0, Total: 140
```

## 3. Adjacent Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~DragonSoulSage|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~SpellDuel|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：

```text
Passed! - Failed: 0, Passed: 388, Skipped: 0, Total: 388
```

## 4. Backend Full

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：

```text
Passed! - Failed: 0, Passed: 3874, Skipped: 0, Total: 3874
```

## 5. Whitespace

命令：

```sh
git diff --check
```

结果：no output.

## 6. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件；本切片未读取、未修改、未暂存。

## 7. Verdict

4D-03L focused slice accepted. Dragon Soul Sage reaction resource skill is now covered by server-authoritative prompt / command / audit representative tests. P0-005 remains open because the complete activated ability / resource skill family and full PaymentEngine quote parity are still not full-official.
