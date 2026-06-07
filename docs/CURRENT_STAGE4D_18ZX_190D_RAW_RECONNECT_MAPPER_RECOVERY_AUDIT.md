# Stage 4D-18ZX-190D Raw Reconnect Mapper Recovery Audit

Date: 2026-06-07

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN ran a mixed parallel batch across disjoint server-test files after the no-target prompt metadata surface was cleared:

- 18ZX: `BoardTaskQueueFoundationTests` reconnect owner visibility for a pending illegal-standby cleanup task.
- 18ZY: `SpellDuelBattleStateMachineTests` reconnect owner hidden-standby visibility plus non-focus prompt shape during spell duel tasks.
- 18ZZ: `OfficialOpeningTests` reordered raw `SUBMIT_DECK` duplicate-intent replay canonicality.
- 190A: `EnemyBattlefieldUnitTargetScopeGuardTests` reordered stale raw `PLAY_CARD` rejected-cache replay canonicality for Megashark Cannon.
- 190B: `ConformanceFixtureRunnerTests` reordered raw `DECLARE_BATTLE` duplicate-intent replay canonicality.
- 190C: `MatchRecoveryTests` authoritative temporary-payment-resource blank allowed-payment-kind diagnostic coverage.
- 190D: `ConformanceFixtureShapeTests` mapper duplicate-id preservation for strict P0 text arrays plus trigger choice order metadata.

Runtime changed: no. Server test coverage only.

## Accepted Commits

- `f3228b31`: A_MAIN-owned direct patch bundle for 18ZZ, 190A, 190B, accepted 190C assertion and 190D. Several workers landed in the main worktree despite path instructions; A_MAIN treated those as direct diffs, validated them before commit, and did not accept any unvalidated behavior.
- `472799a9` -> `0253184a`: 18ZX board-task reconnect owner visibility.
- `99837cdf` -> `8c486404`: 18ZY spell-duel reconnect owner visibility.

Rejected candidate: 190C's proposed negative `CreatedTick` authoritative temporary-payment-resource test failed because the current validator emits no error for that shape. A_MAIN did not integrate that failing test. Only the existing blank allowed-payment-kind diagnostic assertion was accepted.

## Validation

- Pre-dispatch main baseline filter: `36/36`
- Main direct focused validation:
  - raw replay focused filter: `3/3`
  - mapper focused filter: `2/2`
  - recovery focused filter: `1/1`
- Worktree focused validation:
  - 18ZX initially failed on an over-specific cleanup task id assertion, then passed after A_MAIN adjusted it to assert actual task id shape and object consistency: `1/1`
  - 18ZY passed: `1/1`
  - 190C worktree had `1/2` because the negative `CreatedTick` candidate failed; accepted recovery assertion was covered by main focused validation.
- Integrated main focused filter: `8/8`
- Integrated main changed-class filter: `5146/5146`
- Integrated adjacent raw/reconnect/recovery/mapper filter: `5365/5365`
- Backend full conformance project: `7575/7575`
- `git diff --check` and anchored conflict-marker scans passed before integration validation.

## Remaining Risk

This narrows server raw replay canonicality, reconnect visibility/redaction, mapper duplicate preservation, and a small recovery diagnostic edge. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
