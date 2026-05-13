# Stage 4D-02 Spell Duel And Battle State Machine Audit

日期：2026-05-13
结论：**4D-02 FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-02 的第一片 spell duel / battle task-scoped lifecycle 证据。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_HANDOFF.md` 的 focused checklist，可以继续进入 4D-03 PaymentEngine；但 P0-004 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/MatchSession.cs`
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
- 新增测试：
  - `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_EVIDENCE.md`
- Focused new tests：6/6 passed
- Focused handoff regression：35/35 passed
- Adjacent regression：127/127 passed
- Backend full：3786/3786 passed

## 2. A Review Notes

- `MatchSession` 现在会为 active spell duel 选择 deterministic first unfinished contested battlefield，并把 focus / stack metadata 只挂在该 active `START_SPELL_DUEL` task 上；多争夺战场不再同时显示多个 active spell duel。
- `CoreRuleEngine.ResolvePassFocus` 在 spell duel close 后执行 cleanup；若 cleanup 使 matching `START_BATTLE` 消失，会继续推进下一个 pending battlefield task。
- 新增测试覆盖多争夺战场 one-active ordering、非焦点 / 错时机 `PASS_FOCUS` no-mutation、spell-duel stack 回到同一 active task、cleanup 移除参与者后推进下一 task，以及 `SPELL_DUEL_TASKS` / `BATTLE_TASKS` reconnect metadata + redaction。
- 本切片未修改前端、PaymentEngine、卡牌矩阵或 READY 结论；未触碰未跟踪的 `riftbound-dotnet.sln`。

## 3. Remaining No-Ready Items

- P0-004：完整官方 battle lifecycle 仍缺 battle response window、完整 initial stack、完整 swift/reaction 链矩阵、所有 no-result / replacement / prevention / cleanup 组合和全官方 damage assignment breadth。
- P0-002 / P0-003：完整 held/conquer/control lifecycle、control freeze/release、所有进出战场路径和替代/预防效果仍未 full-official 收口。
- P0-005：完整 PaymentEngine / reaction payment windows 仍未统一。
- P1：LayerEngine、关键词 full-pass、1009/811 full-official matrix、最终 Chrome / hidden-info / replay-hash audit 仍未收口。

## 4. Next Step

下一实现切片应进入 `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md` 的 4D-03：Payment Engine Unification。4D-02 的结果可作为 spell duel / battle task-scoped regression foundation，但不得把本切片外推为完整官方 battle lifecycle。
