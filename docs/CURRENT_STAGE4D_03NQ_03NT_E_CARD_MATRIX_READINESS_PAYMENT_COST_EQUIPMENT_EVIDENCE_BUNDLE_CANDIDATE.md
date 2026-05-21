# Stage 4D 03NQ-03NT Payment-Cost Equipment Evidence Bundle Candidate

Status: validated in `DOC_MATRIX_CURRENT` under the 2026-05-21 16:20 / 16:44 A_MAIN rolling approval; validation passed for this commit.

## Selected Rows

| Slice | Functional unit | Card | Effect | Evidence basis |
| --- | --- | --- | --- | --- |
| 4D-03NQ-E | `FU-8cb958d3c6` | `SFD·186/221` 旋转飞斧 | `SPINNING_AXE_AGILE_EPHEMERAL_PLAY_EQUIPMENT` | Existing direct-card runtime, Spinning Axe play / target-reject fixtures, Tempered optional attach regression evidence, assemble-equipment evidence, and `rules-evidence-index.md` entries. |
| 4D-03NR-E | `FU-c1583fa973` | `SFD·190/221` 炉火斗篷 | `HEARTHFIRE_CLOAK_PLAY_EQUIPMENT` | Existing direct-card runtime, Hearthfire Cloak play / target-reject fixtures, assemble-equipment evidence, and `rules-evidence-index.md` entries. |
| 4D-03NS-E | `FU-df499dbfd0` | `SFD·191/221` 灭世者的死亡之冠 | `RABADONS_DEATHCAP_PLAY_EQUIPMENT` | Existing direct-card runtime, Rabadon's Deathcap play / target-reject fixtures, assemble-equipment evidence, and `rules-evidence-index.md` entries. |
| 4D-03NT-E | `FU-4396b9d2be` | `SFD·192/221` 舒瑞娅的安魂曲 | `SHURELYAS_REQUIEM_PLAY_EQUIPMENT_READY_ALL_FRIENDLY_UNITS` | Existing direct-card runtime, Shurelya's Requiem play / target-reject / ready-all fixtures, assemble-equipment evidence, and `rules-evidence-index.md` entries. |

## Count Delta

| Residual | Before | After |
| --- | ---: | ---: |
| all functional units `NEEDS_ENGINE_SUPPORT` | 544 | 540 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 146 | 142 |
| primary residual | 106 | 102 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 285 | 284 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 212 | 211 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 175 | 175 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 333 | 329 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 98 | 97 |
| payment-cost `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| payment-cost `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Non-Closure

This bundle closes only the row-level `NEEDS_ENGINE_SUPPORT` blocker for the selected matrix rows. Automated evidence disposition, complete equipment attach/follow lifecycle, Spinning Axe agile / ephemeral breadth, Shurelya's Requiem ready-all breadth, complete LayerEngine / continuous-effect breadth, cleanup / replacement duration breadth, control-zone movement breadth, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness remain open.
