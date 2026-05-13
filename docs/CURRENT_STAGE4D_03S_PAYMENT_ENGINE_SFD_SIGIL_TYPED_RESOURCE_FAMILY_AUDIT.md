# Stage 4D-03S PaymentEngine SFD Sigil Typed Resource Family Audit

日期：2026-05-14
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

4D-03S 已验收剩余五张 SFD Sigil typed payment-only resource skills。该切片继续收窄 P0-005，但不关闭完整 PaymentEngine、完整 Sigil family、OGN Sigil resource skills、完整 `[A]` / `[C]` resource skill family、P1 LayerEngine、coverage matrix full-official 或最终 READY。

## 1. Scope

本切片实现：

- `SFD·226/221` 专注之印 / Focus Sigil：`{{横置}}：{{反应}}—{{获得}}{{绿色}}，用以支付符能费用。`
- `SFD·229/221` 洞察之印 / Insight Sigil：`{{横置}}：{{反应}}—{{获得}}{{蓝色}}，用以支付符能费用。`
- `SFD·231/221` 力量之印 / Power Sigil：`{{横置}}：{{反应}}—{{获得}}{{橙色}}，用以支付符能费用。`
- `SFD·234/221` 不和之印 / Discord Sigil：`{{横置}}：{{反应}}—{{获得}}{{紫色}}，用以支付符能费用。`
- `SFD·238/221` 团结之印 / Unity Sigil：`{{横置}}：{{反应}}—{{获得}}{{黄色}}，用以支付符能费用。`

本切片复用 4D-03R Rage Sigil typed red resource path，将 Sigil typed resource metadata 参数化为 SFD Sigil profile/helper。OGN `OGN·081/298` / `OGN·120/298` / `OGN·163/298` / `OGN·204/298` / `OGN·245/298` 仍只保留普通装备打出证据，resource skills deferred。

## 2. Implementation Facts

- `P4ActivatedAbilityCatalog` 新增 `P4SigilTypedResourceProfile` 与 SFD Sigil typed resource profiles，覆盖 Rage + Focus / Insight / Power / Discord / Unity 六张 SFD Sigil。
- 新增五个 ability ids：
  - `FOCUS_SIGIL_REACTION_EXHAUST_GAIN_1_GREEN_POWER`
  - `INSIGHT_SIGIL_REACTION_EXHAUST_GAIN_1_BLUE_POWER`
  - `POWER_SIGIL_REACTION_EXHAUST_GAIN_1_ORANGE_POWER`
  - `DISCORD_SIGIL_REACTION_EXHAUST_GAIN_1_PURPLE_POWER`
  - `UNITY_SIGIL_REACTION_EXHAUST_GAIN_1_YELLOW_POWER`
- 每个 definition 都表达 `IsResourceSkill=true`、`ReactionSpeed=true`、`PaymentOnlyResource=true`、`RequiresBaseEquipmentSource=true`、`ExhaustsSourceAsCost=true`、`RequiredTargetCount=0`、`GeneratedPowerByTrait.<trait>=1`。
- `CoreRuleEngine.ResolveActivateAbility` 现在通过 `IsSfdSigilTypedResourceAbility` 分派到共享 `ResolveSfdSigilTypedResourceSkill`。
- 共享 resolver 校验 stack-priority reaction timing、当前玩家控制、公开正面、未横置、controller base equipment source、正确 cardNo、无 targets / optional costs / payment resource actions。
- 成功命令横置 source，创建对应颜色 typed temporary payment-only resource ledger，不创建 ordinary stack item，不改变普通 runePool mana/power。
- `MatchSession` prompt / snapshot restriction 和 display name 改为根据 Sigil profile 输出，prompt metadata 包含 typed resource、reaction policy、no-stack policy、resource lifecycle、allowed payment kind 与 generatedPowerByTrait。
- 既有 temporary payment resource consumption / cleanup 口径复用 4D-03R 路径：同色 typed cost 与 generic `A` rune cost 可支付，wrong color / mana-only rejected no-mutation。

## 3. Tests Added

新增 `tests/Riftbound.ConformanceTests/SfdSigilResourceSkillTests.cs`，覆盖：

- 五张剩余 SFD Sigil catalog definitions。
- 合法 reaction priority prompt 中暴露 SFD Sigil typed resource source requirements。
- 五色 activation 成功横置 source、创建 typed temporary ledger、无普通 stack item。
- 五色 temporary resource 支付同色 typed rune cost 与 generic rune cost，并清理 ledger。
- 五色 temporary resource 拒绝 wrong-color typed cost 与 mana-only cost，保持 no-mutation。
- OGN Sigils 在本切片仍不暴露 executable resource skill prompt。

同时更新 `ConformanceFixtureRunnerTests` 的 executable ability catalog allow/audit 列表。

## 4. Validation

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：passed 213/213。

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel"
```

结果：passed 461/461。

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：passed 4043/4043。

Whitespace:

```sh
git diff --check
```

结果：无输出。

## 5. Residual Risks

- OGN Sigil resource skills 未实现；本切片只锁 SFD typed resource family。
- 完整 `[A]` / `[C]` resource skill family 未关闭。
- 其他 target-bearing colored-cost activated abilities、remaining payment windows、keyword payment branches、Spellshield full-window tax、Echo costs、replacement / optional / alternative costs 仍待后续切片。
- Coverage matrix full-official 状态未升级。
- 前端运行时代码未修改，前端仍只能展示并提交服务端 `ActionPrompt` / snapshot 暴露的 source、timing、typed resource choices 与 ledger。

## 6. Verdict

4D-03S focused slice accepted. SFD Focus / Insight / Power / Discord / Unity Sigil typed payment-only resource skills 已接入服务端 prompt / command / temporary ledger / payment / audit representative path；项目仍 **NOT READY**。
