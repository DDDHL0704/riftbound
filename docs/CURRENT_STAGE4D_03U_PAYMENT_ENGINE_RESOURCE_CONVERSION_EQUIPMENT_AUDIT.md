# Stage 4D-03U PaymentEngine Resource Conversion Equipment Audit

日期：2026-05-14
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

4D-03U 已验收三张 resource conversion equipment skills。该切片承接 4D-03T 后的 resource skill breadth，但只覆盖能量通道、远古簇碑、海克斯异常体三张代表卡，不关闭完整 `[A]` / `[C]` resource skill family、完整 PaymentEngine、coverage matrix full-official 或最终 READY。

## 1. Scope

本切片实现：

- `OGN·098/298` 能量通道：`{{横置}}：{{反应}}—{{获得}}{{1}}，用以支付法力费用。`
- `SFD·117/221` 远古簇碑：`{{横置}}：{{反应}}—支付任意数量的法力来{{获得}}等量的{{A}}。`
- `SFD·083/221` 海克斯异常体：`{{横置}}：{{反应}} — 支付任意数量的{{A}}来{{获得}}等量的法力。`

本切片锁定 stack-priority reaction window、controlled face-up ready base equipment source、no-target / no-stack immediate resolution，以及服务端授权的 conversion amount choices。

## 2. Implementation Facts

- `P4ActivatedAbilityCatalog` 新增三张装备的 executable resource skill definitions：
  - `ENERGY_CHANNEL_REACTION_EXHAUST_GAIN_1_MANA`
  - `ANCIENT_STELE_REACTION_PAY_MANA_GAIN_GENERIC_POWER`
  - `HEXTECH_ANOMALY_REACTION_PAY_GENERIC_POWER_GAIN_MANA`
- 三个 definition 均表达 `IsResourceSkill=true`、`ReactionSpeed=true`、`RequiresBaseEquipmentSource=true`、`ExhaustsSourceAsCost=true`、`RequiredTargetCount=0`。
- `CoreRuleEngine.ResolveActivateAbility` 对三张 conversion equipment 分派到 dedicated resolver。
- 能量通道成功时横置 source，并向 rune pool 增加 1 点 mana，不创建 ordinary stack item 或 temporary resource。
- 远古簇碑通过 `OptionalCosts` 中唯一合法的 `CONVERT_MANA_TO_GENERIC_POWER:<amount>` 选择转换数量；成功时扣除等量 mana，创建 generic temporary payment-only rune ledger。
- 海克斯异常体通过 `OptionalCosts` 中唯一合法的 `CONVERT_GENERIC_POWER_TO_MANA:<amount>` 选择转换数量；成功时只消耗普通 `RunePool.Power` 并增加等量 mana，不允许 temporary resource chaining。
- `MatchSession` prompt 为远古簇碑 / 海克斯异常体生成 1..当前可转换上限的服务端授权 choices；能量通道无 conversion choices。
- Prompt / metadata 暴露 conversion kind、choice prefix、max conversion amount、resource lifecycle、no-stack policy、requires base equipment source 与 ordinary generic power only 边界。
- Temporary payment resource snapshot / prompt restriction 对远古簇碑使用 `PAY_RUNE_COSTS_ONLY_GENERIC_TEMPORARY_LEDGER_4D_03U`。

## 3. Tests Added

新增 `tests/Riftbound.ConformanceTests/ResourceConversionEquipmentSkillTests.cs`，覆盖：

- 三张装备的 catalog definitions。
- 合法 reaction priority prompt 中暴露 source requirements、conversion metadata 与 server-defined choices。
- 能量通道成功横置 source、获得 1 mana、无 ordinary stack item。
- 远古簇碑成功扣 mana、创建 generic temporary payment-only ledger、可支付 generic rune cost。
- 远古簇碑 temporary resource 拒绝 mana-only payment 并保持 no-mutation。
- 海克斯异常体成功消耗 ordinary generic power、获得等量 mana、无 ordinary stack item。
- 缺失 / 0 / 负数 / overpay / 非法 optional cost / target / wrong timing / wrong card / exhausted source 均 rejected no-mutation。
- 海克斯异常体在只有 temporary payment resource、没有 ordinary generic power 时仍拒绝转换，证明本切片未打开 temporary resource chaining。

同时更新 `ConformanceFixtureRunnerTests` executable ability audit list。

## 4. Validation

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AncientStele|FullyQualifiedName~HextechAnomaly|FullyQualifiedName~EnergyChannel|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：passed 230/230。

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AncientStele|FullyQualifiedName~HextechAnomaly|FullyQualifiedName~EnergyChannel|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel|FullyQualifiedName~PayCost"
```

结果：passed 485/485。

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：passed 4089/4089。

Whitespace:

```sh
git diff --check
```

结果：无输出。

## 5. Residual Risks

- 完整 `[A]` / `[C]` resource skill family 未关闭；本切片只处理三张 conversion equipment。
- 海克斯异常体暂不允许 temporary payment resource chaining；该 no-go 已由负例测试固定。
- 其他 target-bearing colored-cost activated abilities、remaining payment windows、keyword payment branches、Spellshield full-window tax、Echo costs、replacement / optional / alternative costs 仍待后续切片。
- Coverage matrix full-official 状态未升级。
- 前端运行时代码未修改，前端仍只能展示并提交服务端 `ActionPrompt` / snapshot 暴露的 source、conversion choices、timing 与 ledger。

## 6. Verdict

4D-03U focused slice accepted. 能量通道、远古簇碑、海克斯异常体 resource conversion equipment skills 已接入服务端 prompt / command / rune pool / temporary ledger / payment / audit representative path；项目仍 **NOT READY**。
