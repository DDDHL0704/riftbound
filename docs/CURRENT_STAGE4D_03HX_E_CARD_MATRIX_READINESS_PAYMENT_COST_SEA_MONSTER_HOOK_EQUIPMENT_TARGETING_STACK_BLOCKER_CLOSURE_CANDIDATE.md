# 4D-03HX-E Card Matrix Readiness Payment-Cost Sea Monster Hook Equipment Blocker Closure Candidate

4D-03HX-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the Sea Monster Hook equipment representative row. The selected functional unit is `FU-2653af0380`; selected card is `OGN·242/298` 海兽钓钩; selected effect is `SEA_MONSTER_HOOK_PLAY_EQUIPMENT`.

This candidate removes `NEEDS_ENGINE_SUPPORT` from the selected functional unit and its single snapshot entry only. It preserves `NEEDS_FAQ_REVIEW`, `NEEDS_AUTOMATED_TEST_EVIDENCE`, `fullOfficial=false`, `fullOfficialTrue=0`, and `ready=false`.

Count continuity:

- payment-cost `NEEDS_ENGINE_SUPPORT`: 294 -> 293
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 482 -> 481
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 192 -> 191
- primary residual: 182 -> 182
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328
- `NEEDS_FAQ_REVIEW`: 92 -> 92

Evidence basis:

- Stage 4C-40 Sea Monster Hook play-equipment guard evidence anchors the representative play-equipment route.
- P2 and P4 Sea Monster Hook conformance fixtures anchor the representative equipment play and target-rejection routes.
- Existing registry, rules evidence, P2/P4 status docs, and conformance fixture coverage anchor the representative route.

Non-closure:

- Sea Monster Hook FAQ adjudication remains open.
- Activated ability pay 1 yellow exhaust destroy friendly unit remains open.
- Top-five look / choice / free-play / recycle route remains open.
- Complete equipment layer and continuous-effect matrix remains open.
- Hidden-info / random-zone visibility breadth remains open.
- Complete FEPR targeting, full PaymentEngine, card matrix, and READY remain open.

Validation status: jq empty passed; PaymentEngineCoverageAuditTests 432/432 passed; dotnet test Riftbound.slnx --no-restore 5003/5003 passed; git diff --check passed.
