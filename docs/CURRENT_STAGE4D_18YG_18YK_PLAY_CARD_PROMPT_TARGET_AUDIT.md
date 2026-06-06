# Stage 4D-18YG-18YK Play-Card Prompt Target Audit

Date: 2026-06-07

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN dispatched five patch-only workers against disjoint guard files to extend main-action `PLAY_CARD` prompt no-target coverage for direct unit/spell plays:

- 18YG: `OverchargedEnergyGuardTests`
- 18YH: `HuntReadyGuardTests`
- 18YI: `ReksaiNoOptionalHasteOverwhelmGuardTests`
- 18YJ: `ReksaiAttackRevealPlayUnitGuardTests`
- 18YK: `EzrealCombatDamageTextPlayUnitGuardTests`

The workers changed only their assigned test files. A_MAIN handled focused validation, one Ezreal metadata-shape assertion adjustment, source commits and cherry-pick integration. No runtime code changed in this batch.

## Accepted Commits

- `e33b2d9a` -> `9dc27393`: Overcharged Energy no-target prompt regression
- `1df03ac5` -> `968b70f7`: Hunt no-target prompt regression
- `f6c612f1` -> `3d8696cc`: Rek'Sai no-optional no-target prompt regression
- `7fb06426` -> `733c6c92`: Rek'Sai attack-reveal no-target prompt regression
- `dc2c3f4b` -> `7ae46941`: Ezreal combat text no-target prompt regression

## Validation

- Pre-dispatch main target-class baseline: `52/52`
- Worktree focused validation:
  - Overcharged Energy: `13/13`
  - Hunt: `12/12`
  - Rek'Sai no-optional: `10/10`
  - Rek'Sai attack-reveal: `10/10`
  - Ezreal combat text: initially failed on metadata dictionary shape, then `12/12`
- Main changed-class filter: `57/57`
- Main adjacent no-target/prompt guard filter: `149/149`
- Backend full conformance project: `7570/7570`

## Remaining Risk

This narrows no-target prompt exposure for these direct `PLAY_CARD` guard surfaces only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
