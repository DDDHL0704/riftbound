# Stage 4D-223CB Recovery Battle Damage Assignment Identity Details Audit

Status time: 2026-06-19 06:04 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `5468b96f`

Project status: **NOT READY**

## Scope

This slice narrows recovery snapshot and spectator replay timing battle damage assignment diagnostics only. `MatchRecoveryValidator` now appends stable `expected ... but got ...` details to identity-consistency diagnostics that compare embedded `battle.damageAssignment` identity fields with the enclosing `battle` payload.

Covered identity surfaces:

- `damageAssignment.battleId` versus enclosing `battle.battleId`.
- `damageAssignment.battlefieldId` versus enclosing `battle.battlefieldObjectId`.

Runtime behavior changed only for validation diagnostic text. Valid recovery replay behavior, battle creation, battle damage assignment computation, damage legality, simultaneous damage, battle cleanup, battlefield control, prompt rendering, redaction, authoritative state serialization and random determinism were not changed.

## Rule Gate

Rule source checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs.

Relevant anchors:

- Latest core rules 142-143 for damage and unit combat power / damage thresholds.
- Latest core rules 417 and 417.1.a / 417.3.a / 417.6.c for assignment versus damage being caused and combat-damage source attribution.
- Latest core rules 454-461 for battle lifecycle and identity context.
- Latest core rules 459-461 for battle opening, damage assignment and cleanup/result sequencing.
- Latest core rule 460.2.c for player damage assignment during the battle damage step.
- Established battle-damage-assignment FAQ gate 6.1-6.4.

## Implementation

`src/Riftbound.Engine/MatchRecovery.cs`:

- `ValidateBattleDamageAssignmentIdentityConsistency` now appends `FormatExpectedActualForRecovery(normalizedBattleValue, normalizedDamageAssignmentValue)`.
- Existing prefix text is unchanged, preserving older broad assertions and log-search compatibility.

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSnapshotTimingBattleDamageAssignmentIdentityInconsistentWithBattle` now asserts detailed suffixes for snapshot identity drift.
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentIdentityInconsistentWithBattle` now asserts detailed suffixes for spectator timing identity drift.

## Validation

Passed:

- Focused battle-damage-assignment identity detail tests: `2/2`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent Recovery/SpectatorReplayTiming/BattleDamageAssignment/DamageAssignment/Battle/BattlefieldTask/PendingTaskQueue/TriggerQueue/ContinuousEffect filter: `2714/2714`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src` and `tests`.

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 97 commits behind current `main` and `0` commits ahead at the pre-docs-sync divergence check. A_MAIN must inspect it before any UI followup integration and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no new commits ahead of `main` at this batch. Continue the standing cadence check before future integration and before push.

## Remaining Open

This slice does not close Stage 4D. Open areas remain, including but not limited to remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 and final readiness.
