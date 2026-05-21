# 4D-03RK..03RO Candidate Audit

Status: audit validated on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Authorization And Write Scope

The latest shared-board actionable entry keeps `DOC_MATRIX_CURRENT` under `APPROVED_ACTIVE_NO_IDLE`. This 03RK-03RO batch stays inside the matrix-number-reduction lane:

- Allowed: `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`, current checkpoint docs, per-bundle candidate/audit docs and the narrow `PaymentEngineCoverageAuditTests.cs` expected-count/current-slice baseline sync.
- Not touched: server runtime, frontend runtime, API/protocol core fields, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness flags and `riftbound-dotnet.sln`.

## Evidence Audit

| Row | Existing implementation | Evidence note |
|---|---|---|
| `FU-110ec2c31a` / `UNL-194/219` / 黑影 | direct-card-behavior `SHADOW_BASE_UNIT_STATIC` | 4D-03Q Shadow swift stun representative evidence exists; row can move to automated-evidence residual only. |
| `FU-646e7bdec3` / `UNL-196/219` / 小菊！ | direct-card-behavior `DAISY_ACTIVE_UNIT_NO_TRAIT_DISCOUNT` | rules-evidence index records the Daisy no-trait-discount active-unit preflight; row can move to automated-evidence residual only. |
| `FU-9bd7bdab0c` / `UNL-197/219` / 皎月女神 | non-play-domain representative `LEGEND_ACTION_DOMAIN` | Diana legend bridge evidence exists, but full legend official breadth remains open. |
| `FU-6dc4a6dbed` / `UNL-206/219` / 鲜血祭坛 | non-play-domain representative `BATTLEFIELD_RULE_DOMAIN` | Blood Altar battlefield-domain and battle-destroyed recall/control evidence exists; full battlefield official breadth remains open. |
| `FU-e009e2e814` / `UNL-211/219` / 失落书库 | non-play-domain representative `BATTLEFIELD_RULE_DOMAIN` | Lost Library high-cost spell insight/recycle control evidence exists; hidden/layer/control breadth remains open. |

All five selected rows have `faqRefs=[]` and `faqEvidenceStatus=NO_FAQ_CANDIDATE_IN_MATRIX`, so this batch does not close or lower any FAQ residual.

## Count Audit

The draft moves:

- all functional units `NEEDS_ENGINE_SUPPORT`: 446 -> 441
- payment-cost `NEEDS_ENGINE_SUPPORT`: 48 -> 43
- primary payment-cost residual: 13 -> 8
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 216 -> 212
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: 173 -> 170
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: 146 -> 145
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 235 -> 230
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 29 -> 25
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains 328
- `NEEDS_FAQ_REVIEW`: remains 92
- primary FAQ residual: remains 61
- `fullOfficialTrue`: remains 0
- `ready`: remains false

## Closure Judgment

This is acceptable only as row-level `NEEDS_ENGINE_SUPPORT` evidence synchronization. It is not a full-official or final-readiness claim.

Closed:

- row-level `NEEDS_ENGINE_SUPPORT` on the five selected FUs / snapshot entries.

Open:

- automated evidence disposition for all selected FUs
- complete PaymentEngine / `PAY_COST` matrix
- complete battle / spell-duel lifecycle where applicable
- cleanup / hidden / control / layer / non-play-domain official breadth where applicable
- frontend/browser/formal E2E gates

## Validation Results

Passed on `DOC_MATRIX_CURRENT`:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan over `docs` and `tests` returned no matches
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: 697/697 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: 3019/3019 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: 5344/5344 passed

Frontend build and Chrome smoke are not required for this batch because no frontend runtime or browser script changed.
