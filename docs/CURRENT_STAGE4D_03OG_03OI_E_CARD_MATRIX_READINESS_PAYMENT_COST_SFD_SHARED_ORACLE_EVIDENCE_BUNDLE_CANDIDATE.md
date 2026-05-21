# 4D-03OG..03OI E_CARD_MATRIX_READINESS Payment-Cost SFD Shared-Oracle Evidence Bundle Candidate

Status: validated on DOC_MATRIX_CURRENT branch before checkpoint commit.

## Selected Rows

- 4D-03OG-E: FU-de2dbad3c5 / SFD·113/221 卢锡安 / SFD_LUCIAN_ALT_A_NO_OPTIONAL_ASSEMBLE_PLAY_UNIT;SFD_LUCIAN_NO_OPTIONAL_ASSEMBLE_PLAY_UNIT
- 4D-03OH-E: FU-d1c30c4216 / SFD·116/221 永恩 / SFD_YONE_PLAY_NO_OPTIONAL_ASSEMBLE;SFD_YONE_PRECON_NO_OPTIONAL_ASSEMBLE_PLAY_UNIT;SFD_YONE_PROMO_PLAY_NO_OPTIONAL_ASSEMBLE
- 4D-03OI-E: FU-83471f1082 / SFD·120/221 希维尔 / SFD_SIVIR_ALT_A_SPELLSHIELD2_PLAY_KEYWORD_UNIT;SFD_SIVIR_SPELLSHIELD2_PLAY_KEYWORD_UNIT

## Evidence Basis

- Existing runtime registry: CardBehaviorRegistry contains the selected Lucian, Yone and Sivir direct-card behavior effect kinds.
- Existing fixture runner evidence: ConformanceFixtureRunnerTests includes the corresponding no-optional-assemble / Spellshield2 play-unit fixtures and target-guard coverage.
- Existing rules evidence: docs/rules-evidence-index.md records RULE_AUDITED rows for the selected fixtures.
- Matrix FAQ refs are empty for all three selected functional units; this batch does not add or infer FAQ adjudication.

## Count Continuity

| Metric | Before | After |
|---|---:|---:|
| all functionalUnits NEEDS_ENGINE_SUPPORT | 528 | 525 |
| payment-cost NEEDS_ENGINE_SUPPORT | 130 | 127 |
| payment-cost primary residual freezeStatus NEEDS_ENGINE_SUPPORT | 90 | 90 |
| targeting-stack-timing NEEDS_ENGINE_SUPPORT | 273 | 270 |
| cleanup-replacement-duration NEEDS_ENGINE_SUPPORT | 208 | 208 |
| hidden-info-random-zone NEEDS_ENGINE_SUPPORT | 170 | 170 |
| payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT | 317 | 314 |
| payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT | 86 | 83 |
| payment-cost NEEDS_AUTOMATED_TEST_EVIDENCE | 328 | 328 |
| payment-cost NEEDS_FAQ_REVIEW | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| fullOfficialTrue | 0 | 0 |
| ready | false | false |

## Closed And Open Blockers

Closed only: selected row-level NEEDS_ENGINE_SUPPORT blockers for the three functional units and their seven snapshot entries.

Still open: NEEDS_AUTOMATED_TEST_EVIDENCE for all selected rows, Lucian / Yone Tempered assemble and armament attach breadth, Sivir Spellshield2 target-tax breadth, battle / layer / FEPR target-stack breadth, complete PaymentEngine / PAY_COST matrix, P0/P1, frontend/browser/formal E2E, fullOfficial and final readiness.

## Write Scope

This bundle is limited to matrix JSON, current checkpoint/audit/coverage docs, this candidate/audit pair and PaymentEngineCoverageAuditTests residual expected-count/current-slice guard synchronization. It does not modify runtime, frontend, official catalog, protocol core fields, general tests or riftbound-dotnet.sln.
