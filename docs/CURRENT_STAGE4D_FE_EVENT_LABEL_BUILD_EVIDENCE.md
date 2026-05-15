# Stage 4D Frontend Event Label Build Evidence

日期：2026-05-16
结论：**VALIDATED / PROJECT NOT READY**

## Commands

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run build
```

Initial result:

- Failed in `check:event-labels`.
- Missing labels: `ABILITY_NO_EFFECT`, `ABILITY_RESOLVED`, `BATTLEFIELD_REPLACEMENT_APPLIED`, `BATTLE_DAMAGE_ASSIGNMENT_OPENED`, `BATTLE_RESPONSE_PRIORITY_CLOSED`, `BATTLE_RESPONSE_PRIORITY_OPENED`, `EQUIPMENT_CONTROL_CHANGED`, `EQUIPMENT_CONTROL_RETURNED`, `EQUIPMENT_EXHAUSTED`, `EQUIPMENT_REATTACHED`, `TEMPORARY_PAYMENT_RESOURCE_CLEARED`, `TEMPORARY_PAYMENT_RESOURCE_SPENT`.
- Worktree after failed build remained clean except expected `?? riftbound-dotnet.sln`.

Final result after adding labels:

- `EventLog labels cover 132 backend event kinds.`
- `User-facing fallback text check passed.`
- `tsc -b`: passed as part of `npm run build`.
- `vite build`: passed, 1803 modules transformed.
- Existing warning only: SignalR/Rollup PURE annotation comment warning.

Closeout recheck:

```sh
source ../../scripts/dev-env.sh && npm run check:event-labels && npm run check:user-facing-text
```

- `EventLog labels cover 132 backend event kinds.`
- `User-facing fallback text check passed.`

## Files

- `src/Riftbound.DevUi/src/components/match/EventLog.tsx`

## Non-Closure

This evidence does not close P0/P1, Chrome smoke, formal 18-step, full-card matrix, full official PaymentEngine / LayerEngine, or READY. It only proves the current code state passes the frontend build / typecheck / user-facing-text gate after the event-label map refresh.
