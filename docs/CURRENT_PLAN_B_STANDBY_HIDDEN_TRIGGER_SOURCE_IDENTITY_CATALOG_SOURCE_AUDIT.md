# Plan B Standby Hidden Trigger Source Identity Catalog Source Audit

日期：2026-06-26
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Ember Monk / 余火修士暗置待命牌触发的来源单位身份，从 `CoreRuleEngine` 里的直接 `sourceState.CardNo` 分支迁移到 `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`。该切片只收窄 standby-hidden trigger source identity 硬编码；不关闭完整 TriggerSpec、完整 standby/hidden-info trigger timing、`ORDER_TRIGGERS`、APNAP ordering 或 READY。

## Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/TriggerSourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_STANDBY_HIDDEN_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_STANDBY_HIDDEN_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
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
| Ember Monk standby-hidden trigger source no longer directly selects by card number | `ResolveEmberMonkStandbyHiddenPowerTrigger` calls `IsControlledFaceUpFieldUnitWithEffectKind` instead of comparing `sourceState.CardNo` with `EmberMonkCardNo` | Accepted |
| Runtime source check consumes registered source behavior row | source identity uses `EMBER_MONK_STANDBY_TRIGGER_PLAY_UNIT` through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` | Accepted |
| Hidden/standby source boundary remains enforced | shared helper requires unit tag, not face-down, and not `CardObjectTags.Standby`; existing Ember Monk fixture still proves only the visible friendly field source gains power | Accepted |
| Existing representative behavior is preserved | `P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden` remains green and still asserts face-down, standby, and opposing Ember Monk objects do not receive the modifier | Accepted |
| Full standby-trigger engine breadth | complete `TriggerSpec` migration, standby hidden-info timing, simultaneous trigger ordering, and APNAP remain residual | Residual, no READY claim |

## Verification

Focused source identity guard:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result: 12/12 passed.

Focused Ember Monk behavior:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden" --nologo
```

Result: 1/1 passed.

Adjacent Ember/source identity regression:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~EmberMonk|FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result: 14/14 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8649/8649 passed.

## Residual Risks

- This does not move Ember Monk's trigger condition into `TriggerSpecRules`; it only removes the direct source card-number identity check from the current representative runtime path.
- This does not implement complete standby-hidden trigger timing, simultaneous trigger ordering, or `ORDER_TRIGGERS` breadth.
- Project remains **NOT READY**.
