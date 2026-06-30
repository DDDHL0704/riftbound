# Plan B Attack Damage Trigger Source Identity Catalog Source Audit

日期：2026-06-26
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Sharpshooter Pirate / 神射海盗进攻伤害触发的来源单位身份，从 `CoreRuleEngine` 里的直接 `attackerState.CardNo` 分支迁移到 `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`。该切片只收窄 attack-damage trigger source identity 硬编码；不关闭完整 combat-trigger TriggerSpec、完整战斗触发目标选择、`ORDER_TRIGGERS`、APNAP ordering 或 READY。

## 2026-06-30 Follow-up: Sharpshooter Pirate Uses Behavior Fields

The live Sharpshooter Pirate attack-damage selector has moved beyond catalog effect-kind identity. `CoreRuleEngine.ResolveSourceAttackDamageToFirstDefenderTriggers(...)` now derives matching sources and the damage amount from `SourceAttackDamageToFirstDefenderAmount=1` and `SourceAttackDamageToFirstDefenderEffectKind=SHARPSHOOTER_PIRATE_ATTACK_DAMAGE_1`; the row identity effect id `SHARPSHOOTER_PIRATE_ATTACK_TRIGGER_PLAY_UNIT` remains catalog / fixture / matrix evidence data only. See `docs/CURRENT_PLAN_B_SOURCE_ATTACK_DAMAGE_BEHAVIOR_FIELDS_AUDIT.md`.

## Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/TriggerSourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_ATTACK_DAMAGE_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_ATTACK_DAMAGE_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`

Not changed:

- official card catalog JSON
- card matrix JSON
- protocol shape
- trigger timing / ordering semantics
- frontend runtime
- `fullOfficial` / READY status

## Historical Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Sharpshooter Pirate attack-damage trigger source no longer directly selected by card number in the 2026-06-26 slice | `ResolveSharpshooterPirateAttackDamageTrigger` called `IsControlledFaceUpFieldUnitWithEffectKind` instead of comparing `attackerState.CardNo` with `SharpshooterPirateCardNo`; this was later superseded by the 2026-06-30 behavior-field path | Accepted |
| Runtime source check consumed registered source behavior row in the 2026-06-26 slice | source identity used `SHARPSHOOTER_PIRATE_ATTACK_TRIGGER_PLAY_UNIT` through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`; this was later superseded by `SourceAttackDamageToFirstDefenderAmount` behavior fields | Accepted |
| Hidden/standby source boundary remains enforced | shared helper requires unit tag, not face-down, and not `CardObjectTags.Standby`; existing battle tests still prove only the attacking source path emits the damage trigger | Accepted |
| Existing representative behavior is preserved | `P79SharpshooterPirateDamagesEnemyUnitWhenAttackingBattlefield` and `P79SharpshooterPirateSkipsAttackDamageWhenDefending` remain green | Accepted |
| Full combat-trigger engine breadth | complete `TriggerSpec` migration, battle trigger target selection, simultaneous trigger ordering, and APNAP remain residual | Residual, no READY claim |

## Verification

Focused source identity guard:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result: 15/15 passed.

Focused Sharpshooter behavior:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79SharpshooterPirate" --nologo
```

Result: 2/2 passed.

Adjacent battle/source identity regression:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SharpshooterPirate|FullyQualifiedName~DeclareBattle|FullyQualifiedName~BattleDamageAssignment|FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result: 238/238 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8652/8652 passed.

## Residual Risks

- This does not move Sharpshooter Pirate's combat trigger condition into `TriggerSpecRules`; it only removes the direct source card-number identity check from the current representative runtime path.
- This does not implement complete battle-trigger target selection, simultaneous trigger ordering, or `ORDER_TRIGGERS` breadth.
- Project remains **NOT READY**.
