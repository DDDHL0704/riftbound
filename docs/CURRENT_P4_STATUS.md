# Current P4 Status

更新时间：2026-05-02

这是 P4 高频关键词与基础卡牌的短状态文件。P3 的卡牌数据与 BehaviorSpec 只读骨架完成状态仍以 `docs/CURRENT_P3_STATUS.md` 为准；P2 core rules preflight 与 P2.5 开发期测试 UI 状态分别以 `docs/CURRENT_P2_STATUS.md` 和 `docs/CURRENT_P2_5_STATUS.md` 为准。

## Goal

完成 P4 高频关键词与基础卡牌：按风险分层小批次实现权限关键词、战斗关键词、生命周期关键词、资源关键词、互动关键词、装备关键词和基础动作模板，复用 P3 BehaviorSpec/template skeleton，保持 P2/P2.5/P3 绿色，补测试、文档、状态文件并提交。

## P4.0 Scope

本阶段只做审计与候选分层：

- 读取 P2/P2.5/P3 状态、P4 主计划、README、START_HERE、BehaviorSpec contracts、BehaviorSpec catalog、规则文本 parser、template executor、CardBehaviorRegistry、CoreRuleEngine、catalog baseline tests、conformance runner tests 和官方卡表快照。
- 通过本地 API `/catalog/p3-status` 与 `/catalog/behavior-specs` 只读复核 P3 BehaviorSpec 与 template report。
- 建立 P4 关键词和基础动作模板候选清单、风险分层、推荐小批次顺序和验证门禁。
- 不改 `CoreRuleEngine` 主路径，不启用 P3 template executor 写状态，不迁移全卡牌。

本阶段明确不做：

- 不进入 P5 装备/控制权/触发替换大系统。
- 不进入 P6 全卡牌批量实现。
- 不进入 P7 最终产品 UI。
- 不提交规则 PDF/FAQ。
- 不提交未跟踪的 `riftbound-dotnet.sln`。

## Baseline

- 最新提交：`4a3b45f feat: complete p3 card behavior specs`
- 官方快照：`data/official/card-catalog.zh-CN.json`
- 快照日期：`2026-04-27`
- 官方条目：`1009`
- Functional units：`811`
- P2 core rules preflight：`811/811 = 100.0%`
- 最小 card behavior registry：`794/811 = 97.9%`
- 可打出官方牌差集：已清空
- P3 schema validation：`1009/1009`，violations `0`
- P3 BehaviorSpec：`1009/1009`
- P3 BehaviorSpec status counts：`implemented 785`、`manual-rule-required 211`、`unimplemented 13`
- P3 missing reason：`0`
- 工作区预期：只剩未跟踪 `riftbound-dotnet.sln`

## P4.0 Audit Snapshot

复核命令：

```bash
source scripts/dev-env.sh && ASPNETCORE_URLS=http://127.0.0.1:5091 dotnet run --no-restore --project src/Riftbound.Api/Riftbound.Api.csproj
curl -s http://127.0.0.1:5091/catalog/p3-status
curl -s http://127.0.0.1:5091/catalog/behavior-specs
```

`/catalog/p3-status` 结果：

```json
{
  "officialEntries": 1009,
  "total": 1009,
  "schemaValid": true,
  "schemaViolationCount": 0,
  "functionalUnits": 811,
  "idsAreUnique": true,
  "behaviorSpecs": 1009,
  "statusCounts": {
    "implemented": 785,
    "manual-rule-required": 211,
    "unimplemented": 13
  },
  "missingReasonCardNos": []
}
```

解释：

- `implemented` 仍表示 P3 spec 能映射到现有 P2 手写 registry 或同 functional unit 映射，不表示 P4 keyword/template 已规则化执行。
- `manual-rule-required` 当前主要是 `传奇 106`、`战场 57`、`符文 48`，需要独立规则域。
- `unimplemented 13` 均为指示物/指示物战场/指示物装备，需要 token factory 或非 `PLAY_CARD` 绑定。
- `BehaviorTemplateExecutor` 当前只生成 plan，不改 `MatchState`；P4.1 只能先做安全桥接测试，不能替换 `CoreRuleEngine`。

## Template Candidates

以下统计按 P3 `BehaviorSpec.TemplateIds` 的 distinct card count 计算。

| Template | Total | Implemented | Manual | Unimplemented | P4 risk | P4.0 decision |
|---|---:|---:|---:|---:|---|---|
| `temp_might` | 292 | 255 | 36 | 1 | Low/Medium | 低风险桥接候选；真实执行先复用 P2 `POWER_MODIFIED_UNTIL_END_OF_TURN` 与清理。 |
| `damage` | 148 | 141 | 7 | 0 | Low/Medium | 低风险桥接候选；代表路径已有非致命、致命、多目标、全战场。 |
| `move` | 136 | 116 | 19 | 1 | Medium | 可桥接；精确多战场/游走/此处目的地仍需后续模型。 |
| `draw` | 131 | 105 | 26 | 0 | Low | P4.1 首批候选；抽牌与燃尽已由 P2 覆盖。 |
| `destroy` | 127 | 115 | 8 | 4 | Low/Medium | 可桥接；替代/触发导致的摧毁仍分层处理。 |
| `assemble` | 55 | 53 | 2 | 0 | High | 暂不进 P4.1；涉及贴附、owner/controller、费用与 P5 边界。 |
| `gain_experience` | 51 | 43 | 8 | 0 | Medium/High | 后续单独小批次；需要经验状态与支付/消耗契约。 |
| `recall` | 49 | 39 | 10 | 0 | Medium | 可桥接；召回到基地/手牌已有 P2 原语，精确时序分层。 |
| `stun` | 33 | 30 | 3 | 0 | Low | P4.1 首批候选；当前以本回合内 `STUNNED` 状态执行。 |
| `echo` | 24 | 22 | 2 | 0 | Medium | 已有 `ECHO` optional cost 代表路径，但回响重复“指示”需 FAQ 校验。 |
| `ambush` | 18 | 18 | 0 | 0 | High | 暂不首批；需要待命/反应/战场目的地和 face-down 交互。 |

## Keyword Candidates

以下统计按 P3 `BehaviorSpec.Keywords` 的 distinct card count 计算。

| Keyword | Implemented | Manual | Unimplemented | P4 risk | P4.0 decision |
|---|---:|---:|---:|---|---|
| 迅捷 | 82 | 0 | 0 | Medium | P4.2 候选；需把普通回合/法术对决时机从卡牌特例提升为关键词模型。 |
| 反应 | 136 | 14 | 2 | Medium/High | P4.2 候选；P2 已有 `CanPlayDuringPriority`，但符文/装备/指示物反应需分域。 |
| 急速 | 34 | 0 | 0 | Medium | P4.2 候选；当前多为标签/不支付额外费用入场，额外支付活跃进场需新增最小 optional cost。 |
| 强攻 | 37 | 2 | 0 | High | 暂缓到战斗小批次；需要进攻身份与战斗战力修正。 |
| 坚守 | 24 | 4 | 0 | High | 暂缓到战斗小批次；需要防守身份与战斗战力修正。 |
| 壁垒 | 26 | 0 | 0 | High | 暂缓到战斗小批次；需要承伤顺序和同优先级选择。 |
| 后排 | 6 | 0 | 0 | High | 暂缓到战斗小批次；与承伤顺序强绑定。 |
| 游走 | 38 | 4 | 0 | Medium/High | 当前多为标签；真实跨战场移动需要多战场目的地与移动权限。 |
| 瞬息 | 21 | 7 | 2 | Medium | P4.3 候选；P2 已记录标签，缺“控制者下个回合开始、得分前摧毁”。 |
| 绝念 | 25 | 0 | 0 | High | 暂缓；需要离场触发队列和摧毁来源时序。 |
| 预知 | 12 | 0 | 0 | Medium | P4.3 候选；部分顶牌查看/回收已有 P2 代表路径，可先做最小显式模型。 |
| 狩猎 | 14 | 0 | 0 | Medium/High | 暂缓到经验批次；需要征服/据守事件与经验获得。 |
| 等级 | 15 | 3 | 0 | Medium/High | 暂缓到经验批次；需要经验阈值和入场/静态分支。 |
| 鼓舞 | 12 | 3 | 0 | Medium | 后续小批次；需要本回合已打出其他卡牌记忆。 |
| 法盾 | 47 | 1 | 1 | Medium/High | 后续小批次；需要目标选择额外支付税，FAQ 指明每次被选为目标都要收费。 |
| 待命 | 47 | 6 | 0 | High | 暂缓到互动批次；需要 face-down、隐藏信息、翻开打出和位置限制。 |
| 回响 | 22 | 2 | 0 | Medium | P4.4 候选；已有 optional cost 重复效果，但要确认重复“指示”与费用边界。 |
| 伏击 | 18 | 0 | 0 | High | P4.4 之后再拆；依赖待命/反应/战场目的地。 |
| 装配 | 51 | 0 | 0 | High | 暂缓到装备小批次或 P5；涉及贴附、费用和未激活文本。 |
| 灵便 | 6 | 0 | 0 | High | 暂缓；依赖装备反应打出和自动贴附。 |
| 百炼 | 16 | 2 | 0 | High | 暂缓；FAQ 指明为可选，且依赖装配和贴附边界。 |

## Official Text Anchors

P4.0 选出下一批最小代表，不代表已完成规则执行。

| Candidate | Official card text anchor | Existing evidence/tests | Next action |
|---|---|---|---|
| Draw | `SFD·087/221 先知之兆`：抽三张牌。 | `draw` template 105 implemented；P2 抽牌/燃尽规则已覆盖。 | P4.1 建安全桥接测试，确认 template plan 委托 P2 手写行为。 |
| Damage | `OGS·003/024 焚烧`：对一名单位造成 2 点伤害。 | `p2-preflight-play-incinerate-damage-stack` 与致命伤害清理族。 | P4.1 建 template-to-registry route 断言，不替换伤害结算。 |
| Destroy | `OGN·229/298 复仇`：摧毁一名单位。 | `destroy` template 115 implemented；已有摧毁/放逐替代代表路径。 | P4.1 桥接至现有 `DestroysTarget` 行为。 |
| Stun | `OGN·050/298 符文禁锢`：眩晕一名单位。 | `STUNNED` 已用 until-end-of-turn effect 表达。 | P4.1 桥接并覆盖状态清理。 |
| Temp might | `OGN·004/298 顺劈`：让一名单位本回合内获得强攻 3。 | P2 已有 `POWER_MODIFIED_UNTIL_END_OF_TURN` 和清理。 | P4.1 先测 plan/delegation；P4 战斗强攻真实修正另拆。 |
| Move | `SFD·007/221 晶能阻断器`：目标单位本回合内获得游走。 | P2 已有 move/base/battlefield 原语和 `ROAM` 状态标签。 | P4.1 可桥接，真实游走权限在后续多战场模型。 |
| Recall | `OGN·188/298 祖安保镖`：让战场单位返回所属者手牌。 | P2 已有 `UNIT_RETURNED_TO_HAND` / `EQUIPMENT_RETURNED_TO_HAND`。 | P4.1 可桥接，隐藏/控制权边界另拆。 |
| Echo | `SFD·031/221 点沙成兵`：回响 2，打出一名黄沙士兵。 | P2 已有 `ECHO` optional cost 和 repeat count 样例。 | P4.4 单独小批次，补 FAQ 证据和支付边界。 |
| Ephemeral | `OGN·094/298 精灵召唤`：打出有瞬息的精灵。 | P2 只记录 `瞬息` 标签，未做下个控制者回合开始摧毁。 | P4.3 实现到期前先写单元/fixture。 |
| Swift/Reaction/Haste | `OGN·004/298 顺劈`、`OGN·045/298 蔑视`、`OGN·001/298 灼焰飞龙`。 | P2 已有部分反应优先权窗口和急速标签路径。 | P4.2 建关键词最小时机/额外费用模型。 |

## Risk Layers

低风险，可先做桥接和只读验证：

- `draw`、`damage`、`destroy`、`stun`、`temp_might`
- 已由 P2 手写行为覆盖的 `move` / `recall` 代表路径
- 目标：证明 P3 `BehaviorSpec` / template skeleton 可以安全定位到现有 `CardBehaviorDefinition`，并在 P4.1 继续保持 `CoreRuleEngine` 主路径不变。

中风险，需要小模型后再接入可玩路径：

- 迅捷、反应、急速
- 瞬息到期、预知最小回收分支
- 回响 optional cost/repeat 分支
- 法盾目标税的最小支付校验
- 经验获得/消耗、等级阈值、鼓舞本回合记忆

高风险，暂不进入 P4.1：

- 强攻、坚守、壁垒、后排的完整战斗承伤/战力修正
- 游走的多战场合法移动和移动触发得分
- 待命、伏击的 face-down/隐藏信息/反应翻开路径
- 装配、灵便、百炼的贴附、费用、未激活文本和 owner/controller 边界
- 绝念和其他离场触发队列

## P4 Part Plan

| Part | Status | Percentage | Notes |
|---|---|---:|---|
| P4.0 审计与状态文档 | Done | 100% | 本文件记录候选、统计、风险分层和下一批边界。 |
| P4.1 基础模板安全桥接 | Pending | 0% | 先测 draw/damage/destroy/stun/temp might，不替换 `CoreRuleEngine`。 |
| P4.2 权限关键词最小模型 | Pending | 0% | 迅捷/反应/急速，优先复用现有 P2 fixture。 |
| P4.3 生命周期/资源低风险小批 | Pending | 0% | 瞬息到期、预知代表路径或明确 blocked reason。 |
| P4.4 互动关键词一小批 | Pending | 0% | 先选回响或待命/伏击中的最窄路径。 |
| P4.5 基础动作 executor 小批测试 | Pending | 0% | 明确哪些继续 delegated to P2 handwritten。 |
| P4.6 文档同步与全量验证 | Pending | 0% | README/docs/status 同步，跑全量验证。 |

P4 当前整体进度：按计划 part 计 `1/7 = 14.3%`；按新增已验证规则能力计 `0` 个，因为 P4.0 只完成审计，不新增可玩路径。

## Validation Gate

每个进入 P4 可玩路径的能力都必须补齐：

- 规则证据：至少关联 `docs/rules-evidence-index.md` 中的 PDF/FAQ 条目；关键词默认从 `CORE-260330` p92-p105 rules 800+ 起步，法盾/回响/百炼必须核对 `SOUL-OFAQ-260114` / `SOUL-JFAQ-260114`。
- 官网卡面文本：从 `data/official/card-catalog.zh-CN.json` 或 `BehaviorSpec.OfficialText` 固定代表卡文本。
- Engine test：覆盖状态变化或明确验证 delegated behavior plan。
- Conformance fixture：至少一条 `ConformanceFixtureRunnerTests` 可回放路径，或记录 Java legacy oracle 差异。
- SignalR/Room 或等价 E2E：高风险能力进入可玩路径时补 GameHub/Browser Use smoke；P4.1 的纯桥接可先用 engine/conformance 等价测试。
- 文档状态：同步本文件、必要时同步 `docs/rules-evidence-index.md` / `docs/p2-rules-preflight.md` / README。

## Latest Validation

P4.0 已完成验证：

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`：通过，`0` warnings / `0` errors
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过 `1632/1632`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`：通过 `1574/1574`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`：通过 `8/8`
- `git diff --check`：通过

## Next Step

进入 P4.1：建立基础动作模板到现有 P2 手写 `CardBehaviorDefinition` 的安全桥接测试。首批只选 `draw`、`damage`、`destroy`、`stun`、`temp_might` 的代表卡，验证 P3 `BehaviorSpec` 能定位模板和既有 registry 行为，但 `BehaviorTemplateExecutor` 仍不改状态，`CoreRuleEngine` 主路径保持权威。
