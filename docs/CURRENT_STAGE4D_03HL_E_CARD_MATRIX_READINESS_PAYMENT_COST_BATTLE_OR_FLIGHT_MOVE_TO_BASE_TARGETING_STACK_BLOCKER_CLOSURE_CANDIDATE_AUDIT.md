# 4D-03HL-E Audit

日期：2026-05-19
结论：**PASS FOR THIS ROW / NOT READY**

## Scope

- Matrix overlay: `stage4D03HlPaymentCostBattleOrFlightMoveToBaseTargetingStackBlockerClosureCandidate`
- Manifest: `Post03HlCardMatrixReadinessPaymentCostBattleOrFlightMoveToBaseTargetingStackBlockerClosureCandidateManifest`
- Gate: `E_CARD_MATRIX_READINESS_POST_03HK_PAYMENT_COST_BATTLE_OR_FLIGHT_MOVE_TO_BASE_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`
- Input previous manifest: `Post03HkCardMatrixReadinessPaymentCostHeimerdingerSharedSourceUnitBlockerClosureCandidateManifest`
- Selected row: `FU-813144e7d4 / OGN·168/298 战或逃`

## Evidence

- Stage 4C-28 Battle or Flight move-to-owner-base audit/evidence
- BattleOrFlightMoveToBase conformance tests
- Battle or Flight preflight fixture
- target guard hardening for invalid, hidden, standby or non-unit targets
- `ConformanceFixtureRunnerTests`
- `CardBehaviorRegistry` and representative target movement route evidence

## Validation

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests` passed: 408/408.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed: 4979/4979.
- `git diff --check` passed.

## Non-Closure

This row remains FAQ-blocked and does not claim swift / standby reaction timing, complete spell-duel or battle lifecycle, complete FEPR target legality, full ZoneOwnership / ControlChange / Movement coverage, full official PaymentEngine behavior or final readiness. P0/P1, E_CARD_MATRIX_READINESS, full PaymentEngine matrix closure, card matrix closure and READY all remain open.
