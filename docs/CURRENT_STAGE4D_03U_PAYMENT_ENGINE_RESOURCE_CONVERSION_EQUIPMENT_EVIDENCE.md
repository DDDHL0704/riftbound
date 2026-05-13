# Stage 4D-03U PaymentEngine Resource Conversion Equipment Evidence

日期：2026-05-14
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

本文件记录 4D-03U focused slice 的验证证据。该证据只证明能量通道、远古簇碑、海克斯异常体三张 resource conversion equipment skills 的 representative path 已接入，不关闭 P0-005 full official。

## 1. Files Changed

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/ResourceConversionEquipmentSkillTests.cs`

未修改：

- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未跟踪文件 `riftbound-dotnet.sln`。

## 2. Behavioral Evidence

已验证：

- 三张 conversion equipment 均有 executable reaction resource skill definition。
- 合法 stack-priority reaction window 中，服务端 prompt 暴露对应 controlled face-up ready base equipment source requirement。
- 远古簇碑与海克斯异常体的转换数量只能来自服务端生成的 `OptionalCosts` conversion choices。
- 能量通道横置 source，获得 1 mana，不创建 ordinary stack item。
- 远古簇碑扣除 mana 后创建 generic temporary payment-only rune ledger，可支付 generic rune cost。
- 远古簇碑 temporary resource 拒绝 mana-only payment，保持 no-mutation。
- 海克斯异常体消耗 ordinary generic `RunePool.Power` 后获得等量 mana。
- 海克斯异常体在只有 temporary payment resource、没有 ordinary generic power 时拒绝转换，保持 no-mutation。
- target、wrong timing、wrong card、exhausted source、missing / invalid / overpay conversion choice 均 rejected no-mutation。

## 3. Validation Log

Focused regression:

```text
已通过! - 失败:     0，通过:   230，已跳过:     0，总计:   230
```

Adjacent regression:

```text
已通过! - 失败:     0，通过:   485，已跳过:     0，总计:   485
```

Backend full:

```text
已通过! - 失败:     0，通过:  4089，已跳过:     0，总计:  4089
```

`git diff --check`：无输出。

## 4. Acceptance Notes

- 本切片 supersedes `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_BASELINE_EVIDENCE.md`，但二者仍保留为回归护栏。
- 本切片不修改前端运行时代码；前端仍以服务端 `ActionPrompt` / snapshot 为唯一权威。
- 本切片不升级 coverage matrix full-official，也不关闭完整 `[A]` / `[C]` resource skill family 或 P0-005。

## 5. Verdict

4D-03U evidence accepted. 项目仍 **NOT READY**。
