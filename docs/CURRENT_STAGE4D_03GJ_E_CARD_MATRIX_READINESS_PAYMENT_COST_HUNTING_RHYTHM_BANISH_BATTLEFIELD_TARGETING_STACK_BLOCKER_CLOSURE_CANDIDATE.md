# 4D-03GJ-E Payment-Cost Hunting Rhythm Banish Battlefield Targeting-Stack Blocker Closure Candidate

Date: 2026-05-18

Status: **ACCEPTED FOR THIS ROW / NOT READY**

Scope: `E_CARD_MATRIX_READINESS_POST_03GI_PAYMENT_COST_HUNTING_RHYTHM_BANISH_BATTLEFIELD_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`

This batch records one isolated card-matrix row-level blocker reduction for `FU-d3afd3a6db` / `UNL-184/219` / `狩猎律动` / `HUNTING_RHYTHM_BANISH_FRIENDLY_UNIT_PLAY_TO_BATTLEFIELD`.

`Post03GjCardMatrixReadinessPaymentCostHuntingRhythmBanishBattlefieldTargetingStackBlockerClosureCandidateManifest` takes `Post03GiCardMatrixReadinessPaymentCostLuxSpellOnlyResourceTargetingStackBlockerClosureCandidateManifest` as the previous closure-candidate manifest. It keeps the selected partition `bd-engine-support-payment-cost`, selected matrix row query `payment-cost`, and selected secondary row query `payment-and-targeting-stack-timing`.

Matrix transition:

```txt
snapshotEntries 1009 -> 1009
functionalUnits 811 -> 811
payment-cost functionalUnits 360 -> 360
payment-cost snapshotEntries 446 -> 446
NEEDS_ENGINE_SUPPORT 334 -> 333
primary residual 190 -> 189
payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 522 -> 521
payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 231 -> 230
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
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-hunting-rhythm-banish-play-battlefield.fixture.json`
- `docs/rules-evidence-index.md`
- `docs/p2-rules-preflight.md`
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
