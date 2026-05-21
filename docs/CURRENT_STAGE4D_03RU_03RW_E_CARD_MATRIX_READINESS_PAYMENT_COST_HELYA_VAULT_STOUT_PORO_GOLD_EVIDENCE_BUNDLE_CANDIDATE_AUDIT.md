# 4D-03RU..03RW Candidate Audit

Status: audit validated on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Authorization And Write Scope

The latest shared-board actionable entry keeps `DOC_MATRIX_CURRENT` under `APPROVED_ACTIVE_NO_IDLE`. This 03RU-03RW batch stays inside the matrix-number-reduction lane.

## Evidence Audit

| Row | Existing implementation | Evidence note |
|---|---|---|
| `FU-19253b2525` / `UNL-219/219` / 海力亚秘库 | representative `BATTLEFIELD_RULE_DOMAIN` | Existing `CoreRuleEngine` / `MatchSession` held-battlefield non-token unit cost-increase path; `docs/CURRENT_P7_9_STATUS.md` P7.9.7 slice 41; existing `UNL-219/219` fixture/state references in conformance tests. FAQ refs empty / `NO_FAQ_CANDIDATE_IN_MATRIX`; row can move only to automated-evidence residual. |
| `FU-ace397b616` / `UNL-223/219` / 壮壮魄罗 | representative `UNL_STOUT_PORO_PLAY_KEYWORD_UNIT` | Existing `CardBehaviorRegistry` direct-card behavior and `p2-preflight-play-unl-stout-poro-keyword-unit.fixture.json` coverage for the UNL keyword unit row. FAQ refs empty / `NO_FAQ_CANDIDATE_IN_MATRIX`; row can move only to automated-evidence residual. |
| `FU-d6323b5258` / `UNL·T05` / 金币 | representative `TOKEN_FACTORY_DOMAIN` | Existing `GoldTokenResourceSkillTests` bind `UNL·T05` prompt, destroy-cost command, generated generic temporary payment resource lifetime, legal rune payment cleanup and invalid-source rollback to the official token row. FAQ refs empty / `NO_FAQ_CANDIDATE_IN_MATRIX`; row can move only to automated-evidence residual. |

## Count Audit

- all functional units `NEEDS_ENGINE_SUPPORT`: 436 -> 433
- payment-cost `NEEDS_ENGINE_SUPPORT`: 38 -> 35
- primary payment-cost residual: 3 -> 0
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 209 -> 208
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: 169 -> 167
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: 144 -> 144
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 225 -> 222
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 22 -> 21
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains 328
- `NEEDS_FAQ_REVIEW`: remains 92
- primary FAQ residual: remains 61
- `fullOfficialTrue`: remains 0
- `ready`: remains false

## Closure Judgment

This is acceptable only as row-level `NEEDS_ENGINE_SUPPORT` evidence synchronization. Automated evidence, full official breadth, frontend/browser/formal E2E and READY remain open.

## Validation Results

Passed on `DOC_MATRIX_CURRENT`:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- conflict-marker scan over `docs` and `tests` returned no matches
- PaymentEngineCoverageAuditTests: 697/697 passed
- ConformanceFixtureRunnerTests: 3019/3019 passed
- backend full test: 5344/5344 passed
