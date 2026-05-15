# Stage 4D Frontend Chrome Smoke Audit

日期：2026-05-16
结论：**CHROME SMOKE FRESH-RUN PASSED / PROJECT NOT READY**

本批是 A 主控在 4D-FE event-label build gate 之后，对当前代码状态执行的 Chrome smoke fresh-run。它只验证 DevUi core routes、API / Vite / Chrome 编排和 smoke-covered 页面加载，不修改 runtime、前端代码、测试脚本、card matrix JSON、`fullOfficial` 或 READY 状态。

## 1. Trigger

Fresh smoke command:

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api
```

The script started:

- API: `http://127.0.0.1:5088`
- Vite preview: `http://127.0.0.1:5173/`
- Chrome DevTools endpoint: `ws://127.0.0.1:9338/devtools/browser/...`

## 2. Covered Routes

The smoke script reported `Chrome smoke OK` for:

- `/`
- `/lobby`
- `/decks`
- `/cards`
- `/rooms/stage3-smoke`
- `/matches/stage3-smoke`
- `/matches/stage3-smoke/result`

Final line:

- `Chrome smoke passed.`

Some catalog requests reported `499` while the smoke runner was navigating or shutting down. The run still exited with code 0 and the script reported pass.

## 3. Scope And Non-Closure

This batch did not touch:

- server runtime / rules / protocols
- frontend runtime source
- Chrome smoke or formal E2E scripts
- card matrix JSON
- `fullOfficial` / READY
- `riftbound-dotnet.sln`

This is current-code Chrome smoke evidence only. It does not close P0/P1, formal 18-step, full-card matrix, full official PaymentEngine / LayerEngine, or final READY.
