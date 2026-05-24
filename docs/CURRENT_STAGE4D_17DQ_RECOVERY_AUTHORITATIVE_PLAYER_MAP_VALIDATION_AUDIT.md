# Stage 4D-17DQ Recovery Authoritative Player-Map Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now validates authoritative state player-keyed maps after seat-map validation. `RunePools`, `PlayerZones`, `PlayerScores`, `PlayerExperience`, `PlayerCardsPlayedThisTurn`, and `PlayerDecklists` reject null maps, blank keys, keys with surrounding whitespace, duplicate normalized keys, and keys that do not resolve to a player in authoritative `Seats`.

Test change: `RecoveryValidatorRejectsAuthoritativeStatePlayerMapsOutsideSeats` covers rune-pool, zone, score, experience, cards-played and decklist map keys outside authoritative alice/bob seats.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `147/147`.
- Adjacent recovery/opening: passed `727/727`.
- Backend full: passed `6093/6093`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative state player-map validation. Broader command/recovery/random determinism, deeper authoritative object/location ownership validation, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
