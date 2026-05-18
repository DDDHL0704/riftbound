# 4D-03GO-E Payment-Cost Mysterious Weapon equipment Targeting-Stack Blocker Closure Candidate

Date: 2026-05-18

Status: **ACCEPTED FOR THIS ROW / NOT READY**

Scope: `E_CARD_MATRIX_READINESS_POST_03GN_PAYMENT_COST_MYSTERIOUS_WEAPON_EQUIPMENT_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`

This batch records one isolated card-matrix row-level blocker reduction for `FU-9aaacc7bdb` / `OGN·023/298` / `来路不明的武器` / `MYSTERIOUS_WEAPON_PLAY_EQUIPMENT`.

`Post03GoCardMatrixReadinessPaymentCostMysteriousWeaponEquipmentTargetingStackBlockerClosureCandidateManifest` takes `Post03GnCardMatrixReadinessPaymentCostThunderingSkyCostReducedDamageTargetingStackBlockerClosureCandidateManifest` as the previous closure-candidate manifest. It keeps the selected partition `bd-engine-support-payment-cost`, selected matrix row query `payment-cost`, and selected secondary row query `payment-and-targeting-stack-timing`.

Matrix transition:

```txt
snapshotEntries 1009 -> 1009
functionalUnits 811 -> 811
payment-cost functionalUnits 360 -> 360
payment-cost snapshotEntries 446 -> 446
NEEDS_ENGINE_SUPPORT 329 -> 328
primary residual 185 -> 184
payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 517 -> 516
payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 226 -> 225
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
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-mysterious-weapon-equipment.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-mysterious-weapon-target-rejected.fixture.json`
- `docs/rules-evidence-index.md`
- `docs/p2-rules-preflight.md`
- `docs/CURRENT_P4_STATUS.md`
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
