# Stage 4D-03R PaymentEngine Rage Sigil Typed Resource Baseline Evidence

日期：2026-05-14
结论：**IMPLEMENTATION BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03R Rage Sigil typed resource focused slice 的实现前基线。该基线只说明当前相邻路径绿色，可作为 B 侧实现回归护栏；它不代表 Rage Sigil resource skill 已实现，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. Scope

目标实现切片：

- `SFD·222/221` `暴怒之印` / `Rage Sigil`
- 官方 activated resource text：`{{横置}}：{{反应}}—{{获得}}{{红色}}，用以支付符能费用。（获得费用资源的技能无法成为其他法术的反应目标。）`
- 建议 ability id：`RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER`
- 建议 effect kind：`RAGE_SIGIL_REACTION_TYPED_RESOURCE_GAIN_RED`
- 建议 resource restriction：`PAY_RUNE_COSTS_ONLY_TYPED_RED_TEMPORARY_LEDGER_4D_03R`
- Source：controlled face-up ready base equipment `SFD·222/221`
- Effect：横置 source，立即创建 1 点 red typed payment-only temporary rune resource；不创建普通 stack item

本基线不实现该 skill；只锁定 Rage Sigil / Sigil / activated ability / resource skill / payment / RunePool adjacent 路径当前均通过。

## 2. Current Facts

- `p2-preflight-play-rage-sigil-equipment` 当前验证 SFD Rage Sigil 0 费、0 目标 ordinary equipment play to controller base。
- `p4-play-rage-sigil-target-rejected` 当前验证 SFD Rage Sigil 打出路径携带显式目标时 rejected no-mutation。
- `CardBehaviorRegistry` 已能让 `SFD·222/221` 成为 controller base equipment object，但 activated resource skill 尚未接入 `P4ActivatedAbilityCatalog`。
- `TemporaryPaymentResourceState` 当前只支持 generic `GeneratedPower` / `RemainingPower`；4D-03R 需要 typed red temporary resource 表达。
- `RunePool.PowerByTrait` 与 typed power payment rules 已存在，可作为 red resource consumption 基础。
- 本基线刻意不实现 OGN `OGN·040/298` Rage Sigil 或其他颜色 Sigil family。

## 3. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：

```text
Passed! - Failed: 0, Passed: 173, Skipped: 0, Total: 173
```

## 4. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel"
```

结果：

```text
Passed! - Failed: 0, Passed: 421, Skipped: 0, Total: 421
```

## 5. Worktree Note

Baseline 时工作区只有既有未跟踪 `riftbound-dotnet.sln`；A 未读取、未修改、未暂存该文件。

## 6. B-Side Acceptance Gate

B 侧实现完成后至少需要重新运行本文件第 3 / 4 节命令，并由 A 复核 diff。若实现触及 temporary resource schema、payment resource quote / commit、inline payment windows 或 shared `PaymentCostRules`，A 可追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. Verdict

4D-03R implementation baseline accepted. 当前 Rage Sigil / Sigil / activated ability / resource skill / payment / RunePool adjacent 路径绿色，可进入 B 侧 focused implementation；项目仍 **NOT READY**。
