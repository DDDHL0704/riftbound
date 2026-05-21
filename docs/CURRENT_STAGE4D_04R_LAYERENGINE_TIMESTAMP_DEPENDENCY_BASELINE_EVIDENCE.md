# Stage 4D-04R LayerEngine Timestamp Dependency Baseline Evidence

Date: 2026-05-21

Conclusion: **BASELINE OPEN / PROJECT NOT READY**

## Scope

This file records the baseline for the proposed 4D-04R LayerEngine timestamp / dependency graph representative slice.

No runtime, frontend, card matrix or protocol changes are made in this baseline batch.

## Current Baseline

Current 4D-04Q accepted state already exposes server-authored continuous-effect metadata:

- `layer`
- `duration`
- `sourceObjectId`
- `targetObjectId`
- `powerDelta`
- `basePower`
- `effectivePower`
- `effectKind`
- `sourceCardNo`
- `sourcePath`
- `appliedOrder`
- `condition`
- `lifecycle`
- `participantObjectIds`

Current baseline gap for 4D-04R:

- no explicit timestamp / sequence metadata field is recorded on `ContinuousEffectState`;
- no explicit dependency object-id graph is recorded beyond source / target / participant ids;
- current ordering evidence is representative, not a full dependency graph;
- static aura lifecycle foundation is accepted, but full timestamp / dependency ordering remains deferred.

## Baseline Validation

Baseline command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~LayerEngine|FullyQualifiedName~EquipmentKeyword"
```

Result: **passed 37/37**.

Patch hygiene:

```sh
git diff --check
```

Result: **passed**.

## Non-Closure

This baseline does not close full official LayerEngine, P1-001, P1-002, card matrix fullOfficial, frontend final validation or READY.
