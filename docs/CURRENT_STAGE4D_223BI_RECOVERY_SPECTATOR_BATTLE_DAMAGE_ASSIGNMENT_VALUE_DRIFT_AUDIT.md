# Stage 4D-223BI Recovery Spectator Battle Damage Assignment Value Drift Audit

Date: 2026-06-19
Owner: A_MAIN
Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
Branch: `main`
Code commit: `e234ebd3`

## Scope

This shard narrows recovery spectator replay timing validation for `battle.damageAssignment` readable value drift. It does not change combat damage assignment legality, battle resolution, damage dealing, valid replay behavior, prompt rendering, hidden information redaction, random determinism, frontend behavior, database behavior, `fullOfficial`, formal E2E, Chrome smoke, or final readiness.

## Runtime Change

`MatchRecoveryValidator` now compares readable spectator `battle.damageAssignment` values against the authoritative open battle damage assignment window for:

- `damagePool`
- `legalTargets`
- `existingDamage`
- `lethalDamageThreshold`
- `requiredAssignments`

When a readable field has the correct collection shape and count but the values do not match the authoritative state, validation now emits a field-level diagnostic instead of relying only on `spectator replay frame timing battle does not match authoritative state battle`.

## Test Coverage

Existing `MatchRecoveryTests` battle-damage-assignment field mismatch tests now assert the new field-level diagnostics while preserving the broad battle mismatch guard:

- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentDamagePoolMismatch`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentLegalTargetsMismatch`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentExistingDamageMismatch`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentLethalThresholdMismatch`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentRequiredAssignmentsMismatch`

## Rule Authority

Rule source was checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` from the five root PDFs:

- latest core rules 417.1.a, 417.3.a and 417.6.c
- latest core rules 460.2.c-d
- latest core rules 815 and 826
- `裁判FAQ_251023.pdf` questions 6.1-6.5

## Validation

- Focused battle-damage-assignment value-drift filter: `5/5`
- Changed-class `MatchRecoveryTests`: `1974/1974`
- Adjacent BattleDamageAssignment/AssignCombatDamage/Battle/SpectatorReplayTiming/Recovery filter: `2670/2670`
- Backend full via `Riftbound.slnx`: `8307/8307`
- `git diff --check`: passed
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no findings before docs sync

Project remains **NOT READY**.
