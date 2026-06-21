import type { ActionPromptCandidateDto } from "../types/protocol";
import type { CardDetailPlan } from "./cardDetailPlan";
import { buildSourceCandidateActionPlan, type SourceCandidateActionPlan } from "./sourceCandidateActionPlan";

export type WireCardDetailActionEntryPlan = {
  actionPlan: SourceCandidateActionPlan;
  candidate: ActionPromptCandidateDto;
  key: string;
  mode: "button" | "composer";
};

export type WireCardDetailActionState = "blocked" | "empty" | "ready" | "readonly";

export type WireCardDetailActionRouteState = "blocked" | "composer" | "direct" | "incomplete" | "readonly";

export type WireCardDetailActionSummaryRow = {
  key: string;
  label: string;
  value: string;
};

export type WireCardDetailActionRouteRow = {
  action: string;
  commandType: string;
  entryKey: string;
  fieldSummary: string;
  key: string;
  label: string;
  modeLabel: string;
  nextStepLabel: string;
  reasonLabel: string;
  state: WireCardDetailActionRouteState;
  stateLabel: string;
};

export type WireCardDetailActionPlan = {
  emptyLabel: string;
  entries: WireCardDetailActionEntryPlan[];
  routeRows: WireCardDetailActionRouteRow[];
  sourceObjectId?: string;
  state: WireCardDetailActionState;
  stateLabel: string;
  summaryRows: WireCardDetailActionSummaryRow[];
};

export type BuildWireCardDetailActionPlanOptions = {
  canSubmitCommands: boolean;
  detailPlan: CardDetailPlan;
  disabledByConnection: boolean;
};

export function buildWireCardDetailActionPlan({
  canSubmitCommands,
  detailPlan,
  disabledByConnection
}: BuildWireCardDetailActionPlanOptions): WireCardDetailActionPlan {
  const entries = detailPlan.actionCandidates.map((candidate, index) => {
    const actionPlan = buildSourceCandidateActionPlan({
      canSubmitCommands,
      candidate,
      disabledByConnection,
      sourceObjectId: detailPlan.sourceObjectId
    });

    return {
      actionPlan,
      candidate,
      key: `${candidate.action}-${candidate.label ?? "candidate"}-${index}`,
      mode: actionPlan.needsComposer && canSubmitCommands ? "composer" : "button"
    } satisfies WireCardDetailActionEntryPlan;
  });

  const state = stateFor(entries, canSubmitCommands, disabledByConnection);

  return {
    emptyLabel: detailPlan.actionEmptyLabel,
    entries,
    routeRows: routeRowsFor(entries, canSubmitCommands, disabledByConnection),
    sourceObjectId: detailPlan.sourceObjectId,
    state,
    stateLabel: stateLabelFor(state, disabledByConnection),
    summaryRows: summaryRowsFor(entries)
  };
}

function routeRowsFor(
  entries: WireCardDetailActionEntryPlan[],
  canSubmitCommands: boolean,
  disabledByConnection: boolean
): WireCardDetailActionRouteRow[] {
  return entries.map((entry) => {
    const state = routeStateFor(entry, canSubmitCommands, disabledByConnection);
    return {
      action: entry.candidate.action,
      commandType: entry.candidate.commandTemplate?.cmdType?.trim() || entry.candidate.action,
      entryKey: entry.key,
      fieldSummary: routeFieldSummary(entry.candidate),
      key: `route:${entry.key}`,
      label: entry.candidate.label?.trim() || entry.actionPlan.label,
      modeLabel: routeModeLabel(state),
      nextStepLabel: routeNextStepLabel(state, entry),
      reasonLabel: entry.actionPlan.title || entry.candidate.reason || "服务端候选",
      state,
      stateLabel: routeStateLabel(state)
    };
  });
}

function routeStateFor(
  entry: WireCardDetailActionEntryPlan,
  canSubmitCommands: boolean,
  disabledByConnection: boolean
): WireCardDetailActionRouteState {
  if (disabledByConnection || !canSubmitCommands) {
    return "readonly";
  }

  if (!entry.candidate.enabled) {
    return "blocked";
  }

  if (entry.mode === "composer") {
    return "composer";
  }

  if (entry.actionPlan.command) {
    return "direct";
  }

  return "incomplete";
}

function routeModeLabel(state: WireCardDetailActionRouteState): string {
  switch (state) {
    case "blocked":
      return "阻断";
    case "composer":
      return "组合";
    case "direct":
      return "直接";
    case "incomplete":
      return "不完整";
    case "readonly":
      return "只读";
  }
}

function routeStateLabel(state: WireCardDetailActionRouteState): string {
  switch (state) {
    case "blocked":
      return "服务端阻断";
    case "composer":
      return "需要组合";
    case "direct":
      return "可直接提交";
    case "incomplete":
      return "等待服务端字段";
    case "readonly":
      return "只读检查";
  }
}

function routeNextStepLabel(
  state: WireCardDetailActionRouteState,
  entry: WireCardDetailActionEntryPlan
): string {
  switch (state) {
    case "blocked":
      return entry.actionPlan.title || "等待服务端开放该候选。";
    case "composer":
      return "打开组合入口补齐服务端要求的选择。";
    case "direct":
      return "可直接提交服务端候选。";
    case "incomplete":
      return "等待服务端公开完整选择或命令模板。";
    case "readonly":
      return entry.actionPlan.title || "当前只可检查，不提交命令。";
  }
}

function routeFieldSummary(candidate: ActionPromptCandidateDto): string {
  const bindings = candidate.commandTemplate?.bindings ?? [];
  if (bindings.length === 0) {
    return "命令字段未公开";
  }

  const requiredCount = bindings.filter((binding) => binding.required).length;
  const serverCount = bindings.filter((binding) => binding.source === "requirementMetadata").length;
  const playerCount = bindings.length - serverCount;
  return `${bindings.length} 字段 / ${requiredCount} 必需 / ${playerCount} 玩家 / ${serverCount} 服务端`;
}

function summaryRowsFor(entries: WireCardDetailActionEntryPlan[]): WireCardDetailActionSummaryRow[] {
  const enabledCount = entries.filter((entry) => entry.candidate.enabled).length;
  const blockedCount = entries.length - enabledCount;
  const composerCount = entries.filter((entry) => entry.mode === "composer").length;
  const directCommandCount = entries.filter((entry) => Boolean(entry.actionPlan.command)).length;
  const commandTypes = uniqueStrings(entries.map((entry) =>
    entry.candidate.commandTemplate?.cmdType?.trim() || entry.candidate.action));
  const commandBindings = entries.flatMap((entry) => entry.candidate.commandTemplate?.bindings ?? []);
  const requiredFieldCount = commandBindings.filter((binding) => binding.required).length;
  const serverFieldCount = commandBindings.filter((binding) => binding.source === "requirementMetadata").length;
  const blockedReasons = uniqueStrings(entries
    .filter((entry) => !entry.candidate.enabled)
    .map((entry) => entry.candidate.reason));

  return [
    { key: "candidate", label: "候选", value: `${enabledCount} 可用 / ${blockedCount} 阻断` },
    { key: "route", label: "入口", value: `${composerCount} 组合 / ${directCommandCount} 直接` },
    { key: "command", label: "命令", value: commandTypes.length > 0 ? commandTypes.join(" / ") : "未公开" },
    {
      key: "field",
      label: "字段",
      value: commandBindings.length > 0
        ? `${commandBindings.length} 字段 / ${requiredFieldCount} 必需 / ${serverFieldCount} 服务端`
        : "未公开"
    },
    {
      key: "blocked",
      label: "阻断",
      value: blockedReasons.length > 0 ? compactList(blockedReasons, 2) : "无"
    }
  ];
}

function stateFor(
  entries: WireCardDetailActionEntryPlan[],
  canSubmitCommands: boolean,
  disabledByConnection: boolean
): WireCardDetailActionState {
  if (entries.length === 0) {
    return "empty";
  }

  if (!canSubmitCommands || disabledByConnection) {
    return "readonly";
  }

  if (!entries.some((entry) => entry.candidate.enabled)) {
    return "blocked";
  }

  return "ready";
}

function stateLabelFor(state: WireCardDetailActionState, disabledByConnection: boolean): string {
  if (state === "blocked") {
    return "服务端候选暂不可提交";
  }

  if (state === "empty") {
    return "无服务端候选";
  }

  if (disabledByConnection) {
    return "连接恢复前仅可查看";
  }

  if (state === "readonly") {
    return "当前视图仅可查看";
  }

  return "服务端候选可提交";
}

function uniqueStrings(values: string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))];
}

function compactList(values: string[], limit: number): string {
  const visible = values.slice(0, limit);
  return values.length > limit
    ? `${visible.join(" / ")} +${values.length - limit}`
    : visible.join(" / ");
}
