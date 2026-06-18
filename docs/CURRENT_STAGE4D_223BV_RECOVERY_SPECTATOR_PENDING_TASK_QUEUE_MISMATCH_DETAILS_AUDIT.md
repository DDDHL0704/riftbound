# Stage 4D-223BV Recovery Spectator Pending Task Queue Mismatch Details Audit

Status: accepted on 2026-06-19 04:53 CST.

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / commit: `main` code commit `59dae727`; docs checkpoint follows.

## Scope

This slice narrows recovery spectator replay-frame timing diagnostics for `pendingTaskQueue` keyed authoritative state mismatches. It keeps the existing broad diagnostic prefixes and appends stable expected/actual detail suffixes for keyed task `kind`, `reason`, `playerId`, `battlefieldObjectId`, `objectId`, `hiddenObject` and `hiddenObjectKind` drift.

Runtime changed: yes, narrow recovery validation diagnostic detail only.

Frontend changed: no.

## Rule Source

Rule source was rechecked through `docs/rules-authority-and-audit.md`, `docs/rules-evidence-index.md`, `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/` extracted from the five root PDFs.

Relevant current anchors for this slice are latest core rules 316.4-316.5, 323.9-323.14, 334-335 and 454-455/458, covering pending battlefield/battle/spell-duel task lifecycle, queueing and ordered task execution semantics that the authoritative recovery payload must preserve.

## Runtime Delta

`MatchRecoveryValidator` now appends `expected ... but got ...` details to spectator replay-frame timing pending task queue keyed mismatch diagnostics while preserving the old diagnostic prefix. Missing keyed fields now report `<missing>`, unreadable keyed values report `<unreadable>`, and wrong readable strings/bools report their actual value.

The changed validation remains diagnostic-only. It does not change valid recovery replay behavior, queue construction, task execution, battle or spell-duel start, trigger ordering, payment behavior, continuous effects, prompt rendering, random determinism, hidden-source redaction or authoritative state serialization.

## Test Coverage

Updated existing `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueTaskKeyedValuesWithTaskCountMismatch` now asserts detailed expected/actual suffixes for wrong readable keyed pending task queue values.
- `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueKeyedRequiredFieldAbsenceWithTaskCountMismatch` now asserts detailed `<missing>` / `<unreadable>` suffixes for absent and unreadable keyed pending task queue fields.

## Validation

Passed:

- Focused pending-task-queue keyed values + required-field detail tests: `2/2`.
- Changed-class `MatchRecoveryTests`: `1974/1974`.
- Adjacent Recovery/SpectatorReplayTiming/PendingTaskQueue/BattlefieldTask/TriggerQueue/ContinuousEffect filter: `2059/2059`.
- Backend full via `Riftbound.slnx`: `8307/8307`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src` and `tests`.

## Coordination

No subagent was created; A_MAIN continued directly on `/Users/dinghaolin/IdeaProjects/riftbound` local `main` per user request.

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 77 commits behind current `main` and `0` commits ahead at the pre-docs-sync divergence check. Exact divergence must be rechecked before integration.

`codex/rule-audit-remaining-20260615` had no commits ahead of `main`; root PDF text remained available.

Project remains **NOT READY**.
