# Stage 4D-03U PaymentEngine Resource Conversion Equipment Baseline Evidence

日期：2026-05-14
结论：**IMPLEMENTATION BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03U resource conversion equipment slice 的实现前基线。该基线只说明当前相邻路径绿色，可作为 B 侧实现回归护栏；它不代表三张 resource conversion skills 已实现，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. Scope

目标实现切片：

- `OGN·098/298` 能量通道：reaction-speed base equipment skill，横置获得 1 mana resource。
- `SFD·117/221` 远古簇碑：reaction-speed base equipment skill，支付任意数量 mana 获得等量 generic temporary payment-only rune resource。
- `SFD·083/221` 海克斯异常体：reaction-speed base equipment skill，支付任意数量 generic rune power 获得等量 mana。

## 2. Current Facts

- 三张 ordinary equipment play fixtures 与 target rejection fixtures 已存在并通过。
- 当前三张 resource conversion skills 尚未接入 executable profiles；这是 4D-03U 的实现范围。
- 当前 `DragonSoulSageResourceSkillTests` 已覆盖 reaction mana gain representative，`SfdSigilResourceSkillTests` / `OgnSigilResourceSkillTests` 已覆盖 typed temporary payment-only rune resource representative，可作为实现参考。
- 本基线保留未跟踪 `riftbound-dotnet.sln`，A 未读取、未修改、未暂存该文件。

## 3. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AncientStele|FullyQualifiedName~HextechAnomaly|FullyQualifiedName~EnergyChannel|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：

```text
Passed! - Failed: 0, Passed: 209, Skipped: 0, Total: 209
```

## 4. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AncientStele|FullyQualifiedName~HextechAnomaly|FullyQualifiedName~EnergyChannel|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel|FullyQualifiedName~PayCost"
```

结果：

```text
Passed! - Failed: 0, Passed: 464, Skipped: 0, Total: 464
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

4D-03U implementation baseline accepted. 当前 Ancient Stele / Hextech Anomaly / Energy Channel ordinary play、activated ability、resource skill、temporary resource、payment、RunePool adjacent 路径绿色，可进入 B 侧 focused implementation；项目仍 **NOT READY**。
