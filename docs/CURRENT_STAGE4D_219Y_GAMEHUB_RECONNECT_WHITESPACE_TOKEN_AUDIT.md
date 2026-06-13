# Stage 4D-219Y GameHub Reconnect Whitespace Token Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- Dedicated closure surface: raw/reconnect/GameHub whitespace-wrapped reconnect token normalization, normalized routing and rotated-token hash persistence.

## Coverage

- Added `ReconnectWithWhitespaceWrappedTokenRejoinsGroupsAndPersistsRotatedHash`.
- The new test joins `alice` with a recording player store, then reconnects as whitespace-wrapped `alice` using a reconnect token wrapped in leading/trailing whitespace.
- The reconnect must add the new connection to the normalized room and player groups, emit a `RECONNECT` joined message for normalized `alice`, and send snapshot/prompt messages to normalized `alice`.
- The reconnect must rotate the session token and record the second saved player session with `ReconnectTokenHasher.Hash(reconnect.ReconnectToken)`, while preserving the initial saved hash for the original join token.
- Existing valid reconnect, rotated-token replay rejection, rotated-token hash persistence, stale persisted-old-token rejection and invalid reconnect coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests.ReconnectWithWhitespaceWrappedTokenRejoinsGroupsAndPersistsRotatedHash"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` passed `196/196`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx` passed `8183/8183`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over `docs`, `src` and `tests`.

## Commits

- Code: `507bf99c` (`test: cover whitespace reconnect token rotation`)
- Docs: this checkpoint.

## Remaining

- This narrows raw/reconnect/GameHub whitespace-wrapped reconnect token normalization plus rotated-token hash persistence only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
