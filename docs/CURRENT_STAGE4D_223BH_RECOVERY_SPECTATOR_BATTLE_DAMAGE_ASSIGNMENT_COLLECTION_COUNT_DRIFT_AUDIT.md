# Stage 4D-223BH Recovery Spectator Battle Damage Assignment Collection Count Drift Audit

Date: 2026-06-19
Owner: A_MAIN
Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
Branch: `main`
Pre-slice fast-forward: `77d9276b` (`重构对战桌面线框里程碑`)
Code commit: `8b43cfea`

## Scope

This slice narrows recovery spectator replay timing validation for `battle.damageAssignment` when the payload fields are readable collections but their counts differ from the authoritative open battle damage assignment window.

Runtime changed: yes, narrow recovery validation diagnostic only.
Frontend changed: no in this A_MAIN slice. The incoming UI/tabletop milestone `77d9276b` was fast-forwarded into local `main` before this server slice was opened.

## Runtime Change

`MatchRecoveryValidator` now reports actual spectator collection counts for readable `battle.damageAssignment` payload fields when they differ from authoritative state:

- `damagePool`
- `legalTargets`
- `existingDamage`
- `lethalDamageThreshold`
- `requiredAssignments`

Missing, null, non-object, and malformed field-shape cases continue to report count `0` where that behavior already existed. The change does not alter valid recovery replay behavior, battle damage assignment legality, battle resolution, damage dealing, or UI rendering.

## Rule Evidence

Rule source was checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, and `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs. Relevant anchors remain core rules 417.1.a, 417.3.a, 417.6.c, 460.2.c-d, 815 and 826, plus `裁判FAQ_251023.pdf` questions 6.1-6.5.

## Validation

- Focused battle-damage-assignment collection-count/missing/map-shape filter: `3/3`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent BattleDamageAssignment/AssignCombatDamage/Battle/SpectatorReplayTiming/Recovery filter: `2670/2670`
- Backend full via `Riftbound.slnx`: `8307/8307`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src`, and `tests`: no findings

## Coordination

No subagent was created. A_MAIN continued directly on local `main` per user request.

A_MAIN first fetched and fast-forwarded incoming `origin/main` commit `77d9276b` before the code slice. External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 39 commits behind current `main` after the code commit and with no commits ahead of `main`.

`codex/rule-audit-remaining-20260615` had no new commits ahead of `main`.

Project remains **NOT READY**.
