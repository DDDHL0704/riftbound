# Plan B Active-Entry Static Ability Spec Audit

更新时间：2026-06-27

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

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `OGN·011/298` 熔浆巨龙: `当我在场上时，其他友方单位以活跃状态进场。`
- `SFD·171/221` / `SFD·171a/221` 烈娜塔·戈拉斯克: `你的指示物以活跃状态进场。`
- Core timing / play rules: `CORE-260330` p4-p8 rules 107-129 and p39-p42 rules 355-356.
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## Not Closed

- Full active-entry family breadth remains open: low-hand entry, level-gated entry, turn-scoped spell-granted entry, battlefield token entry payload coverage, and battlefield/hand source-zone variants still need separate BehaviorSpec slices.
- This slice does not add legal official-deck score-victory replay coverage for 熔浆巨龙.
- This slice does not close P0 full objective or READY.

## Validation

Focused red/green:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~BehaviorSpecCatalogParsesOtherFriendlyUnitsEnterReadyStaticAbility" --nologo
```

Result: initially failed 2/3 before implementation; after implementation 3/3 passed.

Renata token filtered active-entry focused red/green:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RenataTokenActiveEntryStaticAbilityTests" --nologo
```

Result: initially failed at compile because `FRIENDLY_FILTERED_UNITS_ENTER_READY` / `TargetFilter` did not exist; after unit-token implementation 3/3 passed. The equipment-token follow-up initially failed because Gold equipment tokens lacked official token identity and remained exhausted under public Renata; after implementation 5/5 passed.

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

Result: 8846/8846 passed after the Renata equipment-token follow-up slice.
