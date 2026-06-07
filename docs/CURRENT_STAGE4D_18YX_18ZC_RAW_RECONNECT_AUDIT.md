# Stage 4D-18YX-18ZC Raw Command And Reconnect Audit

Date: 2026-06-07

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN dispatched six patch-only workers against disjoint test files to tighten mapper, raw-command replay, reconnect and redaction contracts:

- 18YX: `ConformanceFixtureShapeTests`
- 18YY: `GameHubJoinTests`
- 18YZ: `BoardTaskQueueFoundationTests`
- 18ZA: `SpellDuelBattleStateMachineTests`
- 18ZB: `UndercoverAgentTriggerTests`
- 18ZC: `FluftPoroActivatedAbilityTests`

The workers changed only their assigned test files. A_MAIN handled focused validation, source commits and cherry-pick integration. No runtime code changed in this batch.

## Accepted Commits

- `14db8ee3` -> `4a405406`: mapper legacy `triggerIds` also populates ordered trigger ids
- `88c925b5` -> `f3d610c8`: reconnect snapshot/prompt caller-only token redaction
- `5d443682` -> `011215bf`: cleanup reconnect hidden-standby redaction
- `64b940ec` -> `94332c0a`: spell-duel reconnect hidden-standby redaction
- `a5c06184` -> `64b669b0`: raw replay canonical JSON property-order replay
- `46d022c3` -> `d1587b14`: stale replay journal hash stability

## Validation

- Pre-dispatch main focused baseline: `6/6`
- Worktree focused validation:
  - Mapper P0 contract: `1/1`
  - GameHub reconnect: `1/1`
  - Board task cleanup reconnect: `1/1`
  - Spell duel reconnect: `1/1`
  - Undercover raw replay: `1/1`
  - Fluft Poro stale replay: `1/1`
- Main changed-class filter: `386/386`
- Main adjacent raw/reconnect/recovery filter: `1524/1524`
- Backend full conformance project: `7572/7572`

## Remaining Risk

This narrows mapper, raw-command replay and reconnect/redaction contract coverage only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
