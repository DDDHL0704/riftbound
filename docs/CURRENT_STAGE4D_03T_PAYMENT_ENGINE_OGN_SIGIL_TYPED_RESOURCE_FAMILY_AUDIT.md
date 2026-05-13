# Stage 4D-03T PaymentEngine OGN Sigil Typed Resource Family Audit

日期：2026-05-14
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

4D-03T 已验收 OGN 六张 Sigil typed payment-only resource skills。该切片补齐 4D-03R / 4D-03S 后的 OGN reprint Sigil family representative，但不关闭完整 PaymentEngine、完整 `[A]` / `[C]` resource skill family、P1 LayerEngine、coverage matrix full-official 或最终 READY。

## 1. Scope

本切片实现：

- `OGN·040/298` 暴怒之印 / Rage Sigil：`{{横置}}：{{反应}}—{{获得}}{{红色}}，用以支付符能费用。`
- `OGN·081/298` 专注之印 / Focus Sigil：`{{横置}}：{{反应}}—{{获得}}{{绿色}}，用以支付符能费用。`
- `OGN·120/298` 洞察之印 / Insight Sigil：`{{横置}}：{{反应}}—{{获得}}{{蓝色}}，用以支付符能费用。`
- `OGN·163/298` 力量之印 / Power Sigil：`{{横置}}：{{反应}}—{{获得}}{{橙色}}，用以支付符能费用。`
- `OGN·204/298` 不和之印 / Discord Sigil：`{{横置}}：{{反应}}—{{获得}}{{紫色}}，用以支付符能费用。`
- `OGN·245/298` 团结之印 / Unity Sigil：`{{横置}}：{{反应}}—{{获得}}{{黄色}}，用以支付符能费用。`

本切片复用并泛化 4D-03S 的 Sigil typed resource profile / resolver / prompt / temporary ledger 口径，同时保留 SFD / OGN ability id 与 source cardNo 的可审计边界。

## 2. Implementation Facts

- `P4ActivatedAbilityCatalog` 新增 OGN Sigil constants / profiles，并将 Sigil helper 泛化为 shared `IsSigilTypedResourceAbility`、`TryGetSigilTypedResourceProfile` 与 `GetSigilTypedResourceProfiles`。
- 新增六个 OGN ability ids：
  - `OGN_RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER`
  - `OGN_FOCUS_SIGIL_REACTION_EXHAUST_GAIN_1_GREEN_POWER`
  - `OGN_INSIGHT_SIGIL_REACTION_EXHAUST_GAIN_1_BLUE_POWER`
  - `OGN_POWER_SIGIL_REACTION_EXHAUST_GAIN_1_ORANGE_POWER`
  - `OGN_DISCORD_SIGIL_REACTION_EXHAUST_GAIN_1_PURPLE_POWER`
  - `OGN_UNITY_SIGIL_REACTION_EXHAUST_GAIN_1_YELLOW_POWER`
- 每个 OGN definition 都表达 `IsResourceSkill=true`、`ReactionSpeed=true`、`PaymentOnlyResource=true`、`RequiresBaseEquipmentSource=true`、`ExhaustsSourceAsCost=true`、`RequiredTargetCount=0`、`GeneratedPowerByTrait.<trait>=1`。
- `CoreRuleEngine.ResolveActivateAbility` 通过 shared Sigil helper 分派到 `ResolveSigilTypedResourceSkill`。
- Shared resolver 仍校验 stack-priority reaction timing、当前玩家控制、公开正面、未横置、controller base equipment source、正确 source cardNo、无 targets / optional costs / payment resource actions。
- 成功命令横置 source，创建对应颜色 typed temporary payment-only resource ledger，不创建 ordinary stack item，不改变普通 runePool mana/power。
- `MatchSession` prompt / snapshot restriction、display label 与 source requirement metadata 改为 shared Sigil profile，OGN 与 SFD 均输出 typed resource、reaction policy、no-stack policy、resource lifecycle、allowed payment kind 与 generatedPowerByTrait。
- SFD / OGN guard 保持明确：SFD ability id 不能激活 OGN source，OGN ability id 不能激活 SFD source。

## 3. Tests Added

新增 `tests/Riftbound.ConformanceTests/OgnSigilResourceSkillTests.cs`，覆盖：

- OGN 六张 Sigil catalog definitions。
- 合法 reaction priority prompt 中暴露 OGN Sigil typed resource source requirements。
- 六色 activation 成功横置 source、创建 typed temporary ledger、无普通 stack item。
- 六色 temporary resource 支付同色 typed rune cost 与 generic rune cost，并清理 ledger。
- 六色 temporary resource 拒绝 wrong-color typed cost 与 mana-only cost，保持 no-mutation。
- SFD / OGN ability id 与 source printing 双向 cross-print rejection。

同时更新 `ConformanceFixtureRunnerTests` 的 executable ability catalog allow/audit 列表，并保留既有 SFD Sigil tests 继续覆盖 SFD 六色 path。

## 4. Validation

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：passed 238/238。

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel"
```

结果：passed 486/486。

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：passed 4068/4068。

Whitespace:

```sh
git diff --check
```

结果：无输出。

## 5. Residual Risks

- 完整 `[A]` / `[C]` resource skill family 未关闭；本切片只补 Sigil family representative。
- 其他 target-bearing colored-cost activated abilities、remaining payment windows、keyword payment branches、Spellshield full-window tax、Echo costs、replacement / optional / alternative costs 仍待后续切片。
- Coverage matrix full-official 状态未升级。
- 前端运行时代码未修改，前端仍只能展示并提交服务端 `ActionPrompt` / snapshot 暴露的 source、timing、typed resource choices 与 ledger。

## 6. Verdict

4D-03T focused slice accepted. OGN Rage / Focus / Insight / Power / Discord / Unity Sigil typed payment-only resource skills 已接入服务端 prompt / command / temporary ledger / payment / audit representative path；项目仍 **NOT READY**。
