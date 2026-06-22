import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const promptInspectionSourcePath = resolve(scriptDir, "../src/utils/promptInspectionPlan.ts");
const promptInspectionOutput = ts.transpileModule(readFileSync(promptInspectionSourcePath, "utf8"), {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const promptInspectionModuleShim = { exports: {} };
new Function("exports", "module", promptInspectionOutput)(promptInspectionModuleShim.exports, promptInspectionModuleShim);

const sourcePath = resolve(scriptDir, "../src/utils/actionPanelPromptPlan.ts");
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
  "connectionStatusLabel",
  "promptActionLabel",
  "promptReasonLabel",
  "buildPromptInspectionPlan",
  "redactInternalText",
  "buildServerSubmissionGatePlan",
  output
)(
  moduleShim.exports,
  moduleShim,
  connectionStatusLabel,
  promptActionLabel,
  promptReasonLabel,
  promptInspectionModuleShim.exports.buildPromptInspectionPlan,
  redactInternalText,
  buildServerSubmissionGatePlan
);

const { buildActionPanelPromptPlan } = moduleShim.exports;

const disconnectedPlan = buildActionPanelPromptPlan({
  connectionStatus: "disconnected",
  playerId: "P1"
});

assert.equal(disconnectedPlan.canAct, false);
assert.equal(disconnectedPlan.promptTitle, "当前行动");
assert.equal(disconnectedPlan.statusLabel, "连接未就绪");
assert.ok(disconnectedPlan.rows.some((row) => row.text.includes("连接状态：已断开")));
assert.equal(disconnectedPlan.genericPrompt, undefined);
assert.equal(disconnectedPlan.inspection, undefined);

const knownSimplePlan = buildActionPanelPromptPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["END_TURN"],
    candidates: [],
    playerId: "P1",
    reason: "MAIN_ACTION",
    view: {
      message: "选择一个主行动。",
      title: "主行动",
      type: "MAIN_ACTION"
    }
  }
});

assert.equal(knownSimplePlan.canAct, true);
assert.equal(knownSimplePlan.statusTone, "good");
assert.equal(knownSimplePlan.genericPrompt, undefined);
assert.equal(knownSimplePlan.inspection?.sourceLabel, "前端公开 prompt 汇总");
assert.equal(knownSimplePlan.inspection?.summaryRows.find((row) => row.key === "candidate")?.value, "0 可提交 / 0 阻断");

const stalePromptPlan = buildActionPanelPromptPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["END_TURN"],
    candidates: [{ action: "END_TURN", enabled: true, label: "结束回合", reason: "可结束" }],
    playerId: "P1",
    reason: "MAIN_ACTION",
    snapshotTick: 7,
    view: {
      message: "选择一个主行动。",
      title: "主行动",
      type: "MAIN_ACTION"
    }
  },
  snapshot: { tick: 8 }
});

assert.equal(stalePromptPlan.canAct, false);
assert.equal(stalePromptPlan.statusLabel, "等待同步");
assert.ok(stalePromptPlan.rows.some((row) => row.key === "submission-gate" && row.text.includes("tick 7") && row.text.includes("tick 8")));

const complexPrompt = {
  actionable: true,
  actions: ["PAY_COST"],
  candidates: [
    {
      action: "PAY_COST",
      destinations: [{ id: "paid", label: "费用池" }],
      enabled: true,
      label: "支付费用",
      optionalCosts: [{ id: "boost", label: "额外费用" }],
      reason: "可支付",
      sources: [
        { id: "rune-1", label: "火符文", objectIds: ["rune-1"] },
        { id: "rune-2", label: "风符文", objectIds: ["rune-2"] },
        { id: "rune-3", label: "土符文", objectIds: ["rune-3"] },
        { id: "rune-4", label: "水符文", objectIds: ["rune-4"] },
        { id: "rune-5", label: "光符文", objectIds: ["rune-5"] }
      ]
    }
  ],
  contract: {
    candidateAction: "PAY_COST",
    hiddenMetadata: ["serverPaymentState", "privateChoiceGraph"],
    legalChoices: ["sources", "optionalCosts"],
    promptKind: "PAY_COST",
    requiredPayload: ["paymentId", "paymentChoiceIds"],
    validationErrors: [],
    visibleMetadata: ["phase", "window"]
  },
  playerId: "P1",
  inspection: {
    boundary: "服务端只公开当前行动窗口的类型、候选、命令契约和对象索引摘要；隐藏 metadata 不进入提示检查。",
    groups: [
      {
        key: "candidate",
        rows: [{ key: "candidate-0", label: "可提交", tone: "good", value: "PAY_COST / 来源 5 / 费用 1" }],
        title: "服务端候选"
      },
      {
        key: "safe-boundary",
        rows: [{ key: "frontend", label: "前端职责", tone: "neutral", value: "展示与提交，不重算规则" }],
        title: "信息边界"
      }
    ],
    source: "server-action-prompt",
    summaryRows: [
      { key: "kind", label: "提示类型", value: "PAY_COST" },
      { key: "candidate", label: "候选", value: "1 可提交 / 0 阻断" }
    ]
  },
  reason: "PAY_COST",
  view: {
    message: "",
    metadata: {
      phase: "MAIN",
      privateNote: "never show this raw value",
      requirementGraph: { a: 1, b: 2 },
      selectableIds: ["a", "b", "c"],
      status: "OPEN"
    },
    relatedStackItemId: "stack-1",
    title: "支付费用",
    type: "PAY_COST"
  }
};

const complexPlan = buildActionPanelPromptPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: complexPrompt
});

assert.equal(complexPlan.promptMessage, "PAY_COST");
assert.ok(complexPlan.rows.some((row) => row.text === "关联结算链：stack-1"));
assert.equal(complexPlan.genericPrompt?.statusLabel, "复杂窗口");
assert.equal(complexPlan.genericPrompt?.candidateRows.length, 1);
assert.equal(complexPlan.genericPrompt.candidateRows[0].previews[0].text, "来源：火符文、风符文、土符文、水符文 等 5 项");
assert.equal(complexPlan.genericPrompt.metadataRows.find((row) => row.key === "phase")?.value, "MAIN");
assert.equal(complexPlan.genericPrompt.metadataRows.find((row) => row.key === "privateNote")?.value, "文本");
assert.equal(complexPlan.genericPrompt.metadataRows.find((row) => row.key === "selectableIds")?.value, "3 项");
assert.equal(complexPlan.genericPrompt.metadataRows.find((row) => row.key === "requirementGraph")?.value, "2 项");
assert.equal(complexPlan.genericPrompt.contract?.lines.find((line) => line.key === "hiddenMetadata")?.value, "2 项由服务端保留，不向客户端展开。");
assert.equal(complexPlan.inspection?.sourceLabel, "服务端提示检查");
assert.equal(complexPlan.inspection?.summaryRows.find((row) => row.key === "candidate")?.value, "1 可提交 / 0 阻断");
assert.equal(complexPlan.inspection?.groups.find((group) => group.key === "safe-boundary")?.rows[0]?.value, "展示与提交，不重算规则");
assert.equal(JSON.stringify(complexPlan).includes("serverPaymentState"), false);
assert.equal(JSON.stringify(complexPlan).includes("never show this raw value"), false);

const spellDuelPlan = buildActionPanelPromptPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: true,
    actions: ["PLAY_CARD", "ACTIVATE_ABILITY", "PASS_FOCUS"],
    candidates: [
      { action: "PLAY_CARD", enabled: true, label: "打出法术", reason: "可响应" },
      { action: "ACTIVATE_ABILITY", enabled: false, label: "激活技能", reason: "资源不足" },
      { action: "PASS_FOCUS", enabled: true, label: "让过焦点", reason: "可让过" }
    ],
    playerId: "P1",
    reason: "SPELL_DUEL_FOCUS",
    view: {
      message: "处理法术对决焦点。",
      relatedBattlefieldId: "bf-1",
      relatedSpellDuelId: "spell-duel-1",
      responsibility: {
        actionableForPromptPlayer: true,
        isResponsiblePlayer: true,
        nextStep: "根据服务端候选处理法术对决。",
        promptPlayerId: "P1",
        promptType: "SPELL_DUEL_FOCUS",
        queueCounts: { stack: 1 },
        relatedObjectIds: ["spell-1"],
        responsiblePlayerId: "P1",
        state: "PLAYER_ACTION"
      },
      title: "法术对决",
      type: "SPELL_DUEL_FOCUS"
    }
  },
  snapshot: {
    stack: [{ stackItemId: "stack-1" }],
    tick: 10,
    timing: {
      focusPlayerId: "P1",
      spellDuel: {
        battlefieldObjectId: "bf-from-snapshot",
        focusPlayerId: "P1",
        isActive: true,
        passedFocusPlayerIds: ["P2"],
        spellDuelId: "spell-duel-from-snapshot",
        stackControllerIds: ["P1"],
        stackItemIds: ["stack-1"]
      }
    }
  }
});

assert.equal(spellDuelPlan.genericPrompt, undefined);
assert.equal(spellDuelPlan.spellDuel?.stateLabel, "轮到你处理焦点");
assert.equal(spellDuelPlan.spellDuel?.nextStep, "根据服务端候选处理法术对决。");
assert.equal(spellDuelPlan.spellDuel?.metrics.find((row) => row.key === "spell-duel-id")?.value, "spell-duel-1");
assert.equal(spellDuelPlan.spellDuel?.metrics.find((row) => row.key === "battlefield")?.value, "bf-1");
assert.equal(spellDuelPlan.spellDuel?.metrics.find((row) => row.key === "focus")?.mine, true);
assert.equal(spellDuelPlan.spellDuel?.metrics.find((row) => row.key === "stack")?.value, "1 项");
assert.equal(spellDuelPlan.spellDuel?.actionRows.find((row) => row.key === "responses")?.value, "打出法术");
assert.equal(spellDuelPlan.spellDuel?.actionRows.find((row) => row.key === "pass")?.value, "可让过");
assert.equal(spellDuelPlan.spellDuel?.actionRows.find((row) => row.key === "blocked")?.value, "1 项");

const stackPriorityPlan = buildActionPanelPromptPlan({
  connectionStatus: "connected",
  playerId: "P2",
  prompt: {
    actionable: true,
    actions: ["PLAY_CARD", "LEGEND_ACT", "PASS_PRIORITY"],
    candidates: [
      { action: "PLAY_CARD", enabled: true, label: "响应法术", reason: "可响应" },
      { action: "LEGEND_ACT", enabled: false, label: "传奇行动", reason: "时机不允许" },
      { action: "PASS_PRIORITY", enabled: true, label: "让过优先权", reason: "可让过" }
    ],
    playerId: "P2",
    reason: "STACK_PRIORITY",
    view: {
      message: "处理结算链响应。",
      relatedStackItemId: "stack-2",
      responsibility: {
        actionableForPromptPlayer: true,
        isResponsiblePlayer: true,
        nextStep: "响应顶部结算项目或让过优先权。",
        promptPlayerId: "P2",
        promptType: "STACK_PRIORITY",
        queueCounts: { stack: 2 },
        relatedObjectIds: ["spell-2"],
        responsiblePlayerId: "P2",
        state: "PLAYER_ACTION"
      },
      title: "优先行动",
      type: "STACK_PRIORITY"
    }
  },
  snapshot: {
    stack: [
      { cardNo: "OGN-001/298", effectKind: "SPELL", sourceObjectId: "spell-1", stackItemId: "stack-1" },
      { effectKind: "ABILITY", sourceObjectId: "unit-1", stackItemId: "stack-2", targetObjectIds: ["target-1", "target-2"] }
    ],
    tick: 12,
    timing: {
      passedPriorityPlayerIds: ["P1"],
      priorityPlayerId: "P2",
      turnWindow: {
        actingPlayerId: "P2"
      }
    }
  }
});

assert.equal(stackPriorityPlan.genericPrompt, undefined);
assert.equal(stackPriorityPlan.stackPriority?.stateLabel, "轮到你响应");
assert.equal(stackPriorityPlan.stackPriority?.nextStep, "响应顶部结算项目或让过优先权。");
assert.equal(stackPriorityPlan.stackPriority?.metrics.find((row) => row.key === "priority-player")?.mine, true);
assert.equal(stackPriorityPlan.stackPriority?.metrics.find((row) => row.key === "top-stack")?.value, "stack-2");
assert.equal(stackPriorityPlan.stackPriority?.metrics.find((row) => row.key === "source")?.value, "unit-1");
assert.equal(stackPriorityPlan.stackPriority?.metrics.find((row) => row.key === "effect")?.value, "ABILITY");
assert.equal(stackPriorityPlan.stackPriority?.metrics.find((row) => row.key === "targets")?.value, "2 项");
assert.equal(stackPriorityPlan.stackPriority?.metrics.find((row) => row.key === "passed")?.value, "P1");
assert.equal(stackPriorityPlan.stackPriority?.actionRows.find((row) => row.key === "responses")?.value, "响应法术");
assert.equal(stackPriorityPlan.stackPriority?.actionRows.find((row) => row.key === "pass")?.value, "可让过");
assert.equal(stackPriorityPlan.stackPriority?.actionRows.find((row) => row.key === "blocked")?.value, "1 项");

const unknownPlan = buildActionPanelPromptPlan({
  connectionStatus: "connected",
  playerId: "P1",
  prompt: {
    actionable: false,
    actions: [],
    candidates: [],
    playerId: "P2",
    reason: "future window",
    view: {
      message: "等待后端正式支持。",
      title: "新窗口",
      type: "FUTURE_RULE_WINDOW"
    }
  }
});

assert.equal(unknownPlan.genericPrompt?.statusLabel, "未知窗口");
assert.equal(unknownPlan.genericPrompt?.emptyCandidateLabel, "当前窗口没有服务端可提交选项。");

console.log("Action panel prompt plan check passed.");

function connectionStatusLabel(status) {
  return status === "connected" ? "已连接" : status === "disconnected" ? "已断开" : status;
}

function promptActionLabel(candidate) {
  return candidate.label || candidate.action;
}

function promptReasonLabel(reason, fallback = "服务端候选") {
  return reason || fallback;
}

function redactInternalText(value) {
  return String(value).replace(/serverPaymentState|privateChoiceGraph/g, "服务端字段");
}

function buildServerSubmissionGatePlan({ connectionStatus, prompt, snapshot }) {
  if (connectionStatus !== "connected") {
    return { canSubmit: false, reason: `连接状态：${connectionStatusLabel(connectionStatus)}，暂不提交行动。`, state: "disconnected", stateLabel: "连接未就绪" };
  }
  if (prompt?.snapshotTick == null) {
    return { canSubmit: true, reason: "服务端未要求特定快照 tick。", state: "connected", stateLabel: "可提交" };
  }
  if (!snapshot) {
    return { canSubmit: false, reason: `行动提示属于 tick ${prompt.snapshotTick}，但本地尚未收到服务端快照。`, state: "missing-snapshot", stateLabel: "等待快照" };
  }
  if (snapshot.tick !== prompt.snapshotTick) {
    return { canSubmit: false, reason: `行动提示属于 tick ${prompt.snapshotTick}，当前桌面快照是 tick ${snapshot.tick}。`, state: "stale-snapshot", stateLabel: "等待同步" };
  }
  return { canSubmit: true, reason: `行动提示和桌面快照同属 tick ${snapshot.tick}。`, state: "connected", stateLabel: "可提交" };
}
