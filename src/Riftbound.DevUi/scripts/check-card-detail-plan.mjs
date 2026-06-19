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
  isHiddenObject
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
        enabled: true,
        label: "打出手牌",
        reason: "可提交",
        requiredCommandFields: ["sourceObjectId"],
        roles: ["来源"]
      },
      {
        commandFields: ["目标"],
        commandType: "ACTIVATE_ABILITY",
        enabled: false,
        label: "激活能力",
        reason: "缺少合法目标",
        requiredCommandFields: ["targetObjectIds"],
        roles: ["目标"]
      }
    ],
    candidateSource: "server",
    cardNo: "OGN-001/298",
    controllerId: "P1",
    eventLinks: [{ description: "测试法术加入结算链", kind: "STACK_ITEM_ADDED", role: "来源" }],
    objectId: "p1-hand-spell",
    ownerId: "P1",
    promptDisabledCount: 1,
    promptEnabledCount: 1,
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
assert.equal(visiblePlan.actionCandidates.length, 1);
assert.equal(visiblePlan.actionCandidates[0].action, "PLAY_CARD");
assert.equal(visiblePlan.inspector.summaryRows.find((row) => row.key === "zone")?.value, "我方手牌");
assert.equal(visiblePlan.inspector.summaryRows.find((row) => row.key === "candidate")?.value, "1 可提交 / 1 阻断");
assert.equal(visiblePlan.inspector.summaryRows.find((row) => row.key === "source")?.value, "服务端对象上下文");
assert.ok(visiblePlan.inspector.groups.find((group) => group.key === "identity")?.rows.some((row) => row.value.includes("prompt-1")));
assert.ok(visiblePlan.inspector.groups.find((group) => group.key === "candidate")?.rows.some((row) => row.value.includes("缺少合法目标")));
assert.ok(visiblePlan.inspector.groups.find((group) => group.key === "stack")?.rows.some((row) => row.value === "结算链来源"));
assert.ok(visiblePlan.inspector.groups.find((group) => group.key === "events")?.rows.some((row) => row.value.includes("STACK_ITEM_ADDED")));

console.log("Card detail plan check passed.");

function sourceCandidatesForPrompt(prompt, sourceObjectId) {
  return (prompt?.candidates ?? []).filter((candidate) =>
    candidate.enabled && (candidate.sources ?? []).some((source) => source.id === sourceObjectId));
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
