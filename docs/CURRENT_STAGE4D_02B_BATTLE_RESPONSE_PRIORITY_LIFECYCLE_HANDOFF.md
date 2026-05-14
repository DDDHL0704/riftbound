# Stage 4D-02B Battle-Response Priority Lifecycle Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文件把 4D-03AH 后的下一步 P0 收口锁定为 4D-02B：把 Shadow swift / reaction representative 从“可构造 battle-response 状态”推进到服务端自然 battle lifecycle 产生的 battle-response priority window。该切片服务于 P0-004，不继续扩 PaymentEngine breadth，不启动 LayerEngine 或 frontend。

## Goal

Prove that a real battle lifecycle can produce the server-authoritative priority window needed by battle-response swift / reaction abilities:

`DECLARE_BATTLE` / contested battlefield task queue -> spell-duel / battle task -> battle-response priority window -> legal swift / reaction prompt -> stack pass-pass -> resolution / cleanup.

## Blocker

- Primary: P0-004 Spell Duel And Battle State Machine.
- Secondary: keeps P0-005 `ACTIVATE_ABILITY` battle-response representative honest in a real battle window, but does not continue PaymentEngine breadth.

## Current Facts

- 4D-02 already accepted a focused spell-duel / battle task slice, but it explicitly did not close full official battle lifecycle.
- 4D-03Q accepted Shadow swift stun as a focused representative, but its battle-response state is constructed directly rather than proven through the natural battle task lifecycle.
- 4D-03AH now classifies PaymentEngine action windows, so continuing PaymentEngine representative breadth has lower value than binding the existing reaction representative to P0-004 lifecycle evidence.

## Required Behavior

- A contested battlefield can progress through server task state into a battle-response priority window without frontend inference.
- The battle-response prompt exposes legal Shadow swift / reaction `ACTIVATE_ABILITY` only for the correct player, source, battlefield, attacker, and timing.
- Illegal player / source / target / timing submissions are rejected without mutation.
- Successful activation creates the existing stack item and preserves priority / pass-pass resolution semantics.
- Reconnect / prompt metadata must preserve task, battle, battlefield, and priority information without hidden-info leakage.

## Suggested Write Scope

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
- `tests/Riftbound.ConformanceTests/ShadowActivatedAbilityTests.cs`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- Optional only if needed: `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`

## Expected Tests

Add focused tests that prove:

- natural contested battlefield task progression reaches battle-response priority for the correct battlefield;
- Shadow prompt is exposed in that natural window and hidden outside it;
- Shadow activation from the natural window pays cost, exhausts source, creates stack, and resolves after pass-pass;
- wrong player / wrong battlefield / stale target commands reject with no mutation;
- prompt and reconnect snapshots expose task/battle context without leaking hidden details.

## Baseline Commands

Focused battle-response baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Adjacent battlefield / payment classification baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

After focused and adjacent pass, run backend full before A acceptance:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## No-Go

- Do not do a full combat rewrite.
- Do not start LayerEngine.
- Do not add new card representatives.
- Do not continue expanding PaymentEngine family breadth in this slice.
- Do not change frontend code.
- Do not update `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- Do not close P0-004 / P0-005 / P1 / READY.

## Acceptance

A acceptance requires focused tests, adjacent tests, backend full test, `git diff --check`, audit/evidence docs, and checkpoint updates that keep the project **NOT READY**.
