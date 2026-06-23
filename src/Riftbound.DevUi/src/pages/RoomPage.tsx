import { Check, RefreshCw, Send, Swords } from "lucide-react";
import { useCallback, useMemo } from "react";
import { AppRoute } from "../app/router";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { useMatchController } from "../stores/useMatchController";
import type { CommandSubmissionFeedback } from "../stores/useMatchController";
import { useSettings } from "../stores/settingsStore";
import { candidateListLabel } from "../components/match/ActionPanel";
import { eventDescriptionLabel, eventKindLabel } from "../components/match/EventLog";
import { errorCodeLabel, errorMessageLabel } from "../utils/errors";
import { connectionStatusLabel, connectionStatusTone } from "../utils/formatters";
import { buildServerQuickActionPlan, quickActionCommandUiSource, type ServerQuickActionEntry } from "../utils/serverQuickActionPlan";
import { buildServerSubmissionGatePlan } from "../utils/serverSubmissionGatePlan";
import { asRecord, asString } from "../utils/collections";

export function RoomPage({ roomId, onNavigate }: { roomId: string; onNavigate: (route: AppRoute) => void }) {
  const { settings } = useSettings();
  const controller = useMatchController(settings.serverUrl, roomId, settings.playerId);
  const snapshot = controller.state.snapshot;
  const prompt = controller.state.prompt;
  const players = Object.values(snapshot?.players ?? {});
  const roomStatus = asString(asRecord(snapshot?.timing).roomStatus, "");
  const connected = controller.state.status === "connected";
  const canAct = Boolean(prompt?.actionable && prompt.playerId === settings.playerId);
  const submissionGate = useMemo(() => buildServerSubmissionGatePlan({
    connectionStatus: controller.state.status,
    prompt,
    snapshot
  }), [controller.state.status, prompt, snapshot]);
  const roomQuickActionPlan = useMemo(() => buildServerQuickActionPlan({
    canAct,
    connected,
    ids: ["submitDeck", "ready"],
    prompt,
    submissionGate,
    snapshot
  }), [canAct, connected, prompt, snapshot, submissionGate]);
  const runRoomQuickAction = useCallback((entry: ServerQuickActionEntry) => {
    if (entry.disabled) {
      return;
    }

    if (entry.command) {
      void controller.submitCommand(entry.command, {
        ...quickActionCommandUiSource(entry),
        label: `房间快捷：${entry.label}`,
        surface: "room"
      });
      return;
    }

    if (entry.directAction === "submitDeck") {
      void controller.submitStarterDeck({
        ...quickActionCommandUiSource(entry),
        label: `房间快捷：${entry.label}`,
        surface: "room"
      });
      return;
    }

    if (entry.directAction === "ready") {
      void controller.ready({
        ...quickActionCommandUiSource(entry),
        label: `房间快捷：${entry.label}`,
        surface: "room"
      });
    }
  }, [controller]);

  return (
    <div className="page-grid">
      <section className="page-header">
        <div>
          <span className="eyebrow">房间</span>
          <h1>{roomId}</h1>
          <p>入座、提交卡组、准备和开局均通过服务端实时连接确认。</p>
        </div>
        <StatusPill tone={connectionStatusTone(controller.state.status)}>
          {connectionStatusLabel(controller.state.status)}
        </StatusPill>
      </section>
      <section className="room-actions">
        <Button icon={<RefreshCw size={18} />} onClick={() => void controller.join()}>连接/重连并入座</Button>
        <RoomPromptButtons
          entries={roomQuickActionPlan.entries}
          onRunAction={runRoomQuickAction}
        />
        <Button icon={<Swords size={18} />} onClick={() => onNavigate({ name: "match", matchId: roomId })}>进入对战桌面</Button>
      </section>
      <RoomSetupChecklist
        connectionStatus={controller.state.status}
        currentPlayer={snapshot?.players[settings.playerId]}
        lastCommandSubmission={controller.state.lastCommandSubmission}
        playerCount={players.length}
        players={players}
        quickActions={roomQuickActionPlan.entries}
        roomStatus={roomStatus}
        submissionGateReason={submissionGate.reason}
      />
      <section className="seat-grid">
        {Object.entries(snapshot?.players ?? {}).map(([playerId, player]) => (
          <article className="seat-card" key={playerId}>
            <span className="eyebrow">{player.seat ?? "席位"}</span>
            <h2>{player.name ?? playerId}</h2>
            <p>分数 {player.score ?? 0} / 手牌 {player.handSize ?? player.zones?.handHidden ?? player.zones?.hand?.length ?? 0}</p>
            <div className="player-pills">
              <StatusPill tone={player.deckSubmitted ? "good" : "warn"}>{player.deckSubmitted ? "已提交卡组" : "未提交卡组"}</StatusPill>
              <StatusPill tone={player.ready ? "good" : "neutral"}>{player.ready ? "已准备" : "未准备"}</StatusPill>
            </div>
          </article>
        ))}
        {!snapshot && <div className="empty-panel">尚未收到房间快照，请先连接。</div>}
      </section>
      <section className="audit-banner">
        <strong>当前可提交行动：</strong>
        <span>{candidateListLabel(controller.state.prompt)}</span>
      </section>
      <RoomSubmissionReceipt feedback={controller.state.lastCommandSubmission} />
      <section className="room-log-panel">
        <header>
          <div>
            <span className="eyebrow">房间日志</span>
            <h2>服务端消息</h2>
          </div>
          <StatusPill tone={controller.state.errors.length > 0 ? "bad" : "good"}>
            {controller.state.errors.length > 0 ? `${controller.state.errors.length} 个错误` : "无错误"}
          </StatusPill>
        </header>
        <p>{controller.state.lastSystemMessage ?? "等待服务端房间消息。"}</p>
        <div className="room-log-list">
          {controller.state.errors.length === 0 && controller.state.events.length === 0 && <span className="empty-hint">暂无服务端事件或错误。</span>}
          {controller.state.errors.map((error, index) => (
            <article className="room-log-entry is-error" key={`${error.code}-${index}`}>
              <strong>{errorCodeLabel(error.code)}</strong>
              <span>{errorMessageLabel(error)}</span>
            </article>
          ))}
          {controller.state.events.slice(0, 8).map((event, index) => (
            <article className="room-log-entry" key={`${event.kind}-${index}`}>
              <strong>{eventKindLabel(event.kind)}</strong>
              <span>{eventDescriptionLabel(event)}</span>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}

function RoomSetupChecklist({
  connectionStatus,
  currentPlayer,
  lastCommandSubmission,
  playerCount,
  players,
  quickActions,
  roomStatus,
  submissionGateReason
}: {
  connectionStatus: ReturnType<typeof useMatchController>["state"]["status"];
  currentPlayer?: { deckSubmitted?: boolean; ready?: boolean; seat?: string };
  lastCommandSubmission?: CommandSubmissionFeedback;
  playerCount: number;
  players: Array<{ deckSubmitted?: boolean; ready?: boolean; seat?: string }>;
  quickActions: ServerQuickActionEntry[];
  roomStatus: string;
  submissionGateReason: string;
}) {
  const submittedCount = players.filter((player) => player.deckSubmitted).length;
  const readyCount = players.filter((player) => player.ready).length;
  const quickActionById = Object.fromEntries(quickActions.map((entry) => [entry.id, entry]));
  const submitDeckAction = quickActionById.submitDeck;
  const readyAction = quickActionById.ready;
  const connected = connectionStatus === "connected";
  const seated = Boolean(currentPlayer);
  const targetPlayerCount = 2;
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

  return (
    <section className="room-flow-panel">
      <article>
        <strong>1. 服务端连接</strong>
        <StatusPill tone={connectionStatusTone(connectionStatus)}>{connectionStatusLabel(connectionStatus)}</StatusPill>
        <p>{connected ? "已连接，可接收房间快照与行动提示。" : "下一步：连接/重连并入座。"}</p>
      </article>
      <article>
        <strong>2. 入座</strong>
        <StatusPill tone={allSeated ? "good" : seated ? "info" : "warn"}>
          {playerCount}/{targetPlayerCount} 人
        </StatusPill>
        <p>{seated ? `当前席位：${currentPlayer?.seat ?? "服务端未命名席位"}。` : "阻塞：当前玩家尚未出现在服务端快照中。"}</p>
        <p>下一步：{allSeated ? "进入卡组提交确认。" : "等待另一名玩家入座或重新连接。"}</p>
      </article>
      <article>
        <strong>3. 卡组提交</strong>
        <StatusPill tone={allSubmitted ? "good" : submitDeckAction?.state === "ready" ? "info" : "warn"}>
          {submittedCount}/{targetPlayerCount} 已提交
        </StatusPill>
        <p>{currentPlayer?.deckSubmitted ? "当前玩家已提交卡组。" : `阻塞：${submitDeckAction?.title ?? "等待服务端提交构筑候选。"}`}</p>
        <p>下一步：{deckNextStep(currentPlayer?.deckSubmitted, submitDeckAction)}</p>
      </article>
      <article>
        <strong>4. 准备</strong>
        <StatusPill tone={allReady ? "good" : readyAction?.state === "ready" ? "info" : "warn"}>
          {readyCount}/{targetPlayerCount} 已准备
        </StatusPill>
        <p>{currentPlayer?.ready ? "当前玩家已准备。" : `阻塞：${readyAction?.title ?? "等待服务端准备候选。"}`}</p>
        <p>下一步：{readyNextStep(currentPlayer?.ready, readyAction)}</p>
      </article>
      <article>
        <strong>5. 开局</strong>
        <StatusPill tone={startGate.tone}>{startGate.label}</StatusPill>
        <p>{startGate.tone === "good" ? "状态：" : "阻塞："}{startGate.reason}</p>
        <p>下一步：{lastCommandSubmission?.state === "failed" ? "根据服务端拒绝原因调整后重试，或重新同步快照。" : startGate.nextStep}</p>
      </article>
    </section>
  );
}

function RoomSubmissionReceipt({ feedback }: { feedback?: CommandSubmissionFeedback }) {
  if (!feedback) {
    return (
      <section className="room-submission-panel" data-room-submission-state="empty">
        <span className="eyebrow">提交回执</span>
        <h2>等待玩家提交</h2>
        <p>房间页会在这里显示服务端接受、拒绝或失败原因。</p>
      </section>
    );
  }

  const tone = feedback.state === "sent" ? "good" : feedback.state === "failed" ? "bad" : "info";
  const nextStep = feedback.state === "sent"
    ? "等待服务端事件、快照或下一条行动提示。"
    : feedback.state === "failed"
      ? "按服务端消息修正操作；必要时重新同步快照后再提交。"
      : "保持当前页面，等待服务端入口回执。";

  return (
    <section className="room-submission-panel" data-room-submission-state={feedback.state}>
      <header>
        <div>
          <span className="eyebrow">提交回执</span>
          <h2>{feedback.stateLabel}</h2>
        </div>
        <StatusPill tone={tone}>{feedback.cmdType}</StatusPill>
      </header>
      <p>{feedback.message}</p>
      <dl>
        <div>
          <dt>服务端状态</dt>
          <dd>{feedback.receiptState ?? feedback.errorCode ?? "等待回执"}</dd>
        </div>
        <div>
          <dt>快照</dt>
          <dd>{feedback.snapshotTick ?? "未绑定"}</dd>
        </div>
        <div>
          <dt>下一步</dt>
          <dd>{nextStep}</dd>
        </div>
      </dl>
    </section>
  );
}

function deckNextStep(deckSubmitted: boolean | undefined, action?: ServerQuickActionEntry): string {
  if (deckSubmitted) {
    return "等待全部玩家完成提交。";
  }

  if (action?.state === "ready") {
    return "点击导入构筑并等待服务端回执。";
  }

  return action?.state === "missing" ? "等待服务端提供 SUBMIT_DECK 候选。" : "按服务端提示解除提交阻塞。";
}

function readyNextStep(ready: boolean | undefined, action?: ServerQuickActionEntry): string {
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
}): { label: string; nextStep: string; reason: string; tone: "neutral" | "good" | "warn" | "bad" | "info" } {
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

function RoomPromptButtons({
  entries,
  onRunAction
}: {
  entries: ServerQuickActionEntry[];
  onRunAction: (entry: ServerQuickActionEntry) => void;
}) {
  const hasServerLifecycleCandidate = entries.some((entry) => entry.candidateAction);

  return (
    <>
      {entries.map((entry) => (
        <Button
          data-room-quick-action={entry.id}
          data-room-quick-action-candidate={entry.candidateAction ?? ""}
          data-room-quick-action-command-source={entry.commandSource}
          data-room-quick-action-command-source-label={entry.commandSourceLabel}
          data-room-quick-action-state={entry.state}
          disabled={entry.disabled}
          icon={entry.id === "submitDeck" ? <Send size={16} /> : <Check size={16} />}
          key={entry.id}
          onClick={() => onRunAction(entry)}
          title={entry.title}
          variant="secondary"
        >
          {entry.label}
        </Button>
      ))}
      {!hasServerLifecycleCandidate && <span className="empty-hint">等待服务端可提交候选。</span>}
    </>
  );
}
