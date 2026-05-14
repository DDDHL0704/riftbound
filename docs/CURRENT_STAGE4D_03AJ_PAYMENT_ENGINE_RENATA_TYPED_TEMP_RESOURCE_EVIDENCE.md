# Stage 4D-03AJ PaymentEngine Renata Typed Temporary Resource Evidence

日期：2026-05-15

状态：**accepted / project NOT READY**

## Changed Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/RenataActivatedAbilityTests.cs`

## Commands

### Focused

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RenataActivatedAbilityTests|FullyQualifiedName~PaymentEngineUnificationTests"
```

结果：85/85 通过。

### Adjacent

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RenataActivatedAbilityTests|FullyQualifiedName~OgnSigilResourceSkillTests|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Spellshield|FullyQualifiedName~Haste|FullyQualifiedName~Echo|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~ResourceSkill|FullyQualifiedName~RunePool|FullyQualifiedName~GameHub"
```

结果：687/687 通过。

### Backend Full

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：4239/4239 通过。

## Acceptance Notes

- Prompt 侧：Renata draw typed-blue shortfall 现在 exposes matching `INSIGHT_SIGIL` blue temporary resource in `paymentResourceChoices` and `paymentResourcePowerByChoice`。
- Command 侧：Renata draw / score 均可通过 matching blue `TEMP_PAYMENT_RESOURCE:*` 支付 typed blue shortfall。
- Audit 侧：temporary resource 消费事件与 `COST_PAID` typed consumption metadata 均可断言。
- Generic temporary resource 仍不能支付 Renata typed-blue cost。

## Remaining

P0-005 仍未关闭；remaining work 仍包括完整 `[A]` / `[C]` resource skill family、remaining payment windows、target-bearing activated ability breadth、keyword payment branches 与 replacement / optional / alternative / tax quote-command-audit parity。
