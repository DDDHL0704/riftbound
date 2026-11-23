# ADR 0007 · 对局中双方同意回退

玩家自建房间（好友房、练习房、现开房）启用回退，快速匹配房间关闭。房间资格由服务端在创建时确定并持久化。已有胜者或处于局间时不能发起或作答。三局两胜的每个目标都属于当前单局。

## SignalR 协议

通过 `SubmitIntent(roomId, playerId, clientIntentId, cmd)` 提交，`cmdType` 与候选 `action` 使用同一名称。

| cmdType / action | 命令参数 | 操作者 |
| --- | --- | --- |
| `REQUEST_REWIND` | `targetStep` | 任一座位，且当前没有请求 |
| `ACCEPT_REWIND` | `requestStep` | 对手座位 |
| `REJECT_REWIND` | `requestStep` | 对手座位 |
| `CANCEL_REWIND` | `requestStep` | 发起座位 |

- `snapshot.rewindAllowed` 是房间资格。启用房间只在进行中的单局提供 `snapshot.rewindTargets`，这是按步骤升序排列的列表，每项包含 `step`、`gameNumber`、`turnNumber`、`playerId`、`description`。步骤是该命令首个事件的房间序号，使用 JSON 整数。开局命令的回退目标是开局前最后一次准备窗口。双方可以选择自己或对手的步骤，也可以选择本局先前回退过的步骤。
- `description` 是供前端直接展示的中文文本，按该步发生时的座位视角生成。已公开的对象可用牌名，例如“打出「冥想」”“移动「亚索」到「战场名」”“发动「疾风剑豪」的能力”。批量横置显示“横置 2 枚符文”。背面待命、洞察回收与私下选牌对对手只显示动作名称，操作者可以看到自己当时已知的牌名。后续公开不会补全私下操作的描述。
- 每次成功回退只产生一个可选目标，使用同意命令的 `step`，描述为“撤销回到第 N 回合的回退”。选中它并获对手同意后，恢复到上次回退生效前的规则状态。发起、拒绝和撤回仍记入日志。
- `snapshot.rewindRequest` 在请求待处理时包含 `requestStep`、`requesterPlayerId`、`targetStep`。其中 `requestStep` 是请求命令的步骤。
- `prompt.rewindCandidates` 包含独立于规则提示的回退动作候选。每项有 `action`，作答和撤回候选另有 `requestStep`。候选在任意规则窗口都可出现，独立于原有的 `prompt.actionable` 和 `prompt.candidates`。发送回退命令时可以沿用当前的 `promptId`、`snapshotTick`。
- `REWIND_REQUESTED`、`REWIND_REJECTED`、`REWIND_CANCELLED` 和 `MATCH_REWOUND` 是公开事件。事件 payload 包含 `requestStep`、`requesterPlayerId`、`targetStep`，其中 `MATCH_REWOUND` 还包含目标的 `gameNumber` 和 `turnNumber`。另一个游戏命令被接受时，请求自动作废，随该命令推送的快照清掉请求，双方可以继续当前的规则窗口。
- 人机练习由前端为电脑座位提交 `ACCEPT_REWIND`。回退后的快照和提示照常按座位推送。

## 状态与日志

同意命令追加到日志。规则状态原样恢复，包含内部规则刻度、随机种子、游标、牌序和未完成窗口。对外的 `snapshot.tick`、`prompt.snapshotTick` 和 `serverTick` 继续递增，已发出的 `promptId` 全部过期。回退请求属于房间交互状态，同意后清空。恢复时从完整命令日志重建可回退步骤和待处理请求，当前规则状态的历史校验沿实际保留的分支进行。
