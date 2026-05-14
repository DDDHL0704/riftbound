# Stage 4D-02B Battle-Response Priority Lifecycle Audit

日期：2026-05-14
结论：**FOCUSED LIFECYCLE ACCEPTED / PROJECT NOT READY**

本切片把 Shadow swift / reaction representative 从构造态推进到真实 battle task lifecycle。服务端现在可在 contested battlefield `START_BATTLE` task 的 `DECLARE_BATTLE` 后，根据当前 priority player 是否拥有合法 battle-response `ACTIVATE_ABILITY`，开启 battle-response priority window；若没有合法 response，保留既有即时战斗结算路径。

## Implemented Behavior

- `DECLARE_BATTLE` / contested `START_BATTLE` task 可先产生 `BATTLE_RESPONSE_PRIORITY_OPENED`，并保持 `BattleState.IsActive`、`TimingState=NEUTRAL_CLOSED`、`PriorityPlayerId` 指向防守方。
- `ActionPrompt` / reconnect snapshot 在该窗口暴露 stack-priority prompt、related battlefield / battle id 与 pending battle task metadata。
- Shadow prompt 只在自然 battle-response window 中向正确 priority player 暴露，目标仍限定为同一战场正在进攻的敌方单位。
- Shadow activation 从自然窗口支付费用、横置 source、创建 ordinary stack item；stack pass-pass 后结算 stun，并返回 battle-response priority window。
- battle-response 双方 pass 后继续调用既有 battle resolver，输出 `BATTLE_RESPONSE_PRIORITY_CLOSED` 与原战斗 / cleanup / battlefield-control event。
- 无合法 battle-response action 的普通 `DECLARE_BATTLE` 仍即时结算，避免扩大为完整 combat rewrite。
- 该 deferred response path 只覆盖最小代表战斗命令形态：必需 `COMBAT_ASSIGNMENT`，且无额外 battlefield target / payment-resource / brush replacement 选择；更复杂战斗选项继续即时结算，等待 full combat lifecycle 收口。

## Modified Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ShadowActivatedAbilityTests.cs`
- `docs/CURRENT_STAGE4D_02B_BATTLE_RESPONSE_PRIORITY_LIFECYCLE_AUDIT.md`
- `docs/CURRENT_STAGE4D_02B_BATTLE_RESPONSE_PRIORITY_LIFECYCLE_EVIDENCE.md`
- current checkpoint / audit docs

## Guardrails

- No new card representative was added.
- No frontend runtime, coverage matrix, LayerEngine, or PaymentEngine family breadth was touched.
- No extra declare-battle option preservation layer was introduced; complex battlefield choices remain outside this focused slice.
- P0-004 is narrowed but remains open for full official battle lifecycle.
- P0-005, P1, READY, and full-card official coverage remain open.

## Verdict

4D-02B provides focused evidence that battle-response swift / reaction prompt can arise from a natural server task lifecycle. It does **not** claim full combat official closure or READY status. Project remains **NOT READY**.
