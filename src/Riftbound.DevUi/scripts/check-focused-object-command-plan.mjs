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
const commandFieldDisplaySource = readFileSync(commandFieldDisplayPath, "utf8");
const commandFieldDisplayOutput = transpile(commandFieldDisplaySource);
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

const wireActionSyntaxPath = resolve(scriptDir, "../src/utils/wireActionSyntaxPlan.ts");
const wireActionSyntaxOutput = transpile(readFileSync(wireActionSyntaxPath, "utf8"));
const wireActionSyntaxModuleShim = { exports: {} };
new Function(
  "exports",
  "module",
  "promptChoiceRoleFromString",
  "promptChoiceRoleLabel",
  wireActionSyntaxOutput
)(
  wireActionSyntaxModuleShim.exports,
  wireActionSyntaxModuleShim,
  promptChoiceRoleFromString,
  promptChoiceRoleLabel
);

const sourcePath = resolve(scriptDir, "../src/utils/focusedObjectCommandPlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = transpile(source);
const moduleShim = { exports: {} };

new Function(
  "exports",
  "module",
  "commandFieldLabelsForCandidate",
  "tableObjectContextSourceLabel",
  "buildWireActionSyntaxPlanFromTableContext",
  output
)(
  moduleShim.exports,
  moduleShim,
  candidateSemanticsModuleShim.exports.commandFieldLabelsForCandidate,
  tableObjectContextSourceLabel,
  wireActionSyntaxModuleShim.exports.buildWireActionSyntaxPlanFromTableContext
);

const { buildFocusedObjectCommandPlan } = moduleShim.exports;

const plan = buildFocusedObjectCommandPlan({
  context: {
    candidateLinks: [
      {
        category: "play",
        commandFields: ["来源:sourceObjectId*", "服务端:cardNo*", "目标:targetObjectId"],
        commandType: "PLAY_CARD",
        composerReason: "服务端已公开组合提交。",
        composerState: "server",
        composerStateLabel: "服务端声明",
        enabled: true,
        intent: "play-card",
        label: "打出卡牌",
        priority: 100,
        reason: "可提交",
        requiredCommandFields: ["来源:sourceObjectId*", "服务端:cardNo*"],
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
        priority: 160,
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
    contextBoundary: "服务端对象上下文只公开当前行动提示中的对象候选、选择角色和命令字段；隐藏 metadata 不进入对象上下文。",
    contextSource: "server-action-prompt",
    eventLinks: [
      { description: "进场", kind: "UNIT_ENTERED", role: "对象" },
      { description: "横置", kind: "OBJECT_EXHAUSTED", role: "对象" }
    ],
    objectId: "P1-HAND-001",
    ownerId: "P1",
    promptDisabledCount: 1,
    promptEnabledCount: 1,
    stackRoles: ["结算链来源"],
    stateLabels: ["横置", "伤害 1"],
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
    validationErrors: ["invalid target"],
    visibleMetadata: ["sourceRequirements"]
  },
  focusModel: {
    blockedCount: 1,
    blockingReasons: ["窗口不允许"],
    candidates: [
      {
        candidate: {
          action: "PLAY_CARD",
          enabled: true,
          label: "打出卡牌",
          reason: "可提交",
          choices: [],
          steps: []
        },
        key: "PLAY_CARD::打出卡牌",
        nextStep: {
          count: 1,
          label: "目标",
          required: true,
          role: "target",
          sampleLabels: ["敌方单位"]
        },
        stateLabel: "可提交候选"
      }
    ],
    enabledCount: 1,
    nextStepLabel: "下一步：目标",
    sourceObjectId: "P1-HAND-001",
    stateLabel: "1 个可提交候选",
    submittedByServer: true,
    totalCount: 2
  }
});

assert.equal(plan.authorityState, "server");
assert.equal(plan.authorityLabel, "服务端对象上下文");
assert.equal(plan.contextSourceLabel, "服务端对象上下文");
assert.equal(plan.statusCards.length, 5);
assert.equal(plan.statusCards[0].value, "我方手牌");
assert.equal(plan.statusCards[3].value, "服务端对象上下文");
assert.deepEqual(
  plan.sectionRows.map((row) => `${row.key}:${row.state}:${row.count}:${row.stateLabel}`),
  [
    "identity:ready:1:已定位",
    "authority:server:2:服务端对象上下文",
    "syntax:warning:3:缺少 1",
    "commands:ready:2:有可提交",
    "relations:empty:0:无关联",
    "stack:ready:1:有关联",
    "events:ready:2:有记录",
    "contract:server:1:服务端契约"
  ]
);
assert.ok(plan.sectionRows.find((row) => row.key === "authority")?.summary.includes("服务端对象上下文"));
assert.ok(plan.sectionRows.find((row) => row.key === "commands")?.summary.includes("1 可用 / 1 阻断"));
assert.ok(plan.sectionRows.find((row) => row.key === "syntax")?.summary.includes("还需 目标"));
assert.ok(plan.boundaryLabel.includes("服务端对象上下文"));
assert.equal(plan.commandRows.length, 2);
assert.equal(plan.commandRows[0].commandType, "PLAY_CARD");
assert.equal(plan.commandRows[0].category, "play");
assert.equal(plan.commandRows[0].intent, "play-card");
assert.equal(plan.commandRows[0].priority, 100);
assert.equal(plan.commandRows[0].uiHint, "card-action");
assert.equal(plan.commandRows[0].composerState, "server");
assert.equal(plan.commandRows[0].composerStateLabel, "服务端声明");
assert.equal(plan.commandRows[0].stepSummary, "来源* 1/1 / 目标* 0/2");
assert.deepEqual(plan.commandRows[0].requiredFields, ["来源:sourceObjectId*", "服务端字段*"]);
assert.deepEqual(plan.commandRows[0].secondaryFields, ["目标:targetObjectId"]);
assert.equal(plan.commandRows[1].enabled, false);
assert.equal(plan.commandRows[1].category, "ability");
assert.equal(plan.nextStepRows[0].nextStepLabel, "目标");
assert.equal(plan.eventRows[0].kind, "OBJECT_EXHAUSTED");
assert.equal(plan.contract.hiddenMetadataCount, 1);
assert.equal(plan.contract.requiredPayloadCount, 2);
assert.equal(plan.syntax.rows.length, 3);
assert.equal(plan.syntax.usableCount, 2);
assert.equal(plan.syntax.missingRequiredCount, 1);
assert.ok(plan.syntax.summary.includes("可作为 来源"));
assert.ok(plan.syntax.summary.includes("还需 目标"));
assert.deepEqual(
  plan.syntax.rows.map((row) =>
    `${row.source}:${row.roleLabel}:${row.state}:${row.objectChoiceCount}/${row.choiceCount}:${row.required}:${row.candidateLabel}`),
  [
    "object-context:来源:usable-required:1/1:true:启动能力",
    "object-context:来源:usable-required:1/1:true:打出卡牌",
    "object-context:目标:missing-required:0/2:true:打出卡牌"
  ]
);
assert.equal(JSON.stringify(plan).includes("服务端:cardNo"), false);
assert.equal(JSON.stringify(plan).includes("serverPaymentState"), false);

console.log("Focused object command plan check passed.");

function tableObjectContextSourceLabel(context) {
  switch (context?.contextSource) {
    case "server-action-prompt":
      return "服务端对象上下文";
    case "prompt-public-derived":
      return "公开候选只读派生";
    case "snapshot-public-index":
      return "公开快照索引";
    default:
      switch (context?.candidateSource) {
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
}

function promptChoiceRoleFromString(role) {
  return ["source", "target", "destination", "mode", "optionalCost"].includes(role) ? role : undefined;
}

function promptChoiceRoleLabel(role) {
  return {
    destination: "位置",
    mode: "模式",
    optionalCost: "费用",
    source: "来源",
    target: "目标"
  }[role] ?? role;
}
