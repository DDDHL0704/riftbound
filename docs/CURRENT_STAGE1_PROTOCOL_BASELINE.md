# 阶段 1 协议基线

更新日期：2026-05-09
结论：**NOT READY**

本文记录阶段 1 前端契约审计基线，只描述当前 C#/TS 已存在的真实 DTO 与 WebSocket payload。它不是正式产品 UI 的目标 schema，也不替代 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`。

## 1. 传输包络

服务端通过 SignalR hub 方法下发 `WsServerMessage<T>` 包络。JSON 字段按当前前端类型消费为 camelCase：

- `type: string`
- `roomId: string`
- `playerId: string`
- `serverTick: number`
- `payload: T`
- `protocolVersion: number`
- `schemaVersion: number`

当前 hub 消息：

- `Joined`: `payload = PlayerSessionDto`
- `Snapshot`: `payload = SnapshotDto`
- `Prompt`: `payload = ActionPromptDto`
- `Events`: `payload = GameEvent[]`
- `Error`: `payload = ErrorDto`

`READY` 命令成功但未开局时，事件包络 type 可为 `READY`；开局时可为 `START`；其他 accepted command 事件通常为 `EVENTS`。前端仍应按 hub 方法与 payload 类型消费，不应靠 type 推导规则结果。

## 2. SnapshotDto

真实 C# DTO：

```text
SnapshotDto(
  Tick: long,
  TurnNumber: int,
  ActivePlayerId: string,
  Players: IReadOnlyDictionary<string, object?>,
  Lanes: IReadOnlyDictionary<string, object?>,
  Stack: IReadOnlyList<object?>,
  Timing: IReadOnlyDictionary<string, object?>,
  TurnState: string)
```

当前 TypeScript 视图：

```text
tick: number
turnNumber: number
activePlayerId: string
players: Record<string, PlayerSnapshotView>
lanes: Record<string, unknown>
stack: unknown[]
timing: Record<string, unknown>
turnState: string
```

`players` 当前前端子视图不是独立 C# DTO，只是 snapshot 字典里的对象投影。前端类型包含：

- `PlayerSnapshotView`: `id/name/seat/ready/deckSubmitted/mulliganCompleted/handSize/score/experience/runePool/zones/objects`
- `RunePoolView`: `mana/power/totalPower/untypedPower/powerByTrait`
- `ZoneView`: `mainDeckCount/runeDeckCount/hand/handHidden/base/battlefields/graveyard/banished/legendZone/championZone`
- `CardObjectView`: `objectId/cardNo/damage/basePower/effectivePower/power/untilEndOfTurnPowerModifier/isExhausted/isFaceDown/isAttacking/isDefending/tags/untilEndOfTurnEffects/manaCost/attachedToObjectId/ownerId/controllerId/location`

`timing` 当前由服务端构造为字典，已知键包括：

- `phase`
- `timingState`
- `turnPlayerId`
- `priorityPlayerId`
- `passedPriorityPlayerIds`
- `focusPlayerId`
- `passedFocusPlayerIds`
- `winnerPlayerId`
- `destroyedUnitOwnerIdsThisTurn`
- `turnWindow`
- `spellDuel`
- `battle`
- `battleResolutions`
- `battlefieldTasks`
- `battlefieldResolutions`
- `pendingTaskQueue`
- `continuousEffects`
- `triggerQueue`
- `winningScore`
- `roomStatus`
- `readyPlayerIds`

风险：`lanes`、`stack`、`timing` 和大部分 nested view 仍是字典或 unknown 投影，适合 DevUi 联调，不是正式产品 UI 的稳定 typed contract。

## 3. ActionPromptDto

真实字段：

```text
playerId: string
actionable: boolean
reason: string
actions: string[]
promptId?: string | null
snapshotTick?: number | null
candidates?: ActionPromptCandidateDto[] | null
view?: PromptViewDto | null
```

阶段 1 事实：

- `promptId` 当前由服务端以 `roomId:tick:playerId:actions` 派生。
- `snapshotTick` 当前等于权威 state tick。
- 前端提交命令时会把当前 `promptId/snapshotTick` 附到 command 上。
- 服务端发现 prompt 戳过期时返回 `PROMPT_EXPIRED`，不进入规则引擎。
- 旧客户端不提交 prompt 戳仍会进入既有服务端合法性检查。

## 4. PromptViewDto

真实字段：

```text
type: PromptType
title: string
message: string
relatedBattlefieldId?: string | null
relatedStackItemId?: string | null
relatedBattleId?: string | null
relatedSpellDuelId?: string | null
minSelection?: number | null
maxSelection?: number | null
metadata?: Record<string, unknown> | null
```

当前 PromptType 常量：

- 已实际按 `BuildView` 发出的类型：`ROOM_SETUP`、`MULLIGAN`、`MAIN_ACTION`、`STACK_PRIORITY`、`SPELL_DUEL_FOCUS`、`BATTLE_DECLARATION`、`TASK_QUEUE`、`WAIT`、`MATCH_RESULT`
- 已在 C#/TS 预留但当前不作为正式复杂窗口发出：`SPELL_DUEL_ACTION`、`ASSIGN_COMBAT_DAMAGE`、`PAY_COST`、`ORDER_TRIGGERS`

风险：`PromptViewDto` 目前是现有 `ActionPrompt` 的兼容视图，不包含正式复杂 prompt 所需的 typed options、constraints、payload schema 或 submit command。

## 5. ActionPromptCandidateDto

真实字段：

```text
action: string
label: string
enabled: boolean
reason: string
sources?: ActionPromptChoiceDto[] | null
targets?: ActionPromptChoiceDto[] | null
destinations?: ActionPromptChoiceDto[] | null
modes?: ActionPromptChoiceDto[] | null
optionalCosts?: ActionPromptChoiceDto[] | null
metadata?: Record<string, unknown> | null
```

`ActionPromptChoiceDto` 真实字段：

```text
id: string
label: string
reason?: string | null
```

当前 candidate 以 `action` 和若干 choice 列表表达合法来源、目标、目的地、模式和费用 token。复杂来源约束主要塞在 `metadata.sourceRequirements` 中，常见 metadata 键包括：

- policy 类：`sourcePolicy`、`targetPolicy`、`destinationPolicy`、`modePolicy`、`optionalCostPolicy`
- play-card/ability 类：`sourceRequirements`、`targetChoicesByIndex`、`legalTargetSelections`、`destinationChoices`、`optionalCostChoices`
- payment 相关临时键：`paymentResourceChoices`、`paymentResourcePowerByChoice`、`availablePowerByTrait`、`availablePowerByTraitWithPaymentResources`
- battle 代表路径键：`attackerChoicesByIndex`、`battlefieldChoices`、`requiredOptionalCosts`、`multiParticipantBattlePolicy`、`samePriorityAssignmentPolicy`

风险：这些 metadata 是当前 DevUi composer 可用的临时结构，并非稳定正式 schema。正式 UI 不能把它误读成完整 `PAY_COST`、`ASSIGN_COMBAT_DAMAGE` 或 `ORDER_TRIGGERS` payload。

## 6. GameEvent

真实字段：

```text
kind: string
description: string
payload: Record<string, unknown>
```

阶段 1 事实：

- `payload` 是开放字典，没有统一 event schema。
- 前端日志可展示 `kind/description/payload`，但不能从 event payload 自行裁决胜负、费用、战斗、清理或触发排序。
- 重要状态仍应以后续权威 `SnapshotDto` 和 `ActionPromptDto` 为准。

## 7. ErrorDto

真实字段：

```text
code: string
message: string
```

当前已知错误码常量包括：

- `PLAYER_ID_REQUIRED`
- `PLAYER_NOT_IN_ROOM`
- `ROOM_FULL`
- `INVALID_RECONNECT_TOKEN`
- `CLIENT_INTENT_ID_REQUIRED`
- `CLIENT_INTENT_CONFLICT`
- `MATCH_NOT_STARTED`
- `MATCH_FINISHED`
- `UNSUPPORTED_COMMAND`
- `PHASE_NOT_ALLOWED`
- `INSUFFICIENT_COST`
- `INVALID_TARGET`
- `CARD_NOT_IN_HAND`
- `INVALID_DECK`
- `UNSUPPORTED_CARD_BEHAVIOR`
- `RECOVERY_INCONSISTENT`
- `PROMPT_EXPIRED`

风险：没有独立 `ActionError` DTO，也没有 typed error details。正式 UI 只能展示 `code/message`，细粒度非法原因仍主要来自 prompt candidate reason 或开放 payload。

## 8. 当前命令 union

当前 DevUi `GameCommand` 是带可选 prompt 戳的 union：

- `SUBMIT_DECK`
- `MULLIGAN`
- `PASS_PRIORITY`
- `PASS_FOCUS`
- `PASS`
- `END_TURN`
- `SURRENDER`
- `PLAY_CARD`
- `HIDE_CARD`
- `REVEAL_CARD`
- `TAP_RUNE`
- `RECYCLE_RUNE`
- `MOVE_UNIT`
- `ASSEMBLE_EQUIPMENT`
- `DECLARE_BATTLE`
- `ACTIVATE_ABILITY`
- `LEGEND_ACT`

阶段 1 仍不存在这些正式复杂命令：

- `PAY_COST`
- `DECLINE_PAY_COST`
- `ASSIGN_COMBAT_DAMAGE`
- `ORDER_TRIGGERS`
- `SPELL_DUEL_ACTION`

## 9. 不存在的独立 DTO

当前仓库没有独立的以下协议 DTO：

- `MatchSnapshot`
- `LegalAction`
- `RoomState`
- `GameLogEntry`
- `ActionError`

对应当前实现是：

- match snapshot 使用 `SnapshotDto`。
- legal actions 使用 `ActionPromptDto.actions` 加 `ActionPromptCandidateDto`。
- room state 混在未开局/准备阶段的 `SnapshotDto.players`、`ActionPromptDto` 与 `PlayerSessionDto` 中，没有独立 room DTO。
- game log 使用 `GameEvent[]`。
- action error 使用 `ErrorDto`。

## 10. 阶段 1 审计结论

阶段 1 契约已经足够支撑 DevUi 继续做服务端联调和安全降级展示，但距离产品级正式 UI 仍有 P0 缺口：

- 复杂 prompt 只有 `PromptType` 名称预留和 `ActionPanel` 降级展示，没有正式 payload/command。
- `Payment` 仍以 `optionalCosts`、临时 metadata 和 command 内 token 为主，没有独立 `PAY_COST` window。
- `Damage assignment` 仍混在 `DECLARE_BATTLE` 代表路径中，没有独立 `ASSIGN_COMBAT_DAMAGE` prompt。
- `Trigger ordering` 只有事件和 trigger queue 视图，没有 `ORDER_TRIGGERS` prompt。
- `battle/spellDuel` 有关联 id 与快照摘要，但完整生命周期仍未契约化。
