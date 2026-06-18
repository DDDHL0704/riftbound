# Stage 4D-223BN Recovery Spectator Battle Damage Assignment Top-Level Required Assignments Audit

Date: 2026-06-19
Owner: A_MAIN
Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
Branch: `main`
Code commit: `7b3b92b6`

## Scope

This shard narrows recovery spectator replay timing validation for top-level unreadable `battle.damageAssignment.requiredAssignments` payloads. It does not change combat damage assignment legality, battle resolution, damage dealing, valid replay behavior, prompt rendering, hidden information redaction, random determinism, frontend behavior, database behavior, `fullOfficial`, formal E2E, Chrome smoke, or final readiness.

## Runtime Change

`MatchRecoveryValidator` now reports an authoritative required-assignments mismatch when authoritative state has an open battle damage assignment window but spectator `battle.damageAssignment.requiredAssignments` is missing, null, or not readable as a list.

Same-count unreadable required-assignment item drift remains covered by 223BM. Required/list/count diagnostics remain intact.

## Test Coverage

Existing `MatchRecoveryTests` now assert the new required-assignments authoritative mismatch diagnostic while preserving required/list/count diagnostics:

- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPayloadShapeDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentMissingRequiredAssignments`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentNullRequiredAssignments`

The focused filter also matched snapshot-timing companions, so the focused run covered `5/5`.

## Rule Authority

Rule source was checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs:

- latest core rules 417.1.a, 417.3.a and 417.6.c
- latest core rules 460.2.c-d
- latest core rules 815 and 826
- `裁判FAQ_251023.pdf` questions 6.1-6.5

## Validation

- Focused battle-damage-assignment required-assignments payload-shape/missing/null filter: `5/5`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent BattleDamageAssignment/AssignCombatDamage/Battle/SpectatorReplayTiming/Recovery filter: `2670/2670`
- Backend full via `Riftbound.slnx`: `8307/8307`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no findings before docs sync

Project remains **NOT READY**.
