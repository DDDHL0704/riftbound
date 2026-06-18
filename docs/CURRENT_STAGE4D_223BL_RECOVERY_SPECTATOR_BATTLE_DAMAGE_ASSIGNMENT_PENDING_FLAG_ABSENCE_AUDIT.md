# Stage 4D-223BL Recovery Spectator Battle Damage Assignment Pending Flag Absence Audit

Date: 2026-06-19
Owner: A_MAIN
Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
Branch: `main`
Code commit: `7e991097`

## Scope

This shard narrows recovery spectator replay timing validation for open-window `battle.damageAssignment.isPending` absence drift. It does not change combat damage assignment legality, battle resolution, damage dealing, valid replay behavior, prompt rendering, hidden information redaction, random determinism, frontend behavior, database behavior, `fullOfficial`, formal E2E, Chrome smoke, or final readiness.

## Runtime Change

`MatchRecoveryValidator` now reports an authoritative pending-flag mismatch when `battle.damageAssignment.isPending` is missing, null, or otherwise unreadable while authoritative state has an open battle damage assignment window.

Readable pending-flag drift remains covered by 223BJ. Required-field diagnostics remain intact.

## Test Coverage

Existing `MatchRecoveryTests` now assert the new pending-flag authoritative mismatch diagnostic while preserving the required-field diagnostic:

- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPendingFlagMissingPayload`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPendingFlagNullPayload`

## Rule Authority

Rule source was checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs:

- latest core rules 417.1.a, 417.3.a and 417.6.c
- latest core rules 460.2.c-d
- latest core rules 815 and 826
- `裁判FAQ_251023.pdf` questions 6.1-6.5

## Validation

- Focused battle-damage-assignment pending flag missing/null drift filter: `2/2`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent BattleDamageAssignment/AssignCombatDamage/Battle/SpectatorReplayTiming/Recovery filter: `2670/2670`
- Backend full via `Riftbound.slnx`: `8307/8307`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no findings before docs sync

Project remains **NOT READY**.
