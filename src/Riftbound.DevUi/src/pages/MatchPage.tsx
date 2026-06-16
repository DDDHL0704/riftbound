import { Maximize2, RefreshCw, RotateCcw, X } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { AppRoute } from "../app/router";
import { CardDetailDrawer } from "../components/cards/CardDetailDrawer";
import { CardFace, InspectedCard } from "../components/cards/CardFace";
import { ActionPanel } from "../components/match/ActionPanel";
import { BattlefieldArea } from "../components/match/BattlefieldArea";
import { EventLog, eventDescriptionLabel, eventKindLabel } from "../components/match/EventLog";
import { MatchStatusPanel } from "../components/match/MatchStatusPanel";
import { MatchTopBar } from "../components/match/MatchTopBar";
import { PlayerBoard } from "../components/match/PlayerBoard";
import { SnapshotDebugPanel } from "../components/match/SnapshotDebugPanel";
import { StackPanel } from "../components/match/StackPanel";
import { Button } from "../components/ui/Button";
import { ScrollArea } from "../components/ui/ScrollArea";
import { StatusPill } from "../components/ui/StatusPill";
import { useCatalog } from "../stores/catalogStore";
import { useMatchController } from "../stores/useMatchController";
import { useSettings } from "../stores/settingsStore";
import { CardObjectView, GameEvent, SnapshotDto } from "../types/protocol";
import { battlefieldResolutionTimeline } from "../utils/battlefieldResolutions";

export function MatchPage({ matchId, onNavigate }: { matchId: string; onNavigate: (route: AppRoute) => void }) {
  const { settings } = useSettings();
  const { specByNo } = useCatalog();
  const controller = useMatchController(settings.serverUrl, matchId, settings.playerId);
  const snapshot = controller.state.snapshot;
  const players = Object.entries(snapshot?.players ?? {});
  const self = players.find(([playerId]) => playerId === settings.playerId);
  const opponents = players.filter(([playerId]) => playerId !== settings.playerId);
  const [inspectedCard, setInspectedCard] = useState<InspectedCard | undefined>();
  const [detailCard, setDetailCard] = useState<InspectedCard | undefined>();
  const roomStatus = typeof snapshot?.timing?.roomStatus === "string" ? snapshot.timing.roomStatus : "";
  const objectLookup = useMemo<Record<string, CardObjectView>>(() => {
    const entries = Object.values(snapshot?.players ?? {})
      .flatMap((player) => Object.entries(player.objects ?? {}));
    return Object.fromEntries(entries);
  }, [snapshot?.players]);
  const defaultFocusedCard = useMemo<InspectedCard | undefined>(() => {
    const player = self?.[1];
    const candidateIds = [
      ...(player?.zones?.hand ?? []),
      ...(player?.zones?.battlefields ?? []),
      ...(player?.zones?.base ?? []),
      ...(player?.zones?.championZone ?? []),
      ...(player?.zones?.legendZone ?? [])
    ];

    for (const objectId of candidateIds) {
      const object = objectLookup[objectId];
      if (object?.cardNo) {
        return {
          object,
          objectId,
          spec: specByNo[object.cardNo]
        };
      }
    }

    return undefined;
  }, [objectLookup, self, specByNo]);
  const focusedCard = inspectedCard ?? defaultFocusedCard;

  useEffect(() => {
    if (roomStatus === "FINISHED") {
      onNavigate({ name: "result", matchId });
    }
  }, [matchId, onNavigate, roomStatus]);

  return (
    <div className="match-page">
      <MatchTopBar playerId={settings.playerId} prompt={controller.state.prompt} snapshot={snapshot} status={controller.state.status} />
      <section className="match-command-row">
        <div className="match-command-actions">
          <Button icon={<RefreshCw size={16} />} onClick={() => void controller.join()} variant="secondary">连接/重连</Button>
          <Button icon={<RotateCcw size={16} />} onClick={() => void controller.requestSnapshot()} variant="ghost">重新同步快照</Button>
        </div>
        <div className="match-command-meta">
          <span>房间/对局：{matchId}</span>
          <span>当前玩家：{settings.playerId}</span>
        </div>
      </section>
      <RuleEventRibbon events={controller.state.events} snapshot={snapshot} />
      <DomainDock />
      <div className="match-workbench">
        <section aria-label="对战桌面" className="play-surface">
          {opponents.map(([playerId, player]) => (
            <PlayerBoard key={playerId} onInspectCard={setInspectedCard} perspectivePlayerId={settings.playerId} player={player} playerId={playerId} specs={specByNo} />
          ))}
          <BattlefieldArea events={controller.state.events} onInspectCard={setInspectedCard} snapshot={snapshot} specs={specByNo} />
          {self ? (
            <PlayerBoard onInspectCard={setInspectedCard} perspectivePlayerId={settings.playerId} player={self[1]} playerId={self[0]} specs={specByNo} />
          ) : (
            <div className="empty-panel">还没有自己的玩家视角。请先在房间页入座。</div>
          )}
        </section>
        <aside className="action-rail">
          <FocusedCardDock
            card={focusedCard}
            onClear={() => setInspectedCard(undefined)}
            onOpenDetails={() => {
              if (focusedCard) {
                setDetailCard(focusedCard);
              }
            }}
          />
          <ActionPanel
            connectionStatus={controller.state.status}
            onCommand={(command) => void controller.submitCommand(command)}
            onReady={() => void controller.ready()}
            onSubmitStarterDeck={() => void controller.submitStarterDeck()}
            playerId={settings.playerId}
            prompt={controller.state.prompt}
            snapshot={snapshot}
          />
          <details className="match-diagnostics">
            <summary>状态 / 日志 / 规则队列</summary>
            <ScrollArea className="match-diagnostics-scroll">
              <div className="match-diagnostics-grid">
                <MatchStatusPanel playerId={settings.playerId} prompt={controller.state.prompt} snapshot={snapshot} />
                <StackPanel snapshot={snapshot} />
                <EventLog density={settings.logDensity} errors={controller.state.errors} events={controller.state.events} />
                <SnapshotDebugPanel prompt={controller.state.prompt} snapshot={snapshot} />
              </div>
            </ScrollArea>
          </details>
        </aside>
      </div>
      <CardDetailDrawer
        card={detailCard}
        objectLookup={objectLookup}
        onClose={() => setDetailCard(undefined)}
        onCommand={(command) => void controller.submitCommand(command)}
        prompt={controller.state.prompt}
      />
    </div>
  );
}

function DomainDock() {
  const domains = [
    { label: "奥术", tone: "arcane" },
    { label: "狂怒", tone: "fury" },
    { label: "秩序", tone: "order" },
    { label: "自然", tone: "wild" },
    { label: "混沌", tone: "chaos" },
    { label: "守护", tone: "guard" }
  ];

  return (
    <div className="match-domain-dock" aria-label="符文特性">
      {domains.map((domain) => (
        <span className={`domain-token domain-${domain.tone}`} key={domain.tone} title={domain.label}>
          {domain.label.slice(0, 1)}
        </span>
      ))}
    </div>
  );
}

function FocusedCardDock({
  card,
  onClear,
  onOpenDetails
}: {
  card?: InspectedCard;
  onClear: () => void;
  onOpenDetails: () => void;
}) {
  return (
    <section className="side-panel focused-card-dock" aria-label="焦点卡牌">
      <header>
        <span className="eyebrow">焦点卡牌</span>
        <div className="focused-card-actions">
          <Button disabled={!card} icon={<Maximize2 size={14} />} onClick={onOpenDetails} variant="ghost">详情</Button>
          <Button disabled={!card} icon={<X size={14} />} onClick={onClear} variant="ghost">清除</Button>
        </div>
      </header>
      <div className="focused-card-stage">
        {card ? (
          <CardFace object={card.object} objectId={card.objectId} spec={card.spec} />
        ) : (
          <div className="focused-card-placeholder" aria-hidden="true">
            <span />
          </div>
        )}
      </div>
    </section>
  );
}

function RuleEventRibbon({ events, snapshot }: { events: GameEvent[]; snapshot?: SnapshotDto }) {
  const timing = snapshot?.timing ?? {};
  const pendingTaskQueue = timing.pendingTaskQueue as { phase?: string; activeTaskId?: string; tasks?: Array<{ kind?: string }> } | undefined;
  const resolutionItems = battlefieldResolutionTimeline(snapshot).slice(0, 4);
  const keyEvents = events.filter((event) => importantEventKinds.has(event.kind)).slice(0, Math.max(0, 5 - resolutionItems.length));
  const taskKinds = (pendingTaskQueue?.tasks ?? [])
    .map((task) => task.kind)
    .filter((kind): kind is string => Boolean(kind))
    .slice(0, 3);

  return (
    <section className="match-rule-ribbon" aria-label="关键规则状态">
      <div className="rule-ribbon-state">
        <StatusPill tone={pendingTaskQueue?.phase && pendingTaskQueue.phase !== "IDLE" ? "warn" : "neutral"}>
          规则队列：{pendingTaskQueue?.phase ?? "空闲"}
        </StatusPill>
        {taskKinds.map((kind) => <span key={kind}>{battlefieldTaskText(kind)}</span>)}
      </div>
      <div className="rule-ribbon-events">
        {resolutionItems.length === 0 && keyEvents.length === 0 ? (
          <span>暂无关键战场事件</span>
        ) : resolutionItems.map((item) => (
          <span className="rule-ribbon-resolution" key={item.id}>
            <strong>{item.label}</strong>
            {item.detail}
          </span>
        ))}
        {keyEvents.map((event, index) => (
          <span key={`${event.kind}-${index}`}>
            <strong>{eventKindLabel(event.kind)}</strong>
            {eventDescriptionLabel(event)}
          </span>
        ))}
      </div>
    </section>
  );
}

const importantEventKinds = new Set([
  "BATTLEFIELD_CONTESTED",
  "SPELL_DUEL_STARTED",
  "SPELL_DUEL_CLOSED",
  "BATTLE_DECLARED",
  "BATTLEFIELD_CONTROL_RESOLVED",
  "BATTLEFIELD_CONQUERED",
  "BATTLEFIELD_HELD",
  "BATTLEFIELD_TRIGGER_RESOLVED",
  "SCORE_GAINED",
  "UNIT_TOKEN_CREATED"
]);

function battlefieldTaskText(kind: string): string {
  switch (kind) {
    case "BATTLEFIELD_CONTESTED":
      return "战场控制检查";
    case "START_SPELL_DUEL":
      return "法术对决";
    case "START_BATTLE":
      return "战斗";
    default:
      return "服务端任务";
  }
}
