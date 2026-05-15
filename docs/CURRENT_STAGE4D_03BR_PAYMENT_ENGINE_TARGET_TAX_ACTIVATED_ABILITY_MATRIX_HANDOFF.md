# Stage 4D-03BR PaymentEngine Target / Tax Activated Ability Matrix Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B DISPATCH BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03BR follows the accepted 4D-03BQ-B resource skill all-window matrix verifier.

This handoff does not implement a new verifier. It fixes the next narrow B-side boundary for the remaining P0-005 `TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` / `TARGET_TAXES` official gap: expand the current target-bearing, typed, experience and Spellshield-tax activated ability manifest into an executable matrix that can check source timing, target selection, payment quote, command revalidation, target-tax / optional branch audit and rollback parity.

This batch only changes A-side handoff / baseline / checkpoint docs. It does not change runtime behavior, tests, frontend behavior, browser smoke scripts, card matrix JSON, `fullOfficial`, READY status, P0-005 closure or `riftbound-dotnet.sln`.

## 2. Current Inputs

The current `PaymentEngineCoverageAuditTests` shape is:

- `ResidualBlockerManifest` still classifies `TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` as `covered-representative`.
- `OfficialPaymentEngineMatrixResidualManifest` still classifies `TARGET_TAXES` as `remaining-official-gap`.
- `TargetColoredActivatedAbilityCoverageManifest` currently has 8 executable ability entries selected by the catalog predicate:
  - `PAY_RED_EXHAUST_DAMAGE_3`
  - `RENATA_GLASC_PAY_1_BLUE_DRAW_1`
  - `RENATA_GLASC_PAY_4_BLUE4_EXHAUST_SCORE_1`
  - `AZIR_SWIFT_PAY_GREEN_SWAP_WITH_CONTROLLED_UNIT`
  - `GATEKEEPER_MADULI_PAY_PURPLE_MOVE_TO_WEAKER_ENEMY_BATTLEFIELD`
  - `EZREAL_PAY_BLUE_SWIFT_MOVE_TO_BASE`
  - `CRIMSON_ROSE_EXPERIENCE3_EXHAUST_READY_UNIT`
  - `SHADOW_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER`
- `SpellshieldTaxCoverageManifest` remains the target-tax anchor for Spellshield payment quote / command / audit parity.

The future verifier should stay in the `ACTIVATE_ABILITY` payment domain. It should not broaden to `PLAY_CARD`, `PAY_COST`, `ASSEMBLE_EQUIPMENT`, `TRIGGER_PAYMENT`, `BATTLEFIELD_HELD_SCORE_PAYMENT`, `HIDE_CARD`, `LEGEND_ACT` or `MOVE_UNIT` unless a later explicit dispatch reclassifies those windows for target-tax activated ability work.

## 3. Future B Scope

Future 4D-03BR-B should be a test/docs-only verifier unless it exposes a concrete mismatch.

Allowed write scope:

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_EVIDENCE.md`

Expected verifier contract:

1. Preserve the 8 ability ids from `TargetColoredActivatedAbilityCoverageManifest` and keep them exactly aligned with the catalog predicate.
2. Build a target / tax activated ability matrix over 6 dimensions for an expected 48 rows: source timing, target profile, payment profile, target-tax / optional branch profile, command-side rollback profile and official-closure / card-matrix trace profile.
3. Require every matrix row to bind ability id, `ACTIVATE_ABILITY` domain, source / target facts, prompt quote, command-side revalidation, `COST_PAID` or `ABILITY_ACTIVATED` audit expectation, rollback / no-mutation expectation, remaining official breadth and doc anchors.
4. Require Spellshield-tax rows to point back to `SpellshieldTaxCoverageManifest` and the `TARGET_TAXES` official residual axis.
5. Require typed, experience and optional target-scoped rows to keep `TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` residual blocker and related official residual axes visible without claiming closure.
6. Continue to assert `NOT READY`, P0-005 open, no `fullOfficial=true` and no READY claim.

## 4. No-Go Scope

Future 4D-03BR-B must not touch:

- runtime files under `src/**`
- frontend runtime or browser smoke scripts
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad PaymentEngine rewrites
- resource skill, keyword branch, rollback, cross-window or card-matrix verifier rewrites outside the narrow target/tax matrix
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

This handoff does not close full official target-bearing activated ability coverage. It only reserves the next executable matrix boundary so current target / tax / typed / experience representatives can be checked without promoting representative evidence to full official status.

Project status remains **NOT READY**; P0-005 remains open.
