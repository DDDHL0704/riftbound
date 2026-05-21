# Stage 4D 03NM-03NP Payment-Cost Mixed Evidence Bundle Candidate

Status: validated in `DOC_MATRIX_CURRENT` under the 2026-05-21 15:52 / 15:57 A_MAIN rolling approval.

## Selected Rows

| Slice | Functional unit | Card | Effect | Evidence basis |
| --- | --- | --- | --- | --- |
| 4D-03NM-E | `FU-5b5926cc34` | `SFD·114/221` 行军号令 | `MARCHING_ORDERS_MUTUAL_POWER_DAMAGE` | Existing `CardBehaviorRegistry` runtime, Marching Orders play / target-reject fixtures, conformance runner tests, and `rules-evidence-index.md` entries. |
| 4D-03NN-E | `FU-2071e3b7c8` | `SFD·178/221` 破败王者之刃 | `BLADE_OF_RUINED_KING_PLAY_EQUIPMENT` | Existing equipment runtime/profile support, play / target-reject fixtures, assemble destroy-friendly-unit evidence, server-rule audit and `rules-evidence-index.md` entries. |
| 4D-03NO-E | `FU-05ba60b8e9` | `SFD·151/221` 力量之缚 | `POWER_BIND_TWO_FRIENDLY_POWER_PLUS_1` | Existing runtime, Echo two-friendly power fixture, modifier-order regression, and `rules-evidence-index.md` entry. |
| 4D-03NP-E | `FU-270d7bad6f` | `SFD·182/221` 危险温度 | `DANGER_TEMPERATURE_MECHANICAL_POWER_PLUS_1` | Existing runtime, mechanical-only buff fixture/regression, and `rules-evidence-index.md` entry. |

## Count Delta

| Residual | Before | After |
| --- | ---: | ---: |
| all functional units `NEEDS_ENGINE_SUPPORT` | 548 | 544 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 150 | 146 |
| primary residual | 110 | 106 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 288 | 285 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 215 | 212 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 175 | 175 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 337 | 333 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 101 | 98 |
| payment-cost `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| payment-cost `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Non-Closure

This bundle closes only the row-level `NEEDS_ENGINE_SUPPORT` blocker for the selected matrix rows. Marching Orders battle / spell-duel lifecycle, Blade of the Ruined King full equipment attach/follow lifecycle, Power Bind Echo / payment-branch breadth, Danger Temperature mechanical-tag official breadth, automated evidence disposition, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness remain open.
