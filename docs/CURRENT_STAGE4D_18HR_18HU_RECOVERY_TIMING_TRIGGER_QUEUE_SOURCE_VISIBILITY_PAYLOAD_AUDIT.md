# Stage 4D-18HR-18HU Recovery Timing Trigger Queue Source Visibility Payload Audit

Date: 2026-06-05

Owner: A_MAIN

Status: accepted on main after integrating four parallel worker commits. Project remains **NOT READY**.

## Scope

Stage 4D-18HR-18HU adds server recovery regression coverage for timing `triggerQueue[]` card-specific source-visibility payload diagnostics. Runtime validation code, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and solution files remain unchanged.

Worker source commits:

- 18HR: `fde24734c61704a2899f199068025b6b88ea5e61` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hr`, adding OGS Lux high-cost spell snapshot and spectator source-visibility payload drift tests.
- 18HS: `f422b1e62aa5bc9cfcb712c40899b71bea1422fd` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hs`, adding Teemo on-play self-power snapshot and spectator source-visibility payload drift tests.
- 18HT: `0796d393d2d2bf0aa22576b7c3076fbd48116950` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18ht`, adding the Blue Sentinel delayed-resource spectator source-visibility payload drift counterpart.
- 18HU: `5c58813ee20bf8914de46434969fd909a3211825` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hu`, adding Ghostly Centaur friendly-destroyed snapshot and spectator source-visibility payload drift tests.

The tests lock existing validator branches that require card-specific visible-source trigger payloads to remain `sourceVisibility = "VISIBLE"` when the underlying source state is visible. Each test uses valid source object, controller, zone and location context, mutates only the trigger payload visibility/redaction fields needed for the slice, and asserts the card-specific `source visibility must be VISIBLE` diagnostic.

## Validation

- Worker-local focused validation passed for each slice.
- A_MAIN focused new source-visibility payload tests: `7/7`.
- A_MAIN focused `TriggerQueue` filter: `433/433`.
- A_MAIN focused `MatchRecoveryTests` filter: `1260/1260`.
- A_MAIN adjacent recovery/official-opening/Postgres recovery-store filter under the current no-DB environment: `1841/1841`; `ConnectionStrings__Riftbound` was unset, so `PostgresMatchRecoveryStoreSmokeTests` used its no-connection-string early return.
- A_MAIN backend full via tracked `Riftbound.slnx` under the same no-DB environment: `7206/7206`.
- Mechanical checks passed before docs sync: `git diff --cached --check` and anchored conflict-marker scan.

## Residual Risk

This is recovery test coverage only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real Postgres recovery-store smoke in a DB-backed environment, `fullOfficial` or final readiness.
