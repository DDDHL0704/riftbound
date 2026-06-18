# Stage 4D-223CE Recovery Battle Active Participant Details Audit

Status time: 2026-06-19 06:28 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `3c63f744`

Project status: **NOT READY**

## Scope

This slice narrows recovery snapshot and spectator replay timing battle diagnostics only. `MatchRecoveryValidator` now appends stable `expected ... but got ...` details when the battle active flag is inconsistent with the enclosing battle participant set.

Covered battle active / participant surfaces:

- `isActive == true` with no `attackerObjectIds` / `defenderObjectIds`.
- `isActive == false` with present battle participants.

Runtime behavior changed only for validation diagnostic text. Valid recovery replay behavior, battle creation, battle damage assignment computation, damage legality, simultaneous damage, battle cleanup, battlefield control, prompt rendering, redaction, authoritative state serialization and random determinism were not changed.

## Rule Gate

Rule source checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs.

Relevant anchors:

- Latest core rules 454-461 for battle lifecycle and battle participant identity.
- Latest core rule 457 for battle involving two and only two players' controlled units.
- Latest core rules 459.2.b.1-459.2.b.4 for attacker/defender player and unit identities.
- Latest core rule 460.2.c for player damage assignment during the battle damage step.
- Established battle-damage-assignment FAQ gate 6.1-6.4.

## Implementation

`src/Riftbound.Engine/MatchRecovery.cs`:

- `ValidateBattleActiveParticipantConsistency` now computes sorted actual participant object ids.
- Active battles with no participants append `expected <non-empty> but got []`.
- Inactive battles with participants append `expected [] but got [participant ids]`.
- Existing prefix text is unchanged, preserving older broad assertions and log-search compatibility.

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSnapshotTimingBattleActiveParticipantConsistencyDrift` now asserts the active/no-participants detail suffix.
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleActiveParticipantConsistencyDrift` now asserts the inactive/participants detail suffix.

## Validation

Passed:

- Focused battle active participant detail tests: `2/2`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent Recovery/SpectatorReplayTiming/BattleDamageAssignment/DamageAssignment/Battle/BattlefieldTask/PendingTaskQueue/TriggerQueue/ContinuousEffect filter: `2714/2714`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src` and `tests`.

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 103 commits behind the post-code `main` and `0` commits ahead at the pre-docs-sync divergence check. A_MAIN must inspect it before any UI followup integration and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no new commits ahead of `main` at this batch. Continue the standing cadence check before future integration and before push.

## Remaining Open

This slice does not close Stage 4D. Open areas remain, including but not limited to remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 and final readiness.
