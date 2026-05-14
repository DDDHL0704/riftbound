# Stage 4D-02C Natural Battle Damage Assignment Audit

日期：2026-05-14
结论：**FOCUSED LIFECYCLE ACCEPTED / PROJECT NOT READY**

本切片把已有 `ASSIGN_COMBAT_DAMAGE` prompt/runtime 从构造态 battle state 推进到一条自然 active `START_BATTLE` lifecycle。服务端现在可在 minimal `DECLARE_BATTLE + COMBAT_ASSIGNMENT` 且多防守者包含 `Bulwark` / `Back Row` assignment-ordering 的场景中，打开 battle damage assignment window，而不是继续即时结算。

## Implemented Behavior

- active contested `START_BATTLE` 的 minimal `DECLARE_BATTLE` 可以进入 `BATTLE_DAMAGE_ASSIGNMENT_OPENED`。
- `ActionPrompt` 暴露 `ASSIGN_COMBAT_DAMAGE`、battle id、battlefield id、assigning player、damage pool、legal targets、lethal thresholds、required assignments 与 assignment choices。
- pending `START_BATTLE` task queue 不会阻塞 open damage assignment window。
- reconnect snapshot 保留 battle/task metadata，并继续隐藏对手 face-down standby 细节。
- wrong player、wrong battle id、wrong battlefield id、stale prompt、invalid target 等 command 继续 no-mutation reject。
- legal assignment 复用既有 simultaneous damage / cleanup / battle close / battlefield control runtime，并在结算后不残留 matching `START_BATTLE` task。
- 一对一 no-response immediate battle representative 保持稳定。
- 4D-02B Shadow battle-response path 保持优先：有合法 response prompt 时仍先开 battle-response priority；response pass 后可进入 assignment 分支。

## Modified Files

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- `docs/CURRENT_STAGE4D_02C_NATURAL_BATTLE_DAMAGE_ASSIGNMENT_AUDIT.md`
- `docs/CURRENT_STAGE4D_02C_NATURAL_BATTLE_DAMAGE_ASSIGNMENT_EVIDENCE.md`
- current checkpoint / audit docs

## Guardrails

- No frontend runtime was changed.
- No LayerEngine work was started.
- No PaymentEngine breadth was expanded.
- No card representative or coverage matrix status was changed.
- Complex declare-battle option preservation remains outside this slice.
- P0-004 is narrowed but remains open for full official battle lifecycle.
- P0-005, P1, READY, and full-card official coverage remain open.

## Verdict

4D-02C provides focused evidence that natural battle task lifecycle can produce a server-authoritative damage assignment prompt and complete through existing assignment runtime. It does **not** claim full combat official closure or READY status. Project remains **NOT READY**.
