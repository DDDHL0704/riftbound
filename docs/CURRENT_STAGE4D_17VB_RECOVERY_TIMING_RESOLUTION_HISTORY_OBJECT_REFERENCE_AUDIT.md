2026-06-02 Stage 4D-17VB recovery timing resolution-history object-reference validation audit

Scope
- A_MAIN tightened `MatchRecoveryValidator` resolution-history validation for recovered player snapshots and spectator replay frames.
- Recovered snapshot timing `battlefieldResolutions[]` now validates `battlefieldObjectId`, optional `sourceObjectId` and `participantObjectIds[]` against the recovered snapshot object set.
- Recovered snapshot timing `battleResolutions[]` now validates `battlefieldId`, `attackerObjectIds[]`, `defenderObjectIds[]`, `survivingAttackerObjectIds[]`, `survivingDefenderObjectIds[]` and `destroyedObjectIds[]` against the recovered snapshot object set.
- Spectator replay-frame timing applies the same resolution-history object-reference checks against the authoritative object registry before broad authoritative parity checks continue.

Files changed
- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current Stage 4D checkpoint/completion/P0-P1/next-dispatch docs and shared coordination board.

Tests added
- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryObjectReferencesOutsideSnapshotObjects`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryObjectReferencesOutsideRegistry`

Validation
- Focused new resolution-history object-reference tests: `2/2`.
- Focused `ResolutionHistory` filter: `25/25`.
- Focused `MatchRecoveryTests`: `687/687`.
- Adjacent recovery/opening/store-smoke broad filter: `1287/1287`.
- Backend full: `6633/6633`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `src`, `tests` and `docs`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format Riftbound.slnx --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs --verify-no-changes --no-restore`: passed.
- Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore` still fails only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`.

Status
- Runtime changed: yes, recovery frame validation only.
- Protocol shape changed: no.
- Frontend, matrix, official catalog, Chrome/browser/formal E2E and `fullOfficial`: unchanged.
- This narrows P1-004 replay/recovery determinism and recovered/spectator resolution-history object-reference validation, but broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
- Project remains **NOT READY**.
