# Stage 4D-223BE Recovery Spectator Battle-Damage-Assignment Payload-Shape Count Drift Audit

Date: 2026-06-18 10:41 CST

Owner: A_MAIN

Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`

Branch / code commit: `main` / `eea84190`

## Scope

This shard closes the follow-on recovery-validator gap for spectator replay-frame timing `battle.damageAssignment` shape errors.

When authoritative state has an open battle damage assignment window, spectator timing payloads that provide a non-object `battle.damageAssignment` now report:

- the existing required-payload diagnostic;
- damage-pool count `0` versus the authoritative battle damage assignment damage-pool count;
- legal-target count `0` versus the authoritative battle damage assignment legal-target count;
- existing-damage count `0` versus the authoritative battle damage assignment existing-damage count;
- lethal-damage-threshold count `0` versus the authoritative battle damage assignment lethal-damage-threshold count; and
- required-assignment count `0` versus the authoritative battle damage assignment required-assignment count.

Non-object `battle.damageAssignment` payloads without an open authoritative damage-assignment window still report only the required-payload diagnostic and do not emit battle-damage count mismatch diagnostics.

## Rule Source

The five root PDF rule files remained present. `/tmp/riftbound_rules_pdf_text/` remained available from the root PDF extraction and was checked before documenting the slice.

Relevant latest rule anchors re-checked:

- core rules 417.1.a, 417.3.a and 417.6.c for battle damage assignment causing later simultaneous damage and unit-source attribution;
- core rules 460.2.c and 460.2.d for assigning battle damage before dealing the assigned damage;
- core rules 815 and 826 for Barrier and Back Row damage-assignment ordering/legality constraints; and
- `裁判FAQ_251023.pdf` questions 6.1-6.5 for assignment versus damage, lethal assignment, priority conflicts and impossible-damage assignment clarifications.

## Runtime Change

`src/Riftbound.Engine/MatchRecovery.cs` now reuses the missing battle damage assignment diagnostics when spectator `battle.damageAssignment` is present but not an object. The validator continues to emit the required-payload error first; when the authoritative battle damage assignment window is open, it also reports zeroed spectator dimensions against authoritative damage-pool, legal-target, existing-damage, lethal-threshold and required-assignment counts.

This changes only recovery diagnostic reporting. It does not change combat damage assignment legality, battle resolution, damage dealing, prompt rendering, hidden-source redaction, authoritative state serialization or valid replay behavior.

## Tests

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` now covers:

- non-object `battle.damageAssignment` without an open authoritative battle damage assignment window: required-payload error and no count mismatch; and
- non-object `battle.damageAssignment` with an open authoritative battle damage assignment window: required-payload error plus all five zeroed count mismatches.

The existing battle damage assignment payload-shape drift test remains in place for same-payload shape coverage.

## Validation

- Focused battle-damage-assignment payload-shape filter: `2/2` passed.
- Changed-class `MatchRecoveryTests`: `1973/1973` passed.
- Adjacent BattleDamageAssignment/AssignCombatDamage/Battle/SpectatorReplayTiming/Recovery filter: `2669/2669` passed.
- Backend full via `Riftbound.slnx`: `8305/8305` passed.
- `git diff --check` passed before code commit.
- Runtime/test anchored conflict-marker scan had no findings before code commit.

## Coordination

External UI followup worktree `/Users/dinghaolin/MyProjects/riftbound-codex-ui-followup-20260616` on `codex/ui-followup-20260616` was clean at `01364ee2`, 32 commits behind current `main` after code commit `eea84190` and with no commits ahead of `main`. `codex/rule-audit-remaining-20260615` had no commits ahead of `main`.

Project remains **NOT READY**. FullOfficial, frontend build/Chrome/formal E2E, real DB-backed Postgres smoke, remaining recovery/authoritative/spectator nested payload breadth and final readiness remain open.
