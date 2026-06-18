# Stage 4D-223BD Recovery Spectator Battle-Damage-Assignment Missing/Null Payload Count Drift Audit

Date: 2026-06-18 10:31 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / code commit: `main` / `b51a3e64`

## Scope

This shard closes a narrow recovery-validator gap for spectator replay-frame timing `battle.damageAssignment`.

When authoritative state has an open battle damage assignment window, spectator timing payloads that omit `battle.damageAssignment` or set it to `null` now report:

- the existing required-payload diagnostic;
- damage-pool count `0` versus the authoritative battle damage assignment damage-pool count;
- legal-target count `0` versus the authoritative battle damage assignment legal-target count;
- existing-damage count `0` versus the authoritative battle damage assignment existing-damage count;
- lethal-damage-threshold count `0` versus the authoritative battle damage assignment lethal-damage-threshold count; and
- required-assignment count `0` versus the authoritative battle damage assignment required-assignment count.

Missing or null `battle.damageAssignment` payloads without an open authoritative damage-assignment window still report only the required-payload diagnostic and do not emit battle-damage count mismatch diagnostics.

## Rule Source

The five root PDF rule files remained present. `/tmp/riftbound_rules_pdf_text/` was checked during this batch from the root PDF extraction before documenting the slice.

Relevant latest rule anchors re-checked:

- core rules 417.1.a, 417.3.a and 417.6.c for battle damage assignment causing later simultaneous damage and unit-source attribution;
- core rules 460.2.c and 460.2.d for assigning battle damage before dealing the assigned damage;
- core rules 815 and 826 for Barrier and Back Row damage-assignment ordering/legality constraints; and
- `裁判FAQ_251023.pdf` questions 6.1-6.5 for assignment versus damage, lethal assignment, priority conflicts and impossible-damage assignment clarifications.

## Runtime Change

`src/Riftbound.Engine/MatchRecovery.cs` now adds missing-payload diagnostics for spectator battle damage assignment payloads. When the authoritative battle damage assignment window is open, missing or null spectator payloads still emit the required-payload error and additionally compare zeroed spectator dimensions against authoritative damage-pool, legal-target, existing-damage, lethal-threshold and required-assignment counts.

This changes only recovery diagnostic reporting. It does not change combat damage assignment legality, battle resolution, damage dealing, prompt rendering, hidden-source redaction, authoritative state serialization or valid replay behavior.

## Tests

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` now covers:

- missing `battle.damageAssignment` without an open authoritative battle damage assignment window: required-payload error and no count mismatch;
- missing `battle.damageAssignment` with an open authoritative battle damage assignment window: required-payload error plus all five zeroed count mismatches;
- `battle.damageAssignment = null` without an open authoritative battle damage assignment window: required-payload error and no count mismatch; and
- `battle.damageAssignment = null` with an open authoritative battle damage assignment window: required-payload error plus all five zeroed count mismatches.

Shared helpers build empty/open battle damage assignment fixtures and mutate spectator replay timing battle payloads consistently.

## Validation

- Focused battle-damage-assignment missing/null filter: `4/4` passed.
- Changed-class `MatchRecoveryTests`: `1971/1971` passed.
- Adjacent BattleDamageAssignment/AssignCombatDamage/Battle/SpectatorReplayTiming/Recovery filter: `2667/2667` passed.
- Backend full via `Riftbound.slnx`: `8303/8303` passed.
- `git diff --check` passed before code commit.
- Runtime/test anchored conflict-marker scan had no findings before code commit.

## Coordination

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 30 commits behind current `main` after code commit `b51a3e64` and with no commits ahead of `main`. `codex/rule-audit-remaining-20260615` had no commits ahead of `main`.

Project remains **NOT READY**. FullOfficial, frontend build/Chrome/formal E2E, real DB-backed Postgres smoke, remaining recovery/authoritative/spectator nested payload breadth and final readiness remain open.
