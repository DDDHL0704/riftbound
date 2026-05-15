# Stage 4D-04L LayerEngine Foundation Handoff

日期：2026-05-15
结论：**HANDOFF READY / PAUSE POINT / PROJECT NOT READY**

本 handoff 是 A 主控在 4D-04K-B 之后对 P1-001 的下一步拆分。本批不要求 B 立刻实现完整 LayerEngine；目标是把当前 `ContinuousEffectState` / `UntilEndOfTurnPowerModifier` / Ornn dynamic recompute 的真实边界拆成下一枚可验收的 foundation slice，避免后续把现有 snapshot layer view 误判为完整连续效果层系统。

## 1. Target

下一枚建议 B 切片：**4D-04L LayerEngine foundation / source-aware power modifier ledger**。

目标：

- 保留当前 server-authoritative arithmetic 行为，不重写全部 power / keyword / equipment static 规则。
- 为现有 until-end power modifier representative 建立 source-aware / effect-aware ledger 或 verifier foundation，使后续 LayerEngine 能从“修改当前 Power + 聚合 modifier”迁移到“base value + ordered effects + derived effective value”。
- 明确现有 `ContinuousEffectState` 只是 snapshot/report view，不是完整 official LayerEngine。
- 继续保留 timestamp、dependency、full static equipment modifiers、keyword gain/loss、multi-source ordering、minimum-power layering 与 full official LayerEngine deferred。

## 2. Input Facts

- 当前分支为 `main`，工作树只允许保留未跟踪 `riftbound-dotnet.sln`；该文件不得触碰、暂存或提交。
- `src/Riftbound.Engine/MatchSession.cs` 已有 `ContinuousEffectState` / `ContinuousEffectLayers`，会从 `UntilEndOfTurnEffects` 和 `CardObjectState.UntilEndOfTurnPowerModifier` 派生 snapshot view。
- `ResolveBasePower` 仍按 `Power - UntilEndOfTurnPowerModifier` 反推 base power。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的 `ApplyPowerModifier` 仍直接修改 `CardObjectState.Power`，再累加 `UntilEndOfTurnPowerModifier`。
- Ornn dynamic recompute 目前是窄 representative：从 registry source unit power 或 current minus modifier 得到 stable base，再加 friendly public equipment count 与 until-end modifier。
- P1-001 在 `docs/CURRENT_SERVER_RULE_AUDIT.md` 仍是 `PARTIALLY RESOLVED / 服务端持续效果层视图已落地，完整 LayerEngine 仍待实现`。

## 3. Suggested B Write Scope

默认写锁：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- focused tests under `tests/Riftbound.ConformanceTests/`

可选且仅最小必要：

- New small engine helper under `src/Riftbound.Engine/` if it keeps arithmetic behavior unchanged and only centralizes source-aware effect metadata.
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` only if keyword / catalog closure wording needs a guard.

禁止触碰：

- frontend runtime.
- card matrix JSON.
- broad PaymentEngine.
- battle lifecycle / task queue semantics.
- equipment runtime outside the focused power/layer ledger boundary.
- fullOfficial / READY.
- `riftbound-dotnet.sln`.

## 4. Required Behavior

1. Existing `Power` / `basePower` / `effectivePower` snapshot behavior remains compatible with current tests.
2. Existing Ornn entry-time and dynamic friendly-equipment recompute representatives remain green.
3. Existing until-end power modifier representative paths remain green, including Switcheroo, battle-response assignment effective power, trigger payment power modifier, pending-task zero-power guard, and turn-end cleanup.
4. The new foundation must expose or verify source/effect metadata for at least one representative power modifier path, without claiming full timestamp/dependency LayerEngine completion.
5. Full LayerEngine residuals remain explicit: timestamp, dependency, source ordering, keyword gain/loss, multiple equipment/static aura interactions, minimum-power layering, and full official coverage.

## 5. Acceptance

Minimum A-side acceptance after B diff:

1. Focused LayerEngine foundation tests prove the new source-aware / effect-aware boundary.
2. Current focused baseline remains green.
3. Adjacent power/layer/equipment regression remains green.
4. `git diff --check` passes.
5. Completion docs continue to say P1-001 remains open and project `NOT READY`.

## 6. Suggested Verification

Implementation-before baseline is recorded in `docs/CURRENT_STAGE4D_04L_LAYERENGINE_FOUNDATION_BASELINE_EVIDENCE.md`.

Focused LayerEngine baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~PendingTaskQueueDoesNotExposeUndamagedZeroPowerFromPowerModifierAsStateBasedTask|FullyQualifiedName~TurnEndCleanupRestoresNegativeBasePowerAfterPositiveModifierExpires|FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn|FullyQualifiedName~NaturalBattleResponseActivationPowerModifierUsesEffectiveAssignmentDamagePool|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests"
```

Adjacent power / layer / equipment regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~Switcheroo|FullyQualifiedName~Ornn|FullyQualifiedName~TriggerPayment|FullyQualifiedName~BattleDamageAssignmentLifecycleTests"
```

Final hygiene:

```sh
git diff --check
```

## 7. Non-Goals

- Do not replace every power mutation path with a full LayerEngine in one slice.
- Do not claim timestamp / dependency / official layer ordering completion.
- Do not change frontend behavior.
- Do not update card matrix JSON or `fullOfficial`.
- Do not close P1-001, P1-002, frontend final validation or READY.
- Do not call `update_goal complete`.

## 8. Handoff Verdict

4D-04L is ready as a B implementation handoff, but no B worker is dispatched and no write lock is open until A explicitly opens it. Project remains **NOT READY**.
