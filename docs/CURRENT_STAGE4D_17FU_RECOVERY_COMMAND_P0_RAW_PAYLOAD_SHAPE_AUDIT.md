# Stage 4D-17FU Recovery Command P0 Raw Payload Shape Audit

Date: 2026-05-25

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now rejects present raw commands for P0 complex payload command types when the required replay payload fields are missing or malformed. The guarded command types are `PAY_COST`, `ASSIGN_COMBAT_DAMAGE`, `ORDER_TRIGGERS` and `CHOOSE_HAND_CARDS`, covering payment ids/windows/choice lists, combat assignment source/target/damage records, ordered trigger id lists, and hand-choice ids/windows/chosen object lists. This prevents recovered commands from retaining only `{ "cmdType": ... }` while losing the payload needed for deterministic replay/audit.

Test change: `RecoveryValidatorRejectsP0PayloadCommandRawPayloadShapeDrift` proves cmdType-only or malformed raw payloads are rejected for payment, combat assignment, trigger ordering and hand choice commands.

Validation:

- Focused single test: `RecoveryValidatorRejectsP0PayloadCommandRawPayloadShapeDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `195/195`.
- Adjacent recovery/opening/store-smoke: passed `776/776`.
- Backend full: passed `6141/6141`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `src`/`tests`/`docs`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness gate, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes P0 complex recovered-command raw-payload field shape only. Broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and final readiness remain open.
