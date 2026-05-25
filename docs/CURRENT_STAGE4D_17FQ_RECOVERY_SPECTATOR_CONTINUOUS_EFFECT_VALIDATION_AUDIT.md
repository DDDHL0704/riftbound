# Stage 4D-17FQ Recovery Spectator Continuous Effect Validation Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot timing `continuousEffects` against authoritative `MatchState.ContinuousEffects`. The validator checks list presence/count and per-effect identity, scope, layer, duration, target/source object ids, power values, sequence, optional effect kind/source metadata, LayerEngine foundation status, requested/applied/minimum/resulting power metadata, applied/source order, condition/lifecycle, dependency object lists, participant object lists and deferred LayerEngine residual metadata.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectsMismatch` builds an authoritative ledger-backed until-end power modifier so the spectator timing view emits LayerEngine foundation metadata, mutates every emitted continuous-effect field, and proves the recovery validator reports continuous-effect payload drift.

Validation:

- Focused single test: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectsMismatch` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `192/192`.
- Adjacent recovery/opening/store-smoke: passed `773/773`.
- Backend full: passed `6138/6138`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Environment note: this machine did not have `.NET` installed at the start of the slice, so SDK `10.0.100` was installed into `~/.dotnet` before running validation. `scripts/dev-env.sh` still reports missing local `psql` / `redis-cli`; tests were run with explicit `DOTNET_ROOT` / `PATH`.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator timing continuous-effect payload parity only. Trigger queue, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
