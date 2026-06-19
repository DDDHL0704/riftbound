import type { ActionPromptCandidateDto } from "../types/protocol";
import type { CardDetailPlan } from "./cardDetailPlan";
import { buildSourceCandidateActionPlan, type SourceCandidateActionPlan } from "./sourceCandidateActionPlan";

export type WireCardDetailActionEntryPlan = {
  actionPlan: SourceCandidateActionPlan;
  candidate: ActionPromptCandidateDto;
  key: string;
  mode: "button" | "composer";
};

export type WireCardDetailActionState = "empty" | "ready" | "readonly";

export type WireCardDetailActionPlan = {
  emptyLabel: string;
  entries: WireCardDetailActionEntryPlan[];
  sourceObjectId?: string;
  state: WireCardDetailActionState;
  stateLabel: string;
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
    stateLabel: stateLabelFor(state, disabledByConnection)
  };
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

  return "ready";
}

function stateLabelFor(state: WireCardDetailActionState, disabledByConnection: boolean): string {
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
