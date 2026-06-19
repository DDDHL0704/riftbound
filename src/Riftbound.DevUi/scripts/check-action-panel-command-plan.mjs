import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/actionPanelCommandPlan.ts");
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
  "commandFromActionPromptTemplate",
  "sourceRequirementFor",
  "findCardNo",
  "canComposeActionCandidate",
  output
)(
  moduleShim.exports,
  moduleShim,
  commandFromActionPromptTemplate,
  sourceRequirementFor,
  findCardNo,
  canComposeActionCandidate
);

const { buildActionPanelCandidateCommandPlan } = moduleShim.exports;

const ready = plan({
  action: "READY",
  enabled: true,
  label: "准备",
  reason: "可准备"
});
assert.equal(ready.directAction, "ready");
assert.equal(ready.command, undefined);
assert.equal(ready.disabled, false);
assert.equal(ready.icon, "check");
assert.equal(ready.variant, "primary");

const submitDeckDisconnected = plan({
  action: "SUBMIT_DECK",
  enabled: true,
  label: "提交构筑",
  reason: "可提交"
}, { disabledByConnection: true });
assert.equal(submitDeckDisconnected.directAction, "submitDeck");
assert.equal(submitDeckDisconnected.disabled, true);
assert.equal(submitDeckDisconnected.icon, "send");

const endTurn = plan({
  action: "END_TURN",
  enabled: true,
  label: "结束回合",
  reason: "可结束"
});
assert.deepEqual(endTurn.command, { cmdType: "END_TURN" });
assert.equal(endTurn.needsComposer, false);
assert.equal(endTurn.icon, "play");

const surrender = plan({
  action: "SURRENDER",
  enabled: true,
  label: "投降",
  reason: "确认"
});
assert.deepEqual(surrender.command, { cmdType: "SURRENDER" });
assert.equal(surrender.icon, "flag");
assert.equal(surrender.variant, "danger");

const payCost = plan({
  action: "PAY_COST",
  enabled: true,
  label: "支付费用",
  metadata: {
    paymentChoiceIds: ["rune-1", "rune-2"],
    paymentId: "payment-1",
    paymentWindow: "main"
  },
  reason: "可支付"
});
assert.deepEqual(payCost.command, {
  cmdType: "PAY_COST",
  paymentChoiceIds: ["rune-1", "rune-2"],
  paymentId: "payment-1",
  paymentWindow: "main"
});
assert.equal(payCost.disabled, false);

const missingPayCost = plan({
  action: "PAY_COST",
  enabled: true,
  label: "支付费用",
  metadata: { paymentId: "payment-1" },
  reason: "服务端尚未提供完整支付窗口"
});
assert.equal(missingPayCost.command, undefined);
assert.equal(missingPayCost.disabled, true);
assert.equal(missingPayCost.labelSuffix, "（需选择）");

const snapshot = {
  players: {
    P1: {
      objects: {
        "hand-1": { cardNo: "OGN-001/298", objectId: "hand-1" }
      }
    }
  }
};

const singleSourcePlay = plan({
  action: "PLAY_CARD",
  enabled: true,
  label: "打出卡牌",
  reason: "可打出",
  sources: [{ id: "hand-1", label: "测试卡", objectIds: ["hand-1"] }]
}, { snapshot });
assert.deepEqual(singleSourcePlay.command, {
  cardNo: "OGN-001/298",
  cmdType: "PLAY_CARD",
  sourceObjectId: "hand-1",
  targetObjectIds: []
});
assert.equal(singleSourcePlay.needsComposer, false);

const targetRequiredPlay = plan({
  action: "PLAY_CARD",
  enabled: true,
  label: "打出卡牌",
  reason: "需要目标",
  sources: [{ id: "hand-1", label: "测试卡", objectIds: ["hand-1"] }],
  targets: [{ id: "target-1", label: "目标", objectIds: ["target-1"] }]
}, { snapshot });
assert.equal(targetRequiredPlay.command, undefined);
assert.equal(targetRequiredPlay.needsComposer, true);
assert.equal(targetRequiredPlay.disabled, false);
assert.equal(targetRequiredPlay.labelSuffix, "");

const templated = plan({
  action: "ACTIVATE_ABILITY",
  commandTemplate: {
    bindings: [
      { field: "sourceObjectId", required: true, source: "selectedSource" },
      { field: "abilityId", metadataKey: "abilityId", required: true, source: "requirementMetadata" }
    ],
    cmdType: "ACTIVATE_ABILITY"
  },
  enabled: true,
  label: "启动能力",
  metadata: {
    sourceRequirements: [
      { abilityId: "ability-1", sourceObjectId: "unit-1" }
    ]
  },
  reason: "可启动",
  sources: [{ id: "unit-1", label: "单位", objectIds: ["unit-1"] }]
});
assert.deepEqual(templated.command, {
  abilityId: "ability-1",
  cmdType: "ACTIVATE_ABILITY",
  sourceObjectId: "unit-1"
});
assert.equal(templated.needsComposer, false);

const unknownNeedsChoice = plan({
  action: "FUTURE_ACTION",
  enabled: true,
  label: "未来行动",
  reason: "服务端未开放"
});
assert.equal(unknownNeedsChoice.disabled, true);
assert.equal(unknownNeedsChoice.icon, "hourglass");
assert.equal(unknownNeedsChoice.labelSuffix, "（需选择）");

const wait = plan({
  action: "WAIT",
  enabled: false,
  label: "等待",
  reason: "等待服务端"
});
assert.equal(wait.disabled, true);
assert.equal(wait.icon, "hourglass");
assert.equal(wait.labelSuffix, "");

console.log("Action panel command plan check passed.");

function plan(candidate, options = {}) {
  return buildActionPanelCandidateCommandPlan({
    candidate,
    disabledByConnection: options.disabledByConnection ?? false,
    snapshot: options.snapshot
  });
}

function commandFromActionPromptTemplate(template, selection, requirement) {
  if (!template?.cmdType) {
    return undefined;
  }

  const command = { cmdType: template.cmdType };
  for (const binding of template.bindings ?? []) {
    let value;
    if (binding.source === "selectedSource") {
      value = selection.sourceId;
    } else if (binding.source === "requirementMetadata") {
      value = requirement?.[binding.metadataKey];
    }
    if (binding.required && !value) {
      return undefined;
    }
    if (value) {
      command[binding.field] = value;
    }
  }
  return command;
}

function sourceRequirementFor(candidate, sourceObjectId) {
  const records = candidate.metadata?.sourceRequirements;
  return Array.isArray(records)
    ? records.find((record) => record.sourceObjectId === sourceObjectId)
    : undefined;
}

function findCardNo(snapshot, objectId) {
  for (const player of Object.values(snapshot?.players ?? {})) {
    const cardNo = player.objects?.[objectId]?.cardNo;
    if (cardNo) {
      return cardNo;
    }
  }
  return undefined;
}

function canComposeActionCandidate(candidate) {
  return Boolean(candidate.commandTemplate)
    || ["PLAY_CARD", "HIDE_CARD", "REVEAL_CARD", "MOVE_UNIT", "ASSEMBLE_EQUIPMENT", "DECLARE_BATTLE", "ACTIVATE_ABILITY", "LEGEND_ACT"]
      .includes(candidate.action);
}
