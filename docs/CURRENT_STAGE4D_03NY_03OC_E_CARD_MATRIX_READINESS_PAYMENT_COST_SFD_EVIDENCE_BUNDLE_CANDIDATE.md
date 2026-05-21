# Stage 4D 03NY-03OC Payment-Cost SFD Evidence Bundle Candidate

Status: validated in `DOC_MATRIX_CURRENT` under the 2026-05-21 17:31 A_MAIN rolling approval answer; validation passed for this commit.

## Selected Rows

| Slice | Functional unit | Card | Effect | Evidence basis |
| --- | --- | --- | --- | --- |
| 4D-03NY-E | `FU-f8a1c01b1f` | `SFD·122/221` 预判攻势 | `PREDICTIVE_OFFENSIVE_DRAW_ONE_RECYCLE_OTHER` | Existing direct-card runtime, draw-one / recycle-other fixture evidence, top-two / invalid-window rejection tests, and `rules-evidence-index.md` entry `p2-preflight-play-predictive-offensive-draw-one-recycle-other`. |
| 4D-03NZ-E | `FU-000f38461c` | `SFD·127/221` 炳文大师 | `MASTER_BINGWEN_PLAY_UNIT_NO_OPTIONAL_ASSEMBLE` | Existing direct-card runtime, no-optional-assemble fixture evidence, and `rules-evidence-index.md` entry `p2-preflight-play-master-bingwen-no-optional-assemble`. |
| 4D-03OA-E | `FU-807ad1d0c7` | `SFD·131/221` 远古战狂 | `ANCIENT_BERSERKER_PLAY_UNIT_NO_OPTIONAL_HASTE` | Existing direct-card runtime, no-optional-Haste fixture evidence, Haste optional-ready branch test evidence, and `rules-evidence-index.md` entries `p2-preflight-play-ancient-berserker-no-optional-haste` / `p4-play-ancient-berserker-haste-ready`. |
| 4D-03OB-E | `FU-e8ab25c204` | `SFD·138/221` 吟风翼 | `WINDSONG_WING_PLAY_UNIT_OPTIONAL_RETURN_SMALL_BATTLEFIELD` | Existing direct-card runtime, return-small-battlefield / no-target / power-too-high rejection tests, and `rules-evidence-index.md` entry `p2-preflight-play-windsong-wing-return-small-battlefield`. |
| 4D-03OC-E | `FU-5085d2421e` | `SFD·140/221` 菲兹 | `SFD_FIZZ_GRAVEYARD_SPELL_PLAY_UNIT` | Existing direct-card runtime, normal graveyard-spell static fixture evidence, and `rules-evidence-index.md` entry `p2-preflight-play-sfd-fizz-graveyard-spell-static`. |

## Count Delta

| Residual | Before | After |
| --- | ---: | ---: |
| all functional units `NEEDS_ENGINE_SUPPORT` | 536 | 531 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 138 | 133 |
| primary residual | 98 | 93 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 281 | 276 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 210 | 210 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 175 | 172 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 325 | 320 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 94 | 89 |
| payment-cost `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| payment-cost `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Non-Closure

This bundle closes only the row-level `NEEDS_ENGINE_SUPPORT` blocker for the selected matrix rows. Automated evidence disposition, Predictive Offensive Echo / replay breadth, Master Bingwen Tempered assemble / armament attach breadth, Ancient Berserker exact Haste resource breadth, Windsong Wing standby / reaction placement breadth, SFD Fizz graveyard spell selection / free-play breadth, hidden-info / control-zone / layer / battle-spell-duel breadth, complete FEPR target / stack timing, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness remain open.
