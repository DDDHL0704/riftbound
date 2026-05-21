# 4D-03RA..03RE E_CARD_MATRIX_READINESS Payment-Cost Evidence Bundle Candidate

Status: candidate prepared on `DOC_MATRIX_CURRENT`; validation must be rerun on the final dirty tree before commit. Project remains **NOT READY**.

## Scope

This bundle is a matrix/current-docs plus `PaymentEngineCoverageAuditTests.cs` baseline synchronization only. It does not modify server runtime, frontend runtime, protocol core fields, official catalog data, browser/Chrome scripts, formal 18-step E2E scripts, `fullOfficial`, final readiness flags or `riftbound-dotnet.sln`.

## Selected Rows

| Stage | functionalUnit | cardId | collectorId | Card | Effect / oracle | Evidence basis |
|---|---|---:|---|---|---|---|
| 4D-03RA-E | `FU-aa66712b50` | 34722 | `UNL-173/219` | 牺牲 | `SACRIFICE_DESTROY_FRIENDLY_POWERFUL_DRAW_2_CALL_RUNE` | Existing direct runtime behavior; `p2-preflight-play-sacrifice-destroy-friendly-powerful-draw-call-rune` fixture; rules-evidence index row for UNL-173/219. |
| 4D-03RB-E | `FU-88e6bf6e77` | 34729 | `UNL-178/219` | 波比 | `POPPY_AMBUSH_BARRIER_NO_EXPERIENCE_STATIC` | Existing direct runtime behavior; `p2-preflight-play-poppy-no-experience-ambush-barrier-static` and `p4-play-poppy-spend-experience-reduce-cost` evidence; rules-evidence index rows for UNL-178/219. |
| 4D-03RC-E | `FU-976bb37cdd` | 34730 | `UNL-178a/219` | 波比 | `POPPY_ALT_A_AMBUSH_BARRIER_NO_EXPERIENCE_STATIC` | Existing direct runtime behavior; `p2-preflight-play-poppy-alt-a-no-experience-ambush-barrier-static` evidence; same Poppy experience-cost representative rules evidence. |
| 4D-03RD-E | `FU-c7b4c62435` | 34735 | `UNL-182/219` | 完美谢幕 | `PERFECT_FINALE_BASE_DAMAGE_3`; `PERFECT_FINALE_BATTLEFIELD_DAMAGE_2`; `PERFECT_FINALE_BATTLEFIELD_POWER_MINUS_4`; `PERFECT_FINALE_DRAW_1` | Existing direct runtime behavior; four Perfect Finale mode fixtures; rules-evidence index rows for draw, battlefield damage, base damage and battlefield power modes. |
| 4D-03RE-E | `FU-22895e628a` | 34738 | `UNL-185/219` | 血港鬼影 | `LEGEND_ACTION_DOMAIN` | Existing non-play-domain representative runtime; `P79LegendActPykeReturnsBattlefieldUnitAndCreatesCoin` evidence; existing legend-action domain audit/test coverage. |

## Count Movement

| Metric | Before | After |
|---|---:|---:|
| all functional units `NEEDS_ENGINE_SUPPORT` | 456 | 451 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 58 | 53 |
| primary payment-cost residual | 23 | 18 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 225 | 220 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 179 | 176 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 153 | 150 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 245 | 240 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 38 | 33 |
| `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Blocker Disposition

Closed only for the selected row-level matrix entries:

- `NEEDS_ENGINE_SUPPORT` is removed from the five selected functional units and corresponding snapshot entries.
- Their `stage4B.freezeStatus` moves from `NEEDS_ENGINE_SUPPORT` to `IMPLEMENTED_UNTESTED`.

Still open:

- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains open for all five selected functional units.
- Complete PaymentEngine / `PAY_COST` breadth remains open.
- Poppy ambush / reaction battlefield-play breadth and Barrier damage-order breadth remain open.
- Sacrifice cleanup, hidden-info and layer breadth remain open beyond the representative direct fixture.
- Perfect Finale Echo, target lifecycle, cleanup and hidden-info breadth remain open beyond the four representative modes.
- Pyke legend-action full official breadth, standby / reaction timing, cleanup, hidden-info and non-play-domain representative breadth remain open.

## Why Not Ready

This bundle only closes five row-level `NEEDS_ENGINE_SUPPORT` blockers where existing implementation and evidence already exist. It does not claim complete official coverage, automated evidence closure, FAQ closure, P0/P1 closure, frontend validation, Chrome/browser smoke, formal 18-step E2E or final project readiness.

## Development Window Gaps

The development window still owns any future runtime/test expansion needed for full official breadth: complete PaymentEngine matrix, full ambush / standby / reaction timing, Barrier and battle-spell-duel details, cleanup/replacement-duration breadth, hidden-info/redaction coverage, legend-action official breadth, and any missing automated evidence disposition. This DOC_MATRIX batch does not add or change those implementations.
