# 4D-03OX..03PB E_CARD_MATRIX_READINESS Payment-Cost UNL Haste / Vi / Dragon Tiger Evidence Bundle Candidate

Status: validated on DOC_MATRIX_CURRENT branch before checkpoint commit.

## Selected Rows

- 4D-03OX-E: FU-b0059eceb7 / UNL-029/219 绯红印记树怪 / CRIMSON_SIGNET_TREANT_PLAY_UNIT_NO_OPTIONAL_HASTE
- 4D-03OY-E: FU-1217d525f4 / UNL-029a/219 绯红印记树怪 / CRIMSON_SIGNET_TREANT_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE
- 4D-03OZ-E: FU-70ba9864d3 / UNL-030/219 蔚 / VI_PLAY_KEYWORD_UNIT
- 4D-03PA-E: FU-b880ef8428 / UNL-030a/219 蔚 / VI_ALT_A_PLAY_KEYWORD_UNIT
- 4D-03PB-E: FU-04ec02e924 / UNL-032/219 龙虎双雄 / DRAGON_TIGER_DRAW_UNIT_RECYCLE_REST_NO_ECHO

## Evidence Basis

- Existing runtime registry: `src/Riftbound.Engine/CardBehaviorRegistry.cs` contains all five selected effect kinds.
- Existing activated-skill runtime evidence: `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs` registers the Vi `PAY_2_RED_DOUBLE_POWER` representative skill.
- Existing fixture runner evidence: `ConformanceFixtureRunnerTests` binds Crimson Signet Treant no-optional and HASTE_READY fixtures, Vi / Vi alt keyword-unit fixtures, Vi activated-skill fixtures, and Dragon Tiger draw/recycle fixtures.
- Existing rules evidence: `docs/rules-evidence-index.md` records RULE_AUDITED rows for `p2-preflight-play-crimson-signet-treant-no-optional-haste`, `p4-play-crimson-signet-treant-haste-ready`, `p2-preflight-play-crimson-signet-treant-alt-a-no-optional-haste`, `p4-play-crimson-signet-treant-alt-a-haste-ready`, `p2-preflight-play-vi-keyword-unit`, `p2-preflight-play-vi-alt-a-keyword-unit`, `p4-activate-vi-double-power-skill` and Dragon Tiger draw/recycle fixtures.
- FAQ handling: the two Crimson Signet Treant rows keep `NEEDS_FAQ_REVIEW`; this bundle removes only their row-level `NEEDS_ENGINE_SUPPORT` blocker. Vi, Vi alt and Dragon Tiger have no matrix FAQ refs.

## Count Continuity

| Metric | Before | After |
|---|---:|---:|
| all functionalUnits NEEDS_ENGINE_SUPPORT | 511 | 506 |
| payment-cost NEEDS_ENGINE_SUPPORT | 113 | 108 |
| payment-cost primary residual freezeStatus NEEDS_ENGINE_SUPPORT | 76 | 73 |
| targeting-stack-timing NEEDS_ENGINE_SUPPORT | 260 | 257 |
| cleanup-replacement-duration NEEDS_ENGINE_SUPPORT | 201 | 199 |
| hidden-info-random-zone NEEDS_ENGINE_SUPPORT | 167 | 166 |
| payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT | 300 | 295 |
| payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT | 73 | 70 |
| payment-cost NEEDS_AUTOMATED_TEST_EVIDENCE | 328 | 328 |
| payment-cost NEEDS_FAQ_REVIEW | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| fullOfficialTrue | 0 | 0 |
| ready | false | false |

## Closed And Open Blockers

Closed only: selected row-level `NEEDS_ENGINE_SUPPORT` blockers for the five functional units and their five snapshot entries.

Still open: `NEEDS_AUTOMATED_TEST_EVIDENCE` for all selected rows; `NEEDS_FAQ_REVIEW` for both Crimson Signet Treant rows; Crimson Signet Treant red-resource HASTE_READY exactness, conquest trigger and post-conquest boon breadth; Vi activated-skill breadth beyond the representative guards; Vi alt paid skill branch; Dragon Tiger Echo/recast branch; cleanup / hidden-info / control-zone / layer / targeting-stack breadth; complete PaymentEngine / PAY_COST matrix; P0/P1; frontend/browser/formal E2E; fullOfficial and final readiness.

## Write Scope

This bundle is limited to matrix JSON, current checkpoint/audit/coverage docs, this candidate/audit pair and `PaymentEngineCoverageAuditTests` residual expected-count/current-slice guard synchronization. It does not modify runtime, frontend, official catalog, protocol core fields, general tests or `riftbound-dotnet.sln`.
