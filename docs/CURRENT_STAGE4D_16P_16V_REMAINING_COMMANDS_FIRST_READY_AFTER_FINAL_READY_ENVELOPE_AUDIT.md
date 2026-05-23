2026-05-24 Stage 4D-16P..16V remaining commands first-ready after-final-ready envelope audit accepted.

Scope: test-only server P0/P1 closure slice in `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.

Coverage added:

- `PromptIdOnlyRevealCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlyRevealCardWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `PromptIdOnlyLegendActWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlyLegendActWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `PromptIdOnlyAssembleEquipmentWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlyAssembleEquipmentWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `PromptIdOnlyPayCostWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlyPayCostWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `PromptIdOnlyAssignCombatDamageWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlyAssignCombatDamageWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `PromptIdOnlyOrderTriggersWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlyOrderTriggersWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `PromptIdOnlyChooseHandCardsWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`
- `SnapshotOnlyChooseHandCardsWithFirstReadyPromptAfterFinalReadyRejectsWithoutMutation`

Behavior proved:

- After both players submit official decks, P1 accepts the first room `READY` prompt and P2's final `READY` starts official mulligan.
- Old first-ready `promptId`-only envelopes for `REVEAL_CARD`, `LEGEND_ACT`, `ASSEMBLE_EQUIPMENT`, `PAY_COST`, `ASSIGN_COMBAT_DAMAGE`, `ORDER_TRIGGERS` and `CHOOSE_HAND_CARDS` reject with `PROMPT_EXPIRED` / `行动窗口已过期，请按最新提示重新提交。`.
- Old first-ready `snapshotTick`-only envelopes remain tick-fresh for the current active mulligan prompt and reach each command's current legality / support / payload guard without mutation.
- Both envelope paths preserve events, state hash, tick, RNG cursor, ready ids, mulligan-completed ids, opening second-action player, hands, main decks, current mulligan prompts, prompt ids and snapshot ticks.

Snapshot-only outcomes:

- `REVEAL_CARD`: `UNSUPPORTED_CARD_BEHAVIOR` / `暂不支持该牌的待命翻开行为：missing-card`.
- `LEGEND_ACT`: `UNSUPPORTED_CARD_BEHAVIOR` / `当前传奇行动尚未由服务端开放。`.
- `ASSEMBLE_EQUIPMENT`: `UNSUPPORTED_COMMAND` / `当前装备装配路径尚未由服务端开放。`.
- `PAY_COST`: `PHASE_NOT_ALLOWED` / `当前没有服务端支付窗口可处理 PAY_COST。`.
- `ASSIGN_COMBAT_DAMAGE`: `INVALID_PAYLOAD` / `ASSIGN_COMBAT_DAMAGE 需要 battleId、battlefieldId 与非空 assignments。`.
- `ORDER_TRIGGERS`: `INVALID_PAYLOAD` / `ORDER_TRIGGERS 需要非空且不重复的 orderedTriggerIds。`.
- `CHOOSE_HAND_CARDS`: `PHASE_NOT_ALLOWED` / `当前没有服务端手牌选择窗口可处理 CHOOSE_HAND_CARDS。`.

Validation:

- Focused: `14/14`.
- Adjacent opening/shape/prompt/hash/GameHub filter: `1017/1017`.
- Backend full: `5969/5969`.

Runtime changed: no. Protocol shape changed: no. Hidden-info leakage found: no. Frontend/browser/formal E2E not run because this was a backend test-only slice.

Project remains **NOT READY**.
