# 4D-03FR-BD Payment-Cost Echo-Ready Automated Evidence

日期：2026-06-26
结论：**FOCUSED GREEN / PROJECT NOT READY**

This evidence records the focused automated coverage added for `UNL-009/219 大幕渐起` / `THE_CURTAIN_RISES_READY_UNIT`.

## Files

- `tests/Riftbound.ConformanceTests/TheCurtainRisesPaymentCostTests.cs`
- `docs/CURRENT_STAGE4D_03FR_BD_PAYMENT_COST_ECHO_READY_AUTOMATED_EVIDENCE_AUDIT.md`

## Regression

Focused command:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TheCurtainRisesPaymentCostTests" --nologo
```

Result:

```text
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4
```

Additional validation:

- adjacent TheCurtainRises / AnyUnit / PaymentEngine representatives: 113/113 passed.
- MatchRecovery hidden-information representative: 1989/1989 passed.
- backend full conformance: 8704/8704 passed.

## Coverage

- Prompt exposes the legal play source, target choice, and `ECHO` optional cost.
- Base play pays 2 mana, adds a stack item, and pass-pass readies the target.
- ECHO play pays 4 total mana, records `optionalCosts=["ECHO"]`, adds a repeat-count-2 stack item, and pass-pass readies the target.
- Insufficient ECHO mana rejects without events, stack mutation, hand movement, rune pool mutation, or pending payment state.

## Remaining Scope

This focused evidence upgrades the selected 03FR row from candidate-only coverage to direct automated evidence. It remains one representative row; full payment-cost breadth, full targeting-stack timing, FAQ disposition, matrix write readiness, full official PaymentEngine closure and READY remain open.
