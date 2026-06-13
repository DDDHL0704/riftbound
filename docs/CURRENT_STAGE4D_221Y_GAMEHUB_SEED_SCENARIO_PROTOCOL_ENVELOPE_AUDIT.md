# Stage 4D-221Y GameHub SeedScenario Protocol Envelope Audit

Date: 2026-06-14 01:24 CST

Status: accepted for this narrow server-test shard. Project remains **NOT READY**.

## Scope

- Owner: `A_MAIN`
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Code commit: `dc6d6ae3 test: cover seed scenario protocol envelope versions`
- Runtime changed: no
- Test coverage changed: yes, `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

## Coverage

`SeedScenarioMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts` joins both players with a development host environment, calls `SeedScenario` for `basic-play` through whitespace-normalized player identity, and verifies:

- The seed result emits one group `EVENTS` message.
- The seed result emits two group `SNAPSHOT` messages.
- The seed result emits two group `PROMPT` messages.
- Every emitted message carries `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`.

This extends the existing GameHub protocol-envelope contract into the development seed path while leaving the production-only rejection gate untouched.

## Validation

- Focused: `GameHubJoinTests.SeedScenarioMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts` `1/1`
- Changed class: `GameHubJoinTests` `201/201`
- Adjacent: Hub/protocol/SeedScenario/Development filter `218/218`
- Backend full: `8235/8235`
- Mechanical: `git diff --check` passed before docs sync
- Mechanical: anchored conflict-marker scan over `docs src tests` found no matches before docs sync

## Coordination

- No subagent or new worktree was created.
- DOC_MATRIX_CURRENT actual worktree `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current` was clean at `17bde0c3`, observed 2026-06-14 01:24 CST.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Remaining Risk

This shard only proves GameHub development SeedScenario protocol-envelope versioning. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
