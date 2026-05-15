# Stage 4D-03BP-B PaymentEngine Keyword Branch All-Window Matrix Audit

Audit date: 2026-05-16
Conclusion: **TEST-ONLY VERIFIER IMPLEMENTED / PROJECT NOT READY**

## 1. Scope

4D-03BP-B implements the verifier reserved by the 4D-03BP handoff. It does not change runtime behavior, frontend behavior, browser smoke scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

The verifier expands the 4D-03AY `KeywordPaymentBranchManifest` into an all-window matrix:

- 8 keyword payment branch entries
- 6 current PaymentEngine payment surfaces
- 48 generated matrix rows

## 2. Verifier Contract

`PaymentEngineCoverageAuditTests` now includes `KeywordPaymentBranchAllWindowMatrixManifest`.

Each matrix row binds:

- action window
- keyword branch id
- `KEYWORD_PAYMENT_BRANCHES` residual blocker
- `KEYWORD_BRANCHES` / `COST_MODIFIERS` / `OPTIONAL_EXTRA_ALTERNATIVE_COSTS` / `REPLACEMENT_PREVENTION` official residual axes
- prompt quote anchor
- command-side revalidation anchor
- `COST_PAID` audit expectation
- rollback / no-mutation expectation
- remaining official breadth
- doc anchors

The matrix intentionally excludes `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` from the current keyword payment all-window surfaces.

## 3. Rows

Required action windows:

- `PLAY_CARD`
- `PAY_COST`
- `ACTIVATE_ABILITY`
- `ASSEMBLE_EQUIPMENT`
- `TRIGGER_PAYMENT`
- `BATTLEFIELD_HELD_SCORE_PAYMENT`

Required keyword branches:

- `HASTE_READY`
- `ECHO_OPTIONAL_PAYMENT`
- `SPELLSHIELD_TARGET_TAX`
- `EXPERIENCE_PAYMENT`
- `BATTLEFIELD_REPLACEMENT_COSTS`
- `COST_MODIFIER_PAYMENTS`
- `OPTIONAL_EXTRA_ALTERNATIVE_COSTS`
- `TEMPORARY_RESOURCE_PARITY`

Expected row count: 48.

## 4. Remaining Risk

This verifier proves that current keyword payment branch representative evidence is routed into an executable all-window contract. It does not prove full official keyword payment parity across every card row, cost modifier stack, target/tax branch, replacement/prevention ordering, optional/extra/alternative branch, or temporary-resource lifetime.

P0-005 remains open. Project status remains **NOT READY**.
