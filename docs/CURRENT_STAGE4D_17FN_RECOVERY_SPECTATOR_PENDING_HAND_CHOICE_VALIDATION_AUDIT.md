# Stage 4D-17FN Recovery Spectator Pending Hand Choice Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot timing `pendingHandChoice` base payloads against authoritative `PendingHandChoice`. The guard covers required/null shape, choice id, choice window, player id, required count, max count, reason, source object id, effect kind, spectator choice state, and redaction of `legalObjectIds`.

Test change: new `RecoveryValidatorRejectsSpectatorReplayTimingPendingHandChoiceMismatch` builds a pending hand-choice recovery frame, mutates the spectator `pendingHandChoice` payload, leaks `legalObjectIds`, and proves explicit diagnostics for identity, counts, scalar fields, spectator state, and redaction drift.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `189/189`.
- Adjacent recovery/opening/store-smoke: passed `770/770`.
- Backend full: passed `6135/6135`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator timing pending-hand-choice base payload and legal-object redaction parity only. Pending payment resource action derivation, temporary payment resources, continuous effects, trigger queue, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
