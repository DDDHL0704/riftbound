# Stage 4C-72 Hextech Ray Damage Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `OGN·009/298` / `HEXTECH_RAY_DAMAGE_3`
  - `Cost: 1`
  - `TargetCount: 1`
  - `DamageAmount: 3`
  - `CanPlayDuringSpellDuel: true`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - 普通打出路径支付费用、创建 stack item、双方让过后结算。
  - 目标限制为战场单位；基地单位目标在支付、入栈和伤害变更前被拒绝。
  - Swift 法术对决焦点窗口路径允许打出并在结算后把 focus 交给对手。

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-hextech-ray-damage-stack.fixture.json`
  - ordinary hand play / pay 1 / battlefield-unit target / stack pass-pass / 3 damage / spell to graveyard
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-hextech-ray-damage-clears-end-turn.fixture.json`
  - damage applied by Hextech Ray is removed during end-turn special cleanup and turn advances
- `tests/Riftbound.ConformanceTests/Fixtures/p6-play-swift-hextech-ray-in-spell-duel-focus.fixture.json`
  - Swift spell-duel focus play route, damage resolution, focus handoff
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysHextechRayThroughStack`
  - `CoreRuleEngineClearsHextechRayDamageAtEndTurn`
  - `P6SwiftKeywordAllowsHextechRayInSpellDuelFocusWindow`
  - `CoreRuleEngineRejectsBattlefieldOnlySpellWhenTargetIsBaseUnit`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysHextechRayThroughStack|FullyQualifiedName~CoreRuleEngineClearsHextechRayDamageAtEndTurn|FullyQualifiedName~P6SwiftKeywordAllowsHextechRayInSpellDuelFocusWindow|FullyQualifiedName~CoreRuleEngineRejectsBattlefieldOnlySpellWhenTargetIsBaseUnit"
```

结果：4/4 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HextechRay|FullyQualifiedName~Swift|FullyQualifiedName~Damage|FullyQualifiedName~Cleanup|FullyQualifiedName~EndTurn|FullyQualifiedName~BattlefieldOnlySpell"
```

结果：202/202 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3754/3754 passed。

```sh
cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build
cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api
```

结果：frontend build passed；Chrome smoke passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch72HextechRayDamageEvidence`
  - `functionalUnits[].stage4C72`
  - `snapshotEntries[].stage4C72`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账代表性 damage / cleanup / Swift spell-duel focus route，不清零 FAQ-wide 或 full-official 缺口。
