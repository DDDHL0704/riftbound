import { BehaviorSpec } from "../types/catalog";
import {
  ActionPromptDto,
  CardObjectView,
  GameEvent,
  PlayerSnapshotView,
  SnapshotDto,
  type ActionPromptComposerDto
} from "../types/protocol";

type FixtureCard = {
  cardNo: string;
  cardName: string;
  category: string;
  frontImage: string;
  mana?: number | null;
  power?: number | null;
  text?: string;
};

const fixtureCards = [
  card("UNL-181/219", "戏命师", "传奇", "https://cdn.playloltcg.com/lol/card/20260319/af04e77a6eea4a58ba2ccec4f94eb8a8.webp"),
  card("UNL-187/219", "皮城执法官", "传奇", "https://cdn.playloltcg.com/lol/card/20260319/18b9587cd7dc463bb9610453870c5c36.webp"),
  card("UNL-022/219", "烬", "英雄单位", "https://cdn.playloltcg.com/lol/card/20260319/3e84e2bd274c47d4b0642909f61e46ba.webp", 4, 4),
  card("UNL-030/219", "蔚", "英雄单位", "https://cdn.playloltcg.com/lol/card/20260319/beaf0126ad9f4001ae4330a9dbfc9742.webp", 4, 3),
  card("UNL-001/219", "竞技场理事", "单位", "https://cdn.playloltcg.com/lol/card/20260326/476c4f003a4449868e32b75f7e94a446.webp", 5, 3),
  card("UNL-002/219", "伊焚娜", "单位", "https://cdn.playloltcg.com/lol/card/20260319/479c288d279e44bb9e462d8dc470857b.webp", 2, 1),
  card("UNL-003/219", "鲛人滋事者", "单位", "https://cdn.playloltcg.com/lol/card/20260319/1155aa27333d4079806b8bde86f776b1.webp", 2, 2),
  card("UNL-006/219", "小鲨鱼", "单位", "https://cdn.playloltcg.com/lol/card/20260320/84bc57f9ba5941789d14942174614417.webp", 3, 1),
  card("UNL-008/219", "莽林巨象", "单位", "https://cdn.playloltcg.com/lol/card/20260320/a050510cae71433090ce267967360655.webp", 6, 6),
  card("UNL-011/219", "魔法鲜豆", "装备", "https://cdn.playloltcg.com/lol/card/20260319/f6de8aed9fd647f8a4a12c34168763fd.webp", 2),
  card("UNL-019/219", "枯萎战斧", "装备", "https://cdn.playloltcg.com/lol/card/20260320/7d138597c0844ab5864e58e29e950227.webp", 4, 4),
  card("UNL-007/219", "惩戒", "法术", "https://cdn.playloltcg.com/lol/card/20260319/46161dd31a0a4fb28d2ccc29dc5cec7f.webp", 2),
  card("UNL-009/219", "大幕渐起", "法术", "https://cdn.playloltcg.com/lol/card/20260319/1759d1e9ca134dbea8ff596d71938321.webp", 2),
  card("UNL-205/219", "废弃大厅", "战场", "https://cdn.playloltcg.com/lol/card/20260328/25b2ac5be09f4adb963ebe235838b1b9.webp"),
  card("UNL-206/219", "鲜血祭坛", "战场", "https://cdn.playloltcg.com/lol/card/20260328/4a9ceade8ae24844894c32d8b55ccc18.webp"),
  card("UNL-R01", "炽烈符文", "符文", "https://cdn.playloltcg.com/lol/card/20260402/5cd9c22ab0bd4160a62eef5369bc736c.webp"),
  card("UNL-R02", "翠意符文", "符文", "https://cdn.playloltcg.com/lol/card/20260402/cc71063b8abb40ef95ffbab78e72aceb.webp"),
  card("UNL-R03", "灵光符文", "符文", "https://cdn.playloltcg.com/lol/card/20260402/b122fe259abd430c90e675f208ac3920.webp"),
  card("UNL-R04", "摧破符文", "符文", "https://cdn.playloltcg.com/lol/card/20260402/843feaf0a8c7486b95c71c9bbcaee0a6.webp"),
  card("UNL-R05", "混沌符文", "符文", "https://cdn.playloltcg.com/lol/card/20260402/4335fc7a7fa1437dba7092da707d8430.webp"),
  card("UNL-R06", "序理符文", "符文", "https://cdn.playloltcg.com/lol/card/20260402/09d687b1468c42b29f6ee21f600b8ada.webp")
] satisfies FixtureCard[];

export const wireLayoutFixtureSpecByNo: Record<string, BehaviorSpec> = Object.fromEntries(
  fixtureCards.map((fixture) => [fixture.cardNo, toBehaviorSpec(fixture)])
);

const fixturePlayCardComposer = {
  commandFields: ["sourceObjectId", "cardNo", "targetObjectIds", "destination", "optionalCosts"],
  reason: "服务端 fixture 已公开 PLAY_CARD 命令模板；前端只组装选择并提交校验。",
  requiredSelectionRoles: ["source"],
  selectionRoles: ["destination", "optionalCost", "source", "target"],
  supported: true
} satisfies ActionPromptComposerDto;

const fixtureMoveUnitComposer = {
  commandFields: ["sourceObjectId", "origin", "destination", "optionalCosts"],
  reason: "服务端 fixture 已公开 MOVE_UNIT 命令模板；移动合法性由服务端最终裁定。",
  requiredSelectionRoles: ["source"],
  selectionRoles: ["destination", "optionalCost", "source"],
  supported: true
} satisfies ActionPromptComposerDto;

const fixtureTapRuneComposer = {
  commandFields: ["sourceObjectId"],
  reason: "服务端 fixture 已公开 TAP_RUNE 命令模板；资源状态由服务端快照决定。",
  requiredSelectionRoles: ["source"],
  selectionRoles: ["source"],
  supported: true
} satisfies ActionPromptComposerDto;

const fixtureDeclareBattleComposer = {
  commandFields: ["attackerObjectIds", "battlefieldId", "battlefieldTargetObjectIds", "defenderObjectIds", "optionalCosts"],
  reason: "服务端 fixture 已公开 DECLARE_BATTLE 命令模板；战场、攻击方、防守方仍由服务端规则校验。",
  requiredSelectionRoles: ["destination", "source", "target"],
  selectionRoles: ["destination", "optionalCost", "source", "target"],
  supported: true
} satisfies ActionPromptComposerDto;

const fixtureBlockedAbilityComposer = {
  commandFields: [],
  reason: "服务端 fixture 暂未开放该技能的组合提交入口。",
  requiredSelectionRoles: [],
  selectionRoles: [],
  supported: false
} satisfies ActionPromptComposerDto;

export function isWireLayoutFixtureEnabled(search = window.location.search): boolean {
  const params = new URLSearchParams(search);
  return params.get("fixture") === "layout" || params.get("layoutFixture") === "cards";
}

export function buildWireLayoutFixturePrompt(perspectivePlayerId: string): ActionPromptDto {
  const selfId = perspectivePlayerId || "P1";

  return {
    actionable: true,
    actions: ["PLAY_CARD", "MOVE_UNIT", "TAP_RUNE", "DECLARE_BATTLE", "PASS"],
    candidates: [
      {
        action: "PLAY_CARD",
        commandTemplate: {
          bindings: [
            { field: "sourceObjectId", label: "来源", required: true, source: "selectedSource" },
            { field: "cardNo", label: "服务端", metadataKeys: ["cardNo", "equipmentCardNo"], required: true, source: "requirementMetadata" },
            { field: "targetObjectIds", asArray: true, label: "目标", omitEmpty: false, source: "selectedTargets" },
            { field: "destination", label: "位置", source: "selectedDestination" },
            { field: "optionalCosts", asArray: true, label: "费用", source: "selectedOptionalCosts" }
          ],
          cmdType: "PLAY_CARD"
        },
        composer: fixturePlayCardComposer,
        destinations: [{ id: "STACK", label: "结算链" }],
        enabled: true,
        label: "打出手牌样例",
        metadata: {
          sourceRequirements: [
            {
              cardNo: "UNL-007/219",
              composable: true,
              destinationChoices: [{ id: "STACK", label: "结算链" }],
              displayName: "惩戒",
              maxTargetCount: 1,
              minTargetCount: 1,
              optionalCostChoices: [{ id: "RECYCLE_RUNE:p1-rune-2", label: "回收已抽出符文" }],
              paymentResourceChoices: [{ id: "POWER:p1-rune-3", label: "支付符能：灵光符文" }],
              sourceObjectId: "p1-hand-spell",
              targetChoicesByIndex: {
                "0": [
                  { id: "p2-right-1", label: "对方右战场单位" },
                  { id: "p2-left-1", label: "对方左战场单位" }
                ]
              }
            }
          ]
        },
        optionalCosts: [{ id: "RECYCLE_RUNE:p1-rune-2", label: "回收已抽出符文" }],
        reason: "前端线框样例；真实合法性由服务端规则窗口裁定。",
        selectionSteps: [
          {
            choices: [{ id: "p1-hand-spell", label: "手牌法术", objectIds: ["p1-hand-spell"] }],
            label: "来源",
            required: true,
            role: "source"
          },
          {
            choices: [{ id: "p2-right-1", label: "对方单位", objectIds: ["p2-right-1"] }],
            label: "目标",
            required: false,
            role: "target"
          },
          {
            choices: [{ id: "STACK", label: "结算链", objectIds: ["STACK"] }],
            label: "位置",
            required: false,
            role: "destination"
          },
          {
            choices: [{ id: "RECYCLE_RUNE:p1-rune-2", label: "回收已抽出符文", objectIds: ["RECYCLE_RUNE:p1-rune-2", "p1-rune-2"] }],
            label: "费用",
            required: false,
            role: "optionalCost"
          }
        ],
        sources: [{ id: "p1-hand-spell", label: "手牌法术" }],
        targets: [{ id: "p2-right-1", label: "对方单位" }]
      },
      {
        action: "MOVE_UNIT",
        commandTemplate: {
          bindings: [
            { field: "sourceObjectId", label: "来源", required: true, source: "selectedSource" },
            { field: "origin", label: "服务端", metadataKey: "origin", required: true, source: "requirementMetadata" },
            { field: "destination", label: "位置", source: "selectedDestination" },
            { field: "optionalCosts", asArray: true, label: "费用", source: "selectedOptionalCosts" }
          ],
          cmdType: "MOVE_UNIT"
        },
        composer: fixtureMoveUnitComposer,
        destinations: [{ id: "BATTLEFIELD:fixture-right-battlefield", label: "右战场" }],
        enabled: true,
        label: "移动单位样例",
        metadata: {
          sourceRequirements: [
            {
              composable: true,
              destinationChoices: [
                { id: "BATTLEFIELD:fixture-right-battlefield", label: "右战场" },
                { id: "BASE", label: "基地" }
              ],
              displayName: "小鲨鱼",
              mode: "MOVE",
              modeLabel: "移动",
              optionalCostChoices: [{ id: "ROAM", label: "游走费用" }],
              origin: "BATTLEFIELD:fixture-left-battlefield",
              originLabel: "左战场",
              requiredOptionalCosts: ["ROAM"],
              sourceObjectId: "p1-left-2"
            }
          ]
        },
        optionalCosts: [{ id: "ROAM", label: "游走费用" }],
        reason: "前端线框样例；移动窗口、费用和目标由服务端决定。",
        selectionSteps: [
          {
            choices: [{ id: "p1-left-2", label: "我方左战场单位", objectIds: ["p1-left-2"] }],
            label: "来源",
            required: true,
            role: "source"
          },
          {
            choices: [{ id: "BATTLEFIELD:fixture-right-battlefield", label: "右战场", objectIds: ["BATTLEFIELD:fixture-right-battlefield", "fixture-right-battlefield"] }],
            label: "位置",
            required: false,
            role: "destination"
          },
          {
            choices: [{ id: "ROAM", label: "游走费用", objectIds: ["ROAM"] }],
            label: "费用",
            required: false,
            role: "optionalCost"
          }
        ],
        sources: [{ id: "p1-left-2", label: "我方左战场单位" }]
      },
      {
        action: "TAP_RUNE",
        commandTemplate: {
          bindings: [{ field: "sourceObjectId", label: "来源", required: true, source: "selectedSource" }],
          cmdType: "TAP_RUNE"
        },
        composer: fixtureTapRuneComposer,
        enabled: true,
        label: "横置符文样例",
        reason: "前端线框样例；真实资源池由服务端快照决定。",
        sources: [{ id: "p1-rune-3", label: "未横置符文" }]
      },
      {
        action: "DECLARE_BATTLE",
        commandTemplate: {
          bindings: [
            { asArray: true, field: "attackerObjectIds", label: "攻击", required: true, source: "selectedSource" },
            { field: "battlefieldId", label: "战场", required: true, source: "selectedDestination" },
            { asArray: true, field: "battlefieldTargetObjectIds", label: "战场目标", required: true, source: "selectedDestination" },
            { asArray: true, field: "defenderObjectIds", label: "防守", required: true, source: "selectedTargets" },
            { asArray: true, field: "optionalCosts", label: "费用", source: "selectedOptionalCosts" }
          ],
          cmdType: "DECLARE_BATTLE"
        },
        composer: fixtureDeclareBattleComposer,
        destinations: [{ id: "fixture-right-battlefield", label: "右战场牌" }],
        enabled: true,
        label: "声明战斗样例",
        metadata: {
          sourceRequirements: [
            {
              cardNo: "UNL-001/219",
              battlefieldChoices: [{ id: "fixture-right-battlefield", label: "右战场牌" }],
              composable: true,
              displayName: "竞技场理事",
              maxAttackerCount: 1,
              maxDefenderCount: 2,
              minAttackerCount: 1,
              minDefenderCount: 1,
              optionalCostChoices: [{ id: "BATTLE_POWER:p1-rune-5", label: "战斗符能" }],
              paymentResourceChoices: [{ id: "POWER:p1-rune-5", label: "支付符能：混沌符文" }],
              sourceObjectId: "p1-right-1",
              targetChoicesByIndex: {
                "0": [
                  { id: "p2-right-1", label: "对方右战场单位 1" },
                  { id: "p2-right-2", label: "对方右战场单位 2" }
                ],
                "1": [
                  { id: "p2-right-2", label: "对方右战场单位 2" },
                  { id: "p2-right-3", label: "对方右战场单位 3" }
                ]
              }
            }
          ]
        },
        optionalCosts: [{ id: "BATTLE_POWER:p1-rune-5", label: "战斗符能" }],
        reason: "前端线框样例；声明、战场和防守方仍由服务端规则校验。",
        sources: [{ id: "p1-right-1", label: "我方右战场单位" }],
        targets: [{ id: "fixture-left-battlefield", label: "左战场牌" }]
      },
      {
        action: "ACTIVATE_ABILITY",
        enabled: false,
        label: "激活技能禁用样例",
        modes: [{ id: "ABILITY:p1-base-equip", label: "基地装备技能" }],
        reason: "前端线框样例；真实不可提交原因由服务端规则窗口提供。",
        sources: [{ id: "p1-base-equip", label: "基地装备" }],
        targets: [{ id: "p2-right-1", label: "对方右战场单位" }]
      },
      {
        action: "PASS",
        enabled: true,
        label: "让过",
        reason: "前端线框样例；真实窗口由服务端推进。"
      }
    ],
    contract: {
      candidateAction: "SERVER_CANDIDATE",
      hiddenMetadata: ["serverPaymentState", "ruleWindowState"],
      legalChoices: ["candidate.sources", "candidate.targets", "candidate.destinations", "candidate.optionalCosts", "candidate.selectionSteps"],
      promptKind: "MAIN_ACTION",
      requiredPayload: ["candidate.commandTemplate", "candidate.selectionSteps", "promptId", "snapshotTick"],
      validationErrors: ["INVALID_PAYLOAD", "PHASE_NOT_ALLOWED", "INVALID_TARGET", "INSUFFICIENT_COST", "PROMPT_EXPIRED"],
      visibleMetadata: ["sourceRequirements", "paymentResourceChoices", "targetChoicesByIndex"]
    },
    objectContexts: [
      {
        candidates: [
          {
            action: "PLAY_CARD",
            commandFields: ["来源:sourceObjectId*", "服务端字段*", "目标:targetObjectIds", "位置:destination", "费用:optionalCosts"],
            commandType: "PLAY_CARD",
            composer: fixturePlayCardComposer,
            enabled: true,
            label: "打出手牌样例",
            reason: "前端线框样例；真实合法性由服务端规则窗口裁定。",
            requiredCommandFields: ["来源:sourceObjectId*", "服务端字段*"],
            roles: ["来源"],
            selectionSteps: [
              { choiceCount: 1, index: 0, label: "来源", objectChoiceCount: 1, required: true, role: "source" },
              { choiceCount: 1, index: 1, label: "目标", objectChoiceCount: 0, required: false, role: "target" },
              { choiceCount: 1, index: 2, label: "位置", objectChoiceCount: 0, required: false, role: "destination" },
              { choiceCount: 1, index: 3, label: "费用", objectChoiceCount: 0, required: false, role: "optionalCost" }
            ]
          }
        ],
        disabledCandidateCount: 0,
        enabledCandidateCount: 1,
        inspection: {
          boundary: "服务端只公开当前行动提示中的对象候选、角色和命令字段；隐藏 metadata 与未公开卡牌身份不进入检查摘要。",
          groups: [
            {
              key: "candidate",
              rows: [
                {
                  key: "candidate-0",
                  label: "可提交",
                  tone: "good",
                  value: "PLAY_CARD / 来源 / 需 来源:sourceObjectId*/服务端字段* / 组合 服务端声明"
                }
              ],
              title: "服务端候选"
            },
            {
              key: "safe-boundary",
              rows: [
                {
                  key: "rules",
                  label: "规则判断",
                  tone: "neutral",
                  value: "由服务端候选与后续校验裁定，前端不重算"
                }
              ],
              title: "信息边界"
            }
          ],
          source: "server-action-prompt",
          summaryRows: [
            { key: "object", label: "对象", value: "p1-hand-spell" },
            { key: "candidate", label: "候选", value: "1 可提交 / 0 阻断" },
            { key: "source", label: "来源", value: "服务端检查摘要" }
          ]
        },
        objectId: "p1-hand-spell"
      },
      {
        candidates: [
          {
            action: "TAP_RUNE",
            commandFields: ["来源:sourceObjectId*"],
            commandType: "TAP_RUNE",
            composer: fixtureTapRuneComposer,
            enabled: true,
            label: "横置符文样例",
            reason: "前端线框样例；真实资源池由服务端快照决定。",
            requiredCommandFields: ["来源:sourceObjectId*"],
            roles: ["来源"]
          }
        ],
        disabledCandidateCount: 0,
        enabledCandidateCount: 1,
        objectId: "p1-rune-3"
      },
      {
        candidates: [
          {
            action: "MOVE_UNIT",
            commandFields: ["来源:sourceObjectId*", "服务端字段*", "位置:destination", "费用:optionalCosts"],
            commandType: "MOVE_UNIT",
            composer: fixtureMoveUnitComposer,
            enabled: true,
            label: "移动单位样例",
            reason: "前端线框样例；移动窗口、费用和目标由服务端决定。",
            requiredCommandFields: ["来源:sourceObjectId*", "服务端字段*"],
            roles: ["来源"]
          }
        ],
        disabledCandidateCount: 0,
        enabledCandidateCount: 1,
        objectId: "p1-left-2"
      },
      {
        candidates: [
          {
            action: "PLAY_CARD",
            commandFields: ["来源:sourceObjectId*", "服务端字段*", "目标:targetObjectIds", "位置:destination", "费用:optionalCosts"],
            commandType: "PLAY_CARD",
            composer: fixturePlayCardComposer,
            enabled: true,
            label: "打出手牌样例",
            reason: "前端线框样例；真实合法性由服务端规则窗口裁定。",
            requiredCommandFields: ["来源:sourceObjectId*", "服务端字段*"],
            roles: ["目标"],
            selectionSteps: [
              { choiceCount: 1, index: 0, label: "来源", objectChoiceCount: 0, required: true, role: "source" },
              { choiceCount: 1, index: 1, label: "目标", objectChoiceCount: 1, required: false, role: "target" },
              { choiceCount: 1, index: 2, label: "位置", objectChoiceCount: 0, required: false, role: "destination" },
              { choiceCount: 1, index: 3, label: "费用", objectChoiceCount: 0, required: false, role: "optionalCost" }
            ]
          },
          {
            action: "ACTIVATE_ABILITY",
            commandFields: ["来源:sourceObjectId*", "服务端字段*", "目标:targetObjectIds", "费用:optionalCosts"],
            commandType: "ACTIVATE_ABILITY",
            composer: fixtureBlockedAbilityComposer,
            enabled: false,
            label: "激活技能禁用样例",
            reason: "前端线框样例；真实不可提交原因由服务端规则窗口提供。",
            requiredCommandFields: ["来源:sourceObjectId*", "服务端字段*"],
            roles: ["目标"],
            selectionSteps: [
              { choiceCount: 1, index: 0, label: "来源", objectChoiceCount: 0, required: true, role: "source" },
              { choiceCount: 1, index: 1, label: "目标", objectChoiceCount: 1, required: true, role: "target" },
              { choiceCount: 1, index: 2, label: "费用", objectChoiceCount: 0, required: false, role: "optionalCost" }
            ]
          }
        ],
        disabledCandidateCount: 1,
        enabledCandidateCount: 1,
        objectId: "p2-right-1"
      }
    ],
    inspection: {
      boundary: "服务端只公开当前行动窗口的类型、候选、命令契约和对象索引摘要；隐藏 metadata、隐藏区内容和未公开卡牌身份不进入提示检查。",
      groups: [
        {
          key: "candidate",
          rows: [
            { key: "candidate-0", label: "可提交", tone: "good", value: "PLAY_CARD / 来源 1 / 目标 1 / 位置 1 / 费用 1 / 步骤 4" },
            { key: "candidate-1", label: "可提交", tone: "good", value: "MOVE_UNIT / 来源 1 / 位置 2 / 费用 1 / 步骤 3" },
            { key: "candidate-2", label: "阻断", tone: "warn", value: "ACTIVATE_ABILITY / 来源 1 / 目标 1 / 真实不可提交原因由服务端规则窗口提供。" }
          ],
          title: "服务端候选"
        },
        {
          key: "safe-boundary",
          rows: [
            { key: "candidate-source", label: "合法性", tone: "neutral", value: "仅以服务端候选和提交校验为准" },
            { key: "frontend", label: "前端职责", tone: "neutral", value: "展示与提交，不重算规则" }
          ],
          title: "信息边界"
        }
      ],
      source: "server-action-prompt",
      summaryRows: [
        { key: "kind", label: "提示类型", value: "MAIN_ACTION" },
        { key: "state", label: "窗口", tone: "good", value: "可行动" },
        { key: "candidate", label: "候选", value: "5 可提交 / 1 阻断" },
        { key: "objects", label: "对象索引", value: "4 个对象" }
      ]
    },
    playerId: selfId,
    promptId: "wire-layout-fixture-prompt",
    reason: "前端线框样例 prompt，不参与规则判断。",
    serverFlow: {
      actionableForPromptPlayer: true,
      candidateCount: 6,
      disabledCandidateCount: 1,
      enabledCandidateCount: 5,
      isResponsiblePlayer: true,
      lanes: [
        { count: 1, headline: "前端样例结算链", key: "stack", label: "结算链", state: "active" },
        { count: 1, headline: "前端样例规则任务", key: "task", label: "任务", state: "active" },
        { count: 0, headline: "无触发", key: "trigger", label: "触发", state: "empty" },
        { count: 1, headline: "最近一次战场结算", key: "resolution", label: "结算", state: "history" }
      ],
      nextStep: "根据服务端公开的关联对象选择来源、目标或费用。",
      primaryLabel: "服务端关联对象",
      promptPlayerId: selfId,
      promptType: "MAIN_ACTION",
      queueCounts: {
        resolution: 1,
        stack: 1,
        task: 1,
        trigger: 0
      },
      reason: "服务端提示流公开了当前可操作对象和规则关联对象。",
      relatedObjectIds: ["p1-hand-spell", "p2-right-1", "fixture-left-battlefield"],
      relatedObjects: [
        {
          candidateBoundary: "服务端只公开当前行动提示中的对象候选、选择步骤和命令字段；隐藏 metadata 与未公开卡牌身份不进入检查摘要。",
          candidateRoles: ["来源"],
          candidateSteps: [
            { choiceCount: 1, index: 0, label: "来源", objectChoiceCount: 1, required: true, role: "source" },
            { choiceCount: 1, index: 1, label: "目标", objectChoiceCount: 0, required: false, role: "target" },
            { choiceCount: 1, index: 2, label: "位置", objectChoiceCount: 0, required: false, role: "destination" },
            { choiceCount: 1, index: 3, label: "费用", objectChoiceCount: 0, required: false, role: "optionalCost" }
          ],
          candidateSource: "server-action-prompt",
          disabledCandidateCount: 0,
          enabledCandidateCount: 1,
          objectId: "p1-hand-spell",
          role: "候选来源"
        },
        {
          candidateBoundary: "服务端只公开当前行动提示中的对象候选、选择步骤和命令字段；隐藏 metadata 与未公开卡牌身份不进入检查摘要。",
          candidateRoles: ["目标"],
          candidateSteps: [
            { choiceCount: 1, index: 0, label: "来源", objectChoiceCount: 0, required: true, role: "source" },
            { choiceCount: 1, index: 1, label: "目标", objectChoiceCount: 1, required: false, role: "target" },
            { choiceCount: 1, index: 2, label: "位置", objectChoiceCount: 0, required: false, role: "destination" },
            { choiceCount: 1, index: 3, label: "费用", objectChoiceCount: 0, required: false, role: "optionalCost" }
          ],
          candidateSource: "server-action-prompt",
          disabledCandidateCount: 1,
          enabledCandidateCount: 1,
          objectId: "p2-right-1",
          role: "候选目标"
        },
        { objectId: "fixture-left-battlefield", role: "相关战场" }
      ],
      responsiblePlayerId: selfId,
      state: "ready",
      stateLabel: "可提交",
      steps: [
        {
          detail: "候选命令仍以服务端返回的 action candidates 为准。",
          key: "candidate",
          label: "候选",
          role: "candidate",
          state: "ready",
          stateLabel: "可提交",
          value: "5 项"
        },
        {
          detail: "桌面对象只展示服务端已经公开的关联关系。",
          key: "related",
          label: "关联对象",
          role: "related",
          state: "ready",
          stateLabel: "已公开",
          value: "3 个"
        }
      ],
      summary: "主行动 / 服务端关联对象 / 可提交",
      tone: "good"
    },
    snapshotTick: 9001,
    view: {
      message: "展示点击卡牌、候选行动、目标/费用和规则队列的线框占位。",
      relatedBattlefieldId: "fixture-left-battlefield",
      responsibility: {
        actionableForPromptPlayer: true,
        isResponsiblePlayer: true,
        nextStep: "根据服务端候选处理主行动。",
        promptPlayerId: selfId,
        promptType: "MAIN_ACTION",
        queueCounts: {
          activeBattle: 1,
          pendingHandChoice: 0,
          pendingPayment: 0,
          pendingTasks: 1,
          stack: 1,
          triggerQueue: 2
        },
        relatedObjectIds: ["fixture-left-battlefield"],
        responsiblePlayerId: selfId,
        state: "PLAYER_ACTION"
      },
      title: "前端线框交互样例",
      type: "MAIN_ACTION"
    }
  };
}

export function buildWireLayoutFixtureEvents(perspectivePlayerId: string): GameEvent[] {
  const selfId = perspectivePlayerId || "P1";
  const opponentId = selfId === "P2" ? "P1" : "P2";

  return [
    {
      description: "惩戒加入结算链，目标为右战场对方单位。",
      kind: "STACK_ITEM_ADDED",
      objectRefs: [
        { cardNo: "UNL-007/219", objectId: "p1-hand-spell", role: "来源" },
        { cardNo: "UNL-008/219", objectId: "p2-right-1", role: "目标" }
      ],
      payload: {
        controllerId: selfId,
        sourceObjectId: "p1-hand-spell",
        stackItemId: "fixture-stack-1",
        targetObjectIds: ["p2-right-1"]
      }
    },
    {
      description: "左战场控制结算完成。",
      kind: "BATTLEFIELD_CONTROL_RESOLVED",
      objectRefs: [
        { cardNo: "UNL-205/219", objectId: "fixture-left-battlefield", role: "战场" },
        { cardNo: "UNL-002/219", objectId: "p1-left-1", role: "参与" },
        { cardNo: "UNL-030/219", objectId: "p2-left-1", role: "参与" }
      ],
      payload: {
        battlefieldObjectId: "fixture-left-battlefield",
        controllerId: selfId,
        participantObjectIds: ["p1-left-1", "p2-left-1"],
        previousControllerId: opponentId
      }
    },
    {
      description: "右战场战斗结算，无胜者。",
      kind: "BATTLE_NO_RESULT",
      objectRefs: [
        { cardNo: "UNL-206/219", objectId: "fixture-right-battlefield", role: "战场" },
        { cardNo: "UNL-001/219", objectId: "p1-right-1", role: "攻击" },
        { cardNo: "UNL-008/219", objectId: "p2-right-1", role: "防守" },
        { cardNo: "UNL-002/219", objectId: "p2-right-2", role: "被摧毁" }
      ],
      payload: {
        attackerObjectIds: ["p1-right-1"],
        battlefieldId: "fixture-right-battlefield",
        defenderObjectIds: ["p2-right-1"],
        destroyedObjectIds: ["p2-right-2"]
      }
    }
  ];
}

export function buildWireLayoutFixtureSnapshot(perspectivePlayerId: string): SnapshotDto {
  const selfId = perspectivePlayerId || "P1";
  const opponentId = selfId === "P2" ? "P1" : "P2";

  const self = buildPlayer(selfId, "P1 视觉样例", [
    obj("p1-legend", "UNL-181/219", selfId),
    obj("p1-hero", "UNL-022/219", selfId),
    obj("p1-base-unit", "UNL-001/219", selfId),
    obj("p1-base-equip", "UNL-011/219", selfId),
    obj("p1-base-spell", "UNL-009/219", selfId),
    obj("p1-rune-1", "UNL-R01", selfId),
    obj("p1-rune-2", "UNL-R02", selfId, selfId, { isExhausted: true }),
    obj("p1-rune-3", "UNL-R03", selfId),
    obj("p1-rune-4", "UNL-R04", selfId, selfId, { isExhausted: true }),
    obj("p1-rune-5", "UNL-R05", selfId),
    obj("p1-rune-6", "UNL-R06", selfId),
    obj("p1-hand-unit", "UNL-003/219", selfId),
    obj("p1-hand-spell", "UNL-007/219", selfId),
    obj("p1-hand-equip", "UNL-019/219", selfId),
    obj("p1-grave-spell", "UNL-009/219", selfId),
    obj("p1-grave-equip", "UNL-011/219", selfId),
    obj("p1-banished-spell", "UNL-007/219", selfId),
    obj("p1-left-1", "UNL-002/219", selfId),
    obj("p1-left-2", "UNL-006/219", selfId, selfId, { isExhausted: true }),
    obj("p1-left-3", "UNL-008/219", selfId),
    obj("p1-left-4", "UNL-019/219", selfId),
    obj("p1-left-standby", "UNL-009/219", selfId, selfId, { location: { battlefieldObjectId: "fixture-left-battlefield", playerId: selfId, zone: "BATTLEFIELD" } }),
    obj("p1-right-1", "UNL-001/219", selfId),
    obj("p1-right-2", "UNL-003/219", selfId),
    obj("p1-right-3", "UNL-022/219", selfId),
    obj("p1-right-standby", "UNL-007/219", selfId, selfId, { location: { battlefieldObjectId: "fixture-right-battlefield", playerId: selfId, zone: "BATTLEFIELD" } })
  ], {
    base: ["p1-base-unit", "p1-base-equip", "p1-base-spell", "p1-rune-1", "p1-rune-2", "p1-rune-3", "p1-rune-4", "p1-rune-5", "p1-rune-6"],
    baseCards: ["p1-base-unit", "p1-base-equip", "p1-base-spell"],
    baseRunes: ["p1-rune-1", "p1-rune-2", "p1-rune-3", "p1-rune-4", "p1-rune-5", "p1-rune-6"],
    banished: ["p1-banished-spell"],
    championZone: ["p1-hero"],
    graveyard: ["p1-grave-spell", "p1-grave-equip"],
    hand: ["p1-hand-unit", "p1-hand-spell", "p1-hand-equip"],
    legendZone: ["p1-legend"],
    mainDeckCount: 31,
    runeDeckCount: 6
  });

  const opponent = buildPlayer(opponentId, "P2 视觉样例", [
    obj("p2-legend", "UNL-187/219", opponentId),
    obj("p2-hero", "UNL-030/219", opponentId),
    obj("p2-base-unit", "UNL-006/219", opponentId),
    obj("p2-base-equip", "UNL-019/219", opponentId),
    obj("p2-rune-1", "UNL-R06", opponentId),
    obj("p2-rune-2", "UNL-R05", opponentId, opponentId, { isExhausted: true }),
    obj("p2-rune-3", "UNL-R04", opponentId),
    obj("p2-rune-4", "UNL-R03", opponentId),
    obj("p2-rune-5", "UNL-R02", opponentId, opponentId, { isExhausted: true }),
    obj("p2-grave-spell", "UNL-007/219", opponentId),
    obj("p2-banished-equip", "UNL-011/219", opponentId),
    obj("p2-left-1", "UNL-030/219", opponentId),
    obj("p2-left-2", "UNL-001/219", opponentId),
    obj("p2-left-3", "UNL-003/219", opponentId, opponentId, { isExhausted: true }),
    obj("p2-right-1", "UNL-008/219", opponentId),
    obj("p2-right-2", "UNL-002/219", opponentId),
    obj("p2-right-3", "UNL-006/219", opponentId),
    obj("p2-right-4", "UNL-019/219", opponentId),
    obj("p2-right-standby", "UNL-011/219", opponentId, opponentId, { location: { battlefieldObjectId: "fixture-right-battlefield", playerId: opponentId, zone: "BATTLEFIELD" } })
  ], {
    base: ["p2-base-unit", "p2-base-equip", "p2-rune-1", "p2-rune-2", "p2-rune-3", "p2-rune-4", "p2-rune-5"],
    baseCards: ["p2-base-unit", "p2-base-equip"],
    baseRunes: ["p2-rune-1", "p2-rune-2", "p2-rune-3", "p2-rune-4", "p2-rune-5"],
    banished: ["p2-banished-equip"],
    championZone: ["p2-hero"],
    graveyard: ["p2-grave-spell"],
    handHidden: 5,
    legendZone: ["p2-legend"],
    mainDeckCount: 33,
    runeDeckCount: 7
  });

  return {
    activePlayerId: selfId,
    lanes: {
      battlefields: [
        {
          battlefieldObjectId: "fixture-left-battlefield",
          cardNo: "UNL-205/219",
          controllerId: selfId,
          hiddenStandbyCount: 1,
          occupantObjectIds: ["p2-left-1", "p2-left-2", "p2-left-3", "p1-left-1", "p1-left-2", "p1-left-3", "p1-left-4"],
          scoredThisTurn: false,
          status: "CONTESTED",
          standbySlotCount: 2,
          standbySlots: [
            {
              battlefieldObjectId: "fixture-left-battlefield",
              controllerId: selfId,
              isFaceDown: false,
              objectId: "p1-left-standby",
              sidePlayerId: selfId,
              slotId: "fixture-left-battlefield:standby:1",
              state: "VISIBLE",
              visible: true
            },
            {
              battlefieldObjectId: "fixture-left-battlefield",
              controllerId: opponentId,
              isFaceDown: false,
              sidePlayerId: opponentId,
              slotId: "fixture-left-battlefield:standby:2",
              state: "HIDDEN",
              visible: false
            }
          ],
          unitsBySide: {
            [opponentId]: ["p2-left-1", "p2-left-2", "p2-left-3"],
            [selfId]: ["p1-left-1", "p1-left-2", "p1-left-3", "p1-left-4"]
          },
          zonePlayerId: selfId
        },
        {
          battlefieldObjectId: "fixture-right-battlefield",
          cardNo: "UNL-206/219",
          controllerId: opponentId,
          hiddenStandbyCount: 0,
          occupantObjectIds: ["p2-right-1", "p2-right-2", "p2-right-3", "p2-right-4", "p1-right-1", "p1-right-2", "p1-right-3"],
          scoredThisTurn: true,
          scoredThisTurnPlayerIds: [opponentId],
          status: "CONTROLLED",
          standbySlotCount: 2,
          standbySlots: [
            {
              battlefieldObjectId: "fixture-right-battlefield",
              controllerId: opponentId,
              isFaceDown: false,
              objectId: "p2-right-standby",
              sidePlayerId: opponentId,
              slotId: "fixture-right-battlefield:standby:1",
              state: "VISIBLE",
              visible: true
            },
            {
              battlefieldObjectId: "fixture-right-battlefield",
              controllerId: selfId,
              isFaceDown: true,
              objectId: "p1-right-standby",
              sidePlayerId: selfId,
              slotId: "fixture-right-battlefield:standby:2",
              state: "VISIBLE",
              visible: true
            }
          ],
          unitsBySide: {
            [opponentId]: ["p2-right-1", "p2-right-2", "p2-right-3", "p2-right-4"],
            [selfId]: ["p1-right-1", "p1-right-2", "p1-right-3"]
          },
          zonePlayerId: opponentId
        }
      ]
    },
    players: {
      [selfId]: self,
      [opponentId]: opponent
    },
    stack: [
      {
        cardNo: "UNL-007/219",
        controllerId: selfId,
        damageAmount: 2,
        effectKind: "SPELL",
        sourceObjectId: "p1-hand-spell",
        destination: "GRAVEYARD",
        stackItemId: "fixture-stack-1",
        targetObjectIds: ["p2-right-1"]
      },
      {
        cardNo: "UNL-011/219",
        controllerId: opponentId,
        effectKind: "ABILITY",
        sourceObjectId: "p2-banished-equip",
        stackItemId: "fixture-stack-2",
        targetObjectIds: ["p2-right-2", "p1-right-1"]
      }
    ],
    tick: 9001,
    timing: {
      battleResolutions: [
        {
          attackerObjectIds: ["p1-right-1"],
          battlefieldId: "fixture-left-battlefield",
          defenderObjectIds: ["p2-right-1"],
          destroyedObjectIds: ["p2-right-2"],
          kind: "NO_RESULT",
          relatedEventKinds: ["BATTLE_CLOSED"],
          resolutionId: "fixture-battle-resolution-1",
          tick: 9000,
          winnerPlayerId: null
        }
      ],
      battlefieldResolutions: [
        {
          battlefieldObjectId: "fixture-left-battlefield",
          controllerId: selfId,
          kind: "CONTROL_RESOLVED",
          participantObjectIds: ["p1-left-1", "p2-left-1"],
          playerId: selfId,
          previousControllerId: opponentId,
          reason: "前端线框样例：服务端战场控制结果。",
          relatedEventKinds: ["BATTLEFIELD_CONTROL_RESOLVED"],
          resolutionId: "fixture-battlefield-resolution-1",
          sourceObjectId: "fixture-left-battlefield",
          tick: 9000
        }
      ],
      pendingTaskQueue: {
        activeTaskId: "fixture-task-1",
        isBlocking: true,
        phase: "BATTLEFIELD_TASKS",
        tasks: [
          {
            battlefieldObjectId: "fixture-left-battlefield",
            kind: "BATTLEFIELD_CONTESTED",
            participantObjectIds: ["p1-left-1", "p2-left-1"],
            reason: "BATTLEFIELD_CONTESTED",
            status: "PENDING",
            taskId: "fixture-task-1"
          },
          {
            actingPlayerId: selfId,
            battleId: "fixture-battle-right",
            battlefieldObjectId: "fixture-right-battlefield",
            kind: "START_BATTLE",
            participantControllerIds: [selfId, opponentId],
            participantObjectIds: ["p1-right-1", "p2-right-1"],
            reason: "BATTLE_CLEANUP",
            stackItemIds: ["fixture-stack-1"],
            status: "READY",
            taskId: "fixture-task-2"
          }
        ]
      },
      phase: "MAIN",
      roomStatus: "VISUAL_FIXTURE",
      timingState: "MAIN_ACTION",
      triggerQueue: [
        {
          controllerId: selfId,
          effectKind: "TRIGGER",
          sourceObjectId: "p1-left-1",
          triggerId: "fixture-trigger-1",
          triggeredByEventKind: "BATTLEFIELD_HELD"
        },
        {
          controllerId: opponentId,
          effectKind: "TRIGGER",
          sourceObjectId: "HIDDEN",
          sourceVisibility: "HIDDEN",
          triggerId: "fixture-trigger-2",
          triggeredByEventKind: "BATTLEFIELD_CONQUERED"
        }
      ],
      turnWindow: { state: "MAIN_ACTION" }
    },
    turnNumber: 7,
    turnState: "MAIN_ACTION"
  };
}

function card(cardNo: string, cardName: string, category: string, frontImage: string, mana?: number | null, power?: number | null): FixtureCard {
  return { cardNo, cardName, category, frontImage, mana, power };
}

function toBehaviorSpec(fixture: FixtureCard): BehaviorSpec {
  return {
    activatedAbilities: [],
    cardCategoryName: fixture.category,
    cardName: fixture.cardName,
    cardNo: fixture.cardNo,
    conformanceReason: "前端布局样例牌，不参与规则判定。",
    conformanceTier: "layout-fixture",
    cost: { additionalCosts: [], mana: fixture.mana ?? null, optionalCosts: [], power: null, returnEnergy: null },
    effects: [],
    frontImage: fixture.frontImage,
    backImage: "",
    functionalUnitId: "wire-layout-fixture",
    implementedEffectKind: null,
    implementedByCardNo: null,
    keywords: [],
    officialText: fixture.text ?? "",
    reason: "前端布局样例牌，不参与规则判定。",
    replacements: [],
    staticAbilities: [],
    status: "fixture",
    targets: [],
    templateIds: [],
    triggers: []
  };
}

function buildPlayer(id: string, name: string, objects: CardObjectView[], zones: NonNullable<PlayerSnapshotView["zones"]>): PlayerSnapshotView {
  return {
    cardsPlayedThisTurn: 2,
    deckSubmitted: true,
    experience: 0,
    handSize: zones.hand?.length ?? zones.handHidden ?? 0,
    id,
    mulliganCompleted: true,
    name,
    objects: Object.fromEntries(objects.map((object) => [object.objectId ?? object.cardNo ?? crypto.randomUUID(), object])),
    ready: true,
    runePool: { mana: 12, power: 3, powerByTrait: { 红色: 2, 蓝色: 1 }, totalPower: 3, untypedPower: 0 },
    score: id.startsWith("P2") ? 4 : 5,
    seat: id.startsWith("P2") ? "opponent" : "self",
    zones
  };
}

function obj(
  objectId: string,
  cardNo: string,
  ownerId: string,
  controllerId = ownerId,
  overrides: Partial<CardObjectView> = {}
): CardObjectView {
  const spec = wireLayoutFixtureSpecByNo[cardNo];
  return {
    basePower: fixtureCards.find((cardSpec) => cardSpec.cardNo === cardNo)?.power ?? undefined,
    cardNo,
    controllerId,
    effectivePower: fixtureCards.find((cardSpec) => cardSpec.cardNo === cardNo)?.power ?? undefined,
    isExhausted: false,
    objectId,
    ownerId,
    power: fixtureCards.find((cardSpec) => cardSpec.cardNo === cardNo)?.power ?? undefined,
    tags: spec?.cardCategoryName === "符文" ? ["CARD_TYPE:RUNE"] : [],
    ...overrides
  };
}
