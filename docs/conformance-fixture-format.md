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
- `PLAY_CARD` payload 支持 `sourceObjectId`、`cardNo`、`targetObjectIds`、`mode`、`optionalCosts` 和 `destination`；P4.64 只为 `mode = "AMBUSH"` 锁定伏击战场目的地 envelope，Core 仍显式拒绝真实反应战场打出。P4.97 补伏击 `mode = "AMBUSH"` 显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不改变战场、不创建结算链。P4.325 补伏击带目标显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不改变战场、不创建结算链。P4.326 补伏击 `destination = "BASE"` 显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不进入基地、不改变战场、不创建结算链。P4.327 补伏击优先权窗口显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不进入基地/战场、不改变既有结算链。P4.335 补伏击来源不在手牌显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动基地对象、不改变战场、不创建结算链。P4.377 补伏击未知来源显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不创建手牌来源、不改变战场、不创建结算链。P4.378 补伏击对手手牌来源显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动任一手牌对象、不改变战场、不创建结算链。P4.379 补伏击非伏击手牌来源显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌对象、不改变战场、不创建结算链。P4.101 补 `预知` 选择非顶部主牌堆牌时的显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌或主牌堆、不创建结算链。P4.102 补《游击战》选择废牌堆非待命目标时的显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌或废牌堆、不创建结算链。P4.103 补《灼焰飞龙》`HASTE_READY` 缺少 power 费用时的拒绝 fixture：不推进 tick、不写事件、不移动手牌、不创建结算链。P4.104 补《焚烧》选择敌方 `法盾` 单位但无法支付目标税时的拒绝 fixture：不推进 tick、不写事件、不支付基础费用或目标税、不移动手牌、不创建结算链。P4.105 补《妖异狐火》同时选择敌方 `法盾` 与 `法盾2` 单位但无法支付聚合目标税时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不摧毁目标、不创建结算链。P4.106 补《妖异狐火》选择目标总战力超过 4 时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不摧毁目标、不创建结算链。P4.107 补《顽皮触手》选择敌方战场单位总战力超过 8 时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不移动目标、不创建结算链。P4.108 补《狩魂》选择 4 战力目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不摧毁目标、不创建结算链。P4.109 补《罡风》选择 4 战力目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不回手目标、不创建结算链。P4.110 补《巧取豪夺》选择敌方废牌堆单位牌时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。P4.111 补《致命华彩》选择友方单位时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。P4.112 补《血钱》选择 3 战力战场单位时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不摧毁目标、不创建结算链。P4.113 补《惩戒》选择基地单位时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。P4.114 补《扑咚！》选择非进攻方单位时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不眩晕目标、不创建结算链。P4.115 补《天顶之刃》选择敌方基地单位时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不眩晕目标、不创建结算链。P4.116 补《天顶之刃》选择友方单位时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不眩晕目标、不创建结算链。P4.117 补《狂风绝息斩》第二目标选择敌方基地单位时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不重置友方单位、不伤害目标、不创建结算链。P4.118 补《狂风绝息斩》目标顺序错误时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不重置友方单位、不伤害目标、不创建结算链。P4.119 补《聚合变异》第二目标选择敌方单位时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不修改战力、不创建结算链。P4.120 补《聚合变异》重复选择同一友方单位时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不修改战力、不创建结算链。P4.121 补《存在焦虑》选择正在进攻的友方单位时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不眩晕目标、不创建结算链。P4.122 补《霹天雳地》无法支付所需费用时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。P4.123 补《御衡守念》未满足减费条件且无法支付未降低费用时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不抽牌、不召出符文、不创建结算链。P4.124 补《透体圣光》重复选择同一目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。P4.125 补《风箱炎息》选择第四个目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。P4.126 补《烈火风暴》携带显式单位目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。P4.127 补《新月打击》选择友方目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。P4.130 补《新月打击》选择敌方基地目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。P4.128 补《换换乐》重复选择同一目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不修改战力、不创建结算链。P4.129 补《换换乐》选择基地单位目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不修改战力、不创建结算链。
- P4.336 补伏击来源 `cardNo` 身份不匹配显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不改变战场、不创建结算链。
- P4.337 补伏击携带 unsupported optional cost 显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不改变战场、不创建结算链。
- P4.387 补 `p4-ambush-play-card-gloomy-apothecary-battlefield.fixture.json`：`PLAY_CARD mode=AMBUSH destination=BATTLEFIELD:P1-MAIN` 在优先权窗口中只接受官网《阴森药剂师》无目标代表路径，支付 3、移出手牌、加入带 `destination` 的结算链项目；双方让过后写 `UNIT_PLAYED_TO_BATTLEFIELD` 并把源单位放入控制者战场。`p4-ambush-play-card-priority-window-rejected.fixture.json` 现在锁定缺少己方战场单位代理时拒绝且不改状态。
- P4.338 补待命暗置来源位于对手手牌时的显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动任一手牌对象、不创建结算链。
- P4.339 补已知非待命牌进入待命暗置路径时的显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不创建正面朝下对象、不创建结算链。
- P4.340 补待命暗置缺少支付项时的显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不创建正面朝下对象、不创建结算链。
- P4.341 补待命显露缺少支付项时的显式拒绝 fixture：不推进 tick、不写事件、不翻开来源、不支付费用、不创建结算链。
- P4.342 补已知非待命牌进入待命显露路径时的显式拒绝 fixture：不推进 tick、不写事件、不翻开来源、不支付费用、不创建结算链。
- P4.343 补已知非待命牌进入待命反应入栈路径时的显式拒绝 fixture：不推进 tick、不写事件、不翻开来源、不移动基地对象、不支付费用、不创建新的结算链项目。
- P4.344 补待命反应入栈省略 `STANDBY_REVEAL_0` 时的显式拒绝 fixture：不推进 tick、不写事件、不翻开来源、不移动基地对象、不支付费用、不创建新的结算链项目。
- P4.345 补待命反应入栈携带 unsupported optional cost 时的显式拒绝 fixture：不推进 tick、不写事件、不翻开来源、不移动基地对象、不支付费用、不创建新的结算链项目。
- P4.346 补待命反应入栈来源已公开时的显式拒绝 fixture：不推进 tick、不写事件、不移动基地对象、不支付费用、不创建新的结算链项目。
- P4.347 补待命反应入栈来源不在命令玩家基地时的显式拒绝 fixture：不推进 tick、不写事件、不翻开对手基地对象、不移动任一基地对象、不支付费用、不创建新的结算链项目。
- P4.348 补待命反应入栈来源 `cardNo` 身份不匹配时的显式拒绝 fixture：不推进 tick、不写事件、不翻开来源、不移动基地对象、不支付费用、不创建新的结算链项目。
- P4.349 补待命反应入栈目的地不是 `STACK` 时的显式拒绝 fixture：不推进 tick、不写事件、不翻开来源、不移动基地对象、不支付费用、不创建新的结算链项目。
- P4.131 补《风箱炎息》重复选择同一单位目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。
- P4.132 补《台前作秀》支付 `ECHO` 但 mana 不足时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不抽牌、不创建结算链。
- P4.133 补非回响法术《惩戒》携带 `ECHO` optional cost 时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。
- P4.134 补《火箭轰击》缺失 `mode` 时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标、不创建结算链。
- P4.135 补《火箭轰击》摧毁装备模式指定单位目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不摧毁目标、不创建结算链。
- P4.136 补《紧急召回》指定单位目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不返回目标、不创建结算链。
- P4.137 补《魄罗佳肴》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不抽牌、不创建结算链。
- P4.138 补《舒瑞娅的安魂曲》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不活跃单位、不创建结算链。
- P4.139 补《未来熔炉》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建随从、不创建结算链。
- P4.140 补《废料堆》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不抽牌、不创建结算链。
- P4.141 补《精灵提灯》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建精灵、不创建结算链。
- P4.142 补《地沟区地图》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.143 补《占卜花朵》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.144 补《魔法鲜豆》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.145 补《钢铁弩炮》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.146 补《玄冰之心》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.147 补《懊悔法球》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.148 补《灵魂之剑》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.149 补《锯齿短匕》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.150 补《多兰之盾》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.151 补《海克斯注力刚壁》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.152 补《多兰之刃》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.153 补《多兰之戒》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.154 补《行军号令》敌方基地第二目标拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不造成伤害、不创建结算链。
- P4.155 补《决斗》目标顺序反转拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不造成伤害、不创建结算链。
- P4.156 补《战斗号令》目标顺序反转拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不移动单位、不创建结算链。
- P4.157 补《虚空来袭》目标顺序反转拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不移动单位、不创建结算链。
- P4.158 补《先锋之眼》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.159 补《反曲之弓》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.160 补《长剑》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.161 补《布甲》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.162 补《斯特拉克的挑战护手》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.163 补《旋转飞斧》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.164 补《牧人的传家宝》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不获得经验、不创建结算链。
- P4.165 补《残暴之力》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.166 补《守护天使》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.167 补《海克斯饮魔刀》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.168 补《狂徒铠甲》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.169 补《三相之力》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.170 补《轻灵之靴》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.171 补《萃取》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.172 补《神圣剪刀》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.173 补《暴风大剑》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.174 补《云游图鉴》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.175 补《阿瑞昂的陨落》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.176 补《猎人的宽刃刀》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.177 补《枯萎战斧》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.178 补《碎骨棒》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.179 补《远古簇碑》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.180 补《海克斯异常体》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.181 补《能量通道》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.182 补《预时之门》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.183 补《邪鸦魔典》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.184 补《太阳圆盘》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.185 补《远见面具》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.186 补《烈阳圣坛》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.187 补《炼金科技桶》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.188 补《灵魂之轮》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.189 补《蘑菇袋》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.190 补《竞技场酒吧》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.191 补《海盗避风港》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.192 补《被遗忘的路标》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.193 补《冰封宝石》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.194 补《倾颓宫殿》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.195 补《猩红玫瑰》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.196 补《逆转碎片》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.197 补《装配架》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.198 补《斯弗尔尚歌》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.199 补《Z型驱动》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.200 补《先锋军备》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.201 补《追忆祭坛》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.202 补《暴怒之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.203 补《专注之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.204 补《洞察之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.205 补《力量之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.206 补《不和之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.207 补《团结之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.208 补 OGN 版《暴怒之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.209 补 OGN 版《专注之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.210 补 OGN 版《洞察之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.211 补 OGN 版《力量之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.212 补 OGN 版《不和之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.213 补 OGN 版《团结之印》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.214 补《奇妙行囊》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.215 补《塞壬号》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.216 补《无主宝藏》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.217 补《拾荒小能手》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.218 补《雾临剑冢》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.219 补《闪耀极光》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.220 补《烈阳徽记》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.221 补《先锋之盔》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.222 补《蜜糖果实》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.223 补《临终仪式》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.224 补《破败王者之刃》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.225 补《来路不明的武器》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.226 补《海兽钓钩》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.227 补《禁魔石丰碑》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.228 补《中娅沙漏》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.229 补《夜之锋刃》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.230 补《炉火斗篷》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.231 补《灭世者的死亡之冠》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.232 补《喷射球果》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.233 补《夺命名单》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.234 补《受诅咒的石棺》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不放逐废牌堆单位、不创建结算链。
- P4.235 补 promo 编号《碎骨棒》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.236 补《海克斯科技护手》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建结算链。
- P4.237 补《宝藏魔像》带目标打出时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建金币、不创建结算链。
- P4.238 补《泽拉斯》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.239 补《龙魂贤者》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.240 补《绵绵魄罗》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.241 补 SFD·088《烈娜塔·戈拉斯克》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.242 补 SFD·088a《烈娜塔·戈拉斯克》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.243 补 OGN·028《德莱文》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.244 补 SFD·110《菲奥娜》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.245 补 SFD·110a《菲奥娜》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.246 补 SFD·141《艾瑞莉娅》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.247 补 SFD·141a《艾瑞莉娅》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.248 补 OGN·140《唤龙使者》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.249 补 OGN·055《驭水者》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.250 补 OGN·065《睿智长者》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.251 补 OGN·084《踊跃的学徒》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.252 补 OGN·091《竞技场勤务小队》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.253 补 OGN·061《魄罗牧者》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.254 补 OGN·103《拉文布鲁姆学生》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.255 补 OGN·118《残响之魂》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.256 补 OGN·125《比尔吉沃特恶霸》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.257 补 OGN·130《神射海盗》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.258 补 OGN·131《沙丘亚龙》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.259 补 OGN·167《余火修士》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.260 补 OGN·177《隐秘追踪者》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.261 补 OGN·178《卧底特工》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.262 补 OGN·185《旅行商人》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.263 补 OGN·190《克格莫》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.264 补 OGN·222《诺克萨斯鼓手》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.265 补 OGN·199《控潮者》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P7.9 后续补 `p2-preflight-play-tidecaller-swap-target-location`：OGN·199《控潮者》普通手牌打出时可选择己方单位目标，结算后源牌与目标单位交换位置并同步 `ObjectLocations`；非法非己方单位目标仍由 P4.265 拒绝 fixture 覆盖。
- P4.266 补 OGN·202《金克丝》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.267 补 OGN·226《幽灵主母》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P7.9 后续补 `p2-preflight-play-ghost-matron-graveyard-unit-base`：OGN·226《幽灵主母》普通手牌打出时可选择己方废牌堆中费用不高于 3 且不高于当前 A 的单位，结算后源牌和所选废牌堆单位均由服务端打出到基地；非法非废牌堆目标仍由 P4.267 拒绝 fixture 覆盖。
- P4.268 补 OGN·230《阿不思·菲罗斯》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.269 补 OGN·236《卡尔萨斯》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.270 补 SFD·027《穿沙角兽》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.271 补 SFD·035《幽径守卫》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.272 补 SFD·041《学徒铁匠》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.273 补 SFD·047《山猿老祖》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.274 补 SFD·089《兰博》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.275 补 SFD·089a《兰博》异画 A 普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.276 补 SFD·100《约德尔探险家》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.277 补 SFD·121《黑市掮客》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.278 补 SFD·137《猎海小队》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.279 补 UNL-068《幽魂半人马》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.280 补 SFD·123《腐化执法官》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.281 补 SFD·125《大力仙灵》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.282 补 SFD·142《贾尔·米达尔达》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.283 补 SFD·146《薇古丝》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.284 补 SFD·152《显赫金主》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.285 补 SFD·039《皇家随从》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.286 补 SFD·049《厄斐琉斯》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.287 补 SFD·058《奥恩》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.288 补 SFD·058a《奥恩》异画 A 普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.289 补 SFD·079《巴德》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.290 补 SFD·081《大老千》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.291 补 SFD·084《杰斯》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.292 补 SFD·160《祖安混混》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.293 补 SFD·165《戈拉斯克调酒师》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.294 补 SFD·170《雷克塞》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.295 补 SFD·170a《雷克塞》异画 A 普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.296 补 SFD·173《索拉卡》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.297 补 SFD·175《垓兽》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.298 补 SFD·180《菲奥娜》普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- P4.299 补 SFD·180a《菲奥娜》异画 A 普通手牌打出带目标时的拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动手牌、不入场单位、不创建结算链。
- `ACTIVATE_ABILITY` payload 支持 `sourceObjectId`、`abilityId`、`targetObjectIds` 和 `optionalCosts`；P4.73/P4.74 执行 `UNL-030/219 蔚` 的 `PAY_2_RED_DOUBLE_POWER` 无目标技能：来源场上对象必须记录 `cardNo = "UNL-030/219"`，支付 2 mana + 1 power，把技能加入结算链，双方让过后让来源本回合内战力翻倍。P4.89 补同一技能携带目标时的拒绝 fixture；P4.90 补同一技能携带未支持 optional cost 时的拒绝 fixture；P4.91 补同一技能费用不足时的拒绝 fixture；P4.92 补非《蔚》来源伪造同一技能时的拒绝 fixture；P4.87 补同一技能来源由对手控制时的拒绝 fixture；P4.88 补同一技能来源不在场上时的拒绝 fixture：不推进 tick、不写事件、不支付资源、不入栈。P4.77 额外执行 `UNL-026/219 泽拉斯` 的 `PAY_RED_EXHAUST_DAMAGE_3` 单目标技能：来源必须是控制者战场中未横置的泽拉斯，目标必须是一名场上单位，敌方法盾目标会额外支付目标税，激活时支付 1 power 并横置来源，双方让过后造成 3 点伤害。P4.93 补该技能敌方法盾目标税 mana 不足时的拒绝 fixture：不推进 tick、不写事件、不支付 power、不横置来源、不入栈。P4.78 补同一技能选择己方法盾单位的 no-tax 边界：`spellshieldTaxMana = 0`，但仍支付 1 power、横置来源并造成 3 点伤害。P4.79 补该技能来源已横置时的拒绝 fixture：不推进 tick、不写事件、不支付资源、不入栈。P4.80 补该技能缺少目标时的拒绝 fixture：不推进 tick、不写事件、不支付资源、不横置来源、不入栈。P4.81 补该技能提供两个目标时的拒绝 fixture：不推进 tick、不写事件、不支付资源、不横置来源、不伤害目标、不入栈。P4.82 补该技能携带未支持 optional cost 时的拒绝 fixture：不推进 tick、不写事件、不支付资源、不横置来源、不伤害目标、不入栈。P4.83 补非《泽拉斯》来源伪造该技能 id 时的拒绝 fixture：不推进 tick、不写事件、不支付资源、不横置来源、不伤害目标、不入栈。P4.84 补该技能选择场上装备等非单位目标时的拒绝 fixture。P4.85 补该技能来源位于基地而非战场时的拒绝 fixture。P4.86 补该技能来源由对手控制时的拒绝 fixture，并把 P4.79-P4.86/P4.93 拒绝 fixtures 纳入资源关键词聚合回放：不推进 tick、不写事件、不支付资源、不横置来源、不入栈。P4.389 将《蔚》和《泽拉斯》两个已验证代表技能登记到 `P4ActivatedAbilityCatalog`，Core 先查 registry 再复用既有执行路径；P4.391 追加 deferred surface catalog 与 direct engine 审计，锁定《龙魂贤者》《绵绵魄罗》《烈娜塔·戈拉斯克》《猩红玫瑰》《黑影》等未登记技能的 ability id 仍返回 `UNSUPPORTED_COMMAND`，不支付资源、不横置来源、不修改目标、不创建结算链；其他技能、可选费用技能、装备技能和更多泛化 registry 扩展仍暂缓。
- `MOVE_UNIT` payload 支持 `sourceObjectId`、`origin`、`destination` 和 `optionalCosts`；P4.65 锁定游走/基础移动 command envelope，P4.98/P4.384 保留精确战场移动缺少 `ROAM` 时显式拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动对象、不改变战力、不创建结算链。P4.300 补 coarse `BASE` -> `BATTLEFIELD` 友方单位移动 fixture；P4.301 补 coarse `BATTLEFIELD` -> `BASE` 友方单位移动 fixture：均推进 1 tick，写对应 `UNIT_MOVED_TO_*` 事件，不支付费用、不改变战力、不创建结算链。P4.302 补 opponent-source rejection fixture：当前玩家不能移动对手控制的来源对象。P4.303 补 face-down source rejection fixture：当前 coarse 公开移动路径不能移动正面朝下来源对象。P4.304 补 non-unit source rejection fixture：当前 coarse 单位移动路径不能移动装备等非单位对象。P4.305 补 same-zone destination rejection fixture：当前 coarse 移动必须进入不同区域。P4.306 补 window rejection fixture：当前 coarse 移动只能由主动玩家在主阶段开放窗口发起。P4.307 补 optional cost rejection fixture：当前 coarse 移动不接受 `ROAM` 等额外费用。P4.308 补 unsupported zone rejection fixture：当前 coarse 移动只接受 `BASE` 与 `BATTLEFIELD` 区域，拒绝 `HAND` 等非公开场上区域。P4.309 补 origin mismatch rejection fixture：来源对象实际所在区域必须匹配 command `origin`。P4.310 补 pending stack rejection fixture：结算链非空的优先权窗口不能执行当前 coarse 移动。P4.311 补 hand-source rejection fixture：仍在手牌中的来源对象不能伪装为场上来源移动。P4.333 补 combatant-source rejection fixture：已标记为进攻或防守的来源对象不能在完整战斗移动模型前通过当前 coarse 移动离场。P4.334 保留 attached-equipment-source rejection fixture：未显式声明 owner/controller 的已贴附装备仍拒绝且零副作用。P5.2 新增 `p5-move-unit-attached-equipment-follows-host.fixture.json`：显式身份匹配的装备随宿主单位 `BASE` -> `BATTLEFIELD` 移动，并写 `EQUIPMENT_MOVED_WITH_UNIT`。P4.328 补 precise destination rejection fixture：即使不携带 `ROAM`，`BATTLEFIELD:P1-MAIN` 等精确战场目的地也继续拒绝。P4.329 补 precise origin rejection fixture：即使不携带 `ROAM`，`BATTLEFIELD:P1-MAIN` 等精确战场来源也继续拒绝。P4.384 补 accepted 精确游走 fixture：`origin=BATTLEFIELD:P1-LEFT`、`destination=BATTLEFIELD:P1-RIGHT`、`optionalCosts=["ROAM"]` 且来源为当前玩家正面战场单位并拥有 `游走` 权限时推进 1 tick，写 `UNIT_MOVED_TO_BATTLEFIELD` 事件并记录精确 origin/destination；持久多战场坐标、移动次数和移动触发仍 deferred。
- `ASSEMBLE_EQUIPMENT` payload 支持 `sourceObjectId`、`targetObjectId` 和 `optionalCosts`；P4.66 锁定装备装配 command envelope。P4.388 将普通窗口《长剑》最小路径提升为 accepted fixture `p4-assemble-equipment-long-sword-attach.fixture.json`：`optionalCosts=["ASSEMBLE_RED"]` 时支付 1 power，保留装备在基地列表中，设置 `attachedToObjectId`，写 `COST_PAID` / `EQUIPMENT_ATTACHED`，不创建结算链。P5.1 在 `p5-equipment-state-assemble-long-sword-owner-controller.fixture.json` 中补充 card object 的 `ownerId` / `controllerId` 局部比对和可见快照字段：已声明身份的装备与单位在代表装配路径中必须保留 owner/controller，并写入 `EQUIPMENT_ATTACHED.ownerId/controllerId`；身份和当前区域控制者冲突时仍拒绝且零副作用。P4.330 补 priority-window rejection fixture：即使 `NEUTRAL_CLOSED` 且 stack 非空，当前也不接受真实装配，不改变既有结算链。P4.331 补 already-attached source rejection fixture：已贴附装备再次执行 `ASSEMBLE_EQUIPMENT` 当前仍拒绝，并保留原 `attachedToObjectId`。P4.353/P4.375/P4.354/P4.376 补 missing/unknown target, missing source 和 unknown source rejection fixture：缺少 `targetObjectId`、未知 `targetObjectId`、缺少 `sourceObjectId` 或未知 `sourceObjectId` 当前仍拒绝。P4.355 补 hand-source rejection fixture：仍在手牌中的来源装备当前仍拒绝，不支付费用、不移动手牌或基地对象、不贴附、不创建结算链。P4.356 补 opponent-source rejection fixture：对手基地中的装备来源当前仍拒绝，不支付费用、不移动任一玩家基地对象、不贴附、不创建结算链。P4.357 补 non-equipment-source rejection fixture：单位等非装备来源当前仍拒绝，不支付费用、不移动基地对象、不贴附、不创建结算链。P4.358 补 non-unit-target rejection fixture：装备等非单位目标当前仍拒绝，不支付费用、不移动基地对象、不贴附、不创建结算链。P4.359 补 unsupported-optional-cost rejection fixture：`ECHO` 等非装配费用名当前仍拒绝，不支付费用、不移动基地对象、不贴附、不创建结算链。P4.360 补 missing-optional-cost rejection fixture：省略装配费用名当前仍拒绝，不支付费用、不移动基地对象、不贴附、不创建结算链。
- `DECLARE_BATTLE` payload 支持 `battlefieldId`、`attackerObjectIds`、`defenderObjectIds` 和 `optionalCosts`；P4.67 只锁定战斗声明 command envelope，Core 仍显式拒绝真实开战/承伤执行。P4.100 补战斗声明 `DECLARE_BATTLE` 显式拒绝 fixture：不推进 tick、不写事件、不设置攻防状态、不改变战力、不移动对象、不创建结算链。P4.332 补 priority-window rejection fixture：即使 `NEUTRAL_CLOSED` 且 stack 非空，当前也不接受真实战斗声明，不改变既有结算链。P4.350/P4.351/P4.352 补空攻击者、空防守者和缺少 `battlefieldId` 拒绝 fixture：不推进 tick、不写事件、不设置攻防状态、不改变战力、不移动对象、不创建结算链。P4.374 补 unknown-battlefield rejection fixture：非空但未知的 `battlefieldId` 当前也拒绝，不推进 tick、不写事件、不设置攻防状态、不改变战力、不移动对象、不创建结算链。P4.361 补 unsupported-optional-cost rejection fixture：`ECHO` 等非战斗费用名当前仍拒绝，不推进 tick、不写事件、不设置攻防状态、不改变战力、不移动对象、不创建结算链。P4.362 补 missing-optional-cost rejection fixture：省略战斗费用名当前仍拒绝，不推进 tick、不写事件、不设置攻防状态、不改变战力、不移动对象、不创建结算链。P4.363/P4.364 补 attacker/defender-outside-battlefield rejection fixture：基地等非战场来源当前不能成为攻击者或防守者，不推进 tick、不写事件、不设置攻防状态、不改变战力、不移动对象、不创建结算链。P4.365/P4.366 补 opponent-attacker/current-player-defender rejection fixture：当前玩家不能把对手控制的场上单位声明为自己的攻击者，也不能把自己控制的场上单位声明为防守者。P4.367/P4.368 补 duplicate-attacker/duplicate-defender rejection fixture：同一对象不能在一次 `DECLARE_BATTLE` 的攻击者或防守者列表中重复出现。P4.369 补 attacker-defender-overlap rejection fixture：同一对象不能同时出现在攻击者与防守者列表。P4.370/P4.371 补 non-active-player/non-main-phase rejection fixture：非主动玩家或非主阶段不能声明战斗。P4.372/P4.373 补 unknown-attacker/unknown-defender rejection fixture：未知攻击者或防守者对象不能被创建或设置为攻防中；拒绝时不推进 tick、不写事件、不设置攻防状态、不改变战力、不移动对象、不创建结算链。
- P4.381/P4.382 补 `DECLARE_BATTLE` accepted 代表 fixture：普通主阶段开放窗口、stack 为空、`battlefieldId = "BATTLEFIELD:P1-MAIN"`、单攻击者、单防守者且 `optionalCosts = ["COMBAT_ASSIGNMENT"]` 时推进 tick、写入 `BATTLE_DECLARED`，设置攻击者/防守者攻防状态，并应用强攻/坚守数值战斗伤害与致命清理。P4.383 追加单攻击者/两防守者 accepted 代表 fixture：同一窗口内按 `壁垒` 优先、`后排` 延后分配进攻伤害并做致命清理。P4.385 追加 `狩猎N` 最小征服经验代表 fixture：攻击者存活且全部声明防守者被本次战斗清理时，写入 `BATTLEFIELD_CONQUERED` 和 `EXPERIENCE_GAINED`，并在 `finalState.experience` 断言经验总值；后续命令级回归覆盖防守方赢得战斗并据守时的 `BATTLEFIELD_HELD` + `EXPERIENCE_GAINED` 狩猎经验；普通多防守者选择、多攻防对象、完整得分和任务状态机仍暂缓。
- `HIDE_CARD` payload 支持 `sourceObjectId`、`cardNo`、`destination` 和 `optionalCosts`；P4.70 执行 `destination = "STANDBY"` 且 `optionalCosts = ["STANDBY_A"]` 的待命单位手牌放置路径：支付 1 点费用、把来源牌移入控制者基地并设为 `isFaceDown = true`，公开事件不携带 `cardNo`、`power`、`tags` 或 `manaCost`。P4.94 补同一路径费用不足拒绝 fixture：不推进 tick、不写事件、不移动手牌、不创建正面朝下对象。P4.312 补同一路径来源不在手牌拒绝 fixture：不推进 tick、不写事件、不支付费用、不移动基地对象、不创建正面朝下对象。P4.338 补同一路径来源位于对手手牌拒绝 fixture：不推进 tick、不支付费用、不移动任一手牌对象。P4.339 补已知非待命牌拒绝 fixture：命令 `cardNo` 对应牌面必须暴露待命关键词，不推进 tick、不支付费用、不移动手牌、不创建正面朝下对象。P4.340 补缺少待命支付项拒绝 fixture：当前不接受省略 `STANDBY_A` / `STANDBY_FREE`，不推进 tick、不支付费用、不移动手牌、不创建正面朝下对象。P4.313 补同一路径 unsupported destination 拒绝 fixture：当前只接受 `destination = "STANDBY"`，不推进 tick、不支付费用、不移动手牌、不创建正面朝下对象。P4.314 补同一路径 unsupported optional cost 拒绝 fixture：当前不接受 `ECHO` 等非待命费用名，不推进 tick、不支付费用、不移动手牌、不创建正面朝下对象。P4.315 补同一路径 window rejection fixture：当前只能由主动玩家在主阶段开放窗口执行，不推进 tick、不支付费用、不移动手牌、不创建正面朝下对象。P4.316 补同一路径 pending stack rejection fixture：当前不能在结算链非空的优先权窗口执行，不推进 tick、不支付费用、不移动手牌、不创建正面朝下对象且保留 stack item。P4.317 补已知来源 `cardNo` mismatch rejection fixture：来源对象已有 `cardNo` 时必须匹配命令 `cardNo`，不推进 tick、不支付费用、不移动手牌、不创建正面朝下对象。P4.72 额外支持《游击战》授予的 `FREE_STANDBY_HIDE:{playerId}` 本回合效果，允许同一路径使用 `optionalCosts = ["STANDBY_FREE"]` 并支付 0 点费用；P4.96 补 `STANDBY_FREE` 无权限拒绝 fixture：不推进 tick、不写事件、不移动手牌、不创建正面朝下对象；待命触发和完整隐藏区仍暂缓。
- `REVEAL_CARD` payload 支持 `sourceObjectId`、`cardNo`、`targetObjectIds`、`mode`、`optionalCosts` 和 `destination`；P4.71 执行 `mode = "STANDBY_REVEAL"`、`destination = "BASE"`、`optionalCosts = ["STANDBY_REVEAL_0"]` 且无目标的待命基地显露路径，把已有正面朝下对象翻为公开状态；P4.76 额外执行 `mode = "STANDBY_REACTION"`、`destination = "STACK"`、`optionalCosts = ["STANDBY_REVEAL_0"]` 且无目标的优先权窗口路径，把正面朝下待命单位公开、移出基地并加入结算链；P4.386 追加 `p4-reveal-card-standby-reaction-teemo-self-power`，验证 `OGN·197/298 提莫` 的待命反应结算可复用已有 source-unit-to-base 与自身本回合战力 +3 事件；P4.95 补同一反应路径无优先权窗口拒绝 fixture：不推进 tick、不写事件、不翻开对象、不加入结算链；P4.318 补显露已知来源 `cardNo` mismatch rejection fixture，P4.348 补反应入栈已知来源 `cardNo` mismatch rejection fixture：正面朝下来源对象已有 `cardNo` 时必须匹配命令 `cardNo`，不推进 tick、不写事件、不翻开对象、不移动基地对象、不支付费用、不创建新的结算链项目；P4.319 补显露来源不在命令玩家基地拒绝 fixture，P4.347 补反应入栈来源不在命令玩家基地拒绝 fixture：不推进 tick、不写事件、不翻开对手基地对象、不移动任一基地对象、不支付费用、不创建新的结算链项目；P4.320 补显露来源已公开拒绝 fixture，P4.346 补反应入栈来源已公开拒绝 fixture：待命来源必须是正面朝下对象，不推进 tick、不写事件、不移动基地对象、不支付费用、不创建新的结算链项目；P4.321 补带目标拒绝 fixture：当前目标伤害分支仍 deferred，不推进 tick、不写事件、不翻开对象、不伤害目标、不创建结算链；P4.322 补显露 unsupported optional cost 拒绝 fixture，P4.345 补反应入栈 unsupported optional cost 拒绝 fixture：当前只接受 `STANDBY_REVEAL_0`，不推进 tick、不写事件、不翻开对象、不移动基地对象、不支付费用、不创建新的结算链项目；P4.341 补显露 missing reveal cost 拒绝 fixture，P4.344 补反应入栈 missing reveal cost 拒绝 fixture：当前不接受省略 `STANDBY_REVEAL_0`，不推进 tick、不写事件、不翻开对象、不移动基地对象、不支付费用、不创建新的结算链项目；P4.342 补已知非待命牌显露拒绝 fixture，P4.343 补已知非待命牌反应入栈拒绝 fixture：命令 `cardNo` 对应牌面必须暴露待命关键词，不推进 tick、不写事件、不翻开对象、不移动基地对象、不支付费用、不创建新的结算链项目；P4.323 补显露 unsupported destination 拒绝 fixture，P4.349 补反应入栈 unsupported destination 拒绝 fixture：当前只接受 `BASE` 显露和 `STACK` 反应入栈，不推进 tick、不写事件、不翻开对象、不移动对象、不创建结算链；P4.324 补反应入栈带目标拒绝 fixture：当前反应路径仍只接受无目标，不推进 tick、不写事件、不翻开对象、不伤害目标、不创建新的结算链项目；P4.390 追加横向审计测试，锁定 P4.321 / P4.324 两条带目标 deferred fixture 均保留官网卡面证据、规则证据和零副作用回放；待命目标伤害执行和完整隐藏区仍暂缓。
- Snapshot 对手视角下的正面朝下对象只暴露 `objectId` 与 `isFaceDown = true`；P4.69 不暴露其 `power`、`tags`、`manaCost` 等卡面/规则细节，拥有者视角仍保留完整对象信息。
- `cardObjects[objectId].cardNo` 是可选对象身份字段；P4.74 起源牌打出、待命暗置和待命显露会保存该字段，fixture 可在 `initialState.cardObjects` 或 `expected.finalState.cardObjects` 中断言它。对手视角的正面朝下对象仍不会暴露 `cardNo`。

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

schema v2 目前已支持 P2 初始状态和 expected 中的事件 tick/sequence/payload 局部匹配、turn/phase/timing、符文池、分数、经验、玩家区域、对象状态（含 `damage`、`power`、`untilEndOfTurnPowerModifier`、`untilEndOfTurnEffects`、`isFaceDown`、`isAttacking`、`isDefending`、`isExhausted`、`tags`、`manaCost`、`attachedToObjectId`、`ownerId`、`controllerId`）、全局 `untilEndOfTurnEffects`、`winnerPlayerId`，以及 FEPR/法术对决所需的 `priorityPlayerId`、`passedPriorityPlayerIds`、`stackItems`、P5.4/P5.5 触发队列快照 `triggerQueue` 与 `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` 事件、`focusPlayerId`、`passedFocusPlayerIds`。`initialState.seed` 已接入权威 `MatchState.seed`，先用于多张卡牌同时回收到主牌堆底部和燃尽回收洗匀时的可回放随机顺序。`CompareExpected` 已接入出牌与回合结束组合 fixture，下一步继续把更多 P2 fixture 从手写断言迁移到通用 expected diff。

P5.8 起，场上对象离场事件（如 `UNIT_DESTROYED`）可在 payload 中携带 `detachedEquipmentObjectIds`，用于锁定宿主离场时已贴附装备的引用清理。

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
