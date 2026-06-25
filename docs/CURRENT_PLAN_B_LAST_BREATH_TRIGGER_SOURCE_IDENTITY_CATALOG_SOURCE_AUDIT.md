# Plan B Last-Breath Trigger Source Identity Catalog Source Audit

日期：2026-06-26
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Kogmaw、Undercover Agent、Honest Broker、Unsung Hero 这组 last-breath representative 触发的来源单位身份，从 `CoreRuleEngine` 里的直接 `destroyedState.CardNo` 分支迁移到 `CardBehaviorRegistry.IsImplementedUnitWithEffectKind`。该切片只收窄 last-breath trigger source identity 硬编码；不关闭完整 `TriggerSpec`、完整 `ORDER_TRIGGERS`、APNAP ordering、完整 last-breath family breadth、effective-power 强力判定矩阵或 READY。

## Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/TriggerSourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_LAST_BREATH_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_LAST_BREATH_TRIGGER_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
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
| Last-breath representative sources no longer directly select by these source card numbers | `ResolveKogmawLastBreathAoePlayerId`, `ResolveUndercoverAgentLastBreathPlayerId`, `ResolveHonestBrokerLastBreathGoldPlayerId`, and `ResolveUnsungHeroLastBreathDrawPlayerId` call `IsFaceUpNonStandbyUnitWithEffectKind` instead of comparing `destroyedState.CardNo` with the representative card constants | Accepted |
| Runtime source checks consume registered source behavior rows | source identities use `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT`, `UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT`, `HONEST_BROKER_LAST_BREATH_GOLD_PLAY_UNIT`, and `UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_PLAY_UNIT` through `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` | Accepted |
| Hidden/standby source boundary remains enforced consistently | the shared helper requires unit tag, not face-down, and not `CardObjectTags.Standby` before accepting the catalog source effect kind | Accepted |
| Existing representative behavior is preserved | adjacent Kogmaw / Undercover Agent / Honest Broker / Unsung Hero regression remains green | Accepted |
| Full last-breath trigger engine breadth | complete `TriggerSpec` migration, optional trigger ordering, APNAP, effective-power checks, and full official breadth remain residual | Residual, no READY claim |

## Verification

Starting backend baseline:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8652/8652 passed.

Focused source identity guard:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result after implementation: 23/23 passed.

Adjacent last-breath representatives:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Kogmaw|FullyQualifiedName~UndercoverAgent|FullyQualifiedName~HonestBroker|FullyQualifiedName~UnsungHero|FullyQualifiedName~TriggerSourceIdentityGuardTests" --nologo
```

Result: 111/111 passed.

Trigger/recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~UndercoverAgentTriggerTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2058/2058 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8660/8660 passed.

## Residual Risks

- This does not move these last-breath conditions into `TriggerSpecRules`; it only removes direct source card-number identity checks from the current representative runtime paths.
- This does not implement complete simultaneous trigger ordering, complete last-breath family breadth, or full `ORDER_TRIGGERS` semantics.
- Unsung Hero still uses the existing `destroyedState.Power >= 5` representative guard for powerful; complete effective-power / LayerEngine integration remains open.
- Project remains **NOT READY**.
