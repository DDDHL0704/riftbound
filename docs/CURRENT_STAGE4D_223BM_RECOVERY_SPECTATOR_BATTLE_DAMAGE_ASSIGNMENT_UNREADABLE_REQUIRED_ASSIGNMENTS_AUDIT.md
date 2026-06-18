# Stage 4D-223BM Recovery Spectator Battle Damage Assignment Unreadable Required Assignments Audit

Date: 2026-06-19
Owner: A_MAIN
Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
Branch: `main`
Code commit: `1da7d545`

## Scope

This shard narrows recovery spectator replay timing validation for same-count unreadable `battle.damageAssignment.requiredAssignments` item payloads. It does not change combat damage assignment legality, battle resolution, damage dealing, valid replay behavior, prompt rendering, hidden information redaction, random determinism, frontend behavior, database behavior, `fullOfficial`, formal E2E, Chrome smoke, or final readiness.

## Runtime Change

`MatchRecoveryValidator` now reports an authoritative required-assignments mismatch when `battle.damageAssignment.requiredAssignments` is readable as a list with the authoritative count, but one or more item payloads cannot be parsed because required item fields are missing, null, or otherwise unreadable while authoritative state has an open battle damage assignment window.

Readable required-assignment value drift remains covered by the existing value-drift tests. Required-field diagnostics remain intact.

## Test Coverage

Existing `MatchRecoveryTests` now assert the new required-assignments authoritative mismatch diagnostic while preserving item required-field diagnostics:

- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentRequiredAssignmentItemMissingFields`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentRequiredAssignmentItemNullFields`

The focused filter also matched the snapshot-timing companions, so the focused run covered `4/4`.

## Rule Authority

Rule source was checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs:

- latest core rules 417.1.a, 417.3.a and 417.6.c
- latest core rules 460.2.c-d
- latest core rules 815 and 826
- `裁判FAQ_251023.pdf` questions 6.1-6.5

## Validation

- Focused battle-damage-assignment required-assignment item missing/null field filter: `4/4`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent BattleDamageAssignment/AssignCombatDamage/Battle/SpectatorReplayTiming/Recovery filter: `2670/2670`
- Backend full via `Riftbound.slnx`: `8307/8307`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no findings before docs sync

Project remains **NOT READY**.
