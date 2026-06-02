2026-06-02 Stage 4D-17VD recovery timing resolution-history related-event-kind validation audit

Scope
- A_MAIN tightened `MatchRecoveryValidator` resolution-history `relatedEventKinds[]` value-domain validation for recovered player snapshots, authoritative state and spectator replay frames.
- Recovered and spectator battlefield resolution related event kinds now reject values outside the current battlefield-resolution event set: `BATTLEFIELD_HELD`, `BATTLEFIELD_CONQUERED` and `BATTLEFIELD_CONTROL_RESOLVED`.
- Recovered and spectator battle resolution related event kinds now reject values outside the current battle-resolution event set: `DAMAGE_APPLIED`, `UNIT_DESTROYED`, `DAMAGE_REMOVED`, `UNIT_RECALLED_TO_BASE`, `BATTLE_CLOSED` and `BATTLE_NO_RESULT`.
- Authoritative state resolution-history related event kind lists now apply the same event-kind value-domain validation in addition to existing required/whitespace/redaction/duplicate checks.

Files changed
- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Current Stage 4D checkpoint/completion/P0-P1/next-dispatch docs and shared coordination board.

Tests added
- `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryRelatedEventKindValueDrift`
- `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryRelatedEventKindValueDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryRelatedEventKindValueDrift`

Validation
- Focused new resolution-history related-event-kind tests: `3/3`.
- Focused `ResolutionHistory` filter: `30/30`.
- Focused `MatchRecoveryTests`: `692/692`.
- Adjacent recovery/opening/store-smoke broad filter: `1292/1292`.
- Backend full: `6638/6638`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `src`, `tests` and `docs`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format Riftbound.slnx --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs --verify-no-changes --no-restore`: passed.
- Full `dotnet format Riftbound.slnx --verify-no-changes --no-restore` still fails only on unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`.

Status
- Runtime changed: yes, recovery frame and authoritative-state validation only.
- Protocol shape changed: no.
- Frontend, matrix, official catalog, Chrome/browser/formal E2E and `fullOfficial`: unchanged.
- This narrows P1-004 replay/recovery determinism and recovered/authoritative/spectator resolution-history related-event-kind value-domain validation, but broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
- Project remains **NOT READY**.
