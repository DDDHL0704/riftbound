# Stage 4D-18HL-18HN Recovery Timing Trigger Queue Keyed Visible Source Empty Values Audit

Date: 2026-06-05

Owner: A_MAIN

Status: accepted on main after integrating three parallel worker commits. Project remains **NOT READY**.

## Scope

Stage 4D-18HL-18HN adds server recovery regression coverage for spectator replay-frame timing `triggerQueue[]` same-key visible-source required-string empty-value drift under trigger-count mismatch. Runtime validation code, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and solution files remain unchanged.

Worker source commits:

- 18HL: `dd2ee8f0536ed85b6147aa53521997ada081ae58` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hl`, adding `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectIdEmptyValueWithCountMismatch`.
- 18HM: `5c5a0e56017cc82dbdfb35de297de62e678c8610` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hm`, adding `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceVisibilityEmptyValueWithCountMismatch`.
- 18HN: `f04362b40fb2e4240aa43a6c9e90579c42a61ab5` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hn`, adding `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceEffectKindEmptyValueWithCountMismatch`.

Each test builds an authoritative visible-source trigger from real `MatchState` source object, zone and location state, mutates exactly one required visible-source payload field to `string.Empty`, appends `trigger-extra` to force trigger-count mismatch, and asserts the required diagnostic, keyed authoritative mismatch, unknown extra-trigger and count-mismatch diagnostics. The tests intentionally do not assert an `invalid` diagnostic because empty strings are treated as required-string failures in this validator path.

## Validation

- Worker-local validation passed for each slice after the workers bypassed local `scripts/dev-env.sh` probe failures caused by missing `psql` and `redis-cli`.
- A_MAIN focused new empty-value tests: `3/3`.
- A_MAIN focused `TriggerQueue` filter: `423/423`.
- A_MAIN focused `MatchRecoveryTests` filter: `1250/1250`.
- A_MAIN adjacent recovery/official-opening/Postgres recovery-store filter under the current no-DB environment: `1831/1831`; `ConnectionStrings__Riftbound` was unset, so `PostgresMatchRecoveryStoreSmokeTests` used its no-connection-string early return.
- A_MAIN backend full via tracked `Riftbound.slnx` under the same no-DB environment: `7196/7196`.
- Environmental probe note: `source scripts/dev-env.sh` stopped before `dotnet test` because this shell cannot find `psql` or `redis-cli`, and local port `5432` was not reachable.

## Residual Risk

This is recovery test coverage only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real Postgres recovery-store smoke in a DB-backed environment, `fullOfficial` or final readiness.
