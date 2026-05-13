# Stage 4D-03T PaymentEngine OGN Sigil Typed Resource Family Evidence

日期：2026-05-14
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

本文件记录 4D-03T focused slice 的验证证据。该证据只证明 OGN 六张 Sigil typed payment-only resource skills 的 representative path 已接入，不关闭 P0-005 full official。

## 1. Files Changed

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/SfdSigilResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/OgnSigilResourceSkillTests.cs`

未修改：

- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未跟踪文件 `riftbound-dotnet.sln`。

## 2. Behavioral Evidence

已验证：

- OGN Rage / Focus / Insight / Power / Discord / Unity Sigil 均有 executable typed resource definition。
- 合法 stack-priority reaction window 中，服务端 prompt 暴露对应 OGN base equipment source requirement。
- Source guard 要求 controller base、公开正面、equipment、未横置、正确 OGN cardNo。
- 成功 activation 横置 source，创建对应颜色 typed temporary payment-only resource ledger，不创建 ordinary stack item。
- Temporary resource 可支付同色 typed rune cost 与 generic `A` rune cost。
- Temporary resource 拒绝 wrong-color typed cost 与 mana-only cost，并保持 no-mutation。
- SFD ability id 不能激活 OGN source，OGN ability id 不能激活 SFD source。
- SFD Sigil 与 Rage Sigil 既有测试继续通过。

## 3. Validation Log

Focused regression:

```text
已通过! - 失败:     0，通过:   238，已跳过:     0，总计:   238
```

Adjacent regression:

```text
已通过! - 失败:     0，通过:   486，已跳过:     0，总计:   486
```

Backend full:

```text
已通过! - 失败:     0，通过:  4068，已跳过:     0，总计:  4068
```

`git diff --check`：无输出。

## 4. Acceptance Notes

- 本切片 supersedes `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_BASELINE_EVIDENCE.md`，但二者仍保留为回归护栏。
- 本切片让 SFD + OGN Sigil typed resource family representative 收口，但不代表完整 `[A]` / `[C]` resource skill family 或 P0-005 full official。
- 本切片不修改前端运行时代码；前端仍以服务端 `ActionPrompt` / snapshot 为唯一权威。

## 5. Verdict

4D-03T evidence accepted. 项目仍 **NOT READY**。
