# Formal 18-Step E2E Evidence

日期：2026-05-16
结论：**CURRENT-CODE FRESH-RUN PASSED / 但项目整体仍 NOT READY**

本文件记录当前 formal 18-step E2E 的可复跑证据。该证据证明双浏览器等效流程可在同一连续正式房间中完成官方卡组提交、准备、起手、首回合资源与出牌、结算链双方让过、单位移动到战场、重连恢复、下一回合战场得分、投降与结果页胜者展示。它不替代完整 P0/P1 规则清零、1009/811 full-official card matrix、完整 PaymentEngine、LayerEngine 或全战斗/争夺生命周期。

2026-05-16 fresh-run 更新：当前代码状态已执行 `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run e2e:formal-18 -- --start-api` 并通过。最新房间为 `formal-18-1778886172096-1`，active player `P1`，P1 battlefield `OGN·276/298`，P2 battlefield `OGN·290/298`。脚本 1/18 到 18/18 全部 `OK`，最终输出 `Formal 18-step E2E passed: formal-18-1778886172096-1`。详见 `docs/CURRENT_STAGE4D_FE_FORMAL_18_FRESH_RUN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_FE_FORMAL_18_FRESH_RUN_EVIDENCE.md`。

## 脚本

- `src/Riftbound.DevUi/scripts/chrome-formal-18-e2e.mjs`
- `src/Riftbound.DevUi/package.json` 新增命令：`npm run e2e:formal-18 -- --start-api`

脚本行为：

- 启动本地 API `http://127.0.0.1:5088` 与 `vite preview`。
- 使用两个独立 headless Chrome profile 模拟 P1 / P2。
- 通过 SignalR 创建正式房间并提交同一套合法标准构筑测试卡组。
- 重试房间号直到官方开局稳定满足：P1 先手、P1 战场非 `OGN·290/298`、P2 战场为 `OGN·290/298`，用于在同一连续对局中验证 P2 首回合战场得分。
- 断言页面正文不暴露 `mainDeck`、`runeDeck`、`handHidden`、`stackItemId`、`reconnectToken` 等 raw debug / hidden-info 文本。

## 18 步映射

| 步骤 | 脚本断言 |
|---|---|
| 1-2 双浏览器 / 双玩家 | P1、P2 分别用独立 Chrome profile 连接同一正式房间 |
| 3 创建 / 加入房间 | SignalR `JoinRoom` 返回 P1 / P2 session 与 reconnect token |
| 4-6 合法卡组 / ready | 双方 `SUBMIT_DECK`、`READY`，事件含 `DECK_SUBMITTED`、`MATCH_STARTED` |
| 7-8 起手调整 | 双方 `MULLIGAN` 后进入 P1 `MAIN` |
| 9-11 首回合开始 / 召符文 / 抽牌 | 事件含 `RUNES_CALLED`、`CARD_DRAWN` |
| 12 打出合法单位 | P1 按服务端 `PLAY_CARD` candidate 打出单位，事件含 `CARD_PLAYED`、`STACK_ITEM_ADDED` |
| 13 移动单位到战场 | P1 按服务端 `MOVE_UNIT.sourceRequirements` 移动单位到 P2 战场 |
| 14 结算链 / 法术对决窗口 | 页面与事件覆盖 stack window，双方 `PASS_PRIORITY` |
| 15 双方让过 | P1 / P2 均产生 `PRIORITY_PASSED` |
| 16 结算推进不分叉 | `STACK_ITEM_RESOLVED` 后 `UNIT_PLAYED_TO_BASE`，随后可移动、重连、结束回合 |
| 17 战场得分 | P2 首回合在同一连续对局中触发 `BATTLEFIELD_TRIGGER_RESOLVED` 与 `SCORE_GAINED`，P2 分数为 1 |
| 18 投降 / 胜负结算 | P2 `SURRENDER` 后 `MATCH_WON reason=SURRENDER`，双端结果页显示“胜者：P1” |

## 通过记录

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && node --check scripts/chrome-formal-18-e2e.mjs
source ../../scripts/dev-env.sh && npm run build
source ../../scripts/dev-env.sh && npm run e2e:formal-18 -- --start-api
source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api
```

结果：

- `node --check scripts/chrome-formal-18-e2e.mjs`：通过。
- `npm run build`：通过；仅保留既有 SignalR/Rollup PURE 注释 warning。
- `npm run e2e:formal-18 -- --start-api`：当前代码 fresh-run 通过，房间 `formal-18-1778886172096-1`；历史通过房间 `formal-18-1778623926434-15`。
- `npm run smoke:chrome -- --start-api`：通过，覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。

## 剩余风险

- 本脚本覆盖 A 主控 formal 18-step 的连续正式主流程，但不把现有代表性战斗/争夺实现升级为 full official battle lifecycle。
- `docs/任务补充.md` 中“触发战场争夺或战斗”的严格战斗路径仍依赖现有 seeded battle/control smoke 与后续 P0-002/P0-004 收口；本脚本本身只证明合法移动、结算链/法术对决窗口、得分与胜负能在同一正式房间连续衔接。
- 项目整体仍 **NOT READY**，不得调用 `update_goal complete`。
