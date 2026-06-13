# Stage 4D-219V GameHub Reconnect Rotated Token Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- Dedicated closure surface: raw/reconnect/GameHub stale reconnect-token replay after a successful token rotation.

## Coverage

- Added `ReconnectWithRotatedOldTokenDoesNotJoinGroupsOrLeakSessionData`.
- The test joins `alice`, captures the first reconnect token, reconnects successfully once with that token, and asserts the Hub returns a different rotated token.
- The test then retries `Reconnect` with the stale old token and whitespace-wrapped player id, proving the Hub normalizes the player id but rejects the stale token with `InvalidReconnectToken`.
- The stale replay must not add room/player groups, must not emit joined, snapshot or prompt messages to caller or group clients, and must preserve protocol defaults on the error envelope.
- The serialized error payload must not contain either the old stale token or the current rotated token.
- Existing valid reconnect, invalid reconnect, persisted reconnect-token hash and request-snapshot coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests.ReconnectWithRotatedOldTokenDoesNotJoinGroupsOrLeakSessionData"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `193/193`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` passed `8180/8180`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over the changed test and current Stage 4D docs.

## Commits

- Code: `fe245525` (`test: cover rotated reconnect token replay`)
- Docs: this checkpoint.

## Remaining

- This narrows raw/reconnect/GameHub stale rotated reconnect-token replay rejection only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
