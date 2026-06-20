import type { ActionPromptCandidateDto, ActionPromptChoiceDto, ActionPromptContractDto, ActionPromptDto, ConnectionStatus, SnapshotDto } from "../types/protocol";
import { connectionStatusLabel, promptActionLabel, promptReasonLabel } from "./formatters";
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

export type ActionPanelPromptPlan = {
  canAct: boolean;
  genericPrompt?: ActionPanelGenericPromptPlan;
  promptMessage: string;
  promptTitle: string;
  rows: ActionPanelPromptSummaryRow[];
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
    promptMessage,
    promptTitle,
    rows: promptSummaryRows(prompt, promptMessage, connectionStatus, gate),
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

  return complexPromptTypes.has(type) || !knownPromptTypes.has(type);
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
  "ASSIGN_COMBAT_DAMAGE",
  "SPELL_DUEL_ACTION"
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
