# 4D-04P LayerEngine Minimum Power Ordering Baseline Evidence

日期：2026-05-16
结论：**BASELINE GREEN / HANDOFF ONLY / PROJECT NOT READY**

本文件记录 4D-04P-B 实现前基线。该批次只建立 handoff，不改 runtime、不改 tests、不派发 worker、不打开写锁。

## 1. Baseline Gap

当前服务端已经分别具备 minimum-power metadata 与 explicit applied-order metadata，但还没有一个同目标 sequence representative 证明二者组合后的审计语义：

- `src/Riftbound.Engine/CoreRuleEngine.cs` 的 `ApplyPowerModifier` 会先按当前 `Power` 计算 requested delta，再通过 `MinimumPowerAfterModifier` floor 得到 applied delta 和 `resultingPower`。
- 非零 applied delta 会追加 `PowerModifierLedgerEntry`，并保留 requested / applied / minimum / resulting / `AppliedOrder` metadata；applied delta 为 0 时不追加 ledger。
- Blastcone Sprout 证明单个 minimum floor metadata 正确，Power Bind Echo 证明同目标双 modifier order 正确，但现有测试没有把 minimum floor 与同目标多 modifier ordering 放在一个 sequence 中验证。
- Extortion 证明 floor 到 zero-applied 不会产生 misleading ledger；后续切片仍需要确保 zero-applied 不消耗或伪造 visible order。
- `ContinuousEffectPowerModifierAppliedOrderSurvivesEffectIdNormalization` 证明 ordered ledger 不被 `EffectId` 字典序改写，但该 shape test 不覆盖 minimum floor metadata。

因此 4D-04P 的下一步不是 full LayerEngine，而是补一条最小战力与 order metadata 的组合 representative，作为后续 timestamp / dependency / source-ordering 模型前的窄护栏。

## 2. Commands

Focused minimum-power ordering baseline:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~PowerModifierAppliedOrderFollowsPowerBindEchoAppendSequence|FullyQualifiedName~CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack|FullyQualifiedName~CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack|FullyQualifiedName~CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~ContinuousEffectPowerModifierAppliedOrderSurvivesEffectIdNormalization"
```

Result: **passed 7/7**.

Adjacent minimum / ordering / continuous-effect regression:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~Extortion|FullyQualifiedName~SmokeBomb|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

Result: **passed 15/15**.

Backend full:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **passed 4449/4449**.

## 3. Boundary

This is A-side baseline evidence only. It does not close:

- P1-001 full continuous effect LayerEngine
- timestamp / dependency graph
- source ordering for all static / replacement / direct breadth
- keyword gain/loss layer ordering
- multiple equipment / static aura interactions
- complete minimum-power ordering
- P1-002 keyword execution breadth
- full-card matrix official coverage
- frontend final validation
- READY
