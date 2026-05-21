4D-03KV-E payment-cost Commander Ledros optional-sacrifice Spellshield/Roam targeting-stack blocker closure candidate

结论：**NOT READY / GOAL NOT COMPLETE**。本批只是 E_CARD_MATRIX_READINESS 对 4D-03KU-E 后的一枚 row-level NEEDS_ENGINE_SUPPORT blocker-count reduction，不是 fullOfficial、READY 或 4G/4H 完成。

Scope:
- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-and-targeting-stack-timing`
- selected functionalUnit: `FU-8d2d30613a`
- selected card: `OGN·231/298` / `莱卓斯指挥官`
- selected effect: `OGN_COMMANDER_LETROS_OPTIONAL_SACRIFICE_PLAY_UNIT`
- input previous closure candidate manifest: `Post03KuCardMatrixReadinessPaymentCostAlbusFerrosNoBoonCallRuneLayerTargetingStackBlockerClosureCandidateManifest`

Matrix transition:
- `NEEDS_ENGINE_SUPPORT` 219 -> 218
- primary residual 149 -> 148
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` 407 -> 406
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` 136 -> 135
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
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers `OGN·231/298` / `莱卓斯指挥官` as `OGN_COMMANDER_LETROS_OPTIONAL_SACRIFICE_PLAY_UNIT`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-commander-ledros-spirit-static.fixture.json` covers 6-cost hand play, zero-target stack resolution and source-to-base 8-power unit entry with `法盾` / `游走` / `灵体` tags.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers the target-rejection inline data for `P1-UNIT-OGN-COMMANDER-LEDROS`.
- `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record the Commander Ledros evidence anchors.

Non-closure:
- Commander Ledros automated evidence disposition remains open.
- Commander Ledros optional sacrifice / cost-reduction branch remains open.
- Commander Ledros Spellshield target-tax breadth remains open.
- Commander Ledros Roam / control-zone movement breadth remains open.
- Commander Ledros cleanup / replacement-duration breadth remains open.
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

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 580/580; Commander Ledros focused 3021/3021; adjacent prompt/payment/Spellshield/Roam/control/cleanup 2080/2080; backend full 5151/5151; git diff --check passed.
