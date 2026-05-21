# 4D-03RK..03RO E_CARD_MATRIX_READINESS Payment-Cost Evidence Bundle Candidate

Status: candidate validated on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Scope

This bundle is a matrix/current-docs plus `PaymentEngineCoverageAuditTests.cs` baseline synchronization only. It does not modify server runtime, frontend runtime, protocol core fields, official catalog data, browser/Chrome scripts, formal 18-step E2E scripts, `fullOfficial`, final readiness flags or `riftbound-dotnet.sln`.

## Selected Rows

| Stage | functionalUnit | cardId | collectorId | Card | Effect / oracle | Evidence basis |
|---|---|---:|---|---|---|---|
| 4D-03RK-E | `FU-110ec2c31a` | 34747 | `UNL-194/219` | 黑影 | `SHADOW_BASE_UNIT_STATIC` | Existing direct-card-behavior matrix implementation; 4D-03Q Shadow swift stun representative evidence; no FAQ refs in matrix. |
| 4D-03RL-E | `FU-646e7bdec3` | 34749 | `UNL-196/219` | 小菊！ | `DAISY_ACTIVE_UNIT_NO_TRAIT_DISCOUNT` | Existing direct-card-behavior matrix implementation; rules-evidence index `p2-preflight-play-daisy-no-trait-discount-active-unit`; no FAQ refs in matrix. |
| 4D-03RM-E | `FU-9bd7bdab0c` | 34750 | `UNL-197/219` | 皎月女神 | `LEGEND_ACTION_DOMAIN` | Existing non-play-domain representative implementation; 4D-03CF/03CS Diana legend bridge evidence; no FAQ refs in matrix. |
| 4D-03RN-E | `FU-6dc4a6dbed` | 34759 | `UNL-206/219` | 鲜血祭坛 | `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; 4D-03Y and P1-004 Blood Altar control/recall evidence; no FAQ refs in matrix. |
| 4D-03RO-E | `FU-e009e2e814` | 34764 | `UNL-211/219` | 失落书库 | `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; P1-004 Lost Library high-cost-spell insight/recycle control evidence; no FAQ refs in matrix. |

## Count Movement

| Metric | Before | After |
|---|---:|---:|
| all functional units `NEEDS_ENGINE_SUPPORT` | 446 | 441 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 48 | 43 |
| primary payment-cost residual | 13 | 8 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 216 | 212 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 173 | 170 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 146 | 145 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 235 | 230 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 29 | 25 |
| `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Blocker Disposition

Closed only for the selected row-level matrix entries:

- `NEEDS_ENGINE_SUPPORT` is removed from the five selected functional units and corresponding snapshot entries.
- Their `stage4B.freezeStatus` moves from `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED`.

Still open:

- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains open for all five selected functional units.
- Complete PaymentEngine / `PAY_COST` breadth remains open.
- Shadow battle-response, Daisy trait discount, Diana legend resource bridge breadth, Blood Altar battlefield replacement/control and Lost Library hidden/layer/control breadth remain open beyond this row-level evidence sync.

## Validation Results

Passed on `DOC_MATRIX_CURRENT`:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan over `docs` and `tests` returned no matches
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: 697/697 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: 3019/3019 passed
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: 5344/5344 passed

Frontend build and Chrome smoke were not run because this batch changed only matrix/current docs and the narrow payment audit baseline, with no frontend runtime or browser-script changes.

## Why Not Ready

This bundle only closes five row-level `NEEDS_ENGINE_SUPPORT` blockers where existing implementation and evidence already exist. It does not claim complete official coverage, automated evidence closure, FAQ closure, P0/P1 closure, frontend validation, Chrome/browser smoke, formal 18-step E2E or final project readiness.

## Development Window Gaps

The development window still owns any future runtime/test expansion needed for full official breadth: complete PaymentEngine matrix, full battle / spell-duel lifecycle, complete battlefield and legend-action official breadth, cleanup/replacement-duration breadth, hidden-info/redaction coverage, LayerEngine breadth, and any missing automated evidence disposition. This DOC_MATRIX batch does not add or change those implementations.
