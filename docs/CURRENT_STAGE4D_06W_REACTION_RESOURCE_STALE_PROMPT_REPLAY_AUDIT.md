# Stage 4D-06W Reaction Resource Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server reaction-resource prompt-expiry closure slice. Project remains **NOT READY**.

## Scope

This slice covers prompt-stamped stale replay for two P0-005 PaymentEngine / resource-skill representative branches:

- Dragon Soul Sage reaction resource skill: immediate `ACTIVATE_ABILITY` gains ordinary mana in the rune pool without creating a stack item.
- Honeyfruit level-six reaction resource skill: immediate `ACTIVATE_ABILITY` gains mana and creates a payment-only temporary generic-power ledger without creating a stack item.

Allowed runtime surface was observation-only. No runtime change was required. The current session-layer behavior correctly rejects the old prompt stamp with `PROMPT_EXPIRED` before resolving the old resource-skill command again.

Locked surfaces remained unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `DragonSoulSageReactionResourceStalePromptReplayAfterManaGainRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/ReactionResourceSkillTests.cs`.
- Added `HoneyfruitLevelSixResourceStalePromptReplayAfterTemporaryLedgerRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/HoneyfruitResourceSkillTests.cs`.
- Proves the current prompt-scoped Dragon Soul Sage reaction resource skill accepts once, emits exactly one `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED` and `MANA_GAINED`, exhausts the source, gains 1 mana, preserves the pending stack item and opens no ordinary stack item.
- Proves the current prompt-scoped Honeyfruit level-six branch accepts once, emits `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED`, `MANA_GAINED` and `POWER_GAINED`, exhausts the source, gains 1 mana, creates exactly one payment-only temporary resource ledger and preserves the pending stack item.
- Proves replaying the same `ActivateAbilityCommand` with the old `promptId` / `snapshotTick` rejects with `PROMPT_EXPIRED`.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate generated mana, temporary ledger, power, exhaustion or stack side effects, no pending payment or task fork, and no enabled stale `ACTIVATE_ABILITY` source exposure for those resource skills.

## Non-Closure

This narrows P0-005 resource-skill prompt replay, duplicate generated-resource and duplicate temporary-ledger risk only. It does not close full PaymentEngine breadth, complete resource-skill official breadth, target/tax activated ability breadth, keyword payment branches, remaining payment windows, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial`, P0/P1 or READY.
