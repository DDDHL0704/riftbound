# Stage 4D-219W GameHub Reconnect Rotated Token Hash Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- Dedicated closure surface: raw/reconnect/GameHub persisted reconnect-token hash rotation after a successful reconnect.

## Coverage

- Added `ReconnectPersistsRotatedReconnectTokenHashWithoutPlaintext`.
- The test joins `alice` through a registry configured with `RecordingMatchPlayerStore`, captures the initial reconnect token and verifies reconnect with that token returns a different rotated token.
- The test proves the player store records a second saved session for `room-a` / `alice` / `P1` when reconnect rotates the token.
- The initial save must match `ReconnectTokenHasher.Hash` for the old token, and the rotated save must match the hash for the new token.
- The initial and rotated hashes must differ, and the rotated stored hash must not contain either plaintext reconnect token.
- Existing join-token persistence, valid reconnect, stale rotated-token replay rejection and invalid reconnect coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests.ReconnectPersistsRotatedReconnectTokenHashWithoutPlaintext"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `194/194`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` passed `8181/8181`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over the changed test and current Stage 4D docs.

## Commits

- Code: `e0ce5b13` (`test: cover reconnect token hash rotation`)
- Docs: this checkpoint.

## Remaining

- This narrows raw/reconnect/GameHub persisted reconnect-token hash rotation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
