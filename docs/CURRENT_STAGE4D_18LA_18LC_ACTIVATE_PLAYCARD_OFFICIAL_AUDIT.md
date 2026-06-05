# Stage 4D-18LA/18LB/18LC Activate PlayCard Official Audit

Date: 2026-06-06 01:15 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18LA integrated worker commit `838aac85`: `ConformanceFixtureRunnerTests` now covers `ACTIVATE_ABILITY` duplicate `clientIntentId` raw-payload behavior through `MatchSession.SubmitAsync`. Exact same raw payload replays the cached accepted ability result without journal growth or state/tick/event/stack/prompt/snapshot drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.
- 18LB integrated worker commit `24be9cfd`: `GameHubJoinTests` now covers GameHub seeded `PLAY_CARD` duplicate `clientIntentId` raw-payload behavior. Exact same raw payload replays the accepted event/snapshot/prompt broadcast without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without group/caller mutation.
- 18LC integrated worker commit `3371ca24`: `OfficialOpeningTests` now covers official first-turn `SURRENDER` duplicate `clientIntentId` raw-payload behavior after final mulligan. Exact same raw payload replays the finished result without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.

## Validation

- Focused new tests: `3/3`.
- Touched class filter `ConformanceFixtureRunnerTests|GameHubJoinTests|OfficialOpeningTests`: `3770/3770`.
- Broader adjacent server filter `ConformanceFixtureRunnerTests|GameHubJoinTests|OfficialOpeningTests|MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests`: `5065/5065`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7272/7272`.
- Mechanical checks after code integration: `git diff --cached --check` passed before docs sync; post-doc mechanical checks are recorded in the checkpoint commit.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
