# Plan B Stack Trigger Source Identity Catalog Source Evidence

日期：2026-06-26
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for removing direct source card-number checks from the current stack/card-play trigger representatives for Eclipse Vanguard, Ravenbloom Student, OGS Lux, and Arena Service Crew.

## 2026-06-28 Follow-up Evidence

- `CoreRuleEngine` no longer defines `EclipseVanguardCardNo`, `ArenaServiceCrewCardNo`, or a local `EclipseVanguardStunTriggerBehavior`.
- `ResolveEclipseVanguardStunTriggers(...)` now calls `CardBehaviorRegistry.TryGetByEffectKind(ECLIPSE_VANGUARD_STUN_TRIGGER_PLAY_UNIT, ...)` and derives the resolution-time `ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1` ready/+1 behavior from the registered source row.
- The Eclipse trigger stack item now falls back to the registry source row cardNo if the matched source object lacks a cardNo, instead of falling back to a Core-owned representative constant.
- `TriggerSourceIdentityGuardTests.CoreRuleEngineTriggerSourceSelectionUsesCatalogEffectKindIdentity` now blocks reintroducing those dead Core source constants or the local Eclipse trigger behavior row.

Validation passed for this follow-up: `TriggerSourceIdentityGuardTests` 23/23; `P79EclipseVanguard|P79ArenaServiceCrew` 6/6; `MatchRecovery` 1989/1989; backend full conformance 8873/8873.

## 2026-06-30 Follow-up Evidence

- `CoreRuleEngine.ResolveSourceReadyOnEquipmentPlayedTriggers(...)` no longer checks `ARENA_SERVICE_CREW_EQUIPMENT_TRIGGER_PLAY_UNIT`.
- `CardBehaviorRegistry` stores `SourceReadiesWhenControllerPlaysEquipment=true` and `SourceReadyOnEquipmentPlayedEffectKind=ARENA_SERVICE_CREW_EQUIPMENT_READY` on `OGN·091/298`.
- `TriggerSourceIdentityGuardTests.CoreRuleEngineTriggerSourceSelectionUsesBehaviorFieldsWhereAvailable` now blocks reintroducing the Arena Service Crew runtime effect-kind selector.
- The representative behavior-field evidence is recorded in `docs/CURRENT_PLAN_B_SOURCE_READY_ON_EQUIPMENT_PLAYED_BEHAVIOR_FIELDS_EVIDENCE.md`.

## 2026-06-30 Eclipse Vanguard Behavior-Field Follow-up Evidence

- `CoreRuleEngine.ResolveEclipseVanguardStunTriggers(...)` no longer checks `ECLIPSE_VANGUARD_STUN_TRIGGER_PLAY_UNIT`.
- `CardBehaviorRegistry` stores `SourceReadiesWhenControllerStunsEnemyUnit=true`, `SourcePowerOnControllerStunsEnemyUnitAmount=1`, and `SourceStunEnemyUnitTriggerEffectKind=ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1` on `OGN·059/298`.
- `TriggerSourceIdentityGuardTests.CoreRuleEngineTriggerSourceSelectionUsesBehaviorFieldsWhereAvailable` now blocks reintroducing the Eclipse Vanguard runtime effect-kind selector and emitted effect-kind / power constants.
- The representative behavior-field evidence is recorded in `docs/CURRENT_PLAN_B_SOURCE_STUN_READY_POWER_BEHAVIOR_FIELDS_EVIDENCE.md`.

## Runtime Evidence

- Historically, `CoreRuleEngine.IsControlledFaceUpFieldUnitWithEffectKind(...)` was the shared source predicate for this slice.
- Its source-identity predicate required:
  - `CardObjectTags.UnitCard`
  - `IsFaceDown == false`
  - no `CardObjectTags.Standby`
  - `CardBehaviorRegistry.IsImplementedUnitWithEffectKind(sourceState.CardNo, sourceEffectKind)`
- Historically this slice recorded `ResolveEclipseVanguardStunTriggers(...)` using source effect kind `ECLIPSE_VANGUARD_STUN_TRIGGER_PLAY_UNIT`; the 2026-06-30 behavior-field follow-up supersedes that path with `SourceReadiesWhenControllerStunsEnemyUnit`.
- `ResolveRavenbloomStudentSpellPlayedTriggers(...)` now uses source effect kind `RAVENBLOOM_STUDENT_SPELL_TRIGGER_PLAY_UNIT`.
- `ResolveOgsLuxHighCostSpellPlayedTriggers(...)` now uses source effect kind `OGS_LUX_HIGH_COST_SPELL_TRIGGER_PLAY_UNIT`.
- Historically this slice recorded `ResolveArenaServiceCrewEquipmentPlayedTriggers(...)` using source effect kind `ARENA_SERVICE_CREW_EQUIPMENT_TRIGGER_PLAY_UNIT`; the 2026-06-30 behavior-field follow-up supersedes that path with `ResolveSourceReadyOnEquipmentPlayedTriggers(...)`.
- Trigger stack items now use the actual matched source object's `CardNo`; historically the Eclipse Vanguard ready/+1 trigger fell back to the registered source behavior row instead of a Core-owned representative constant, and the 2026-06-30 follow-up now gets the emitted effect kind and amount from that card's behavior fields.

## Test Evidence

Focused test files:

- `tests/Riftbound.ConformanceTests/TriggerSourceIdentityGuardTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

Coverage:

- `CardBehaviorRegistryIdentifiesStackTriggerSourceUnitsByEffectKind` accepts the four registered source rows used by this slice.
- `CardBehaviorRegistryRejectsNonMatchingStackTriggerSourceUnits` rejects cross-effect source identity matches.
- `CoreRuleEngineTriggerSourceSelectionUsesCatalogEffectKindIdentity` blocks reintroducing direct `sourceState.CardNo` comparisons for the four source constants, blocks dead Eclipse/Arena Core source constants, and verifies the shared helper consumes `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`.
- `CoreRuleEngineSkipsRavenbloomStudentSpellTriggerWhenSourceIsStandby` proves a standby Ravenbloom Student does not receive the spell-played power modifier.
- `P79EclipseVanguardSkipsTriggerWhenSourceIsStandby` proves a standby Eclipse Vanguard does not ready or gain power after its controller stuns an enemy unit.
- `P79ArenaServiceCrewSkipsEquipmentTriggerWhenSourceIsStandby` proves a standby Arena Service Crew does not ready when its controller plays equipment.

## Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~TriggerSourceIdentityGuardTests"
```

Result: 9/9 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~RavenbloomStudentSpellTriggerWhenSourceIsStandby|FullyQualifiedName~EclipseVanguardSkipsTriggerWhenSourceIsStandby|FullyQualifiedName~ArenaServiceCrewSkipsEquipmentTriggerWhenSourceIsStandby"
```

Result: 3/3 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~RavenbloomStudent|FullyQualifiedName~EclipseVanguard|FullyQualifiedName~ArenaServiceCrew|FullyQualifiedName~OgsLuxHighCostSpell|FullyQualifiedName~LuxHighCost"
```

Result: 57/57 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8646/8646 passed.

## Non-Closure Statement

This evidence does not close complete stack trigger timing, complete trigger queue ordering, complete OGS Lux paid-cost / high-cost breadth, complete equipment trigger breadth, card matrix full-official state, frontend final validation, or READY.
