# Stage 4D-03T PaymentEngine OGN Sigil Typed Resource Family Baseline Evidence

日期：2026-05-14
结论：**IMPLEMENTATION BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03T OGN Sigil typed resource family slice 的实现前基线。该基线只说明当前相邻路径绿色，可作为 B 侧实现回归护栏；它不代表 OGN Sigil resource skills 已实现，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. Scope

目标实现切片：

- `OGN·040/298` 暴怒之印：red typed payment-only temporary rune resource。
- `OGN·081/298` 专注之印：green typed payment-only temporary rune resource。
- `OGN·120/298` 洞察之印：blue typed payment-only temporary rune resource。
- `OGN·163/298` 力量之印：orange typed payment-only temporary rune resource。
- `OGN·204/298` 不和之印：purple typed payment-only temporary rune resource。
- `OGN·245/298` 团结之印：yellow typed payment-only temporary rune resource。

## 2. Current Facts

- SFD Sigil family representative 已在 4D-03R / 4D-03S 验收。
- OGN Sigil ordinary play fixtures 与 target rejection fixtures 仍通过。
- 当前 OGN Sigil resource skills 尚未接入 executable profiles；这正是 4D-03T 的实现范围。
- 本基线保留未跟踪 `riftbound-dotnet.sln`，A 未读取、未修改、未暂存该文件。

## 3. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：

```text
Passed! - Failed: 0, Passed: 213, Skipped: 0, Total: 213
```

## 4. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel"
```

结果：

```text
Passed! - Failed: 0, Passed: 461, Skipped: 0, Total: 461
```

## 5. Acceptance Gate

B 侧实现完成后至少重新运行第 3 / 4 节命令，并由 A 复核 diff。若实现触及 shared profile/prompt/ledger path，A 应追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 6. Verdict

4D-03T implementation baseline accepted. 当前 Sigil / activated ability / resource skill / payment / RunePool adjacent 路径绿色，可进入 B 侧 focused implementation；项目仍 **NOT READY**。
