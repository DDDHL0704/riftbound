# Conformance Fixture 格式

更新时间：2026-04-30

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
  "fixtureId": "p1-placeholder-pass-priority",
  "description": "human readable reason",
  "source": "java-oracle | manual-csharp-skeleton",
  "auditStatus": "NEEDS_RULE_AUDIT | RULE_AUDITED",
  "faqVersion": "optional FAQ file/date summary",
  "seed": 2603301001,
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
      "clientIntentId": "intent-pass-priority-1",
      "cmd": {
        "cmdType": "PASS_PRIORITY"
      }
    }
  ],
  "expected": {
    "finalTick": 1,
    "eventKinds": ["PASS_PRIORITY"],
    "promptActions": {
      "P1": ["PLAY_CARD", "ACTIVATE_ABILITY", "ASSEMBLE_EQUIPMENT", "MOVE_UNIT", "HIDE_CARD", "TAP_RUNE", "LEGEND_ACT", "PASS", "END_TURN"],
      "P2": ["WAIT"]
    }
  }
}
```

`expected.eventKinds` 表示写入事件日志的规则事件类型，而不是每次客户端重试返回的响应事件。

补充约定：

- P1 runner 会先为 fixture 中的玩家自动执行 `READY`，但比较时过滤 `PLAYER_READY` / `MATCH_STARTED` 等房间生命周期事件。
- 重复 `clientIntentId` 必须不重复推进 tick，也不重复写入规则事件日志。
- `expected.events[]` 可以继续只写 `kind`，也可以按需补 `tick`、`sequence` 和 `payload`。
- `payload` 是局部匹配，只需要写本 fixture 关心的字段。
- `PLAY_CARD` payload 支持 `sourceObjectId`、`cardNo`、`targetObjectIds`、`mode`、`optionalCosts` 和 `destination`；P4.64 只为 `mode = "AMBUSH"` 锁定伏击战场目的地 envelope，Core 仍显式拒绝真实反应战场打出。
- `MOVE_UNIT` payload 支持 `sourceObjectId`、`origin`、`destination` 和 `optionalCosts`；P4.65 只锁定游走/基础移动 command envelope，Core 仍显式拒绝真实跨战场移动。
- `ASSEMBLE_EQUIPMENT` payload 支持 `sourceObjectId`、`targetObjectId` 和 `optionalCosts`；P4.66 只锁定装备装配 command envelope，Core 仍显式拒绝真实装配/贴附执行。
- `DECLARE_BATTLE` payload 支持 `battlefieldId`、`attackerObjectIds`、`defenderObjectIds` 和 `optionalCosts`；P4.67 只锁定战斗声明 command envelope，Core 仍显式拒绝真实开战/承伤执行。
- `REVEAL_CARD` payload 支持 `sourceObjectId`、`cardNo`、`targetObjectIds`、`mode`、`optionalCosts` 和 `destination`；P4.68 只锁定待命翻开/显露 command envelope，Core 仍显式拒绝真实隐藏信息和翻开打出执行。
- Snapshot 对手视角下的正面朝下对象只暴露 `objectId` 与 `isFaceDown = true`；P4.69 不暴露其 `power`、`tags`、`manaCost` 等卡面/规则细节，拥有者视角仍保留完整对象信息。

现有 fixture 不在本格式文档逐条维护，避免每新增卡牌都同步长清单。需要查找样例时：

- 运行 `rg --files tests/Riftbound.ConformanceTests/Fixtures | sort` 查看当前 fixture。
- 在 `docs/p2-rules-preflight.md` 的 fixture 表和进度项中查找规则场景。
- 本文件只保留 schema、字段语义和代表性片段。

## 2.1 P2 schema v2 草案

P2 fixture 已开始使用 `schemaVersion = 2`。当前 C# 侧已能读取以下字段，并能把 `initialState` 构造成真实权威 `MatchState`。

当前代表性覆盖：

- `p2-preflight-turn-start.fixture.json` 验证普通回合开始行为。
- `p2-preflight-end-turn-advances-to-next-start.fixture.json` 验证 `END_TURN` 后自动推进并结算下一回合开始。
- `p2-preflight-end-turn-special-cleanup.fixture.json` 验证 `cardObjects` 中的伤害与本回合内效果会被特殊清理处理。
- `p2-preflight-cleanup-repeats-until-stable.fixture.json` 验证特殊清理后的常规清理重复事件。
- `p2-preflight-pass-priority-does-not-end-turn.fixture.json` 验证拒绝态不推进 tick 或事件。

`ConformanceFixtureRunner.CompareExpected` 已开始通用比较 final tick、event kinds、event tick/sequence/payload 局部字段、prompt actions、最终 timing、符文池、分数、经验、玩家区域、对象状态和结算链；后续继续扩展 snapshots canonical diff：

```json
{
  "schemaVersion": 2,
  "fixtureId": "p2-preflight-turn-start-runes-and-draw",
  "initialState": {
    "turnNumber": 1,
    "activePlayerId": "P2",
    "turnPlayerId": "P2",
    "phase": "TURN_START",
    "timingState": "NEUTRAL_CLOSED",
    "players": {
      "P2": {
        "mainDeck": ["P2-MAIN-001"],
        "runeDeck": ["P2-RUNE-001", "P2-RUNE-002"],
        "hand": []
      }
    },
    "runePools": {
      "P2": { "mana": 0, "power": 0 }
    },
    "experience": {
      "P2": 0
    },
    "untilEndOfTurnEffects": [],
    "cardObjects": {
      "P2-UNIT-001": {
        "damage": 2,
        "power": 3,
        "untilEndOfTurnPowerModifier": 0,
        "untilEndOfTurnEffects": ["effect-temp-power"],
        "isFaceDown": false,
        "isExhausted": false,
        "attachedToObjectId": "optional-equipment-attachment-target"
      }
    }
  },
  "expected": {
    "finalState": {
      "phase": "MAIN",
      "timingState": "NEUTRAL_OPEN",
      "runePools": {
        "P2": { "mana": 0, "power": 0 }
      },
      "experience": {
        "P2": 0
      },
      "players": {
        "P2": {
          "hand": ["P2-MAIN-001"],
          "base": ["P2-RUNE-001", "P2-RUNE-002"]
        }
      },
      "stackItems": [],
      "untilEndOfTurnEffects": [],
      "cardObjects": {
        "P2-UNIT-001": {
          "damage": 0,
          "power": 3,
          "untilEndOfTurnPowerModifier": 0,
          "untilEndOfTurnEffects": [],
          "isFaceDown": false,
          "isExhausted": false,
          "attachedToObjectId": "optional-equipment-attachment-target"
        }
      }
    },
    "events": [
      { "kind": "TURN_START_BEGAN" },
      { "kind": "RUNES_CALLED" },
      { "kind": "CARD_DRAWN" },
      { "kind": "RUNE_POOL_CLEARED" },
      { "kind": "MAIN_PHASE_BEGAN" }
    ],
    "prompts": {
      "P1": { "actionable": false, "actions": ["WAIT"] },
      "P2": { "actionable": true, "actions": ["END_TURN"] }
    }
  }
}
```

schema v2 目前已支持 P2 初始状态和 expected 中的事件 tick/sequence/payload 局部匹配、turn/phase/timing、符文池、分数、经验、玩家区域、对象状态（含 `damage`、`power`、`untilEndOfTurnPowerModifier`、`untilEndOfTurnEffects`、`isFaceDown`、`isAttacking`、`isDefending`、`isExhausted`、`tags`、`manaCost`、`attachedToObjectId`）、全局 `untilEndOfTurnEffects`、`winnerPlayerId`，以及 FEPR/法术对决所需的 `priorityPlayerId`、`passedPriorityPlayerIds`、`stackItems`、`focusPlayerId`、`passedFocusPlayerIds`。`initialState.seed` 已接入权威 `MatchState.seed`，先用于多张卡牌同时回收到主牌堆底部和燃尽回收洗匀时的可回放随机顺序。`CompareExpected` 已接入出牌与回合结束组合 fixture，下一步继续把更多 P2 fixture 从手写断言迁移到通用 expected diff。

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

P2 第一批 fixture 的规则审查顺序见 `docs/p2-rules-preflight.md`。其中 `p2-turn-start-runes-and-draw`、`p2-end-turn-special-cleanup`、`p2-pass-priority-does-not-end-turn`、`p2-fepr-priority-pass-resolves-stack`、`p2-fepr-resolves-latest-keeps-remaining-stack`、`p2-spell-duel-pass-focus-closes-window`、`p2-turn-start-burnout-empty-graveyard-wins`、`p2-play-punishment-damage-stack` 是进入核心规则实现前的优先门禁。

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

C# 侧当前已把 `PASS`、`END_TURN`、重复 `PASS` 的事件日志和 prompt actions 对齐到旧 Java 行为。`ConformanceFixture` 已能读取可选 `rulesEvidence`、`faqVersion`、`auditStatus`、`legacyOracle`、P2 `initialState` 和 richer `expected`；Java exporter 已输出 `legacyOracle`，并暂时保留旧 `oracle` 兼容字段。现有 3 条 legacy fixture 已补细化 evidence，但仍标记为 `NEEDS_RULE_AUDIT`。当前已确认 `PASS -> TURN_ENDED` 是 legacy mismatch candidate；若后续 PDF/FAQ 裁决与 Java 行为冲突，expected 应以 PDF/FAQ 为准。
