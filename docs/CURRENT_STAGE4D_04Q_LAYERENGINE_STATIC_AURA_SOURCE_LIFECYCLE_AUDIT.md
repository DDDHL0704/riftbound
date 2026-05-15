# Stage 4D-04Q LayerEngine Static Aura Source Lifecycle Audit

日期：2026-05-16
结论：**IMPLEMENTED AND A-VALIDATED / WRITELOCK CLOSED / PROJECT NOT READY**

本文件记录 4D-04Q-B 的 A-side 验收。B-Implementation / Euclid `019e2caf-92c5-7502-8db3-b091e443ad3c` 已完成 static aura source lifecycle foundation；A 已审查 diff 并复跑 focused / adjacent / backend full / patch hygiene。该切片只关闭 4D-04Q representative，不关闭完整 LayerEngine、timestamp dependency graph、P1-001、P1-002、card matrix full-official、frontend final validation 或 READY。

## 1. Scope

Runtime / test write scope used:

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

Not touched:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- frontend runtime / DevUi stores
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad PaymentEngine
- battle lifecycle / task queue semantics
- wide equipment runtime / full `百炼` breadth
- full LayerEngine / timestamp dependency graph rewrite
- full-card matrix status, `fullOfficial`, READY
- `riftbound-dotnet.sln`

## 2. Accepted Behavior

4D-04Q-B is accepted because A verified all of the following:

1. `ContinuousEffectLayers.StaticAura` now gives the server a distinct foundation layer for static aura audit views.
2. `ContinuousEffectState` and snapshot `timing.continuousEffects[]` can expose `condition`, `lifecycle` and `participantObjectIds`.
3. Ornn friendly-equipment static recompute now emits a server-derived static aura view with source object, target object, equipment participants, condition, lifecycle, power delta, base power and effective power.
4. Ornn static aura metadata disappears when the source unit leaves public field.
5. Battlefield `OGN·294/298` all-units +1 representative now emits a battlefield static aura view for battle participants without changing combat arithmetic.
6. Battlefield static aura metadata disappears after the battle resolves and participants leave field.
7. Existing `power`, `combatPower`, `staticPowerBonus`, until-end power modifier ledger, minimum-power metadata and applied-order metadata do not regress.
8. Snapshot metadata remains a foundation/report view, not a full LayerEngine claim.

## 3. A-Side Validation

Focused static-aura / LayerEngine-view guard:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **11/11 passed**.

Adjacent static / continuous-effect / equipment regression:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~BattlefieldStaticPower|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~LayerEngine|FullyQualifiedName~EquipmentKeyword"
```

Result: **49/49 passed**.

Backend full:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **4451/4451 passed**.

Patch hygiene:

```sh
git diff --check
```

Result: **passed**.

## 4. Residual Risk

- This remains a foundation/report view, not a full continuous-effect LayerEngine.
- Timestamp ordering, dependency ordering and source ordering remain deferred.
- Broader static aura families, replacement effects, all equipment static modifiers and full `百炼` breadth remain outside this slice.
- Battlefield static aura metadata has a representative fallback for legacy test states that lack detailed `ObjectLocations.BattlefieldObjectId`; full official battlefield-location dependency modeling remains deferred.
- Frontend final validation, Chrome smoke, formal 18-step E2E and final completion audit were not run in this slice.

## 5. Verdict

4D-04Q-B is accepted and its runtime / focused-test write lock is closed. Project remains **NOT READY**.
