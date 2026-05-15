# Stage 4D-04C Agile Equipment Direct Attach Handoff

日期：2026-05-15
结论：**B DISPATCHED / WRITELOCK OPEN / PROJECT NOT READY**

本 handoff 是 A 主控对 P1-002 equipment keyword residual 的下一枚窄实现规格。4D-04B 已经把 equipment keyword report 拆成 implemented representative assemble boundary 与 still-deferred official breadth；4D-04C 只打开印刷 `灵便` 装备从手牌打出时的 server-authoritative direct attach representative，不处理完整装备规则。

## 1. Target

下一枚 B 切片：**Agile equipment direct-play attach representative**。

目标：对印刷 `灵便` 的装备，在玩家从手牌执行 `PLAY_CARD` 时，服务端必须公开并校验一个“你控制的单位”目标；合法命令进入既有支付 / 结算链流程，结算后装备对象的 `AttachedToObjectId` 指向该目标单位，并产生可审计 attachment event。

当前固定快照中的印刷 `灵便` 装备代表面：

- `SFD·022/221` 长剑
- `SFD·056/221` 斯特拉克的挑战护手
- `SFD·064/221` 布甲
- `SFD·186/221` 旋转飞斧

## 2. Input Facts

- 4D-04B accepted: `CardEquipmentKeywordRules` 已能把 registered assemble representative boundary 标为 `implemented-representative`，同时保留 agile / tempered / weapon/static residual 为 deferred。
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-long-sword-target-rejected.fixture.json` 仍记录历史 no-target guard，并明确 `灵便` reaction attachment 暂缓。
- 当前 `ASSEMBLE_EQUIPMENT` 已覆盖多张装备从基地装配到己方单位的 representative profile。
- 现有 attachment event / `AttachedToObjectId` / unattached equipment cleanup / attached-equipment movement 语义已经由 assemble、Take Up、Azir reattach、Jax attach 相关测试覆盖。

## 3. B Write Scope

B-Implementation / Singer `019e2b7e-8eed-7803-b03a-ab9bf538171c` 当前独占 4D-04C runtime / focused-test 写锁。

允许范围：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- focused Agile equipment tests, preferably a new narrow test file under `tests/Riftbound.ConformanceTests/`
- minimal `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` update if keyword status needs to expose Agile representative coverage
- only if unavoidable, update `tests/Riftbound.ConformanceTests/Fixtures/p4-play-long-sword-target-rejected.fixture.json`

禁止触碰：

- frontend / DevUi runtime
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad equipment rewrite
- LayerEngine or continuous effect replacement
- battle lifecycle and PaymentEngine broad refactors
- Azir / Maduli / Ezreal historical focused slices
- `riftbound-dotnet.sln`

## 4. Acceptance

4D-04C may be accepted only if A verifies all of the following:

1. `ActionPrompt` / source requirements / target choices expose legal `PLAY_CARD` targets for printed Agile equipment from server data.
2. Legal target means a face-up, controlled unit in a legal public zone; missing target, enemy unit, non-unit, equipment, spell, rune, stale object, face-down object, or wrong controller rejects no-mutation.
3. Legal command preserves existing payment and stack behavior, then resolves by creating / moving the equipment object with `AttachedToObjectId` set to the selected unit.
4. Resolution emits auditable attachment evidence, such as `EQUIPMENT_ATTACHED`, with source/equipment/unit object ids.
5. Existing assemble, Take Up, Azir reattach, Maduli, Ezreal and equipment cleanup representative tests remain green.
6. Coverage/profile docs may say Agile has a direct-play representative only if reaction timing, Jax-granted Agile, ephemeral destruction, static modifiers, copy-text effects, owner/controller changes and full equipment official breadth remain explicitly deferred.
7. Project remains **NOT READY** and P1-002 remains open.

## 5. Suggested Verification

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Agile|FullyQualifiedName~EquipmentKeyword|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~P4EquipmentKeywordProfiles"
```

Full backend and hygiene:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Non-Goals

- Do not implement full reaction timing.
- Do not implement Jax hand-granted Agile breadth.
- Do not implement Tempered optional attachment.
- Do not implement weapon/static power modifiers, copy-text effects, owner/controller changes or full attach lifecycle breadth.
- Do not update card matrix `fullOfficial`.
- Do not run frontend final validation as a proxy for P1 closure.
- Do not call `update_goal complete`.

## 7. Handoff Verdict

4D-04C is dispatched as a narrow P1-002 runtime slice. It should reduce the Agile equipment residual by adding one server-authoritative direct-play attachment representative, while preserving the larger equipment / LayerEngine / full-card matrix blockers.
