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
const focusSourcePath = resolve(scriptDir, "../src/utils/wireSidePanelFocusPlan.ts");
const focusSource = readFileSync(focusSourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const focusOutput = ts.transpileModule(focusSource, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const focusModuleShim = { exports: {} };

new Function("exports", "module", focusOutput)(focusModuleShim.exports, focusModuleShim);

const { buildWireSidePanelFocusPlan } = focusModuleShim.exports;

const emptyPlan = buildWireObjectCommandTrayPlan({
  focusedPlan: focusedPlan({ readinessState: "no-focus" })
});
assert.equal(emptyPlan.visible, false);
assert.equal(emptyPlan.state, "empty");
const emptyFocusPlan = buildWireSidePanelFocusPlan({ trayPlan: emptyPlan });
assert.equal(emptyFocusPlan.visible, false);
assert.equal(emptyFocusPlan.state, "empty");
assert.deepEqual(routeContract(emptyFocusPlan.routes), [
  "actions:side-panel:interaction:disabled:无操作:不可用",
  "map:side-panel:actionMap:disabled:无地图:不可用",
  "rules:side-panel:ruleQueue:disabled:无规则:不可用",
  "detail:drawer:drawer:disabled:无详情:不可用"
]);
assert.equal(emptyFocusPlan.routes[0].reason, "未选择公开对象。");

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
assert.deepEqual(readyPlan.submitPreviewRows.map((row) => `${row.key}:${row.tone}:${row.value}`), [
  "route:good:可送服务端",
  "command:neutral:PLAY_CARD",
  "missing:good:0 选择 / 0 字段",
  "server:neutral:1",
  "next:good:可以提交服务端候选。"
]);
const readyFocusPlan = buildWireSidePanelFocusPlan({
  objectContext: objectContext({
    eventLinks: [{ description: "已打出手牌", kind: "CARD_PLAYED", role: "source" }]
  }),
  selectedObjectPlan: selectedObjectPlan(),
  trayPlan: readyPlan
});
assert.equal(readyFocusPlan.visible, true);
assert.equal(readyFocusPlan.objectId, "p1-hand-spell");
assert.equal(readyFocusPlan.state, "ready");
assert.equal(readyFocusPlan.relationCount, 2);
assert.equal(readyFocusPlan.eventCount, 1);
assert.deepEqual(readyFocusPlan.metrics.map((metric) => `${metric.key}:${metric.value}`), [
  "candidate:1 可用 / 0 阻断",
  "command:PLAY_CARD",
  "gate:可提交",
  "window:可行动"
]);
assert.deepEqual(readyFocusPlan.contextMetrics.map((metric) => `${metric.key}:${metric.value}`), [
  "relation:2",
  "event:1",
  "syntax:PLAY_CARD",
  "source:服务端对象"
]);
assert.deepEqual(readyFocusPlan.relations.map((relation) => `${relation.sourceLabel}:${relation.state}:${relation.value}`), [
  "服务端对象上下文:ready:服务端对象上下文 / 来源 / PLAY_CARD",
  "近期事件:history:近期事件 / 来源"
]);
assert.equal(readyFocusPlan.relations[1].detail?.id, "rule:resolution:card-played");
assert.deepEqual(routeContract(readyFocusPlan.routes), [
  "actions:side-panel:interaction:available:查看操作:可进入",
  "map:side-panel:actionMap:available:查看地图:可进入",
  "rules:side-panel:ruleQueue:available:查看规则:可进入",
  "detail:drawer:drawer:available:打开详情:可打开"
]);
assert.equal(readyFocusPlan.routes.find((route) => route.key === "rules")?.reason.includes("服务端流程关联"), true);

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
assert.equal(selectingPlan.submitPreviewRows.find((row) => row.key === "route")?.value, "草稿未齐");
assert.equal(selectingPlan.submitPreviewRows.find((row) => row.key === "missing")?.tone, "warn");
assert.equal(selectingPlan.submitPreviewRows.find((row) => row.key === "next")?.value, "选择目标");

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
assert.equal(hiddenPlan.submitPreviewRows.length, 0);
const hiddenFocusPlan = buildWireSidePanelFocusPlan({ trayPlan: hiddenPlan });
assert.equal(hiddenFocusPlan.visible, true);
assert.equal(hiddenFocusPlan.state, "readonly");
assert.equal(hiddenFocusPlan.metrics.find((metric) => metric.key === "command")?.value, "不公开");
assert.deepEqual(routeContract(hiddenFocusPlan.routes), [
  "actions:side-panel:interaction:disabled:无操作:不可用",
  "map:side-panel:actionMap:disabled:无地图:不可用",
  "rules:side-panel:ruleQueue:disabled:无规则:不可用",
  "detail:drawer:drawer:available:打开详情:可打开"
]);
assert.equal(JSON.stringify(hiddenFocusPlan).includes("PLAY_CARD"), false);

console.log("Wire object command tray plan check passed.");

function routeContract(routes) {
  return routes.map((route) => `${route.key}:${route.targetKind}:${route.slot ?? "drawer"}:${route.state}:${route.actionLabel}:${route.stateLabel}`);
}

function focusedPlan({
  commandType,
  legalState = "ready",
  nextStepLabel = "可以提交服务端候选。",
  readinessState
} = {}) {
  return {
    actionEntries: [{ key: "PLAY_CARD:打出", mode: "button" }],
    commandReview: commandReviewFor({ commandType, nextStepLabel, readinessState }),
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

function commandReviewFor({
  commandType,
  nextStepLabel,
  readinessState
}) {
  if (readinessState === "ready") {
    return {
      canSubmit: true,
      candidateLabel: "打出",
      checkRows: [],
      command: { cmdType: commandType ?? "PLAY_CARD" },
      commandPreview: [],
      commandType: commandType ?? "PLAY_CARD",
      metrics: [
        { key: "selection", label: "已选步骤", value: "2" },
        { key: "missing", label: "缺少", value: "0 选择 / 0 字段" },
        { key: "server", label: "服务端字段", value: "1" }
      ],
      nextStepLabel,
      state: "ready",
      stateLabel: "可送服务端",
      submitLabel: "提交当前路线",
      submitReason: "命令已由服务端候选模板组装完成，提交后仍由服务端规则校验。",
      summary: "打出 / PLAY_CARD / 服务端候选"
    };
  }

  if (readinessState === "needs-selection") {
    return {
      canSubmit: false,
      candidateLabel: "打出",
      checkRows: [],
      command: undefined,
      commandPreview: [],
      commandType: commandType ?? "PLAY_CARD",
      metrics: [
        { key: "selection", label: "已选步骤", value: "1" },
        { key: "missing", label: "缺少", value: "1 选择 / 0 字段" },
        { key: "server", label: "服务端字段", value: "1" }
      ],
      nextStepLabel,
      state: "drafting",
      stateLabel: "草稿未齐",
      submitLabel: "提交当前路线",
      submitReason: nextStepLabel,
      summary: "打出 / PLAY_CARD / 待选择"
    };
  }

  return {
    canSubmit: false,
    candidateLabel: "未选择候选",
    checkRows: [],
    command: undefined,
    commandPreview: [],
    commandType: "无",
    metrics: [
      { key: "selection", label: "选择", value: "0" },
      { key: "missing", label: "缺少", value: "无路线" },
      { key: "server", label: "服务端字段", value: "0" }
    ],
    nextStepLabel: "先点击服务端候选对象，建立提交路线。",
    state: readinessState === "server-blocked" ? "blocked" : "empty",
    stateLabel: readinessState === "server-blocked" ? "提交阻断" : "等待选择",
    submitLabel: "提交当前路线",
    submitReason: "尚未选择服务端候选。",
    summary: "尚未选择服务端候选。"
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
        category: "play",
        commandFields: ["来源:sourceObjectId*", "位置:destination"],
        enabled: true,
        intent: "play-card",
        priority: 100,
        reason: "可提交",
        requiredCommandFields: ["来源:sourceObjectId*"],
        uiHint: "card-action"
      }
    ],
    candidateSource: "server",
    contextBoundary: "服务端对象上下文只公开当前行动提示中的对象候选、选择角色和命令字段；隐藏 metadata、隐藏区内容和未公开卡牌身份不进入对象上下文。",
    contextSource: "server-action-prompt",
    eventLinks: [],
    zone: { kind: "hand", label: "我方手牌", playerId: "P1" },
    ...overrides
  };
}

function selectedObjectPlan() {
  return {
    candidateCount: 1,
    disabledCandidateCount: 0,
    enabledCandidateCount: 1,
    missingRequiredSyntaxCount: 0,
    objectId: "p1-hand-spell",
    relationCount: 2,
    relations: [
      {
        candidateActions: ["PLAY_CARD"],
        detailLabel: "服务端对象上下文",
        key: "selected:object-context:p1-hand-spell",
        laneLabel: "服务端对象上下文",
        roleLabel: "来源",
        source: "object-context",
        sourceLabel: "服务端对象上下文",
        state: "ready",
        stateLabel: "可提交"
      },
      {
        candidateActions: [],
        detail: {
          id: "rule:resolution:card-played",
          lines: [{ label: "事件", value: "CARD_PLAYED" }],
          refs: [{ id: "p1-hand-spell", role: "source", visibility: "visible" }],
          source: "rule",
          title: "卡牌已打出"
        },
        detailId: "rule:resolution:card-played",
        detailLabel: "卡牌已打出",
        key: "selected:section:resolution:p1-hand-spell",
        laneKey: "resolution",
        laneLabel: "近期事件",
        roleLabel: "来源",
        source: "section",
        sourceLabel: "近期事件",
        state: "history",
        stateLabel: "历史"
      }
    ],
    state: "linked",
    syntaxRows: [{ label: "命令", state: "ready", source: "server", value: "PLAY_CARD" }],
    syntaxSummary: "PLAY_CARD",
    usableSyntaxCount: 1
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
