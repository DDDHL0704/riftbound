# Stage 4D-03BR PaymentEngine Target / Tax Activated Ability Matrix Audit

Audit date: 2026-05-16
Conclusion: **IMPLEMENTED VERIFIER / A-VALIDATED / PROJECT NOT READY**

## 1. Scope

4D-03BR-B implements the test/docs-only verifier reserved by `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_HANDOFF.md`.

The verifier stays inside `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`. It does not change runtime behavior, frontend behavior, browser smoke scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Verifier Contract

The new `TargetTaxActivatedAbilityMatrixManifest` expands `TargetColoredActivatedAbilityCoverageManifest` into a 48-row matrix:

- 8 current target-bearing / typed / experience / Spellshield-tax activated ability entries.
- 6 matrix dimensions: source timing, target profile, payment profile, target-tax or optional branch, command rollback and official closure / card-matrix trace.
- Every row remains in the `ACTIVATE_ABILITY` payment domain.
- Every row preserves prompt quote, command-side revalidation, `COST_PAID` or `ABILITY_ACTIVATED` audit expectation, rollback / no-mutation expectation, remaining official breadth and doc anchors.
- Every row links back to `TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` and `TARGET_TAXES`.
- Spellshield-tax rows link back to `SpellshieldTaxCoverageManifest`.

## 3. Focused Guards

The new focused guards require:

1. Exactly 8 abilities x 6 dimensions = 48 rows.
2. No non-`ACTIVATE_ABILITY` action window enters the target/tax activated ability matrix.
3. Every row has source timing, target profile, payment profile, target-tax / optional branch, prompt, command, audit, rollback, official closure and doc-anchor fields.
4. Spellshield-tax ability rows explicitly reference `SpellshieldTaxCoverageManifest`.
5. Residual axes and closure text keep P0-005 open and project status **NOT READY**.

## 4. Validation

Validation is recorded in `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_EVIDENCE.md`.

Current A-side validation:

- Focused PaymentEngine coverage guard: passed 107/107
- Adjacent PaymentEngine / resource skill / prompt / hub regression: passed 665/665
- Backend full: passed 4544/4544
- `git diff --check`: passed

## 5. Remaining Risk

This verifier does not make target/tax activated abilities full official. It only converts the current representative target-bearing / typed / experience / Spellshield-tax activated ability manifest into an executable matrix so future drift is visible.

P0-005, P1, frontend final validation, full-card matrix and READY remain open.
