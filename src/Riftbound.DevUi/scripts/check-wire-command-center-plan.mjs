import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const { buildWireCommandCenterPlan } = loadTsModule(resolve(scriptDir, "../src/utils/wireCommandCenterPlan.ts"));

const readyPlan = buildWireCommandCenterPlan({
  coachPlan: coach("ready", "submit"),
  focusedPlan: focused("ready", { actionCount: 1, legalState: "ready" }),
  objectContext: context("手牌")
});
assert.equal(readyPlan.state, "ready");
assert.equal(readyPlan.stateLabel, "可提交");
assert.equal(readyPlan.canShowFocusedActions, true);
assert.equal(readyPlan.rows.find((row) => row.key === "focus")?.value, "hand-1");
assert.equal(readyPlan.rows.find((row) => row.key === "submit")?.state, "ready");
assert.deepEqual(readyPlan.actionRows.map((row) => [row.action, row.state]), [["PLAY_CARD", "ready"]]);

const selectingPlan = buildWireCommandCenterPlan({
  coachPlan: coach("selecting", "target"),
  focusedPlan: focused("needs-selection", { actionCount: 1, legalState: "needs-selection" }),
  objectContext: context("战场")
});
assert.equal(selectingPlan.state, "selecting");
assert.equal(selectingPlan.stepRole, "target");
assert.equal(selectingPlan.rows.find((row) => row.key === "candidate")?.state, "selecting");

const noFocusPlan = buildWireCommandCenterPlan({
  coachPlan: coach("waiting", "wait"),
  focusedPlan: focused("no-focus", { sourceObjectId: undefined }),
  objectContext: undefined
});
assert.equal(noFocusPlan.state, "no-focus");
assert.equal(noFocusPlan.canShowFocusedActions, false);
assert.equal(noFocusPlan.rows.find((row) => row.key === "focus")?.state, "empty");

const blockedPlan = buildWireCommandCenterPlan({
  coachPlan: coach("blocked", "sync"),
  focusedPlan: focused("ready", { canSubmit: false }),
  objectContext: context("基地")
});
assert.equal(blockedPlan.state, "blocked");
assert.equal(blockedPlan.rows.find((row) => row.key === "submit")?.state, "blocked");

console.log("Wire command center plan check passed.");

function focused(readinessState, options = {}) {
  const sourceObjectId = Object.hasOwn(options, "sourceObjectId") ? options.sourceObjectId : "hand-1";
  const canSubmit = options.canSubmit ?? true;
  const legalState = options.legalState ?? "ready";
  const actionCount = options.actionCount ?? 0;
  return {
    actionEntries: Array.from({ length: actionCount }, (_, index) => ({ key: `action-${index}` })),
    legalActionRows: sourceObjectId ? [{
      action: "PLAY_CARD",
      commandType: "PLAY_CARD",
      key: "PLAY_CARD-1",
      label: "打出卡牌",
      nextStepLabel: readinessState === "needs-selection" ? "选择目标" : "可以提交服务端候选。",
      roleLabels: ["来源"],
      state: legalState,
      stateLabel: legalState === "ready" ? "可提交" : "需选择"
    }] : [],
    readiness: {
      blockedCount: canSubmit ? 0 : 1,
      commandType: sourceObjectId ? "PLAY_CARD" : undefined,
      enabledCount: sourceObjectId ? 1 : 0,
      nextStepLabel: readinessState === "no-focus" ? "点击桌面对象。" : "可以提交服务端候选。",
      state: readinessState,
      stateLabel: readinessState
    },
    sourceObject: {
      objectIdLabel: sourceObjectId ?? "无对象 ID",
      serverCandidateLabel: sourceObjectId ? "1 可用 / 0 禁用" : "无候选"
    },
    sourceObjectId,
    submissionGate: {
      canSubmit,
      reason: canSubmit ? "同一 tick。" : "等待同步。",
      state: canSubmit ? "connected" : "stale-snapshot",
      stateLabel: canSubmit ? "可提交" : "等待同步"
    },
    windowGate: {
      canAct: true,
      reason: "当前玩家拥有服务端行动窗口。",
      state: "ready",
      stateLabel: "当前可行动"
    }
  };
}

function coach(state, stepRole) {
  return {
    candidateLabel: "打出卡牌",
    metrics: [],
    nextStepLabel: "选择来源。",
    primaryLabel: state === "ready" ? "可提交" : "等待窗口",
    reason: "服务端提示。",
    rows: [],
    state,
    stateLabel: state,
    stepRole,
    summary: "行动窗口摘要。",
    tone: state === "ready" ? "good" : "neutral"
  };
}

function context(zoneLabel) {
  return {
    candidateLinks: [],
    candidateSource: "server",
    contextBoundary: "公开对象",
    contextSource: "server-action-prompt",
    eventLinks: [],
    objectId: "hand-1",
    promptDisabledCount: 0,
    promptEnabledCount: 1,
    stackRoles: [],
    stateLabels: [],
    zone: { kind: "hand", label: zoneLabel }
  };
}

function loadTsModule(sourcePath) {
  const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const moduleShim = { exports: {} };
  new Function("exports", "module", output)(moduleShim.exports, moduleShim);
  return moduleShim.exports;
}
