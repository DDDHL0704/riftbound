# Stage 4D-18NV/18NW/18NX Payment / GameHub / SpellDuel Audit

Date: 2026-06-06

Owner: `A_MAIN`

Status: accepted on main after review of three parallel worker commits. Project remains **NOT READY**.

## Scope

- 18NV source `a28ec985`: `PaymentEngineUnificationTests` now covers pending `PAY_COST` temporary-payment-resource exact raw-payload duplicate intent replay, changed raw payload conflict, no event/state/prompt/snapshot drift, no temporary-resource resurrection and no extra `PayCost` journal entry.
- 18NW source `319fa0ff`: `GameHubJoinTests` now covers after-finished `END_TURN` sentinel/client-intent/internal raw text redaction, no caller/group broadcast, no journal growth and unchanged finished P1/P2 snapshots.
- 18NX source `5f18d0c9`: `SpellDuelBattleStateMachineTests` now covers `PASS_PRIORITY` from a non-priority player, neutral timing and spell-duel focus timing rejecting with `PhaseNotAllowed` while preserving stack, focus, task queue and prompt shape.

## Main Integration

- 18NV cherry-picked as `6384bd42`.
- 18NW cherry-picked as `76321946`.
- 18NX cherry-picked as `054d2825`.
- Runtime code changed: no.
- Protocol shape changed: no.
- Matrix JSON changed: no.
- Frontend changed: no.

## Validation

- Focused new tests: `3/3`.
- Touched class filter: `257/257`.
- Broader adjacent server filter: `5251/5251`.
- Backend full via tracked `Riftbound.slnx`: `7327/7327` under the current no-DB environment.
- `git diff --check`: passed before docs sync.

## Remaining Open

- Broader P0/P1 closure.
- Command/recovery/random determinism outside this batch.
- Remaining recovered/spectator/authoritative nested payload breadth.
- Full LayerEngine breadth.
- Real DB-backed Postgres smoke, because no `ConnectionStrings__Riftbound` is available in this environment.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness status.
