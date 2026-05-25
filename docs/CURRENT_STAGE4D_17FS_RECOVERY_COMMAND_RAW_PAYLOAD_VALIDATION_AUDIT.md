# Stage 4D-17FS Recovery Command Raw Payload Validation Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects accepted recovered commands that require raw payload data but have no persisted `RawCommand`. This prevents recovery command validation from accepting payload-bearing commands that could only be replayed from a synthesized `cmdType`-only command, losing submitted targets, object ids, payment choices, decklists or other payload fields.

Test change: new `RecoveryValidatorRejectsAcceptedPayloadCommandWithoutRawCommand` proves an accepted `PAY_COST` command without raw payload is rejected, while a zero-payload accepted `PASS` command can still omit raw command data.

Validation:

- Focused single test: `RecoveryValidatorRejectsAcceptedPayloadCommandWithoutRawCommand` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `194/194`.
- Adjacent recovery/opening/store-smoke: passed `775/775`.
- Backend full: passed `6140/6140`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes accepted payload-bearing recovered-command raw-payload presence only. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
