import { ActionPromptDto, ConnectionStatus } from "../types/protocol";
import { actionLabel } from "./formatters";

export type MatchGuidanceTurnState = "offline" | "yours" | "opponent" | "over";

export type MatchGuidanceTone = "good" | "warn" | "neutral" | "bad";

export type MatchGuidancePlan = {
  turnState: MatchGuidanceTurnState;
  headline: string;
  detail: string;
  youCanLabels: string[];
  tone: MatchGuidanceTone;
};

// Actions that are always present but are not a useful "you can do this now" suggestion.
const HIDDEN_ACTIONS = new Set(["WAIT", "SURRENDER"]);

// One-click actions surfaced as primary buttons in the banner, so they are not repeated as chips.
const PRIMARY_BUTTON_ACTIONS = new Set(["END_TURN", "PASS", "PASS_PRIORITY", "PASS_FOCUS"]);

export function buildMatchGuidancePlan({
  connectionStatus,
  prompt,
  winnerPlayerId,
  playerId
}: {
  connectionStatus: ConnectionStatus;
  prompt?: ActionPromptDto;
  winnerPlayerId?: string | null;
  playerId: string;
}): MatchGuidancePlan {
  const online = connectionStatus === "connected" || connectionStatus === "resyncing";
  if (!online) {
    return {
      turnState: "offline",
      headline: connectionStatus === "reconnecting" ? "正在重新连接…" : "未连接服务端",
      detail: "连接服务端后即可看到你的行动与对局进展。",
      youCanLabels: [],
      tone: "neutral"
    };
  }

  if (winnerPlayerId && winnerPlayerId.trim()) {
    const youWon = winnerPlayerId === playerId;
    return {
      turnState: "over",
      headline: youWon ? "对局结束 · 你赢了" : "对局结束 · 你输了",
      detail: youWon ? "恭喜获胜！可返回大厅再来一局。" : "本局结束，可返回大厅再来一局。",
      youCanLabels: [],
      tone: youWon ? "good" : "bad"
    };
  }

  const yours = Boolean(prompt?.actionable && prompt.playerId === playerId);
  if (yours && prompt) {
    return {
      turnState: "yours",
      headline: "轮到你了",
      detail: prompt.reason?.trim() || "请从下方的服务端候选中选择你的行动。",
      youCanLabels: dedupe(
        prompt.actions
          .filter((action) => !HIDDEN_ACTIONS.has(action) && !PRIMARY_BUTTON_ACTIONS.has(action))
          .map(actionLabel)
      ),
      tone: "good"
    };
  }

  return {
    turnState: "opponent",
    headline: "等待对手行动",
    detail: prompt?.reason?.trim() || "对手正在行动，请稍候。",
    youCanLabels: [],
    tone: "warn"
  };
}

function dedupe(values: string[]): string[] {
  return Array.from(new Set(values));
}
