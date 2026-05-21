# 4D-03OS..03OW E_CARD_MATRIX_READINESS Payment-Cost UNL Early Evidence Bundle Candidate

Status: validated on DOC_MATRIX_CURRENT branch before checkpoint commit.

## Selected Rows

- 4D-03OS-E: FU-88a67bee9c / UNL-022/219 烬 / JHIN_SPELLSHIELD_ROAM_PLAY_KEYWORD_UNIT
- 4D-03OT-E: FU-6167a33f64 / UNL-022a/219 烬 / JHIN_ALT_A_SPELLSHIELD_ROAM_PLAY_KEYWORD_UNIT
- 4D-03OU-E: FU-fa568fbe53 / UNL-025/219 不死军团 / UNDEAD_LEGION_HAND_PLAY_SPIRIT_UNIT
- 4D-03OV-E: FU-523f9b22de / UNL-026/219 泽拉斯 / XERATH_ACTIVATED_SKILL_PLAY_UNIT
- 4D-03OW-E: FU-88fe3a652e / UNL-028a/219 派克 / PYKE_ALT_A_PLAY_UNIT_OPTIONAL_READY_POWER

## Evidence Basis

- Existing runtime registry: `src/Riftbound.Engine/CardBehaviorRegistry.cs` contains the five selected effect kinds.
- Existing fixture runner evidence: `ConformanceFixtureRunnerTests` binds Jhin, Jhin alt, Undead Legion, Xerath and Pyke alt fixtures.
- Existing rules evidence: `docs/rules-evidence-index.md` records RULE_AUDITED rows for the selected fixture evidence. Xerath also has P4 activated-skill / Spellshield-tax guard evidence, and Pyke alt has both optional and no-optional ready-power fixture evidence.
- Matrix FAQ refs are empty for all five selected functional units; this bundle does not add or infer FAQ adjudication.

## Count Continuity

| Metric | Before | After |
|---|---:|---:|
| all functionalUnits NEEDS_ENGINE_SUPPORT | 516 | 511 |
| payment-cost NEEDS_ENGINE_SUPPORT | 118 | 113 |
| payment-cost primary residual freezeStatus NEEDS_ENGINE_SUPPORT | 81 | 76 |
| targeting-stack-timing NEEDS_ENGINE_SUPPORT | 263 | 260 |
| cleanup-replacement-duration NEEDS_ENGINE_SUPPORT | 203 | 201 |
| hidden-info-random-zone NEEDS_ENGINE_SUPPORT | 169 | 167 |
| payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT | 305 | 300 |
| payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT | 76 | 73 |
| payment-cost NEEDS_AUTOMATED_TEST_EVIDENCE | 328 | 328 |
| payment-cost NEEDS_FAQ_REVIEW | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| fullOfficialTrue | 0 | 0 |
| ready | false | false |

## Closed And Open Blockers

Closed only: selected row-level NEEDS_ENGINE_SUPPORT blockers for the five functional units and their five snapshot entries.

Still open: NEEDS_AUTOMATED_TEST_EVIDENCE for all selected rows, Jhin Spellshield / roam resource-trigger breadth, Jhin alt Spellshield / roam resource-trigger breadth, Undead Legion graveyard encourage / extra-cost / cleanup / hidden breadth, Xerath activated damage skill and Spellshield-tax breadth beyond the indexed representative guards, Pyke alt standby / reaction / roam / cleanup / hidden breadth, complete FEPR target / stack timing matrix, complete PaymentEngine / PAY_COST matrix, P0/P1, frontend/browser/formal E2E, fullOfficial and final readiness.

## Write Scope

This bundle is limited to matrix JSON, current checkpoint/audit/coverage docs, this candidate/audit pair and `PaymentEngineCoverageAuditTests` residual expected-count/current-slice guard synchronization. It does not modify runtime, frontend, official catalog, protocol core fields, general tests or `riftbound-dotnet.sln`.
