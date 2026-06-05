# Stage 4D-18NY/18NZ/18OA Mapper / Battle Damage / LayerEngine Audit

Date: 2026-06-06

Owner: `A_MAIN`

Status: accepted on main after review of three parallel worker/worktree slices. Project remains **NOT READY**.

## Scope

- 18NY source `b9fb7f42`: `ConformanceFixtureShapeTests` now covers `ASSIGN_COMBAT_DAMAGE` mapper shape stability for malformed `assignments` payloads, proving non-array assignments, non-object assignment entries, missing `damage` and out-of-range numeric damage all map to `AssignCombatDamageCommand` with `Assignments == null` while a valid assignment array still maps normally.
- 18NZ source `630fcaf1`: `BattleDamageAssignmentLifecycleTests` now covers stale prompt-scoped raw `ASSIGN_COMBAT_DAMAGE` after the next contest has opened, proving it rejects with `PromptExpired`, records a rejected journal entry using the stale raw prompt metadata, and does not mutate state, tick, stack, task queue, prompts, graveyard/battlefield projections or hidden-standby redaction. The worker initially exposed that the current runtime records this rejected journal entry; A_MAIN aligned the test to lock that current replayable contract rather than changing runtime.
- 18OA source `e6891806`: `LayerEngineTimestampDependencyTests` now covers battlefield static-aura source-order dependency metadata when one source leaves and another other-battlefield object remains present, proving authoritative and P1/P2 snapshot dependency signatures exclude the removed source and unrelated battlefield object while preserving the surviving source/participant metadata.

## Main Integration

- 18NY cherry-picked as `ef303913`.
- 18NZ cherry-picked as `63ce9de1`.
- 18OA cherry-picked as `c11ba8b6`.
- Runtime code changed: no.
- Protocol shape changed: no.
- Matrix JSON changed: no.
- Frontend changed: no.

## Validation

- Focused new tests: `3/3`.
- Touched class filter: `190/190`.
- Broader adjacent server filter: `5404/5404`.
- Backend full via tracked `Riftbound.slnx`: `7330/7330` under the current no-DB environment.
- `git diff --check`: passed before docs sync.
- `git diff --cached --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

## Remaining Open

- Broader P0/P1 closure.
- Command/recovery/random determinism outside this batch.
- Remaining recovered/spectator/authoritative nested payload breadth.
- Full LayerEngine breadth.
- Real DB-backed Postgres smoke, because no `ConnectionStrings__Riftbound` is available in this environment.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness status.
