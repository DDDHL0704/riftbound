# Stage 4C-70 Skullcrack Battlefield Stun Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `OGN·220/298` / `SKULLCRACK_STUN_FRIENDLY_AND_ENEMY_BATTLEFIELD_UNITS`
  - `Cost: 2`
  - `TargetCount: 2`
  - `StatusEffectId: STUNNED`
  - `TargetScope: FriendlyBattlefieldThenEnemyBattlefieldUnits`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - 目标校验要求 target slot 0 是友方公开战场单位，target slot 1 是敌方公开战场单位。
  - stack pass-pass resolution 对两个目标施加 `STUNNED`，并把源法术移入控制者废牌堆。
  - 错误目标顺序、基地目标或同阵营第二目标在支付、入栈和状态 mutation 前被拒绝。

## 测试证据

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-skullcrack-stun-friendly-and-enemy-battlefield-units.fixture.json`
  - official card snapshot evidence
  - ordinary hand play / pay 2 / two-target stack / pass-pass resolution
  - target 0 friendly battlefield unit, target 1 enemy battlefield unit
  - two `STATUS_EFFECT_APPLIED` events with `effectId: STUNNED`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysSkullcrackStunsFriendlyAndEnemyBattlefieldUnits`
  - `CoreRuleEngineRejectsSkullcrackAgainstWrongOrderOrBaseUnits`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysSkullcrackStunsFriendlyAndEnemyBattlefieldUnits|FullyQualifiedName~CoreRuleEngineRejectsSkullcrackAgainstWrongOrderOrBaseUnits"
```

结果：2/2 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Skullcrack|FullyQualifiedName~RunePrison|FullyQualifiedName~Kerplunk|FullyQualifiedName~HeroicCharge|FullyQualifiedName~ZenithBlade|FullyQualifiedName~Stun|FullyQualifiedName~Stunned|FullyQualifiedName~SolariLeader|FullyQualifiedName~Stunning"
```

结果：64/64 passed。

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
  - `stage4CBatch70SkullcrackBattlefieldStunEvidence`
  - `functionalUnits[].stage4C70`
  - `snapshotEntries[].stage4C70`

4B freeze status / status flags 保持不变，`fullOfficial=false`。
