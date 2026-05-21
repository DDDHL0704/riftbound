# 4D-03OJ..03OM E_CARD_MATRIX_READINESS Payment-Cost UNL Direct-Unit Evidence Bundle Candidate

Status: validated on DOC_MATRIX_CURRENT branch before checkpoint commit.

## Selected Rows

- 4D-03OJ-E: FU-cfaef75b23 / UNL-004/219 晋升信徒 / ASCENDED_BELIEVER_NO_SPELL_VANILLA_PLAY_UNIT
- 4D-03OK-E: FU-ec8f0355c7 / UNL-006/219 小鲨鱼 / BABY_SHARK_PLAY_UNIT_NO_OPTIONAL_HASTE_OVERWHELM
- 4D-03OL-E: FU-7535ec3aac / UNL-018/219 雪人斗士 / YETI_BRAWLER_VANILLA_PLAY_UNIT
- 4D-03OM-E: FU-bd6c450460 / UNL-122/219 新月禁卫 / CRESCENT_GUARD_NO_SPELL_VANILLA_PLAY_UNIT

## Evidence Basis

- Existing runtime registry: `CardBehaviorRegistry` contains the four selected direct-card behavior effect kinds.
- Existing fixture runner evidence: `ConformanceFixtureRunnerTests` binds the selected UNL fixtures for Ascended Believer, Baby Shark, Yeti Brawler and Crescent Guard.
- Existing rules evidence: `docs/rules-evidence-index.md` records RULE_AUDITED rows for the selected fixture evidence.
- Matrix FAQ refs are empty for all four selected functional units; this bundle does not add or infer FAQ adjudication.

## Count Continuity

| Metric | Before | After |
|---|---:|---:|
| all functionalUnits NEEDS_ENGINE_SUPPORT | 525 | 521 |
| payment-cost NEEDS_ENGINE_SUPPORT | 127 | 123 |
| payment-cost primary residual freezeStatus NEEDS_ENGINE_SUPPORT | 90 | 86 |
| targeting-stack-timing NEEDS_ENGINE_SUPPORT | 270 | 266 |
| cleanup-replacement-duration NEEDS_ENGINE_SUPPORT | 208 | 205 |
| hidden-info-random-zone NEEDS_ENGINE_SUPPORT | 170 | 170 |
| payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT | 314 | 310 |
| payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT | 83 | 79 |
| payment-cost NEEDS_AUTOMATED_TEST_EVIDENCE | 328 | 328 |
| payment-cost NEEDS_FAQ_REVIEW | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| fullOfficialTrue | 0 | 0 |
| ready | false | false |

## Closed And Open Blockers

Closed only: selected row-level NEEDS_ENGINE_SUPPORT blockers for the four functional units and their four snapshot entries.

Still open: NEEDS_AUTOMATED_TEST_EVIDENCE for all selected rows, Ascended Believer true 4+ spell-memory breadth, Baby Shark exact HASTE_READY / Assault breadth, Yeti Brawler conquer / overdamage / Gold-equipment token breadth, Crescent Guard spell-ready optional purple-payment breadth, cleanup / layer / battle / FEPR target-stack breadth, complete PaymentEngine / PAY_COST matrix, P0/P1, frontend/browser/formal E2E, fullOfficial and final readiness.

## Write Scope

This bundle is limited to matrix JSON, current checkpoint/audit/coverage docs, this candidate/audit pair and `PaymentEngineCoverageAuditTests` residual expected-count/current-slice guard synchronization. It does not modify runtime, frontend, official catalog, protocol core fields, general tests or `riftbound-dotnet.sln`.
