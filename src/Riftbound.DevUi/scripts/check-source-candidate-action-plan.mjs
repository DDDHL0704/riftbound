import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/sourceCandidateActionPlan.ts");
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
  "commandForSourceCandidate",
  "canComposeActionCandidate",
  "promptActionLabel",
  "promptReasonTitle",
  output
)(
  moduleShim.exports,
  moduleShim,
  commandForSourceCandidate,
  canComposeActionCandidate,
  promptActionLabel,
  promptReasonTitle
);

const { buildSourceCandidateActionPlan } = moduleShim.exports;

const tapRune = plan({
  action: "TAP_RUNE",
  enabled: true,
  label: "横置符文",
  reason: "可支付",
  sources: [{ id: "rune-1", label: "符文 1" }]
});
assert.deepEqual(tapRune.command, { cmdType: "TAP_RUNE", sourceObjectId: "rune-1" });
assert.equal(tapRune.disabled, false);
assert.equal(tapRune.needsComposer, false);
assert.equal(tapRune.label, "横置符文");
assert.equal(tapRune.title, "原因：可支付");
assert.equal(tapRune.variant, "primary");

const sourcePlay = plan({
  action: "PLAY_CARD",
  enabled: true,
  label: "打出卡牌",
  reason: "需要选择费用或目标",
  sources: [{ id: "hand-1", label: "手牌" }],
  targets: [{ id: "target-1", label: "目标" }]
}, { sourceObjectId: "hand-1" });
assert.equal(sourcePlay.command, undefined);
assert.equal(sourcePlay.needsComposer, true);
assert.equal(sourcePlay.disabled, false);
assert.equal(sourcePlay.labelSuffix, "");

const templatedWithTarget = plan({
  action: "ACTIVATE_ABILITY",
  commandTemplate: { cmdType: "ACTIVATE_ABILITY" },
  enabled: true,
  label: "启动能力",
  reason: "需要目标",
  sources: [{ id: "unit-1", label: "单位" }],
  targets: [{ id: "target-1", label: "目标" }]
}, { sourceObjectId: "unit-1" });
assert.equal(templatedWithTarget.command, undefined);
assert.equal(templatedWithTarget.needsComposer, true);
assert.equal(templatedWithTarget.disabled, false);

const templatedDirect = plan({
  action: "ACTIVATE_ABILITY",
  commandTemplate: { cmdType: "ACTIVATE_ABILITY" },
  enabled: true,
  label: "启动能力",
  reason: "不需要额外选择",
  sources: [{ id: "unit-1", label: "单位" }]
}, { sourceObjectId: "unit-1" });
assert.deepEqual(templatedDirect.command, { cmdType: "ACTIVATE_ABILITY", sourceObjectId: "unit-1" });
assert.equal(templatedDirect.needsComposer, false);

const readOnly = plan({
  action: "TAP_RUNE",
  enabled: true,
  label: "横置符文",
  reason: "可支付",
  sources: [{ id: "rune-1", label: "符文 1" }]
}, { canSubmitCommands: false });
assert.deepEqual(readOnly.command, { cmdType: "TAP_RUNE", sourceObjectId: "rune-1" });
assert.equal(readOnly.disabled, true);
assert.equal(readOnly.title, "当前视图只能查看，不能提交行动");

const disconnected = plan({
  action: "PLAY_CARD",
  enabled: true,
  label: "打出卡牌",
  reason: "连接断开时不能提交",
  sources: [{ id: "hand-1", label: "手牌" }]
}, { disabledByConnection: true, sourceObjectId: "hand-1" });
assert.equal(disconnected.needsComposer, true);
assert.equal(disconnected.disabled, true);
assert.equal(disconnected.title, "连接恢复前不能提交行动");

const incomplete = plan({
  action: "FUTURE_ACTION",
  enabled: true,
  label: "未来行动",
  reason: "服务端未开放"
});
assert.equal(incomplete.command, undefined);
assert.equal(incomplete.needsComposer, false);
assert.equal(incomplete.disabled, true);
assert.equal(incomplete.labelSuffix, "（需选择）");
assert.equal(incomplete.title, "该候选还需要服务端提供完整选择后才能提交");
assert.equal(incomplete.variant, "ghost");

const wait = plan({
  action: "WAIT",
  enabled: false,
  label: "等待",
  reason: "等待对手"
});
assert.equal(wait.labelSuffix, "");
assert.equal(wait.disabled, true);

console.log("Source candidate action plan check passed.");

function plan(candidate, options = {}) {
  return buildSourceCandidateActionPlan({
    canSubmitCommands: options.canSubmitCommands ?? true,
    candidate,
    disabledByConnection: options.disabledByConnection ?? false,
    sourceObjectId: options.sourceObjectId ?? "rune-1"
  });
}

function commandForSourceCandidate(candidate, sourceObjectId) {
  if (!sourceObjectId || !candidate.enabled) {
    return undefined;
  }
  if (candidate.action === "TAP_RUNE") {
    return { cmdType: "TAP_RUNE", sourceObjectId };
  }
  if (candidate.commandTemplate?.cmdType === "ACTIVATE_ABILITY") {
    return { cmdType: "ACTIVATE_ABILITY", sourceObjectId };
  }
  return undefined;
}

function canComposeActionCandidate(candidate) {
  return Boolean(candidate.commandTemplate)
    || ["PLAY_CARD", "HIDE_CARD", "REVEAL_CARD", "MOVE_UNIT", "ASSEMBLE_EQUIPMENT", "DECLARE_BATTLE", "ACTIVATE_ABILITY", "LEGEND_ACT"]
      .includes(candidate.action);
}

function promptActionLabel(candidate) {
  return candidate.label ?? candidate.action;
}

function promptReasonTitle(reason) {
  return reason ? `原因：${reason}` : "服务端候选";
}
