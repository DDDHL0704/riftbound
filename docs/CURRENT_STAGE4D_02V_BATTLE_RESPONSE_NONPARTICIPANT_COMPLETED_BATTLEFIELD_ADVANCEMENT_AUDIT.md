# Stage 4D-02V Battle Response Nonparticipant Completed Battlefield Advancement Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

本切片验证 4D-02U 之后的 follow-up 边界：natural battle response 中真实 Shadow activation 作为非参战 response source，stack resolution 后保留 precise battlefield location；final immediate battle close 后，已完成 spell duel 的当前 battlefield 不会在同一推进链里重新开启 spell duel，而是推进下一处未完成 contested battlefield。

覆盖代表：

- 当前 battle / completed battlefield：`BF-DAMAGE`
- nonparticipant response source：`P2-SHADOW`
- declared defender：`P2-BULWARK`
- stack effect：Shadow stun `UNL-194/219`
- next unfinished contested battlefield：`BF-NEXT`

## Implemented Behavior

- 新增 guard 证明 `BF-DAMAGE` 已有 `BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId)`。
- Shadow 不在 current battle attacker / defender 列表中。
- Stack-open、stack-resolved returned-response、P2 final response pass 期间均不推进 `BF-NEXT`。
- Stack resolution 后 Shadow 仍 exhausted、仍在 P2 battlefield zone、仍保留 `ObjectLocations[ShadowObjectId].BattlefieldObjectId == BattlefieldObjectId`。
- P1 final response pass closes immediate battle without opening assignment.
- Final events do not emit `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` for completed `BF-DAMAGE`.
- Current `START_BATTLE:BF-DAMAGE` does not remain pending.
- `BF-NEXT` advances to `SPELL_DUEL_TASKS` / `SpellDuelFocus`, with event order `BATTLE_RESPONSE_PRIORITY_CLOSED` -> `BATTLE_CLOSED` -> `BATTLEFIELD_CONTROL_RESOLVED` -> `BF-NEXT BATTLEFIELD_CONTESTED` -> `BF-NEXT SPELL_DUEL_STARTED`.

## Runtime Notes

- No runtime changes were required.
- This guard makes the existing `AdvancePendingBattlefieldTasksAfterStateChange` policy explicit: battlefields with same-turn `SpellDuelCompleted` markers are skipped for immediate spell-duel re-entry during this advancement chain.
- The guard preserves the 4D-02U source-location fix; it does not hide Shadow by moving it, removing it, or dropping its precise battlefield id.

## Files

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Guardrails

- No frontend, LayerEngine, PaymentEngine broad rewrite, or coverage matrix work was done.
- This slice only locks same-turn completed battlefield skip policy for the representative immediate battle response branch.
- Full official recontest semantics across later turns, repeated battlefield entries, multiple response sources, and non-representative battle outcomes remain later P0-004 work.
- P0-004 remains open for full official battle lifecycle breadth.
- P0-002, P0-003, P0-005, P1, READY, and full-card official coverage remain open.

## Verdict

4D-02V closes the explicit guard gap between nonparticipant response source precise location preservation and same-turn completed battlefield advancement policy. It does **not** claim full combat official closure or READY status. Project remains **NOT READY**.
