# Stage 4D-223CF Recovery Battle Damage Assignment Object Reference Details Audit

Status time: 2026-06-19 06:41 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch: `main`

Code commit: `131b740a`

Project status: **NOT READY**

## Scope

This slice narrows recovery snapshot and spectator replay timing battle damage assignment diagnostics only. `MatchRecoveryValidator` now appends stable `expected ... but got ...` details when battle damage assignment object references are missing from the player snapshot `objects` payload or the authoritative spectator object registry.

Covered object-reference surfaces:

- `damageAssignment.battlefieldId`.
- `damagePool` source object ids.
- `legalTargets` source object ids and legal target object ids.
- `existingDamage` object ids.
- `lethalDamageThreshold` object ids.
- `requiredAssignments[].sourceObjectId`.
- `requiredAssignments[].legalTargetObjectIds`.

Runtime behavior changed only for validation diagnostic text. Valid recovery replay behavior, battle creation, battle damage assignment computation, damage legality, simultaneous damage, battle cleanup, battlefield control, prompt rendering, redaction, authoritative state serialization and random determinism were not changed.

## Rule Gate

Rule source checked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs.

Relevant anchors:

- Latest core rules 120-124 for game-object identity.
- Latest core rules 142-143 and 417 for unit power, damage and combat damage assignment context.
- Latest core rules 454-461 for battle lifecycle, participant identity, battle damage assignment and cleanup.
- Latest core rule 460.2.c for player damage assignment during the battle damage step.
- Established battle-damage-assignment FAQ gate 6.1-6.4.

## Implementation

`src/Riftbound.Engine/MatchRecovery.cs`:

- `ValidateBattleDamageAssignmentObjectReferences` now routes all battle-damage-assignment object reference checks through dedicated detail helpers.
- Missing snapshot object references preserve the existing `is missing from objects` prefix and append the sorted expected object id set plus the offending object id.
- Missing spectator object references preserve the existing `is missing from object registry` prefix and append the sorted authoritative registry set plus the offending object id.
- Generic timing object reference helpers were left unchanged to avoid widening this diagnostic text change beyond the battle damage assignment surface.

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- `RecoveryValidatorRejectsSnapshotTimingBattleDamageAssignmentObjectReferencesOutsideSnapshotObjects` now asserts detailed suffixes for all eight covered object-reference paths.
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentObjectReferencesOutsideRegistry` now asserts detailed suffixes for all eight covered object-reference paths.

## Validation

Passed:

- Focused battle-damage-assignment object-reference detail tests: `2/2`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent Recovery/SpectatorReplayTiming/BattleDamageAssignment/DamageAssignment/Battle/BattlefieldTask/PendingTaskQueue/TriggerQueue/ContinuousEffect filter: `2714/2714`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src` and `tests`.

## Coordination

No subagent was created. A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 108 commits behind the post-code `main` and `0` commits ahead at the pre-docs-sync divergence check. A_MAIN must inspect it before any UI followup integration and must not develop directly there.

`codex/rule-audit-remaining-20260615` had no new commits ahead of `main` at this batch. Continue the standing cadence check before future integration and before push.

## Remaining Open

This slice does not close Stage 4D. Open areas remain, including but not limited to remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 and final readiness.
