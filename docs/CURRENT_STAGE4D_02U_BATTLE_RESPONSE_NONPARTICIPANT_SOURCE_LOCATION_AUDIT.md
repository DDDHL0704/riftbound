# Stage 4D-02U Battle Response Nonparticipant Source Location Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

## Scope

本切片验证 natural battle response 中真实 Shadow activation 作为非参战 response source 时，stack resolution 后仍保留 source 的 precise `ObjectLocation.BattlefieldObjectId`，不会把仍在 battlefield zone 的 source 退化为 unknown battlefield。

覆盖代表：

- 当前 battle：`BF-DAMAGE`
- declared defender：`P2-BULWARK`
- nonparticipant response source：`P2-SHADOW`
- stack effect：Shadow stun `UNL-194/219`
- 后续 contested battlefield：`BF-NEXT`

## Implemented Behavior

- 新增 guard 证明 Shadow 初始位于 `BF-DAMAGE`，但不在 current battle attacker / defender 列表中。
- Shadow activation stack-open 期间不推进 `BF-NEXT`。
- Stack pass-pass resolution 后回到 battle response priority，且仍不推进 `BF-NEXT`。
- `ObjectLocations[ShadowObjectId]` 在 activation 后和 stack resolution 后均保持：
  - `PlayerId == "P2"`
  - `Zone == "BATTLEFIELD"`
  - `BattlefieldObjectId == BattlefieldObjectId`
- Shadow 仍在 `PlayerZones["P2"].Battlefields`，并保留 exhausted 状态。

## Runtime Notes

- `ApplyResolvedStackSourceLocation` 现在对仍在 battlefield zone 的 stack source 保留当前 precise battlefield id，只要 stack item 没有明确 battlefield movement destination，且当前 object location 本身仍是 `BATTLEFIELD`。
- 明确 destination 仍优先：`BATTLEFIELD:<id>` 继续使用 stack item destination，不受本切片影响。
- Base / graveyard / banished source reconciliation 保持原语义。
- `NaturalBattleResponseActivationAssignmentAdvancesNextContestedBattlefieldTask` 的 fixture 调整为让 Shadow 成为 declared defender，并使用 `ShadowResponseLegalAssignments()`。原因是本切片修正后，非参战 Shadow 会正确保留在当前 concrete battlefield；该旧 guard 的验收目标是 activation-returned assignment close 后推进 `BF-NEXT`，不应隐含依赖丢失 Shadow battlefield identity 来清空当前 battlefield。

## Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

## Guardrails

- No frontend, LayerEngine, PaymentEngine broad rewrite, or coverage matrix work was done.
- This slice stops at stack-resolved / returned-response source location preservation.
- Nonparticipant response source after final battle close and possible current-battlefield recontest remains a later battle lifecycle edge.
- P0-004 remains open for full official battle lifecycle breadth.
- P0-002, P0-003, P0-005, P1, READY, and full-card official coverage remain open.

## Verdict

4D-02U closes the precise object-location gap introduced by nonparticipant battle response sources resolving stack items without movement destinations. It does **not** claim full combat official closure or READY status. Project remains **NOT READY**.
