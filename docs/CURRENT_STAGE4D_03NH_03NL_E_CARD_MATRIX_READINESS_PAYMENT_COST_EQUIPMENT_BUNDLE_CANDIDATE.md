# Stage 4D 03NH-03NL Payment-Cost Equipment Bundle Candidate

Status: validated in `DOC_MATRIX_CURRENT` under the 2026-05-21 15:12 A_MAIN rolling approval.

## Selected Rows

| Slice | Functional unit | Card | Effect | Evidence basis |
| --- | --- | --- | --- | --- |
| 4D-03NH-E | `FU-9c71d431eb` | `SFD·134/221` 萃取 | `CULL_PLAY_EQUIPMENT` | Existing `CardBehaviorRegistry` runtime, play/target-reject fixtures, `ASSEMBLE_PURPLE` attach fixture, `rules-evidence-index.md` entries. |
| 4D-03NI-E | `FU-ff2d2bc524` | `SFD·150/221` 临终仪式 | `LAST_RITES_PLAY_EQUIPMENT` | Existing runtime, play/target-reject fixtures, recycle-graveyard attach fixture, `rules-evidence-index.md` entries. |
| 4D-03NJ-E | `FU-1ef9ede834` | `SFD·153/221` 先锋之眼 | `VANGUARDS_EYE_PLAY_EQUIPMENT` | Existing runtime, play/target-reject fixtures, `ASSEMBLE_YELLOW` attach fixture, `rules-evidence-index.md` entries. |
| 4D-03NK-E | `FU-39170cc19c` | `SFD·161/221` 暴风大剑 | `BF_SWORD_PLAY_EQUIPMENT` | Existing runtime, play/target-reject fixtures, `ASSEMBLE_YELLOW` attach fixture, `rules-evidence-index.md` entries. |
| 4D-03NL-E | `FU-b02b48c074` | `SFD·172/221` 神圣剪刀 | `SACRED_SHEARS_PLAY_EQUIPMENT` | Existing runtime, play/target-reject fixtures, `ASSEMBLE_YELLOW` attach fixture, `rules-evidence-index.md` entries. |

## Count Delta

| Residual | Before | After |
| --- | ---: | ---: |
| all functional units `NEEDS_ENGINE_SUPPORT` | 553 | 548 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 155 | 150 |
| primary residual | 115 | 110 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 288 | 288 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 215 | 215 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 176 | 175 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 342 | 337 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 101 | 101 |
| payment-cost `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| payment-cost `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Non-Closure

This bundle closes only the row-level `NEEDS_ENGINE_SUPPORT` blocker for the selected matrix rows. Automated evidence disposition remains open for all five rows, Last Rites hidden-info breadth remains open, and complete equipment attach/follow lifecycle, LayerEngine / continuous-effect breadth, control-zone movement breadth, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness remain open.
