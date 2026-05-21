4D-03KU-E payment-cost Albus Ferros no-boon call-rune layer/targeting-stack blocker closure candidate

结论：**NOT READY / GOAL NOT COMPLETE**。本批只是 E_CARD_MATRIX_READINESS 对 4D-03KT-E 后的一枚 row-level NEEDS_ENGINE_SUPPORT blocker-count reduction，不是 fullOfficial、READY 或 4G/4H 完成。

Scope:
- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-and-targeting-stack-timing`
- selected functionalUnit: `FU-cead48a12d`
- selected card: `OGN·230/298` / `阿不思·菲罗斯`
- selected effect: `ALBUS_FERROS_NO_BOON_CALL_RUNE_PLAY_UNIT`
- input previous closure candidate manifest: `Post03KtCardMatrixReadinessPaymentCostGhostMatronSpiritReviveHiddenTargetingStackBlockerClosureCandidateManifest`

Matrix transition:
- `NEEDS_ENGINE_SUPPORT` 220 -> 219
- primary residual 150 -> 149
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` 408 -> 407
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` 137 -> 136
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
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers `OGN·230/298` / `阿不思·菲罗斯` as `ALBUS_FERROS_NO_BOON_CALL_RUNE_PLAY_UNIT`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-albus-ferros-no-boon-call-rune-static.fixture.json` covers 4-cost hand play, zero-target stack resolution and source-to-base 3-power unit entry.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-albus-ferros-target-rejected.fixture.json` covers explicit-target rejection before payment, stack creation or unit entry.
- `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record the Albus Ferros evidence anchors.

Non-closure:
- Albus Ferros automated evidence disposition remains open.
- Albus Ferros boon-consuming dormant-rune call branch remains open.
- Albus Ferros layer / continuous-effect breadth remains open.
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

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 580/580; Albus Ferros focused 3021/3021; adjacent prompt/payment/layer/targeting-stack 1976/1976; backend full 5151/5151; git diff --check passed.
