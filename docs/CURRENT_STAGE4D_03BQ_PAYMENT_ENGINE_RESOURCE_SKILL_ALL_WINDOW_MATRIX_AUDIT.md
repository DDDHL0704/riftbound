# Stage 4D-03BQ-B PaymentEngine Resource Skill All-Window Matrix Audit

Audit date: 2026-05-16
Conclusion: **FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

## 1. Scope

4D-03BQ-B follows the 4D-03BQ handoff / baseline and implements the reserved resource skill all-window matrix verifier.

This is a test/docs-only slice. It does not modify runtime behavior, frontend behavior, browser smoke scripts, card matrix JSON, `fullOfficial`, READY status, P0-005 closure or `riftbound-dotnet.sln`.

## 2. Implemented Verifier

`PaymentEngineCoverageAuditTests.cs` now adds `ResourceSkillAllWindowMatrixManifest`, expanding the current 4D-03AZ resource skill representative set across the same 6 current PaymentEngine payment surfaces used by recent all-window matrices.

Matrix inputs:

- 6 `ResourceSkillCoverageManifest` family entries
- 19 current catalog `IsResourceSkill=true` ability ids
- 6 current PaymentEngine payment surfaces:
  - `PLAY_CARD`
  - `PAY_COST`
  - `ACTIVATE_ABILITY`
  - `ASSEMBLE_EQUIPMENT`
  - `TRIGGER_PAYMENT`
  - `BATTLEFIELD_HELD_SCORE_PAYMENT`

Expected matrix size: **36 family-window rows**.

Each row binds:

- action window
- resource skill family
- family ability ids
- generated resource / payment-only lifecycle scope
- prompt quote
- command-side revalidation
- `ABILITY_ACTIVATED` / generated-resource audit expectation
- rollback / no-mutation expectation
- `RESOURCE_SKILL_A_C_FAMILY` residual blocker
- `RESOURCE_SKILLS` official residual axis
- 03BQ / 03AZ / family / action-window doc anchors

## 3. No-Go Confirmation

This slice did not touch:

- `src/**`
- frontend runtime
- browser smoke scripts
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad PaymentEngine runtime
- battle lifecycle / cleanup queues
- LayerEngine / P1 keyword implementation
- `fullOfficial` / READY status
- `riftbound-dotnet.sln`

## 4. Validation

Validation is recorded in `docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_EVIDENCE.md`.

## 5. Remaining Risk

This verifier does not prove full official `[A]` / `[C]` resource skill coverage. It only makes the current catalog-bound representative families executable across the current PaymentEngine payment surfaces.

P0-005, P1, frontend final validation, full-card matrix and READY remain open. Project status remains **NOT READY**.
