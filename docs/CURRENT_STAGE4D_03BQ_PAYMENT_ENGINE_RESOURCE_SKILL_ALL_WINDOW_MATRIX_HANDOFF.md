# Stage 4D-03BQ PaymentEngine Resource Skill All-Window Matrix Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B DISPATCH BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03BQ follows the accepted 4D-03BP-B keyword branch all-window matrix verifier.

This handoff does not implement a new verifier. It fixes the next narrow B-side boundary for the remaining P0-005 `RESOURCE_SKILL_A_C_FAMILY` / `RESOURCE_SKILLS` official gap: expand the current resource skill coverage manifest into an all-window executable matrix that can check prompt, command, audit, generated-resource lifetime and rollback parity across the current PaymentEngine payment surfaces.

This batch only changes A-side handoff / baseline / checkpoint docs. It does not change runtime behavior, tests, frontend behavior, browser smoke scripts, card matrix JSON, `fullOfficial`, READY status, P0-005 closure or `riftbound-dotnet.sln`.

## 2. Current Inputs

The current `PaymentEngineCoverageAuditTests` shape is:

- `ResidualBlockerManifest` still classifies `RESOURCE_SKILL_A_C_FAMILY` as `catalog-bound-representative`
- `OfficialPaymentEngineMatrixResidualManifest` still classifies `RESOURCE_SKILLS` as `remaining-official-gap`
- `ResourceSkillCoverageManifest` currently has 6 executable family-level entries and 19 catalog `IsResourceSkill=true` ability ids:
  - Malzahar target-as-cost payment-only resource skill
  - Dragon Soul Sage reaction mana resource skill
  - SFD Sigil typed payment-only resource skill family
  - OGN Sigil typed payment-only resource skill family
  - Resource conversion equipment skill family
  - Gold token payment-only resource skill family

Current downstream PaymentEngine payment surfaces for all-window matrices are:

- `PLAY_CARD`
- `PAY_COST`
- `ACTIVATE_ABILITY`
- `ASSEMBLE_EQUIPMENT`
- `TRIGGER_PAYMENT`
- `BATTLEFIELD_HELD_SCORE_PAYMENT`

## 3. Future B Scope

Future 4D-03BQ-B should be a test/docs-only verifier unless it exposes a concrete mismatch.

Allowed write scope:

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_EVIDENCE.md`

Expected verifier contract:

1. Preserve the 6 family entries and 19 catalog ability ids from `ResourceSkillCoverageManifest`.
2. Build an all-window matrix over the 6 current PaymentEngine payment surfaces, for an expected 36 family-window rows.
3. Require every matrix row to bind action window, resource-skill family, generated resource kind, prompt quote, command-side revalidation, `ABILITY_ACTIVATED` / generated-resource audit expectation, rollback / no-mutation expectation, remaining official breadth and doc anchors.
4. Require every matrix row to point back to the matching `RESOURCE_SKILL_A_C_FAMILY` residual blocker and the `RESOURCE_SKILLS` official residual axis.
5. Keep `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` outside the current resource-skill all-window matrix unless a future explicit policy change reclassifies them.
6. Continue to assert `NOT READY`, P0-005 open, no `fullOfficial=true` and no READY claim.

## 4. No-Go Scope

Future 4D-03BQ-B must not touch:

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

This handoff does not close full official resource skill coverage. It only reserves the next executable matrix boundary so current resource skill representatives can be checked against all current PaymentEngine payment surfaces without promoting catalog-bound representative evidence to full official status.

Project status remains **NOT READY**; P0-005 remains open.
