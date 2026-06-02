2026-06-02 Stage 4D-17VC recovery timing resolution-history player-reference validation audit

Scope
- A_MAIN tightened `MatchRecoveryValidator` resolution-history player-reference validation for recovered player snapshots and spectator replay frames.
- Recovered snapshot timing `battlefieldResolutions[]` now validates optional `playerId`, `previousControllerId` and `controllerId` values against the recovered snapshot `players` map.
- Recovered snapshot timing `battleResolutions[]` now validates optional `attackingPlayerId`, `defendingPlayerId` and `winnerPlayerId` values against the recovered snapshot `players` map.
- Spectator replay-frame timing applies the same resolution-history player-reference checks against authoritative seats before broad authoritative parity checks continue.

Files changed
- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current Stage 4D checkpoint/completion/P0-P1/next-dispatch docs and shared coordination board.

Tests added
- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryPlayerReferencesOutsideSnapshotPlayers`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryPlayerReferencesOutsideSeats`

Validation
- Focused new resolution-history player-reference tests: `2/2`.
- Focused `ResolutionHistory` filter: `27/27`.
- Focused `MatchRecoveryTests`: `689/689`.
- Adjacent recovery/opening/store-smoke broad filter: `1289/1289`.
- Backend full: `6635/6635`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `src`, `tests` and `docs`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format Riftbound.slnx --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs --verify-no-changes --no-restore`: passed.
- Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore` still fails only on unrelated pre-existing whitespace diagnostics outside this slice in `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`.

Status
- Runtime changed: yes, recovery frame validation only.
- Protocol shape changed: no.
- Frontend, matrix, official catalog, Chrome/browser/formal E2E and `fullOfficial`: unchanged.
- This narrows P1-004 replay/recovery determinism and recovered/spectator resolution-history player-reference validation, but broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
- Project remains **NOT READY**.
