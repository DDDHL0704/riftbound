# Stage 4D-17FR Recovery Spectator Trigger Queue Validation Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot timing `triggerQueue` against authoritative `MatchState.TriggerQueue`. The validator checks list presence/count and per-trigger identity, controller id, spectator-redacted source object id, source visibility, spectator-redacted effect kind and triggered event kind.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMismatch` builds one visible trigger and one hidden standby-source trigger, mutates emitted trigger queue payloads, and proves recovery validation reports trigger queue field drift plus hidden-source redaction drift.

Validation:

- Focused single test: `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMismatch` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `193/193`.
- Adjacent recovery/opening/store-smoke: passed `774/774`.
- Backend full: passed `6139/6139`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator timing trigger queue payload parity only. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
