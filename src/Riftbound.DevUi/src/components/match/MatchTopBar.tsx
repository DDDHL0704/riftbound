import { Wifi, WifiOff } from "lucide-react";
import { ActionPromptDto, ConnectionStatus, SnapshotDto } from "../../types/protocol";
import { asRecord, asString } from "../../utils/collections";
import { connectionStatusLabel, connectionStatusTone, matchPhaseLabel, timingStateLabel } from "../../utils/formatters";
import { StatusPill } from "../ui/StatusPill";

export function MatchTopBar({
  prompt,
  snapshot,
  status,
  playerId
}: {
  prompt?: ActionPromptDto;
  snapshot?: SnapshotDto;
  status: ConnectionStatus;
  playerId: string;
}) {
  const timing = asRecord(snapshot?.timing);
  const turnWindow = asRecord(timing.turnWindow);
  const spellDuel = asRecord(timing.spellDuel);
  const promptPlayer = asString(timing.promptPlayerId, "无");
  const promptView = prompt?.view;
  const phase = asString(timing.phase, "");
  const windowState = asString(turnWindow.state, asString(timing.timingState, ""));
  const title = snapshot
    ? `第 ${snapshot.turnNumber ?? 0} 回合｜${matchPhaseLabel(phase)}｜${timingStateLabel(windowState)}`
    : "等待服务端快照";
  const promptTitle = promptView?.title?.trim();
  const promptType = promptView?.type?.trim();
  const promptStatus = promptTitle
    ? promptType
      ? `${promptTitle} / ${promptType}`
      : promptTitle
    : "无";

  return (
    <section className="match-topbar">
      <div>
        <span className="eyebrow">对战状态</span>
        <h1>{title}</h1>
      </div>
      <StatusPill tone={connectionStatusTone(status)}>{connectionStatusLabel(status)}</StatusPill>
      <StatusPill tone={snapshot?.activePlayerId === playerId ? "good" : "neutral"}>回合玩家：{snapshot?.activePlayerId ?? "无"}</StatusPill>
      <StatusPill tone={asString(turnWindow.actingPlayerId, "") === playerId ? "good" : "neutral"}>行动权：{asString(turnWindow.actingPlayerId, "无")}</StatusPill>
      <StatusPill tone={asString(spellDuel.focusPlayerId, "") === playerId ? "good" : "info"}>焦点：{asString(spellDuel.focusPlayerId, "无")}</StatusPill>
      <StatusPill tone={promptPlayer === playerId ? "warn" : "neutral"}>提示：{promptPlayer}</StatusPill>
      <StatusPill tone={prompt?.actionable ? "warn" : "neutral"}>窗口：{promptStatus}</StatusPill>
      {status === "connected" ? <Wifi size={20} aria-hidden /> : <WifiOff size={20} aria-hidden />}
    </section>
  );
}
