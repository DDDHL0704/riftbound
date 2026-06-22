import type { ActionPromptCandidateDto, ActionPromptChoiceDto, ActionPromptContractDto, ActionPromptDto, ConnectionStatus, SnapshotDto } from "../types/protocol";
import { connectionStatusLabel, promptActionLabel, promptReasonLabel } from "./formatters";
import { buildPromptInspectionPlan, type PromptInspectionPlan } from "./promptInspectionPlan";
import { redactInternalText } from "./redaction";
import { buildServerSubmissionGatePlan, type ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";

export type ActionPanelPromptSummaryRow = {
  key: string;
  text: string;
};

export type ActionPanelPromptChoicePreviewPlan = {
  key: string;
  text: string;
};

export type ActionPanelPromptCandidatePlan = {
  key: string;
  label: string;
  reason: string;
  previews: ActionPanelPromptChoicePreviewPlan[];
};

export type ActionPanelPromptMetadataRow = {
  key: string;
  label: string;
  value: string;
};

export type ActionPanelPromptContractLine = {
  key: string;
  label: string;
  value: string;
};

export type ActionPanelPromptContractPlan = {
  heading: string;
  lines: ActionPanelPromptContractLine[];
};

export type ActionPanelGenericPromptPlan = {
  candidateRows: ActionPanelPromptCandidatePlan[];
  contract?: ActionPanelPromptContractPlan;
  emptyCandidateLabel?: string;
  metadataRows: ActionPanelPromptMetadataRow[];
  note: string;
  statusLabel: string;
};

export type ActionPanelSpellDuelMetric = {
  key: string;
  label: string;
  mine?: boolean;
  value: string;
};

export type ActionPanelSpellDuelActionRow = {
  count: number;
  key: string;
  label: string;
  value: string;
};

export type ActionPanelSpellDuelPlan = {
  actionRows: ActionPanelSpellDuelActionRow[];
  metrics: ActionPanelSpellDuelMetric[];
  nextStep: string;
  stateLabel: string;
};

export type ActionPanelStackPriorityMetric = {
  key: string;
  label: string;
  mine?: boolean;
  value: string;
};

export type ActionPanelStackPriorityActionRow = {
  count: number;
  key: string;
  label: string;
  value: string;
};

export type ActionPanelStackPriorityPlan = {
  actionRows: ActionPanelStackPriorityActionRow[];
  metrics: ActionPanelStackPriorityMetric[];
  nextStep: string;
  stateLabel: string;
};

export type ActionPanelPromptPlan = {
  canAct: boolean;
  genericPrompt?: ActionPanelGenericPromptPlan;
  inspection?: PromptInspectionPlan;
  promptMessage: string;
  promptTitle: string;
  rows: ActionPanelPromptSummaryRow[];
  spellDuel?: ActionPanelSpellDuelPlan;
  stackPriority?: ActionPanelStackPriorityPlan;
  statusLabel: string;
  statusTone: "good" | "neutral";
};

type BuildActionPanelPromptPlanOptions = {
  connectionStatus: ConnectionStatus;
  playerId: string;
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  submissionGate?: ServerSubmissionGatePlan;
};

export function buildActionPanelPromptPlan({
  connectionStatus,
  playerId,
  prompt,
  snapshot,
  submissionGate
}: BuildActionPanelPromptPlanOptions): ActionPanelPromptPlan {
  const gate = submissionGate ?? buildServerSubmissionGatePlan({ connectionStatus, prompt, snapshot });
  const canAct = Boolean(gate.canSubmit && prompt?.actionable && prompt.playerId === playerId);
  const promptView = prompt?.view;
  const promptTitle = promptView?.title?.trim() || "当前行动";
  const promptMessage = promptView?.message?.trim()
    || (prompt ? promptReasonLabel(prompt.reason, "服务端行动提示") : "尚未收到行动提示");

  return {
    canAct,
    genericPrompt: prompt && shouldShowGenericPromptDetails(prompt) ? buildGenericPromptPlan(prompt) : undefined,
    inspection: prompt ? buildPromptInspectionPlan({ prompt }) : undefined,
    promptMessage,
    promptTitle,
    rows: promptSummaryRows(prompt, promptMessage, connectionStatus, gate),
    spellDuel: prompt && isSpellDuelPromptType(prompt.view?.type) ? buildSpellDuelPlan({ playerId, prompt, snapshot }) : undefined,
    stackPriority: prompt?.view?.type === "STACK_PRIORITY" ? buildStackPriorityPlan({ playerId, prompt, snapshot }) : undefined,
    statusLabel: canAct ? "轮到你操作" : gate.canSubmit ? "等待服务端或对手" : gate.stateLabel,
    statusTone: canAct ? "good" : "neutral"
  };
}

function promptSummaryRows(
  prompt: ActionPromptDto | undefined,
  promptMessage: string,
  connectionStatus: ConnectionStatus,
  submissionGate: ServerSubmissionGatePlan
): ActionPanelPromptSummaryRow[] {
  const rows: ActionPanelPromptSummaryRow[] = [
    { key: "prompt-status", text: `提示状态：${prompt ? "已收到" : "无"}` }
  ];
  const promptView = prompt?.view;

  if (promptView?.type) {
    rows.push({ key: "prompt-type", text: `类型：${promptView.type}` });
  }

  rows.push({ key: "prompt-message", text: `${promptView ? "说明" : "原因"}：${promptMessage}` });

  if (promptView?.relatedBattlefieldId) {
    rows.push({ key: "battlefield", text: `关联战场：${promptView.relatedBattlefieldId}` });
  }
  if (promptView?.relatedBattleId) {
    rows.push({ key: "battle", text: `关联战斗：${promptView.relatedBattleId}` });
  }
  if (promptView?.relatedSpellDuelId) {
    rows.push({ key: "spell-duel", text: `关联法术对决：${promptView.relatedSpellDuelId}` });
  }
  if (promptView?.relatedStackItemId) {
    rows.push({ key: "stack-item", text: `关联结算链：${promptView.relatedStackItemId}` });
  }
  if (connectionStatus !== "connected") {
    rows.push({
      key: "connection",
      text: `连接状态：${connectionStatusLabel(connectionStatus)}，行动入口已暂停。`
    });
  }
  if (connectionStatus === "connected" && !submissionGate.canSubmit) {
    rows.push({
      key: "submission-gate",
      text: `提交门禁：${submissionGate.reason}`
    });
  }

  return rows;
}

function buildGenericPromptPlan(prompt: ActionPromptDto): ActionPanelGenericPromptPlan {
  const metadataRows = Object.entries(prompt.view?.metadata ?? {})
    .filter(([, value]) => value != null)
    .slice(0, 6)
    .map(([key, value]) => ({
      key,
      label: redactInternalText(key),
      value: safePromptValue(key, value)
    }));
  const candidateRows = (prompt.candidates ?? [])
    .slice(0, 6)
    .map((candidate, index) => genericCandidatePlan(candidate, index));
  const type = prompt.view?.type?.trim() ?? "";

  return {
    candidateRows,
    contract: prompt.contract ? contractPlan(prompt.contract) : undefined,
    emptyCandidateLabel: candidateRows.length === 0 ? "当前窗口没有服务端可提交选项。" : undefined,
    metadataRows,
    note: "该窗口需要服务端正式交互字段支持；当前只展示安全候选摘要，不在前端计算或模拟规则结果。",
    statusLabel: knownPromptTypes.has(type) ? "复杂窗口" : "未知窗口"
  };
}

function buildSpellDuelPlan({
  playerId,
  prompt,
  snapshot
}: {
  playerId: string;
  prompt: ActionPromptDto;
  snapshot?: SnapshotDto;
}): ActionPanelSpellDuelPlan {
  const timing = record(snapshot?.timing);
  const spellDuel = record(timing.spellDuel);
  const focusPlayerId = firstNonEmpty(
    stringValue(spellDuel.focusPlayerId),
    stringValue(timing.focusPlayerId),
    prompt.view?.responsibility?.responsiblePlayerId,
    prompt.playerId
  );
  const stackItemIds = array<string>(spellDuel.stackItemIds);
  const stackControllerIds = array<string>(spellDuel.stackControllerIds);
  const passedFocusPlayerIds = array<string>(spellDuel.passedFocusPlayerIds);
  const responseActions = prompt.candidates?.filter((candidate) => candidate.enabled && isSpellDuelResponseAction(candidate.action)) ?? [];
  const passFocus = prompt.candidates?.filter((candidate) => candidate.action === "PASS_FOCUS") ?? [];
  const blockedActions = prompt.candidates?.filter((candidate) => !candidate.enabled && candidate.action !== "PASS_FOCUS") ?? [];
  const canAct = Boolean(prompt.actionable && prompt.playerId === playerId);

  return {
    actionRows: [
      {
        count: responseActions.length,
        key: "responses",
        label: "可响应",
        value: responseActions.length > 0
          ? responseActions.slice(0, 3).map((candidate) => promptActionLabel(candidate)).join("、")
          : "无"
      },
      {
        count: passFocus.filter((candidate) => candidate.enabled).length,
        key: "pass",
        label: "让过焦点",
        value: passFocus.length > 0
          ? passFocus.map((candidate) => candidate.enabled ? "可让过" : promptReasonLabel(candidate.reason, "暂不可让过")).join("、")
          : "服务端未提供"
      },
      {
        count: blockedActions.length,
        key: "blocked",
        label: "阻断候选",
        value: blockedActions.length > 0
          ? `${blockedActions.length} 项`
          : "无"
      }
    ],
    metrics: [
      { key: "spell-duel-id", label: "法术对决", value: firstNonEmpty(prompt.view?.relatedSpellDuelId, stringValue(spellDuel.spellDuelId), "服务端未提供") },
      { key: "battlefield", label: "战场", value: firstNonEmpty(prompt.view?.relatedBattlefieldId, stringValue(spellDuel.battlefieldObjectId), "服务端未提供") },
      { key: "focus", label: "焦点玩家", mine: focusPlayerId === playerId, value: focusPlayerId || "服务端未提供" },
      { key: "stack", label: "对决结算链", value: stackItemIds.length > 0 ? `${stackItemIds.length} 项` : `${snapshot?.stack?.length ?? 0} 项` },
      { key: "controllers", label: "结算控制者", value: summarizeIds(stackControllerIds) },
      { key: "passed", label: "已让过", value: passedFocusPlayerIds.length > 0 ? summarizeIds(passedFocusPlayerIds) : "无" }
    ],
    nextStep: prompt.view?.responsibility?.nextStep
      || (canAct ? "选择一个服务端响应候选，或让过当前法术对决焦点。" : `等待 ${focusPlayerId || prompt.playerId} 处理法术对决焦点。`),
    stateLabel: canAct ? "轮到你处理焦点" : focusPlayerId === playerId ? "等待服务端入口" : "等待焦点玩家"
  };
}

function buildStackPriorityPlan({
  playerId,
  prompt,
  snapshot
}: {
  playerId: string;
  prompt: ActionPromptDto;
  snapshot?: SnapshotDto;
}): ActionPanelStackPriorityPlan {
  const timing = record(snapshot?.timing);
  const turnWindow = record(timing.turnWindow);
  const priorityPlayerId = firstNonEmpty(
    stringValue(turnWindow.actingPlayerId),
    stringValue(timing.priorityPlayerId),
    prompt.view?.responsibility?.responsiblePlayerId,
    prompt.playerId
  );
  const stackItems = array<Record<string, unknown>>(snapshot?.stack);
  const topStackItem = record(stackItems[stackItems.length - 1]);
  const passedPriorityPlayerIds = array<string>(timing.passedPriorityPlayerIds);
  const responseActions = prompt.candidates?.filter((candidate) => candidate.enabled && isStackResponseAction(candidate.action)) ?? [];
  const passPriority = prompt.candidates?.filter((candidate) => candidate.action === "PASS_PRIORITY") ?? [];
  const blockedActions = prompt.candidates?.filter((candidate) => !candidate.enabled && candidate.action !== "PASS_PRIORITY") ?? [];
  const canAct = Boolean(prompt.actionable && prompt.playerId === playerId);
  const topStackItemId = firstNonEmpty(
    prompt.view?.relatedStackItemId,
    stringValue(topStackItem.stackItemId),
    stringValue(topStackItem.id),
    "服务端未提供"
  );
  const topSource = firstNonEmpty(
    stringValue(topStackItem.cardNo),
    stringValue(topStackItem.sourceCardNo),
    stringValue(topStackItem.sourceObjectId),
    "服务端项目"
  );
  const topEffect = firstNonEmpty(
    stringValue(topStackItem.effectKind),
    stringValue(topStackItem.kind),
    "服务端效果"
  );
  const targetCount = array(topStackItem.targetObjectIds).length;

  return {
    actionRows: [
      {
        count: responseActions.length,
        key: "responses",
        label: "可响应",
        value: responseActions.length > 0
          ? responseActions.slice(0, 3).map((candidate) => promptActionLabel(candidate)).join("、")
          : "无"
      },
      {
        count: passPriority.filter((candidate) => candidate.enabled).length,
        key: "pass",
        label: "让过优先权",
        value: passPriority.length > 0
          ? passPriority.map((candidate) => candidate.enabled ? "可让过" : promptReasonLabel(candidate.reason, "暂不可让过")).join("、")
          : "服务端未提供"
      },
      {
        count: blockedActions.length,
        key: "blocked",
        label: "阻断候选",
        value: blockedActions.length > 0 ? `${blockedActions.length} 项` : "无"
      }
    ],
    metrics: [
      { key: "priority-player", label: "优先权玩家", mine: priorityPlayerId === playerId, value: priorityPlayerId || "服务端未提供" },
      { key: "top-stack", label: "顶部项目", value: topStackItemId },
      { key: "source", label: "来源", value: topSource },
      { key: "effect", label: "效果", value: topEffect },
      { key: "targets", label: "目标", value: targetCount > 0 ? `${targetCount} 项` : "无" },
      { key: "stack", label: "结算链", value: `${stackItems.length} 项` },
      { key: "passed", label: "已让过", value: passedPriorityPlayerIds.length > 0 ? summarizeIds(passedPriorityPlayerIds) : "无" }
    ],
    nextStep: prompt.view?.responsibility?.nextStep
      || (canAct ? "选择一个服务端响应候选，或让过当前优先权。" : `等待 ${priorityPlayerId || prompt.playerId} 处理优先权。`),
    stateLabel: canAct ? "轮到你响应" : priorityPlayerId === playerId ? "等待服务端入口" : "等待优先权玩家"
  };
}

function genericCandidatePlan(
  candidate: ActionPromptCandidateDto,
  index: number
): ActionPanelPromptCandidatePlan {
  return {
    key: `${candidate.action}-${candidate.label}-${index}`,
    label: promptActionLabel(candidate),
    previews: [
      choicePreviewPlan("sources", "来源", candidate.sources),
      choicePreviewPlan("targets", "目标", candidate.targets),
      choicePreviewPlan("destinations", "位置", candidate.destinations),
      choicePreviewPlan("modes", "模式", candidate.modes),
      choicePreviewPlan("optionalCosts", "费用", candidate.optionalCosts)
    ].filter((preview): preview is ActionPanelPromptChoicePreviewPlan => preview != null),
    reason: candidate.enabled ? promptReasonLabel(candidate.reason, "可提交") : promptReasonLabel(candidate.reason, "暂不可提交")
  };
}

function choicePreviewPlan(
  key: string,
  title: string,
  choices?: ActionPromptChoiceDto[] | null
): ActionPanelPromptChoicePreviewPlan | undefined {
  if (!choices?.length) {
    return undefined;
  }

  return {
    key,
    text: `${title}：${choices.slice(0, 4).map(choiceLabel).join("、")}${choices.length > 4 ? ` 等 ${choices.length} 项` : ""}`
  };
}

function contractPlan(contract: ActionPromptContractDto): ActionPanelPromptContractPlan {
  return {
    heading: `${contract.promptKind} / ${contract.candidateAction}`,
    lines: [
      contractLine("requiredPayload", "提交字段", contract.requiredPayload),
      contractLine("legalChoices", "合法选项", contract.legalChoices),
      contractLine("visibleMetadata", "公开数据", contract.visibleMetadata),
      hiddenContractLine(contract.hiddenMetadata),
      contractLine("validationErrors", "服务端校验", contract.validationErrors)
    ].filter((line): line is ActionPanelPromptContractLine => line != null)
  };
}

function contractLine(
  key: string,
  label: string,
  values?: string[] | null
): ActionPanelPromptContractLine | undefined {
  if (!values?.length) {
    return undefined;
  }

  return {
    key,
    label,
    value: `${values.slice(0, 5).map(redactInternalText).join(" / ")}${values.length > 5 ? ` 等 ${values.length} 项` : ""}`
  };
}

function hiddenContractLine(values?: string[] | null): ActionPanelPromptContractLine | undefined {
  if (!values?.length) {
    return undefined;
  }

  return {
    key: "hiddenMetadata",
    label: "隐藏数据",
    value: `${values.length} 项由服务端保留，不向客户端展开。`
  };
}

function shouldShowGenericPromptDetails(prompt: ActionPromptDto): boolean {
  const type = prompt.view?.type?.trim();
  if (!type) {
    return false;
  }

  return !isSpellDuelPromptType(type) && (complexPromptTypes.has(type) || !knownPromptTypes.has(type));
}

function choiceLabel(choice: ActionPromptChoiceDto): string {
  return redactInternalText(choice.label || choice.id || "服务端选项");
}

function safePromptValue(key: string, value: unknown): string {
  if (typeof value === "string") {
    return safeStringMetadataKeys.has(key) ? redactInternalText(value) || "文本" : "文本";
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  if (Array.isArray(value)) {
    return `${value.length} 项`;
  }

  if (value && typeof value === "object") {
    return `${Object.keys(value).length} 项`;
  }

  return "无";
}

const knownPromptTypes = new Set<string>([
  "ROOM_SETUP",
  "MULLIGAN",
  "MAIN_ACTION",
  "STACK_PRIORITY",
  "SPELL_DUEL_FOCUS",
  "SPELL_DUEL_ACTION",
  "BATTLE_DECLARATION",
  "HAND_CHOICE",
  "ASSIGN_COMBAT_DAMAGE",
  "PAY_COST",
  "ORDER_TRIGGERS",
  "TASK_QUEUE",
  "WAIT",
  "MATCH_RESULT"
]);

const complexPromptTypes = new Set<string>([
  "PAY_COST",
  "ORDER_TRIGGERS",
  "HAND_CHOICE",
  "ASSIGN_COMBAT_DAMAGE"
]);

const safeStringMetadataKeys = new Set<string>([
  "action",
  "actionType",
  "kind",
  "phase",
  "promptType",
  "state",
  "status",
  "window"
]);

function isSpellDuelPromptType(type: string | undefined): boolean {
  return type === "SPELL_DUEL_FOCUS" || type === "SPELL_DUEL_ACTION";
}

function isSpellDuelResponseAction(action: string): boolean {
  return action === "PLAY_CARD"
    || action === "ACTIVATE_ABILITY"
    || action === "LEGEND_ACT"
    || action === "REVEAL_CARD"
    || action === "HIDE_CARD";
}

function isStackResponseAction(action: string): boolean {
  return action === "PLAY_CARD"
    || action === "ACTIVATE_ABILITY"
    || action === "LEGEND_ACT"
    || action === "REVEAL_CARD"
    || action === "HIDE_CARD";
}

function record(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

function array<T = unknown>(value: unknown): T[] {
  return Array.isArray(value) ? value as T[] : [];
}

function stringValue(value: unknown): string | undefined {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : undefined;
}

function firstNonEmpty(...values: Array<string | null | undefined>): string {
  return values.find((value): value is string => Boolean(value?.trim()))?.trim() ?? "";
}

function summarizeIds(ids: string[]): string {
  const safeIds = ids.filter((id) => typeof id === "string" && id.trim().length > 0);
  if (safeIds.length === 0) {
    return "无";
  }

  return `${safeIds.slice(0, 2).join("、")}${safeIds.length > 2 ? ` 等 ${safeIds.length} 项` : ""}`;
}
