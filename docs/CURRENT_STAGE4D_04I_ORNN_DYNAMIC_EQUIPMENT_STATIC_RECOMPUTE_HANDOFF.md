# Stage 4D-04I Ornn Dynamic Equipment Static Recompute Handoff

日期：2026-05-15
结论：**HANDOFF READY / PAUSE POINT / PROJECT NOT READY**

本文件是 A 主控给后续 B-Implementation 的窄实现规格。目标是把 4D-04H 已落地的 Ornn 入场一次性 friendly-equipment static power representative，推进到“Ornn 已在公开场上后，友方公开场上装备数量变化时可被服务端权威重算”的 representative。不得把本批验收扩展为完整 LayerEngine、完整 `百炼`、所有装备静态修正、card matrix full-official 或 READY。

## 1. 输入事实

- 当前分支为 `main`，最新提交为 `c2dab522 feat: add ornn friendly equipment static power representative`。
- 当前工作树基线只允许保留未跟踪 `riftbound-dotnet.sln`；该文件不得触碰、暂存或提交。
- `SFD·085/221` / `SFD·085a/221`《奥恩》当前 registry 已设置 `AddsFriendlyFieldEquipmentCountToSourceUnitPower=true`。
- 4D-04H 当前只在 `PLAY_CARD` entry resolution 时通过 `CountControlledPublicFieldEquipmentObjects` 计算一次入场战力，并在非零加成时写 `friendlyEquipmentPowerBonus`。
- `docs/CURRENT_SERVER_RULE_AUDIT.md` 的 P1-001 / P1-002 段落明确：4D-04H 不是完整层系统重算；装备后续进出场、控制权变化、贴附状态变化、失去来源、同层时间戳与依赖重算仍 open。

## 2. Proposed B Write Scope

默认写锁：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`（仅当现有 flag 不足以表达 recompute source）
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs` 或新增 focused test file
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

可选且仅最小必要：

- `src/Riftbound.Engine/MatchSession.cs`，只在 snapshot `basePower` / `effectivePower` / `continuousEffects` 投影必须补充 static recompute metadata 时触碰。
- 最小事件 payload helper，前提是 A/B 认定客户端需要审计 event；不得以事件扩张替代 authoritative state。

禁止触碰：

- frontend runtime / DevUi stores
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad LayerEngine rewrite
- unrelated equipment keywords, PaymentEngine, battle lifecycle, hidden-info redaction
- full-card matrix status、`fullOfficial`、Stage 4B count
- `riftbound-dotnet.sln`

## 3. Required Behavior

1. Ornn 已作为公开单位在 controller field 后，友方公开场上装备数量增加时，Ornn 的 authoritative power / effectivePower 必须随之增加。
2. 友方公开场上装备离开 field、变为 face-down、失去 controller 友方关系或不再是 equipment 时，Ornn 的 authoritative power / effectivePower 必须随之下降。
3. Enemy equipment、hand / deck / graveyard equipment、face-down equipment、dirty-controller equipment、non-equipment object 不得贡献 bonus。
4. 重算必须从 Ornn base power + 当前友方公开场上装备数量得到结果，不得在多次 reconcile、重复 snapshot、end turn 或连续装备变动后产生累加漂移。
5. 若当前实现继续直接写 `CardObjectState.Power`，必须有 focused guard 证明不会 double-count 4D-04H entry-time bonus；若引入 derived-power helper，则 snapshot `basePower` / `effectivePower` / `continuousEffects` 必须仍保持服务端权威。
6. Existing Ornn entry-time tests、equipment keyword profile guards、Tempered / Jax / Akshan / Armed Assaulter representatives and `MatchStateExposesContinuousEffectPowerLayerViews` must remain green.
7. 本批不更新 frontend runtime、card matrix JSON、P1-001 / P1-002 status 或 READY。

## 4. Required Focused Tests

B should add focused tests covering at minimum:

- Ornn enters with zero friendly public field equipment, then a friendly public field equipment object is created / moved / resolved into field; Ornn recomputes from 4 to 5.
- Ornn enters with two friendly public field equipment objects, then one legal equipment leaves field or stops being friendly public equipment; Ornn recomputes from 6 to 5.
- Repeated recompute after the same state transition is idempotent and does not grow beyond base + current count.
- Enemy equipment, hand equipment, face-down equipment, dirty-controller equipment and non-equipment units remain excluded after recompute, not only during entry-time play.
- Snapshot `basePower` / `effectivePower` or equivalent authoritative projection remains consistent with the recomputed state.

## 5. Suggested A-Side Acceptance Commands

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Non-Goals

- Do not claim complete LayerEngine.
- Do not implement every equipment static modifier.
- Do not change card matrix full-official status.
- Do not run frontend final validation as a proxy for P1 closure.
- Do not erase deferred statuses to make coverage look better.
- Do not call `update_goal complete`.

## 7. Handoff Verdict

4D-04I is ready as a B implementation handoff, but no runtime write lock is open until A explicitly dispatches it. This A-side batch records the target, write scope, acceptance gate and baseline evidence only. Project remains **NOT READY**.
