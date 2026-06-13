# Stage 4D-222A GameHub SubmitIntent Preflight Protocol Envelope Audit

Timestamp: 2026-06-14 01:39 CST

Owner: A_MAIN

Scope: direct single-agent server-test shard for GameHub `SubmitIntent` preflight error protocol-envelope versioning.

## Change

- Added `SubmitIntentPreflightMessagesCarryProtocolVersionsOnClientIntentIdError` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- The test joins Alice and Bob, starts the match with `ReadyBothAsync`, submits `PASS_PRIORITY` through whitespace-normalized player identity with a blank `clientIntentId`, and checks the caller `ERROR` message.
- The new assertions prove the preflight rejected `SubmitIntent` path carries `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`, preserves normalized `room-a` / `alice` routing, reports `ErrorCodes.ClientIntentIdRequired`, and does not emit group `EVENTS`, `SNAPSHOT` or `PROMPT` messages.

## Validation

- Focused: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests.SubmitIntentPreflightMessagesCarryProtocolVersionsOnClientIntentIdError"` -> `1/1`.
- Changed class: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` -> `203/203`.
- Adjacent: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~SubmitIntent|FullyQualifiedName~Error|FullyQualifiedName~Raw|FullyQualifiedName~Mapper|FullyQualifiedName~ClientIntent"` -> `529/529`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` -> `8237/8237`.
- Mechanical: `git diff --check` passed; anchored conflict-marker scan over `docs src tests` had no matches.

## Coordination

- Main code commit: `0578cd17` (`test: cover submit intent preflight protocol envelope`).
- DOC_MATRIX_CURRENT actual worktree `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current` was clean at `17bde0c3`, observed 2026-06-14 01:39 CST.
- No subagent or new worktree was created.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Remaining Risk

This closes only the SubmitIntent preflight `clientIntentId` error-envelope versioning slice. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
