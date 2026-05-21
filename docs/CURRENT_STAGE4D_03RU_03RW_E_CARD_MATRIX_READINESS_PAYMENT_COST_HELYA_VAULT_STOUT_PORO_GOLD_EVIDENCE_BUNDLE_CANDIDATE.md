# 4D-03RU..03RW E_CARD_MATRIX_READINESS Payment-Cost Evidence Bundle Candidate

Status: candidate validated on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Scope

This bundle is a matrix/current-docs plus `PaymentEngineCoverageAuditTests.cs` baseline synchronization only. It does not modify server runtime, frontend runtime, protocol core fields, official catalog data, browser/Chrome scripts, formal 18-step E2E scripts, `fullOfficial`, final readiness flags or `riftbound-dotnet.sln`.

## Selected Rows

| Stage | functionalUnit | cardId | collectorId | Card | Effect / oracle | Evidence basis |
|---|---|---:|---|---|---|---|
| 4D-03RU-E | `FU-19253b2525` | 34772 | `UNL-219/219` | 海力亚秘库 | `BATTLEFIELD_RULE_DOMAIN` | Existing `CoreRuleEngine` / `MatchSession` held-battlefield non-token unit cost-increase path; `docs/CURRENT_P7_9_STATUS.md` P7.9.7 slice 41; existing `UNL-219/219` conformance fixture/state coverage. FAQ refs are empty / `NO_FAQ_CANDIDATE_IN_MATRIX`; battle/cleanup/non-play-domain breadth remains representative-only. |
| 4D-03RV-E | `FU-ace397b616` | 34776 | `UNL-223/219` | 壮壮魄罗 | `UNL_STOUT_PORO_PLAY_KEYWORD_UNIT` | Existing direct-card behavior registry entry for `UNL_STOUT_PORO_PLAY_KEYWORD_UNIT`; existing `p2-preflight-play-unl-stout-poro-keyword-unit.fixture.json` coverage. FAQ refs are empty / `NO_FAQ_CANDIDATE_IN_MATRIX`; Poro trait automated breadth remains representative-only. |
| 4D-03RW-E | `FU-d6323b5258` | 31008 | `UNL·T05` | 金币 | `TOKEN_FACTORY_DOMAIN` | Existing `GoldTokenResourceSkillTests`, `P4ActivatedAbilityCatalog` UNL Gold resource-skill definitions and `CoreRuleEngine` gold-token resource-skill path. FAQ refs are empty / `NO_FAQ_CANDIDATE_IN_MATRIX`; token factory cleanup/target-stack breadth remains representative-only. |

## Count Movement

| Metric | Before | After |
|---|---:|---:|
| all functional units `NEEDS_ENGINE_SUPPORT` | 436 | 433 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 38 | 35 |
| primary payment-cost residual | 3 | 0 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 209 | 208 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 169 | 167 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 144 | 144 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 225 | 222 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 22 | 21 |
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

This bundle only closes the final three primary row-level `NEEDS_ENGINE_SUPPORT` blockers where existing implementation and evidence already exist. It does not claim automated evidence closure, FAQ closure, full official coverage or final readiness.
