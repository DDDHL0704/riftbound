# Stage 4D-03S PaymentEngine SFD Sigil Typed Resource Family Evidence

日期：2026-05-14
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

本文件记录 4D-03S focused slice 的验证证据。该证据只证明剩余五张 SFD Sigil typed payment-only resource skills 的 representative path 已接入，不关闭 P0-005 full official。

## 1. Files Changed

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/SfdSigilResourceSkillTests.cs`

未修改：

- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未跟踪文件 `riftbound-dotnet.sln`。

## 2. Behavioral Evidence

已验证：

- Focus Sigil / Insight Sigil / Power Sigil / Discord Sigil / Unity Sigil 均有 executable typed resource definition。
- 合法 stack-priority reaction window 中，服务端 prompt 暴露对应 base equipment source requirement。
- Source guard 要求 controller base、公开正面、equipment、未横置、正确 SFD cardNo。
- 成功 activation 横置 source，创建对应颜色 typed temporary payment-only resource ledger，不创建 ordinary stack item。
- Temporary resource 可支付同色 typed rune cost 与 generic `A` rune cost。
- Temporary resource 拒绝 wrong-color typed cost 与 mana-only cost，并保持 no-mutation。
- OGN Sigils 在本切片不暴露 executable prompt。
- Rage Sigil 既有 4D-03R 测试继续通过。

## 3. Validation Log

Focused regression:

```text
已通过! - 失败:     0，通过:   213，已跳过:     0，总计:   213
```

Adjacent regression:

```text
已通过! - 失败:     0，通过:   461，已跳过:     0，总计:   461
```

Backend full:

```text
已通过! - 失败:     0，通过:  4043，已跳过:     0，总计:  4043
```

`git diff --check`：无输出。

## 4. Acceptance Notes

- 本切片 supersedes `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_BASELINE_EVIDENCE.md`，但二者仍保留为回归护栏。
- 本切片不代表完整 Sigil family，因为 OGN resource skills 仍 deferred。
- 本切片不代表完整 `[A]` / `[C]` resource skill family 或 P0-005 full official。

## 5. Verdict

4D-03S evidence accepted. 项目仍 **NOT READY**。
