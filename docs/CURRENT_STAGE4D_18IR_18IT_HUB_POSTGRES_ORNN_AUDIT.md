# Stage 4D-18IR/18IS/18IT Hub Postgres Ornn Audit

Date: 2026-06-05

Status: accepted by A_MAIN for the current main bundle. Project remains **NOT READY**.

## Scope

- 18IR: GameHub duplicate `clientIntentId` raw-payload conflict coverage in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- 18IS: Ornn friendly-equipment static-aura metadata and player-view parity coverage in `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`.
- 18IT: Postgres command journal duplicate-intent raw payload / command drift guard in `src/Riftbound.Persistence/PostgresMatchJournal.cs` and `tests/Riftbound.ConformanceTests/PostgresMatchRecoveryStoreSmokeTests.cs`.

Worker source commits:

- `b1d48ffc` - checkpoint: stage 4D hub raw intent conflict coverage.
- `22ff3640` - checkpoint: stage 4D ornn aura metadata coverage.
- `e20c89fc` - checkpoint: stage 4D postgres journal duplicate intent guard.

## Result

- GameHub coverage now proves exact same-command raw-payload retries replay stable events/snapshots/prompts while changed raw payload retries return `CLIENT_INTENT_CONFLICT` without group broadcasts, caller snapshots/prompts or changed journal entries.
- Ornn coverage now locks authoritative static-aura metadata including effect id, scope, duration, source path, source order, `FOUNDATION_ONLY` status, deferred LayerEngine residuals, participant/dependency ids and P1/P2 snapshot parity.
- Postgres command journal now treats duplicate `(match_id, player_id, client_intent_id)` rows as idempotent only when the stored `command_type` and full JSONB `payload` match. Raw payload or command drift throws before later event/snapshot/prompt journal writes can commit.

## Validation

Main worktree validation passed under the current no-DB environment:

- Focused new tests: `3/3`.
- Adjacent file-level filter: `146/146`.
- Broader adjacent server filter: `2014/2014`.
- Backend full: `dotnet test Riftbound.slnx --no-restore --logger "console;verbosity=minimal"` passed `7232/7232`.
- Mechanical checks: `git diff --check`, `git diff --cached --check`, anchored conflict-marker scan over `src tests docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Open Risks

- Real DB-backed Postgres duplicate-intent conflict smoke remains open because `ConnectionStrings__Riftbound` was not configured. The new Postgres smoke test was discovered and passed through the existing no-connection-string early return only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
