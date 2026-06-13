# Stage 4D-219X GameHub Reconnect Stale Persisted Token Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test/helper shard.
- Runtime changed: no, server test/helper coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- Dedicated closure surface: raw/reconnect/GameHub stale persisted-old-token replay after reconnect-token hash rotation.

## Coverage

- Added `ReconnectWithRotatedOldPersistedTokenDoesNotJoinGroupsOrLeakSessionData`.
- The local `RecordingMatchPlayerStore` still records every saved session for assertions, but `HasReconnectTokenHashAsync` now checks only the latest saved row for a room/player. This matches production `PostgresMatchPlayerStore` upsert/current-row semantics and the existing recovery test store's dictionary semantics.
- The new test joins `alice` with a recording player store, reconnects once with the original token, proves the store history contains the old and rotated token hashes, then retries reconnect with the old token.
- The stale retry must leave the save history count unchanged at two, must not join room/player groups, and must not emit joined, snapshot or prompt messages to caller or group clients.
- The stale retry must return a stable `InvalidReconnectToken` error envelope with protocol defaults, normalized player id and no old/current reconnect-token plaintext in the serialized payload.
- Existing valid reconnect, stale in-memory token replay, rotated-token hash persistence and invalid reconnect coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests.ReconnectWithRotatedOldPersistedTokenDoesNotJoinGroupsOrLeakSessionData"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `195/195`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` passed `8182/8182`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over the changed test and current Stage 4D docs.

## Commits

- Code: `20bcc59f` (`test: cover stale persisted reconnect token replay`)
- Docs: this checkpoint.

## Remaining

- This narrows raw/reconnect/GameHub stale persisted-old-token replay rejection after token-hash rotation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
