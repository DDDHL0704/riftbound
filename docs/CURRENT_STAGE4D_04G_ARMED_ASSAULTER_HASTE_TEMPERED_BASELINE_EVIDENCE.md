# Stage 4D-04G Armed Assaulter Haste + Tempered Baseline Evidence

日期：2026-05-15
结论：**IMPLEMENTATION-BEFORE BASELINE RECORDED / PROJECT NOT READY**

本文件记录 4D-04G 派发前的 A-side baseline。它不是实现验收；只证明当前相邻路径绿色，并锁定下一步要补的组合缺口。

## 1. Baseline Facts

- 当前分支：`main`
- 最新提交：`4c267bca feat: add akshan equipment steal representative`
- 工作树基线：只保留未跟踪 `riftbound-dotnet.sln`，不得触碰。
- `SFD·002/221`《武装强袭者》已有 HASTE_READY representative：`p4-play-armed-assaulter-haste-ready.fixture.json`。
- `SFD·002/221` 同时带 `急速|百炼`，但现有 fixture 明确 `百炼装配和武装贴附路径暂缓`。
- 4D-04D / 4D-04E 已证明 `TEMPERED_ATTACH:<equipmentObjectId>` 的 command-time validate / stack-time revalidate / stale no-effect shape。
- 当前缺口：同一个 Armed Assaulter `PLAY_CARD` 不能被验收为同时支付 HASTE_READY 与选择 `TEMPERED_ATTACH:*` 的组合 representative。

## 2. Commands

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForArmedAssaulter|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **32/32 passed**.

## 3. Interpretation

- Existing Armed Assaulter HASTE_READY path is green before 4D-04G.
- Existing Sentinel / Jax Tempered attach representatives remain green before 4D-04G.
- Existing equipment keyword profile / coverage report guards remain green before 4D-04G.
- This evidence does not close P1-002. It only establishes the safe baseline for adding one combination representative.

## 4. Not Covered

- full printed `百炼` breadth beyond current Sentinel / Jax / Akshan representatives and the proposed Armed Assaulter combination representative
- Ornn static equipment modifiers
- full HASTE_READY official breadth
- owner/controller breadth
- full attach lifecycle breadth
- LayerEngine
- frontend final validation
- card matrix full-official coverage
- READY / active goal completion
