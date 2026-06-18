# Stage 4D-223BF Recovery Spectator Battle Damage Assignment Field Count Drift Audit

Date: 2026-06-18
Owner: A_MAIN
Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
Branch: `main`
Code commit: `dae5ef04`

## Scope

This slice narrows recovery spectator replay timing validation for `battle.damageAssignment` when the payload itself is an object but a nested required battle damage assignment map/list is missing or null.

Runtime changed: yes, narrow recovery validation diagnostic only.
Frontend changed: no.

## Runtime Change

`MatchRecoveryValidator` now reports the corresponding count `0` drift against authoritative open battle damage assignment window dimensions when any of these spectator nested payload fields is missing or null:

- `damagePool`
- `legalTargets`
- `existingDamage`
- `lethalDamageThreshold`
- `requiredAssignments`

The existing required map/list error is preserved. The change does not alter valid recovery replay behavior, battle damage assignment legality, battle resolution, or damage dealing.

## Rule Evidence

Rule source was checked through `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs. Relevant anchors remain core rules 417.1.a, 417.3.a, 417.6.c, 460.2.c-d, 815 and 826, plus `裁判FAQ_251023.pdf` questions 6.1-6.5.

## Validation

- Focused battle-damage-assignment nested required field filter: `4/4`
- Changed-class `MatchRecoveryTests`: `1973/1973`
- Adjacent BattleDamageAssignment/AssignCombatDamage/Battle/SpectatorReplayTiming/Recovery filter: `2669/2669`
- Backend full via `Riftbound.slnx`: `8305/8305`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src`, and `tests`: no findings

## Coordination

No subagent was created. A_MAIN continued directly on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 34 commits behind current `main` after the code commit and with no commits ahead of `main`.

`codex/rule-audit-remaining-20260615` had no new commits ahead of `main`.

Project remains **NOT READY**.
