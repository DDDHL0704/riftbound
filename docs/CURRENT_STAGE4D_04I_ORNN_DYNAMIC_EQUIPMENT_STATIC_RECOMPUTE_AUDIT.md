# Stage 4D-04I Ornn Dynamic Equipment Static Recompute Audit

日期：2026-05-15
结论：**IMPLEMENTED AND A-VALIDATED / WRITELOCK CLOSED / PROJECT NOT READY**

本文件记录 4D-04I-B 的 A-side 验收。B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 已完成 Ornn dynamic friendly-equipment static recompute representative；A 已审查 diff 并复跑 focused / adjacent / backend full / patch hygiene。该切片只关闭 4D-04I representative，不关闭完整 LayerEngine、完整 `百炼`、所有装备静态修正、card matrix full-official、frontend final validation 或 READY。

## 1. Scope

Runtime / test write scope used:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`

Not touched:

- frontend runtime / DevUi stores
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad LayerEngine rewrite
- unrelated equipment keywords, PaymentEngine, battle lifecycle, hidden-info redaction
- full-card matrix status, `fullOfficial`, READY
- `riftbound-dotnet.sln`

## 2. Accepted Behavior

4D-04I-B is accepted because A verified all of the following:

1. Accepted core commands now run a narrow friendly-equipment static power recompute before returning snapshots/prompts.
2. Recompute only targets public field units whose registered behavior has `AddsFriendlyFieldEquipmentCountToSourceUnitPower`.
3. Ornn power is derived from registered source unit power + current friendly public field equipment count + existing until-end power modifier, avoiding 4D-04H entry-time bonus double-count.
4. Ornn already in field recomputes upward when a friendly public equipment resolves into field.
5. Ornn recomputes downward when a formerly counted friendly equipment is no longer in field.
6. Repeated accepted commands after the same equipment state do not drift beyond base + current count.
7. Hand, enemy, face-down, dirty-controller and non-equipment objects remain excluded after dynamic recompute, not only during entry-time play.
8. Snapshot `power`, `basePower` and `effectivePower` stay consistent with the recomputed authoritative state under the current snapshot model.
9. Existing Ornn entry-time tests, equipment keyword profile guards, Tempered, Jax, Akshan, Armed Assaulter and continuous-effect snapshot representatives remain green.

## 3. A-Side Validation

Focused / keyword / LayerEngine-view guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **9/9 passed**.

Adjacent equipment / payment regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

Result: **117/117 passed**.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **4446/4446 passed**.

Patch hygiene:

```sh
git diff --check
```

Result: **passed**.

## 4. Residual Risk

- This remains a narrow representative recompute, not a full continuous-effect LayerEngine.
- Other equipment static modifiers remain outside this slice.
- Full `百炼` breadth, owner/controller breadth, attach lifecycle breadth and card-matrix full-official status remain open.
- Frontend final validation, Chrome smoke, formal 18-step E2E and final completion audit were not run in this slice.

## 5. Verdict

4D-04I-B is accepted and its runtime / focused-test write lock is closed. Project remains **NOT READY**.
