# 4D-03IO-E Noxian Recruit Encourage Cost FAQ Blocker Closure Audit

Stage: 4D-03IO-E

Scope: E_CARD_MATRIX_READINESS row-level blocker reduction for `OGN·012/298` 诺克萨斯新兵 / `FU-5d9c4aaa91` / `NOXIAN_RECRUIT_NO_ENCOURAGE_TRIFARIAN_PLAY_UNIT`.

Evidence basis:

- Existing P2 no-encourage unit entry fixture for Noxian Recruit.
- Existing P4 encourage cost-reduction fixture for the same card after the controller has played another card this turn.
- Existing no-prior-card rejection and prompt legality regression for the conditional cost-reduction route.
- Existing P4 status, rules preflight, and rules evidence index rows for `BREAK-JFAQ-260416 p2` and the encourage-cost representative path.

Accepted matrix transition:

- `stage4B.freezeStatus`: `NEEDS_FAQ_REVIEW -> NEEDS_FAQ_REVIEW`.
- `stage4B.statusFlags`: remove `NEEDS_ENGINE_SUPPORT`; keep `IMPLEMENTED_UNTESTED` and `NEEDS_FAQ_REVIEW`.
- `stage4B.fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT+NEEDS_FAQ_REVIEW+NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_FAQ_REVIEW+NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false` remains unchanged.
- `ready=false` remains unchanged.

Prevalidation:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NoxianRecruit|FullyQualifiedName~Encourage|FullyQualifiedName~CostReduction"` passed 24/24.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~NoxianRecruit|FullyQualifiedName~Encourage"` passed 222/222.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed 464/464.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 5035/5035.
- `git diff --check` passed.

Final validation passed for this 03IO slice.

Holdbacks: Noxian Recruit automated evidence disposition, Noxian Recruit FAQ adjudication, complete encourage-family breadth, cleanup / replacement duration interactions, no-prior-card and turn-memory breadth, FEPR target / stack lifecycle, full PaymentEngine / PAY_COST matrix, 1009/811 full-official coverage, formal 18-step E2E, and READY remain open.
