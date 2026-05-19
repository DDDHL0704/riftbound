# 4D-03HW-E Card Matrix Readiness Payment-Cost Glasc Bartender Last-Breath Blocker Closure Candidate

4D-03HW-E records a single row-level E_CARD_MATRIX_READINESS blocker reduction for the Glasc Bartender last-breath representative row. The selected functional unit is `FU-16d3a6dd4e`; selected card is `SFD·165/221` 戈拉斯克调酒师; selected effect is `GLASC_BARTENDER_LAST_BREATH_PLAY_UNIT`.

This candidate removes `NEEDS_ENGINE_SUPPORT` from the selected functional unit and its single snapshot entry only. It preserves `NEEDS_FAQ_REVIEW`, `fullOfficial=false`, `fullOfficialTrue=0`, and `ready=false`.

Count continuity:

- payment-cost `NEEDS_ENGINE_SUPPORT`: 295 -> 294
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 483 -> 482
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 193 -> 192
- primary residual: 182 -> 182
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328
- `NEEDS_FAQ_REVIEW`: 92 -> 92

Evidence basis:

- P2 and P4 Glasc Bartender conformance fixtures anchor the representative play-unit and target-rejection routes.
- Stage 4C-1 records order-trigger blocker evidence for the selected last-breath pressure row.
- Existing registry, rules evidence, P2/P4 status docs, and conformance fixture coverage anchor the representative route.

Non-closure:

- Glasc Bartender FAQ adjudication remains open.
- Last Breath trigger ordering remains open.
- Cleanup/replacement duration breadth remains open.
- Hidden-info / random-zone visibility breadth remains open.
- Complete FEPR targeting, full PaymentEngine, card matrix, and READY remain open.

Validation status: jq empty passed; PaymentEngineCoverageAuditTests 430/430 passed; dotnet test Riftbound.slnx --no-restore 5001/5001 passed; git diff --check passed.
