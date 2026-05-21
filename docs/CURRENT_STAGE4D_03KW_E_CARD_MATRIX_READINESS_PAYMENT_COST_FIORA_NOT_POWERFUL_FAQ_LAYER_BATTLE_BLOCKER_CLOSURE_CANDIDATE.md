4D-03KW-E payment-cost Fiora not-powerful FAQ/layer/battle blocker closure candidate

结论：**NOT READY / GOAL NOT COMPLETE**。本批只是 E_CARD_MATRIX_READINESS 对 4D-03KV-E 后的一枚 row-level NEEDS_ENGINE_SUPPORT blocker-count reduction，不是 FAQ adjudication、fullOfficial、READY 或 4G/4H 完成。

Scope:
- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-or-targeting-stack-timing`
- selected functionalUnit: `FU-ccf4fc420e`
- selected card: `OGN·232/298` / `菲奥娜`
- selected effect: `OGN_FIORA_NOT_POWERFUL_VANILLA_PLAY_UNIT`
- input previous closure candidate manifest: `Post03KvCardMatrixReadinessPaymentCostCommanderLedrosOptionalSacrificeSpellshieldRoamTargetingStackBlockerClosureCandidateManifest`

Matrix transition:
- `NEEDS_ENGINE_SUPPORT` 218 -> 217
- primary residual 148 -> 148
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` 406 -> 405
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` 135 -> 135
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains 328
- `NEEDS_FAQ_REVIEW` residual remains 92
- primary FAQ residual remains 61
- `fullOfficialTrue` 0 -> 0
- `ready` false -> false

Selected row transition:
- `freezeStatus`: `NEEDS_FAQ_REVIEW` -> `NEEDS_FAQ_REVIEW`
- `statusFlags`: `IMPLEMENTED_UNTESTED` + `NEEDS_ENGINE_SUPPORT` + `NEEDS_FAQ_REVIEW` -> `IMPLEMENTED_UNTESTED` + `NEEDS_FAQ_REVIEW`
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT` + `NEEDS_FAQ_REVIEW` + `NEEDS_AUTOMATED_TEST_EVIDENCE` -> `NEEDS_FAQ_REVIEW` + `NEEDS_AUTOMATED_TEST_EVIDENCE`

Evidence:
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers `OGN·232/298` / `菲奥娜` as `OGN_FIORA_NOT_POWERFUL_VANILLA_PLAY_UNIT`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-fiora-not-powerful-vanilla-unit.fixture.json` covers 4-cost hand play, zero-target stack resolution and source-to-base 4-power unit entry.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-ogn-fiora-powerful-boon-keywords.fixture.json` covers a paired permanent-boon path where authoritative server state makes Fiora powerful and grants `法盾` / `游走` / `坚守`.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers direct explicit-target rejection for `P1-UNIT-OGN-FIORA`.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md` and `docs/CURRENT_P2_STATUS.md` record the current Fiora evidence anchors.

Non-closure:
- Fiora automated evidence disposition remains open.
- Fiora FAQ adjudication remains open.
- Fiora full powerful keyword / battle / layer official breadth remains open.
- Fiora one-on-one battle and combat-damage lifecycle breadth remains open.
- complete battle / spell-duel lifecycle matrix remains open.
- complete LayerEngine / continuous-effect matrix remains open.
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

Validation passed: matrix JSON valid (jq empty); 03KW/03KV active-goal guard 6/6; PaymentEngineCoverageAuditTests 580/580; Fiora focused 3042/3042; adjacent prompt/payment/FAQ/layer/battle/damage/spell-duel/target/stack/boon/power 2702/2702; backend full 5151/5151; git diff --check passed.
