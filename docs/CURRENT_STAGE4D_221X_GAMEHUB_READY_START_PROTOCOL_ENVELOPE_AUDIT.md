# Stage 4D-221X GameHub Ready/Start Protocol Envelope Audit

Date: 2026-06-14 01:18 CST

Status: accepted for this narrow server-test shard. Project remains **NOT READY**.

## Scope

- Owner: `A_MAIN`
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Code commit: `a6e878e9 test: cover ready protocol envelope versions`
- Runtime changed: no
- Test coverage changed: yes, `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

## Coverage

`ReadyMessagesCarryProtocolVersionsOnReadyStartSnapshotsAndPrompts` joins Alice and Bob, sends whitespace-normalized `Ready` wrapper commands for both players, and verifies:

- Alice's first ready broadcast is `MessageType.READY`.
- Bob's second ready broadcast is `MessageType.START`.
- Both event envelopes carry `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`.
- Both ready steps emit group `SNAPSHOT` and `PROMPT` fanouts that carry the same defaults.

This extends the existing GameHub protocol-envelope contract from join/reconnect/request-snapshot/SubmitIntent into the Ready wrapper and match-start path.

## Validation

- Focused: `GameHubJoinTests.ReadyMessagesCarryProtocolVersionsOnReadyStartSnapshotsAndPrompts` `1/1`
- Changed class: `GameHubJoinTests` `200/200`
- Adjacent: Hub/protocol/Ready/Start filter `779/779`
- Backend full: `8234/8234`
- Mechanical: `git diff --check` passed before docs sync
- Mechanical: anchored conflict-marker scan over `docs src tests` found no matches before docs sync

## Coordination

- No subagent or new worktree was created.
- DOC_MATRIX_CURRENT actual worktree `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current` was clean at `17bde0c3`, observed 2026-06-14 01:18 CST.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Remaining Risk

This shard only proves GameHub Ready/Start protocol-envelope versioning. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
