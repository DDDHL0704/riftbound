# Plan B B1 Static Aura Spec Audit

更新时间：2026-06-26

## Scope

These B1 slices advance Plan B by moving implemented `STATIC_AURA` projection and combat-power recompute surfaces from card-number allow-lists toward `BehaviorSpec` data.

Implemented in this slice:

- `BehaviorSpec.StaticAuras` protocol/catalog surface.
- Parser coverage for the currently implemented static-power representatives:
  - `SFD·085/221` / `SFD·085a/221` 奥恩：每有一件友方装备，自身 `{{S}}+1`。
  - `OGN·294/298` 崔法利兵营：此处所有单位 `{{S}}+1`。
  - `OGS·013/024` 盖伦、`SFD·236/221` / `SFD·236*/221` 德莱厄斯、`OGN·243/298` / `OGN·243a/298` 德莱厄斯：此处其他友方单位 `{{S}}+1`。
  - `UNL-147/219` / `UNL-147a/219` / `UNL-238/219` 纳什男爵：其他友方单位 `{{S}}+2`。
  - `UNL-077/219` 牧魂人：你的指示物单位获得 `{{S}}+1`。
  - `SFD·089/221` / `SFD·089a/221` 兰博：你的“机械”属性单位获得 `{{S}}+1`。
  - `OGN·151/298` / `OGN·151a/298` 李青：我所在战场上其他拥有增益的友方单位获得 `{{S}}+2`。
  - `UNL·T03` 草丛：此处的“鸟类”、“猫科”、“犬形”、“魄罗”属性单位和艾翁单位获得 `{{S}}+1`。
  - `UNL-076/219` 花瓣仙子：我所处的战场你每有一名拥有 `{{瞬息}}` 的单位，我便获得 `{{S}}+1`。
  - `OGN·240/298` / `OGN·240a/298` 瑟提：我所处的战场每有一名拥有增益的友方单位，我便获得 `{{S}}+1`。
  - `SFD·159/221` 可靠攻城犬：如果你在此处有其他单位，则自身获得 `{{S}}+1`。
  - `OGS·019/024` 无极剑圣：如果你只有一名友方单位防守一处战场，则该单位 `{{S}}+2`。
  - `UNL-191/219` / `UNL-231/219` / `UNL-231*/219` 无极宗师：`{{等级6>}}` 你的单位获得 `{{S}}+1`。
  - `UNL-154/219` 猩红飞鸽：如果和另一名单位一起进攻一处战场，则自身 `{{S}}+2`。
  - `OGN·055/298` 驭水者：如果独自进攻或防守一处战场，则自身 `{{S}}+2`。
  - `OGN·131/298` 沙丘亚龙：进攻时如果此处有处于活跃状态的敌方单位，则自身 `{{S}}+2`。
  - `OGN·297/298` 疾风山丘：此处的单位获得 `{{游走}}`。
- Parser false-positive guards:
  - `UNL-043/219` 热情的播报员：其 card text grants `{{增益}}` tokens and must not be treated as a fixed `STATIC_AURA` power modifier.
  - `UNL-195/219` 翠神：parenthetical reminder text describes the Brush battlefield token and must not be treated as a legend-source `STATIC_AURA`.
- `MatchSession` continuous-effect projection now resolves these `STATIC_AURA` kinds via `StaticAuraSpecRules` instead of `ContinuousEffectStaticAuraCards`.
- `CoreRuleEngine.ResolveBattlefieldAllUnitsPowerBonus`, `CoreRuleEngine.ResolveBattlefieldFilteredUnitsPowerBonus`, `CoreRuleEngine.ResolveBattlefieldAllUnitsKeywordBonus`, `CoreRuleEngine.ResolveSameBattlefieldFriendlyFilteredUnitCountToSourcePowerBonus`, `CoreRuleEngine.ResolveSourceSameLocationOtherFriendlyUnitPowerBonus`, `CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyUnitsPowerBonus`, `CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyFilteredUnitsPowerBonus`, `CoreRuleEngine.ResolveOtherFriendlyUnitsPowerBonus`, and `CoreRuleEngine.ResolveFriendlyFilteredUnitsPowerBonus` now apply these static power / keyword auras from `BehaviorSpec.StaticAuras`.
- `StaticAuraSpec.RequiredParticipantCount` models threshold-style source auras where one or more same-location participants enable a fixed power delta rather than multiplying by every participant.
- `StaticAuraSpec.RequiredPlayerExperience` models level/experience-gated static auras without hard-coding the source card in the engine.
- `CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute` and source-unit entry power now resolve Ornn-style friendly-equipment count-to-source static power through `StaticAuraSpecRules.TryGetFriendlyEquipmentPowerAura` and `StaticAuraSpec.PowerDeltaPerParticipant`; the former `CardBehaviorDefinition.AddsFriendlyFieldEquipmentCountToSourceUnitPower` runtime bridge has been deleted.
- `CoreRuleEngine` resolves Master Yi intro single-defender power and Master Yi level friendly-unit power through `StaticAuraSpec.Kind=FRIENDLY_SINGLE_DEFENDING_UNIT_POWER` and `StaticAuraSpec.Kind=FRIENDLY_UNITS_POWER`; the old `HasMasterYiSingleDefenderBonus` / `ResolveMasterYiLevelLegendPowerBonus` combat-power special paths have been deleted.
- `MatchSession` now projects source-object combat static auras for `SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER`, `SOURCE_LONE_BATTLE_POWER`, and `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER` from `BehaviorSpec.StaticAuras` while the current battle state satisfies their parsed participant-count conditions.
- `tests/Riftbound.ConformanceTests/StaticAuraStackingAndModifierTests.cs` now covers additive stacking between a source-combat `STATIC_AURA`, a non-local other-friendly `STATIC_AURA`, and an until-end-of-turn `POWER_MODIFIER` in both continuous-effect projection and real combat damage.
- The old generic `ResolveWaterbenderLoneBattlePowerBonus` method name has been replaced by `ResolveSourceLoneBattlePowerBonus` so the source path no longer names a single representative card.
- `MatchRecovery` now validates source-object combat static-aura `powerDelta` as the fixed BehaviorSpec power delta rather than multiplying by the participant object count.
- `CardEquipmentKeywordRules` marks the Ornn friendly-equipment static-power representative boundary from `BehaviorSpec.StaticAuras` instead of a registry runtime flag.
- `CoreRuleEngine.HasBattlefieldStaticRoamPermission` and `ActionPromptBuilder.HasMoveUnitPromptRoamPermission` now grant `ROAM` from `StaticAuraSpec.Kind=BATTLEFIELD_ALL_UNITS_KEYWORD` + `GrantedKeyword=游走` instead of the old `BattlefieldStaticRoamCardNo` branch.
- Battlefield keyword aura hidden-boundary guard: battlefield keyword sources that are face-down no longer project `BATTLEFIELD_*_UNITS_KEYWORD` RULE_TEXT effects, grant combat keyword bonuses, or provide static Roam prompt / movement permission.
- Battlefield static-power hidden-boundary guard: battlefield power sources that are face-down no longer project `BATTLEFIELD_*_UNITS_POWER` `STATIC_AURA` effects or grant combat static-power bonuses.
- `MatchSession` projects the Master Yi level legend aura as a legend-source `FRIENDLY_UNITS_POWER` continuous effect when the controller satisfies the required experience threshold; static-aura source ordering/dependencies now include public legend-zone sources.
- Recovery static-aura source-card validation now checks the source card's `BehaviorSpec` aura surface for battlefield all-units, battlefield-filtered, same-battlefield friendly-filtered count-to-source, same-battlefield other-friendly, same-battlefield friendly-filtered, non-local other-friendly, friendly-units, and friendly-filtered unit auras instead of a projection allow-list.

## Not Closed

This slice does not claim full B1 completion:

- Battlefield card recognition still has an implemented-battlefield-card registry; this slice only removes card-number gating from the static-power bonus arithmetic.
- Current non-local other-friendly aura coverage is the fixed static-power family only; Nash battlefield-token creation, replacement entry destination, and enemy spell/skill target protection remain open.
- Broader multiple-aura stacking beyond the current source-combat + other-friendly + until-end power representative, full LayerEngine timestamp ordering, additional conditional subscopes beyond the same-location threshold representative, RULE_TEXT keyword grants, and full official static-aura breadth remain open.
- Master Yi `{{等级11>}}` active-entry text remains on the existing unit-entry lifecycle path and is not closed by this B1 combat/static-power slice.
- Current `private|public static bool Is*CardNo(...)` helper count remains 0.
- Project remains NOT READY.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `SFD·085/221` / `SFD·085a/221`: `每有一件友方装备，我便获得{{S}}+1。`
- `OGN·294/298`: `此处的所有单位获得{{S}}+1。（包括进攻方单位。）`
- `OGS·013/024`: `此处的其他友方单位获得{{S}}+1。`
- `SFD·236/221` / `SFD·236*/221` / `OGN·243/298` / `OGN·243a/298`: `此处的其他友方单位获得{{S}}+1。`
- `UNL-147/219` / `UNL-147a/219` / `UNL-238/219`: `其他友方单位获得{{S}}+2。`
- `UNL-077/219`: `你的指示物单位获得{{S}}+1。`
- `SFD·089/221` / `SFD·089a/221`: `你的“机械”属性单位获得{{S}}+1。（包括我。）`
- `OGN·151/298` / `OGN·151a/298`: `我所在战场上其他拥有增益的友方单位获得{{S}}+2。`
- `UNL·T03`: `此处的“鸟类”、“猫科”、“犬形”、“魄罗”属性单位和艾翁单位获得{{S}}+1。`
- `UNL-076/219`: `我所处的战场你每有一名拥有{{瞬息}}的单位，我便获得{{S}}+1。`
- `OGN·240/298` / `OGN·240a/298`: `我所处的战场每有一名拥有增益的友方单位，我便获得{{S}}+1。`
- `SFD·159/221`: `如果你在此处有其他单位，则我获得{{S}}+1。`
- `OGS·019/024`: `如果你只有一名友方单位防守一处战场，则该单位{{S}}+2。`
- `UNL-191/219` / `UNL-231/219` / `UNL-231*/219`: `{{等级6>}} 你的单位获得{{S}}+1。`
- `UNL-154/219`: `如果我和另一名单位一起进攻一处战场，则我获得{{S}}+2。`
- `OGN·055/298`: `如果我独自进攻或防守一处战场，则我获得 {{S}}+2。`
- `OGN·131/298`: `当我进攻时，如果此处有处于活跃状态的敌方单位，则让我{{S}}+2。`
- `OGN·297/298`: `此处的单位获得{{游走}}。（他们可以向其他战场进行移动。）`
- `UNL-043/219`: `给予此处的所有单位{{增益}}。（未拥有增益的单位获得一个{{S}}+1增益。）`
- `UNL-195/219`: `位于草丛的“鸟类”、“猫科”、“犬形”、“魄罗”和“艾翁”属性单位获得{{S}}+1。` appears only as parenthetical token reminder text.
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~P79OtherFriendlyStaticPowerAddsTwoAcrossPublicField"
```

Result: 2/2 passed for the original B1 representative slice.

Latest same-battlefield friendly-filtered focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~SameBattlefieldOtherFriendlyFilteredStaticPower"
```

Result: 2/2 passed.

Latest same-battlefield friendly-filtered count-to-source focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~PetalPixieCountsFriendlyEphemeral|FullyQualifiedName~StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList"
```

Result: 3/3 passed.

Latest Sett same-battlefield boon count-to-source focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79SettCountsFriendlyBoonUnitsAtSameBattlefieldForBattlePower"
```

Result: 79/79 passed.

Latest battlefield-filtered focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~BattlefieldFilteredStaticPower"
```

Result: 2/2 passed.

2026-06-26 battlefield-filtered keyword hidden-boundary focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79BattlefieldFilteredStaticKeywordGrantDoesNotProject" --nologo
```

Result: 2/2 passed.

2026-06-26 battlefield keyword hidden-boundary adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldFiltered|FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticKeyword|FullyQualifiedName~Roam|FullyQualifiedName~Hidden|FullyQualifiedName~FaceDown|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2206/2206 passed.

2026-06-26 backend full after battlefield keyword hidden-boundary fix:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8619/8619 passed.

2026-06-26 battlefield-filtered static-power hidden-boundary focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79BattlefieldFilteredStaticPowerDoesNotProject" --nologo
```

Result: 2/2 passed.

2026-06-26 battlefield static-power hidden-boundary adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldFiltered|FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~Hidden|FullyQualifiedName~FaceDown|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2192/2192 passed.

2026-06-26 backend full after battlefield static-power hidden-boundary fix:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8621/8621 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~OrnnFriendlyEquipmentStaticPower|FullyQualifiedName~LayerEngineBattlefieldStaticAura|FullyQualifiedName~BattlefieldStaticPower|FullyQualifiedName~P79OtherFriendlyStaticPower|FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffect|FullyQualifiedName~FullGameEndToEndTests"
```

Result: 356/356 passed.

Latest adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect"
```

Result: 399/399 passed.

Latest adjacent after battlefield-filtered slice:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect"
```

Result: 400/400 passed.

Latest adjacent after same-battlefield count-to-source slice:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PetalPixie"
```

Result: 401/401 passed.

Latest adjacent after Sett same-battlefield boon count-to-source slice:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PetalPixie|FullyQualifiedName~Sett"
```

Result: 427/427 passed.

Latest battlefield all-units keyword focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList|FullyQualifiedName~P79BattlefieldStaticRoam|FullyQualifiedName~P79BattlefieldStaticRoamSeedAllowsPreciseBattlefieldMove"
```

Result: 6/6 passed.

Latest battlefield all-units keyword adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~BattlefieldStaticRoam|FullyQualifiedName~MoveUnit|FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~BoardTaskQueueFoundationTests|FullyQualifiedName~FullGameEndToEndTests"
```

Result: 711/711 passed.

Latest MatchRecovery hidden-information boundary check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Latest Ornn friendly-equipment runtime bridge removal focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~EquipmentKeyword"
```

Result: 42/42 passed.

Latest same-location source threshold focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ReliableSiegeDogSameLocationStaticPowerTests|FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives"
```

Result: 5/5 passed.

Latest Master Yi legend static-aura spec focused checks:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardCatalogBaselineTests"
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79LegendStaticMasterYi"
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MasterYiLegendStaticAuraSpecTests"
```

Result: 252/252, 3/3, and 2/2 passed.

Latest Master Yi legend static-aura adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~MasterYi|FullyQualifiedName~MatchRecovery"
```

Result: 2033/2033 passed.

Latest source combat static-aura projection and recovery scalar focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SourceCombatStaticAuraProjection"
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SourceCombatStaticAuraProjection|FullyQualifiedName~RecoveryValidatorRejectsSnapshotTimingContinuousEffectStaticAuraPowerDeltaConsistencyDrift"
```

Result: 3/3 and 4/4 passed.

Latest source combat static-aura projection adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SourceCombatStaticAuraProjection|FullyQualifiedName~P79ScarletPigeon|FullyQualifiedName~P79Waterbender|FullyQualifiedName~P79DuneDrake|FullyQualifiedName~StaticAura|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~MatchRecovery"
```

Result: 2041/2041 passed.

Latest static-aura stacking / until-end power modifier focused check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAuraStackingAndModifier" --nologo
```

Result: 1/1 passed.

Latest static-aura stacking / source-combat / recovery adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAuraStackingAndModifier|FullyQualifiedName~SourceCombatStaticAuraProjection|FullyQualifiedName~P79ScarletPigeon|FullyQualifiedName~P79Waterbender|FullyQualifiedName~P79DuneDrake|FullyQualifiedName~P79OtherFriendlyStaticPower|FullyQualifiedName~StaticAura|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2043/2043 passed.

Latest backend full check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8602/8602 passed.

Latest same-location source threshold adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~ReliableSiegeDog"
```

Result: 406/406 passed.

Latest Ornn friendly-equipment runtime bridge removal adjacent check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~OrnnFriendlyEquipmentStaticPower|FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~LayerEngineTimestampDependency|FullyQualifiedName~ContinuousEffect"
```

Result: 417/417 passed.

Latest Ornn friendly-equipment runtime bridge removal MatchRecovery hidden-information boundary check:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Latest result: 8602/8602 passed.
