# Stage 4C-80 Bullet Time Power Damage Evidence

审计日期：2026-05-13
结论：**代表性证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
  - `OGN·268/298` registered as direct card behavior
  - cost `1`
  - `EffectKind: BULLET_TIME_DAMAGE_ENEMY_BATTLEFIELD_UNITS_BY_POWER_SPENT`
  - `TargetCount: 0`
  - `DamagesAllEnemyBattlefieldUnits: true`
  - `DamageAmountFromOptionalPowerCost: true`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-bullet-time-power-damage-enemy-battlefield.fixture.json`
  - records ordinary hand play / pay 1 mana / `SPEND_POWER:3` / zero target stack route
  - locks stack `damageAmount` from paid power
  - locks pass-pass damage to enemy battlefield units
  - locks enemy base and friendly battlefield unaffected
  - locks spell to graveyard and spent rune pool emptied
  - records catalog and core-rule refs while leaving full FAQ / battle lifecycle coverage deferred

## 测试证据

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `CoreRuleEnginePlaysBulletTimePowerDamageEnemyBattlefield`
  - `CoreRuleEngineRejectsBulletTimeWhenPowerCostIsInsufficient`
  - `P7TypedPowerPaymentAcceptsMatchingTraitAndDebitsOnlyThatTrait`
  - `P7TypedPowerPaymentRejectsWhenRequiredTraitIsMissing`
  - `P7PlayCardRecyclesRuneAsPaymentResourceAction`
  - `P7PlayCardRecyclesLegacyOwnedRuneAsPaymentResourceAction`
  - `P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount`
  - `P7PlayCardPaymentResourceContributionMetadataSeparatesTraits`
  - `P7PlayCardGenericPaymentResourceCanUseMixedTraitContribution`
  - `P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions`
  - `P7PlayCardRejectsOverRecycledPaymentResourceActions`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BulletTime|FullyQualifiedName~TypedPowerPayment|FullyQualifiedName~PaymentResource|FullyQualifiedName~RecycleRune"
```

结果：24/24 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Payment|FullyQualifiedName~PayCost|FullyQualifiedName~PowerByTrait|FullyQualifiedName~SpendPower|FullyQualifiedName~RecycleRune|FullyQualifiedName~DamageAllEnemyBattlefield|FullyQualifiedName~EnemyBattlefield|FullyQualifiedName~Firestorm|FullyQualifiedName~CrescentStrike|FullyQualifiedName~Stack|FullyQualifiedName~Priority"
```

结果：250/250 passed。

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
  - `stage4CBatch80BulletTimePowerDamageEvidence`
  - `functionalUnits[].stage4C80`
  - `snapshotEntries[].stage4C80`

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 Bullet Time representative pay-power enemy battlefield damage route、insufficient power rejection、typed power and recycle payment resource guards，不清零 complete `JFAQ-251023 p6`、battle / spell-duel lifecycle、PaymentEngine、FEPR、hidden-info 或 full-official 缺口。
