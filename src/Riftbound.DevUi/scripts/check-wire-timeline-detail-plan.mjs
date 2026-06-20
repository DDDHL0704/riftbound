import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const stripImports = (value) => value.replace(/^import[\s\S]*?;\n/gm, "");
const transpile = (value) => ts.transpileModule(stripImports(value), {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;

const commandFieldDisplayPath = resolve(scriptDir, "../src/utils/commandFieldDisplay.ts");
const commandFieldDisplayOutput = transpile(readFileSync(commandFieldDisplayPath, "utf8"));
const commandFieldModuleShim = { exports: {} };
new Function("exports", "module", commandFieldDisplayOutput)(commandFieldModuleShim.exports, commandFieldModuleShim);

const candidateSemanticsPath = resolve(scriptDir, "../src/utils/promptCandidateSemantics.ts");
const candidateSemanticsOutput = transpile(readFileSync(candidateSemanticsPath, "utf8"));
const candidateSemanticsModuleShim = { exports: {} };
new Function(
  "exports",
  "module",
  "commandFieldDisplayLabel",
  candidateSemanticsOutput
)(
  candidateSemanticsModuleShim.exports,
  candidateSemanticsModuleShim,
  commandFieldModuleShim.exports.commandFieldDisplayLabel
);

const focusedGrammarPath = resolve(scriptDir, "../src/utils/focusedInteractionGrammarPlan.ts");
const focusedGrammarOutput = transpile(readFileSync(focusedGrammarPath, "utf8"));
const focusedGrammarModuleShim = { exports: {} };
new Function(
  "exports",
  "module",
  "promptChoiceRoleLabel",
  "promptChoiceRoleOrder",
  focusedGrammarOutput
)(
  focusedGrammarModuleShim.exports,
  focusedGrammarModuleShim,
  promptChoiceRoleLabel,
  ["source", "mode", "destination", "target", "optionalCost"]
);

const sourcePath = resolve(scriptDir, "../src/utils/wireTimelineDetailPlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = transpile(source);
const moduleShim = { exports: {} };

new Function(
  "exports",
  "module",
  "buildFocusedInteractionGrammarPlan",
  "buildPromptInteractionModel",
  "candidateComposerKey",
  "promptCommandBindingLabel",
  "promptCommandBindingSourceLabel",
  "promptChoiceRoleLabel",
  "promptChoiceSummaryObjectIds",
  "summarizePromptCandidateSemantics",
  output
)(
  moduleShim.exports,
  moduleShim,
  focusedGrammarModuleShim.exports.buildFocusedInteractionGrammarPlan,
  buildPromptInteractionModel,
  candidateComposerKey,
  promptCommandBindingLabel,
  promptCommandBindingSourceLabel,
  promptChoiceRoleLabel,
  promptChoiceSummaryObjectIds,
  candidateSemanticsModuleShim.exports.summarizePromptCandidateSemantics
);

const { buildWireTimelineDetailPlan } = moduleShim.exports;
const plan = buildWireTimelineDetailPlan({
  detail: {
    id: "rule:stack:fixture-stack-1",
    lines: [{ label: "来源", value: "闪电" }],
    refs: [
      { id: "source-1", role: "来源" },
      { id: "target-1", label: "目标牌", role: "目标" },
      { id: "missing-1", role: "目标" },
      { id: "HIDDEN", role: "隐藏来源" }
    ],
    source: "rule",
    subtitle: "法术",
    title: "结算链项目"
  },
  objectIndex: {
    "source-1": { objectId: "source-1", cardNo: "OGN-001/298" },
    "target-1": { objectId: "target-1", cardNo: "SFD-001/221" }
  },
  prompt: {
    __model: {
      candidates: [
        {
          action: "PLAY_CARD",
          choices: [
            { id: "source-1", label: "手牌闪电", objectIds: ["source-1"], role: "source" },
            { id: "target-1", label: "目标牌", objectIds: ["target-1"], role: "target" }
          ],
          command: {
            bindings: [
              { field: "sourceObjectId", required: true, role: "source", roleLabel: "来源", source: "selectedSource" },
              { field: "targetObjectIds", required: false, role: "target", roleLabel: "目标", source: "selectedTargets" },
              { field: "cardNo", required: true, source: "requirementMetadata" }
            ],
            cmdType: "PLAY_CARD"
          },
          enabled: true,
          label: "打出卡牌",
          reason: "可提交",
          steps: [
            { count: 1, label: "来源", required: true, role: "source", sampleLabels: ["手牌闪电"] },
            { count: 1, label: "目标", required: false, role: "target", sampleLabels: ["目标牌"] }
          ]
        }
      ]
    }
  },
  objectContextById: {
    "source-1": {
      candidateLinks: [
        {
          commandFields: ["来源:sourceObjectId*"],
          commandType: "PLAY_CARD",
          enabled: true,
          label: "打出卡牌",
          reason: "",
          requiredCommandFields: ["来源:sourceObjectId*"],
          roles: ["来源"]
        }
      ],
      eventLinks: [],
      objectId: "source-1",
      promptDisabledCount: 0,
      promptEnabledCount: 1,
      stackRoles: [],
      stateLabels: [],
      zone: { kind: "hand", label: "我方手牌" }
    },
    "target-1": {
      candidateLinks: [
        {
          commandFields: ["目标:targetObjectIds*"],
          commandType: "CHOOSE_TARGET",
          enabled: false,
          label: "选择目标",
          reason: "等待来源",
          requiredCommandFields: ["目标:targetObjectIds*"],
          roles: ["目标"]
        }
      ],
      eventLinks: [],
      objectId: "target-1",
      promptDisabledCount: 1,
      promptEnabledCount: 0,
      stackRoles: [],
      stateLabels: [],
      zone: { kind: "battlefield", label: "右战场 / 对方单位" }
    }
  },
  selectedObjectContext: {
    candidateLinks: [],
    eventLinks: [],
    objectId: "source-1",
    promptDisabledCount: 0,
    promptEnabledCount: 1,
    stackRoles: [],
    stateLabels: [],
    zone: { kind: "hand", label: "我方手牌" }
  },
  selectedObjectId: "source-1"
});

assert.equal(plan.headerTitle, "结算链项目");
assert.equal(plan.statusCards[0].value, "规则队列");
assert.equal(plan.statusCards[1].value, "2 / 4 可定位");
assert.equal(plan.statusCards[2].value, "我方手牌");
assert.equal(plan.statusCards[3].value, "已命中详情对象");
assert.equal(plan.statusCards[4].value, "1 可用 / 1 阻断");
assert.equal(plan.statusCards[5].value, "2 条");
assert.equal(plan.inspector.sourceLabel, "规则队列");
assert.equal(plan.inspector.visibleRefCount, 2);
assert.equal(plan.inspector.selectedProjectionCount, 1);
assert.equal(plan.inspector.hiddenRefCount, 1);
assert.equal(plan.inspector.missingRefCount, 1);
assert.equal(plan.inspector.actionCandidateCount, 2);
assert.equal(plan.inspector.commandBridgeCount, 2);
assert.equal(plan.inspector.projectionRows.find((row) => row.key === "selected")?.count, 1);
assert.equal(plan.inspector.projectionRows.find((row) => row.key === "visible")?.count, 1);
assert.equal(plan.inspector.candidateRows[0].label, "OGN-001/298");

const visibilityPlan = buildWireTimelineDetailPlan({
  detail: {
    id: "event:face-down:1",
    lines: [],
    refs: [
      { id: "face-down-1", label: "隐藏对象", role: "来源", visibility: "hidden" },
      { id: "known-but-redacted", role: "目标", visibility: "missing" },
      { id: "public-1", role: "参与", visibility: "visible" }
    ],
    source: "event",
    title: "隐藏事件"
  },
  objectIndex: {
    "face-down-1": { objectId: "face-down-1", isFaceDown: true },
    "known-but-redacted": { objectId: "known-but-redacted", cardNo: "SHOULD-NOT-WIN" },
    "public-1": { objectId: "public-1", cardNo: "PUB-001" }
  },
  selectedObjectId: "face-down-1"
});

assert.deepEqual(visibilityPlan.projectionRows.map((row) => row.state), ["hidden", "missing", "visible"]);
assert.equal(visibilityPlan.inspector.hiddenRefCount, 1);
assert.equal(visibilityPlan.inspector.missingRefCount, 1);
assert.equal(visibilityPlan.inspector.visibleRefCount, 1);

assert.equal(plan.commandBridgeRows.length, 2);
assert.equal(plan.commandBridgeRows[0].detailObjectId, "source-1");
assert.equal(plan.commandBridgeRows[0].commandType, "PLAY_CARD");
assert.equal(plan.commandBridgeRows[0].draftActive, false);
assert.equal(plan.commandBridgeRows[0].enabled, true);
assert.equal(plan.commandBridgeRows[0].routeState, "inactive");
assert.equal(plan.commandBridgeRows[0].routeStateLabel, "未进入草稿");
assert.equal(plan.commandBridgeRows[0].commandFieldSummary, "0 覆盖 / 1 缺少 / 1 服务端");
assert.deepEqual(plan.commandBridgeRows[0].commandFields.map((field) => field.state), ["missing", "optional", "server"]);
assert.equal(plan.commandBridgeRows[0].grammarState, "incomplete");
assert.equal(plan.commandBridgeRows[0].grammarStateLabel, "待选择");
assert.equal(plan.commandBridgeRows[0].grammarSummary, "打出卡牌 / 待选择 / 选择来源");
assert.deepEqual(plan.commandBridgeRows[0].grammarSteps.map((step) => step.role), ["source", "target", "submit"]);
assert.deepEqual(plan.commandBridgeRows[0].grammarSteps.map((step) => step.state), ["available", "optional", "blocked"]);
assert.equal(plan.commandBridgeRows[0].gateSummary, "2 通过 / 2 阻断 / 1 等待");
assert.deepEqual(plan.commandBridgeRows[0].gateRows.map((gate) => gate.key), ["server-candidate", "connection", "player-draft", "required-fields", "submit-step"]);
assert.deepEqual(plan.commandBridgeRows[0].gateRows.map((gate) => gate.state), ["ready", "ready", "waiting", "blocked", "blocked"]);
assert.deepEqual(plan.commandBridgeRows[0].roleLabels, ["来源"]);
assert.deepEqual(plan.commandBridgeRows[0].selectedRoleLabels, []);
assert.equal(plan.commandBridgeRows[0].selectionLabel, "未进入草稿");
assert.equal(plan.commandBridgeRows[0].nextStepLabel, "可选目标");
assert.deepEqual(plan.commandBridgeRows[0].nextObjectRefs, [{
  key: "PLAY_CARD:target:target-1:target-1",
  label: "目标牌",
  objectId: "target-1",
  roleLabel: "目标"
}]);
assert.equal(plan.commandBridgeRows[1].detailObjectId, "target-1");
assert.deepEqual(plan.commandBridgeRows[1].roleLabels, ["目标"]);
assert.equal(plan.commandBridgeRows[1].nextStepLabel, "需要来源");

const draftPlan = buildWireTimelineDetailPlan({
  detail: {
    id: "rule:stack:fixture-stack-1",
    lines: [{ label: "来源", value: "闪电" }],
    refs: [
      { id: "source-1", role: "来源" },
      { id: "target-1", label: "目标牌", role: "目标" }
    ],
    source: "rule",
    subtitle: "法术",
    title: "结算链项目"
  },
  objectIndex: {
    "source-1": { objectId: "source-1", cardNo: "OGN-001/298" },
    "target-1": { objectId: "target-1", cardNo: "SFD-001/221" }
  },
  prompt: {
    __model: {
      candidates: [
        {
          action: "PLAY_CARD",
          choices: [
            { id: "source-1", label: "手牌闪电", objectIds: ["source-1"], role: "source" },
            { id: "target-1", label: "目标牌", objectIds: ["target-1"], role: "target" }
          ],
          command: {
            bindings: [
              { field: "sourceObjectId", required: true, role: "source", roleLabel: "来源", source: "selectedSource" },
              { field: "targetObjectIds", required: false, role: "target", roleLabel: "目标", source: "selectedTargets" },
              { field: "cardNo", required: true, source: "requirementMetadata" }
            ],
            cmdType: "PLAY_CARD"
          },
          enabled: true,
          label: "打出卡牌",
          reason: "可提交",
          steps: [
            { count: 1, label: "来源", required: true, role: "source", sampleLabels: ["手牌闪电"] },
            { count: 1, label: "目标", required: false, role: "target", sampleLabels: ["目标牌"] }
          ]
        }
      ]
    }
  },
  selectionDraft: {
    candidateKey: "PLAY_CARD::打出卡牌",
    optionalCostIds: [],
    sourceObjectId: "source-1",
    targetChoiceIds: ["target-1"]
  }
});

assert.equal(draftPlan.statusCards.find((card) => card.label === "候选路径")?.value, "2 条 / 2 草稿");
assert.equal(draftPlan.commandBridgeRows[0].draftActive, true);
assert.equal(draftPlan.commandBridgeRows[0].routeState, "ready");
assert.equal(draftPlan.commandBridgeRows[0].routeStateLabel, "可送服务端校验");
assert.equal(draftPlan.commandBridgeRows[0].commandFieldSummary, "2 覆盖 / 0 缺少 / 1 服务端");
assert.deepEqual(draftPlan.commandBridgeRows[0].commandFields.map((field) => field.state), ["covered", "covered", "server"]);
assert.equal(draftPlan.commandBridgeRows[0].grammarState, "ready");
assert.equal(draftPlan.commandBridgeRows[0].grammarStateLabel, "可提交");
assert.equal(draftPlan.commandBridgeRows[0].grammarSummary, "打出卡牌 / 可提交 / 提交服务端候选");
assert.deepEqual(draftPlan.commandBridgeRows[0].grammarSteps.map((step) => step.role), ["source", "target", "submit"]);
assert.deepEqual(draftPlan.commandBridgeRows[0].grammarSteps.map((step) => step.state), ["locked", "selected", "ready"]);
assert.equal(draftPlan.commandBridgeRows[0].gateSummary, "5 通过 / 0 阻断 / 0 等待");
assert.deepEqual(draftPlan.commandBridgeRows[0].gateRows.map((gate) => gate.state), ["ready", "ready", "ready", "ready", "ready"]);
assert.deepEqual(draftPlan.commandBridgeRows[0].selectedRoleLabels, ["来源", "目标"]);
assert.equal(draftPlan.commandBridgeRows[0].selectionLabel, "已选 来源 / 目标");
assert.equal(draftPlan.commandBridgeRows[0].selectedStepCount, 2);
assert.equal(draftPlan.commandBridgeRows[0].totalStepCount, 2);
assert.equal(draftPlan.commandBridgeRows[0].missingRequiredCount, 0);
assert.equal(draftPlan.commandBridgeRows[0].nextStepLabel, "草稿可送服务端校验");
assert.deepEqual(draftPlan.commandBridgeRows[0].nextObjectRefs, []);

const disconnectedDraftPlan = buildWireTimelineDetailPlan({
  detail: {
    id: "rule:stack:fixture-stack-1",
    lines: [{ label: "来源", value: "闪电" }],
    refs: [
      { id: "source-1", role: "来源" },
      { id: "target-1", label: "目标牌", role: "目标" }
    ],
    source: "rule",
    subtitle: "法术",
    title: "结算链项目"
  },
  disabledByConnection: true,
  objectIndex: {
    "source-1": { objectId: "source-1", cardNo: "OGN-001/298" },
    "target-1": { objectId: "target-1", cardNo: "SFD-001/221" }
  },
  prompt: {
    __model: {
      candidates: [
        {
          action: "PLAY_CARD",
          choices: [
            { id: "source-1", label: "手牌闪电", objectIds: ["source-1"], role: "source" },
            { id: "target-1", label: "目标牌", objectIds: ["target-1"], role: "target" }
          ],
          command: {
            bindings: [
              { field: "sourceObjectId", required: true, role: "source", roleLabel: "来源", source: "selectedSource" },
              { field: "targetObjectIds", required: false, role: "target", roleLabel: "目标", source: "selectedTargets" },
              { field: "cardNo", required: true, source: "requirementMetadata" }
            ],
            cmdType: "PLAY_CARD"
          },
          enabled: true,
          label: "打出卡牌",
          reason: "可提交",
          steps: [
            { count: 1, label: "来源", required: true, role: "source", sampleLabels: ["手牌闪电"] },
            { count: 1, label: "目标", required: false, role: "target", sampleLabels: ["目标牌"] }
          ]
        }
      ]
    }
  },
  selectionDraft: {
    candidateKey: "PLAY_CARD::打出卡牌",
    optionalCostIds: [],
    sourceObjectId: "source-1",
    targetChoiceIds: ["target-1"]
  }
});

assert.equal(disconnectedDraftPlan.commandBridgeRows[0].routeState, "blocked");
assert.equal(disconnectedDraftPlan.commandBridgeRows[0].grammarState, "blocked");
assert.equal(disconnectedDraftPlan.commandBridgeRows[0].grammarSummary, "打出卡牌 / 阻断 / 等待连接恢复");
assert.equal(disconnectedDraftPlan.commandBridgeRows[0].gateSummary, "3 通过 / 2 阻断 / 0 等待");
assert.deepEqual(disconnectedDraftPlan.commandBridgeRows[0].gateRows.map((gate) => gate.key), ["server-candidate", "connection", "player-draft", "required-fields", "submit-step"]);
assert.deepEqual(disconnectedDraftPlan.commandBridgeRows[0].gateRows.map((gate) => gate.state), ["ready", "blocked", "ready", "ready", "blocked"]);
assert.deepEqual(disconnectedDraftPlan.commandBridgeRows[0].grammarSteps.map((step) => step.state), ["locked", "selected", "blocked"]);
assert.equal(plan.navigationRows.length, 4);
assert.deepEqual(plan.navigationRows.map((row) => row.focusState), ["selected", "focusable", "missing", "hidden"]);
assert.deepEqual(plan.navigationRows.map((row) => row.projectionState), ["selected", "visible", "missing", "hidden"]);
assert.equal(plan.navigationRows[0].objectId, "source-1");
assert.equal(plan.navigationRows[0].zoneLabel, "我方手牌");
assert.equal(plan.navigationRows[0].actionState, "available");
assert.equal(plan.navigationRows[0].actionLabel, "1 可用");
assert.equal(plan.navigationRows[1].objectId, "target-1");
assert.equal(plan.navigationRows[1].zoneLabel, "右战场 / 对方单位");
assert.equal(plan.navigationRows[1].actionState, "blocked");
assert.equal(plan.navigationRows[1].actionLabel, "1 阻断");
assert.equal(plan.navigationRows[2].objectId, undefined);
assert.equal(plan.navigationRows[2].zoneLabel, "未公开");
assert.equal(plan.navigationRows[2].actionState, "none");
assert.equal(plan.navigationRows[3].objectId, undefined);
assert.equal(plan.navigationRows[3].zoneLabel, "隐藏");
assert.deepEqual(plan.projectionRows.map((row) => row.state), ["selected", "visible", "missing", "hidden"]);
assert.equal(JSON.stringify(plan).includes("missing-1"), true);
assert.equal(plan.projectionRows.find((row) => row.state === "missing")?.label, "未公开对象");
assert.equal(plan.projectionRows.find((row) => row.state === "hidden")?.label, "隐藏对象");
assert.equal(plan.actionHintRows.length, 2);
assert.equal(plan.actionHintRows[0].objectId, "source-1");
assert.equal(plan.actionHintRows[0].commandTypes[0], "PLAY_CARD");
assert.deepEqual(plan.actionHintRows[0].selectionRoleLabels, ["来源"]);
assert.deepEqual(plan.actionHintRows[0].commandFieldLabels, ["来源:sourceObjectId*"]);
assert.deepEqual(plan.actionHintRows[0].requiredCommandFieldLabels, ["来源:sourceObjectId*"]);
assert.equal(plan.actionHintRows[1].reasonLabels[0], "等待来源");
assert.deepEqual(plan.actionHintRows[1].selectionRoleLabels, ["目标"]);
assert.deepEqual(plan.actionHintRows[1].requiredCommandFieldLabels, ["目标:targetObjectIds*"]);

console.log("Wire timeline detail plan check passed.");

function buildPromptInteractionModel(prompt) {
  return prompt?.__model ?? { candidates: [] };
}

function candidateComposerKey(candidate) {
  return `${candidate.action}::${candidate.label}`;
}

function promptCommandBindingLabel(binding) {
  const prefix = binding.roleLabel ? `${binding.roleLabel}:` : binding.source === "requirementMetadata" ? "服务端:" : "";
  return `${prefix}${binding.field}${binding.required ? "*" : ""}`;
}

function promptCommandBindingSourceLabel(binding) {
  return binding.source === "requirementMetadata" ? "服务端注入" : binding.roleLabel ? "玩家选择" : binding.source;
}

function promptChoiceRoleLabel(role) {
  switch (role) {
    case "source":
      return "来源";
    case "target":
      return "目标";
    case "destination":
      return "位置";
    case "optionalCost":
      return "费用";
    case "mode":
      return "模式";
    default:
      return role;
  }
}

function promptChoiceSummaryObjectIds(choice) {
  return choice.objectIds ?? [choice.id];
}
