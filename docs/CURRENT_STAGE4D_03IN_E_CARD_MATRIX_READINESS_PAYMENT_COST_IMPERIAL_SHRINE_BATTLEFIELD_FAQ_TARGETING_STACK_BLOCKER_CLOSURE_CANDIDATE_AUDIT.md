# 4D-03IN-E Imperial Shrine Battlefield FAQ Blocker Closure Audit

Stage: 4D-03IN-E

Scope: E_CARD_MATRIX_READINESS row-level blocker reduction for `SFD·207/221` 帝王神坛 / `FU-ec31812b00` / `BATTLEFIELD_RULE_DOMAIN`.

Evidence basis:

- Existing Stage 4C-86 Imperial Shrine conquer pay-one return-unit create-Sand-Soldier representative evidence.
- Existing service-authoritative route in `CoreRuleEngine` and development-seed prompt/snapshot evidence in `MatchSession`.
- Existing behavior catalog mapping for `BATTLEFIELD_RULE_DOMAIN`.
- Existing conformance and hub tests covering the representative Imperial Shrine conquer route and adjacent battlefield-conquer behavior.
- Existing rules evidence index entry for `SOUL-JFAQ-260114 p22`.

Accepted matrix transition:

- `stage4B.freezeStatus`: `NEEDS_FAQ_REVIEW -> NEEDS_FAQ_REVIEW`.
- `stage4B.statusFlags`: remove `NEEDS_ENGINE_SUPPORT`; keep `IMPLEMENTED_UNTESTED` and `NEEDS_FAQ_REVIEW`.
- `stage4B.fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT+NEEDS_FAQ_REVIEW+NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_FAQ_REVIEW+NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false` remains unchanged.
- `ready=false` remains unchanged.

Prevalidation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldConquerSandSoldier"` passed 3/3.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldConquer"` passed 48/48.

Final validation passed: jq empty passed; P79BattlefieldConquerSandSoldier focused regression 3/3 passed; BattlefieldConquer adjacent regression 48/48 passed; PaymentEngineCoverageAuditTests 462/462 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5033/5033 passed; git diff --check passed.

Holdbacks: Imperial Shrine automated evidence disposition, Imperial Shrine FAQ adjudication, optional trigger prompt / decline, complete PaymentEngine quote / authorize / commit, complete battlefield / spell-duel / battle lifecycle, multi-battlefield APNAP ordering, hidden-info / redaction matrix, FEPR target / stack lifecycle, full PaymentEngine / PAY_COST matrix, 1009/811 full-official coverage, formal 18-step E2E, and READY remain open.
