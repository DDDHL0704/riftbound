# Stage 4D-03CL PaymentEngine Legend Resource Bridge Acceptance Verifier Evidence

Audit date: 2026-05-16
Conclusion: **EVIDENCE RECORDED / PROJECT NOT READY**

## 1. Repository Facts

- Branch: `main`.
- Latest commit before this batch: `48dee284 docs: hand off legend bridge implementation`.
- Expected untracked file: `riftbound-dotnet.sln`.
- Active goal remains **NOT READY**.

## 2. Changed Scope

Changed test file:

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`

New audit artifacts:

- `docs/CURRENT_STAGE4D_03CL_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_ACCEPTANCE_VERIFIER_AUDIT.md`
- `docs/CURRENT_STAGE4D_03CL_PAYMENT_ENGINE_LEGEND_RESOURCE_BRIDGE_ACCEPTANCE_VERIFIER_EVIDENCE.md`

The test-only addition introduces `LegendResourceBridgeImplementationAcceptanceManifest` and three focused assertions. It does not modify runtime, frontend, browser scripts, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 3. Evidence Facts

The acceptance manifest inherits the exact 4D-03CJ aggregate inputs:

- Diana: `UNL-197/219`, `LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA`.
- Ornn: `SFD·189/221`, `SFD·244/221`, `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT`.
- KaiSa: `OGN·247/298`, `OGN·299/298`, `OGN·299*/298`, `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL`.
- Darius: `OGN·253/298`, `OGN·302/298`, `OGN·302*/298`, `LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA`.

Each group now has executable acceptance-contract text for:

- server-filtered `ActionPrompt` legality with no frontend inference;
- command-side revalidation of ability id, source object, source-card group and timing / pending item;
- generated-resource audit, amount, type, lifetime, consumption and cleanup;
- wrong timing / stale pending item / missing previous card / stale source / exhausted source / handwritten illegal command rollback as applicable;
- source-card parity for grouped Ornn, KaiSa and Darius source cards;
- generated resource skills cannot be targeted as responses by other spells.

## 4. Validation Results

```text
PaymentEngineCoverageAuditTests: passed 133/133
Adjacent PaymentEngine / resource skill / prompt / hub regression: passed 691/691
Backend full: passed 4570/4570
git diff --check: passed after final 4D-03CL doc sync
```

## 5. Remaining Open Work

- Future B dispatch is still required before implementing or accepting any legend bridge runtime / verifier closure.
- Future B evidence must prove real prompt / command / audit / lifetime / rollback behavior, not only this acceptance-contract manifest.
- Non-legend resource-skill runtime lanes remain separate from this legend bridge acceptance contract.
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` remains full-official incomplete.
- P0/P1 clearing, final frontend reruns, full-card matrix and final completion audit READY remain open.
