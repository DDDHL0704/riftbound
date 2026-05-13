# Stage 4D-03J PaymentEngine Resource Skill Lifecycle Audit

日期：2026-05-14
结论：**4D-03J FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03J 的 Malzahar resource skill lifecycle focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_HANDOFF.md` 的最小推进要求，把 `OGN·113/298` 玛尔扎哈 `[A A]` resource skill 从 4D-03I 的 open-main representative 扩展到 spell-duel focus timing，并将 generated power 从无约束 `RunePool.Power` 改为可审计 temporary payment-only resource ledger；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
  - `src/Riftbound.Engine/PaymentCostRules.cs`
  - `src/Riftbound.Engine/MatchSession.cs`
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/MalzaharResourceSkillTests.cs`
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_EVIDENCE.md`
- Focused regression：116/116 passed
- Adjacent regression：340/340 passed
- Backend full：3847/3847 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `MatchSession` now permits `ACTIVATE_ABILITY` in spell-duel focus prompts only when `TimingState=SPELL_DUEL_OPEN` and `FocusPlayerId` is the viewing player. Non-focus players, pending payment / hand-choice windows, blocking task queues, ordinary stack priority and closed timing do not expose Malzahar resource skill.
- `CoreRuleEngine.ResolveMalzaharResourceSkill` accepts both open-main and spell-duel focus contexts. It still validates visible controlled Malzahar source, no optional costs, exactly one friendly visible unit/equipment cost target, and rejected invalid timing no-mutation.
- Successful spell-duel focus resolution exhausts Malzahar, destroys the friendly cost object, leaves `TimingState=SPELL_DUEL_OPEN`, preserves focus, clears priority, creates no ordinary stack item and emits no `STACK_ITEM_ADDED`.
- Generated `A A` is now represented by `TemporaryPaymentResourceState` with owner, source, ability, payment window, generated/remaining power, allowed payment kind and created tick. It is exposed only to the owner/spectator snapshot and as a pending `PAY_COST` payment resource action.
- Pending `PAY_COST` can consume the temporary resource only for generic rune power cost windows. It rejects mana-only windows, typed-power shortfalls and unnecessary temporary resource use, emits spent/cleared audit events, closes the payment window and removes the ledger entry.
- End-turn / turn-start rune-pool reset also clears temporary payment resources so the ledger does not persist indefinitely.
- B worker Raman produced the first implementation draft and paused on A request; A reviewed, fixed stale `PaymentEngineUnificationTests` expectations, tightened temporary resource cleanup, reran required tests and completed this audit. Raman did not commit.

## 3. Remaining No-Ready Items

- P0-005 remains open: this slice covers one Malzahar `[A A]` representative, not the complete `[A]` / `[C]` resource skill family.
- Temporary resource consumption is wired to pending `PAY_COST` windows; inline `PLAY_CARD` / `ACTIVATE_ABILITY` payment commits and broader prompt quote parity still need future full PaymentEngine work.
- Reaction prohibition is represented by immediate resolution with no ordinary stack item and no reaction target exposure in the covered prompts; a complete reaction / counter target-filter model for all resource skills is still future work.
- P0-002 / P0-003 / P0-004 full-official lifecycle residuals, P1 LayerEngine / keyword pass, 1009/811 matrix completion, final Chrome / hidden-info / replay-hash audit and completion audit remain open.

## 4. Next Step

Continue P0-005 breadth with the next PaymentEngine slice. Highest-value remaining candidates are full `[A]` / `[C]` resource skill family expansion, `LEGEND_ACT` resource action parity, inline payment-window temporary resource consumption, and broader replacement / optional / extra / typed payment quote parity.
