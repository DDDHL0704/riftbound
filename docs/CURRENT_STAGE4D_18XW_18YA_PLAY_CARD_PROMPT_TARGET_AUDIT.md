# Stage 4D-18XW-18YA Play-Card Prompt Target Audit

Date: 2026-06-07

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN dispatched five patch-only workers against disjoint guard files to extend main-action `PLAY_CARD` prompt target coverage for adjacent no-target and multi-target guard surfaces:

- 18XW: `RideTheWindMoveGuardTests`
- 18XX: `SwitcherooGuardTests`
- 18XY: `EdgeOfNightAssembleGuardTests`
- 18XZ: `DravenVanillaGuardTests`
- 18YA: `VexSpellshieldGuardTests`

The workers changed only their assigned test files. A_MAIN handled focused validation, one Switcheroo assertion adjustment to match the current prompt metadata contract, source commits and cherry-pick integration. No runtime code changed in this batch.

## Accepted Commits

- `ca4279cf` -> `b8dbd4c0`: Ride the Wind prompt target regression
- `7a8bd04f` -> `ae0497c4`: Switcheroo prompt target regression
- `7d515c50` -> `74f44440`: Edge of Night direct-play no-target prompt regression
- `fef02be0` -> `c4f299f3`: Draven no-target prompt regression
- `cf00e6c1` -> `0f53cac9`: Vex no-target prompt regression

## Validation

- Pre-dispatch main target-class baseline: `57/57`
- Worktree focused validation:
  - Ride the Wind: `10/10`
  - Switcheroo: `14/14`
  - Edge of Night: `21/21`
  - Draven: `9/9`
  - Vex: `8/8`
- Main changed-class filter: `62/62`
- Main adjacent target/guard filter: `165/165`
- Backend full conformance project: `7560/7560`

## Remaining Risk

This narrows prompt target exposure for these `PLAY_CARD` guard surfaces only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
