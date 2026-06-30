# Plan B Unit Last-Breath Source-Battlefield AoE Damage TriggerSpec Audit

日期：2026-06-27；更新：2026-06-30
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `OGN·190/298` 克格莫 `{{绝念}}-对我所处战场上的所有单位各造成4点伤害` 的既有代表性结算，从 `CoreRuleEngine` 本地 Kogmaw 专用常量迁移到官方文本解析出的 `BehaviorSpec.Triggers`。该切片只收窄 source / target-scope / damage amount 来源；不关闭完整 AoE damage matrix、完整绝念家族、完整 `ORDER_TRIGGERS` / APNAP、B0 整局善终测试、P0 full objective 或 READY。

## 1. Scope

Changed:

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.Engine/UnitDestroyedTriggerSpecRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- `docs/CURRENT_PLAN_B_UNIT_LAST_BREATH_SOURCE_BATTLEFIELD_AOE_DAMAGE_TRIGGER_SPEC_AUDIT.md`
- `docs/CURRENT_PLAN_B_UNIT_LAST_BREATH_SOURCE_BATTLEFIELD_AOE_DAMAGE_TRIGGER_SPEC_EVIDENCE.md`
- `docs/CURRENT_PLAN_B_LAST_BREATH_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_LAST_BREATH_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- trigger queue / `ORDER_TRIGGERS` protocol shape
- public effect string `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT`
- recovery trigger/effect wire compatibility strings
- frontend runtime
- card matrix full-official status

## 2. Official Inputs

- `data/official/card-catalog.zh-CN.json`: `OGN·190/298` contains `{{绝念}}-对我所处战场上的所有单位各造成4点伤害。（当我被摧毁后，发动此效果。）`
- Existing evidence for `p2-preflight-play-ogn-kogmaw-last-breath-static` records the current representative path: destroyed source's pre-removal battlefield units each take 4 damage, then cleanup stabilizes.

## 3. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Kogmaw last-breath AoE damage text is represented as data | `RuleTextParser` emits `TriggerKinds.UnitLastBreathDamageSourceBattlefieldUnits`, `Timing=UNIT_DESTROYED`, `TargetScope=SOURCE_BATTLEFIELD_UNITS`, `DamageAmount=4` | Accepted |
| Runtime source selection reads TriggerSpec | `ResolveUnitLastBreathSourceBattlefieldAoeDamagePlayerId(...)` now uses `UnitDestroyedTriggerSpecRules.TryGetLastBreathSourceBattlefieldAoeDamageTrigger(...)` plus source unit / face-up / non-standby checks | Accepted |
| Runtime damage amount reads TriggerSpec | stack resolution derives damage amount from `TriggerSpec.DamageAmount` with compatibility fallback | Accepted |
| Recovery source-card validation reads TriggerSpec | 2026-06-30 follow-up removes `KogmawCardNoForRecovery`; recovered snapshot, authoritative-state and spectator replay source objects now pass only when `UnitDestroyedTriggerSpecRules.TryGetLastBreathSourceBattlefieldAoeDamageTrigger(sourceCardNo, out _)` accepts the source card | Accepted |
| Public wire compatibility is preserved | `TriggerKinds.UnitLastBreathDamageSourceBattlefieldUnits` keeps the existing effect string `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT`; trigger id battlefield marker remains `::BATTLEFIELD::` | Accepted |
| Core no longer owns Kogmaw local constants | `KogmawCardNo`, `KogmawLastBreathAoeEffectKind`, `KogmawLastBreathDamage`, and Kogmaw-named Core helpers are absent from `CoreRuleEngine`; the guard test blocks reintroduction | Accepted |
| Full official breadth | complete AoE target matrix, simultaneous trigger ordering, APNAP, full B0 game flow and full last-breath breadth remain residual | Residual, no full-official claim |

## 4. Verification

Initial focused TDD guard failed before implementation because `TriggerKinds.UnitLastBreathDamageSourceBattlefieldUnits` and `TriggerTargetScopes.SourceBattlefieldUnits` did not exist. After implementation:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesUnitLastBreathSourceBattlefieldAoeDamageTrigger|FullyQualifiedName~UnitLastBreathSourceBattlefieldAoeDamageTriggerDoesNotUseCoreCardNumberBehavior" --nologo
```

Result: 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Kogmaw|FullyQualifiedName~LastBreath|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~TriggerSourceIdentityGuard|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~ConformanceFixtureRunner" --nologo
```

Result: 5477/5477 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8823/8823 passed.

2026-06-30 recovery follow-up:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~CardCatalogBaselineTests.UnitLastBreathSourceBattlefieldAoeDamageTriggerDoesNotUseCoreCardNumberBehavior"
```

Result: red/green guard, final 1/1 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~KogmawLastBreathSourceCardContextDrift"
```

Result: 4/4 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~Kogmaw|FullyQualifiedName~LastBreath|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~TriggerSourceIdentityGuard|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~ConformanceFixtureRunner"
```

Result: 5509/5509 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 9049/9049 passed.

DevUi build was not rerun because this slice did not change DevUi source or shared TypeScript catalog payload shape.

## 5. Residual Risks

- This preserves the current trigger id battlefield context marker and public effect string; a future generic trigger context payload can reduce id encoding, but wire compatibility is intentionally unchanged in this slice.
- This does not implement complete AoE damage / replacement / prevention / target matrix breadth.
- This does not implement complete simultaneous last-breath ordering or optional-trigger policy.
- This does not add the B0 full-game end-to-end good-terminal-state test.
- This does not close card matrix full-official, frontend final validation, formal E2E or READY.
