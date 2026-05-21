# 4D-03HY-E Card Matrix Readiness Payment-Cost Sunken Temple Trigger-Payment Blocker Closure Candidate

4D-03HY-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the Sunken Temple trigger-payment battlefield-domain representative row. The selected functional unit is `FU-05ce012700`; selected card is `SFD·218/221` 沉没神庙; selected effect is `BATTLEFIELD_RULE_DOMAIN`.

This candidate removes `NEEDS_ENGINE_SUPPORT` from the selected functional unit and its single snapshot entry only. It preserves `NEEDS_FAQ_REVIEW`, `fullOfficial=false`, `fullOfficialTrue=0`, and `ready=false`.

Count continuity:

- payment-cost `NEEDS_ENGINE_SUPPORT`: 293 -> 292
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 481 -> 480
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 191 -> 190
- primary residual: 182 -> 182
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328
- `NEEDS_FAQ_REVIEW`: 92 -> 92

Evidence basis:

- Stage 4C-21 Sunken Temple trigger-payment evidence anchors the representative `TRIGGER_PAYMENT` / `PAY_COST` accept, decline, invalid, insufficient-mana, and no-powerful no-mutation paths.
- `TriggerPaymentTests` and `ConformanceFixtureRunnerTests` retain the authoritative service-side payment prompt coverage for `SFD·218/221`.
- Existing rules evidence, risk matrix, and CoreRuleEngine coverage anchor the representative battlefield-domain route.

Non-closure:

- Sunken Temple FAQ adjudication remains open.
- Complete conquer / powerful / battlefield lifecycle matrix remains open.
- LayerEngine effective-power and temporary modifier edge cases remain open.
- Battle/spell-duel and ASSIGN_COMBAT_DAMAGE full lifecycle remains open.
- Complete FEPR targeting, hidden-info breadth, full PaymentEngine, card matrix, and READY remain open.

Validation status: pending.
