# Stage 4D-17FB Recovery Spectator Player Object Coverage Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator snapshot player `objects` coverage against authoritative spectator-visible object ids. The guard requires visible base, non-hidden battlefield, graveyard, banished, legend-zone and champion-zone objects to be present, rejects non-visible objects such as hand entries, and checks each expected object's payload identity and face-down flag.

Redaction change: hidden face-down spectator objects may not expose private metadata fields such as `cardNo`, `tags`, or `power`. This preserves the current spectator redaction shape while leaving deeper visible object scalar/list/location parity for later slices.

Test change: new `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectCoverageAndRedactionMismatch` builds a spectator frame with one hidden face-down base object, one visible battlefield object, and one hand object, then proves explicit diagnostics for missing visible object coverage, hand-object leakage, hidden object id drift, hidden face-down flag drift, and private metadata leakage.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `177/177`.
- Adjacent recovery/opening/store-smoke: passed `758/758`.
- Backend full: passed `6123/6123`.
- Mechanical checks: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes spectator player object coverage and hidden face-down private metadata redaction only. Deeper visible object metadata parity, object location parity, broader command/recovery/random determinism, full LayerEngine breadth, battlefield/battle lifecycle breadth, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
