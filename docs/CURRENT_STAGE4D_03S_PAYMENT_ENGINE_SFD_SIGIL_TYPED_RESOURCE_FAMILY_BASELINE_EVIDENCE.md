# Stage 4D-03S PaymentEngine SFD Sigil Typed Resource Family Baseline Evidence

日期：2026-05-14
结论：**IMPLEMENTATION BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03S SFD Sigil typed resource family slice 的实现前基线。该基线只说明当前相邻路径绿色，可作为 B 侧实现回归护栏；它不代表五张剩余 SFD Sigil resource skills 已实现，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. Scope

目标实现切片：

- `SFD·226/221` `专注之印` / `Focus Sigil`：横置，反应，获得 1 点 green typed payment-only temporary rune resource。
- `SFD·229/221` `洞察之印` / `Insight Sigil`：横置，反应，获得 1 点 blue typed payment-only temporary rune resource。
- `SFD·231/221` `力量之印` / `Power Sigil`：横置，反应，获得 1 点 orange typed payment-only temporary rune resource。
- `SFD·234/221` `不和之印` / `Discord Sigil`：横置，反应，获得 1 点 purple typed payment-only temporary rune resource。
- `SFD·238/221` `团结之印` / `Unity Sigil`：横置，反应，获得 1 点 yellow typed payment-only temporary rune resource。

本基线不实现这些 skills；只锁定 Rage Sigil / Sigil / activated ability / resource skill / payment / RunePool adjacent 路径当前均通过。4D-03S 明确不处理 OGN `OGN·081/298` / `OGN·120/298` / `OGN·163/298` / `OGN·204/298` / `OGN·245/298` resource skills。

## 2. Current Facts

- 4D-03R 已验收 `SFD·222/221` Rage Sigil typed red payment-only resource skill：focused 191/191、adjacent 439/439、backend full 4021/4021 通过。
- `p2-preflight-play-focus-sigil-equipment` / `p4-play-focus-sigil-target-rejected` 当前验证 `SFD·226/221` 普通装备打出与显式目标拒绝。
- `p2-preflight-play-insight-sigil-equipment` / `p4-play-insight-sigil-target-rejected` 当前验证 `SFD·229/221` 普通装备打出与显式目标拒绝。
- `p2-preflight-play-power-sigil-equipment` / `p4-play-power-sigil-target-rejected` 当前验证 `SFD·231/221` 普通装备打出与显式目标拒绝。
- `p2-preflight-play-discord-sigil-equipment` / `p4-play-discord-sigil-target-rejected` 当前验证 `SFD·234/221` 普通装备打出与显式目标拒绝。
- `p2-preflight-play-unity-sigil-equipment` / `p4-play-unity-sigil-target-rejected` 当前验证 `SFD·238/221` 普通装备打出与显式目标拒绝。
- `TemporaryPaymentResourceState` 已支持 typed generated / remaining power by trait；4D-03S 应复用该字段，不新增并行资源模型。
- 当前 OGN Sigil fixtures 只验证普通装备打出和目标拒绝；本基线刻意不把它们升级为 executable resource skills。

## 3. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：

```text
Passed! - Failed: 0, Passed: 191, Skipped: 0, Total: 191
```

## 4. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel"
```

结果：

```text
Passed! - Failed: 0, Passed: 439, Skipped: 0, Total: 439
```

## 5. Worktree Note

Baseline 时工作区只有既有未跟踪 `riftbound-dotnet.sln`；A 未读取、未修改、未暂存该文件。

## 6. B-Side Acceptance Gate

B 侧实现完成后至少需要重新运行本文件第 3 / 4 节命令，并由 A 复核 diff。若实现触及 temporary resource schema、payment resource quote / commit、inline payment windows、shared `PaymentCostRules` 或 Rage Sigil 既有 path，A 可追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. Verdict

4D-03S implementation baseline accepted. 当前 Rage Sigil / Sigil / activated ability / resource skill / payment / RunePool adjacent 路径绿色，可进入 B 侧 focused implementation；项目仍 **NOT READY**。
