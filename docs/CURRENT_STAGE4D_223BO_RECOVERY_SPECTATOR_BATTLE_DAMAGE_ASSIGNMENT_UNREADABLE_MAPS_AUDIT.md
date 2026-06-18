# Stage 4D-223BO Recovery Spectator Battle Damage Assignment Unreadable Maps Audit

Date: 2026-06-19 03:28 CST

Owner: `A_MAIN`

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `dcc996a4`; docs checkpoint follows.

## Scope

Stage 4D-223BO narrows one recovery spectator replay timing diagnostic gap in `battle.damageAssignment`.

- Runtime changed: yes, narrow recovery validation diagnostic only.
- Frontend changed: no.
- `MatchRecoveryValidator` now reports field-level authoritative mismatch diagnostics when an open-window spectator `battle.damageAssignment` top-level map payload is missing, null, or not readable for `damagePool`, `legalTargets`, `existingDamage`, or `lethalDamageThreshold` while the authoritative battle damage assignment state has the corresponding non-empty map.
- Existing required-map, payload-shape and count-drift diagnostics are preserved.

## Rule Authority Checked

Rule source was checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, and extracted text in `/tmp/riftbound_rules_pdf_text/` from the five root PDFs.

- Latest core rules 417.1.a, 417.3.a and 417.6.c: assigned combat damage is not dealt until assignment completes, and combat damage caused by assignment is sourced by units.
- Latest core rules 460.2.c-d: battle damage assignment order, lethal damage assignment, restrictions/requirements and simultaneous damage dealing.
- Latest core rules 815 and 826: `壁垒` and `后排` modify battle damage assignment legality and ordering.
- `裁判FAQ_251023.pdf` questions 6.1-6.5: battle damage assignment interactions and priority/requirement handling.

This slice changes recovery validation diagnostics only; it does not change battle damage assignment legality or valid replay behavior.

## Validation

Passed:

- Focused battle-damage-assignment missing/null/map-payload-shape filter: `5/5`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent BattleDamageAssignment/AssignCombatDamage/Battle/SpectatorReplayTiming/Recovery filter: `2670/2670`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src`, and `tests`.

## Coordination

No subagent was created; A_MAIN continued directly on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 55 commits behind current `main` after the code commit and with no commits ahead of `main`.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`.

Project remains **NOT READY**.
