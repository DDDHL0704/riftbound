# Plan B Control Template Spec Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把官方文本中的“控制权”效果纳入 `BehaviorTemplateIds.Control`、BehaviorSpec catalog 统计、template registry 和 basic action profile。该切片只建立 control-zone-movement 家族的 BehaviorSpec / catalog / delegation 面，不改变现有控制权运行时结算语义。

## 1. Scope

Changed:

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs`
- `src/Riftbound.Engine/BehaviorTemplateExecutor.cs`
- `src/Riftbound.Engine/CardBasicActionRules.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `docs/CURRENT_PLAN_B_CONTROL_TEMPLATE_SPEC_AUDIT.md`
- `docs/CURRENT_PLAN_B_CONTROL_TEMPLATE_SPEC_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- Existing control runtime resolution in `CoreRuleEngine`
- Existing `CardBehaviorRegistry` behavior rows for Forced Conscription, Hostile Takeover, Reversal, or Taken for a Ride
- Stack / priority lifecycle
- End-turn Hostile Takeover return-control cleanup
- Control-zone movement or battlefield contest task semantics
- Frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Official control-changing texts are classified by BehaviorSpec | `UNL-140/219`, `SFD·202/221`, `OGN·080/298`, and `OGN·203/298` now include `BehaviorTemplateIds.Control` from `RuleTextParser` when the official text contains “控制权” | Accepted |
| Control is a first-class template id | `BehaviorTemplateIds.Control = "control"` exists and `BehaviorTemplateRegistry.GetAll()` exposes the control skeleton route | Accepted |
| Control template is accepted as an existing safe template family | `BehaviorSpecCatalogBuilder.SafeExistingTemplateMappings` includes `BehaviorTemplateIds.Control`; catalog baseline expects 4 control entries / 4 functional units | Accepted |
| Existing implemented control runtime can be surfaced as delegated P2 behavior | `CardBasicActionRules` exposes `CardBasicActionNames.Control`, `HasControl`, and delegates Hostile Takeover through `GainsControlOfTargetToBattlefield` | Accepted |
| Recall + control effects retain recall delegation | `HasRecallBehavior` now includes `GainsControlOfTargetToBattlefield`, so Hostile Takeover remains covered for its end-turn return/recall shape in the basic-action profile | Accepted |
| Reminder text that says a recall is not movement is not counted as movement | `RuleTextParser` strips `不算作移动` and `不被视为移动` before assigning the move template; Move catalog count drops from 136 entries / 111 FUs to 123 entries / 102 FUs | Accepted |
| Runtime behavior is not broadened accidentally | The slice adds no new resolver and does not change official control representative fixtures; runtime remains delegated to existing implemented behavior rows | Accepted |
| Complete control-zone-movement family | Full control lifecycle, optional cost breadth, retargeting, control freeze/release, and battlefield cleanup remain residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardCatalogBaseline" --nologo
```

Result: first failed before implementation on missing `BehaviorTemplateIds.Control`, `CardBasicActionProfile.HasControl`, and `CardBasicActionNames.Control`; then 275/275 passed.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~HostileTakeover|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~BasicAction|FullyQualifiedName~ConformanceFixtureRunner" --nologo
```

Result: 3395/3395 passed.

Full backend:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8803/8803 passed.

## 4. Residuals

- This slice does not implement a new generic control resolver.
- Reversal retargeting remains outside this slice.
- Forced Conscription optional experience branch remains outside this slice.
- Hostile Takeover full standby / reaction timing and full end-turn cleanup model remain outside this slice.
- Complete control freeze/release, battlefield ownership cleanup, and control-zone movement matrix remain open.
- Project remains NOT READY.
