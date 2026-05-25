# Stage 4D-17FT Recovery Command Rejected Raw Payload Validation Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects any recovered command that requires raw payload data but has no persisted `RawCommand`, regardless of accepted/rejected outcome. This extends the 4D-17FS accepted-command guard so rejected payload-bearing commands also keep the submitted payload needed to audit targets, object ids, payment choices, combat assignments, trigger ordering and hand choices.

Test change: `RecoveryValidatorRejectsPayloadCommandWithoutRawCommand` now proves both accepted `PAY_COST` and rejected `CHOOSE_HAND_CARDS` commands without raw payload are rejected, while a zero-payload accepted `PASS` command can still omit raw command data.

Validation:

- Focused single test: `RecoveryValidatorRejectsPayloadCommandWithoutRawCommand` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `194/194`.
- Adjacent recovery/opening/store-smoke: passed `775/775`.
- Backend full: passed `6140/6140`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes rejected payload-bearing recovered-command raw-payload presence only. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
