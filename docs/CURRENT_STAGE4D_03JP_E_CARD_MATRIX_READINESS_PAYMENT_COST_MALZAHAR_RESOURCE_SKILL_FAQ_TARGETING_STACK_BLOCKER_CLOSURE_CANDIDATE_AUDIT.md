# 4D-03JP-E Audit: Payment-Cost Malzahar Resource Skill FAQ Targeting Stack Blocker Closure Candidate

## Decision

`FU-0f7cbe26ce / OGN·113/298 / 玛尔扎哈` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative path for `OGN_MALZAHAR_TAP_RUNE_GAIN_PLAY_UNIT`.

This is not a full-official closure. The row remains `NEEDS_FAQ_REVIEW` and still keeps `NEEDS_FAQ_REVIEW` plus `NEEDS_AUTOMATED_TEST_EVIDENCE` as full official blockers.

## Accepted Evidence

- `MalzaharResourceSkillTests` proves authoritative prompt exposure for open-main and spell-duel focus windows, source/cost-target filtering, generated payment-only power metadata, immediate no-stack resolution, invalid timing/source/target rollback and misuse rejection.
- `PaymentEngineUnificationTests` keeps Malzahar's generated payment-only resource bridge covered alongside shared PaymentEngine prompt/commit/audit behavior.
- `p2-preflight-play-ogn-malzahar-tap-rune-static.fixture.json` proves the source unit's hand-to-base baseline for the fixed catalog card row.
- `rules-evidence-index.md`, `p2-rules-preflight.md`, `CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md` and `CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md` record the representative evidence while preserving FAQ, reaction timing and full official breadth as open.
- `data/official/card-catalog.zh-CN.json` remains the fixed 2026-04-27 source; no live fetch or catalog mutation was performed.

## Row Transition

- Functional unit: `FU-0f7cbe26ce`.
- Card: `OGN·113/298 玛尔扎哈`.
- Effect: `OGN_MALZAHAR_TAP_RUNE_GAIN_PLAY_UNIT`.
- `freezeStatus`: `NEEDS_FAQ_REVIEW -> NEEDS_FAQ_REVIEW`.
- `statusFlags`: `IMPLEMENTED_UNTESTED + NEEDS_ENGINE_SUPPORT + NEEDS_FAQ_REVIEW -> IMPLEMENTED_UNTESTED + NEEDS_FAQ_REVIEW`.
- `fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT + NEEDS_FAQ_REVIEW + NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_FAQ_REVIEW + NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false`; `ready=false`.

## Validation Contract

Required focused validation for this candidate:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MalzaharResourceSkillTests|FullyQualifiedName~Malzahar|FullyQualifiedName~PaymentEngineUnificationTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Malzahar|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Target|FullyQualifiedName~Stack"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MalzaharResourceSkillTests|FullyQualifiedName~Malzahar|FullyQualifiedName~PaymentEngineUnificationTests"` passed: 70/70.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Malzahar|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Target|FullyQualifiedName~Stack"` passed: 1872/1872.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"` passed: 518/518.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed: 5089/5089.
- `git diff --check` passed after final doc write.
- Chrome smoke was not run for 03JP because this candidate made no frontend or browser-script changes.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
