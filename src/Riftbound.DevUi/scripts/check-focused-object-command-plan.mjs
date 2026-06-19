import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/focusedObjectCommandPlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { buildFocusedObjectCommandPlan } = moduleShim.exports;

const plan = buildFocusedObjectCommandPlan({
  context: {
    candidateLinks: [
      {
        commandFields: ["来源:sourceObjectId*", "目标:targetObjectId"],
        commandType: "PLAY_CARD",
        enabled: true,
        label: "打出卡牌",
        reason: "可提交",
        requiredCommandFields: ["来源:sourceObjectId*"],
        roles: ["来源"]
      },
      {
        commandFields: ["来源:sourceObjectId*"],
        commandType: "ACTIVATE_ABILITY",
        enabled: false,
        label: "启动能力",
        reason: "窗口不允许",
        requiredCommandFields: ["来源:sourceObjectId*"],
        roles: ["来源"]
      }
    ],
    candidateSource: "server",
    cardNo: "OGN-001/298",
    controllerId: "P1",
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

assert.equal(plan.statusCards.length, 5);
assert.equal(plan.statusCards[0].value, "我方手牌");
assert.equal(plan.statusCards[3].value, "服务端索引");
assert.equal(plan.commandRows.length, 2);
assert.equal(plan.commandRows[0].commandType, "PLAY_CARD");
assert.deepEqual(plan.commandRows[0].requiredFields, ["来源:sourceObjectId*"]);
assert.deepEqual(plan.commandRows[0].secondaryFields, ["目标:targetObjectId"]);
assert.equal(plan.commandRows[1].enabled, false);
assert.equal(plan.nextStepRows[0].nextStepLabel, "目标");
assert.equal(plan.eventRows[0].kind, "OBJECT_EXHAUSTED");
assert.equal(plan.contract.hiddenMetadataCount, 1);
assert.equal(plan.contract.requiredPayloadCount, 2);
assert.equal(JSON.stringify(plan).includes("serverPaymentState"), false);

console.log("Focused object command plan check passed.");
