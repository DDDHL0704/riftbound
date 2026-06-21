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
  "commandFromActionPromptTemplate",
  "buildFocusedInteractionGrammarPlan",
  "buildPromptInteractionModel",
  "candidateComposerKey",
  "commandSourceCopy",
  "promptStampedCommand",
  "promptCommandBindingLabel",
  "promptCommandBindingSourceLabel",
  "promptChoiceRoleLabel",
  "promptChoiceSummaryObjectIds",
  "sourceRequirementFor",
  "summarizePromptCandidateSemantics",
  output
)(
  moduleShim.exports,
  moduleShim,
  commandFromActionPromptTemplate,
  focusedGrammarModuleShim.exports.buildFocusedInteractionGrammarPlan,
  buildPromptInteractionModel,
  candidateComposerKey,
  commandSourceCopy,
  promptStampedCommand,
  promptCommandBindingLabel,
  promptCommandBindingSourceLabel,
  promptChoiceRoleLabel,
  promptChoiceSummaryObjectIds,
  sourceRequirementFor,
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
    promptId: "prompt-1",
    snapshotTick: 99,
    candidates: [
      {
        action: "PLAY_CARD",
        commandTemplate: {
          bindings: [
            { field: "sourceObjectId", required: true, source: "selectedSource" },
            { asArray: true, field: "targetObjectIds", required: false, source: "selectedTargets" },
            { field: "cardNo", metadataKey: "cardNo", required: true, source: "requirementMetadata" }
          ],
          cmdType: "PLAY_CARD"
        },
        enabled: true,
        label: "打出卡牌",
        metadata: {
          sourceRequirements: [{ cardNo: "OGN-001/298", sourceObjectId: "source-1" }]
        },
        reason: "可提交"
      }
    ],
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
          composerReason: "服务端已公开组合提交。",
          composerState: "server",
          composerStateLabel: "服务端声明",
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
          composerReason: "候选未公开组合提交协议。",
          composerState: "missing",
          composerStateLabel: "未公开",
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
assert.equal(plan.evidenceRows.find((row) => row.key === "source")?.value, "规则队列");
assert.equal(plan.evidenceRows.find((row) => row.key === "source")?.stateLabel, "服务端规则");
assert.equal(plan.evidenceRows.find((row) => row.key === "projection")?.value, "2/4 可定位");
assert.equal(plan.evidenceRows.find((row) => row.key === "projection")?.state, "warn");
assert.equal(plan.evidenceRows.find((row) => row.key === "candidate")?.value, "1 可用 / 1 阻断");
assert.equal(plan.evidenceRows.find((row) => row.key === "candidate")?.state, "ready");
assert.equal(plan.evidenceRows.find((row) => row.key === "path")?.value, "2 路径 / 0 可送 / 0 待选 / 0 阻断 / 2 未进");
assert.equal(plan.evidenceRows.find((row) => row.key === "path")?.stateLabel, "未进入草稿");
assert.equal(plan.evidenceRows.find((row) => row.key === "boundary")?.value, "1 隐藏 / 1 未公开");
assert.equal(plan.evidenceRows.find((row) => row.key === "boundary")?.state, "warn");
assert.equal(plan.nextStep.state, "selecting");
assert.equal(plan.nextStep.headline, "从详情对象开始选择");
assert.equal(plan.nextStep.body, "来源 -> 打出卡牌");
assert.equal(plan.nextStep.commandType, "PLAY_CARD");
assert.deepEqual(plan.nextStep.refs.map((ref) => ref.objectId), ["target-1"]);
assert.deepEqual(plan.nextStep.checks.map((check) => check.key), ["server-candidate", "connection", "player-draft", "required-fields", "submit-step"]);
assert.deepEqual(plan.nextStep.checks.map((check) => check.state), ["ready", "ready", "waiting", "blocked", "blocked"]);
assert.deepEqual(plan.nextStep.steps.map((step) => step.role), ["source", "target", "submit"]);
assert.deepEqual(plan.nextStep.steps.map((step) => step.state), ["available", "optional", "blocked"]);
assert.equal(plan.routeSummary.state, "inactive");
assert.equal(plan.routeSummary.stateLabel, "未进入");
assert.equal(plan.routeSummary.headline, "等待选择候选对象");
assert.equal(plan.routeSummary.body, "来源 -> 打出卡牌");
assert.equal(plan.routeSummary.nextStepLabel, "可选目标");
assert.equal(plan.routeSummary.totalCount, 2);
assert.equal(plan.routeSummary.draftCount, 0);
assert.deepEqual(plan.routeSummary.rows.map((row) => `${row.key}:${row.value}:${row.state}`), [
  "ready:0:empty",
  "selecting:0:empty",
  "blocked:0:empty",
  "inactive:2:inactive",
  "draft:0:empty"
]);
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
assert.equal(visibilityPlan.evidenceRows.find((row) => row.key === "source")?.value, "日志事件");
assert.equal(visibilityPlan.evidenceRows.find((row) => row.key === "source")?.stateLabel, "服务端日志");
assert.equal(visibilityPlan.evidenceRows.find((row) => row.key === "projection")?.value, "1/3 可定位");
assert.equal(visibilityPlan.evidenceRows.find((row) => row.key === "projection")?.state, "warn");
assert.equal(visibilityPlan.evidenceRows.find((row) => row.key === "boundary")?.value, "1 隐藏 / 1 未公开");
assert.equal(visibilityPlan.nextStep.state, "observe");
assert.equal(visibilityPlan.nextStep.headline, "仅查看公开证据");
assert.equal(visibilityPlan.nextStep.detail, "1 隐藏 / 1 未公开，等待服务端公开后再操作。");
assert.deepEqual(visibilityPlan.nextStep.checks, []);
assert.deepEqual(visibilityPlan.nextStep.steps, []);
assert.equal(visibilityPlan.routeSummary.state, "empty");
assert.equal(visibilityPlan.routeSummary.headline, "无候选路线");
assert.equal(visibilityPlan.routeSummary.body, "当前详情没有可由服务端候选解释的提交路线。");
assert.equal(visibilityPlan.routeSummary.nextStepLabel, "只查看规则事件。");
assert.equal(visibilityPlan.routeSummary.totalCount, 0);
assert.equal(visibilityPlan.inspector.hiddenRefCount, 1);
assert.equal(visibilityPlan.inspector.missingRefCount, 1);
assert.equal(visibilityPlan.inspector.visibleRefCount, 1);

assert.equal(plan.commandBridgeRows.length, 2);
assert.equal(plan.commandBridgeRows[0].detailObjectId, "source-1");
assert.equal(plan.commandBridgeRows[0].detailRoleLabel, "来源");
assert.equal(plan.commandBridgeRows[0].serverRoleSummary, "来源");
assert.equal(plan.commandBridgeRows[0].detailLinkLabel, "详情来源 / 候选来源");
assert.equal(plan.commandBridgeRows[0].commandType, "PLAY_CARD");
assert.equal(plan.commandBridgeRows[0].draftActive, false);
assert.equal(plan.commandBridgeRows[0].enabled, true);
assert.equal(plan.commandBridgeRows[0].routeState, "inactive");
assert.equal(plan.commandBridgeRows[0].routeStateLabel, "未进入草稿");
assert.equal(plan.commandBridgeRows[0].commandFieldSummary, "0 覆盖 / 1 缺少 / 1 服务端");
assert.equal(plan.commandBridgeRows[0].fieldCoverageSummary, "详情 2/3 / 草稿 0 / 服务端 1 / 缺少 1");
assert.deepEqual(plan.commandBridgeRows[0].commandFields.map((field) => field.state), ["missing", "optional", "server"]);
assert.deepEqual(plan.commandBridgeRows[0].commandFields.map((field) => `${field.field}:${field.detailObjectCount}:${field.selectedChoiceCount}:${field.candidateChoiceCount}`), [
  "sourceObjectId:1:0:1",
  "targetObjectIds:1:0:1",
  "cardNo:0:0:0"
]);
assert.deepEqual(plan.commandBridgeRows[0].commandFields.map((field) => field.coverageLabel), [
  "详情引用可作为来源：OGN-001/298",
  "详情引用可作为目标：SFD-001/221",
  "服务端根据候选元数据注入"
]);
assert.equal(plan.commandBridgeRows[0].grammarState, "incomplete");
assert.equal(plan.commandBridgeRows[0].grammarStateLabel, "待选择");
assert.equal(plan.commandBridgeRows[0].grammarSummary, "打出卡牌 / 待选择 / 仅有模板 / 选择来源");
assert.deepEqual(plan.commandBridgeRows[0].grammarSteps.map((step) => step.role), ["source", "target", "submit"]);
assert.deepEqual(plan.commandBridgeRows[0].grammarSteps.map((step) => step.state), ["available", "optional", "blocked"]);
assert.equal(plan.commandBridgeRows[0].gateSummary, "2 通过 / 2 阻断 / 1 等待");
assert.deepEqual(plan.commandBridgeRows[0].gateRows.map((gate) => gate.key), ["server-candidate", "connection", "player-draft", "required-fields", "submit-step"]);
assert.deepEqual(plan.commandBridgeRows[0].gateRows.map((gate) => gate.state), ["ready", "ready", "waiting", "blocked", "blocked"]);
assert.equal(plan.commandBridgeRows[0].submitPlan.state, "inactive");
assert.equal(plan.commandBridgeRows[0].submitPlan.stateLabel, "未进入草稿");
assert.equal(plan.commandBridgeRows[0].submitPlan.canSubmit, false);
assert.equal(plan.commandBridgeRows[0].submitPlan.commandSource, "unavailable");
assert.equal(plan.commandBridgeRows[0].submitPlan.commandSourceLabel, "等待服务端");
assert.equal(plan.commandBridgeRows[0].submitPlan.commandType, "PLAY_CARD");
assert.equal(plan.commandBridgeRows[0].submitPlan.fieldSummary, "0 覆盖 / 1 缺少 / 1 服务端");
assert.deepEqual(plan.commandBridgeRows[0].submitPlan.fields.map((field) => field.state), ["missing", "optional", "server"]);
assert.equal(plan.commandBridgeRows[0].submitPlan.firstBlockingGate?.key, "required-fields");
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
assert.equal(plan.commandBridgeRows[1].detailRoleLabel, "目标");
assert.equal(plan.commandBridgeRows[1].serverRoleSummary, "目标");
assert.equal(plan.commandBridgeRows[1].detailLinkLabel, "详情目标 / 候选目标");
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
    promptId: "prompt-1",
    snapshotTick: 99,
    candidates: [
      {
        action: "PLAY_CARD",
        commandTemplate: {
          bindings: [
            { field: "sourceObjectId", required: true, source: "selectedSource" },
            { asArray: true, field: "targetObjectIds", required: false, source: "selectedTargets" },
            { field: "cardNo", metadataKey: "cardNo", required: true, source: "requirementMetadata" }
          ],
          cmdType: "PLAY_CARD"
        },
        enabled: true,
        label: "打出卡牌",
        metadata: {
          sourceRequirements: [{ cardNo: "OGN-001/298", sourceObjectId: "source-1" }]
        },
        reason: "可提交"
      }
    ],
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
assert.equal(draftPlan.evidenceRows.find((row) => row.key === "path")?.value, "2 路径 / 2 可送 / 0 待选 / 0 阻断 / 0 未进");
assert.equal(draftPlan.evidenceRows.find((row) => row.key === "path")?.state, "ready");
assert.equal(draftPlan.nextStep.state, "ready");
assert.equal(draftPlan.nextStep.headline, "可送服务端校验");
assert.equal(draftPlan.nextStep.commandType, "PLAY_CARD");
assert.deepEqual(draftPlan.nextStep.checks.map((check) => check.state), ["ready", "ready", "ready", "ready", "ready"]);
assert.deepEqual(draftPlan.nextStep.steps.map((step) => step.state), ["locked", "selected", "ready"]);
assert.equal(draftPlan.routeSummary.state, "ready");
assert.equal(draftPlan.routeSummary.stateLabel, "可提交");
assert.equal(draftPlan.routeSummary.headline, "存在可提交路线");
assert.equal(draftPlan.routeSummary.body, "打出卡牌 / PLAY_CARD");
assert.equal(draftPlan.routeSummary.nextStepLabel, "可从卡牌详情或操作区送服务端校验。");
assert.equal(draftPlan.routeSummary.totalCount, 2);
assert.equal(draftPlan.routeSummary.readyCount, 2);
assert.equal(draftPlan.routeSummary.draftCount, 2);
assert.deepEqual(draftPlan.routeSummary.rows.map((row) => `${row.key}:${row.value}:${row.state}`), [
  "ready:2:ready",
  "selecting:0:empty",
  "blocked:0:empty",
  "inactive:0:empty",
  "draft:2:selecting"
]);
assert.equal(draftPlan.commandBridgeRows[0].draftActive, true);
assert.equal(draftPlan.commandBridgeRows[0].routeState, "ready");
assert.equal(draftPlan.commandBridgeRows[0].routeStateLabel, "可送服务端校验");
assert.equal(draftPlan.commandBridgeRows[0].commandFieldSummary, "2 覆盖 / 0 缺少 / 1 服务端");
assert.equal(draftPlan.commandBridgeRows[0].fieldCoverageSummary, "详情 2/3 / 草稿 2 / 服务端 1 / 缺少 0");
assert.deepEqual(draftPlan.commandBridgeRows[0].commandFields.map((field) => field.state), ["covered", "covered", "server"]);
assert.deepEqual(draftPlan.commandBridgeRows[0].commandFields.map((field) => `${field.field}:${field.detailObjectCount}:${field.selectedChoiceCount}:${field.candidateChoiceCount}`), [
  "sourceObjectId:1:1:1",
  "targetObjectIds:1:1:1",
  "cardNo:0:0:0"
]);
assert.deepEqual(draftPlan.commandBridgeRows[0].commandFields.map((field) => field.coverageLabel), [
  "草稿已选来源：手牌闪电",
  "草稿已选目标：目标牌",
  "服务端根据候选元数据注入"
]);
assert.equal(draftPlan.commandBridgeRows[0].grammarState, "ready");
assert.equal(draftPlan.commandBridgeRows[0].grammarStateLabel, "可提交");
assert.equal(draftPlan.commandBridgeRows[0].grammarSummary, "打出卡牌 / 可提交 / 仅有模板 / 提交服务端候选");
assert.deepEqual(draftPlan.commandBridgeRows[0].grammarSteps.map((step) => step.role), ["source", "target", "submit"]);
assert.deepEqual(draftPlan.commandBridgeRows[0].grammarSteps.map((step) => step.state), ["locked", "selected", "ready"]);
assert.equal(draftPlan.commandBridgeRows[0].gateSummary, "5 通过 / 0 阻断 / 0 等待");
assert.deepEqual(draftPlan.commandBridgeRows[0].gateRows.map((gate) => gate.state), ["ready", "ready", "ready", "ready", "ready"]);
assert.equal(draftPlan.commandBridgeRows[0].submitPlan.state, "ready");
assert.equal(draftPlan.commandBridgeRows[0].submitPlan.stateLabel, "可送服务端");
assert.equal(draftPlan.commandBridgeRows[0].submitPlan.canSubmit, true);
assert.equal(draftPlan.commandBridgeRows[0].submitPlan.commandSource, "composer");
assert.equal(draftPlan.commandBridgeRows[0].submitPlan.commandSourceLabel, "服务端组合");
assert.deepEqual(draftPlan.commandBridgeRows[0].submitPlan.command, {
  cardNo: "OGN-001/298",
  cmdType: "PLAY_CARD",
  promptId: "prompt-1",
  snapshotTick: 99,
  sourceObjectId: "source-1",
  targetObjectIds: ["target-1"]
});
assert.equal(draftPlan.commandBridgeRows[0].submitPlan.commandType, "PLAY_CARD");
assert.equal(draftPlan.commandBridgeRows[0].submitPlan.submitLabel, "提交 PLAY_CARD");
assert.equal(draftPlan.commandBridgeRows[0].submitPlan.fieldSummary, "2 覆盖 / 0 缺少 / 1 服务端");
assert.equal(draftPlan.commandBridgeRows[0].submitPlan.firstBlockingGate, undefined);
assert.deepEqual(draftPlan.commandBridgeRows[0].submitPlan.fields.map((field) => field.state), ["covered", "covered", "server"]);
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
assert.equal(disconnectedDraftPlan.evidenceRows.find((row) => row.key === "path")?.value, "2 路径 / 0 可送 / 0 待选 / 2 阻断 / 0 未进");
assert.equal(disconnectedDraftPlan.evidenceRows.find((row) => row.key === "path")?.state, "blocked");
assert.equal(disconnectedDraftPlan.nextStep.state, "blocked");
assert.equal(disconnectedDraftPlan.nextStep.headline, "服务端暂不允许");
assert.deepEqual(disconnectedDraftPlan.nextStep.checks.map((check) => check.state), ["ready", "blocked", "ready", "ready", "blocked"]);
assert.deepEqual(disconnectedDraftPlan.nextStep.steps.map((step) => step.state), ["locked", "selected", "blocked"]);
assert.equal(disconnectedDraftPlan.routeSummary.state, "blocked");
assert.equal(disconnectedDraftPlan.routeSummary.stateLabel, "阻断");
assert.equal(disconnectedDraftPlan.routeSummary.headline, "提交路线阻断");
assert.equal(disconnectedDraftPlan.routeSummary.body, "打出卡牌 / 3 通过 / 2 阻断 / 0 等待");
assert.equal(disconnectedDraftPlan.routeSummary.nextStepLabel, "3 通过 / 2 阻断 / 0 等待");
assert.equal(disconnectedDraftPlan.routeSummary.totalCount, 2);
assert.equal(disconnectedDraftPlan.routeSummary.blockedCount, 2);
assert.equal(disconnectedDraftPlan.routeSummary.draftCount, 2);
assert.equal(disconnectedDraftPlan.commandBridgeRows[0].grammarState, "blocked");
assert.equal(disconnectedDraftPlan.commandBridgeRows[0].grammarSummary, "打出卡牌 / 阻断 / 仅有模板 / 提交入口阻断");
assert.equal(disconnectedDraftPlan.commandBridgeRows[0].gateSummary, "3 通过 / 2 阻断 / 0 等待");
assert.deepEqual(disconnectedDraftPlan.commandBridgeRows[0].gateRows.map((gate) => gate.key), ["server-candidate", "connection", "player-draft", "required-fields", "submit-step"]);
assert.deepEqual(disconnectedDraftPlan.commandBridgeRows[0].gateRows.map((gate) => gate.state), ["ready", "blocked", "ready", "ready", "blocked"]);
assert.equal(disconnectedDraftPlan.commandBridgeRows[0].submitPlan.state, "blocked");
assert.equal(disconnectedDraftPlan.commandBridgeRows[0].submitPlan.stateLabel, "提交阻断");
assert.equal(disconnectedDraftPlan.commandBridgeRows[0].submitPlan.canSubmit, false);
assert.equal(disconnectedDraftPlan.commandBridgeRows[0].submitPlan.firstBlockingGate?.key, "connection");
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

const sameObjectRolePlan = buildWireTimelineDetailPlan({
  detail: {
    id: "rule:stack:shared",
    lines: [],
    refs: [
      { id: "shared-object", role: "来源" },
      { id: "shared-object", role: "目标" },
      { id: "shared-object", role: "目标" }
    ],
    source: "rule",
    title: "同对象多角色"
  },
  objectIndex: {
    "shared-object": { objectId: "shared-object", cardNo: "SHARED-001" }
  },
  objectContextById: {
    "shared-object": {
      candidateLinks: [
        {
          commandFields: ["来源:sourceObjectId*", "目标:targetObjectIds*"],
          commandType: "PLAY_CARD",
          composerReason: "服务端已公开组合提交。",
          composerState: "server",
          composerStateLabel: "服务端声明",
          enabled: true,
          label: "同对象候选",
          reason: "",
          requiredCommandFields: ["来源:sourceObjectId*"],
          roles: ["来源", "目标"]
        }
      ],
      eventLinks: [],
      objectId: "shared-object",
      promptDisabledCount: 0,
      promptEnabledCount: 1,
      stackRoles: [],
      stateLabels: [],
      zone: { kind: "battlefield", label: "共享对象区域" }
    }
  },
  prompt: {
    __model: {
      candidates: [
        {
          action: "PLAY_CARD",
          choices: [
            { id: "shared-source", label: "共享来源", objectIds: ["shared-object"], role: "source" },
            { id: "shared-target", label: "共享目标", objectIds: ["shared-object"], role: "target" }
          ],
          command: {
            bindings: [
              { field: "sourceObjectId", required: true, role: "source", roleLabel: "来源", source: "selectedSource" },
              { field: "targetObjectIds", required: true, role: "target", roleLabel: "目标", source: "selectedTargets" }
            ],
            cmdType: "PLAY_CARD"
          },
          enabled: true,
          label: "同对象候选",
          reason: "可提交",
          steps: [
            { count: 1, label: "来源", required: true, role: "source", sampleLabels: ["共享来源"] },
            { count: 1, label: "目标", required: true, role: "target", sampleLabels: ["共享目标"] }
          ]
        }
      ]
    }
  }
});

assert.deepEqual(
  sameObjectRolePlan.projectionRows.map((row) => `${row.role}:${row.id}`),
  ["来源:shared-object", "目标:shared-object"]
);
assert.deepEqual(
  sameObjectRolePlan.navigationRows.map((row) => `${row.role}:${row.objectId}`),
  ["来源:shared-object", "目标:shared-object"]
);
assert.deepEqual(
  sameObjectRolePlan.actionHintRows.map((row) => `${row.role}:${row.objectId}`),
  ["来源:shared-object", "目标:shared-object"]
);
assert.deepEqual(
  sameObjectRolePlan.commandBridgeRows.map((row) => `${row.detailRoleLabel}:${row.detailObjectId}`),
  ["来源:shared-object", "目标:shared-object"]
);

console.log("Wire timeline detail plan check passed.");

function buildPromptInteractionModel(prompt) {
  return prompt?.__model ?? { candidates: [] };
}

function commandFromActionPromptTemplate(template, selection, context) {
  if (!template?.cmdType) {
    return undefined;
  }

  const command = { cmdType: template.cmdType };
  for (const binding of template.bindings ?? []) {
    const value = commandTemplateValue(binding, selection, context);
    const missing = value == null
      || value === ""
      || (Array.isArray(value) && value.length === 0);
    if (missing && binding.required) {
      return undefined;
    }
    if (missing && binding.omitEmpty !== false) {
      continue;
    }
    command[binding.field] = value;
  }

  return command;
}

function commandTemplateValue(binding, selection, context) {
  switch (binding.source) {
    case "selectedSource":
      return selection.sourceId;
    case "selectedTargets":
      return binding.asArray ? selection.targetObjectIds ?? [] : selection.targetObjectIds?.[0];
    case "requirementMetadata":
      return context?.requirement?.[binding.metadataKey];
    default:
      return undefined;
  }
}

function candidateComposerKey(candidate) {
  return `${candidate.action}::${candidate.label}`;
}

function promptStampedCommand(command, prompt) {
  return {
    ...command,
    promptId: command.promptId ?? prompt?.promptId ?? null,
    snapshotTick: command.snapshotTick ?? prompt?.snapshotTick ?? null
  };
}

function sourceRequirementFor(candidate, sourceObjectId) {
  const requirements = candidate?.metadata?.sourceRequirements;
  return Array.isArray(requirements)
    ? requirements.find((item) => item?.sourceObjectId === sourceObjectId)
    : undefined;
}

function commandSourceCopy(source) {
  return {
    composer: {
      detail: "先选择来源、目标或模式，再按服务端模板提交。",
      label: "服务端组合"
    },
    unavailable: {
      detail: "当前候选没有可提交命令或完整组合计划。",
      label: "等待服务端"
    }
  }[source];
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
