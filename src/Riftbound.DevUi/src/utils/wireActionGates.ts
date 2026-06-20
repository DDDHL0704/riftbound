import type { ActionPromptDto } from "../types/protocol";
import type { ServerSubmissionGatePlan } from "./serverSubmissionGatePlan";

export type WireActionSubmissionGatePlan = {
  canSubmit: boolean;
  reason: string;
  state: ServerSubmissionGatePlan["state"];
  stateLabel: string;
};

export type WireActionWindowGateState = "not-actionable" | "ready" | "waiting-prompt" | "wrong-player";

export type WireActionWindowGatePlan = {
  canAct: boolean;
  reason: string;
  state: WireActionWindowGateState;
  stateLabel: string;
};

export function buildWireActionSubmissionGatePlan(
  submissionGate: ServerSubmissionGatePlan | undefined,
  fallbackCanSubmit = true
): WireActionSubmissionGatePlan {
  if (submissionGate) {
    return {
      canSubmit: submissionGate.canSubmit,
      reason: submissionGate.reason,
      state: submissionGate.state,
      stateLabel: submissionGate.stateLabel
    };
  }

  if (fallbackCanSubmit) {
    return {
      canSubmit: true,
      reason: "当前未提供额外提交门禁。",
      state: "connected",
      stateLabel: "可提交"
    };
  }

  return {
    canSubmit: false,
    reason: "行动入口未就绪，等待服务端窗口、连接或快照同步。",
    state: "disconnected",
    stateLabel: "入口未就绪"
  };
}

export function buildWireActionWindowGatePlan({
  playerId,
  prompt
}: {
  playerId: string;
  prompt?: ActionPromptDto;
}): WireActionWindowGatePlan {
  if (!prompt) {
    return {
      canAct: false,
      reason: "服务端尚未提供行动窗口。",
      state: "waiting-prompt",
      stateLabel: "等待窗口"
    };
  }

  if (prompt.playerId !== playerId) {
    return {
      canAct: false,
      reason: `当前行动窗口属于 ${prompt.playerId || "未知玩家"}，本地玩家 ${playerId} 只读观察。`,
      state: "wrong-player",
      stateLabel: "非当前玩家"
    };
  }

  if (!prompt.actionable) {
    return {
      canAct: false,
      reason: "服务端提示当前只读，不能提交行动。",
      state: "not-actionable",
      stateLabel: "只读窗口"
    };
  }

  return {
    canAct: true,
    reason: "当前玩家拥有服务端行动窗口。",
    state: "ready",
    stateLabel: "当前可行动"
  };
}
