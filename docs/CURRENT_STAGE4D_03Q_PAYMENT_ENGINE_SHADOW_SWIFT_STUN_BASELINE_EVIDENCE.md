# Stage 4D-03Q PaymentEngine Shadow Swift Stun Baseline Evidence

日期：2026-05-14
结论：**IMPLEMENTATION BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03Q Shadow swift stun focused slice 的实现前基线。该基线只说明当前相邻路径绿色，可作为 B 侧实现回归护栏；它不代表 Shadow activated skill 已实现，不关闭 P0-004 或 P0-005，不改变项目 **NOT READY** 结论。

## 1. Scope

目标实现切片：

- `UNL-194/219` `黑影` / `Shadow`
- 官方 activated text：`{{迅捷>}} 支付{{1}}和{{A}}，{{横置}}：{{眩晕}}一名进攻此处的敌方单位`
- 当前 deferred surface：`DEFERRED_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER`
- 建议 ability id：`SHADOW_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER`
- 建议 effect kind：`SHADOW_ACTIVATED_STUN_ATTACKER`
- Cost：1 mana + 1 generic power + source exhaust，必要时附加 enemy Spellshield target tax mana
- Target：一名同一战场正在进攻的敌方单位

本基线不实现该 skill；只锁定 Shadow / activated ability / payment / swift / stun / battle adjacent 路径当前均通过。

## 2. Current Facts

- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 仍保留 Shadow deferred surface，且它是当前已知唯一剩余 P4 activated ability deferred surface。
- `P4ActivateAbilityCommandRejectsDeferredSurfacesOutsideRegistry` 当前会拒绝 `DEFERRED_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER`，不横置 source、不支付资源、不创建 stack、不应用 stun。
- `p2-preflight-play-shadow-base-unit-static` 当前只验证普通手牌打出后成为 3 power `CARD_TYPE:UNIT` source unit，activated swift stun skill 暂缓。
- `CardTargetScopes.EnemyAttackingUnit`、`IsAttackingBattlefieldObject`、battlefield object location 与 `STUNNED` status application patterns 已存在，可作为实现基础。
- 当前完整 P0-004 battle lifecycle 仍未关闭；4D-03Q 只允许 focused representative combat response / battle-response timing，不得宣称 full official battle response 完成。

## 3. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Shadow|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~Swift|FullyQualifiedName~Stun"
```

结果：

```text
Passed! - Failed: 0, Passed: 198, Skipped: 0, Total: 198
```

## 4. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Shadow|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Swift|FullyQualifiedName~Stun|FullyQualifiedName~SpellDuel|FullyQualifiedName~Battlefield|FullyQualifiedName~DeclareBattle"
```

结果：

```text
Passed! - Failed: 0, Passed: 738, Skipped: 0, Total: 738
```

## 5. Worktree Note

Baseline 时工作区只有既有未跟踪 `riftbound-dotnet.sln`；A 未读取、未修改、未暂存该文件。

## 6. B-Side Acceptance Gate

B 侧实现完成后至少需要重新运行本文件第 3 / 4 节命令，并由 A 复核 diff。若实现触及 shared activated ability stack resolution、battle response prompt timing、target tax 或 status effect resolution，A 可追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. Verdict

4D-03Q implementation baseline accepted. 当前 Shadow / activated ability / payment / swift / stun / battle adjacent 路径绿色，可进入 B 侧 focused implementation；项目仍 **NOT READY**。
