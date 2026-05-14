# Stage 4D-02U Battle Response Nonparticipant Source Location Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 4D-02T 之后的下一批服务端实现交接。它只锁定 natural battle response 中真实 Shadow activation 作为非参战 response source 结算后，服务端仍必须保留该 source 的 precise `ObjectLocation.BattlefieldObjectId`。不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

4D-02T 为 activation-returned assignment cleanup path 补齐了两个最小 runtime 修正：battle-response declaration replay 可容忍已因 response cost 横置的参战者，且 active battle participant stack source 在无 movement destination 的 stack resolution 后保留 precise battlefield identity。

仍有一个相邻缺口：4D-02P / 4D-02Q 的 representative immediate / post-payment path 中，Shadow 是合法 battle response source，但不是 declared attacker / defender。当前 `ApplyResolvedStackSourceLocation` 只对 active battle participant 保留 precise battlefield id；非参战 Shadow 在 stack item 没有 battlefield destination 时有风险被重写成 `Zone == "BATTLEFIELD"` 且 `BattlefieldObjectId == null`。这会削弱 authoritative object-location、lane snapshot、cleanup / contest 后续推导的一致性。

本切片只补这个对象位置护栏：battle response source 仍在 battlefield zone 时，stack resolution 不应丢失它既有的 concrete battlefield id。

## 2. Target Behavior

最小代表流程：

1. `BF-DAMAGE` 已完成 spell duel，当前 active task 是 `task:start-battle:BF-DAMAGE`；`BF-NEXT` 也处于 contested queue 中。
2. P1 在 `BF-DAMAGE` 有 attacker；P2 在同一战场有 `P2-BULWARK` 和 `P2-SHADOW`。
3. P1 提交 supported `DECLARE_BATTLE + COMBAT_ASSIGNMENT`，但 declared defenders 只包含 `P2-BULWARK`，所以 `P2-SHADOW` 是 response source 而非 battle participant。
4. P2 在 battle response priority 中激活 Shadow targeting P1 attacker：
   - 输出 `ABILITY_ACTIVATED` / `UNIT_EXHAUSTED` / `COST_PAID` / `STACK_ITEM_ADDED`；
   - stack item 没有 battlefield movement destination；
   - `P2-SHADOW` 仍在 P2 battlefield zone，且 precise location 仍指向 `BF-DAMAGE`。
5. P2 / P1 pass stack priority 后：
   - stack resolved 并回到 battle response priority；
   - `ObjectLocations[P2-SHADOW]` 仍为 `PlayerId == "P2"`、`Zone == "BATTLEFIELD"`、`BattlefieldObjectId == "BF-DAMAGE"`；
   - P2 battlefield zone 仍包含 Shadow；
   - 期间不得推进 `BF-NEXT`。
6. 可继续 final response pass-pass，确保 immediate close / advancement 不依赖丢失 precise source location；但本切片核心验收点是 stack resolution 后的 source location preservation。

## 3. Suggested Write Scope

Owner：B 服务端规则 / 协议 / 测试实现（当前 Raman）。

允许写入：

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

仅 runtime gap 时允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`

禁止写入：

- frontend。
- PaymentEngine broad rewrite、LayerEngine。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- `riftbound-dotnet.sln`。

## 4. Implementation Notes

- Prefer a failing guard first.
- Suggested test name:
  - `NaturalBattleResponseActivationPreservesNonParticipantSourceBattlefieldLocationAfterStackResolution`
- Suggested fixture:
  - start from `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId])`;
  - assert initial `ObjectLocations[ShadowObjectId].BattlefieldObjectId == BattlefieldObjectId`;
  - declare battle with `[AttackerObjectId]` vs `[BulwarkDefenderObjectId]`, `OptionalCosts: ["COMBAT_ASSIGNMENT"]`;
  - activate Shadow from P2 response priority targeting `AttackerObjectId`;
  - after stack pass-pass, assert Shadow is exhausted, remains in `PlayerZones["P2"].Battlefields`, and keeps precise `BattlefieldObjectId`.
- If the guard fails, the expected minimal runtime change is in `ApplyResolvedStackSourceLocation`: when the source object remains in `zones.Battlefields`, no stack battlefield destination exists, and the current object location is already `Zone == "BATTLEFIELD"`, preserve `currentLocation.BattlefieldObjectId` for any battlefield source, not only active battle participants.
- Preserve explicit movement destinations exactly as today. Do not alter stack items whose destination is `BATTLEFIELD:<id>`, `BASE`, graveyard, banished, etc.
- Keep player-submitted `DECLARE_BATTLE` ready / face-up legality unchanged.

## 5. Focused Tests

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Focused acceptance should include:

- nonparticipant Shadow response source keeps precise battlefield id before activation;
- activation opens stack and does not advance `BF-NEXT`;
- stack resolution returns to battle response and does not advance `BF-NEXT`;
- stack resolution leaves `ObjectLocations[ShadowObjectId].BattlefieldObjectId == BattlefieldObjectId`;
- explicit movement destination behavior remains unchanged by existing adjacent object-location / move-unit tests.

## 6. Adjacent Tests

Recommended adjacent command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Final per-slice gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

## 7. No-Go

- Do not rewrite combat damage assignment.
- Do not broaden Shadow, swift, reaction, PaymentEngine or LayerEngine behavior beyond what this guard reveals.
- Do not modify frontend task UI.
- Do not update card coverage matrix.
- Do not close P0-002, P0-003, P0-004, P0-005, P1 or READY.
- Do not mark active goal complete.
