import { RefreshCw } from "lucide-react";
import { useEffect, useState } from "react";
import { AppRoute } from "../app/router";
import { CardDetailDrawer } from "../components/cards/CardDetailDrawer";
import { InspectedCard } from "../components/cards/CardFace";
import { ActionPanel } from "../components/match/ActionPanel";
import { BattlefieldArea } from "../components/match/BattlefieldArea";
import { EventLog } from "../components/match/EventLog";
import { MatchTopBar } from "../components/match/MatchTopBar";
import { PlayerBoard } from "../components/match/PlayerBoard";
import { StackPanel } from "../components/match/StackPanel";
import { Button } from "../components/ui/Button";
import { useCatalog } from "../stores/catalogStore";
import { useMatchController } from "../stores/useMatchController";
import { useSettings } from "../stores/settingsStore";

export function MatchPage({ matchId, onNavigate }: { matchId: string; onNavigate: (route: AppRoute) => void }) {
  const { settings } = useSettings();
  const { specByNo } = useCatalog();
  const controller = useMatchController(settings.serverUrl, matchId, settings.playerId);
  const snapshot = controller.state.snapshot;
  const players = Object.entries(snapshot?.players ?? {});
  const self = players.find(([playerId]) => playerId === settings.playerId);
  const opponents = players.filter(([playerId]) => playerId !== settings.playerId);
  const [inspectedCard, setInspectedCard] = useState<InspectedCard | undefined>();
  const roomStatus = typeof snapshot?.timing?.roomStatus === "string" ? snapshot.timing.roomStatus : "";

  useEffect(() => {
    if (roomStatus === "FINISHED") {
      onNavigate({ name: "result", matchId });
    }
  }, [matchId, onNavigate, roomStatus]);

  return (
    <div className="match-page">
      <MatchTopBar playerId={settings.playerId} snapshot={snapshot} status={controller.state.status} />
      <section className="match-command-row">
        <Button icon={<RefreshCw size={16} />} onClick={() => void controller.join()} variant="secondary">连接/重连</Button>
        <Button onClick={() => void controller.requestSnapshot()} variant="ghost">重新同步 snapshot</Button>
        <span>房间/Match：{matchId}</span>
        <span>当前玩家：{settings.playerId}</span>
      </section>
      <div className="match-layout">
        <aside className="left-rail">
          <EventLog density={settings.logDensity} errors={controller.state.errors} events={controller.state.events} />
        </aside>
        <main className="play-surface">
          {opponents.map(([playerId, player]) => (
            <PlayerBoard key={playerId} onInspectCard={setInspectedCard} perspectivePlayerId={settings.playerId} player={player} playerId={playerId} specs={specByNo} />
          ))}
          <BattlefieldArea onInspectCard={setInspectedCard} snapshot={snapshot} specs={specByNo} />
          {self ? (
            <PlayerBoard onInspectCard={setInspectedCard} perspectivePlayerId={settings.playerId} player={self[1]} playerId={self[0]} specs={specByNo} />
          ) : (
            <div className="empty-panel">还没有自己的玩家视角。请先在房间页入座。</div>
          )}
        </main>
        <aside className="right-rail">
          <StackPanel snapshot={snapshot} />
          <ActionPanel
            connectionStatus={controller.state.status}
            onCommand={(command) => void controller.submitCommand(command)}
            onReady={() => void controller.ready()}
            onSubmitStarterDeck={() => void controller.submitStarterDeck()}
            playerId={settings.playerId}
            prompt={controller.state.prompt}
            snapshot={snapshot}
          />
        </aside>
      </div>
      <CardDetailDrawer
        card={inspectedCard}
        onClose={() => setInspectedCard(undefined)}
        onCommand={(command) => void controller.submitCommand(command)}
        prompt={controller.state.prompt}
      />
    </div>
  );
}
