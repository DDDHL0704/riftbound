# Current Platform P2 Status

更新时间：2026-06-29

本文件记录平台化 P2（身份、匹配发现、战绩资料、部署运维）进度。规则证据仍以 `docs/rules-evidence-index.md`、`docs/p2-rules-preflight.md` 和官方资料为准；本文件不声明任何卡牌或游戏规则行为。

## Snapshot

- P2-A 轻量身份绑定：已有 `PlayerIdentityService`、Hub `Authenticate(handle, key)`、JoinRoom/Reconnect/命令身份一致性校验、内存/Postgres 身份存储。
- P2-B 匹配与发现：B1 快速匹配队列已完成后端最小闭环。新增 `IMatchmakingQueue` / `InMemoryMatchmakingQueue`，Hub 暴露 `EnqueueMatchmaking(playerId)` 与 `CancelMatchmaking(playerId)`；入队必须先认证，冒充其他 handle 会拒绝；两名等待玩家会由服务端生成 `RB-XXXXXX` 房间、分配座位，并向每个玩家自己的匹配组推送 `MATCHMAKING` 消息，payload 只包含该玩家自己的 `PlayerSessionDto`。B2 公开对局发现已完成后端最小闭环，Hub 可创建公开房、列出公开等待房，HTTP `GET /matches` 返回同一目录，第二名玩家加入后公开等待项移除。B3 Dev UI 大厅已接入快速匹配、取消匹配、公开等候和公开房列表加入；匹配/公开房返回的 `PlayerSessionDto` 会写入本地 session，房间页可继续走服务端快照/提示。B4 新增可复用双客户端快速匹配到结算 E2E，覆盖认证、快速匹配同房、预构筑提交、READY、MULLIGAN、投降结算与对手手牌隐藏计数。
- P2-B 剩余：无。
- P2-C 战绩/资料/排行：C1 对局结果记录已完成后端最小闭环。Hub 在 accepted `MATCH_WON` 后记录公开终局结果；结果只包含 roomId、双方 handle/seat/score/win 标记、winner 与 finishedAt，不包含隐藏区或完整快照。无连接串时使用内存结果 store，有连接串时使用 Postgres 结果 store 与幂等迁移。C2 后端资料/历史 API 已完成最小闭环，`GET /players/{handle}` 返回总场次、胜负与胜率，`GET /players/{handle}/matches` 返回最近公开终局记录。
- P2-D 部署/运维：未开始。

## Evidence Log

- 2026-06-29 B1 backend slice:
  - Runtime: `src/Riftbound.Engine/Matchmaking.cs` 新增 FIFO 内存匹配队列；`src/Riftbound.Api/Hubs/GameHub.cs` 新增认证匹配 Hub 方法与 per-player 推送；`src/Riftbound.Contracts/Protocol.cs` 新增 `MATCHMAKING`、`AUTHENTICATION_REQUIRED` 和 `MatchmakingStatusDto`；`src/Riftbound.Api/Program.cs` 注册队列。
  - Tests: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs` 覆盖未认证入队拒绝、认证冒充入队拒绝、两名认证玩家自动匹配到同一房间、取消等待后下一名玩家不会误配。
  - Verification: baseline before changes `~/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj` passed `8971/8971`; focused B1 `~/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` passed `231/231`; post-change full conformance passed `8975/8975`; `git diff --check` passed.
- 2026-06-29 B2 backend slice:
  - Runtime: `src/Riftbound.Engine/PublicMatchDirectory.cs` 新增公开等待房目录；`RoomCodeGenerator` 统一服务端房间码生成；Hub 新增 `CreatePublicMatch(playerId)` / `ListPublicMatches()`，并在 `JoinRoom` 成功加入公开房时移除公开等待项；`src/Riftbound.Api/Program.cs` 暴露 `GET /matches`。
  - Tests: `GameHubJoinTests` 覆盖创建公开房必须认证、公开房创建后 host 入座并出现在目录/Hub 列表、第二名玩家加入后公开列表清空。
  - Verification: focused B2 `~/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` passed `234/234`; full conformance in the current worktree passed `8982/8982`; `git diff --check` passed.
- 2026-06-29 B3 frontend slice:
  - Runtime: `src/Riftbound.DevUi/src/pages/LobbyPage.tsx` 接入快速匹配、取消匹配、公开等候和公开房列表；`src/Riftbound.DevUi/src/services/matchSocket.ts` / `apiClient.ts` / `types/protocol.ts` 增加匹配发现协议；`useMatchController.ts` 导出 session 写入 helper；`globals.css` 增加大厅匹配发现布局。
  - Verification: `npm --prefix src/Riftbound.DevUi run build` passed. Browser QA at `http://127.0.0.1:5173/lobby` with API `http://127.0.0.1:5088`: page identity/DOM/console passed, quick match entered queued state, cancel returned idle, public room `RB-B8AJG9` created and navigated to room, returning to lobby listed that public room, list row join navigated back to the room, mobile viewport `390x844` rendered the new controls in a single column without overlap.
- 2026-06-29 B4 E2E slice:
  - Runtime: `src/Riftbound.DevUi/scripts/check-matchmaking-result-e2e.mjs` 新增双 SignalR 客户端验证脚本；`package.json` 新增 `npm --prefix src/Riftbound.DevUi run e2e:matchmaking-result`，并显式声明 Node 20 WebSocket 构造器依赖 `ws`。脚本要求本地 API 可用，但不加入普通 `build` 门禁。
  - Verification: `npm --prefix src/Riftbound.DevUi run e2e:matchmaking-result` passed against API `http://127.0.0.1:5088`: 两名唯一 handle 认证成功，经 `EnqueueMatchmaking` 匹配到同一房间 `RB-CLD4SN`，各自用匹配 payload 的 reconnect token 重连房间组，从 `/decks/preconstructed` 取两套预构筑并提交，双方 READY 后进入 MULLIGAN，确认 mulligan 后进入 MAIN，检查双方快照中对手手牌仅暴露 `handHidden` 计数且未泄漏 `reconnectToken`，随后第二名玩家 `SURRENDER`，收到 `MATCH_WON`，胜者为先匹配玩家。`npm --prefix src/Riftbound.DevUi run build` passed；`~/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj` passed `8989/8989`；`git diff --check` passed。
- 2026-06-29 C1 result persistence slice:
  - Runtime: `src/Riftbound.Engine/MatchResults.cs` 新增 `IMatchResultStore`、`MatchResultRecord` 与内存实现；`src/Riftbound.Persistence/PostgresMatchResultStore.cs` 与 `Sql/008_p2_match_results.sql` 新增 Postgres 存储和幂等表结构；`GameHub` 在 accepted `MATCH_WON` 后记录公开结果；`AddRiftboundPersistence` 无连接串注册内存结果 store，有连接串注册 Postgres 结果 store。
  - Tests: `GameHubJoinTests.MatchWonThroughHubRecordsPublicResultForBothPlayers` 覆盖 Hub 终局记录双方结果和按玩家可查询；`PersistenceWiringTests` 覆盖内存/Postgres DI 注册。Focused verification `~/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~PersistenceWiringTests"` passed `237/237`; full conformance `~/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj` passed `8990/8990`; `git diff --check` passed; precise `Is*CardNo` whitelist query excluding generic source-card checks returned no matches.
- 2026-06-29 C2 profile/history API slice:
  - Runtime: `src/Riftbound.Api/PlayerProfileEndpoints.cs` 新增玩家资料/最近对局 handler；`src/Riftbound.Contracts/Protocol.cs` 新增 `PlayerProfileDto` / `PlayerMatchDto`；`IMatchResultStore` 增加玩家统计查询，内存与 Postgres store 均实现；`Program.cs` 映射 `GET /players/{handle}` 与 `GET /players/{handle}/matches`。
  - Tests: `PlayerProfileEndpointTests.PlayerProfileEndpointsReturnStatsAndRecentPublicMatches` 覆盖同一玩家多局聚合胜负/胜率、最近对局倒序、结果 payload 仅含公开终局字段。Focused verification `~/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayerProfileEndpointTests"` passed `1/1`; C1+C2 focused regression passed `238/238`; full conformance passed `8991/8991`; `git diff --check` passed; precise `Is*CardNo` whitelist query excluding generic source-card checks returned no matches.
