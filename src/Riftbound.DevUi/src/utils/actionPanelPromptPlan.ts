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

export type ActionPanelComplexPromptMetric = {
  key: string;
  label: string;
  mine?: boolean;
  value: string;
};

export type ActionPanelComplexPromptActionRow = {
  count: number;
  key: string;
  label: string;
  value: string;
};

export type ActionPanelComplexPromptPlan = {
  actionRows: ActionPanelComplexPromptActionRow[];
  heading: string;
  metrics: ActionPanelComplexPromptMetric[];
  nextStep: string;
  stateLabel: string;
};

export type ActionPanelPromptPlan = {
  canAct: boolean;
  complexPrompt?: ActionPanelComplexPromptPlan;
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
    complexPrompt: prompt && isComplexPromptType(prompt.view?.type) ? buildComplexPromptPlan({ playerId, prompt }) : undefined,
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

function buildComplexPromptPlan({
  playerId,
  prompt
}: {
  playerId: string;
  prompt: ActionPromptDto;
}): ActionPanelComplexPromptPlan {
  const type = prompt.view?.type?.trim() ?? "";
  const canAct = Boolean(prompt.actionable && prompt.playerId === playerId);
  const enabledCandidates = prompt.candidates?.filter((candidate) => candidate.enabled) ?? [];
  const blockedCandidates = prompt.candidates?.filter((candidate) => !candidate.enabled) ?? [];
  const primaryCandidate = primaryCandidateForPrompt(prompt);
  const metadata = mergedPromptMetadata(prompt, primaryCandidate);
  const heading = complexPromptHeading(type);
  const contract = prompt.contract;
  const requiredPayloadCount = contract?.requiredPayload?.length ?? 0;
  const legalChoicesCount = contract?.legalChoices?.length ?? 0;

  return {
    actionRows: [
      {
        count: enabledCandidates.length,
        key: "enabled",
        label: "可提交",
        value: enabledCandidates.length > 0
          ? enabledCandidates.slice(0, 3).map((candidate) => promptActionLabel(candidate)).join("、")
          : "无"
      },
      {
        count: blockedCandidates.length,
        key: "blocked",
        label: "阻断",
        value: blockedCandidates.length > 0 ? `${blockedCandidates.length} 项` : "无"
      },
      {
        count: requiredPayloadCount + legalChoicesCount,
        key: "contract",
        label: "契约",
        value: contract
          ? `${requiredPayloadCount} 字段 / ${legalChoicesCount} 选项`
          : "服务端未提供"
      }
    ],
    heading,
    metrics: [
      { key: "responsible", label: "负责玩家", mine: prompt.playerId === playerId, value: prompt.playerId || "服务端未提供" },
      { key: "candidate", label: "候选", value: `${enabledCandidates.length} 可提交 / ${blockedCandidates.length} 阻断` },
      ...complexPromptMetrics(type, prompt, metadata)
    ].slice(0, 8),
    nextStep: prompt.view?.responsibility?.nextStep
      || (canAct ? complexPromptNextStep(type) : `等待 ${prompt.playerId || "负责玩家"} 处理${heading}。`),
    stateLabel: canAct ? "待你处理" : prompt.playerId === playerId ? "等待服务端入口" : "等待负责玩家"
  };
}

function complexPromptMetrics(
  type: string,
  prompt: ActionPromptDto,
  metadata: Record<string, unknown>
): ActionPanelComplexPromptMetric[] {
  switch (type) {
    case "BATTLE_DECLARATION": {
      const declarationCandidate = primaryCandidateForPrompt(prompt);
      return [
        metric("attack-sources", "攻击来源", countLabel(countItems(metadata.sourceRequirements, declarationCandidate?.sources))),
        metric("attacker-bounds", "攻击数量", countBoundsLabel(metadata, "attackerCountMin", "attackerCountMax", "个")),
        metric("defender-bounds", "防守数量", countBoundsLabel(metadata, "defenderCountMin", "defenderCountMax", "个")),
        metric("battlefields", "战场候选", countLabel(countItems(metadata.battlefieldChoices, metadata.battlefields, declarationCandidate?.destinations))),
        metric("defenders", "防守候选", countLabel(countItems(metadata.targetChoicesByIndex, metadata.defenderChoices, declarationCandidate?.targets))),
        metric("optional-costs", "额外费用", countLabel(countItems(metadata.optionalCostChoices, declarationCandidate?.optionalCosts)))
      ];
    }
    case "PAY_COST":
      return [
        metric("payment-window", "窗口", firstStringFromMetadata(metadata, ["paymentWindow"])),
        metric("payment-id", "支付 ID", firstStringFromMetadata(metadata, ["paymentId"])),
        metric("payment-choices", "支付项", countLabel(countItems(metadata.paymentChoices, metadata.legalPaymentChoiceIds, metadata.paymentChoiceIds))),
        metric("resource-choices", "资源动作", countLabel(countItems(metadata.paymentResourceChoices)))
      ];
    case "ORDER_TRIGGERS":
      return [
        metric("trigger-count", "触发", countLabel(countItems(metadata.triggers, metadata.triggerChoices, metadata.orderedTriggerIds, metadata.triggerIds))),
        metric("constraints", "排序约束", countLabel(countItems(metadata.legalOrderingConstraints, metadata.orderingConstraints, metadata.constraints))),
        metric("trigger-event", "来源事件", firstStringFromMetadata(metadata, ["triggeredByEventKind"])),
        metric("related", "关联对象", countLabel(prompt.view?.responsibility?.relatedObjectIds?.length))
      ];
    case "HAND_CHOICE":
      return [
        metric("choice-window", "窗口", firstStringFromMetadata(metadata, ["choiceWindow"])),
        metric("choice-id", "选择 ID", firstStringFromMetadata(metadata, ["choiceId"])),
        metric("hand-choices", "手牌候选", countLabel(countItems(metadata.handChoices, metadata.legalObjectIds))),
        metric("bounds", "选择数量", selectionBoundsLabel(prompt, metadata))
      ];
    case "ASSIGN_COMBAT_DAMAGE":
      return [
        metric("battle", "战斗", firstStringFromMetadata(metadata, ["battleId"]) || prompt.view?.relatedBattleId),
        metric("battlefield", "战场", firstStringFromMetadata(metadata, ["battlefieldId", "battlefieldObjectId"]) || prompt.view?.relatedBattlefieldId),
        metric("assignment-choices", "分配候选", countLabel(countItems(metadata.assignmentChoices, metadata.legalTargets, metadata.legalTargetsBySource))),
        metric("damage-pool", "伤害池", visibleScalarLabel(firstValueFromMetadata(metadata, ["damagePool", "totalDamage", "assignableDamage"])))
      ];
    default:
      return [];
  }
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

  return !isSpellDuelPromptType(type) && !isStackPriorityPromptType(type) && !isComplexPromptType(type) && !knownPromptTypes.has(type);
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
  "BATTLE_DECLARATION",
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

function isStackPriorityPromptType(type: string | undefined): boolean {
  return type === "STACK_PRIORITY";
}

function isComplexPromptType(type: string | undefined): boolean {
  return Boolean(type && complexPromptTypes.has(type));
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

function primaryCandidateForPrompt(prompt: ActionPromptDto): ActionPromptCandidateDto | undefined {
  const action = primaryActionForPromptType(prompt.view?.type);
  return prompt.candidates?.find((candidate) => candidate.action === action)
    ?? prompt.candidates?.find((candidate) => candidate.enabled)
    ?? prompt.candidates?.[0];
}

function primaryActionForPromptType(type: string | undefined): string {
  switch (type) {
    case "ASSIGN_COMBAT_DAMAGE":
      return "ASSIGN_COMBAT_DAMAGE";
    case "BATTLE_DECLARATION":
      return "DECLARE_BATTLE";
    case "HAND_CHOICE":
      return "CHOOSE_HAND_CARDS";
    case "ORDER_TRIGGERS":
      return "ORDER_TRIGGERS";
    case "PAY_COST":
      return "PAY_COST";
    default:
      return "";
  }
}

function mergedPromptMetadata(
  prompt: ActionPromptDto,
  candidate: ActionPromptCandidateDto | undefined
): Record<string, unknown> {
  return {
    ...record(prompt.view?.metadata),
    ...record(candidate?.metadata)
  };
}

function complexPromptHeading(type: string): string {
  switch (type) {
    case "ASSIGN_COMBAT_DAMAGE":
      return "战斗伤害窗口";
    case "BATTLE_DECLARATION":
      return "声明战斗窗口";
    case "HAND_CHOICE":
      return "手牌选择窗口";
    case "ORDER_TRIGGERS":
      return "触发排序窗口";
    case "PAY_COST":
      return "支付费用窗口";
    default:
      return "复杂服务端窗口";
  }
}

function complexPromptNextStep(type: string): string {
  switch (type) {
    case "ASSIGN_COMBAT_DAMAGE":
      return "为服务端候选来源和目标填写伤害，提交后由服务端校验。";
    case "BATTLE_DECLARATION":
      return "选择服务端公开的攻击来源、战场、防守对象和额外费用，然后提交声明战斗。";
    case "HAND_CHOICE":
      return "选择服务端公开给你的手牌候选并提交；隐藏手牌不会在此展开。";
    case "ORDER_TRIGGERS":
      return "排列服务端触发候选的结算顺序，然后提交给服务端校验。";
    case "PAY_COST":
      return "选择服务端给出的支付项或资源动作，然后提交支付。";
    default:
      return "根据服务端候选完成当前窗口。";
  }
}

function metric(
  key: string,
  label: string,
  value: string | number | null | undefined
): ActionPanelComplexPromptMetric {
  return {
    key,
    label,
    value: value == null || value === "" ? "服务端未提供" : String(value)
  };
}

function countLabel(count: number | undefined): string | undefined {
  return count == null ? undefined : `${count} 项`;
}

function countItems(...values: unknown[]): number | undefined {
  for (const value of values) {
    const count = valueCount(value);
    if (count != null) {
      return count;
    }
  }

  return undefined;
}

function valueCount(value: unknown): number | undefined {
  if (Array.isArray(value)) {
    return value.length;
  }
  if (typeof value === "number" && Number.isFinite(value)) {
    return Math.max(0, Math.floor(value));
  }
  if (typeof value === "string" && value.trim().length > 0) {
    return 1;
  }
  if (value && typeof value === "object") {
    return Object.keys(value).length;
  }

  return undefined;
}

function firstStringFromMetadata(metadata: Record<string, unknown>, keys: string[]): string | undefined {
  for (const key of keys) {
    const value = stringValue(metadata[key]);
    if (value) {
      return redactInternalText(value);
    }
  }

  return undefined;
}

function firstValueFromMetadata(metadata: Record<string, unknown>, keys: string[]): unknown {
  for (const key of keys) {
    if (metadata[key] != null) {
      return metadata[key];
    }
  }

  return undefined;
}

function visibleScalarLabel(value: unknown): string | undefined {
  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }
  if (typeof value === "string") {
    return redactInternalText(value);
  }
  const count = valueCount(value);
  return count == null ? undefined : `${count} 项`;
}

function selectionBoundsLabel(prompt: ActionPromptDto, metadata: Record<string, unknown>): string {
  const required = numberValue(metadata.requiredCount) ?? prompt.view?.minSelection ?? undefined;
  const max = numberValue(metadata.maxCount) ?? prompt.view?.maxSelection ?? required;
  if (required == null && max == null) {
    return "服务端未提供";
  }
  if (required === max) {
    return `${required} 张`;
  }
  return `${required ?? 0}-${max ?? "不限"} 张`;
}

function countBoundsLabel(
  metadata: Record<string, unknown>,
  minKey: string,
  maxKey: string,
  unit: string
): string | undefined {
  const min = numberValue(metadata[minKey]);
  const max = numberValue(metadata[maxKey]) ?? min;
  if (min == null && max == null) {
    return undefined;
  }
  if (min === max) {
    return `${min} ${unit}`;
  }
  return `${min ?? 0}-${max ?? "不限"} ${unit}`;
}

function numberValue(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function summarizeIds(ids: string[]): string {
  const safeIds = ids.filter((id) => typeof id === "string" && id.trim().length > 0);
  if (safeIds.length === 0) {
    return "无";
  }

  return `${safeIds.slice(0, 2).join("、")}${safeIds.length > 2 ? ` 等 ${safeIds.length} 项` : ""}`;
}
