# Stage 4D-223BG Recovery Spectator Battle Damage Assignment Shape Field Count Drift Audit

Date: 2026-06-18
Owner: A_MAIN
Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
Branch: `main`
Code commit: `a0d82e22`

## Scope

This slice narrows recovery spectator replay timing validation for `battle.damageAssignment` when the payload itself is an object but a nested required battle damage assignment map/list exists with the wrong payload shape.

Runtime changed: yes, narrow recovery validation diagnostic only.
Frontend changed: no.

## Runtime Change

`MatchRecoveryValidator` now treats malformed nested required payloads as count `0` for authoritative count-drift diagnostics when any of these spectator payload fields cannot be parsed as the expected map/list shape:

- `damagePool`
- `legalTargets`
- `existingDamage`
- `lethalDamageThreshold`
- `requiredAssignments`

The existing payload-shape error is preserved. The change does not alter valid recovery replay behavior, battle damage assignment legality, battle resolution, or damage dealing.

## Rule Evidence

Rule source was checked through `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs. Relevant anchors remain core rules 417.1.a, 417.3.a, 417.6.c, 460.2.c-d, 815 and 826, plus `裁判FAQ_251023.pdf` questions 6.1-6.5.

## Validation

- Focused battle-damage-assignment payload-shape field filter: `2/2`
- Changed-class `MatchRecoveryTests`: `1973/1973`
- Adjacent BattleDamageAssignment/AssignCombatDamage/Battle/SpectatorReplayTiming/Recovery filter: `2669/2669`
- Backend full via `Riftbound.slnx`: `8305/8305`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src`, and `tests`: no findings

## Coordination

No subagent was created. A_MAIN continued directly on local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 36 commits behind current `main` after the code commit and with no commits ahead of `main`.

`codex/rule-audit-remaining-20260615` had no new commits ahead of `main`.

Project remains **NOT READY**.
