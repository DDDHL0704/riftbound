2026-05-24 Stage 4D-17A recovery rejected-command diagnostic replay audit accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchActionLogReplayer.VerifyFinalStateAsync` now compares replayed `ResolutionResult.ErrorMessage` with recovered `RecoveredCommand.ErrorMessage`.
- A rejected recovered command with matching accepted flag, tick, event count and final state hash but different diagnostic now produces an action-log replay audit error.
- This is recovery audit validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `ActionLogReplayerReplaysAcceptedAndRejectedCommandDiagnosticsToFinalStateHash`
- `ActionLogReplayerReportsRejectedCommandDiagnosticMismatch`

Behavior proved:
- Mixed accepted/rejected command logs (`PASS`, unsupported `UNKNOWN_RECOVERY_TEST`, `END_TURN`, `SURRENDER`) replay to the expected final authoritative state hash.
- Rejected unsupported commands preserve no-event/no-tick-advance spans and exact Chinese diagnostic `当前命令不受服务端支持。`.
- Tampering the recovered rejected-command diagnostic is detected even when final state hash still matches.

Validation:
- Focused: `25/25`.
- Adjacent recovery/opening: `606/606`.
- Backend full: `5971/5971`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
