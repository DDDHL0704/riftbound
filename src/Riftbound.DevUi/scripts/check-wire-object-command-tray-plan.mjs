import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/wireObjectCommandTrayPlan.ts");
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
  "isHiddenObject",
  "tableObjectContextSourceLabel",
  output
)(
  moduleShim.exports,
  moduleShim,
  isHiddenObject,
  tableObjectContextSourceLabel
);

const { buildWireObjectCommandTrayPlan } = moduleShim.exports;

const emptyPlan = buildWireObjectCommandTrayPlan({
  focusedPlan: focusedPlan({ readinessState: "no-focus" })
});
assert.equal(emptyPlan.visible, false);
assert.equal(emptyPlan.state, "empty");

const readyPlan = buildWireObjectCommandTrayPlan({
  card: card(),
  focusedPlan: focusedPlan({ commandType: "PLAY_CARD", readinessState: "ready" }),
  objectContext: objectContext()
});
assert.equal(readyPlan.visible, true);
assert.equal(readyPlan.state, "ready");
assert.equal(readyPlan.stateLabel, "可提交");
assert.equal(readyPlan.primaryLabel, "提交 PLAY_CARD");
assert.equal(readyPlan.canShowActions, true);
assert.equal(readyPlan.metrics.find((metric) => metric.key === "candidate")?.value, "1 可用 / 0 阻断");
assert.equal(readyPlan.metrics.find((metric) => metric.key === "semantic")?.value, "play/play-card");
assert.equal(readyPlan.metrics.find((metric) => metric.key === "role")?.value, "来源");
assert.equal(readyPlan.metrics.find((metric) => metric.key === "command")?.value, "PLAY_CARD");
assert.deepEqual(readyPlan.semanticRows.map((row) => `${row.category}:${row.intent}:${row.priority}:${row.uiHint}:${row.count}`), [
  "play:play-card:100:card-action:1"
]);
assert.equal(readyPlan.semanticSummary, "play/play-card");
assert.equal(readyPlan.subtitle, "我方手牌 / 法术 / 服务端对象上下文");
assert.equal(readyPlan.contextRows.find((row) => row.key === "source")?.value, "服务端对象上下文");
assert.equal(readyPlan.contextRows.find((row) => row.key === "fields")?.value, "1 必填 / 2 公开");
assert.equal(readyPlan.contextRows.find((row) => row.key === "fields")?.tone, "warn");
assert.equal(readyPlan.contextRows.find((row) => row.key === "boundary")?.value, "不公开隐藏 metadata");

const selectingPlan = buildWireObjectCommandTrayPlan({
  card: card(),
  focusedPlan: focusedPlan({
    commandType: "PLAY_CARD",
    legalState: "needs-selection",
    nextStepLabel: "选择目标",
    readinessState: "needs-selection"
  }),
  objectContext: objectContext()
});
assert.equal(selectingPlan.state, "selecting");
assert.equal(selectingPlan.tone, "warn");
assert.equal(selectingPlan.primaryLabel, "选择目标");
assert.equal(selectingPlan.nextStepLabel, "选择目标");

const readonlyPlan = buildWireObjectCommandTrayPlan({
  card: card(),
  focusedPlan: focusedPlan({ readinessState: "not-candidate" }),
  objectContext: objectContext({ candidateSource: "none", contextSource: "snapshot-public-index" })
});
assert.equal(readonlyPlan.state, "readonly");
assert.equal(readonlyPlan.canShowActions, false);
assert.equal(readonlyPlan.primaryLabel, "查看对象");

const hiddenPlan = buildWireObjectCommandTrayPlan({
  card: {
    object: { isFaceDown: true, objectId: "hidden-1" },
    objectId: "hidden-1"
  },
  focusedPlan: focusedPlan({ commandType: "PLAY_CARD", readinessState: "ready" })
});
assert.equal(hiddenPlan.state, "readonly");
assert.equal(hiddenPlan.title, "未公开卡牌");
assert.equal(hiddenPlan.canShowActions, false);
assert.equal(hiddenPlan.contextRows.length, 0);
assert.equal(JSON.stringify(hiddenPlan).includes("PLAY_CARD"), false);
assert.equal(hiddenPlan.metrics.find((metric) => metric.key === "command")?.value, "不公开");
assert.equal(hiddenPlan.metrics.find((metric) => metric.key === "semantic")?.value, "不公开");
assert.equal(hiddenPlan.semanticRows.length, 0);
assert.equal(hiddenPlan.nextStepLabel.includes("不展示或提交前端推断操作"), true);

console.log("Wire object command tray plan check passed.");

function focusedPlan({
  commandType,
  legalState = "ready",
  nextStepLabel = "可以提交服务端候选。",
  readinessState
} = {}) {
  return {
    actionEntries: [{ key: "PLAY_CARD:打出", mode: "button" }],
    legalActionRows: [
      {
        action: "PLAY_CARD",
        category: "play",
        commandType,
        intent: "play-card",
        key: "PLAY_CARD:打出",
        label: "打出",
        missingRequiredLabels: legalState === "needs-selection" ? ["目标"] : [],
        nextStepLabel,
        priority: 100,
        reason: "可提交",
        roleLabels: ["来源"],
        state: legalState,
        stateLabel: legalState === "needs-selection" ? "需选择" : "可提交",
        uiHint: "card-action"
      }
    ],
    readiness: {
      blockedCount: readinessState === "server-blocked" ? 1 : 0,
      canSubmit: readinessState === "ready",
      candidateLabel: "打出",
      commandType,
      enabledCount: readinessState === "not-candidate" ? 0 : 1,
      missingRequiredCount: readinessState === "needs-selection" ? 1 : 0,
      nextStepLabel,
      state: readinessState,
      stateLabel: readinessState
    },
    submissionGate: { state: "connected", stateLabel: "可提交" },
    windowGate: { state: "ready", stateLabel: "可行动" }
  };
}

function card() {
  return {
    object: {
      cardNo: "OGN-001/298",
      controllerId: "P1",
      objectId: "p1-hand-spell",
      ownerId: "P1"
    },
    objectId: "p1-hand-spell",
    spec: {
      cardCategoryName: "法术",
      cardName: "测试法术",
      cardNo: "OGN-001/298"
    }
  };
}

function objectContext(overrides = {}) {
  return {
    candidateLinks: [
      {
        commandFields: ["来源:sourceObjectId*", "位置:destination"],
        enabled: true,
        reason: "可提交",
        requiredCommandFields: ["来源:sourceObjectId*"]
      }
    ],
    candidateSource: "server",
    contextBoundary: "服务端对象上下文只公开当前行动提示中的对象候选、选择角色和命令字段；隐藏 metadata、隐藏区内容和未公开卡牌身份不进入对象上下文。",
    contextSource: "server-action-prompt",
    zone: { kind: "hand", label: "我方手牌", playerId: "P1" },
    ...overrides
  };
}

function isHiddenObject(object) {
  return !object || object.isFaceDown === true || !object.cardNo;
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
      return "未建立上下文";
  }
}
