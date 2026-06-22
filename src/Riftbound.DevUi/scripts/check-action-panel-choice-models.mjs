import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/actionPanelChoiceModels.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", "redactInternalText", output)(
  moduleShim.exports,
  moduleShim,
  redactInternalText
);

const {
  buildDamageAssignmentModel,
  buildHandChoiceModel,
  buildOrderTriggersModel,
  buildPayCostModel,
  clampDamageInput,
  findCardNo
} = moduleShim.exports;

const handModel = buildHandChoiceModel({
  action: "CHOOSE_HAND_CARDS",
  enabled: true,
  label: "选择手牌",
  metadata: {
    choiceId: "choice-1",
    choiceWindow: "discard-for-effect",
    choosingPlayerId: "P1",
    effectKind: "DISCARD_THEN_DRAW",
    handChoices: [
      { objectId: "hand-1", label: "可选手牌", reason: "服务端候选" },
      { objectId: "hand-2", label: "非法手牌" },
      "hand-3"
    ],
    legalObjectIds: ["hand-1", "hand-3"],
    maxCount: 2,
    reason: "选择 1-2 张",
    requiredCount: 1
  },
  reason: "可选择"
}, undefined);

assert.equal(handModel.choiceId, "choice-1");
assert.equal(handModel.choiceWindow, "discard-for-effect");
assert.deepEqual(handModel.handChoices.map((choice) => choice.objectId), ["hand-1", "hand-3"]);
assert.equal(handModel.handChoices[1].label, "服务端手牌候选");
assert.equal(handModel.requiredCount, 1);
assert.equal(handModel.maxCount, 2);

const snapshot = {
  players: {
    P1: {
      objects: {
        attacker: { cardNo: "OGN-010/298", damage: 0, objectId: "attacker" }
      }
    },
    P2: {
      objects: {
        defender: { cardNo: "OGN-020/298", damage: 1, objectId: "defender" },
        secondDefender: { cardNo: "OGN-021/298", damage: 0, objectId: "secondDefender" }
      }
    }
  }
};

const damageModel = buildDamageAssignmentModel({
  action: "ASSIGN_COMBAT_DAMAGE",
  enabled: true,
  label: "分配伤害",
  metadata: {
    assignmentChoices: [
      { id: "attacker->defender", lethalThreshold: 3, sourceDamagePool: 4 },
      { sourceObjectId: "attacker", targetObjectId: "secondDefender", targetLabel: "第二目标" }
    ],
    battleId: "battle-1",
    battlefieldId: "bf-1"
  },
  reason: "服务端战斗候选"
}, undefined, snapshot);

assert.equal(damageModel.battleId, "battle-1");
assert.equal(damageModel.battlefieldId, "bf-1");
assert.deepEqual(damageModel.choices.map((choice) => choice.key), ["attacker->defender", "attacker->secondDefender"]);
assert.equal(damageModel.choices[0].sourceLabel, "OGN-010/298 · attacker");
assert.equal(damageModel.choices[0].targetLabel, "OGN-020/298 · defender");
assert.equal(damageModel.choices[0].existingDamage, 1);
assert.equal(damageModel.choices[0].lethalThreshold, 3);
assert.equal(damageModel.choices[1].targetLabel, "第二目标");

const legalTargetsModel = buildDamageAssignmentModel({
  action: "ASSIGN_COMBAT_DAMAGE",
  enabled: true,
  label: "分配伤害",
  metadata: {
    damagePoolBySource: { attacker: 2 },
    legalTargetsBySource: { attacker: ["defender"] }
  },
  reason: "服务端战斗候选"
}, {
  view: {
    message: "",
    relatedBattleId: "battle-from-prompt",
    relatedBattlefieldId: "bf-from-prompt",
    title: "",
    type: "ASSIGN_COMBAT_DAMAGE"
  }
}, snapshot);

assert.equal(legalTargetsModel.battleId, "battle-from-prompt");
assert.equal(legalTargetsModel.battlefieldId, "bf-from-prompt");
assert.equal(legalTargetsModel.damagePoolLabel, "1 个来源");
assert.equal(legalTargetsModel.choices[0].sourceDamagePool, 2);

const triggerModel = buildOrderTriggersModel({
  action: "ORDER_TRIGGERS",
  enabled: true,
  label: "排列触发",
  metadata: {
    constraints: ["先结算同一玩家触发"],
    triggeredByEventKind: "UNIT_ENTERS_BATTLEFIELD",
    triggers: [
      { controllerId: "P1", sourceCardNo: "OGN-030/298", summary: "第三触发", triggerId: "t3" },
      { controllerId: "P1", summary: "第一触发", triggerId: "t1" }
    ],
    triggerIds: ["t1", "t2", "t3"]
  },
  reason: "可排序",
  sources: [
    { id: "t2", label: "第二触发", reason: "服务端候选" }
  ]
}, undefined);

assert.deepEqual(triggerModel.triggers.map((trigger) => trigger.triggerId), ["t1", "t2", "t3"]);
assert.equal(triggerModel.triggers[0].summary, "第一触发");
assert.equal(triggerModel.triggers[1].label, "第二触发");
assert.equal(triggerModel.constraints[0], "先结算同一玩家触发");
assert.equal(triggerModel.triggeredByEventKind, "UNIT_ENTERS_BATTLEFIELD");

const payCostModel = buildPayCostModel({
  action: "PAY_COST",
  enabled: true,
  label: "支付费用",
  metadata: {
    cost: { mana: 1, power: 2 },
    paymentChoices: [
      { id: "SPEND_MANA:1", label: "支付 1 法力", reason: "服务端支付候选" }
    ],
    paymentChoiceIds: ["RECYCLE_RUNE:rune-1", "SPEND_MANA:1"],
    paymentId: "payment-1",
    paymentResourceChoices: [
      "RECYCLE_RUNE:rune-1",
      { id: "TEMP_RESOURCE:boost-1", label: "临时资源", description: "服务端资源动作" }
    ],
    paymentWindow: "PLAY_CARD",
    resourceLedgerBeforePayment: { mana: 1 },
    serverPaymentState: "PENDING"
  }
}, undefined);

assert.equal(payCostModel.paymentId, "payment-1");
assert.equal(payCostModel.paymentWindow, "PLAY_CARD");
assert.deepEqual(payCostModel.choices.map((choice) => choice.id), ["RECYCLE_RUNE:rune-1", "TEMP_RESOURCE:boost-1", "SPEND_MANA:1"]);
assert.deepEqual(payCostModel.choices.map((choice) => choice.source), ["resource", "resource", "spend"]);
assert.deepEqual(payCostModel.paymentChoiceIds, ["RECYCLE_RUNE:rune-1", "SPEND_MANA:1"]);
assert.equal(payCostModel.choices[1].reason, "服务端资源动作");
assert.ok(!String(payCostModel.costLabel).includes("serverPaymentState"));

assert.equal(clampDamageInput(2.8), 2);
assert.equal(clampDamageInput(-1), 0);
assert.equal(findCardNo(snapshot, "attacker"), "OGN-010/298");
assert.equal(findCardNo(snapshot, "missing"), undefined);

console.log("Action panel choice models check passed.");

function redactInternalText(value) {
  return String(value).replace(/privateChoiceGraph|serverPaymentState/g, "服务端字段");
}
