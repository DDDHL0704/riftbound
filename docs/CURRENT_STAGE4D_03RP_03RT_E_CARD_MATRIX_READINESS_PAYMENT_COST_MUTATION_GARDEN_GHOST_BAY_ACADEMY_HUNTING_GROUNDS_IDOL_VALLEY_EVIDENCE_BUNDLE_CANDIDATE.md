# 4D-03RP..03RT E_CARD_MATRIX_READINESS Payment-Cost Evidence Bundle Candidate

Status: candidate validated on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Scope

This bundle is a matrix/current-docs plus `PaymentEngineCoverageAuditTests.cs` baseline synchronization only. It does not modify server runtime, frontend runtime, protocol core fields, official catalog data, browser/Chrome scripts, formal 18-step E2E scripts, `fullOfficial`, final readiness flags or `riftbound-dotnet.sln`.

## Selected Rows

| Stage | functionalUnit | cardId | collectorId | Card | Effect / oracle | Evidence basis |
|---|---|---:|---|---|---|---|
| 4D-03RP-E | `FU-be69df2cec` | 34766 | `UNL-213/219` | 蜕变花园 | `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; mutation garden layer/non-play-domain evidence remains representative-only. |
| 4D-03RQ-E | `FU-d466d0f382` | 34767 | `UNL-214/219` | 鬼影湾 | `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; Ghost Bay hidden/control/targeting evidence remains representative-only. |
| 4D-03RR-E | `FU-3e080d1b63` | 34769 | `UNL-216/219` | 皮城学院 | `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; Piltover Academy battle/cleanup/layer/targeting evidence remains representative-only. |
| 4D-03RS-E | `FU-15ce2642d6` | 34770 | `UNL-217/219` | 捕猎场 | `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; Hunting Grounds battle/non-play evidence remains representative-only. |
| 4D-03RT-E | `FU-41a09f5386` | 34771 | `UNL-218/219` | 偶像谷 | `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; Idol Valley layer/targeting evidence remains representative-only. |

## Count Movement

| Metric | Before | After |
|---|---:|---:|
| all functional units `NEEDS_ENGINE_SUPPORT` | 441 | 436 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 43 | 38 |
| primary payment-cost residual | 8 | 3 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 212 | 209 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 170 | 169 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 145 | 144 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 230 | 225 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 25 | 22 |
| `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Validation Results

Passed on `DOC_MATRIX_CURRENT`:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan over `docs` and `tests` returned no matches
- PaymentEngineCoverageAuditTests: 697/697 passed
- ConformanceFixtureRunnerTests: 3019/3019 passed
- backend full test: 5344/5344 passed

## Why Not Ready

This bundle only closes five row-level `NEEDS_ENGINE_SUPPORT` blockers where existing implementation and evidence already exist. It does not claim automated evidence closure, FAQ closure, full official coverage or final readiness.
