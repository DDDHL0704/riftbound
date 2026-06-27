# Plan B Spell-Played Trigger Spec Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 OGS Lux / 光辉女郎 high-cost spell 代表触发，以及 Ravenbloom Student / 拉文布鲁姆学生 spell-play power 代表触发，从 `CoreRuleEngine` 的单卡专用 resolver 迁移到 BehaviorSpec 驱动的 spell-play trigger 路径。该切片覆盖单位来源的高费法术本回合战力修正、intro legend 来源的高费法术抽牌，以及单位来源的无阈值 spell-play 本回合战力修正；不改变 OGS Lux trigger queue 兼容 effectKind、Ravenbloom compatibility trigger id、恢复校验 payload 形状、完整 APNAP / `ORDER_TRIGGERS` 或完整 spell-play trigger breadth。

## 1. Scope

Changed:

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.Engine/SpellPlayedTriggerSpecRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/LuxHighCostPaidCostTriggerTests.cs`
- `docs/CURRENT_PLAN_B_SPELL_PLAYED_TRIGGER_SPEC_AUDIT.md`
- `docs/CURRENT_PLAN_B_SPELL_PLAYED_TRIGGER_SPEC_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- `MatchRecovery` OGS Lux trigger queue compatibility validation
- OGS Lux unit trigger queue effectKind `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`
- Ravenbloom Student compatibility trigger id `RAVENBLOOM_STUDENT_SPELL_POWER_PLUS_1`
- spell stack / priority lifecycle
- PaymentEngine paid-cost calculation
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Official OGS Lux unit high-cost spell text parses into BehaviorSpec | `OGS·006/024` parses to `UNIT_HIGH_COST_SPELL_POWER_MODIFIER` with `Timing=BATTLEFIELD_SPELL_PLAYED`, `TargetScope=SOURCE_UNIT`, `MinimumPaidMana=5`, `PowerDelta=3`, and `Duration=UNTIL_END_OF_TURN` | Accepted |
| Official OGS Lux intro legend high-cost spell text parses into BehaviorSpec | `OGS·021/024` parses to `LEGEND_HIGH_COST_SPELL_DRAW_ONE` with `Timing=BATTLEFIELD_SPELL_PLAYED`, `MinimumPaidMana=5`, and `DrawCount=1` | Accepted |
| Core no longer uses a Lux-specific spell-play resolver | `ResolveOgsLuxHighCostSpellPlayedTriggers` is removed; `CoreRuleEngine` calls `ResolveUnitHighCostSpellPowerModifierTriggers` and checks `SpellPlayedTriggerSpecRules.TryGetUnitHighCostSpellPowerModifierTrigger` / `TryGetLegendHighCostSpellDrawTrigger` | Accepted |
| Runtime behavior remains covered | Existing Lux high-cost paid-cost representatives still use server-resolved paid mana: reduction below threshold does not trigger, Spellshield tax up to threshold triggers unit +3 and legend draw 1 | Accepted |
| Official Ravenbloom Student spell-play power text parses into BehaviorSpec | `OGN·103/298` parses to `UNIT_SPELL_PLAYED_POWER_MODIFIER` with `Timing=BATTLEFIELD_SPELL_PLAYED`, `TargetScope=SOURCE_UNIT`, `PowerDelta=1`, and `Duration=UNTIL_END_OF_TURN` | Accepted |
| Core no longer uses a Ravenbloom-specific spell-play resolver | `ResolveRavenbloomStudentSpellPlayedTriggers` is removed; `CoreRuleEngine` calls `ResolveUnitSpellPlayedPowerModifierTriggers` and checks `SpellPlayedTriggerSpecRules.TryGetUnitSpellPlayedPowerModifierTrigger` | Accepted |
| Ravenbloom runtime behavior remains covered | Existing Ravenbloom representatives still trigger when the controller plays a spell, give the source unit +1 until end of turn, and skip standby sources | Accepted |
| Hidden-info / recovery boundary | Opponent snapshots still hide drawn card identity; MatchRecovery adjacent representatives remain green while OGS Lux trigger queue effectKind compatibility is preserved | Accepted |
| Complete spell-play trigger breadth | Jhin high-cost banish, battlefield spell-play triggers, full ordering, and optional choices remain residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitHighCostSpellPowerModifier|FullyQualifiedName~LegendHighCostSpellDraw|FullyQualifiedName~HighCostSpellTriggersDoNotUseLuxSpecificResolver|FullyQualifiedName~LuxHighCostPaidCostTriggerTests" --nologo
```

Result: first failed before implementation on missing `TriggerKinds.UnitHighCostSpellPowerModifier` / `TriggerKinds.LegendHighCostSpellDrawOne`; then 9/9 passed.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitHighCostSpellPowerModifier|FullyQualifiedName~LegendHighCostSpellDraw|FullyQualifiedName~HighCostSpellTriggersDoNotUseLuxSpecificResolver|FullyQualifiedName~LuxHighCostPaidCostTriggerTests|FullyQualifiedName~LuxHighCost|FullyQualifiedName~HighCostSpell|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~LegendActionSourceIdentityGuard|FullyQualifiedName~TriggerSourceIdentityGuard" --nologo
```

Result: 2363/2363 passed.

Ravenbloom Student follow-up focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitSpellPlayedPowerModifier|FullyQualifiedName~SpellPlayedPowerTriggersDoNotUseRavenbloomSpecificResolver|FullyQualifiedName~RavenbloomStudent" --nologo
```

Result: first failed before implementation on missing `TriggerKinds.UnitSpellPlayedPowerModifier`; then 5/5 passed.

Ravenbloom Student follow-up adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitSpellPlayedPowerModifier|FullyQualifiedName~SpellPlayedPowerTriggersDoNotUseRavenbloomSpecificResolver|FullyQualifiedName~RavenbloomStudent|FullyQualifiedName~SpellPlayed|FullyQualifiedName~LuxHighCost|FullyQualifiedName~HighCostSpell|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~TriggerSourceIdentityGuard" --nologo
```

Result: 2361/2361 passed.

Full backend after this spell-play follow-up:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8796/8796 passed.

## 4. Residuals

- `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` remains as compatibility effectKind for current recovery and replay tests.
- `RAVENBLOOM_STUDENT_SPELL_POWER_PLUS_1` remains as compatibility trigger id for current Ravenbloom event/fixture tests.
- Jhin high-cost spell banish remains a separate future TriggerSpec migration candidate.
- Complete simultaneous spell-play trigger ordering and APNAP remain open.
- Project remains NOT READY.
