# Stage 4D-02C Natural Battle Damage Assignment Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文件把 4D-02B 后的下一步 P0-004 收口锁定为：让自然 active `START_BATTLE` lifecycle 可以进入服务端权威 `ASSIGN_COMBAT_DAMAGE` prompt / command runtime，而不是继续只依赖构造态 battle state 或 direct immediate combat resolver。

## Goal

Prove a narrow natural battle lifecycle:

`START_BATTLE` task -> `DECLARE_BATTLE` -> optional battle-response priority pass -> `ASSIGN_COMBAT_DAMAGE` prompt -> legal damage assignment command -> simultaneous damage / cleanup / battle close / battlefield control.

## Blocker

- Primary: P0-004 Spell Duel And Battle State Machine.
- Secondary: keeps P0-002 / P0-003 task queue and cleanup evidence honest, but does not try to close them fully.

## Current Facts

- 4D-02 accepted a focused spell-duel / battle task slice.
- 4D-02B accepted natural Shadow battle-response lifecycle evidence, limited to minimal `COMBAT_ASSIGNMENT` battle commands.
- `ASSIGN_COMBAT_DAMAGE` prompt/runtime already exists and is covered by constructed-state tests.
- `ResolveDeclareBattle` still performs most real battle damage / cleanup / result work synchronously for natural battles.

## Required Behavior

- A natural active `START_BATTLE` can open a battle damage assignment window for one minimal supported combat-assignment case.
- The prompt exposes `ASSIGN_COMBAT_DAMAGE`, battle id, battlefield id, assigning player, damage pool, legal targets, lethal thresholds, and assignment choices.
- The non-assigning player sees a non-actionable prompt and no hidden opponent standby details.
- Wrong player, wrong battle id, wrong battlefield id, stale prompt, invalid target, incomplete damage, over-assignment, and lethal-order violations reject without mutation.
- Legal assignment reuses existing simultaneous damage / lethal cleanup / `BATTLE_NO_RESULT` or `BATTLE_CLOSED` / battlefield control code.
- Reconnect during the assignment window preserves task / battle context and hidden-info redaction.

## Suggested Representative

- Build from an actual contested battlefield `START_BATTLE` task, not a manually constructed open damage-assignment state.
- Use a minimal `DECLARE_BATTLE` with required `COMBAT_ASSIGNMENT` only.
- Prefer two defenders where one has `Bulwark` or `Back Row`, because current prompt requirements already model assignment-ordering choices.
- Keep one one-on-one no-response battle path immediate for now if needed; this slice is only the first natural manual assignment branch.

## Suggested Write Scope

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- Optional new focused test file if cleaner, for example `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Expected Tests

Add focused tests that prove:

- natural active `START_BATTLE` with assignment-ordering defender opens `ASSIGN_COMBAT_DAMAGE` prompt instead of immediate battle close;
- prompt metadata includes battle id, battlefield id, damage pool, legal targets, lethal thresholds, and assignment choices;
- reconnect during that window preserves battle/task metadata and hidden-info redaction;
- wrong player / wrong battle id / wrong battlefield / stale prompt / invalid assignments reject with no mutation;
- legal assignment commits simultaneous damage, cleanup, battle close or no-result, and battlefield control through existing runtime;
- adjacent one-on-one immediate battle behavior stays stable unless deliberately migrated in this slice.

## Baseline Commands

Focused battle lifecycle baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~CombatDamage|FullyQualifiedName~BattleDamage|FullyQualifiedName~BattleState|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Reconnect"
```

Adjacent battlefield / cleanup / trigger regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~CombatDamage|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~OrderTriggers|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Backend full before A acceptance:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## No-Go

- Do not do a full combat rewrite.
- Do not implement all replacement / prevention / LayerEngine battle rules.
- Do not expand PaymentEngine or add new card representatives.
- Do not change frontend.
- Do not update `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- Do not close P0-004 / P0-005 / P1 / READY.

## Acceptance

A acceptance requires focused tests, adjacent tests, backend full test, `git diff --check`, audit/evidence docs, and checkpoint updates that keep the project **NOT READY**.
