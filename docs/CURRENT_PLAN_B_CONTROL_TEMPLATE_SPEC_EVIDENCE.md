# Plan B Control Template Spec Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for surfacing official control-changing card text through BehaviorSpec template classification and the basic-action profile layer. Runtime control resolution remains on the existing implemented behavior paths.

## 1. Official Source

- `data/official/card-catalog.zh-CN.json`: `UNL-140/219` 强制征召 includes `你获得其控制权、将其变为休眠状态、并将其召回。`
- `data/official/card-catalog.zh-CN.json`: `SFD·202/221` 恶意收购 includes `获得战场上一名敌方单位的控制权。让其变为活跃状态。` and `回合结束时，失去该单位的控制权，然后将它召回。`
- `data/official/card-catalog.zh-CN.json`: `OGN·080/298` 倒转神通 includes `获得一个法术的控制权。你可以选择为其指定新的目标。`
- `data/official/card-catalog.zh-CN.json`: `OGN·203/298` 据为己有 includes `选择战场上的一名敌方单位，获得它的控制权并将其召回。`
- `docs/CURRENT_STAGE4C_BATCH36_HOSTILE_TAKEOVER_CONTROL_READY_EVIDENCE.md` records the prior Hostile Takeover gain-control + ready representative runtime evidence.
- `docs/CURRENT_STAGE4C_BATCH79_FORCED_CONSCRIPTION_CONTROL_SMALL_ENEMY_RECALL_EVIDENCE.md` records the prior Forced Conscription gain-control + recall representative runtime evidence.
- `docs/CURRENT_STAGE4D_01B_CONTROL_CHANGE_PRECISE_BATTLEFIELD_EVIDENCE.md` records prior precise battlefield preservation evidence for Hostile Takeover control change.

## 2. BehaviorSpec Evidence

`RuleTextParser` now parses official text containing “控制权” into `BehaviorTemplateIds.Control`.

The parser evidence is covered by `BehaviorSpecsParseControlChangingOfficialSpellTemplates`, which checks:

- `SFD·202/221` includes `control` and preserves a phrase containing `获得战场上一名敌方单位的控制权`.
- `UNL-140/219` includes both `control` and `recall`.
- `OGN·203/298` includes both `control` and `recall`.

`BehaviorSpecCatalogReportsSecondaryTemplateFamilies` now expects:

- `control`: 4 entries
- `control`: 4 implemented entries
- `control`: 4 functional units
- `control`: 4 implemented functional units

`BehaviorTemplateRegistryIncludesKnownTemplateSkeletons` now requires the control skeleton route.

`BehaviorTemplateDelegationBridgeMapsCatalogRowsToImplementedRuntimeBehavior` and `BehaviorTemplatePrimitiveExecutorRecognizesDelegatedBehaviorSpecs` now include Hostile Takeover as the representative control delegated behavior.

## 3. Move Template Correction Evidence

The same parser pass now avoids classifying pure reminder text as movement when it only says that a recall is not movement. It strips:

- `不算作移动`
- `不被视为移动`

before assigning `BehaviorTemplateIds.Move`.

This drops the Move secondary template baseline from 136 entries / 111 functional units to 123 entries / 102 functional units while leaving actual positive movement text classified as `move`.

## 4. Runtime Evidence

- `CardBasicActionRules` now exposes `CardBasicActionNames.Control` and `CardBasicActionProfile.HasControl`.
- Control is delegated only when the existing implemented behavior already carries one of these runtime flags:
  - `GainsControlOfTargetToBase`
  - `GainsControlOfTargetToBattlefield`
  - `GainsControlOfTargetStackSpell`
- `SFD·202/221` Hostile Takeover now reports `HasControl=true`, includes delegated `control`, and has no deferred control action.
- `HasRecallBehavior` includes `GainsControlOfTargetToBattlefield` so Hostile Takeover's return/recall shape remains visible in the profile surface.
- No new control resolver is added in this slice.

## 5. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardCatalogBaseline" --nologo
```

Result: first failed before implementation on missing `BehaviorTemplateIds.Control`, `CardBasicActionProfile.HasControl`, and `CardBasicActionNames.Control`; then 275/275 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~HostileTakeover|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~BasicAction|FullyQualifiedName~ConformanceFixtureRunner" --nologo
```

Result: 3395/3395 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8803/8803 passed.

## 6. Non-Closure

This is a focused catalog/spec/profile slice. It does not close complete control-zone-movement, complete owner/controller lifecycle, Hostile Takeover full reaction timing, Forced Conscription optional experience targeting breadth, Reversal retargeting, Taken for a Ride full swift timing, complete battlefield control cleanup, or project READY.
