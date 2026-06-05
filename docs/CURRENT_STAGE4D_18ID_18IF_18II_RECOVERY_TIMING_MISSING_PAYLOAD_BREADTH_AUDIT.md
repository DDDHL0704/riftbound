# Stage 4D-18ID/18IE/18IF/18II Recovery Timing Missing Payload Breadth Audit

Date: 2026-06-05 19:45 CST

Project status: **NOT READY**

## Scope

A_MAIN integrated four parallel worker-produced server recovery slices for spectator replay-frame timing missing-payload coverage:

- 18ID `d38a05ab`: `RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentMissingPayload`
- 18IE `3ffb7a67`: `RecoveryValidatorRejectsSpectatorReplayTimingPendingHandChoiceMissingPayload`
- 18IF `e83f1d96`: `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTasksMissingPayload`
- 18II `2ba0e752`: `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentMissingPayload`

Files touched on main:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint/completion/P0/P1/next-dispatch docs and shared coordination board

Runtime validation changed only for the missing spectator battle damage assignment branch: the diagnostic now reports `spectator replay frame timing battle damage assignment payload is required`, matching the existing malformed payload branch.

## Validation

- Focused new missing-payload tests: `4/4`
- Focused `MatchRecoveryTests`: `1276/1276`
- Adjacent recovery/official-opening/Postgres recovery-store under current no-DB environment: `1857/1857`
- Backend full via tracked `Riftbound.slnx` under current no-DB environment: `7222/7222`
- Mechanical checks before docs sync: `git diff --cached --check`, unstaged `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Residual Risk

This bundle narrows P1-004 replay/recovery determinism coverage for spectator timing missing-payload branches. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` or final readiness.

DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-05 19:45 CST. A_MAIN did not touch that worktree.
