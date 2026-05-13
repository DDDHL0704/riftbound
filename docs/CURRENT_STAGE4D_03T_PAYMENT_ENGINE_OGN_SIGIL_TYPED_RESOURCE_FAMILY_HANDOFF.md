# Stage 4D-03T PaymentEngine OGN Sigil Typed Resource Family Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03T 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03R / 4D-03S 已验收 SFD 六张 Sigil typed payment-only resource skills。4D-03T 只补 OGN 复刻版六张同名 Sigil，目标是关闭 “完整 Sigil family” 这一小家族缺口，同时仍不扩展到其他 `[A]` / `[C]` resource skill family：

- `OGN·040/298` 暴怒之印：red typed temporary payment-only resource。
- `OGN·081/298` 专注之印：green typed temporary payment-only resource。
- `OGN·120/298` 洞察之印：blue typed temporary payment-only resource。
- `OGN·163/298` 力量之印：orange typed temporary payment-only resource。
- `OGN·204/298` 不和之印：purple typed temporary payment-only resource。
- `OGN·245/298` 团结之印：yellow typed temporary payment-only resource。

本切片应复用 4D-03S 的 Sigil typed resource profile / shared resolver / prompt / temporary ledger / payment cleanup 口径。成功路径仍为 controlled face-up ready base equipment source，在合法 stack-priority reaction window 中横置 source，创建对应颜色 1 点 payment-only temporary rune resource，不创建 ordinary stack item。

## 2. 当前代码事实

- `CardBehaviorRegistry` 已有 OGN 六张 Sigil 0 费装备打出路径。
- `rules-evidence-index.md` 已有 OGN Sigil 普通装备打出与显式目标拒绝证据，当前仍写明横置获得对应颜色符能技能暂缓。
- `P4ActivatedAbilityCatalog` 现有 SFD Sigil typed resource profiles/helper；4D-03T 应在同一模型下增加 OGN profiles，而不是新增第二套 resolver。
- 当前 `SfdSigilResourceSkillTests.OgnSigilsRemainDeferredForSfdResourceSkills` 明确锁定 OGN 不会被 SFD ability ids 自动打开；4D-03T 可改为验证 OGN 拥有自己的 executable profiles，同时仍拒绝用 SFD ability id 激活 OGN source。

## 3. 建议写入范围

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/SfdSigilResourceSkillTests.cs`
- 可新增 `tests/Riftbound.ConformanceTests/OgnSigilResourceSkillTests.cs`

不建议写入：

- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 非 Sigil resource skills。
- 未跟踪文件 `riftbound-dotnet.sln`。

## 4. 实现要求

- 为 OGN 六张 Sigil 增加 explicit executable profiles / definitions。建议 ability ids 使用 `OGN_*` 前缀，例如 `OGN_RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER`。
- 不要让 SFD ability ids 接受 OGN source；如要建同名卡别名，也必须在 profile/source guard 中可审计地区分。
- Prompt metadata 与 SFD Sigil 保持一致：`resourceSkill`、`reactionSpeed`、`typedPaymentOnlyResource`、`generatedPowerByTrait`、`resourceRestriction`、`resourceLifecycle=temporary-payment-resource-ledger`、`stackPolicy=no-ordinary-stack-item`。
- Command success 与 SFD Sigil 一致：横置 source、创建 typed temporary ledger、不创建 stack item、不改变 ordinary runePool。
- Payment consumption 与 cleanup 复用 4D-03R/03S 口径：同色 typed cost / generic `A` 可支付，wrong-color / mana-only / experience / non-rune rejected no-mutation。

## 5. 必补测试

- OGN 六张 Sigil catalog definitions 存在，trait / restriction / sourceCardNo 正确。
- 合法 reaction prompt 暴露 OGN six sources。
- 成功 activation 对六种颜色分别创建 typed temporary ledger。
- Temporary resource 可支付同色 typed cost 与 generic rune cost；拒绝 wrong-color 与 mana-only。
- OGN source 使用 SFD ability id rejected no-mutation；SFD source 使用 OGN ability id rejected no-mutation。
- Existing SFD Sigil tests、Rage Sigil tests、OGN ordinary play fixtures、PaymentEngine / RunePool / ActionPrompt / GameHub adjacent regression 继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel"
```

如触及 shared Sigil profile / prompt / temporary ledger path，A 验收时应追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. No-Go Criteria

- 不要扩展到其他 resource skill families。
- 不要把 Sigil resource skill 做成普通 stack item。
- 不要让 typed temporary resource 支付错误颜色、mana-only、experience 或 non-rune costs。
- 不要修改前端运行时代码或 coverage matrix full-official 状态。
- 不要因为 4D-03T 通过而关闭 P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03T 是 Sigil family 收尾切片。它应把 OGN 六张同名 Sigil 纳入 4D-03S 已参数化的服务端 path，同时保留 SFD/OGN ability/source guard 可审计边界。该切片若通过，可声明完整 Sigil typed resource family representative 已验收，但仍不关闭完整 `[A]` / `[C]` resource skill family 或 P0-005 full official。
