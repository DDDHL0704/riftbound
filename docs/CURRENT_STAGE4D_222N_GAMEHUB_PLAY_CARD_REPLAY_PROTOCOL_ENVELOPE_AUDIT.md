# Stage 4D-222N GameHub PlayCard Replay Protocol Envelope Audit

Timestamp: 2026-06-15 10:55 CST

Owner: A_MAIN

Scope: direct single-agent server-test shard for GameHub `PLAY_CARD` raw-command idempotent replay protocol-envelope versioning.

## Change

- Added `PlayCardReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- The test joins two players, seeds the development `typed-power-payment` scenario, verifies the seeded `PLAY_CARD` candidate for `P1-SPELL-BULLET-TIME`, accepts a `PLAY_CARD` raw command under `play-card-replay-protocol-envelope`, then resubmits the same raw command through whitespace-normalized player identity with the same client intent id.
- The new assertions prove the idempotent PlayCard replay path emits replayed group `EVENTS`, `SNAPSHOT` and `PROMPT` envelopes with `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`, preserves normalized player routing on the replayed events envelope, keeps replayed event kinds stable with the accepted message, keeps replayed snapshot/prompt player fanout stable and keeps the replayed events server tick stable with the accepted message.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayCardReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts" --no-restore` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests" --no-restore` -> `216/216`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~PlayCard|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development" --no-restore` -> `2185/2185`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` -> `8250/8250`.
