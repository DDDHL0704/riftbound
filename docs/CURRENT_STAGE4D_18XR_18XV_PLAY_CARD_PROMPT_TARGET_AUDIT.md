# Stage 4D-18XR-18XV Play-Card Prompt Target Audit

Date: 2026-06-07

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN dispatched five patch-only workers against disjoint guard files to extend main-action `PLAY_CARD` prompt target filtering coverage for remaining adjacent guard surfaces:

- 18XR: `SeaMonsterHookGuardTests`
- 18XS: `SfurSongGuardTests`
- 18XT: `FirestormEnemyBattlefieldDamageGuardTests`
- 18XU: `BattleOrFlightMoveToBaseTests`
- 18XV: `GustReturnToHandTests`

The workers changed only their assigned test files. A_MAIN handled focused validation, source commits and cherry-pick integration. No runtime code changed in this batch.

## Accepted Commits

- `ca0c21ca` -> `5cfeaba4`: Sea Monster Hook prompt target regression
- `e44b7ae3` -> `e3d4e934`: Sfur Song prompt target regression
- `efbfc76d` -> `78c22898`: Firestorm prompt target regression
- `0f708efd` -> `b38ceebd`: Battle or Flight prompt target regression
- `fa79e822` -> `9b66d4e3`: Gust prompt target regression

## Validation

- Pre-dispatch main target-class baseline: `38/38`
- Worktree focused validation:
  - Sea Monster Hook: `8/8`
  - Sfur Song: `8/8`
  - Firestorm: `12/12`
  - Battle or Flight: `7/7`
  - Gust: `8/8`
- Main changed-class filter: `43/43`
- Main adjacent target/guard filter: `181/181`
- Backend full conformance project: `7555/7555`

## Remaining Risk

This narrows prompt target exposure for these `PLAY_CARD` guard surfaces only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
