4D-03KX-E payment-cost Darius legend-resource bridge blocker closure candidate

结论：**NOT READY / GOAL NOT COMPLETE**。本批只是 E_CARD_MATRIX_READINESS 对 4D-03KW-E 后的一枚 row-level NEEDS_ENGINE_SUPPORT blocker-count reduction，不是 automated evidence disposition、fullOfficial、READY 或 4G/4H 完成。

Scope:
- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-and-targeting-stack-timing`
- selected functionalUnit: `FU-246150ecd7`
- selected card: `OGN·253/298 + OGN·302*/298 + OGN·302/298` / `诺克萨斯之手`
- selected effect: `LEGEND_ACTION_DOMAIN`
- input previous closure candidate manifest: `Post03KwCardMatrixReadinessPaymentCostFioraNotPowerfulFaqLayerBattleBlockerClosureCandidateManifest`

Matrix transition:
- `NEEDS_ENGINE_SUPPORT` 217 -> 216
- primary residual 148 -> 148
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` 405 -> 404
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT` 135 -> 134
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual remains 328
- `NEEDS_FAQ_REVIEW` residual remains 92
- primary FAQ residual remains 61
- `fullOfficialTrue` 0 -> 0
- `ready` false -> false

Selected row transition:
- `freezeStatus`: `SHARED_ORACLE_IMPLEMENTATION` -> `SHARED_ORACLE_IMPLEMENTATION`
- `statusFlags`: `IMPLEMENTED_UNTESTED` + `SHARED_ORACLE_IMPLEMENTATION` + `NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED` + `SHARED_ORACLE_IMPLEMENTATION`
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT` + `NEEDS_AUTOMATED_TEST_EVIDENCE` -> `NEEDS_AUTOMATED_TEST_EVIDENCE`

Evidence:
- `src/Riftbound.Engine/CoreRuleEngine.cs` binds `LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA` to Darius source cards `OGN·253/298`, `OGN·302/298` and `OGN·302*/298`.
- `src/Riftbound.Engine/MatchSession.cs` emits the server-authored Darius legend ActionPrompt metadata and source-card group.
- `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs` covers Darius bridge profiles for origin and premium source-card rows, previous-card prompt gating, generated mana payment, end-turn cleanup and rollback.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` covers Darius gain-after-prior-card and no-prior-card rejection fixture paths.
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` carries Darius runtime card-row evidence for the three official snapshot entries.

Non-closure:
- Darius automated evidence disposition remains open.
- Darius full legend resource bridge official breadth remains open.
- Darius Inspire / previous-card timing breadth remains open.
- Darius generated mana resource lifetime / cleanup breadth remains open.
- complete priority / stack timing matrix remains open.
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

Validation passed: matrix JSON valid (jq empty); 03KX active-goal guard 1/1; PaymentEngineCoverageAuditTests 580/580; Darius legend resource bridge focused 84/84; adjacent prompt/payment/legend-resource bridge regression 811/811; backend full 5151/5151; git diff --check passed.
