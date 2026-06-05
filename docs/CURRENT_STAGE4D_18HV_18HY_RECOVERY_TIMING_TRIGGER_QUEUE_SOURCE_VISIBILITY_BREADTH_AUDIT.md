# Stage 4D-18HV-18HY Recovery Timing Trigger Queue Source Visibility Breadth Audit

Date: 2026-06-05

Owner: A_MAIN

Status: accepted on main after integrating four parallel worker commits. Project remains **NOT READY**.

## Scope

Stage 4D-18HV-18HY adds server recovery regression coverage for timing `triggerQueue[]` source-visibility breadth across snapshot and spectator replay contexts. Runtime validation code, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and solution files remain unchanged.

Worker source commits:

- 18HV: `3b56ad276fc050b7f00c6f4e73b715e03b8ad3c5` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hv`, adding Kogmaw last-breath snapshot and spectator source-visibility payload drift tests.
- 18HW: `42285d5966f8d45a7286bff59db28352c91b7dd7` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hw`, adding Jhin movement-resource snapshot hidden-source redaction and spectator keyed source-visibility mismatch/count-mismatch tests.
- 18HX: `c360b56e603765dbea283aea68196dc79d5dcb95` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hx`, adding Watchful Sentinel last-breath draw snapshot and spectator source-visibility payload drift tests.
- 18HY: `948bea742db00b9eb84057d8688ace0bfedb2c07` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hy`, adding Scouting Warhawk last-breath call-rune snapshot and spectator source-visibility payload drift tests.

The Kogmaw, Watchful Sentinel and Scouting Warhawk tests lock existing card-specific validator branches that require visible-source trigger payloads to remain `sourceVisibility = "VISIBLE"` when the underlying source state is visible. The Jhin path intentionally locks generic hidden-source redaction diagnostics and spectator keyed authoritative mismatch/count mismatch because Jhin movement resource does not have a card-specific `source visibility must be VISIBLE` branch.

## Validation

- Worker-local focused validation passed for each slice.
- A_MAIN focused new source-visibility breadth tests: `8/8`.
- A_MAIN focused `TriggerQueue` filter: `441/441`.
- A_MAIN focused `MatchRecoveryTests` filter: `1268/1268`.
- A_MAIN adjacent recovery/official-opening/Postgres recovery-store filter under the current no-DB environment: `1849/1849`; `ConnectionStrings__Riftbound` was unset, so `PostgresMatchRecoveryStoreSmokeTests` used its no-connection-string early return.
- A_MAIN backend full via tracked `Riftbound.slnx` under the same no-DB environment: `7214/7214`.
- Mechanical checks passed before docs sync: `git diff --cached --check`, unstaged `git diff --check`, anchored conflict-marker scan over `docs`, `src` and `tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Residual Risk

This is recovery test coverage only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real Postgres recovery-store smoke in a DB-backed environment, `fullOfficial` or final readiness.
