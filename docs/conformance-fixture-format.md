# Conformance Fixture 格式

更新时间：2026-04-28

## 1. 目的

Fixture 是 Java golden oracle 与 C# 新引擎之间的行为契约。迁移期间每个高价值场景都应导出同一份输入，并分别记录 Java 与 C# 的事件、快照和提示结果。

核心链路：

```text
seed + initial setup + command log
  -> Java oracle events/snapshots/prompts
  -> C# replay events/snapshots/prompts
  -> canonical JSON diff
```

## 2. 当前最小格式

当前 P1 runner 已支持最小字段：

```json
{
  "schemaVersion": 1,
  "fixtureId": "p1-placeholder-pass",
  "description": "human readable reason",
  "source": "java-oracle | manual-csharp-skeleton",
  "roomId": "fixture-room",
  "players": ["P1", "P2"],
  "commands": [
    {
      "playerId": "P1",
      "clientIntentId": "intent-pass-1",
      "cmd": {
        "cmdType": "PASS"
      }
    }
  ],
  "expected": {
    "finalTick": 1,
    "eventKinds": ["TURN_ENDED"],
    "promptActions": {
      "P1": ["PLAY_CARD", "ACTIVATE_ABILITY", "ASSEMBLE_EQUIPMENT", "MOVE_UNIT", "HIDE_CARD", "TAP_RUNE", "LEGEND_ACT", "PASS", "END_TURN"],
      "P2": ["WAIT"]
    }
  }
}
```

`expected.eventKinds` 表示写入事件日志的事件类型，而不是每次客户端重试返回的响应事件。重复 `clientIntentId` 必须不重复推进 tick，也不重复写入事件日志。

现有样例：

- `tests/Riftbound.ConformanceTests/Fixtures/p1-placeholder-pass.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/java-oracle/java-oracle-p1-pass.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/java-oracle/java-oracle-p1-end-turn.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/java-oracle/java-oracle-p1-duplicate-pass.fixture.json`

## 3. Java Exporter 后续必须补齐

Java oracle exporter 需要输出更完整字段：

| 字段 | 说明 |
|---|---|
| `rulesVersion` | 例如 `rules-260330`。 |
| `catalogVersion` | 例如 `official-2026-04-27`。 |
| `javaCommit` | oracle 基线 commit，例如 `75bf7cf`。 |
| `seed` | 洗牌、随机选择、随机分配的确定性 seed。 |
| `initialState` | 起手、牌库、战场、资源、特殊场面。 |
| `commands` | 玩家意图日志，必须保留原始 `cmd` JSON。 |
| `oracle.events` | Java 输出的权威事件。 |
| `oracle.snapshots.P1/P2` | Java 输出的玩家视角快照。 |
| `oracle.prompts.P1/P2` | Java 输出的行动提示。 |

## 4. Canonical JSON 规则

对比时忽略：

- 真实时间戳。
- SignalR/WebSocket connection id。
- 服务端 build id。
- 非语义 JSON 属性排序。

对比时必须保留：

- `tick` / `sequence`。
- `event kind`。
- `zone`。
- `ownerId` / `controllerId`。
- 公开与隐藏信息边界。
- `prompt` 可执行行动。
- 费用、目标、响应窗口和结算链状态。

## 5. 第一批 Java Fixture 清单

P1 先导出 10 条：

1. P1/P2 加入和视角快照。
2. 幂等重复提交。
3. 符文横置/回收。
4. EndTurn/Pass。
5. 基础单位打出。
6. 基础移动。
7. 基础战斗得分。
8. 基础法术伤害。
9. 装备装配。
10. owner/controller 边界。

只有当 C# runner 能消费 Java exporter 输出后，后续规则迁移才进入正式 conformance 节奏。

## 6. 当前导出命令

Java oracle exporter 当前位于旧项目 server 测试层：

```bash
mvn -pl server -am \
  -Dtest=OracleFixtureExportTest \
  -Dsurefire.failIfNoSpecifiedTests=false \
  -Doracle.fixture.outputDir=/Users/dinghaolin/MyProjects/riftbound-dotnet/tests/Riftbound.ConformanceTests/Fixtures/java-oracle \
  test
```

当前已导出：

- `java-oracle-p1-pass.fixture.json`
- `java-oracle-p1-end-turn.fixture.json`
- `java-oracle-p1-duplicate-pass.fixture.json`

C# 侧当前已把 `PASS`、`END_TURN`、重复 `PASS` 的事件日志和 prompt actions 对齐到 Java oracle。后续 fixture 只有在对应规则迁移完成后，才要求占位规则骨架或真实 `RuleEngine` 与 Java oracle 行为一致。
