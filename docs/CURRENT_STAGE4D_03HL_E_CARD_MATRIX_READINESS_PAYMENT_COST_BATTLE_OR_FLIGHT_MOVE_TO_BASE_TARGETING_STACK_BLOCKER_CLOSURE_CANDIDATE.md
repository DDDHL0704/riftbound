# 4D-03HL-E Card Matrix Readiness Payment-Cost Battle Or Flight Move-To-Base Targeting-Stack Blocker Closure Candidate

日期：2026-05-19
结论：**ACCEPTED FOR THIS ROW / NOT READY**

本批只记录 `FU-813144e7d4 / OGN·168/298 战或逃 / BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE` 的 row-level matrix blocker reduction。它复用已有 Stage 4C-28 representative move battlefield unit -> owner base target-guard evidence、BattleOrFlight conformance tests、preflight fixture、registry、rules evidence 与 runtime target guard 证据，把这一行从 `IMPLEMENTED_TESTED + NEEDS_ENGINE_SUPPORT + NEEDS_FAQ_REVIEW` 收窄到 `IMPLEMENTED_TESTED + NEEDS_FAQ_REVIEW`。

## Selected Row

- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-and-targeting-stack-timing`
- selected functionalUnit: `FU-813144e7d4`
- selected card: `OGN·168/298 战或逃`
- selected effect: `BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE`

## Count Impact

- `NEEDS_ENGINE_SUPPORT`: 306 -> 305
- primary residual: 182 -> 182
- `payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 494 -> 493
- `payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 204 -> 203
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328
- `NEEDS_FAQ_REVIEW`: 92 -> 92
- primary FAQ residual: 61 -> 61
- `fullOfficialTrue`: 0 -> 0
- `ready`: false -> false

## Non-Closure

- full Battle or Flight swift / standby reaction timing remains open
- complete spell-duel / battle lifecycle matrix remains open
- complete FEPR target selection and target legality matrix remains open
- complete ZoneOwnership / ControlChange / Movement matrix remains open
- complete hidden / face-down / standby target visibility model remains open
- FAQ adjudication for JFAQ-251023 p4, SOUL-JFAQ-260114 p12/p16 remains open
- full PaymentEngine / PAY_COST matrix remains open
- FEPR / targeting-timing breadth remains open
- B/D_ENGINE_SUPPORT payment-cost residual remains open
- A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE payment-cost residual remains open
- E_CARD_MATRIX_FAQ_REVIEW payment-cost residual remains open
- E_CARD_MATRIX_READINESS remains open
- READY remains open
