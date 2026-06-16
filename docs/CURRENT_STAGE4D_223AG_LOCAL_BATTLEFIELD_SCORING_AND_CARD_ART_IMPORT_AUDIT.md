# Stage 4D-223AG Local Battlefield Scoring And Card Art Import Audit

Date: 2026-06-16 11:27 CST

Status: accepted as an import / validation checkpoint on local `main`. Project remains **NOT READY**.

## Imported Changes

- External commit `9f89e686 fix: restore local battlefield scoring flow` was inspected and accepted. It changes `RuleTextParsers` so printed unit power is not exposed as a play power cost, allows play-card unit prompts to use precise battlefield destination ids, applies static battlefield prevention to any `BATTLEFIELD:` destination, restores play-unit-to-controlled-battlefield legality, restores conquest scoring, restores held-at-turn-start battlefield scoring, and adds local playability regression tests for precise battlefield play, prompt destinations, conquest scoring, OGN-275 hold scoring and printed-power cost parsing.
- External commit `11afff1b feat: redesign battle page card presentation` was inspected and accepted. It extends `BehaviorSpec` with `FrontImage` / `BackImage`, passes catalog image urls into behavior specs, renders image-backed cards in `CardFace`, adds card zoom previews and fallback card frames, separates rune cards from base cards in `PlayerBoard`, adds a rune slot / rune-pool meter, and updates the battle-page layout styles.
- A_MAIN commit `f6e16230 style: remove orb gradients from battle surface` keeps the imported battle-page layout but replaces two decorative circular `radial-gradient` background layers with linear / repeating-linear texture layers to satisfy the active frontend-design rule against orb / bokeh decoration.
- A_MAIN commit `78580dcd feat: compact match table presentation` accepts the latest other-window compact match-frame work, adds battlefield-specific image-card treatment, restores the intended 56px icon rail at match width, and fixes shared `Button` accessible names so visually hidden text remains role-queryable.
- A_MAIN commit `037e8f6d style: add resync icon to match controls` accepts the latest other-window match-control icon polish by adding a `RotateCcw` icon to the resync button.

## Rule Source

The import was checked against `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`.

- Latest core rules 355.2 and 355.2.a: playing a unit requires a valid location; default valid locations include the controller's base or a battlefield the player controls.
- Latest core rules 356 and 131.1-131.4, plus 135.2.e.3: play costs are card costs and `[M]` in unit power is a power symbol, not a play power cost.
- Latest core rule 315.2.b.2: the turn player holds controlled battlefields at the scoring step.
- Latest core rules 348.2.a and 348.2.a.1: non-combat spell-duel control establishes battlefield control and conquest.
- Latest core rule 461.5.d: establishing battlefield control causes conquest if it has not already scored by conquest this turn.
- Latest core rules 463-466: battlefield scoring by conquest / hold happens at most once per battlefield per turn and triggers battlefield skills.
- Latest core rule 467: win checks are cleanup-based.
- Latest core rules 823.1.b and 823.1.c.1: Hunt has conquest and hold effects and grants experience.

## Validation

- Focused local-playability regression test `LocalPlayabilityRuleRegressionTests`: `5/5`.
- Adjacent runtime filter `LocalPlayabilityRuleRegressionTests|TurnStartReadiesObjectsTests|SpellDuelBattleStateMachineTests|BlueSentinelResourceSkillTests|TriggerPaymentTests|P79Battlefield|MoveUnit|DeclareBattle|Battlefield|PlayCard|RuleTextParser|CostParser`: `1088/1088`.
- Backend full `dotnet test Riftbound.slnx --no-restore`: `8267/8267`.
- DevUi production build after the frontend follow-ups: passed. EventLog labels cover 135 backend event kinds; user-facing fallback text check passed; Vite transformed 1803 modules. Only existing npm config warnings and Rollup SignalR pure-annotation warnings appeared.
- Browser validation against current local API `http://127.0.0.1:5188` and DevUi `http://127.0.0.1:5177`: Settings reported service online and 1009 catalog specs; `/cards` rendered 80 first-page image-backed cards with loaded nonzero natural sizes and no console warnings/errors; `/matches/local` connected to the current API, rendered `PlayerBoard` with rune slot / rune-pool meter / base / field / hand zones, used the intended `app-frame-match` 56px icon rail, preserved accessible names for hidden-label nav / command buttons, had no horizontal or vertical document overflow, and had no console warnings/errors.
- Mechanical checks before the CSS follow-up commit passed: `git diff --check` and anchored conflict-marker scan over `src`, `tests` and `docs`.
- Standing `rule-audit-remaining-20260615` cadence check after fetch showed no commits ahead of `origin/main`; worktree was behind only. DOC_MATRIX_CURRENT remained clean at `17bde0c3`.

## Negative Scope

This checkpoint does not claim final readiness. It does not close remaining runtime/server breadth, recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final status. The browser validation was a local DevUi/API smoke of Settings, Card Library and initial Match room rendering only; it was not a complete two-player gameplay smoke.
