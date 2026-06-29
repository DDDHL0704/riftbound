# Plan B Active-Entry Static Ability Spec Audit

更新时间：2026-06-29

## Scope

This audit tracks Plan B active-entry static ability slices that move card text from deferred card rows into shared BehaviorSpec-driven entry lifecycles.

Implemented for `OGN·011/298` 熔浆巨龙:

- `StaticAbilityKinds.OtherFriendlyUnitsEnterReady = OTHER_FRIENDLY_UNITS_ENTER_READY`.
- `RuleTextParsers.StaticAbilityParser` parses `当我在场上时，其他友方单位以活跃状态进场。` into `BehaviorSpec.StaticAbilities`.
- `CardStaticAbilitySpecRules.TryGetOtherFriendlyUnitsEnterReadyAbility` exposes the parsed spec to engine runtime.
- `CoreRuleEngine` unit-entry resolution checks public, face-up, non-standby friendly field-unit sources with that static ability before deciding whether a played unit enters exhausted.
- The source object is excluded from its own static ability, and face-down / standby sources do not grant active entry.
- `UNIT_PLAYED_TO_BASE` / `UNIT_PLAYED_TO_BATTLEFIELD` payloads include `entryStaticAbilityKind`, `entryStaticAbilitySourceObjectId`, and source card metadata when this static ability controls the entry state.

Implemented for `SFD·171/221` / `SFD·171a/221` 烈娜塔·戈拉斯克:

- `StaticAbilityKinds.FriendlyFilteredUnitsEnterReady = FRIENDLY_FILTERED_UNITS_ENTER_READY`.
- `StaticAbilitySpec.TargetFilter` can now carry entry filters; Renata parses `你的指示物以活跃状态进场。` with `TargetFilter=TOKEN`.
- `P6TokenFactoryCatalog.IsTokenFactory` backs the generic token filter rather than a local card-number helper.
- `CoreRuleEngine` uses the same public, face-up, non-standby friendly source scan for filtered token active-entry specs.
- Unit token factory entry paths retain `ApplyUnitTokenEntryStaticAbility`, which now delegates to token-aware entry resolution.
- Equipment token creation paths now bind P6 token factory identity before entry resolution, so Gold equipment tokens can carry official `tokenCardNo`, owner/controller, tags, and shared `entryStaticAbilityKind`, `entryStaticAbilitySourceObjectId`, and source card metadata when Renata controls entry.
- `OTHER_FRIENDLY_UNITS_ENTER_READY` remains unit-only; equipment tokens only receive active entry from filtered token specs such as Renata's `TargetFilter=TOKEN`.
- Battlefield token entry payload coverage remains unchanged until its token metadata is upgraded.

Implemented for `UNL-191/219` / `UNL-231/219` / `UNL-231*/219` 无极宗师:

- `StaticAbilityKinds.FriendlyUnitsEnterReady = FRIENDLY_UNITS_ENTER_READY`.
- `StaticAbilitySpec.RequiredPlayerExperience` can now carry level-gated active-entry requirements; Master Yi parses `{{等级11>}} 你的单位以活跃状态进场。` with `RequiredPlayerExperience=11`.
- `CardStaticAbilitySpecRules.TryGetFriendlyUnitsEnterReadyAbility` exposes the parsed spec to engine runtime.
- `CoreRuleEngine` unit-entry resolution now checks this generic static ability and its controller experience requirement before deciding whether a played unit enters exhausted.
- The active-entry source scan can use controlled public legend-zone sources for this generic friendly-unit entry ability, without a Master Yi card-number / legend-identity branch.
- `UNIT_PLAYED_TO_BASE` / `UNIT_PLAYED_TO_BATTLEFIELD` payloads include `entryStaticAbilityKind`, `entryStaticAbilitySourceObjectId`, and source card metadata when this static ability controls the entry state.

Implemented for `SFD·027/221` 穿沙角兽:

- `StaticAbilityKinds.SourceUnitEnterReady = SOURCE_UNIT_ENTER_READY`.
- `StaticAbilitySpec.MaxControllerHandCount` can now carry source-unit active-entry hand-count requirements; Dunehorn Beast parses `如果你的手牌不超过两张，则我以活跃状态进场。` with `MaxControllerHandCount=2`.
- `CardStaticAbilitySpecRules.TryGetSourceUnitEnterReadyAbility` exposes the parsed spec to engine runtime.
- `CoreRuleEngine` source-unit entry resolution now checks this generic static ability against the controller's hand count after the played card has left hand.
- `UNIT_PLAYED_TO_BASE` / `UNIT_PLAYED_TO_BATTLEFIELD` payloads include `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY`, self source object id, and source card metadata when this static ability controls the entry state.

Implemented for `UNL-016/219` 焰爪:

- `StaticAbilityKinds.SourceUnitEnterReady = SOURCE_UNIT_ENTER_READY` is reused for level-gated source-unit active entry.
- `StaticAbilitySpec.RequiredPlayerExperience` now applies to source-unit active-entry requirements as well as friendly-unit active-entry requirements.
- `RuleTextParsers.StaticAbilityParser` parses `{{等级3>}} 我获得{{S}}+1，并以活跃状态进场。` into `SOURCE_UNIT_ENTER_READY` with `RequiredPlayerExperience=3`.
- `CardStaticAbilitySpecRules.TryGetSourceUnitEnterReadyAbility` accepts source-unit active-entry specs with either `MaxControllerHandCount` or `RequiredPlayerExperience`.
- `CoreRuleEngine` checks the parsed controller experience requirement before deciding whether the played source unit enters ready.
- `UNIT_PLAYED_TO_BASE` / `UNIT_PLAYED_TO_BATTLEFIELD` payloads include `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY`, self source object id, and source card metadata when this level-gated source-unit static ability controls the entry state.

Implemented for `UNL-151/219` 班德尔士兵:

- The same `SOURCE_UNIT_ENTER_READY` + `RequiredPlayerExperience` static ability path covers simple level-gated self active entry with no source-object power aura.
- `RuleTextParsers.StaticAbilityParser` parses `{{等级3>}} 我以活跃状态进场。` into `SOURCE_UNIT_ENTER_READY` with `RequiredPlayerExperience=3`.
- `CoreRuleEngine` checks the parsed controller experience requirement before deciding whether Bandle Soldier enters ready.
- `UNIT_PLAYED_TO_BASE` / `UNIT_PLAYED_TO_BATTLEFIELD` payloads include `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY`, self source object id, and source card metadata when this level-gated source-unit static ability controls the entry state.

Implemented for `SFD·094/221` 凶翼 and the same controlled-tag source-unit family:

- `StaticAbilitySpec.RequiredOtherControlledUnitTag` can now carry source-unit active-entry requirements that look for another controlled public unit with a matching official tag.
- `RuleTextParsers.StaticAbilityParser` parses `如果你控制着其他“龙”属性单位，则我以活跃状态进场。` into `SOURCE_UNIT_ENTER_READY` with `RequiredOtherControlledUnitTag=龙`.
- The same parser also covers `SFD·071/221` 疾驰机械 text `如果你控制着其他“机械”单位，则我以活跃状态进场。` with `RequiredOtherControlledUnitTag=机械`.
- `CardStaticAbilitySpecRules.TryGetSourceUnitEnterReadyAbility` accepts source-unit active-entry specs with `MaxControllerHandCount`, `RequiredPlayerExperience`, or `RequiredOtherControlledUnitTag`.
- `CoreRuleEngine` checks the entering unit controller's public field units, excludes the entering source object, and requires a face-up non-standby controlled unit carrying the parsed tag before the source unit enters ready.
- `UNIT_PLAYED_TO_BASE` / `UNIT_PLAYED_TO_BATTLEFIELD` payloads include `entryStaticAbilityKind=SOURCE_UNIT_ENTER_READY`, self source object id, and source card metadata when this controlled-tag source-unit static ability controls the entry state.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `OGN·011/298` 熔浆巨龙: `当我在场上时，其他友方单位以活跃状态进场。`
- `SFD·171/221` / `SFD·171a/221` 烈娜塔·戈拉斯克: `你的指示物以活跃状态进场。`
- `UNL-191/219` / `UNL-231/219` / `UNL-231*/219` 无极宗师: `{{等级11>}} 你的单位以活跃状态进场。`
- `SFD·027/221` 穿沙角兽: `如果你的手牌不超过两张，则我以活跃状态进场。`
- `UNL-016/219` 焰爪: `{{等级3>}} 我获得{{S}}+1，并以活跃状态进场。`
- `UNL-151/219` 班德尔士兵: `{{等级3>}} 我以活跃状态进场。（如果你拥有不少于3经验，则获得该效果。）`
- `SFD·094/221` 凶翼: `如果你控制着其他“龙”属性单位，则我以活跃状态进场。`
- `SFD·071/221` 疾驰机械: `如果你控制着其他“机械”单位，则我以活跃状态进场。`
- Core timing / play rules: `CORE-260330` p4-p8 rules 107-129 and p39-p42 rules 355-356.
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## Not Closed

- Full active-entry family breadth remains open: turn-scoped spell-granted entry, battlefield token entry payload coverage, and battlefield/hand source-zone variants still need separate BehaviorSpec slices.
- This slice does not close P0 full objective or READY.

## Validation

Focused red/green:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~BehaviorSpecCatalogParsesOtherFriendlyUnitsEnterReadyStaticAbility" --nologo
```

Result: initially failed 2/3 before implementation; after implementation 3/3 passed.

Molten Drake other-friendly active-entry B0 official-deck replay focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesMoltenDrakeOtherFriendlyActiveEntry"
```

Result: 1/1 passed.

Renata token filtered active-entry focused red/green:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RenataTokenActiveEntryStaticAbilityTests" --nologo
```

Result: initially failed at compile because `FRIENDLY_FILTERED_UNITS_ENTER_READY` / `TargetFilter` did not exist; after unit-token implementation 3/3 passed. The equipment-token follow-up initially failed because Gold equipment tokens lacked official token identity and remained exhausted under public Renata; after implementation 5/5 passed.

Master Yi level-gated active-entry focused red/green:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MasterYiLevelActiveEntryStaticAbility"
```

Result: initially failed at compile because `FRIENDLY_UNITS_ENTER_READY` / `RequiredPlayerExperience` did not exist; after implementation 4/4 passed.

Master Yi level-gated active-entry B0 official-deck replay focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesMasterYiLevelActiveEntry"
```

Result: 1/1 passed.

SFD Dunehorn Beast low-hand active-entry focused red/green:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~DunehornLowHandActiveEntryStaticAbility"
```

Result: initially failed at compile because `SOURCE_UNIT_ENTER_READY` / `MaxControllerHandCount` did not exist; after implementation 3/3 passed.

Dunehorn Beast low-hand active-entry B0 official-deck replay focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesDunehornBeastLowHandActiveEntry"
```

Result: 1/1 passed.

Master Yi / active-entry adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MasterYiLevelActiveEntryStaticAbility|FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~RenataTokenActiveEntryStaticAbility|FullyQualifiedName~CardCatalogBaseline"
```

Result: 307/307 passed.

Latest Dunehorn / active-entry / hidden-info adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~DunehornLowHandActiveEntryStaticAbility|FullyQualifiedName~UnitBattlefieldHeldDraw|FullyQualifiedName~Dunehorn|FullyQualifiedName~MasterYiLevelActiveEntryStaticAbility|FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~RenataTokenActiveEntryStaticAbility|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecoveryTests"
```

Result: 2297/2297 passed.

Latest Dunehorn low-hand active-entry replay / hidden-info adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesDunehornBeastLowHandActiveEntry|FullyQualifiedName~OfficialDeckMidgameResolvesDunehornBeastUnitHeldDraw|FullyQualifiedName~DunehornLowHandActiveEntryStaticAbility|FullyQualifiedName~MasterYiLevelActiveEntryStaticAbility|FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~RenataTokenActiveEntryStaticAbility|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecoveryTests"
```

Result: 2100/2100 passed.

Latest Molten Drake active-entry replay / hidden-info adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesMoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~LegionRearguardHasteReadyEntry|FullyQualifiedName~OfficialDeckMidgameResolvesDunehornBeastLowHandActiveEntry|FullyQualifiedName~DunehornLowHandActiveEntryStaticAbility|FullyQualifiedName~MasterYiLevelActiveEntryStaticAbility|FullyQualifiedName~RenataTokenActiveEntryStaticAbility|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecoveryTests"
```

Result: 2103/2103 passed.

Latest Master Yi level active-entry replay / hidden-info adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OfficialDeckMidgameResolvesMasterYiLevelActiveEntry|FullyQualifiedName~MasterYiLevelActiveEntryStaticAbility|FullyQualifiedName~OfficialDeckMidgameResolvesMoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~LegionRearguardHasteReadyEntry|FullyQualifiedName~OfficialDeckMidgameResolvesDunehornBeastLowHandActiveEntry|FullyQualifiedName~DunehornLowHandActiveEntryStaticAbility|FullyQualifiedName~RenataTokenActiveEntryStaticAbility|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecoveryTests"
```

Result: 2104/2104 passed.

Latest Flameclaw level-gated source-unit active-entry focused red/green:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~SourceUnitLevelActiveEntryStaticAbility"
```

Result: initially failed because the catalog parser did not expose the `SOURCE_UNIT_ENTER_READY` spec and the runtime left Flameclaw exhausted at level 3; after implementation 3/3 passed.

Flameclaw level-gated active-entry + source-object static-power B0 official-deck replay focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~SourceUnitLevelActiveEntryStaticAbility|FullyQualifiedName~OfficialDeckMidgameResolvesFlameclawLevelActiveEntryStaticAura"
```

Result: 4/4 passed.

Bandle Soldier level-gated source-unit active-entry focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~SourceUnitLevelActiveEntryStaticAbilityTests"
```

Result: 6/6 passed.

Bandle Soldier level-gated source-unit active-entry B0 official-deck replay focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~BandleSoldierLevelActiveEntry|FullyQualifiedName~SourceUnitLevelActiveEntryStaticAbilityTests"
```

Result: 7/7 passed.

Latest Bandle Soldier active-entry / full-game / hidden-info adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~BandleSoldierLevelActiveEntry|FullyQualifiedName~SourceUnitLevelActiveEntry|FullyQualifiedName~SourceUnitEnterReady|FullyQualifiedName~ActiveEntry|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline"
```

Result: 2456/2456 passed.

Fiercewing controlled-tag source-unit active-entry focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~ControlledTaggedSourceUnitActiveEntryStaticAbilityTests|FullyQualifiedName~OfficialDeckMidgameResolvesFiercewingControlledDragonActiveEntry"
```

Result: initially failed at compile because `StaticAbilitySpec.RequiredOtherControlledUnitTag` did not exist; after implementation 7/7 passed.

Latest Fiercewing controlled-tag active-entry / full-game / hidden-info adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~ControlledTaggedSourceUnitActiveEntry|FullyQualifiedName~FiercewingControlledDragonActiveEntry|FullyQualifiedName~SourceUnitEnterReady|FullyQualifiedName~SourceUnitLevelActiveEntry|FullyQualifiedName~DunehornLowHandActiveEntry|FullyQualifiedName~BandleSoldierLevelActiveEntry|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline"
```

Result: 2427/2427 passed.

Latest backend full after Fiercewing controlled-tag active-entry:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8990/8990 passed.

Latest Flameclaw active-entry / static-aura / hidden-info adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~SourceUnitLevelActiveEntry|FullyQualifiedName~SourceUnitEnterReady|FullyQualifiedName~ActiveEntry|FullyQualifiedName~StaticAbility|FullyQualifiedName~StaticAura|FullyQualifiedName~SourceObjectLevelPower|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline"
```

Result: 2501/2501 passed.

Hidden-info / continuous-effect recovery guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"
```

Result: 1984/1984 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~BehaviorSpecCatalogParsesOtherFriendlyUnitsEnterReadyStaticAbility|FullyQualifiedName~LegionRearguardHasteReadyEntry|FullyQualifiedName~ReksaiHasteReadyRedPayment|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2306/2306 passed.

Renata / token adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RenataTokenActiveEntryStaticAbilityTests|FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntryTests|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~P79LegendActAzir|FullyQualifiedName~P79LegendActCreatesMinionWithViktor|FullyQualifiedName~P79BattlefieldHeldCreatesMinionInBase|FullyQualifiedName~P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed|FullyQualifiedName~P79IroncladVanguardCreatesTwoRobotsWhenDestroyed|FullyQualifiedName~Warhawk" --nologo
```

Result: 335/335 passed.

Gold token / fixture adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GoldTokenResourceSkillTests|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~TreasureHunterMoveTriggerTests|FullyQualifiedName~RealTriggerQueueTests" --nologo
```

Result: 190/190 passed.

Conformance fixture runner after Gold token identity sync:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ConformanceFixtureRunnerTests" --nologo
```

Result: 3108/3108 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8881/8881 passed after the Dunehorn low-hand active-entry StaticAbilitySpec follow-up slice; the later Dunehorn B0 official-deck replay follow-up passed 8882/8882; the later Molten Drake B0 official-deck replay follow-up passed 8883/8883; the later Master Yi level B0 official-deck replay follow-up passed 8884/8884; the later Flameclaw level-gated source-unit active-entry + source-object static-power B0 replay follow-up passed 8971/8971.

DevUi catalog type build after adding active-entry static ability fields:

```bash
export PATH="/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/opt/homebrew/bin:/Users/dinghaolin/.nvm/versions/node/v20.20.1/bin:/usr/bin:/bin:/usr/sbin:/sbin"
npm --prefix src/Riftbound.DevUi run build
```

Result: passed.
