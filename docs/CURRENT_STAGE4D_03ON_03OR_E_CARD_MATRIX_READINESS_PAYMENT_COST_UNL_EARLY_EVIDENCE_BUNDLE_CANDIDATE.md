# 4D-03ON..03OR E_CARD_MATRIX_READINESS Payment-Cost UNL Early Evidence Bundle Candidate

Status: validated on DOC_MATRIX_CURRENT branch before checkpoint commit.

## Selected Rows

- 4D-03ON-E: FU-a0023d7dc7 / UNL-001/219 竞技场理事 / ARENA_COUNCILOR_PLAY_ACTIVE_UNIT
- 4D-03OO-E: FU-194c419d09 / UNL-003/219 鲛人滋事者 / MERFOLK_RABBLE_ROUSER_STANDBY_BATTLEFIELD_DAMAGE_STATIC
- 4D-03OP-E: FU-fae4cb2b90 / UNL-005/219 传承者雷芙纳 / REVNA_PLAY_ROAM_UNIT
- 4D-03OQ-E: FU-c751ddbacb / UNL-019/219 枯萎战斧 / WITHERED_BATTLEAXE_PLAY_EQUIPMENT
- 4D-03OR-E: FU-3afa21c91d / UNL-020/219 曼舞手雷 / DANCING_GRENADE_DAMAGE_2_NO_RECAST

## Evidence Basis

- Existing runtime registry: `src/Riftbound.Engine/CardBehaviorRegistry.cs` contains the five selected effect kinds.
- Existing fixture runner evidence: `ConformanceFixtureRunnerTests` binds Arena Councilor, Merfolk Rabble Rouser, Revna, Withered Battleaxe and Dancing Grenade fixtures.
- Existing rules evidence: `docs/rules-evidence-index.md` records RULE_AUDITED rows for the selected fixture evidence; Withered Battleaxe also has target-reject and assemble-red attach representative evidence.
- Matrix FAQ refs are empty for all five selected functional units; this bundle does not add or infer FAQ adjudication.

## Count Continuity

| Metric | Before | After |
|---|---:|---:|
| all functionalUnits NEEDS_ENGINE_SUPPORT | 521 | 516 |
| payment-cost NEEDS_ENGINE_SUPPORT | 123 | 118 |
| payment-cost primary residual freezeStatus NEEDS_ENGINE_SUPPORT | 86 | 81 |
| targeting-stack-timing NEEDS_ENGINE_SUPPORT | 266 | 263 |
| cleanup-replacement-duration NEEDS_ENGINE_SUPPORT | 205 | 203 |
| hidden-info-random-zone NEEDS_ENGINE_SUPPORT | 170 | 169 |
| payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT | 310 | 305 |
| payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT | 79 | 76 |
| payment-cost NEEDS_AUTOMATED_TEST_EVIDENCE | 328 | 328 |
| payment-cost NEEDS_FAQ_REVIEW | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| fullOfficialTrue | 0 | 0 |
| ready | false | false |

## Closed And Open Blockers

Closed only: selected row-level NEEDS_ENGINE_SUPPORT blockers for the five functional units and their five snapshot entries.

Still open: NEEDS_AUTOMATED_TEST_EVIDENCE for all selected rows, Arena Councilor tap / exhausted power-modifier breadth, Merfolk Rabble Rouser standby face-down placement / reaction breadth, Revna roam movement breadth, Withered Battleaxe equipment attach/follow lifecycle breadth, Dancing Grenade recast / repeated-damage branch, cleanup / hidden-info / layer / control-zone / FEPR target-stack breadth, complete PaymentEngine / PAY_COST matrix, P0/P1, frontend/browser/formal E2E, fullOfficial and final readiness.

## Write Scope

This bundle is limited to matrix JSON, current checkpoint/audit/coverage docs, this candidate/audit pair and `PaymentEngineCoverageAuditTests` residual expected-count/current-slice guard synchronization. It does not modify runtime, frontend, official catalog, protocol core fields, general tests or `riftbound-dotnet.sln`.
