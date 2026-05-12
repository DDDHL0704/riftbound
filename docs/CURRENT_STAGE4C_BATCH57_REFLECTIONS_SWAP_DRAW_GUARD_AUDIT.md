# Stage 4C-57 Reflections Swap Draw Guard Audit

Status: **VERIFIED REPRESENTATIVE** test-only baseline; project **NOT READY**; `fullOfficial=false`.

Scope: Reflections / 镜中幻影 `UNL-083/219` / cardId `34618` / `FU-f0eb0fb704` / `REFLECTIONS_SWAP_FRIENDLY_UNITS_DRAW_1` ordinary hand play / pay 2 / two friendly public field units in different represented positions / at least one Ephemeral target / swap locations / draw 1 representative guard.

- Verified coverage: ordinary hand `PLAY_CARD`, paying 2 mana, selecting two friendly public field units represented by base + battlefield positions, at least one target with `瞬息`, stack / pass-pass resolution, `UNIT_LOCATIONS_SWAPPED`, and `CARD_DRAWN` 1.
- Verified command guard: no-Ephemeral pairs, same-position pairs, non-unit objects, hidden face-down standby objects, stale objects, enemy units, and dirty controller targets reject with `INVALID_TARGET` and no cost / stack / swap / draw mutation.
- Core repair: `CoreRuleEngine` now checks `HasValidSwapTargetLocations` before accepting `SwapsTargetLocations` play-card commands, so same-position pairs are rejected at command time instead of resolving as a no-op draw.
- Prompt parity repair: `MatchSession` now treats `AnyTargetRequiredTag` and `SwapsTargetLocations` as server target-selection constraints and emits `legalTargetSelections` filtered to Ephemeral-qualified different-position pairs.
- Validation: focused Reflections / Swap / FriendlyUnit filter passed 54/54; ActionPrompt / Prompt / Sand Soldiers / Reflections regression filter passed 112/112; backend full passed 3679/3679; frontend build passed; Chrome smoke passed.

Closure: 4C-57 closes only the Reflections representative swap/draw guard described above. It is not a full-official card implementation or project READY signal.

Holdback: this batch does not implement or claim full-official Reflections. Exact multi-battlefield / different-position precision beyond the current coarse base/battlefield representative model, standby / reaction and quick / spell-duel timing, full FEPR targeting / stack lifecycle, full movement / control-zone lifecycle, owner/controller split across all zones, hidden-info / redaction matrix, full target prompt matrix, PaymentEngine beyond ordinary pay 2, Ephemeral lifecycle / cleanup interactions, draw replacement / deck exhaustion, FAQ regression, 1009/811 full-official, and formal 18-step E2E remain open.
