# Stage 4D-04M LayerEngine Minimum-Power Ledger Handoff

日期：2026-05-15
结论：**HANDOFF READY / PAUSE POINT / PROJECT NOT READY**

本 handoff 是 A 主控在 4D-04L-B 之后对 P1-001 的下一步拆分。4D-04L-B 已经让 `ApplyPowerModifier` 路径保存 source/effect-aware ledger metadata，但 minimum-power floor 路径仍只有 event payload 能看出 requested delta、applied delta 与 floor 的差异。下一枚切片只补这一层 metadata / verifier，不重写完整 LayerEngine。

## 1. Target

下一枚建议 B 切片：**4D-04M LayerEngine minimum-power power-modifier ledger exactness**。

目标：

- 保留当前 server-authoritative arithmetic 行为，不替换所有 power mutation path。
- 对 `MinimumPowerAfterModifier > 0` 的 representative power modifier 路径，明确区分 requested power delta、applied power delta、minimum power floor 与 resulting power。
- 让 ledger-backed `ContinuousEffectState` / snapshot view 能审计 minimum-power floor 代表路径，而不破坏现有 `powerDelta` / `basePower` / `effectivePower` 兼容字段。
- 继续明确这只是 LayerEngine foundation，不代表 timestamp、dependency、source ordering、keyword gain/loss、multi-equipment/static aura、完整 minimum-power layer ordering 或 full official LayerEngine 完成。

## 2. Input Facts

- 当前分支为 `main`，工作树只允许保留未跟踪 `riftbound-dotnet.sln`；该文件不得触碰、暂存或提交。
- 4D-04L-B 已新增 `PowerModifierLedgerEntry`，但当前 ledger 的 `PowerDelta` 表达 applied delta，不显式保留 requested delta 或 minimum-power floor。
- `CoreRuleEngine.ApplyPowerModifier` 当前会计算 `rawResultingPower`、`resultingPower`、`appliedPowerDelta`，并在 `POWER_MODIFIED_UNTIL_END_OF_TURN` event payload 中输出 `powerDelta`、`appliedPowerDelta`、`minimumPower`、`resultingPower`。
- Existing minimum-power representatives 包括：
  - `OGN·097/298`《爆裂球果仙灵》：target 2 power，requested -2，floor to 1，applied -1。
  - `OGN·093/298`《烟幕弹》：target 3 power，requested -4，floor to 1，applied -2。
  - `OGN·095/298`《“敲”诈》：target already 1 power，requested -1，floor to 1，applied 0。
  - `OGN·116/298`《千尾监视者》与 `OGN·266/298`《虹吸能量》覆盖多目标 / all-battlefield floor representative。
- P1-001 仍为 `PARTIALLY RESOLVED`；4D-04M 只能降低 minimum-power ledger/audit residual，不能关闭完整 LayerEngine。

## 3. Suggested B Write Scope

默认写锁：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- focused tests under `tests/Riftbound.ConformanceTests/`

可选且仅最小必要：

- A small engine helper/model under `src/Riftbound.Engine/` if it keeps arithmetic behavior unchanged and only centralizes power-modifier metadata.

禁止触碰：

- frontend runtime.
- card matrix JSON.
- broad PaymentEngine.
- battle lifecycle / task queue semantics.
- equipment runtime outside this focused power/layer ledger boundary.
- fullOfficial / READY.
- `riftbound-dotnet.sln`.

## 4. Required Behavior

1. Existing `Power`, `UntilEndOfTurnPowerModifier`, `basePower`, `effectivePower` and current snapshot behavior remain compatible.
2. Existing non-floor 4D-04L ledger metadata remains green, including Switcheroo source/effect metadata.
3. Existing minimum-power arithmetic representatives remain green: Blastcone Sprout, Siphon Energy, Thousand-Tailed Watcher, Smoke Bomb, Extortion and HASTE_READY Thousand-Tailed Watcher.
4. At least one floor representative must verify requested delta vs applied delta vs minimum power in state and/or snapshot metadata. Preferred focused path: Smoke Bomb or Blastcone Sprout, because requested and applied delta differ.
5. Extortion-like applied-zero floor behavior must remain no-mutation compatible and must not create misleading nonzero power modifier views unless B intentionally defines a zero-delta audit entry and proves snapshot compatibility.
6. Full LayerEngine residuals remain explicit: timestamp, dependency, source ordering, keyword gain/loss, multiple equipment/static aura interactions, minimum-power ordering across layers and full official coverage.

## 5. Acceptance

Minimum A-side acceptance after B diff:

1. Focused 4D-04M minimum-power ledger guard proves requested/applied/floor metadata.
2. Current 4D-04L source/effect metadata guard remains green.
3. Adjacent power/layer/minimum regression remains green.
4. Backend full test is strongly preferred because this touches public state shape.
5. `git diff --check` passes.
6. Completion docs continue to say P1-001 remains open and project `NOT READY`.

## 6. Suggested Verification

Implementation-before baseline is recorded in `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_BASELINE_EVIDENCE.md`.

Focused minimum-power foundation baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~CoreRuleEnginePlaysSiphonEnergyBattlefieldPowerSplit|FullyQualifiedName~CoreRuleEnginePlaysThousandTailedWatcherAllEnemyUnitsMinus3|FullyQualifiedName~CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack|FullyQualifiedName~CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn|FullyQualifiedName~CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn"
```

Adjacent power / layer / minimum regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~SiphonEnergy|FullyQualifiedName~ThousandTailed|FullyQualifiedName~SmokeBomb|FullyQualifiedName~Extortion|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~UntilEndOfTurnPowerModifier"
```

Final hygiene:

```sh
git diff --check
```

## 7. Non-Goals

- Do not replace all power mutations with a full LayerEngine in this slice.
- Do not change official minimum-power semantics beyond metadata / verifier exactness.
- Do not alter frontend behavior.
- Do not update card matrix JSON or `fullOfficial`.
- Do not close P1-001, P1-002, frontend final validation or READY.
- Do not call `update_goal complete`.

## 8. Handoff Verdict

4D-04M is ready as a B implementation handoff, but no B worker is dispatched and no write lock is open until A explicitly opens it. Project remains **NOT READY**.
