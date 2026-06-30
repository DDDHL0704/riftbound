# Plan B Stack Trigger Source Identity Catalog Source Audit

日期：2026-06-26
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Eclipse Vanguard、Ravenbloom Student、OGS Lux、Arena Service Crew 这组 stack/card-play 代表触发的来源单位身份，从 `CoreRuleEngine` 里的直接 `sourceState.CardNo` 分支迁移到 `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`。该切片只收窄 trigger source identity 硬编码，并补齐 standby 来源不触发的隐藏边界；不关闭完整 TriggerSpec、完整 `ORDER_TRIGGERS`、APNAP ordering、完整 high-cost spell / equipment trigger breadth 或 READY。

## 2026-06-28 Follow-up: Eclipse/Arena Dead Source Rows Removed

`CoreRuleEngine` no longer retains the migrated `EclipseVanguardCardNo` or `ArenaServiceCrewCardNo` constants, and no longer owns a local `EclipseVanguardStunTriggerBehavior` row. `ResolveEclipseVanguardStunTriggers(...)` now reads the registered `ECLIPSE_VANGUARD_STUN_TRIGGER_PLAY_UNIT` source row via `CardBehaviorRegistry.TryGetByEffectKind(...)`, derives the resolution-time ready/+1 trigger behavior from that row, and uses the matched source object's cardNo with fallback to the registry source row rather than a Core-owned representative constant.

Validation passed for this follow-up: source identity guard 23/23; Eclipse Vanguard / Arena Service Crew representative trigger paths 6/6; MatchRecovery hidden-info/recovery boundary 1989/1989; backend full conformance 8873/8873. This follow-up remains source-row cleanup only; it does not close full TriggerSpec, complete `ORDER_TRIGGERS`, APNAP ordering, high-cost spell/equipment trigger breadth, P0 full objective, or READY.

## 2026-06-30 Follow-up: Arena Service Crew Uses Behavior Fields

The Arena Service Crew equipment-played ready selector has moved beyond catalog effect-kind identity. `CoreRuleEngine.ResolveSourceReadyOnEquipmentPlayedTriggers(...)` now derives matching sources from `SourceReadiesWhenControllerPlaysEquipment=true` and emits `SourceReadyOnEquipmentPlayedEffectKind=ARENA_SERVICE_CREW_EQUIPMENT_READY`; the row identity effect id `ARENA_SERVICE_CREW_EQUIPMENT_TRIGGER_PLAY_UNIT` remains catalog / fixture / matrix evidence data only. See `docs/CURRENT_PLAN_B_SOURCE_READY_ON_EQUIPMENT_PLAYED_BEHAVIOR_FIELDS_AUDIT.md`.

## 2026-06-30 Follow-up: Eclipse Vanguard Uses Behavior Fields

The Eclipse Vanguard enemy-stun ready/+1 selector has moved beyond catalog effect-kind identity. `CoreRuleEngine.ResolveEclipseVanguardStunTriggers(...)` now derives matching sources from `SourceReadiesWhenControllerStunsEnemyUnit=true`, emits `SourceStunEnemyUnitTriggerEffectKind=ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1`, and reads the +1 amount from `SourcePowerOnControllerStunsEnemyUnitAmount`; the row identity effect id `ECLIPSE_VANGUARD_STUN_TRIGGER_PLAY_UNIT` remains catalog / fixture / matrix evidence data only. See `docs/CURRENT_PLAN_B_SOURCE_STUN_READY_POWER_BEHAVIOR_FIELDS_AUDIT.md`.

## Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/TriggerSourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_STACK_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_STACK_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`

Not changed:

- official card catalog JSON
- card matrix JSON
- protocol shape
- trigger timing / ordering semantics
- frontend runtime
- `fullOfficial` / READY status

## Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Stack/card-play trigger sources no longer directly select by these source card numbers | `ResolveEclipseVanguardStunTriggers`, `ResolveRavenbloomStudentSpellPlayedTriggers`, `ResolveOgsLuxHighCostSpellPlayedTriggers`, and `ResolveArenaServiceCrewEquipmentPlayedTriggers` call `IsControlledFaceUpFieldUnitWithEffectKind` instead of comparing `sourceState.CardNo` with the representative card constants | Accepted |
| Runtime source checks consume registered source behavior rows | source identities use `ECLIPSE_VANGUARD_STUN_TRIGGER_PLAY_UNIT`, `RAVENBLOOM_STUDENT_SPELL_TRIGGER_PLAY_UNIT`, `OGS_LUX_HIGH_COST_SPELL_TRIGGER_PLAY_UNIT`, and `ARENA_SERVICE_CREW_EQUIPMENT_TRIGGER_PLAY_UNIT` through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` | Accepted |
| Hidden/standby source boundary is enforced consistently | the shared helper requires unit tag, not face-down, and not `CardObjectTags.Standby`; new tests cover Ravenbloom, Eclipse Vanguard, and Arena Service Crew standby sources | Accepted |
| Existing representative behavior is preserved | adjacent Ravenbloom / Eclipse Vanguard / Arena Service Crew / OGS Lux / Lux high-cost regression remains green | Accepted |
| Full trigger engine breadth | complete `TriggerSpec` migration, optional trigger ordering, APNAP, and full official breadth remain residual | Residual, no READY claim |

## Verification

Focused source identity guard:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~TriggerSourceIdentityGuardTests"
```

Result: 9/9 passed.

Focused standby source regressions:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~RavenbloomStudentSpellTriggerWhenSourceIsStandby|FullyQualifiedName~EclipseVanguardSkipsTriggerWhenSourceIsStandby|FullyQualifiedName~ArenaServiceCrewSkipsEquipmentTriggerWhenSourceIsStandby"
```

Result: 3/3 passed.

Adjacent trigger/high-cost/equipment regression:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~RavenbloomStudent|FullyQualifiedName~EclipseVanguard|FullyQualifiedName~ArenaServiceCrew|FullyQualifiedName~OgsLuxHighCostSpell|FullyQualifiedName~LuxHighCost"
```

Result: 57/57 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8646/8646 passed.

## Residual Risks

- This does not move these trigger conditions into `TriggerSpecRules`; it only removes direct source card-number identity checks from the current representative runtime paths.
- This does not implement complete simultaneous trigger ordering or `ORDER_TRIGGERS` breadth for this family.
- OGS Lux recovery validation still has card-context checks for the current protocol payload; those are replay/snapshot integrity guards, not source-selection runtime branches.
- Project remains **NOT READY**.
