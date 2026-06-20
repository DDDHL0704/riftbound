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

export type WireCardDetailActionSummaryRow = {
  key: string;
  label: string;
  value: string;
};

export type WireCardDetailActionPlan = {
  emptyLabel: string;
  entries: WireCardDetailActionEntryPlan[];
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
    sourceObjectId: detailPlan.sourceObjectId,
    state,
    stateLabel: stateLabelFor(state, disabledByConnection),
    summaryRows: summaryRowsFor(entries)
  };
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
