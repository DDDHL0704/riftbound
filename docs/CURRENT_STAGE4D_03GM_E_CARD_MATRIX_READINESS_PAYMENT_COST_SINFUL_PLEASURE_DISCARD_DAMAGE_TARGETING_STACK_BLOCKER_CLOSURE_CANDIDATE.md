# 4D-03GM-E Payment-Cost Sinful Pleasure discard-damage Targeting-Stack Blocker Closure Candidate

Date: 2026-05-18

Status: **ACCEPTED FOR THIS ROW / NOT READY**

Scope: `E_CARD_MATRIX_READINESS_POST_03GL_PAYMENT_COST_SINFUL_PLEASURE_DISCARD_DAMAGE_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`

This batch records one isolated card-matrix row-level blocker reduction for `FU-87def1409d` / `OGN·008/298` / `罪恶快感` / `SINFUL_PLEASURE_DISCARD_HAND_CARD_DAMAGE_MANA_COST`.

`Post03GmCardMatrixReadinessPaymentCostSinfulPleasureDiscardDamageTargetingStackBlockerClosureCandidateManifest` takes `Post03GlCardMatrixReadinessPaymentCostMeditationDrawTargetingStackBlockerClosureCandidateManifest` as the previous closure-candidate manifest. It keeps the selected partition `bd-engine-support-payment-cost`, selected matrix row query `payment-cost`, and selected secondary row query `payment-and-targeting-stack-timing`.

Matrix transition:

```txt
snapshotEntries 1009 -> 1009
functionalUnits 811 -> 811
payment-cost functionalUnits 360 -> 360
payment-cost snapshotEntries 446 -> 446
NEEDS_ENGINE_SUPPORT 331 -> 330
primary residual 187 -> 186
payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 519 -> 518
payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 228 -> 227
NEEDS_AUTOMATED_TEST_EVIDENCE 328 -> 328
NEEDS_FAQ_REVIEW 92 -> 92
primary FAQ residual 61 -> 61
fullOfficialTrue 0 -> 0
ready false -> false
```

The selected row changes `freezeStatus` from `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED`, removes `NEEDS_ENGINE_SUPPORT` from `statusFlags`, and leaves `NEEDS_AUTOMATED_TEST_EVIDENCE` as the remaining full-official blocker.

Evidence anchors:

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sinful-pleasure-discard-damage.fixture.json`
- `docs/rules-evidence-index.md`
- `docs/p2-rules-preflight.md`
- `docs/CURRENT_P2_STATUS.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Non-closure:

```txt
payment-cost blocker closure remains partially open
B/D_ENGINE_SUPPORT payment-cost residual remains open
A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE payment-cost residual remains open
E_CARD_MATRIX_FAQ_REVIEW payment-cost residual remains open
E_CARD_MATRIX_READINESS remains open
card matrix remains open
READY remains open
```

Chrome smoke was not run because this batch does not change frontend code or browser scripts.
