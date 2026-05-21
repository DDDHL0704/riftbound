# Stage 4D 03OD-03OF Payment-Cost SFD Direct Evidence Bundle Candidate

Status: validated in `DOC_MATRIX_CURRENT` under the 2026-05-21 17:52 A_MAIN continuous DOC_MATRIX approval; validation passed for this commit.

## Selected Rows

| Slice | Functional unit | Card | Effect | Evidence basis |
| --- | --- | --- | --- | --- |
| 4D-03OD-E | `FU-d5a3098ec0` | `SFD·154/221` 护驾！ | `PROTECT_THE_EMPEROR_CREATE_SAND_SOLDIER` | Existing direct-card runtime, fixture `p2-preflight-play-protect-the-emperor-create-sand-soldier`, runner test `CoreRuleEnginePlaysProtectTheEmperorCreatesSandSoldierInBase`, and `rules-evidence-index.md` audited entry. |
| 4D-03OE-E | `FU-153094d703` | `SFD·160/221` 祖安混混 | `ZAUNITE_THUG_NO_OPTIONAL_EQUIPMENT_PLAY_UNIT` | Existing direct-card runtime, fixture `p2-preflight-play-zaunite-thug-no-optional-equipment-static`, rejection fixture `p4-play-zaunite-thug-target-rejected`, and `rules-evidence-index.md` audited entries. |
| 4D-03OF-E | `FU-b4430b54fc` | `SFD·164/221` 流沙陷坑 | `QUICKSAND_PIT_DESTROY_BATTLEFIELD_UNIT` | Existing direct-card runtime, fixture `p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack`, runner test `CoreRuleEnginePlaysQuicksandPitThroughStack`, and `rules-evidence-index.md` audited entry. |

## Count Delta

| Residual | Before | After |
| --- | ---: | ---: |
| all functional units `NEEDS_ENGINE_SUPPORT` | 531 | 528 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 133 | 130 |
| primary residual | 93 | 90 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 276 | 273 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 210 | 208 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 172 | 170 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 320 | 317 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 89 | 86 |
| payment-cost `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| payment-cost `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Non-Closure

This bundle closes only the row-level `NEEDS_ENGINE_SUPPORT` blocker for the selected matrix rows. Automated evidence disposition, Protect the Emperor yellow optional-ready branch, Zaunite Thug optional friendly equipment destroy branch, Quicksand Pit non-hand cost-reduction path, battle / cleanup / hidden / targeting-stack breadth, complete FEPR target / stack timing, complete PaymentEngine / PAY_COST matrix, P0/P1, Chrome / formal E2E, `fullOfficial` and final readiness remain open.
