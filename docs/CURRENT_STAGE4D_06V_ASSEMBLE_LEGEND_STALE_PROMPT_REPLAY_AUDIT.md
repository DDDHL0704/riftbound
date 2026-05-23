# Stage 4D-06V Assemble Legend Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server assemble / legend prompt-expiry closure slice. Project remains **NOT READY**.

## Scope

This slice covers prompt-stamped stale replay for two 05I-covered action families that still needed session-layer prompt stamp coverage:

- `ASSEMBLE_EQUIPMENT`: Edge of Night assemble pays purple power and attaches to a friendly public unit.
- `LEGEND_ACT`: all 9 existing legend resource bridge success profiles for Diana, Ornn, KaiSa and Darius generate exactly one mana or power resource from the current prompt.

Allowed runtime surface was observation-only. No runtime change was required. The current session-layer behavior correctly rejects the old prompt stamp with `PROMPT_EXPIRED`; the public rejected prompt may omit the action entirely or retain only disabled explanatory metadata, but it must not be enabled or expose the old source.

Locked surfaces remained unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `EdgeOfNightAssembleStalePromptReplayAfterEquipmentAttachesRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/EdgeOfNightAssembleGuardTests.cs`.
- Added `LegendResourceBridgeStalePromptReplayAfterResourceGainRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs`.
- Proves the current prompt-scoped Edge of Night assemble accepts once, emits exactly `COST_PAID` and `EQUIPMENT_ATTACHED`, spends purple power, attaches the equipment, leaves stack / pending payment / task queue idle, and keeps hidden face-down objects redacted.
- Proves each current prompt-scoped legend resource bridge accepts once, exhausts the source and emits exactly one resource gain event without `COST_PAID`, payment window or task-queue drift.
- Proves replaying the same `AssembleEquipmentCommand` / `LegendActCommand` with the old `promptId` / `snapshotTick` rejects with `PROMPT_EXPIRED`.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate cost / attach / resource / exhaustion / stack side effects, no pending payment or task fork, no hidden-info leakage, and no enabled stale `ASSEMBLE_EQUIPMENT` / `LEGEND_ACT` candidate or old-source candidate exposure.

## Non-Closure

This narrows assemble / legend action prompt replay, duplicate attach, duplicate generated-resource and hidden-info leak risk only. It does not close full PaymentEngine breadth, complete assemble / legend official breadth, full resource-skill breadth, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial`, P0/P1 or READY.
