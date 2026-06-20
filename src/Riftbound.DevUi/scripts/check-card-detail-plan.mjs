import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/cardDetailPlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function(
  "exports",
  "module",
  "sourceCandidatesForPrompt",
  "conformanceLabel",
  "conformanceTone",
  "costText",
  "keywordsText",
  "objectTypeText",
  "rulesText",
  "statusLabel",
  "isHiddenObject",
  "tableObjectCandidateSourceLabel",
  "tableObjectContextSourceLabel",
  output
)(
  moduleShim.exports,
  moduleShim,
  sourceCandidatesForPrompt,
  conformanceLabel,
  conformanceTone,
  costText,
  keywordsText,
  objectTypeText,
  rulesText,
  statusLabel,
  isHiddenObject,
  tableObjectCandidateSourceLabel,
  tableObjectContextSourceLabel
);

const { buildCardDetailPlan } = moduleShim.exports;

const hiddenPlan = buildCardDetailPlan({
  card: {
    object: { isFaceDown: true, objectId: "hidden-card" },
    objectId: "hidden-card"
  },
  prompt: {
    candidates: [
      {
        action: "PLAY_CARD",
        enabled: true,
        label: "不应出现",
        reason: "隐藏对象不能前端推断",
        sources: [{ id: "hidden-card", label: "隐藏来源" }]
      }
    ]
  }
});

assert.equal(hiddenPlan.hidden, true);
assert.equal(hiddenPlan.title, "未公开卡牌");
assert.equal(hiddenPlan.actionCandidates.length, 0);
assert.equal(hiddenPlan.badges[0].label, "隐藏信息");
assert.ok(hiddenPlan.hiddenMessage.includes("不读取或推断"));
assert.equal(hiddenPlan.inspector.summaryRows.find((row) => row.key === "visibility")?.value, "隐藏信息");
assert.equal(hiddenPlan.inspector.authorityState, "hidden");
assert.equal(hiddenPlan.inspector.authorityLabel, "隐藏信息边界");
assert.equal(hiddenPlan.inspector.sourceLabel, "隐藏对象安全外壳");
assert.equal(hiddenPlan.inspector.groups[0].rows.find((row) => row.key === "rules")?.value, "未公开");
assert.equal(JSON.stringify(hiddenPlan).includes("PLAY_CARD"), false);

const visiblePlan = buildCardDetailPlan({
  card: {
    object: {
      basePower: 3,
      cardNo: "OGN-001/298",
      controllerId: "P1",
      damage: 1,
      effectivePower: 4,
      location: { playerId: "P1", zone: "HAND" },
      objectId: "p1-hand-spell",
      ownerId: "P1"
    },
    objectId: "p1-hand-spell",
    spec: {
      cardCategoryName: "法术",
      cardName: "测试法术",
      cardNo: "OGN-001/298",
      conformanceTier: "representative-rule-pass",
      cost: { mana: 2, power: 1 },
      frontImage: "",
      keywords: [{ keyword: "迅捷" }],
      officialText: "造成 {{S}} 点伤害。",
      status: "implemented"
    }
  },
  objectContext: {
    candidateLinks: [
      {
        commandFields: ["来源"],
        commandType: "PLAY_CARD",
        composerReason: "服务端已公开组合提交。",
        composerState: "server",
        composerStateLabel: "服务端声明",
        enabled: true,
        label: "打出手牌",
        reason: "可提交",
        requiredCommandFields: ["sourceObjectId"],
        roles: ["来源"],
        selectionSteps: [
          { choiceCount: 1, index: 0, label: "来源", objectChoiceCount: 1, required: true, role: "source" },
          { choiceCount: 1, index: 1, label: "目标", objectChoiceCount: 0, required: false, role: "target" }
        ]
      },
      {
        commandFields: ["目标"],
        commandType: "ACTIVATE_ABILITY",
        composerReason: "服务端暂未开放组合提交。",
        composerState: "blocked",
        composerStateLabel: "服务端阻断",
        enabled: false,
        label: "激活能力",
        reason: "缺少合法目标",
        requiredCommandFields: ["targetObjectIds"],
        roles: ["目标"],
        selectionSteps: [
          { choiceCount: 1, index: 0, label: "来源", objectChoiceCount: 1, required: true, role: "source" },
          { choiceCount: 0, index: 1, label: "目标", objectChoiceCount: 0, required: true, role: "target" }
        ]
      }
    ],
    candidateSource: "server",
    cardNo: "OGN-001/298",
    controllerId: "P1",
    contextBoundary: "服务端对象上下文只公开当前行动提示中的对象候选、选择角色和命令字段；隐藏 metadata 不进入对象上下文。",
    contextSource: "server-action-prompt",
    eventLinks: [{ description: "测试法术加入结算链", kind: "STACK_ITEM_ADDED", role: "来源" }],
    objectId: "p1-hand-spell",
    ownerId: "P1",
    promptDisabledCount: 1,
    promptEnabledCount: 1,
    serverInspection: {
      boundary: "服务端只公开当前行动提示中的对象候选、角色和命令字段；隐藏 metadata 与未公开卡牌身份不进入检查摘要。",
      groups: [
        {
          key: "candidate",
          title: "服务端候选",
          rows: [
            { key: "candidate-0", label: "可提交", tone: "good", value: "PLAY_CARD / 来源 / 需 sourceObjectId" },
            { key: "candidate-1", label: "阻断", tone: "warn", value: "ACTIVATE_ABILITY / 目标 / 缺少合法目标" }
          ]
        },
        {
          key: "safe-boundary",
          title: "信息边界",
          rows: [
            { key: "rules", label: "规则判断", tone: "neutral", value: "由服务端候选与后续校验裁定，前端不重算" }
          ]
        }
      ],
      source: "server-action-prompt",
      summaryRows: [
        { key: "object", label: "对象", value: "p1-hand-spell" },
        { key: "candidate", label: "候选", value: "1 可提交 / 1 阻断" },
        { key: "source", label: "来源", value: "服务端检查摘要" }
      ]
    },
    stackRoles: ["结算链来源"],
    stateLabels: ["4 战力"],
    zone: { kind: "hand", label: "我方手牌", playerId: "P1" }
  },
  prompt: {
    actionable: true,
    candidates: [
      {
        action: "PLAY_CARD",
        enabled: true,
        label: "打出手牌",
        reason: "可提交",
        sources: [{ id: "p1-hand-spell", label: "测试法术" }]
      },
      {
        action: "ACTIVATE_ABILITY",
        enabled: false,
        label: "激活能力",
        reason: "缺少合法目标",
        sources: [{ id: "p1-hand-spell", label: "测试法术" }]
      },
      {
        action: "MOVE_UNIT",
        enabled: true,
        label: "移动单位",
        reason: "错误来源",
        sources: [{ id: "other-object", label: "其他对象" }]
      }
    ],
    playerId: "P1",
    promptId: "prompt-1",
    reason: "测试窗口",
    snapshotTick: 12
  }
});

assert.equal(visiblePlan.hidden, false);
assert.equal(visiblePlan.title, "测试法术");
assert.equal(visiblePlan.detailRows.find((row) => row.key === "zone")?.value, "我方手牌");
assert.equal(visiblePlan.detailRows.find((row) => row.key === "power")?.value, "4");
assert.equal(visiblePlan.sections.find((section) => section.key === "keywords")?.body, "迅捷");
assert.ok(visiblePlan.sections.find((section) => section.key === "rules")?.body.includes("战力"));
assert.ok(visiblePlan.sections.find((section) => section.key === "evidence")?.body.includes("代表性规则通过"));
assert.ok(visiblePlan.sections.find((section) => section.key === "state")?.body.includes("1 伤害"));
assert.equal(visiblePlan.actionCandidates.length, 2);
assert.equal(visiblePlan.actionCandidates[0].action, "PLAY_CARD");
assert.equal(visiblePlan.actionCandidates[1].action, "ACTIVATE_ABILITY");
assert.equal(visiblePlan.actionCandidates[1].enabled, false);
assert.equal(visiblePlan.inspector.summaryRows.find((row) => row.key === "zone")?.value, "我方手牌");
assert.equal(visiblePlan.inspector.summaryRows.find((row) => row.key === "candidate")?.value, "1 可提交 / 1 阻断");
assert.equal(visiblePlan.inspector.summaryRows.find((row) => row.key === "source")?.value, "服务端对象上下文");
assert.equal(visiblePlan.inspector.summaryRows.find((row) => row.key === "authority")?.value, "服务端对象上下文");
assert.equal(visiblePlan.inspector.authorityState, "server-inspection");
assert.equal(visiblePlan.inspector.authorityLabel, "服务端检查摘要");
assert.equal(visiblePlan.inspector.sourceLabel, "服务端检查摘要");
assert.ok(visiblePlan.inspector.groups.find((group) => group.key === "identity")?.rows.some((row) => row.value.includes("prompt-1")));
assert.ok(visiblePlan.inspector.groups.find((group) => group.key === "candidate")?.rows.some((row) => row.value.includes("缺少合法目标")));
assert.ok(visiblePlan.inspector.groups.find((group) => group.key === "selection-steps")?.rows.some((row) => row.value.includes("来源* 1/1")));
assert.ok(visiblePlan.inspector.groups.find((group) => group.key === "safe-boundary")?.rows.some((row) => row.value.includes("前端不重算")));
assert.ok(visiblePlan.inspector.groups.find((group) => group.key === "stack")?.rows.some((row) => row.value === "结算链来源"));
assert.ok(visiblePlan.inspector.groups.find((group) => group.key === "events")?.rows.some((row) => row.value.includes("STACK_ITEM_ADDED")));

console.log("Card detail plan check passed.");

function sourceCandidatesForPrompt(prompt, sourceObjectId, options = {}) {
  const enabledOnly = options.enabledOnly ?? true;
  return (prompt?.candidates ?? []).filter((candidate) =>
    (!enabledOnly || candidate.enabled)
    && (candidate.sources ?? []).some((source) => source.id === sourceObjectId));
}

function conformanceLabel(value) {
  return value === "representative-rule-pass" ? "代表性规则通过" : "服务端证据";
}

function conformanceTone() {
  return "info";
}

function costText(spec) {
  const cost = spec?.cost;
  return cost ? `${cost.mana ?? 0} 法力 / ${cost.power ?? 0} 符能` : "费用未知";
}

function keywordsText(spec) {
  return spec?.keywords?.map((keyword) => keyword.keyword).join("、") || "无关键词";
}

function objectTypeText(_object, spec) {
  return spec?.cardCategoryName ?? "对象";
}

function rulesText(value) {
  return (value ?? "服务端未提供卡面规则文本。").replace("{{S}}", "战力");
}

function statusLabel(value) {
  return value === "implemented" ? "已实现代表路径" : "未知";
}

function isHiddenObject(object) {
  return !object || object.isFaceDown === true || !object.cardNo;
}

function tableObjectCandidateSourceLabel(source) {
  switch (source) {
    case "server":
      return "服务端对象上下文";
    case "derived":
      return "公开候选只读派生";
    case "none":
      return "无候选上下文";
    default:
      return "未建立上下文";
  }
}

function tableObjectContextSourceLabel(context) {
  switch (context?.contextSource) {
    case "server-action-prompt":
      return "服务端对象上下文";
    case "prompt-public-derived":
      return "公开候选只读派生";
    case "snapshot-public-index":
      return "公开快照索引";
    default:
      return tableObjectCandidateSourceLabel(context?.candidateSource);
  }
}
