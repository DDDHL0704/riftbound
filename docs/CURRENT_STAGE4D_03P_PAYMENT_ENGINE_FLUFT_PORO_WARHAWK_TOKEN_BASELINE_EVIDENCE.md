# Stage 4D-03P PaymentEngine Fluft Poro Warhawk Token Baseline Evidence

日期：2026-05-14
结论：**IMPLEMENTATION BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03P Fluft Poro Warhawk token focused slice 的实现前基线。该基线只说明当前相邻路径绿色，可作为 B 侧实现回归护栏；它不代表 Fluft Poro activated skill 已实现，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. Scope

目标实现切片：

- `UNL-160/219` `绵绵魄罗` / `Fluft Poro`
- 官方 activated text：`{{横置}}：打出两名1{{S}}的“战鹰”，它们拥有{{法盾}}。我必须位于战场上才能使用此技能。`
- 当前 deferred surface：`DEFERRED_TAP_CREATE_TWO_SPELLSHIELD_WARHAWKS`
- 建议 ability id：`FLUFT_PORO_EXHAUST_CREATE_TWO_SPELLSHIELD_WARHAWKS`
- 建议 effect kind：`FLUFT_PORO_ACTIVATED_CREATE_TWO_WARHAWKS`
- Token identity：`UNL·T02` `战鹰`，1 power，unit token，Spellshield / `法盾`

本基线不实现该 skill；只锁定 Fluft / Warhawk / activated ability / token / Spellshield / ActionPrompt 相邻路径当前均通过。

## 2. Current Facts

- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 仍保留 Fluft Poro deferred surface。
- `P4ActivateAbilityCommandRejectsDeferredSurfacesOutsideRegistry` 当前会拒绝 `DEFERRED_TAP_CREATE_TWO_SPELLSHIELD_WARHAWKS`，不横置 source、不支付资源、不创建 stack、不创建 token。
- `p2-preflight-play-fluft-poro-activated-skill-unit` 当前只验证普通手牌打出后成为 5 power `CARD_TYPE:UNIT|魄罗` source unit，activated skill 暂缓。
- `p4-play-fluft-poro-target-rejected` 当前只验证普通手牌打出路径带显式目标时拒绝。
- `P6TokenFactoryCatalog` 已登记 `UNL·T02` Warhawk token。
- 现有 Warhawk representative evidence 已覆盖 `UNL·T02` token 创建到 controller base、1 power、unit / Spellshield tag 可追踪。

## 3. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Fluft|FullyQualifiedName~Warhawk|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~Token"
```

结果：

```text
Passed! - Failed: 0, Passed: 167, Skipped: 0, Total: 167
```

## 4. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Fluft|FullyQualifiedName~Warhawk|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Token|FullyQualifiedName~Spellshield|FullyQualifiedName~Battlefield"
```

结果：

```text
Passed! - Failed: 0, Passed: 663, Skipped: 0, Total: 663
```

## 5. Worktree Note

Baseline 时工作区只有既有未跟踪 `riftbound-dotnet.sln`；A 未读取、未修改、未暂存该文件。

## 6. B-Side Acceptance Gate

B 侧实现完成后至少需要重新运行本文件第 3 / 4 节命令，并由 A 复核 diff。若实现触及 shared activated ability stack resolution、token factory 或 prompt metadata，A 可追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. Verdict

4D-03P implementation baseline accepted. 当前 Fluft / Warhawk / activated ability / token / Spellshield 相邻路径绿色，可进入 B 侧 focused implementation；项目仍 **NOT READY**。
