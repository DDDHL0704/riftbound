# Stage 4D-18HZ-18IC Recovery Timing Required Payload Breadth Audit

Date: 2026-06-05

Owner: A_MAIN

Status: accepted on main after integrating four parallel worker commits. Project remains **NOT READY**.

## Scope

Stage 4D-18HZ-18IC adds server recovery regression coverage for timing payload required/shape diagnostics outside the recently saturated trigger-queue surface. Runtime validation code, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and solution files remain unchanged.

Worker source commits:

- 18HZ: `5d5587b1eaf5157eab2b0b475c47158b391e6b50` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hz`, adding snapshot battle damage-assignment payload shape drift coverage.
- 18IA: `59f5369d44d4248c41ab3e3c0821fa4f49ddfd62` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18ia`, adding spectator continuous-effects missing top-level payload coverage.
- 18IB: `b8520c2bff215d0f8b7f38f47bc236e7dfd3da04` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18ib`, adding spectator pending-task-queue missing metadata coverage.
- 18IC: `aa9cdfdcf90bf221f4dd123eee5b58c59ce3a772` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18ic`, adding spectator temporary-payment-resources missing top-level payload coverage.

The tests lock existing validator branches that require readable timing sub-payloads before nested parity checks can proceed. Each slice mutates only the relevant timing payload field and keeps authoritative state otherwise valid for its focused branch.

## Validation

- Worker-local focused validation passed for each slice.
- A_MAIN focused new required/payload tests: `4/4`.
- A_MAIN focused `MatchRecoveryTests` filter: `1272/1272`.
- A_MAIN adjacent recovery/official-opening/Postgres recovery-store filter under the current no-DB environment: `1853/1853`; `ConnectionStrings__Riftbound` was unset, so `PostgresMatchRecoveryStoreSmokeTests` used its no-connection-string early return.
- A_MAIN backend full via tracked `Riftbound.slnx` under the same no-DB environment: `7218/7218`.
- Mechanical checks passed before docs sync: `git diff --cached --check`, unstaged `git diff --check`, anchored conflict-marker scan over `docs`, `src` and `tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Residual Risk

This is recovery test coverage only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real Postgres recovery-store smoke in a DB-backed environment, `fullOfficial` or final readiness.
