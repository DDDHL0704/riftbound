# Stage 4D Frontend Final Validation Preflight

日期：2026-05-16
结论：**BUILD / CHROME SMOKE / FORMAL 18 FRESH-RUN PASSED / PROJECT NOT READY**

本文件是 A/C 侧对最终 frontend build、Chrome smoke 与 formal 18-step E2E 的 fresh-run 门槛整理。它不把历史通过记录升级为最终 READY evidence。

2026-05-16 更新：A 主控已对当前代码状态执行 frontend build fresh-run。首次 `npm run build` 在 `check:event-labels` 发现 12 个后端事件 kind 缺少中文标题；本批只补 `src/Riftbound.DevUi/src/components/match/EventLog.tsx` 的 `eventKindLabels`，复跑 build 已通过。

2026-05-16 更新：A 主控已对当前代码状态执行 Chrome smoke fresh-run，`npm run smoke:chrome -- --start-api` 通过并覆盖 core routes。

2026-05-16 更新：A 主控已对当前代码状态执行 formal 18-step fresh-run，`npm run e2e:formal-18 -- --start-api` 通过，房间 `formal-18-1778886172096-1`，18/18 steps all OK。Build + smoke + formal 18 仍不能升级为 READY evidence，因为 P0/P1 与 full-card matrix 未关闭。

## 1. Sources Checked

- `docs/A_MASTER_AGENT_GOAL.md` 要求前端只展示并提交服务端 `ActionPrompt` / authoritative snapshot 支持的合法操作，并在 READY 前通过 Chrome smoke 与正式 18 步 E2E。
- `src/Riftbound.DevUi/package.json` 当前脚本：
  - `npm run build`: `check:event-labels` + `check:user-facing-text` + `tsc -b` + `vite build`
  - `npm run smoke:chrome`: `node scripts/chrome-smoke.mjs`
  - `npm run e2e:formal-18`: `node scripts/chrome-formal-18-e2e.mjs`
- `docs/CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md` records the 2026-05-16 current-code formal 18-step fresh-run pass and keeps the warning that it is not a substitute for P0/P1 closure, full-card matrix, full PaymentEngine / LayerEngine or final READY.
- `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md` now marks frontend build, Chrome smoke and formal 18-step as current-code fresh passes for this batch.
- `docs/CURRENT_STAGE4D_FE_EVENT_LABEL_BUILD_AUDIT.md` and `docs/CURRENT_STAGE4D_FE_EVENT_LABEL_BUILD_EVIDENCE.md` record the build-gate failure, label-only fix and final build pass.
- `docs/CURRENT_STAGE4D_FE_CHROME_SMOKE_AUDIT.md` and `docs/CURRENT_STAGE4D_FE_CHROME_SMOKE_EVIDENCE.md` record the current-code Chrome smoke pass.
- `docs/CURRENT_STAGE4D_FE_FORMAL_18_FRESH_RUN_AUDIT.md` and `docs/CURRENT_STAGE4D_FE_FORMAL_18_FRESH_RUN_EVIDENCE.md` record the current-code formal 18-step pass.

## 2. Fresh-Run Commands

Final validation must run from the final code state after P0/P1 and relevant server prompt / snapshot contracts are stable:

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run build
```

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api
```

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run e2e:formal-18 -- --start-api
```

Optional syntax preflight for the formal script, useful before the full Chrome run:

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && node --check scripts/chrome-formal-18-e2e.mjs
```

## 3. Acceptance Criteria

| Gate | Must prove | Not sufficient by itself |
|---|---|---|
| build | event label check, user-facing text check, TypeScript build and Vite build all pass | Does not prove server prompt correctness or 1v1 flow |
| Chrome smoke | core pages load, API / Vite / Chrome orchestration works, no runtime crash in smoke-covered pages | Does not prove formal 18-step or full rules coverage |
| formal 18-step | two player profiles, room creation/join, deck submit, ready, mulligan, first-turn play, stack pass-pass, move, reconnect, score, surrender and result page | Does not close P0/P1, full-card matrix or all battle / payment / layer interactions |
| hidden-info assertions | no raw `mainDeck`, `runeDeck`, `handHidden`, `stackItemId`, `reconnectToken` text appears in formal page body | Does not prove every future UI route or debug surface |

## 4. Proxy Signals Rejected

The following evidence is useful historical context but cannot become final READY evidence without a fresh final-state run:

- `docs/CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md` historical room `formal-18-1778623926434-15`
- prior `npm run build` pass records in completion audit
- prior `npm run smoke:chrome -- --start-api` pass records
- backend full test green status
- representative ActionPrompt / GameHub tests
- card matrix skeleton existence

## 5. Write-Lock Notes

- C remains read-only unless A opens a fresh frontend write window with exact files and validation commands. Future server `ActionPrompt` or event payload changes still require renewed C preflight before READY.
- If final fresh-run finds frontend issues, A must open a separate C write window with exact files, commands and no-go boundaries.
- Frontend must not locally infer Azir targets, green payment, once-per-turn state, position swap or optional armament movement. Those must come from server prompt / authoritative snapshot / event payload.

## 6. Current Verdict

Frontend build, Chrome smoke and formal 18-step fresh-run have passed for the current code state. P0/P1 plus full-card matrix remain open. Project remains **NOT READY**.
