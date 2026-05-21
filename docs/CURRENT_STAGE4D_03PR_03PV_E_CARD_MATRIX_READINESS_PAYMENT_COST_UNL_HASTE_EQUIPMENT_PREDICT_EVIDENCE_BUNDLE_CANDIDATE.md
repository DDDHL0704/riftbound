# 4D-03PR..03PV E_CARD_MATRIX_READINESS Payment-Cost UNL Haste / Equipment / Predict Evidence Bundle Candidate

Status: validation passed on DOC_MATRIX branch. Project remains **NOT READY**.

## Selected Rows

| Stage | Functional unit | Card | Effect | Existing evidence |
| --- | --- | --- | --- | --- |
| 4D-03PR-E | FU-e5572fb7f3 | UNL-082/219 莉莉娅 | LILLIA_PLAY_UNIT_NO_OPTIONAL_HASTE | Runtime registry lines 2525-2536; fixtures p2-preflight-play-lillia-no-optional-haste / p4-play-lillia-haste-ready; rules evidence p2-preflight-play-lillia-no-optional-haste / p4-play-lillia-haste-ready. |
| 4D-03PS-E | FU-c2cad0d9f7 | UNL-082a/219 莉莉娅 | LILLIA_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE | Runtime registry lines 2538-2549; fixtures p2-preflight-play-lillia-alt-a-no-optional-haste / p4-play-lillia-alt-a-haste-ready; rules evidence p2-preflight-play-lillia-alt-a-no-optional-haste / p4-play-lillia-alt-a-haste-ready. |
| 4D-03PT-E | FU-6893d75a52 | UNL-088/219 倾颓宫殿 | CRUMBLING_PALACE_PLAY_EQUIPMENT | Runtime registry lines 1619-1625; fixtures p2-preflight-play-crumbling-palace-equipment / p4-play-crumbling-palace-target-rejected; rules evidence p2-preflight-play-crumbling-palace-equipment / p4-play-crumbling-palace-target-rejected. |
| 4D-03PU-E | FU-1076624b75 | UNL-089/219 烬 | JHIN_PLAY_UNIT_PREDICT | Runtime registry lines 5972-5984; fixture p2-preflight-play-jhin-predict-recycle; rules evidence p2-preflight-play-jhin-predict-recycle. |
| 4D-03PV-E | FU-16f3fb60b9 | UNL-089a/219 烬 | JHIN_ALT_A_PLAY_UNIT_PREDICT | Runtime registry lines 5986-5995; fixture p2-preflight-play-jhin-alt-a-predict-recycle; rules evidence p2-preflight-play-jhin-alt-a-predict-recycle. |

## Counts

Before -> after: all FU NEEDS_ENGINE_SUPPORT 491 -> 486; payment-cost 93 -> 88; primary residual 58 -> 53; targeting-stack-timing 244 -> 240; cleanup-replacement-duration 193 -> 189; hidden-info-random-zone 161 -> 159; payment-or-targeting-stack-timing 280 -> 275; payment-and-targeting-stack-timing 57 -> 53. NEEDS_AUTOMATED_TEST_EVIDENCE remains 328, NEEDS_FAQ_REVIEW remains 92, primary FAQ remains 61, fullOfficialTrue remains 0 and ready remains false.

## Closed / Open

Closed for the selected rows only: stage4B NEEDS_ENGINE_SUPPORT is removed from freezeStatus/statusFlags/fullOfficialBlockers and each selected row moves to IMPLEMENTED_UNTESTED.

Still open: automated evidence disposition for all selected rows, Haste optional ready breadth, equipment hidden-info/target-stack breadth, Predict recycle hidden/control/layer breadth, complete PaymentEngine / PAY_COST matrix, P0/P1 closure, frontend/browser/formal E2E, fullOfficial status, FAQ status and final readiness. Runtime/frontend/protocol/official catalog/general tests remain locked.
