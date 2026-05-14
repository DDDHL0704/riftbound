# Stage 4D Frontend Final Validation Preflight

日期：2026-05-15
结论：**PREFLIGHT ONLY / FRESH RUN NOT STARTED / PROJECT NOT READY**

本文件是 A/C 侧对最终 frontend build、Chrome smoke 与 formal 18-step E2E 的 fresh-run 门槛整理。它不修改前端代码，不启动 API / Vite / Chrome，不把历史通过记录升级为最终 READY evidence。

## 1. Sources Checked

- `docs/A_MASTER_AGENT_GOAL.md` 要求前端只展示并提交服务端 `ActionPrompt` / authoritative snapshot 支持的合法操作，并在 READY 前通过 Chrome smoke 与正式 18 步 E2E。
- `src/Riftbound.DevUi/package.json` 当前脚本：
  - `npm run build`: `check:event-labels` + `check:user-facing-text` + `tsc -b` + `vite build`
  - `npm run smoke:chrome`: `node scripts/chrome-smoke.mjs`
  - `npm run e2e:formal-18`: `node scripts/chrome-formal-18-e2e.mjs`
- `docs/CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md` 记录 2026-05-13 historical pass：build、Chrome smoke、formal 18-step all passed, but explicitly not a substitute for P0/P1 closure, full-card matrix, full PaymentEngine / LayerEngine or final READY.
- `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md` already marks frontend build and Chrome smoke as historical pass that must be fresh-run in final code state.

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

- C remains read-only while 4D-03AM-B may alter server `ActionPrompt` or event payload shape.
- If final fresh-run finds frontend issues, A must open a separate C write window with exact files, commands and no-go boundaries.
- Frontend must not locally infer Azir targets, green payment, once-per-turn state, position swap or optional armament movement. Those must come from server prompt / authoritative snapshot / event payload.

## 6. Current Verdict

Frontend final validation is ready to run later, but not yet run for the final code state. Historical build / smoke / formal 18-step evidence remains historical only. Project remains **NOT READY**.
