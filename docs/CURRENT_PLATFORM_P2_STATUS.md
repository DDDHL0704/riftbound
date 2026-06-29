# Current Platform P2 Status

更新时间：2026-06-29

本文件记录平台化 P2（身份、匹配发现、战绩资料、部署运维）进度。规则证据仍以 `docs/rules-evidence-index.md`、`docs/p2-rules-preflight.md` 和官方资料为准；本文件不声明任何卡牌或游戏规则行为。

## Snapshot

- P2-A 轻量身份绑定：已有 `PlayerIdentityService`、Hub `Authenticate(handle, key)`、JoinRoom/Reconnect/命令身份一致性校验、内存/Postgres 身份存储。
- P2-B 匹配与发现：B1 快速匹配队列已完成后端最小闭环。新增 `IMatchmakingQueue` / `InMemoryMatchmakingQueue`，Hub 暴露 `EnqueueMatchmaking(playerId)` 与 `CancelMatchmaking(playerId)`；入队必须先认证，冒充其他 handle 会拒绝；两名等待玩家会由服务端生成 `RB-XXXXXX` 房间、分配座位，并向每个玩家自己的匹配组推送 `MATCHMAKING` 消息，payload 只包含该玩家自己的 `PlayerSessionDto`。B2 公开对局发现已完成后端最小闭环，Hub 可创建公开房、列出公开等待房，HTTP `GET /matches` 返回同一目录，第二名玩家加入后公开等待项移除。
- P2-B 剩余：B3 Dev UI 快速匹配/公开列表、B4 双客户端 E2E 尚未完成。
- P2-C 战绩/资料/排行：未开始。
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
