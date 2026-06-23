import type { ConnectionStatus } from "../types/protocol";
import { connectionStatusLabel, connectionStatusTone } from "./formatters";

export type RoomSetupPlayer = {
  deckSubmitted?: boolean;
  ready?: boolean;
  seat?: string;
};

export type RoomSetupQuickAction = {
  id: string;
  state?: string;
  title?: string;
};

export type RoomSetupGate = {
  label: string;
  nextStep: string;
  reason: string;
  tone: "bad" | "good" | "info" | "neutral" | "warn";
};

export type RoomSetupStep = {
  detail: string;
  id: "connection" | "deck" | "ready" | "seat" | "start";
  nextStep: string;
  stateLabel: string;
  title: string;
  tone: "bad" | "good" | "info" | "neutral" | "warn";
};

export type RoomSetupFlowPlan = {
  startGate: RoomSetupGate;
  steps: RoomSetupStep[];
  targetPlayerCount: number;
};

export function buildRoomSetupFlowPlan({
  connectionStatus,
  currentPlayer,
  lastCommandSubmissionState,
  players,
  quickActions,
  roomStatus,
  submissionGateReason,
  targetPlayerCount = 2
}: {
  connectionStatus: ConnectionStatus;
  currentPlayer?: RoomSetupPlayer;
  lastCommandSubmissionState?: string;
  players: RoomSetupPlayer[];
  quickActions: RoomSetupQuickAction[];
  roomStatus: string;
  submissionGateReason: string;
  targetPlayerCount?: number;
}): RoomSetupFlowPlan {
  const submittedCount = players.filter((player) => player.deckSubmitted).length;
  const readyCount = players.filter((player) => player.ready).length;
  const quickActionById = Object.fromEntries(quickActions.map((entry) => [entry.id, entry]));
  const submitDeckAction = quickActionById.submitDeck;
  const readyAction = quickActionById.ready;
  const connected = connectionStatus === "connected";
  const seated = Boolean(currentPlayer);
  const playerCount = players.length;
  const allSeated = playerCount >= targetPlayerCount;
  const allSubmitted = allSeated && submittedCount >= targetPlayerCount && players.every((player) => player.deckSubmitted);
  const allReady = allSubmitted && readyCount >= targetPlayerCount && players.every((player) => player.ready);
  const startGate = roomStartGate({
    allReady,
    allSeated,
    allSubmitted,
    connected,
    playerCount,
    readyCount,
    roomStatus,
    submittedCount,
    submissionGateReason,
    targetPlayerCount
  });

  return {
    startGate,
    steps: [
      {
        detail: connected ? "已连接，可接收房间快照与行动提示。" : "下一步：连接/重连并入座。",
        id: "connection",
        nextStep: connected ? "等待服务端房间快照和行动候选。" : "连接/重连并入座。",
        stateLabel: connectionStatusLabel(connectionStatus),
        title: "1. 服务端连接",
        tone: connectionStatusTone(connectionStatus)
      },
      {
        detail: seated ? `当前席位：${currentPlayer?.seat ?? "服务端未命名席位"}。` : "阻塞：当前玩家尚未出现在服务端快照中。",
        id: "seat",
        nextStep: allSeated ? "进入卡组提交确认。" : "等待另一名玩家入座或重新连接。",
        stateLabel: `${playerCount}/${targetPlayerCount} 人`,
        title: "2. 入座",
        tone: allSeated ? "good" : seated ? "info" : "warn"
      },
      {
        detail: currentPlayer?.deckSubmitted ? "当前玩家已提交卡组。" : `阻塞：${submitDeckAction?.title ?? "等待服务端提交构筑候选。"}`,
        id: "deck",
        nextStep: deckNextStep(currentPlayer?.deckSubmitted, submitDeckAction),
        stateLabel: `${submittedCount}/${targetPlayerCount} 已提交`,
        title: "3. 卡组提交",
        tone: allSubmitted ? "good" : submitDeckAction?.state === "ready" ? "info" : "warn"
      },
      {
        detail: currentPlayer?.ready ? "当前玩家已准备。" : `阻塞：${readyAction?.title ?? "等待服务端准备候选。"}`,
        id: "ready",
        nextStep: readyNextStep(currentPlayer?.ready, readyAction),
        stateLabel: `${readyCount}/${targetPlayerCount} 已准备`,
        title: "4. 准备",
        tone: allReady ? "good" : readyAction?.state === "ready" ? "info" : "warn"
      },
      {
        detail: `${startGate.tone === "good" || startGate.tone === "info" ? "状态：" : "阻塞："}${startGate.reason}`,
        id: "start",
        nextStep: lastCommandSubmissionState === "failed" ? "根据服务端拒绝原因调整后重试，或重新同步快照。" : startGate.nextStep,
        stateLabel: startGate.label,
        title: "5. 开局",
        tone: startGate.tone
      }
    ],
    targetPlayerCount
  };
}

function deckNextStep(deckSubmitted: boolean | undefined, action?: RoomSetupQuickAction): string {
  if (deckSubmitted) {
    return "等待全部玩家完成提交。";
  }

  if (action?.state === "ready") {
    return "点击导入构筑并等待服务端回执。";
  }

  return action?.state === "missing" ? "等待服务端提供 SUBMIT_DECK 候选。" : "按服务端提示解除提交阻塞。";
}

function readyNextStep(ready: boolean | undefined, action?: RoomSetupQuickAction): string {
  if (ready) {
    return "等待全部玩家准备并由服务端推进开局。";
  }

  if (action?.state === "ready") {
    return "点击准备并等待服务端回执。";
  }

  return action?.state === "missing" ? "等待服务端提供 READY 候选。" : "按服务端提示解除准备阻塞。";
}

function roomStartGate({
  allReady,
  allSeated,
  allSubmitted,
  connected,
  playerCount,
  readyCount,
  roomStatus,
  submittedCount,
  submissionGateReason,
  targetPlayerCount
}: {
  allReady: boolean;
  allSeated: boolean;
  allSubmitted: boolean;
  connected: boolean;
  playerCount: number;
  readyCount: number;
  roomStatus: string;
  submittedCount: number;
  submissionGateReason: string;
  targetPlayerCount: number;
}): RoomSetupGate {
  if (roomStatus === "IN_PROGRESS") {
    return {
      label: "已开局",
      nextStep: "进入对战桌面。",
      reason: "服务端房间状态已进入对局进行中。",
      tone: "good"
    };
  }

  if (roomStatus === "FINISHED") {
    return {
      label: "已结算",
      nextStep: "打开结果页查看服务端结算。",
      reason: "服务端房间状态已结束。",
      tone: "good"
    };
  }

  if (!connected) {
    return {
      label: "连接阻塞",
      nextStep: "连接/重连并入座。",
      reason: submissionGateReason,
      tone: "bad"
    };
  }

  if (!allSeated) {
    return {
      label: "等待入座",
      nextStep: "等待另一名玩家进入该房间。",
      reason: `服务端快照仅确认 ${playerCount}/${targetPlayerCount} 名玩家。`,
      tone: "warn"
    };
  }

  if (!allSubmitted) {
    return {
      label: "等待卡组",
      nextStep: "缺卡组的玩家提交构筑。",
      reason: `服务端快照仅确认 ${submittedCount}/${targetPlayerCount} 份卡组。`,
      tone: "warn"
    };
  }

  if (!allReady) {
    return {
      label: "等待准备",
      nextStep: "缺准备的玩家点击准备。",
      reason: `服务端快照仅确认 ${readyCount}/${targetPlayerCount} 名玩家准备。`,
      tone: "warn"
    };
  }

  return {
    label: "等待开局",
    nextStep: "等待服务端发布开局快照；可进入对战桌面观察。",
    reason: "入座、卡组与准备均已满足，当前仍未收到 IN_PROGRESS 状态。",
    tone: "info"
  };
}
