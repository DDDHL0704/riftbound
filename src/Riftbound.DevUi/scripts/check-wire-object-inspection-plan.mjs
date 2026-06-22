import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/wireObjectInspectionPlan.ts");
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
  "tableObjectCandidateSourceLabel",
  "tableObjectContextSourceLabel",
  output
)(
  moduleShim.exports,
  moduleShim,
  tableObjectCandidateSourceLabel,
  tableObjectContextSourceLabel
);

const { buildWireObjectInspectionPlan } = moduleShim.exports;

const plan = buildWireObjectInspectionPlan({
  context: {
    candidateLinks: [
      {
        category: "play",
        commandFields: ["来源:sourceObjectId*", "目标:targetObjectId"],
        commandType: "PLAY_CARD",
        composerReason: "服务端已公开组合提交。",
        composerState: "server",
        composerStateLabel: "服务端声明",
        enabled: true,
        intent: "play-card",
        label: "打出卡牌",
        priority: 100,
        reason: "可提交",
        requiredCommandFields: ["来源:sourceObjectId*"],
        roles: ["来源"],
        selectionSteps: [
          { choiceCount: 1, index: 0, label: "来源", objectChoiceCount: 1, required: true, role: "source" },
          { choiceCount: 2, index: 1, label: "目标", objectChoiceCount: 0, required: true, role: "target" }
        ],
        uiHint: "card-action"
      },
      {
        category: "ability",
        commandFields: ["来源:sourceObjectId*"],
        commandType: "ACTIVATE_ABILITY",
        composerReason: "服务端暂未开放组合提交。",
        composerState: "blocked",
        composerStateLabel: "服务端阻断",
        enabled: false,
        intent: "activate-ability",
        label: "启动能力",
        priority: 180,
        reason: "窗口不允许",
        requiredCommandFields: ["来源:sourceObjectId*"],
        roles: ["来源"],
        selectionSteps: [
          { choiceCount: 1, index: 0, label: "来源", objectChoiceCount: 1, required: true, role: "source" }
        ],
        uiHint: "card-action"
      }
    ],
    candidateSource: "server",
    cardNo: "OGN-001/298",
    controllerId: "P1",
    contextBoundary: "服务端对象上下文只公开当前行动提示中的对象候选、选择角色和命令字段。",
    contextSource: "server-action-prompt",
    eventLinks: [
      { description: "单位进场", kind: "UNIT_ENTERED", role: "对象" }
    ],
    object: {
      cardNo: "OGN-001/298",
      controllerId: "P1",
      objectId: "P1-HAND-001",
      ownerId: "P1"
    },
    objectId: "P1-HAND-001",
    ownerId: "P1",
    promptDisabledCount: 1,
    promptEnabledCount: 1,
    serverInspection: {
      boundary: "服务端检查摘要边界。",
      groups: [
        {
          key: "candidate",
          title: "服务端候选",
          rows: [
            { key: "candidate-0", label: "可提交", tone: "good", value: "PLAY_CARD / 来源" }
          ]
        }
      ],
      source: "server-action-prompt",
      summaryRows: [
        { key: "object", label: "对象", value: "P1-HAND-001" }
      ]
    },
    serverRelations: [
      {
        boundary: "服务端流程对象关联边界。",
        candidateActions: ["PLAY_CARD"],
        disabledCandidateCount: 0,
        enabledCandidateCount: 1,
        roles: ["候选来源", "来源"],
        source: "server-action-prompt",
        stepSummary: "来源* 1/1"
      }
    ],
    stackRoles: ["结算链来源"],
    stateLabels: ["横置"],
    zone: {
      kind: "hand",
      label: "我方手牌",
      playerId: "P1"
    }
  },
  contract: {
    candidateAction: "PLAY_CARD",
    hiddenMetadata: ["serverPaymentState"],
    legalChoices: ["candidate.sources", "candidate.targets"],
    promptKind: "MAIN_ACTION",
    requiredPayload: ["sourceObjectId", "targetObjectId"],
    validationErrors: [],
    visibleMetadata: ["sourceRequirements"]
  }
});

assert.equal(plan.authorityState, "server");
assert.equal(plan.authorityLabel, "服务端检查摘要");
assert.equal(plan.contextSourceLabel, "服务端对象上下文");
assert.equal(plan.boundaryLabel, "服务端检查摘要边界。");
assert.equal(plan.metrics.length, 6);
assert.equal(plan.metrics.find((metric) => metric.key === "candidate")?.value, "1 可用 / 1 阻断");
assert.equal(plan.metrics.find((metric) => metric.key === "syntax")?.state, "warning");
assert.equal(plan.metrics.find((metric) => metric.key === "commands")?.value, "1 必填 / 2 公开");
assert.deepEqual(
  plan.routeRows.map((row) => `${row.key}:${row.state}:${row.stateLabel}`),
  [
    "identity:ready:已公开",
    "zone:ready:hand",
    "authority:server:服务端检查摘要",
    "candidate:server:1 可用 / 1 阻断",
    "syntax:warning:缺少 1",
    "commands:server:1 必填 / 2 公开",
    "server-relations:server:已关联",
    "stack-events:ready:1 结算 / 1 事件",
    "contract:server:MAIN_ACTION"
  ]
);
assert.ok(plan.routeRows.find((row) => row.key === "contract")?.summary.includes("隐藏 1"));
assert.ok(plan.groups.find((group) => group.key === "server-candidate")?.rows.some((row) => row.value.includes("PLAY_CARD")));
assert.ok(plan.groups.find((group) => group.key === "syntax")?.rows.some((row) => row.value.includes("目标 / 0/2")));
assert.ok(plan.groups.find((group) => group.key === "commands")?.rows.some((row) => row.value === "目标:targetObjectId"));
assert.ok(plan.groups.find((group) => group.key === "relations")?.rows.some((row) => row.value.includes("PLAY_CARD")));
assert.ok(plan.groups.find((group) => group.key === "events")?.rows.some((row) => row.value.includes("UNIT_ENTERED")));
assert.ok(plan.groups.find((group) => group.key === "boundary")?.rows.some((row) => row.value.includes("服务端候选")));

const snapshotPlan = buildWireObjectInspectionPlan({
  context: {
    candidateLinks: [],
    candidateSource: "none",
    contextBoundary: "当前对象只有公开快照索引。",
    contextSource: "snapshot-public-index",
    eventLinks: [],
    objectId: "P1-DECK",
    promptDisabledCount: 0,
    promptEnabledCount: 0,
    serverRelations: [],
    stackRoles: [],
    stateLabels: [],
    zone: { kind: "main-deck", label: "我方主牌库", playerId: "P1" }
  }
});

assert.equal(snapshotPlan.authorityState, "snapshot");
assert.equal(snapshotPlan.metrics.find((metric) => metric.key === "candidate")?.state, "empty");
assert.equal(snapshotPlan.routeRows.find((row) => row.key === "candidate")?.summary, "该对象当前没有服务端可提交候选。");

console.log("Wire object inspection plan check passed.");

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
    case "server-flow-related-object":
      return "服务端关联对象";
    case "prompt-public-derived":
      return "公开候选只读派生";
    case "snapshot-public-index":
      return "公开快照索引";
    default:
      return tableObjectCandidateSourceLabel(context?.candidateSource);
  }
}
