# Stage 4D-17DU Recovery Authoritative Object-Reference Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateAuthoritativeState` now builds an authoritative object registry from `CardObjects` and `ObjectLocations` when either collection provides object ids. With that registry available, stack item source/target object ids, trigger queue source object ids, pending hand-choice source/legal object ids, and temporary payment resource source object ids reject references missing from the registry. Minimal recovery states without an object registry remain accepted by this specific guard.

Test change: `RecoveryValidatorRejectsAuthoritativeStateObjectReferencesOutsideRegistry` covers stack source/target, trigger source, pending hand-choice source/legal, and temporary payment resource source ids outside a registry containing only `known-1`.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `151/151`.
- Adjacent recovery/opening: passed `731/731`.
- Backend full: passed `6097/6097`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only authoritative stack/trigger/pending object-reference existence validation when the object registry is present. Broader command/recovery/random determinism, full object-id/location coherence, battlefield/battle ownership validation, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
