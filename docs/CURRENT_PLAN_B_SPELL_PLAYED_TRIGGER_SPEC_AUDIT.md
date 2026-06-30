# Plan B Spell-Played Trigger Spec Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 OGS Lux / 光辉女郎 high-cost spell 代表触发、Ravenbloom Student / 拉文布鲁姆学生 spell-play power 代表触发、Diana / 黛安娜主卡与 alt A 的 spell-play power 代表触发，以及 Jhin / 戏命师 high-cost spell banish completion 代表触发，迁移或接入 BehaviorSpec 驱动的 spell-play trigger 路径。该切片覆盖单位来源的高费法术本回合战力修正、intro legend 来源的高费法术抽牌、单位来源的无阈值 spell-play 本回合战力修正，以及 legend 来源的高费法术放逐四张后召符文/抽牌代表路径；不改变 OGS Lux trigger queue 兼容 effectKind、恢复校验 payload 形状、Diana 伏击 / 反应打出流程、Jhin optional yes/no prompt、完整 APNAP / `ORDER_TRIGGERS` 或完整 spell-play trigger breadth。

## 2026-06-30 Follow-up: OGS Lux EffectKind Lives On TriggerSpec

`CoreRuleEngine.ResolveUnitHighCostSpellPowerModifierTriggers(...)` no longer owns the OGS Lux emitted effect id. `TriggerSpec` now carries optional `EffectKind`, `CardBehaviorRegistry` records `UnitHighCostSpellPowerModifierEffectKind=OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` on `OGS·006/024`, and `BehaviorSpecCatalogBuilder` projects that value onto the parsed `UNIT_HIGH_COST_SPELL_POWER_MODIFIER` trigger. Public trigger queue / recovery compatibility effectKind remains unchanged. See `docs/CURRENT_PLAN_B_SPELL_PLAYED_TRIGGER_EFFECT_KIND_SPEC_AUDIT.md`.

## 1. Scope

Changed:

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.Engine/SpellPlayedTriggerSpecRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.DevUi/src/types/catalog.ts`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/LuxHighCostPaidCostTriggerTests.cs`
- `docs/CURRENT_PLAN_B_SPELL_PLAYED_TRIGGER_SPEC_AUDIT.md`
- `docs/CURRENT_PLAN_B_SPELL_PLAYED_TRIGGER_SPEC_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- `MatchRecovery` OGS Lux trigger queue compatibility validation
- OGS Lux unit trigger queue effectKind `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`
- Diana ambush / reaction hand-to-battlefield timing
- Jhin optional yes/no trigger prompt and APNAP ordering; representative auto-resolution is retained
- spell stack / priority lifecycle
- PaymentEngine paid-cost calculation
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Official OGS Lux unit high-cost spell text parses into BehaviorSpec | `OGS·006/024` parses to `UNIT_HIGH_COST_SPELL_POWER_MODIFIER` with `Timing=BATTLEFIELD_SPELL_PLAYED`, `TargetScope=SOURCE_UNIT`, `MinimumPaidMana=5`, `PowerDelta=3`, and `Duration=UNTIL_END_OF_TURN` | Accepted |
| Official OGS Lux intro legend high-cost spell text parses into BehaviorSpec | `OGS·021/024` parses to `LEGEND_HIGH_COST_SPELL_DRAW_ONE` with `Timing=BATTLEFIELD_SPELL_PLAYED`, `MinimumPaidMana=5`, and `DrawCount=1` | Accepted |
| Official Jhin legend high-cost spell banish text parses into BehaviorSpec | `UNL-181/219`, `UNL-226/219`, and `UNL-226*/219` parse to `LEGEND_HIGH_COST_SPELL_BANISH_COMPLETION` with `Timing=BATTLEFIELD_SPELL_PLAYED`, `TargetScope=SOURCE_LEGEND`, `MinimumPaidMana=4`, `BanishCount=4`, `RuneCallCount=4`, `DrawCount=1`, and `Optional=true` | Accepted |
| Core no longer uses a Lux-specific spell-play resolver | `ResolveOgsLuxHighCostSpellPlayedTriggers` is removed; `CoreRuleEngine` calls `ResolveUnitHighCostSpellPowerModifierTriggers` and checks `SpellPlayedTriggerSpecRules.TryGetUnitHighCostSpellPowerModifierTrigger` / `TryGetLegendHighCostSpellDrawTrigger` | Accepted |
| Core no longer uses a Jhin-specific high-cost spell resolver | `ResolveJhinHighCostSpellTrigger`, `JhinHighCostSpellManaThreshold`, `JhinCompletionSpellCount`, and `JhinBanishedHighCostSpellMarker` are removed; `CoreRuleEngine` scans the controller's legend zone and checks `SpellPlayedTriggerSpecRules.TryGetLegendHighCostSpellBanishCompletionTrigger` | Accepted |
| Runtime behavior remains covered | Existing Lux high-cost paid-cost representatives still use server-resolved paid mana: reduction below threshold does not trigger, Spellshield tax up to threshold triggers unit +3 and legend draw 1 | Accepted |
| Jhin runtime behavior remains covered | `P79LegendTriggerJhinCompletesFourthBanishedHighCostSpell` now covers `UNL-181/219`, `UNL-226/219`, and `UNL-226*/219`; each source reads the parsed threshold/counts, marks high-cost spells with the generic `LEGEND_HIGH_COST_SPELL_BANISHED` tag, moves four tracked spells to graveyard, calls four runes, and draws one card | Accepted |
| Official Ravenbloom Student spell-play power text parses into BehaviorSpec | `OGN·103/298` parses to `UNIT_SPELL_PLAYED_POWER_MODIFIER` with `Timing=BATTLEFIELD_SPELL_PLAYED`, `TargetScope=SOURCE_UNIT`, `PowerDelta=1`, and `Duration=UNTIL_END_OF_TURN` | Accepted |
| Official Diana spell-play power text parses into BehaviorSpec | `UNL-149/219` and `UNL-149a/219` parse to `UNIT_SPELL_PLAYED_POWER_MODIFIER` with `Timing=BATTLEFIELD_SPELL_PLAYED`, `TargetScope=SOURCE_UNIT`, `PowerDelta=2`, and `Duration=UNTIL_END_OF_TURN` | Accepted |
| Core no longer uses a Ravenbloom-specific spell-play resolver | `ResolveRavenbloomStudentSpellPlayedTriggers` is removed; `CoreRuleEngine` calls `ResolveUnitSpellPlayedPowerModifierTriggers` and checks `SpellPlayedTriggerSpecRules.TryGetUnitSpellPlayedPowerModifierTrigger` | Accepted |
| Core no longer emits the Ravenbloom-specific compatibility trigger id | `ResolveUnitSpellPlayedPowerModifierTriggers` uses `triggerSpec.Kind` as the trigger / effect kind, so Ravenbloom and Diana both emit `UNIT_SPELL_PLAYED_POWER_MODIFIER` while `PowerDelta` still comes from BehaviorSpec | Accepted |
| Ravenbloom runtime behavior remains covered | Existing Ravenbloom representatives still trigger when the controller plays a spell, give the source unit +1 until end of turn, and skip standby sources | Accepted |
| Diana runtime behavior is covered through the same resolver | `CoreRuleEngineTriggersDianaUnitSpellPlayedPowerModifierWhenSpellPlayed` proves both `UNL-149/219` and `UNL-149a/219` trigger from an already-controlled field unit when the controller plays a spell, read `PowerDelta=2` from BehaviorSpec, and apply +2 until end of turn | Accepted |
| Hidden-info / recovery boundary | Opponent snapshots still hide drawn card identity; MatchRecovery adjacent representatives remain green while OGS Lux trigger queue effectKind compatibility is preserved | Accepted |
| Complete spell-play trigger breadth | Battlefield spell-play triggers, full ordering, optional choices, and complete paid-cost override breadth remain residual | Residual, no full-official claim |

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

Result: first failed before implementation on missing `TriggerKinds.UnitSpellPlayedPowerModifier`; later generic-trigger-id follow-up first failed on the old `RAVENBLOOM_STUDENT_SPELL_POWER_PLUS_1` runtime id; current focused 7/7 passed.

Ravenbloom Student follow-up adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitSpellPlayedPowerModifier|FullyQualifiedName~SpellPlayedPowerTriggersDoNotUseRavenbloomSpecificResolver|FullyQualifiedName~RavenbloomStudent|FullyQualifiedName~SpellPlayed|FullyQualifiedName~LuxHighCost|FullyQualifiedName~HighCostSpell|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~TriggerSourceIdentityGuard" --nologo
```

Result: 2361/2361 passed.

Diana follow-up focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitSpellPlayedPowerModifier|FullyQualifiedName~DianaUnitSpellPlayedPowerModifier|FullyQualifiedName~RavenbloomStudent" --nologo
```

Result: first failed before implementation because Diana parsed as generic `on-play` and runtime remained at 3 power; then 6/6 passed.

Diana follow-up adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitSpellPlayedPowerModifier|FullyQualifiedName~DianaUnitSpellPlayedPowerModifier|FullyQualifiedName~RavenbloomStudent|FullyQualifiedName~SpellPlayed|FullyQualifiedName~Diana|FullyQualifiedName~LuxHighCost|FullyQualifiedName~HighCostSpell|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~TriggerSourceIdentityGuard" --nologo
```

Result: 2373/2373 passed.

Jhin high-cost spell banish completion focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendHighCostSpellBanishCompletion|FullyQualifiedName~HighCostSpellTriggersDoNotUseJhinSpecificResolver|FullyQualifiedName~P79LegendTriggerJhin" --nologo
```

Result: first failed before implementation on missing `TriggerKinds.LegendHighCostSpellBanishCompletion`; then failed on split-text parsing until the full-text parser was added; then 6/6 passed.

Jhin high-cost spell banish completion adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendHighCostSpellBanishCompletion|FullyQualifiedName~HighCostSpellTriggersDoNotUseJhinSpecificResolver|FullyQualifiedName~P79LegendTriggerJhin|FullyQualifiedName~Jhin|FullyQualifiedName~SpellPlayed|FullyQualifiedName~HighCostSpell|FullyQualifiedName~LuxHighCost|FullyQualifiedName~Diana|FullyQualifiedName~RavenbloomStudent|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~FullGameEndToEnd" --nologo
```

Result: 2385/2385 passed.

Generic no-threshold unit spell-play trigger id adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitSpellPlayedPowerModifier|FullyQualifiedName~DianaUnitSpellPlayedPowerModifier|FullyQualifiedName~RavenbloomStudent|FullyQualifiedName~SpellPlayed|FullyQualifiedName~Diana|FullyQualifiedName~LuxHighCost|FullyQualifiedName~HighCostSpell|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~TriggerSourceIdentityGuard|FullyQualifiedName~LegendHighCostSpellBanishCompletion|FullyQualifiedName~HighCostSpellTriggersDoNotUseJhinSpecificResolver|FullyQualifiedName~P79LegendTriggerJhin|FullyQualifiedName~Jhin|FullyQualifiedName~FullGameEndToEnd" --nologo
```

Result: 2463/2463 passed.

Full backend after this spell-play follow-up:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8802/8802 passed.

Frontend type/build gate after adding `TriggerSpec.BanishCount`:

```sh
PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build
```

Result: passed; existing Rollup chunk-size / SignalR pure-annotation warnings only.

## 4. Residuals

- `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` remains as compatibility effectKind for current recovery and replay tests.
- Jhin high-cost spell banish completion now reads threshold/counts from BehaviorSpec, but optional yes/no prompt, full APNAP ordering, and complete paid-cost override breadth remain open.
- Diana ambush / reaction hand-to-battlefield timing remains outside this slice.
- Complete simultaneous spell-play trigger ordering and APNAP remain open.
- Project remains NOT READY.
