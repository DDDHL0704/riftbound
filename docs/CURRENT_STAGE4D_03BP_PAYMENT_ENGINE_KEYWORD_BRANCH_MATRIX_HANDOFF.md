# Stage 4D-03BP PaymentEngine Keyword Branch All-Window Matrix Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B DISPATCH BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03BP follows the accepted 4D-03BO-B official matrix downstream aggregate verifier.

This handoff does not implement a new verifier. It fixes the next narrow B-side boundary for the remaining P0-005 `KEYWORD_PAYMENT_BRANCHES` official gap: expand the 4D-03AY keyword payment branch manifest into an all-window executable matrix that can check quote, command, audit and rollback parity across the current PaymentEngine payment surfaces.

This batch only changes A-side handoff / baseline / checkpoint docs. It does not change runtime behavior, tests, frontend behavior, browser smoke scripts, card matrix JSON, `fullOfficial`, READY status, P0-005 closure or `riftbound-dotnet.sln`.

## 2. Current Inputs

The current `PaymentEngineCoverageAuditTests` shape is:

- `ResidualBlockerManifest` still classifies `KEYWORD_PAYMENT_BRANCHES` as `remaining-official-gap`
- `OfficialPaymentEngineMatrixResidualManifest` still classifies `KEYWORD_BRANCHES`, `COST_MODIFIERS`, `OPTIONAL_EXTRA_ALTERNATIVE_COSTS` and `REPLACEMENT_PREVENTION` as `remaining-official-gap`
- `KeywordPaymentBranchManifest` currently has 8 executable branch-level entries:
  - `HASTE_READY`
  - `ECHO_OPTIONAL_PAYMENT`
  - `SPELLSHIELD_TARGET_TAX`
  - `EXPERIENCE_PAYMENT`
  - `BATTLEFIELD_REPLACEMENT_COSTS`
  - `COST_MODIFIER_PAYMENTS`
  - `OPTIONAL_EXTRA_ALTERNATIVE_COSTS`
  - `TEMPORARY_RESOURCE_PARITY`

Current downstream PaymentEngine payment surfaces for all-window matrices are:

- `PLAY_CARD`
- `PAY_COST`
- `ACTIVATE_ABILITY`
- `ASSEMBLE_EQUIPMENT`
- `TRIGGER_PAYMENT`
- `BATTLEFIELD_HELD_SCORE_PAYMENT`

## 3. Future B Scope

Future 4D-03BP-B should be a test/docs-only verifier unless it exposes a concrete mismatch.

Allowed write scope:

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_EVIDENCE.md`

Expected verifier contract:

1. Preserve the 8 branch ids from `KeywordPaymentBranchManifest`.
2. Build an all-window matrix over the 6 current PaymentEngine payment surfaces, for an expected 48 rows.
3. Require every matrix row to bind action window, branch id, prompt quote, command-side revalidation, `COST_PAID` or domain audit expectation, rollback / no-mutation expectation, remaining official breadth and doc anchors.
4. Require every matrix row to point back to the matching `KEYWORD_PAYMENT_BRANCHES` residual blocker and relevant `OfficialPaymentEngineMatrixResidualManifest` axes.
5. Keep `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` outside the current keyword payment all-window matrix.
6. Continue to assert `NOT READY`, P0-005 open, no `fullOfficial=true` and no READY claim.

## 4. No-Go Scope

Future 4D-03BP-B must not touch:

- runtime files under `src/**`
- frontend runtime or browser smoke scripts
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad PaymentEngine rewrites
- battle lifecycle / cleanup queues
- LayerEngine or P1 keyword implementation
- `fullOfficial` / READY status
- `riftbound-dotnet.sln`

## 5. Required Validation

Future B-side focused validation:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
git diff --check
```

A-side acceptance validation after B returns:

```sh
source scripts/dev-env.sh
dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Remaining Risk

This handoff does not close full official keyword payment coverage. It only reserves the next executable matrix boundary so existing keyword branch representatives can be checked against all current PaymentEngine payment surfaces without promoting representative evidence to full official status.

Project status remains **NOT READY**; P0-005 remains open.
