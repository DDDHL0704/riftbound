# Plan B Other-Friendly Active-Entry Static Ability Spec Audit

更新时间：2026-06-27

## Scope

This slice advances Plan B by moving `OGN·011/298` 熔浆巨龙's static active-entry text from a deferred card row into the shared BehaviorSpec-driven unit-entry lifecycle.

Implemented in this slice:

- `StaticAbilityKinds.OtherFriendlyUnitsEnterReady = OTHER_FRIENDLY_UNITS_ENTER_READY`.
- `RuleTextParsers.StaticAbilityParser` parses `当我在场上时，其他友方单位以活跃状态进场。` into `BehaviorSpec.StaticAbilities`.
- `CardStaticAbilitySpecRules.TryGetOtherFriendlyUnitsEnterReadyAbility` exposes the parsed spec to engine runtime.
- `CoreRuleEngine` unit-entry resolution now checks public, face-up, non-standby friendly field-unit sources with that static ability before deciding whether a played unit enters exhausted.
- The source object is excluded from its own static ability, and face-down / standby sources do not grant active entry.
- `UNIT_PLAYED_TO_BASE` / `UNIT_PLAYED_TO_BATTLEFIELD` payloads include `entryStaticAbilityKind`, `entryStaticAbilitySourceObjectId`, and source card metadata when this static ability controls the entry state.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `OGN·011/298` 熔浆巨龙: `当我在场上时，其他友方单位以活跃状态进场。`
- Core timing / play rules: `CORE-260330` p4-p8 rules 107-129 and p39-p42 rules 355-356.
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## Not Closed

- Full active-entry family breadth remains open: low-hand entry, level-gated entry, turn-scoped spell-granted entry, token-only entry, and battlefield/hand source-zone variants still need separate BehaviorSpec slices.
- This slice does not add legal official-deck score-victory replay coverage for 熔浆巨龙.
- This slice does not close P0 full objective or READY.

## Validation

Focused red/green:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~BehaviorSpecCatalogParsesOtherFriendlyUnitsEnterReadyStaticAbility" --nologo
```

Result: initially failed 2/3 before implementation; after implementation 3/3 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MoltenDrakeOtherFriendlyActiveEntry|FullyQualifiedName~BehaviorSpecCatalogParsesOtherFriendlyUnitsEnterReadyStaticAbility|FullyQualifiedName~LegionRearguardHasteReadyEntry|FullyQualifiedName~ReksaiHasteReadyRedPayment|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2306/2306 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8841/8841 passed.
