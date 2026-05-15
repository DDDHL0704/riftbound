# Stage 4D-03AP Rek'Sai HASTE_READY Red Exactness Evidence

日期：2026-05-15
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This evidence record accompanies `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_AUDIT.md`.

## 1. Implemented Evidence Surface

`tests/Riftbound.ConformanceTests/ReksaiHasteReadyRedPaymentTests.cs` records the accepted evidence for `SFD·029/221` and `SFD·029a/221`.

Covered server-authoritative surfaces:

- `CardBehaviorRegistry` / `CardPermissionKeywordRules.BuildProfile` typed red HASTE_READY profile.
- `ResolutionResult.BuildPrompts` `PLAY_CARD` source requirement and payment-resource choices.
- `CoreRuleEngine.ResolveAsync` command acceptance for existing red power.
- Shared `PaymentCostRules` cost audit payload through `COST_PAID`.
- Red rune recycle through `RUNE_RECYCLED`, `POWER_GAINED`, `paymentResourceActions` and `recycledRuneObjectIds`.
- Stack resolution into base with active entry and `hasteReadyOptionalCostPaid=true`.
- No-mutation rejection by whole-state hash for wrong trait, generic temporary resource, insufficient red, bad recycle, unsupported optional and target bypass attempts.

## 2. Command Evidence

Focused:

```txt
FullyQualifiedName~ReksaiHasteReadyRedPaymentTests
17/17 passed
```

Focused adjacent:

```txt
FullyQualifiedName~ReksaiHasteReady|FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine
126/126 passed
```

Broader adjacent:

```txt
FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PlayCard|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority
442/442 passed
```

Backend full:

```txt
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
4338/4338 passed
```

Diff hygiene:

```txt
git diff --check
passed
```

## 3. Evidence Boundary

This is representative red payment exactness evidence only. It does not implement or prove:

- strong / Overwhelm battle power modifier.
- damage overflow or full `ASSIGN_COMBAT_DAMAGE`.
- non-hand friendly haste granting.
- LayerEngine / cleanup breadth.
- FAQ adjudication.
- 1009 / 811 full-card matrix completion.
- frontend final validation or READY.
