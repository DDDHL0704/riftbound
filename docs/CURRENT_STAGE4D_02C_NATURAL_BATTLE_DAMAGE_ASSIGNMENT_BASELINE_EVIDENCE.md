# Stage 4D-02C Natural Battle Damage Assignment Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文件记录 4D-02C handoff 前当前 HEAD 的测试基线。该基线只证明已有 battle lifecycle / damage assignment / cleanup surfaces 仍为绿色；不代表自然 battle damage assignment lifecycle 已实现。

## Baseline Commands

Focused battle lifecycle baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~CombatDamage|FullyQualifiedName~BattleDamage|FullyQualifiedName~BattleState|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Reconnect"
```

Result: passed 319/319.

Adjacent battlefield / cleanup / trigger regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~CombatDamage|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~OrderTriggers|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result: passed 598/598.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 4188/4188.

Diff hygiene:

```sh
git diff --check
```

Result: passed after handoff docs/checkpoint updates.

## Baseline Meaning

- Current `ASSIGN_COMBAT_DAMAGE` prompt/runtime remains green.
- Current 4D-02B natural Shadow battle-response lifecycle remains green through the focused filters.
- Current active `START_BATTLE` prompt guard and immediate battle representative remain green.
- Current adjacent battlefield / cleanup / trigger / PaymentEngine audit filters remain green.

## Known Gap

This baseline does not prove that a natural active `START_BATTLE` can open `ASSIGN_COMBAT_DAMAGE`. That is the implementation target for 4D-02C.

Project remains **NOT READY**.
