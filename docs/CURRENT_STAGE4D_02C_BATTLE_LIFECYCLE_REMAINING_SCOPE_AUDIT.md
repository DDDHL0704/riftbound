# Stage 4D-02C Battle Lifecycle Remaining Scope Audit

日期：2026-05-14
结论：**AUDIT COMPLETE / HANDOFF REQUIRED / PROJECT NOT READY**

本文是 4D-02B 后的 A 侧剩余范围审计。4D-02B 已证明 Shadow swift / reaction prompt 可以从自然 contested `START_BATTLE` task lifecycle 产生，但这仍是 focused representative，不等于 P0-004 battle lifecycle full official 关闭。

## Evidence Inspected

- `docs/A_MASTER_AGENT_GOAL.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_02B_BATTLE_RESPONSE_PRIORITY_LIFECYCLE_AUDIT.md`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`
- `tests/Riftbound.ConformanceTests/ShadowActivatedAbilityTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`

## Current Green Surface

- `START_SPELL_DUEL` / `START_BATTLE` tasks can be created and shown in prompt / reconnect metadata.
- Active `START_BATTLE` prompt filters current battlefield attacker / defender choices and rejects wrong battlefield, base, stale, face-down standby, equipment, spell, and rune objects without mutation.
- 4D-02B can open a natural battle-response priority window for minimal `COMBAT_ASSIGNMENT` battle commands when the defending priority player has legal Shadow response.
- Shadow can activate from that natural window, resolve on stack pass-pass, return to battle-response priority, and allow the battle to close after both players pass.
- `ASSIGN_COMBAT_DAMAGE` command / prompt / runtime exists and has no-mutation guards, stale prompt guard, hidden-info redaction, simultaneous damage commit, lethal cleanup, no-result, battle close, and battlefield control representative tests.

## Remaining P0-004 Gaps

- Natural active `START_BATTLE` still mostly resolves battle through `ResolveDeclareBattle`'s immediate combat resolver; the existing `ASSIGN_COMBAT_DAMAGE` runtime is strongly tested, but it is not yet the default natural continuation from active battle task / battle-response lifecycle.
- 4D-02B intentionally limits deferred response to minimal `COMBAT_ASSIGNMENT` commands with no extra battlefield target, payment resource, or brush replacement choices. Complex battle option preservation remains outside the slice.
- Multi-attacker / multi-defender / Bulwark / Back Row damage assignment has prompt/runtime coverage in constructed battle states, but does not yet have a narrow natural `START_BATTLE` -> battle-response/pass -> `ASSIGN_COMBAT_DAMAGE` lifecycle guard.
- Direct combat resolver still mixes declaration, damage calculation, lethal cleanup, battle result, battlefield held/conquer triggers, pending payments, battle close, and battlefield control update in one path. That remains risky until the next slice proves at least one natural manual damage-assignment branch.
- Full official battle lifecycle still lacks broad replacement / prevention / LayerEngine, control freeze-release, all battle-result ordering, all multi-combat combinations, and FAQ matrix evidence.

## Next Recommended Slice

Proceed with `4D-02C Natural Battle Damage Assignment Lifecycle`.

Goal: bind the already-existing `ASSIGN_COMBAT_DAMAGE` prompt/runtime to a real active `START_BATTLE` lifecycle for a minimal official combat-assignment branch, without doing a full combat rewrite.

Recommended representative:

- active contested `START_BATTLE`;
- minimal `DECLARE_BATTLE` with `COMBAT_ASSIGNMENT`;
- two defenders where one has Bulwark or Back Row, or another current supported assignment-ordering keyword condition;
- optional battle-response priority window may open only when legal response exists, but the final continuation must reach `ASSIGN_COMBAT_DAMAGE` prompt rather than immediate battle close;
- legal `ASSIGN_COMBAT_DAMAGE` then commits simultaneous damage, cleanup, battle result, battle close, and battlefield control through existing runtime.

## No-Go

- Do not rewrite all combat.
- Do not start LayerEngine.
- Do not expand PaymentEngine.
- Do not change frontend.
- Do not update card coverage matrix.
- Do not preserve complex declare-battle option payloads unless the slice explicitly implements and tests them.
- Do not close P0-004, P0-005, P1, or READY.

## Verdict

P0-004 is narrowed by 4D-02B but remains open. The next useful step is not another Shadow-focused representative; it is a narrow natural battle lifecycle binding for damage assignment.
