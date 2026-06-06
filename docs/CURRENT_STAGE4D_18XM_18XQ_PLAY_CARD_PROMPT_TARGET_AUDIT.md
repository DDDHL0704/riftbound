# Stage 4D-18XM-18XQ Play-Card Prompt Target Audit

Date: 2026-06-07

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN dispatched five patch-only workers against disjoint guard files to extend main-action `PLAY_CARD` prompt target filtering coverage beyond the prior 18XH-18XL bundle:

- 18XM: `IsolateMoveToBaseGuardTests`
- 18XN: `VengeanceDestroyGuardTests`
- 18XO: `HuntTheWeakDestroyGuardTests`
- 18XP: `ZenithBladeStunGuardTests`
- 18XQ: `SpiritFireDestroyGuardTests`

The workers changed only their assigned test files. A_MAIN added runtime support after focused validation showed the server prompt still exposed illegal top-level targets for Hunt the Weak and Spirit Fire.

## Runtime Change

Commit `d84a0328` updates `ActionPromptBuilder` so top-level `PLAY_CARD` targets for behaviors with server-side target-selection constraints are narrowed through `PlayCardLegalTargetSelections`. It also applies prompt target power filtering for `MaxTargetPower` and a per-target upper bound derived from `MaxTotalTargetPower`, preventing impossible single targets from being exposed in prompt choices.

## Accepted Commits

- `d84a0328`: `fix: narrow play card prompt target choices`
- `3addedb2` -> `6c2b0182`: Isolate prompt target regression
- `d72bd473` -> `1412f606`: Vengeance prompt target regression
- `6d693cf4` -> `06466de9`: Hunt the Weak prompt target regression
- `a2fba173` -> `dbfc2eb2`: Zenith Blade prompt target regression
- `6ed75402` -> `a4b52518`: Spirit Fire prompt target regression

## Validation

- Pre-dispatch main target-class baseline: `52/52`
- Worktree focused validation:
  - Isolate: `10/10`
  - Vengeance: `13/13`
  - Zenith Blade: `13/13`
  - Hunt the Weak: failed before runtime fix, passed `8/8` after the runtime fix was applied
  - Spirit Fire: failed before runtime fix, passed `13/13` after the runtime fix was applied
- Main changed-class filter: `57/57`
- Main adjacent target/guard filter: `169/169`
- Backend full conformance project: `7550/7550`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`, `tests`, `src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed before docs sync.

## Remaining Risk

This narrows prompt target exposure for these `PLAY_CARD` guard surfaces only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
