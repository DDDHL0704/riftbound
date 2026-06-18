# Stage 4D-223BY Recovery Spectator Battle Damage Assignment Mismatch Details Audit

Status: accepted on 2026-06-19 05:30 CST.

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `8abd70e5`; docs checkpoint follows.

## Scope

This slice narrows recovery spectator replay-frame timing diagnostics for `battle.damageAssignment` authoritative mismatches. It keeps the existing broad diagnostic prefixes and appends stable expected/actual detail suffixes for pending flag, phase, battle id, battlefield object id, assigning player id, damage pool, legal targets, existing damage, lethal damage thresholds and required assignments.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

## Rule Source

Rule source was rechecked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs.

Relevant current anchors for this slice are latest core rules 142-143, 417 and 454-461, especially 460.2.c for battle damage assignment and 461 for battle cleanup, plus `裁判FAQ_251023.pdf` questions 6.1-6.4 for assigned damage versus dealt damage, lethal assignment caps and assignment-order choices. This slice only makes recovery diagnostics more actionable; it does not reinterpret damage assignment legality.

## Runtime Delta

`MatchRecoveryValidator` now appends `expected ... but got ...` details to spectator replay-frame timing battle damage assignment authoritative mismatch diagnostics while preserving the old diagnostic prefix. Missing keyed fields report `<missing>`, unreadable keyed values report `<unreadable>`, wrong readable scalars report the wrong value, int maps report canonical `{key: value}` details, string-list maps report canonical `{key: [value]}` details and required assignments report canonical assignment item details.

The changed validation remains diagnostic-only. It does not change valid recovery replay behavior, battle creation, battle damage assignment computation, damage legality, battle cleanup, battlefield control, prompt rendering, hidden-source redaction, source-object serialization, authoritative state serialization or random determinism.

## Test Coverage

Updated existing `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPendingFlagMissingPayload` now asserts a detailed `<missing>` suffix for absent pending flag mismatch.
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPhaseMissingPayload` now asserts a detailed `<missing>` suffix for absent phase mismatch.
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPendingMismatch`, `PhaseMismatch` and `IdentityMismatch` now assert detailed expected/actual suffixes for wrong readable scalar values.
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentDamagePoolMismatch`, `LegalTargetsMismatch`, `ExistingDamageMismatch`, `LethalThresholdMismatch` and `RequiredAssignmentsMismatch` now assert detailed expected/actual suffixes for map and required-assignment drift.

## Validation

Passed:

- Focused battle-damage-assignment mismatch detail tests: `10/10`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent Recovery/SpectatorReplayTiming/BattleDamageAssignment/DamageAssignment/Battle/BattlefieldTask/PendingTaskQueue/TriggerQueue/ContinuousEffect filter: `2714/2714`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src` and `tests`.

## Coordination

No subagent was created; A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 86 commits behind current `main` and `0` commits ahead at the pre-docs-sync divergence check. Exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; root PDF text remained available.

Project remains **NOT READY**.
