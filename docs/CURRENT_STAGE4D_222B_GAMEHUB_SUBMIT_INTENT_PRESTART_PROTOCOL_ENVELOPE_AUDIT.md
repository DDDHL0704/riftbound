# Stage 4D-222B GameHub SubmitIntent Pre-Start Protocol Envelope Audit

Timestamp: 2026-06-14 01:45 CST

Owner: A_MAIN

Scope: direct single-agent server-test shard for GameHub `SubmitIntent` pre-start error protocol-envelope versioning.

## Change

- Added `SubmitIntentPreStartMessagesCarryProtocolVersionsOnMatchNotStartedError` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- The test joins Alice and Bob without readying them, submits `PASS_PRIORITY` through whitespace-normalized player identity, and checks the caller `ERROR` message.
- The new assertions prove the not-yet-started match `SubmitIntent` path carries `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`, preserves normalized `room-a` / `alice` routing, reports `ErrorCodes.MatchNotStarted`, and does not emit group `EVENTS`, `SNAPSHOT` or `PROMPT` messages.

## Validation

- Focused: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests.SubmitIntentPreStartMessagesCarryProtocolVersionsOnMatchNotStartedError"` -> `1/1`.
- Changed class: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` -> `204/204`.
- Adjacent: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~SubmitIntent|FullyQualifiedName~Error|FullyQualifiedName~Raw|FullyQualifiedName~Mapper|FullyQualifiedName~ClientIntent|FullyQualifiedName~MatchNotStarted"` -> `530/530`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` -> `8238/8238`.
- Mechanical: `git diff --check` passed; anchored conflict-marker scan over `docs src tests` had no matches.

## Coordination

- Main code commit: `74ce451b` (`test: cover submit intent prestart protocol envelope`).
- DOC_MATRIX_CURRENT actual worktree `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current` was clean at `17bde0c3`, observed 2026-06-14 01:45 CST.
- No subagent or new worktree was created.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Remaining Risk

This closes only the SubmitIntent `MatchNotStarted` pre-start error-envelope versioning slice. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
