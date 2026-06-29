import { Check, Copy, FileText, RefreshCcw, Send, Swords, Users, Wifi } from "lucide-react";
import { useCallback, useMemo, useState } from "react";
import { AppRoute } from "../app/router";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { useMatchController } from "../stores/useMatchController";
import type { CommandSubmissionFeedback } from "../stores/useMatchController";
import { useSettings } from "../stores/settingsStore";
import { candidateListLabel } from "../components/match/ActionPanel";
import { ConnectionRecoveryPanel } from "../components/match/ConnectionRecoveryPanel";
import { eventDescriptionLabel, eventKindLabel } from "../components/match/EventLog";
import { errorCodeLabel, errorMessageLabel } from "../utils/errors";
import { buildErrorResolutionPlan, type ErrorResolutionAction, type ErrorResolutionPlan } from "../utils/errorResolutionPlan";
import { connectionStatusLabel, connectionStatusTone } from "../utils/formatters";
import { buildRoomSetupFlowPlan } from "../utils/roomSetupFlowPlan";
import { buildServerQuickActionPlan, quickActionCommandUiSource, type ServerQuickActionEntry } from "../utils/serverQuickActionPlan";
import { buildServerSubmissionGatePlan } from "../utils/serverSubmissionGatePlan";
import { asRecord, asString } from "../utils/collections";
import { buildRoomWorkflowSurfacePlan, type RoomWorkflowSurfacePlan, type RoomWorkflowRegionState } from "../utils/roomWorkflowSurfacePlan";

export function RoomPage({ roomId, onNavigate }: { roomId: string; onNavigate: (route: AppRoute) => void }) {
  const { settings } = useSettings();
  const controller = useMatchController(settings.serverUrl, roomId, settings.playerId);
  const snapshot = controller.state.snapshot;
  const prompt = controller.state.prompt;
  const players = Object.values(snapshot?.players ?? {});
  const roomStatus = asString(asRecord(snapshot?.timing).roomStatus, "");
  const connected = controller.state.status === "connected";
  const [codeCopied, setCodeCopied] = useState(false);
  const copyRoomCode = useCallback(() => {
    void navigator.clipboard?.writeText(roomId).then(() => {
      setCodeCopied(true);
      window.setTimeout(() => setCodeCopied(false), 1500);
    });
  }, [roomId]);
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
  const errorResolutionPlan = useMemo(() => buildErrorResolutionPlan({
    connectionStatus: controller.state.status,
    errors: controller.state.errors,
    hasSnapshot: Boolean(snapshot),
    lastCommandSubmission: controller.state.lastCommandSubmission,
    surface: "room"
  }), [
    controller.state.errors,
    controller.state.lastCommandSubmission,
    controller.state.status,
    snapshot
  ]);
  const roomSetupFlowPlan = useMemo(() => buildRoomSetupFlowPlan({
    connectionStatus: controller.state.status,
    currentPlayer: snapshot?.players[settings.playerId],
    lastCommandSubmissionState: controller.state.lastCommandSubmission?.state,
    players,
    quickActions: roomQuickActionPlan.entries,
    roomStatus,
    submissionGateReason: submissionGate.reason
  }), [
    controller.state.lastCommandSubmission?.state,
    controller.state.status,
    players,
    roomQuickActionPlan.entries,
    roomStatus,
    settings.playerId,
    snapshot?.players,
    submissionGate.reason
  ]);
  const roomWorkflowSurfacePlan = useMemo(() => buildRoomWorkflowSurfacePlan({
    connectionStatus: controller.state.status,
    errorCount: controller.state.errors.length,
    errorState: errorResolutionPlan.state,
    eventCount: controller.state.events.length,
    hasSnapshot: Boolean(snapshot),
    promptSnapshotTick: prompt?.snapshotTick,
    quickActions: roomQuickActionPlan.entries,
    roomStatus,
    setupGate: roomSetupFlowPlan.startGate,
    snapshotTick: snapshot?.tick,
    submissionState: controller.state.lastCommandSubmission?.state
  }), [
    controller.state.errors.length,
    controller.state.events.length,
    controller.state.lastCommandSubmission?.state,
    controller.state.status,
    errorResolutionPlan.state,
    roomQuickActionPlan.entries,
    roomSetupFlowPlan.startGate,
    roomStatus,
    prompt?.snapshotTick,
    snapshot,
    snapshot?.tick
  ]);
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
  const runErrorResolutionAction = useCallback((action: ErrorResolutionAction) => {
    if (action.disabled) {
      return;
    }

    switch (action.id) {
      case "connect":
        void controller.join();
        return;
      case "openDecks":
        onNavigate({ name: "decks" });
        return;
      case "resync":
        void controller.requestSnapshot();
        return;
      case "reviewPrompt":
        document.getElementById("room-current-actions")?.focus();
        return;
      case "waitServer":
        return;
    }
  }, [controller, onNavigate]);

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
      <RoomCodeBanner
        codeCopied={codeCopied}
        connected={connected}
        onCopy={copyRoomCode}
        playerCount={players.length}
        roomId={roomId}
      />
      <RoomWorkflowSurface plan={roomWorkflowSurfacePlan} />
      <section className="room-actions">
        <div className="room-action-region" data-room-recovery-region>
          <ConnectionRecoveryPanel
            connectionStatus={controller.state.status}
            hasSnapshot={Boolean(snapshot)}
            lastSystemMessage={controller.state.lastSystemMessage}
            onConnect={() => void controller.join()}
            onDisconnect={() => void controller.disconnect()}
            onResync={() => void controller.requestSnapshot()}
            promptSnapshotTick={prompt?.snapshotTick}
            snapshotTick={snapshot?.tick}
            surface="room"
          />
        </div>
        <div className="room-action-region room-quick-action-region" data-room-actions-region>
          <RoomPromptButtons
            entries={roomQuickActionPlan.entries}
            onRunAction={runRoomQuickAction}
          />
        </div>
        <Button icon={<Swords size={18} />} onClick={() => onNavigate({ name: "match", matchId: roomId })}>进入对战桌面</Button>
      </section>
      <RoomSetupChecklist
        plan={roomSetupFlowPlan}
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
        <span id="room-current-actions" tabIndex={-1} />
        <strong>当前可提交行动：</strong>
        <span>{candidateListLabel(controller.state.prompt)}</span>
      </section>
      <RoomSubmissionReceipt feedback={controller.state.lastCommandSubmission} />
      <RoomErrorResolutionPanel
        onRunAction={runErrorResolutionAction}
        plan={errorResolutionPlan}
      />
      <section className="room-log-panel" data-room-log-region>
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

function RoomCodeBanner({
  codeCopied,
  connected,
  onCopy,
  playerCount,
  roomId
}: {
  codeCopied: boolean;
  connected: boolean;
  onCopy: () => void;
  playerCount: number;
  roomId: string;
}) {
  const opponentPresent = playerCount >= 2;
  const presenceState = !connected ? "offline" : opponentPresent ? "ready" : "waiting";
  const presenceLabel = !connected
    ? "未连接"
    : opponentPresent
      ? "对手已加入"
      : "等待对手加入";

  return (
    <section
      className="room-code-banner"
      data-room-code={roomId}
      data-room-code-banner
      data-room-presence={presenceState}
      data-room-player-count={playerCount}
    >
      <div className="room-code-banner-main">
        <span className="eyebrow">房间码 · 分享给对手</span>
        <div className="room-code-row">
          <code className="room-code-value">{roomId}</code>
          <Button
            data-room-code-copy
            icon={codeCopied ? <Check size={16} /> : <Copy size={16} />}
            onClick={onCopy}
            variant="secondary"
          >
            {codeCopied ? "已复制" : "复制房间码"}
          </Button>
        </div>
        <p>把这个房间码发给对手，对手在大厅「加入房间」处输入即可进入同一对局。</p>
      </div>
      <div className="room-code-presence" data-room-presence-state={presenceState}>
        <StatusPill tone={presenceState === "ready" ? "good" : presenceState === "waiting" ? "warn" : "neutral"}>
          <Users size={14} /> {presenceLabel}
        </StatusPill>
        <span className="room-code-presence-count">{Math.min(playerCount, 2)}/2 玩家</span>
      </div>
    </section>
  );
}

function RoomErrorResolutionPanel({
  onRunAction,
  plan
}: {
  onRunAction: (action: ErrorResolutionAction) => void;
  plan: ErrorResolutionPlan;
}) {
  return (
    <section className="room-error-resolution-panel" data-error-resolution-state={plan.state} data-room-errors-region>
      <header>
        <div>
          <span className="eyebrow">错误处理</span>
          <h2>{plan.headline}</h2>
        </div>
        <StatusPill tone={plan.tone}>{plan.statusLabel}</StatusPill>
      </header>
      <p>{plan.detail}</p>
      <dl>
        {plan.evidenceRows.map((row) => (
          <div
            data-error-resolution-evidence-label={row.label}
            data-error-resolution-evidence-row={row.label}
            data-error-resolution-evidence-value={row.value}
            key={row.label}
          >
            <dt>{row.label}</dt>
            <dd>{row.value}</dd>
          </div>
        ))}
      </dl>
      <div className="room-error-next-step" data-error-resolution-next-step>
        <strong>下一步</strong>
        <span>{plan.nextStep}</span>
      </div>
      <div className="room-error-actions">
        {plan.actions.map((action) => (
          <Button
            data-error-resolution-action={action.id}
            data-error-resolution-action-disabled={action.disabled ? "true" : "false"}
            data-error-resolution-action-state={action.state}
            disabled={action.disabled}
            icon={errorResolutionActionIcon(action.id)}
            key={action.id}
            onClick={() => onRunAction(action)}
            title={action.title}
            variant={action.state === "primary" ? "primary" : action.state === "secondary" ? "secondary" : "ghost"}
          >
            {action.label}
          </Button>
        ))}
      </div>
    </section>
  );
}

function errorResolutionActionIcon(actionId: ErrorResolutionAction["id"]) {
  switch (actionId) {
    case "connect":
      return <Wifi size={16} />;
    case "openDecks":
      return <FileText size={16} />;
    case "resync":
      return <RefreshCcw size={16} />;
    case "reviewPrompt":
      return <Check size={16} />;
    case "waitServer":
      return <Send size={16} />;
  }
}

function RoomSetupChecklist({ plan }: { plan: ReturnType<typeof buildRoomSetupFlowPlan> }) {
  return (
    <section className="room-flow-panel" data-room-setup-region>
      {plan.steps.map((step) => (
        <article data-room-setup-step={step.id} key={step.id}>
          <strong>{step.title}</strong>
          <StatusPill tone={step.tone}>{step.stateLabel}</StatusPill>
          <p>{step.detail}</p>
          <p>下一步：{step.nextStep}</p>
        </article>
      ))}
    </section>
  );
}

function RoomSubmissionReceipt({ feedback }: { feedback?: CommandSubmissionFeedback }) {
  if (!feedback) {
    return (
      <section className="room-submission-panel" data-room-submission-region data-room-submission-state="empty">
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
    <section className="room-submission-panel" data-room-submission-region data-room-submission-state={feedback.state}>
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

function RoomWorkflowSurface({ plan }: { plan: RoomWorkflowSurfacePlan }) {
  return (
    <section
      aria-label="房间流程总览"
      className="room-workflow-surface"
      data-room-workflow-active-region={plan.activeRegionId}
      data-room-workflow-surface
      data-room-workflow-summary={plan.summary}
    >
      <header>
        <div>
          <span className="eyebrow">流程总览</span>
          <h2>房间到对战的服务端链路</h2>
        </div>
        <StatusPill tone="neutral">{plan.activeRegionId}</StatusPill>
      </header>
      <p>{plan.summary}</p>
      <div className="room-workflow-grid">
        {plan.sections.map((section) => (
          <article
            className="room-workflow-region"
            data-room-workflow-region={section.id}
            data-room-workflow-source={section.source}
            data-room-workflow-state={section.state}
            key={section.id}
          >
            <div>
              <strong>{section.label}</strong>
              <StatusPill tone={toneForWorkflowState(section.state)}>{section.value}</StatusPill>
            </div>
            <span>{section.source}</span>
            <p>{section.nextStep}</p>
          </article>
        ))}
      </div>
    </section>
  );
}

function toneForWorkflowState(state: RoomWorkflowRegionState) {
  switch (state) {
    case "blocking":
      return "bad";
    case "ready":
      return "good";
    case "waiting":
      return "warn";
    case "clear":
      return "neutral";
  }
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
