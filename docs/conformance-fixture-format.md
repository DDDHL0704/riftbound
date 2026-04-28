# Conformance Fixture 格式

更新时间：2026-04-28

## 1. 目的

Fixture 是规则依据、旧 Java 行为样本与 C# 新引擎之间的行为契约。迁移期间每个高价值场景都应导出同一份输入，并分别记录 PDF/FAQ 裁决、Java legacy oracle 输出与 C# 的事件、快照和提示结果。

核心链路：

```text
seed + initial setup + command log
  -> rules evidence from 5 PDF/FAQ docs + official card text
  -> Java legacy oracle events/snapshots/prompts
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
  "auditStatus": "NEEDS_RULE_AUDIT | RULE_AUDITED",
  "faqVersion": "optional FAQ file/date summary",
  "rulesEvidence": [
    {
      "source": "《符文战场》核心规则_260330.pdf",
      "locator": "page/chapter TBD",
      "note": "short non-verbatim summary"
    }
  ],
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

## 3. Fixture 后续必须补齐

Fixture 需要输出更完整字段：

| 字段 | 说明 |
|---|---|
| `rulesVersion` | 例如 `rules-260330`。 |
| `faqVersion` | 涉及 FAQ 时记录文件名或日期。 |
| `catalogVersion` | 例如 `official-2026-04-27`。 |
| `javaCommit` | oracle 基线 commit，例如 `75bf7cf`。 |
| `rulesEvidence` | PDF/FAQ 文件名、页码或章节、非原文摘要。 |
| `seed` | 洗牌、随机选择、随机分配的确定性 seed。 |
| `initialState` | 起手、牌库、战场、资源、特殊场面。 |
| `commands` | 玩家意图日志，必须保留原始 `cmd` JSON。 |
| `legacyOracle.events` | 旧 Java 输出的事件，仅作历史对照。 |
| `legacyOracle.snapshots.P1/P2` | 旧 Java 输出的玩家视角快照，仅作历史对照。 |
| `legacyOracle.prompts.P1/P2` | 旧 Java 输出的行动提示，仅作历史对照。 |
| `expected` | 按五份 PDF/FAQ 与官网卡面裁决后的 C# 期望结果。 |

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

只有当 C# runner 能消费 Java exporter 输出，并且 fixture 已补齐 PDF/FAQ 规则依据后，后续规则迁移才进入正式 conformance 节奏。

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

C# 侧当前已把 `PASS`、`END_TURN`、重复 `PASS` 的事件日志和 prompt actions 对齐到旧 Java 行为。`ConformanceFixture` 已能读取可选 `rulesEvidence`、`faqVersion`、`auditStatus`；现有 3 条 legacy fixture 已补初始 evidence，但仍标记为 `NEEDS_RULE_AUDIT`。若 FAQ 裁决与 Java 行为冲突，后续 expected 应以 PDF/FAQ 为准。
