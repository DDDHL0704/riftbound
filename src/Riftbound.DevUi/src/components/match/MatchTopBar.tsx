import { Wifi, WifiOff } from "lucide-react";
import { ConnectionStatus, SnapshotDto } from "../../types/protocol";
import { asRecord, asString } from "../../utils/collections";
import { connectionStatusLabel, connectionStatusTone, matchPhaseLabel, timingStateLabel } from "../../utils/formatters";
import { StatusPill } from "../ui/StatusPill";

export function MatchTopBar({ snapshot, status, playerId }: { snapshot?: SnapshotDto; status: ConnectionStatus; playerId: string }) {
  const timing = asRecord(snapshot?.timing);
  const turnWindow = asRecord(timing.turnWindow);
  const spellDuel = asRecord(timing.spellDuel);
  const promptPlayer = asString(timing.promptPlayerId, "无");
  const phase = asString(timing.phase, "");
  const windowState = asString(turnWindow.state, asString(timing.timingState, ""));

  return (
    <section className="match-topbar">
      <div>
        <span className="eyebrow">对战状态</span>
        <h1>第 {snapshot?.turnNumber ?? 0} 回合｜{matchPhaseLabel(phase)}｜{timingStateLabel(windowState)}</h1>
      </div>
      <StatusPill tone={connectionStatusTone(status)}>{connectionStatusLabel(status)}</StatusPill>
      <StatusPill tone={snapshot?.activePlayerId === playerId ? "good" : "neutral"}>回合玩家：{snapshot?.activePlayerId ?? "无"}</StatusPill>
      <StatusPill tone={asString(turnWindow.actingPlayerId, "") === playerId ? "good" : "neutral"}>行动权：{asString(turnWindow.actingPlayerId, "无")}</StatusPill>
      <StatusPill tone={asString(spellDuel.focusPlayerId, "") === playerId ? "good" : "info"}>焦点：{asString(spellDuel.focusPlayerId, "无")}</StatusPill>
      <StatusPill tone={promptPlayer === playerId ? "warn" : "neutral"}>Prompt：{promptPlayer}</StatusPill>
      {status === "connected" ? <Wifi size={20} aria-hidden /> : <WifiOff size={20} aria-hidden />}
    </section>
  );
}
