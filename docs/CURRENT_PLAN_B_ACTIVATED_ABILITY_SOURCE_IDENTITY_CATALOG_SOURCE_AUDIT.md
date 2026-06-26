# Plan B Activated Ability Source Identity Catalog Source Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `CoreRuleEngine` 中剩余 `ACTIVATE_ABILITY` command-side source card-number revalidation 从直接比较 `sourceState.CardNo == ability.SourceCardNo` 改为统一 `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)`。2026-06-27 follow-up 继续把 Gatekeeper Maduli prompt target filtering 与 command target legality 中的 direct `GatekeeperMaduliCardNo` source check 改为同一个 source-card group helper。该切片不新增能力、不扩展 source card group 数据、不改变支付、目标战力比较、横置、stack item、事件或快照语义；它只把已存在的 activated ability source group helper 作为单一入口。

## 1. Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ActivatedAbilitySourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_ACTIVATED_ABILITY_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_ACTIVATED_ABILITY_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs` source group rows
- activated ability catalog rows, payment, target battle-power comparison, stack resolution, or event payloads
- hidden information snapshot boundaries
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Command-side activated ability source checks consume the catalog source group helper | The seven remaining `!string.Equals(sourceState.CardNo, ability.SourceCardNo, ...)` checks in `CoreRuleEngine` were replaced with `!P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)` | Accepted |
| Gatekeeper Maduli prompt and command target source checks consume the same helper | `IsPromptGatekeeperMaduliMoveTarget` and `IsLegalGatekeeperMaduliMoveTarget` now fetch `GatekeeperMaduliMoveAbilityId` and call `P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)` instead of comparing `sourceState.CardNo` to `GatekeeperMaduliCardNo` | Accepted |
| No new card-number branch was introduced | `P4ActivatedAbilityCatalog.SourceCardNosForAbility` is unchanged; this slice only consumes it | Accepted |
| Regression guards prevent direct source-card comparison from returning | `CoreActivatedAbilitySourceChecksUseCatalogSourceCardGroups` fails on the direct `ability.SourceCardNo` comparison string; `GatekeeperMaduliTargetLegalityUsesCatalogSourceCardGroup` fails on direct `sourceState.CardNo, P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo` source checks and requires the catalog helper in Core and MatchSession | Accepted |
| Adjacent activated ability behavior is unchanged | Activated ability/payment/MatchRecovery adjacent filter passed 2992/2992 for the original command-side slice; Gatekeeper / CrimsonRose / PaymentEngine adjacent filter passed 759/759 for the follow-up | Accepted |
| Backend full conformance remains green | 8768/8768 passed for the original slice; 8771/8771 passed for the follow-up | Accepted |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreActivatedAbilitySourceChecksUseCatalogSourceCardGroups" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `ability.SourceCardNo` comparison, then 1/1 passed after implementation.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GatekeeperMaduliTargetLegalityUsesCatalogSourceCardGroup" --nologo
```

Result: failed before implementation on direct `sourceState.CardNo` / `P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo` comparison, then 1/1 passed after implementation.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ActivatedAbilitySourceIdentityGuardTests|FullyQualifiedName~ViActivated|FullyQualifiedName~Xerath|FullyQualifiedName~Malzahar|FullyQualifiedName~DragonSoulSage|FullyQualifiedName~CrimsonRose|FullyQualifiedName~FluftPoro|FullyQualifiedName~ShadowActivated|FullyQualifiedName~RenataActivated|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~EzrealBlueSwift|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2992/2992 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ActivatedAbilitySourceIdentityGuardTests|FullyQualifiedName~GatekeeperMaduliActivatedAbilityTests|FullyQualifiedName~CrimsonRoseActivatedAbilityTests|FullyQualifiedName~PaymentEngineCoverageAuditTests" --nologo
```

Result: 759/759 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8771/8771 passed.

## 4. Residual Risks

- `P4ActivatedAbilityCatalog` is still hand-authored catalog data; this slice does not extract activated ability source groups from BehaviorSpec.
- Complete activated ability family breadth, complete PaymentEngine matrix, complete target/timing/stack breadth, and READY remain open.
- Project remains **NOT READY**.
