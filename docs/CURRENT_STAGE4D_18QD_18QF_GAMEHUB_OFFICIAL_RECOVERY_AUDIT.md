# Stage 4D 18QD-18QF GameHub Official Recovery Audit

Date: 2026-06-06 13:15 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18QD added `GameHubJoinTests.AssignCombatDamageAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18QE added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshAssignCombatDamageAfterMatchFinishedThrowsStableErrorWithoutMutation`.
- 18QF added `MatchRecoveryTests.RecoveryValidatorRejectsCombatAssignmentElementShapeDrift`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `ASSIGN_COMBAT_DAMAGE` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, secret, internal, debug, assignment, battle and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Official session coverage now proves a fresh `ASSIGN_COMBAT_DAMAGE` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.
- Recovery coverage now proves raw recovered `ASSIGN_COMBAT_DAMAGE` payload validation catches non-array assignments, non-object assignment elements, missing/blank battle fields, blank assignment source/target ids and missing/non-integer damage with stable diagnostics.

## Source Commits

- 18QD worker source `156c64ccaf7af48d92f9fc83c91fc9d3fa279f38`, cherry-picked to main as `d9714b1e`.
- 18QE worker source `25ce1f1e731e0016d8396be2b247c5edd4114ea7`, cherry-picked to main as `79302003`.
- 18QF worker source `3e50d48db277401dc89bb183bb00bf00573a88dd`, cherry-picked to main as `60ab29f8`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `2077/2077`.
- Broader adjacent server filter: `5544/5544`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7387/7387`.
- `git diff --check`: passed.
- `git diff 8994b7f5..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 13:15 CST.

## Remaining Open

This narrows GameHub finished-session redaction, official finished-session fresh-command coverage, and recovery raw command shape validation only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
