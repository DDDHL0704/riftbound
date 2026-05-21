# 4D-03ID-E Jinx Discard-Two-Hand Blocker Closure Audit

Stage: 4D-03ID-E

Scope: E_CARD_MATRIX_READINESS row-level blocker reduction for `OGN·030/298` and `OGN·030a/298` 金克丝 / `FU-61ed447290` / `JINX_ALT_A_PLAY_UNIT_DISCARD_TWO_HAND;JINX_PLAY_UNIT_DISCARD_TWO_HAND`.

Evidence basis:

- Existing catalog fixture coverage for both Jinx discard-two-hand printings.
- Existing P4 HASTE_READY fixture coverage for both Jinx discard-two-hand printings.
- Existing rules evidence entry for the CORE-260330 p2 play/target/pay/resolve path.
- Focused Jinx / discard / Haste Ready regression and adjacent ActionPrompt / Reveal / Hide regression were run before the matrix transition.

Accepted matrix transition:

- `stage4B.freezeStatus`: `SHARED_ORACLE_IMPLEMENTATION -> SHARED_ORACLE_IMPLEMENTATION`.
- `stage4B.statusFlags`: remove `NEEDS_ENGINE_SUPPORT`.
- `stage4B.fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT+NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false` remains unchanged.
- `ready=false` remains unchanged.

Validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Jinx|FullyQualifiedName~DiscardTwo|FullyQualifiedName~DiscardHand|FullyQualifiedName~HasteReady"` passed 76/76.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~HideCard|FullyQualifiedName~RevealCard|FullyQualifiedName~Jinx"` passed 298/298.

Holdbacks: Jinx automated evidence disposition, complete hidden-info / random-zone visibility matrix, discard / draw replacement / deck exhaustion matrix, FEPR target / stack lifecycle, battle / spell-duel lifecycle, full PaymentEngine / PAY_COST matrix, 1009/811 full-official coverage, formal 18-step E2E, and READY remain open.
