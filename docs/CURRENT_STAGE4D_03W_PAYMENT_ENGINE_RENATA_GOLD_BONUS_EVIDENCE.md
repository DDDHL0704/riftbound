# Stage 4D-03W PaymentEngine Renata Gold Bonus Evidence

日期：2026-05-14
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

本文件记录 4D-03W focused slice 的验证证据。该证据只证明 `RENATA_GOLD_EXTRA_1_MANA` marker 对 Gold resource activation 的 +1 mana representative path 已接入，不关闭 P0-005 full official。

## 1. Files Changed

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`

未修改：

- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 未跟踪文件 `riftbound-dotnet.sln`。

## 2. Behavioral Evidence

已验证：

- 普通 Gold source prompt metadata 暴露 `renataGoldExtraManaAvailable=false`、`bonusMana=0`。
- 带 marker 的 Gold source prompt metadata 暴露 `renataGoldExtraManaAvailable=true`、`bonusMana=1`、`bonusTag=RENATA_GOLD_EXTRA_1_MANA`。
- 普通 Gold activation 仍只创建 1 点 generic temporary payment-only rune resource，不加 mana。
- 带 marker 的 `UNL·T05` 与 `SFD·T03` Gold activation 均摧毁 source、创建 1 点 generic temporary payment-only rune resource，并使 controller rune pool mana +1。
- Marker success events 暴露 `renataGoldExtraManaApplied=true`、`generatedMana=1`、`bonusTag` 与 `MANA_GAINED`。
- Bonus mana 留在 normal rune pool；Gold temporary resource 仍不能作为 mana-only payment resource。
- Wrong timing、target、optional cost、wrong controller 等 rejected path 保持 no-mutation，不增加 mana。

## 3. Validation Log

Focused regression:

```text
已通过! - 失败:     0，通过:   320，已跳过:     0，总计:   320
```

Adjacent regression:

```text
已通过! - 失败:     0，通过:   965，已跳过:     0，总计:   965
```

Backend full:

```text
已通过! - 失败:     0，通过:  4120，已跳过:     0，总计:  4120
```

`git diff --check`：无输出。

## 4. Acceptance Notes

- 本切片 supersedes `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_BASELINE_EVIDENCE.md`，但二者仍保留为回归护栏。
- 本切片不修改前端运行时代码；前端仍以服务端 `ActionPrompt` / snapshot 为唯一权威。
- 本切片不升级 coverage matrix full-official，也不关闭 equipment-token full rules、完整 `[A]` / `[C]` resource skill family 或 P0-005。

## 5. Verdict

4D-03W evidence accepted. 项目仍 **NOT READY**。
