4D-03KT-E payment-cost Ghost Matron spirit-revive hidden/targeting-stack blocker closure candidate

结论：**NOT READY / GOAL NOT COMPLETE**。本批只是 E_CARD_MATRIX_READINESS 对 4D-03KS-E 后的一枚 row-level NEEDS_ENGINE_SUPPORT blocker-count reduction，不是 fullOfficial、READY 或 4G/4H 完成。

Scope:
- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-and-targeting-stack-timing`
- selected functionalUnit: `FU-ab6836e360`
- selected card: `OGN·226/298` / `幽灵主母`
- selected effect: `GHOST_MATRON_SPIRIT_REVIVE_PLAY_UNIT`
- input previous closure candidate manifest: `Post03KsCardMatrixReadinessPaymentCostColdBloodedAristocratDestroyFriendlyUnitFaqCleanupBlockerClosureCandidateManifest`

Matrix transition:
- `NEEDS_ENGINE_SUPPORT` 221 -> 220
- primary residual 151 -> 150
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` 409 -> 408
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` 138 -> 137
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains 328
- `NEEDS_FAQ_REVIEW` residual remains 92
- primary FAQ residual remains 61
- `fullOfficialTrue` 0 -> 0
- `ready` false -> false

Selected row transition:
- `freezeStatus`: `NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED`
- `statusFlags`: `IMPLEMENTED_UNTESTED` + `NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED`
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT` + `NEEDS_AUTOMATED_TEST_EVIDENCE` -> `NEEDS_AUTOMATED_TEST_EVIDENCE`

Evidence:
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers `OGN·226/298` / `幽灵主母` as `GHOST_MATRON_SPIRIT_REVIVE_PLAY_UNIT`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ghost-matron-spirit-revive-static.fixture.json` covers 4-cost hand play, no graveyard target, zero-target stack resolution and Spirit unit entry to base.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ghost-matron-graveyard-unit-base.fixture.json` covers optional friendly graveyard low-cost unit target and play-to-base path.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-ghost-matron-target-rejected.fixture.json` covers illegal non-graveyard target rejection and high-cost / high-current-A target rejection.
- `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record the Ghost Matron evidence anchors.

Non-closure:
- Ghost Matron automated evidence disposition remains open.
- Ghost Matron hidden-info / random-zone breadth remains open.
- Ghost Matron optional graveyard-unit play breadth remains open.
- complete FEPR target / stack lifecycle matrix remains open.
- complete PaymentEngine / PAY_COST matrix remains open.
- fullOfficial remains false.
- READY remains open.

Locked scope:
- No runtime change.
- No frontend change.
- No Chrome/browser script change.
- No official catalog change.
- No protocol core field change.
- No final readiness flag change.
- `riftbound-dotnet.sln` remains untracked and excluded.

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 578/578; Ghost Matron focused 3021/3021; adjacent prompt/payment/hidden/targeting-stack 1920/1920; backend full 5149/5149; git diff --check passed.
