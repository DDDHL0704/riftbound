# Stage 4C-17 Ironclad Vanguard Trigger Evidence

更新时间：2026-05-10
结论：**NOT READY**

## Scope

- Card: Ironclad Vanguard / 《铁甲先锋》 / `SFD·021/221`
- Frozen matrix FU: `FU-6d0971786b`
- Trigger effect kind: `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`
- Overlay: `stage4CBatch17IroncladVanguardTriggerEnqueue`

## Verified Runtime Slice

- true stack `UNIT_DESTROYED`
- `TRIGGER_QUEUED`
- single-trigger auto-stack or multi-trigger `ORDER_TRIGGERS`
- `StackItems`
- priority pass
- `TRIGGER_RESOLVED`
- `UNIT_TOKEN_CREATED x2` robots

## Guards

- face-down source: no enqueue, no prompt metadata, no tokens
- standby source: no enqueue, no prompt metadata, no tokens
- no frontend rule adjudication added

## Tests

- `RealIroncladVanguardLastBreathTriggersOrderAndCreateRobotsThroughStack`
- `RealIroncladVanguardHiddenSourcesDoNotEnqueueOrCreateRobots`
- `P79IroncladVanguardCreatesTwoRobotsWhenDestroyed updated`
- backend full: 3384/3384 passed by A
- frontend build: passed by A
- Chrome smoke: passed by A

## Coverage Impact

- `stage4C17` verified FUs: 1
- `stage4C17` verified snapshot entries: 1
- cumulative real-trigger enqueue verified FUs: 13
- cumulative state-based cleanup trigger enqueue verified FUs: 11
- full-official upgrades: 0

This closes only the Ironclad Vanguard true stack representative trigger migration. It does not close state-based cleanup coverage, full trigger engine, FAQ regression, 1009/811 full-official coverage, or formal 18-step E2E.
