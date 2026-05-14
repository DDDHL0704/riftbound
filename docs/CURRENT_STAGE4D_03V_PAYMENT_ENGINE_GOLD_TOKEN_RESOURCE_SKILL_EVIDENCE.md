# Stage 4D-03V PaymentEngine Gold Token Resource Skill Evidence

日期：2026-05-14
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

本文件记录 4D-03V focused slice 的验证证据。该证据只证明 `UNL·T05` 与 `SFD·T03` Gold token resource / reaction ability 的 representative path 已接入，不关闭 P0-005 full official。

## 1. Files Changed

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/P6TokenFactoryCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`

未修改：

- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未跟踪文件 `riftbound-dotnet.sln`。

## 2. Behavioral Evidence

已验证：

- 两个 Gold token printings 均有 executable reaction resource skill definition。
- 两个 Gold deferred activated resource surfaces 已从 P6 deferred list 移除，其他 token deferred surfaces 保持。
- 合法 stack-priority reaction window 中，服务端 prompt 只向 priority player 暴露 controlled face-up ready base Gold equipment token source。
- Prompt metadata 暴露 destroy-as-cost、payment-only、temporary ledger lifecycle、no ordinary stack item 与 generated generic power。
- Activation 成功时 source 横置并摧毁，离开 base，进入 owner graveyard。
- Activation 成功时创建 1 点 generic temporary payment-only rune ledger，不创建 ordinary stack item。
- Gold temporary resource 可支付 generic rune cost，并在 payment close 后清理。
- Gold temporary resource 拒绝 mana-only、wrong trait 与 unnecessary use，保持 no-mutation。
- Renata Gold bonus tag 仍 deferred：带 marker 的 Gold 不加 mana，只生成 1 点 generic temporary power。
- target、optional cost、temporary resource payload、recycle rune payload、wrong timing、wrong controller、not-base、face-down、exhausted、non-equipment、missing Gold tag、wrong card、missing source 均 rejected no-mutation。

## 3. Validation Log

Focused regression:

```text
已通过! - 失败:     0，通过:   288，已跳过:     0，总计:   288
```

Adjacent regression:

```text
已通过! - 失败:     0，通过:   782，已跳过:     0，总计:   782
```

Backend full:

```text
已通过! - 失败:     0，通过:  4113，已跳过:     0，总计:  4113
```

`git diff --check`：无输出。

## 4. Acceptance Notes

- 本切片 supersedes `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_BASELINE_EVIDENCE.md`，但二者仍保留为回归护栏。
- 本切片不修改前端运行时代码；前端仍以服务端 `ActionPrompt` / snapshot 为唯一权威。
- 本切片不升级 coverage matrix full-official，也不关闭 Renata Gold extra mana bonus、equipment-token full rules、完整 `[A]` / `[C]` resource skill family 或 P0-005。

## 5. Verdict

4D-03V evidence accepted. 项目仍 **NOT READY**。
