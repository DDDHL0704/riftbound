# Stage 4D-223CA Recovery Spectator Battle Damage Assignment Count Details Audit

Status time: 2026-06-19 05:52 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `eb13d311`

Project status: **NOT READY**

## Scope

This slice narrows recovery spectator replay timing battle damage assignment diagnostics only. `MatchRecoveryValidator` now appends stable `expected ... but got ...` details to battle damage assignment collection/count mismatch diagnostics while preserving the existing diagnostic prefix.

Covered count surfaces:

- missing, null, or unreadable `battle.damageAssignment` payloads.
- `damagePool` count drift.
- `legalTargets` count drift.
- `existingDamage` count drift.
- `lethalDamageThreshold` count drift.
- `requiredAssignments` count drift.

Runtime behavior changed only for validation diagnostic text. Valid recovery replay behavior, battle creation, battle damage assignment computation, damage legality, simultaneous damage, battle cleanup, battlefield control, prompt rendering, redaction, authoritative state serialization and random determinism were not changed.

## Rule Gate

Rule source checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs.

Relevant anchors:

- Latest core rules 142-143 for damage and unit combat power / damage thresholds.
- Latest core rules 417 and 417.1.a / 417.3.a / 417.6.c for assignment versus damage being caused and combat-damage source attribution.
- Latest core rules 454-461 for battle lifecycle.
- Latest core rule 460.2.c, especially 460.2.c.3-460.2.c.5, for required damage assignment, lethal damage ordering and assignment requirements / restrictions.
- Latest core rule 461 for battle cleanup and result handling.
- Established battle-damage-assignment FAQ gate 6.1-6.4.

## Implementation

`src/Riftbound.Engine/MatchRecovery.cs`:

- `AddSpectatorBattleDamageAssignmentCountDiagnostic` now appends `FormatExpectedActualForRecovery(authoritativeCount, spectatorCount)` to count mismatch diagnostics.
- Existing prefix text is unchanged, preserving older broad assertions and log-search compatibility.

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentMissingPayloadWithCountMismatch` now asserts `expected 2 but got 0` suffixes.
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentCollectionCountMismatch` now asserts `expected 2 but got 1` suffixes.

## Validation

Passed:

- Focused battle-damage-assignment count detail tests: `2/2`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent Recovery/SpectatorReplayTiming/BattleDamageAssignment/DamageAssignment/Battle/BattlefieldTask/PendingTaskQueue/TriggerQueue/ContinuousEffect filter: `2714/2714`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src` and `tests`.

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 92 commits behind current `main` and `0` commits ahead at the pre-docs-sync divergence check. A_MAIN must inspect it before any UI followup integration and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no new commits ahead of `main` at this batch. Continue the standing cadence check before future integration and before push.

## Remaining Open

This slice does not close Stage 4D. Open areas remain, including but not limited to remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 and final readiness.
