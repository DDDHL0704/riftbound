# Stage 4D Frontend Event Label Build Audit

日期：2026-05-16
结论：**BUILD GATE FIXED / FRESH FRONTEND BUILD PASSED / PROJECT NOT READY**

本批是 A 主控在继续 active goal 时对当前代码状态执行 frontend build fresh-run 后发现并修复的最小前端文案门槛。它只补 `EventLog` 的玩家可见中文事件标题，不改变服务端规则权威、协议结构、前端动作提交逻辑、Chrome smoke、formal 18-step、card matrix JSON、`fullOfficial` 或 READY 状态。

## 1. Trigger

Fresh build command:

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run build
```

首次执行在 `check:event-labels` 失败，缺少以下后端事件 kind 的中文标签：

- `ABILITY_NO_EFFECT`
- `ABILITY_RESOLVED`
- `BATTLEFIELD_REPLACEMENT_APPLIED`
- `BATTLE_DAMAGE_ASSIGNMENT_OPENED`
- `BATTLE_RESPONSE_PRIORITY_CLOSED`
- `BATTLE_RESPONSE_PRIORITY_OPENED`
- `EQUIPMENT_CONTROL_CHANGED`
- `EQUIPMENT_CONTROL_RETURNED`
- `EQUIPMENT_EXHAUSTED`
- `EQUIPMENT_REATTACHED`
- `TEMPORARY_PAYMENT_RESOURCE_CLEARED`
- `TEMPORARY_PAYMENT_RESOURCE_SPENT`

## 2. Change Scope

Modified file:

- `src/Riftbound.DevUi/src/components/match/EventLog.tsx`

Allowed scope for this batch:

- Add missing entries to the existing `eventKindLabels` map.
- Keep unknown future backend events falling back to `服务端事件`.
- Preserve `redactInternalText` behavior and existing event description handling.

Forbidden and untouched scope:

- No server runtime / rules / protocol edits.
- No frontend local rule inference.
- No browser smoke script or formal E2E script edits.
- No card matrix JSON / `fullOfficial` / READY edits.
- Do not touch `riftbound-dotnet.sln`.

## 3. Acceptance

The corrected build passed:

- `check:event-labels`: passed, `EventLog labels cover 132 backend event kinds`.
- `check:user-facing-text`: passed.
- `tsc -b`: passed.
- `vite build`: passed, with only the existing SignalR/Rollup PURE comment warning.

This is current-code frontend build evidence only. Chrome smoke and formal 18-step remain historical until separately fresh-run, and project remains **NOT READY**.
