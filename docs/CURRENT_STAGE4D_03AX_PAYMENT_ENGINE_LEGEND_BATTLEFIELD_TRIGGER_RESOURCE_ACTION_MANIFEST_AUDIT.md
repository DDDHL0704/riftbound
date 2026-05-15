# Stage 4D-03AX PaymentEngine Legend / Battlefield / Trigger Resource Action Manifest Audit

日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

本切片新增 server-side conformance verifier，把 4D-03AV 中 `LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTIONS` residual family 拆成更细的 executable manifest。它不修改 runtime、不修改前端、不修改 card matrix、不升级 fullOfficial，也不关闭 P0-005、P1 或 READY。

## Scope

写入范围：

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_EVIDENCE.md`
- A-side checkpoint / completion / server-rule / closure-plan / dispatch docs

No-go：

- 不改 `src/Riftbound.Engine` runtime。
- 不改 `ActionPrompt` contracts、API、前端、Chrome smoke、formal E2E 或 card matrix JSON。
- 不触碰 `riftbound-dotnet.sln`。
- 不把 verifier 通过解释为 full official legend / battlefield / trigger PaymentEngine closure。

## Manifest Policy

`PaymentEngineLegendBattlefieldTriggerResourceActionManifest*` tests 精确锁定 3 个 action windows：

| Window | Representative scope | Residual |
| --- | --- | --- |
| `LEGEND_ACT` | Legend active / reaction representatives，且 playable path 保持为 `LEGEND_ACT` | 完整 LEGEND_ACT resource-action breadth、recycle / temporary quote parity、timing 与 target dependency branches 未关闭 |
| `BATTLEFIELD_HELD_SCORE_PAYMENT` | Battlefield held pay-4-power score 的 generic / typed / recycle / temporary / mixed / prevention representatives | 完整 battlefield skill breadth、replacement ordering、score-prevention variants 与 cross-window resource generation 未关闭 |
| `TRIGGER_PAYMENT` | Pending trigger payment prompt、pay / decline、typed-yellow SFD Fiora、recycle / temporary / mixed resources 与 stale guards | 完整 trigger payment resource family、multi-trigger ordering、replacement ordering 与 cross-window resource generation 未关闭 |

## Guardrails

- Manifest windows 必须 exactly once，并且必须回连 `PaymentEngineActionWindowCoverageManifest` 中的 representative-covered action-window entries。
- 每个 entry 必须保留 prompt、command、audit、no-mutation rollback anchors。
- Manifest 必须继续回连 4D-03AV residual blocker family `LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTIONS`。
- Remaining official breadth 必须显式保留 `resource-action`、battlefield、trigger、replacement ordering 与 cross-window resource generation。
- Manifest 必须继续写明 `NOT READY` 与 `P0-005 remains open`。
- Manifest 不得出现 `FullOfficialRulePass`、`fullOfficial=true` 或 READY closure。

## Validation

- Focused PaymentEngine coverage guard: **27 / 27 passed**.
- Adjacent LegendAct / BattlefieldHeld / TriggerPayment / PaymentEngine / prompt / hub regression: **408 / 408 passed**.
- Backend full: **4464 / 4464 passed**.
- `git diff --check`: passed.

## Verdict

4D-03AX 把 4D-03AV 中的 `LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTIONS` residual family 进一步拆成 executable window-level manifest。它改善后续 routing 与回归可见性，但不证明完整 legend resource action family、完整 battlefield skill family、完整 trigger payment resource family、replacement ordering、cross-window resource generation、P0/P1、frontend final validation、card matrix full-official coverage 或 READY。项目仍 **NOT READY**。
