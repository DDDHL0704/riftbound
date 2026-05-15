# Stage 4D-04N LayerEngine Direct Power Ledger Handoff

日期：2026-05-16
结论：**HANDOFF READY / PAUSED / PROJECT NOT READY**

本文是 A 主控在 4D-04M-B 后建立的下一枚 P1-001 handoff。它只做 direct until-end power mutation ledger inventory、边界约束和实现前验收口径，不实现 runtime，不修改测试，不派发 B，不升级 fullOfficial / READY。

## 1. 背景

4D-04L-B 已让 `ApplyPowerModifier` 代表路径追加 source-aware / effect-aware `PowerModifierLedgerEntry`，4D-04M-B 已为 `MinimumPowerAfterModifier > 0` 的代表路径补上 requested/applied/minimum/resulting metadata。当前仍有若干 direct power mutation path 直接修改 `Power` 与 `UntilEndOfTurnPowerModifier`，没有 ledger-backed source / effect metadata；snapshot 只能显示 legacy untracked remainder，或无法描述来源与效果语义。

4D-04N 的目标不是完整 LayerEngine，而是把下一批 B 实现收窄到 direct until-end power mutation representatives 的 ledger metadata foundation：保持现有 arithmetic 与清理行为，补足 source/effect/direct-path metadata，避免 frontend、日志和后续 audit 只能看到无来源的战力剩余量。

## 2. Direct Mutation Inventory

当前建议 4D-04N-B 优先检查以下 direct paths：

- `src/Riftbound.Engine/CoreRuleEngine.cs:1774` `ResolveIcevaleArcherAttackTriggerPayment`：直接设置 `Power = resultingPower`，并累加 `UntilEndOfTurnPowerModifier += appliedPowerDelta`。
- `src/Riftbound.Engine/CoreRuleEngine.cs:13048` `ResolveEmberMonkStandbyHiddenPowerTrigger`：friendly standby hidden trigger 直接让来源 +2。
- `src/Riftbound.Engine/CoreRuleEngine.cs:20249` `UNIT_CONQUEST_FRIENDLY_PLUS_8_THIS_TURN`：conquest trigger 直接让目标 +8。
- `src/Riftbound.Engine/CoreRuleEngine.cs:25274` `ResolveRengarUnitPlayedPowerTrigger`：Rengar unit-play trigger 直接让目标 +1。
- `src/Riftbound.Engine/CoreRuleEngine.cs:26381` `ApplyBattlefieldMovedUnitPowerPlusOne`：battlefield-moved unit 直接 +1。
- `src/Riftbound.Engine/CoreRuleEngine.cs:29442` `TryResolveSourceUnitOptionalReadyPower`：optional ready branch 直接给来源单位 until-end power。
- `src/Riftbound.Engine/CoreRuleEngine.cs:33985` `ResolveViDoublePowerAbilityStackItem`：Vi activated ability 直接把来源战力翻倍。

以上 inventory 是实现前路由；B 应在开始修改前以当前 checkout 重新核对 line number 与方法名。

## 3. Suggested B Write Scope

允许范围：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- focused regression tests under `tests/Riftbound.ConformanceTests/`
- 可选：最小 helper / model，用于复用 direct power ledger append / projection，前提是不扩大 LayerEngine 语义。

禁止范围：

- frontend runtime / browser UI
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad PaymentEngine rewrite
- battle lifecycle / task queue semantics
- equipment runtime breadth
- fullOfficial / READY status
- `riftbound-dotnet.sln`

## 4. Required Behavior

4D-04N-B 应保持以下约束：

1. 现有 `Power`、`UntilEndOfTurnPowerModifier`、`basePower`、`effectivePower`、turn-end cleanup 与 snapshot compatibility 不改变。
2. direct until-end power mutation 在 applied delta 非零时追加或暴露 ledger metadata，包括 `sourceObjectId`、`sourceCardNo`、`effectKind`、`sourcePath`、requested / applied / minimum / resulting 信息中该 path 能准确得出的部分。
3. applied delta 为 0 的 floor / stale / no-op path 不应创建 misleading zero-delta ledger entry。
4. 回合结束、离场、zone cleanup 时，direct ledger metadata 应与 aggregate until-end modifier 一起清理，不能残留到后续 authoritative snapshot。
5. 至少选择一个 trigger/payment representative 断言 state 与 snapshot metadata；建议覆盖 Icevale Archer 或 Vi，再加 Rengar / Ember Monk / Thousand-Tailed Watcher 等一个或多个相邻路径。
6. 4D-04L / 4D-04M 的 `ApplyPowerModifier` source/effect metadata 与 minimum-power metadata 必须保持绿色。

## 5. Deferred Residuals

即使 4D-04N-B 通过，也不得关闭以下事项：

- full LayerEngine
- timestamp / dependency / source ordering
- keyword gain/loss layering
- multiple equipment / static aura interactions
- complete minimum-power ordering
- all direct / static / replacement effects outside selected representatives
- P1-002 keyword full execution
- full official card matrix
- frontend final validation
- READY

## 6. Acceptance Commands

实现前 A baseline 已记录在 `docs/CURRENT_STAGE4D_04N_LAYERENGINE_DIRECT_POWER_LEDGER_BASELINE_EVIDENCE.md`。B 完成后，A 应至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LuxHighCostSpellQueuesResolvesAndGainsPowerUntilEndOfTurn|FullyQualifiedName~IcevaleArcherAttackPaymentAcceptedAppliesTemporaryPowerMinusOne|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~P79LegendTriggerRengarGivesUnitPlusOneAfterUnitPlayed|FullyQualifiedName~P4ActivateAbilityCommandResolvesViDoublePowerSkillOnStack|FullyQualifiedName~P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~TriggerPayment|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~Rengar|FullyQualifiedName~ViDoublePower|FullyQualifiedName~EmberMonk|FullyQualifiedName~HasteOptional"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

## 7. Pause Point

当前批次停在 A-side handoff / baseline：4D-04N-B 尚未派发，runtime / test / frontend / matrix 写锁均未打开。项目仍 **NOT READY**，active goal 不得标记 complete。
