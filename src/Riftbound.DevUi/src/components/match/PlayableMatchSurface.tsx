import { ArrowLeft, Wifi } from "lucide-react";
import type { CSSProperties, ReactNode } from "react";
import { Button } from "../ui/Button";

export type PlayableMatchSurfaceProps = {
  actionLayer: ReactNode;
  canAct: boolean;
  connectionLabel: string;
  debugContent: ReactNode;
  guidance: ReactNode;
  matchId: string;
  objectTray?: ReactNode;
  onExit: () => void;
  phaseLabel: string;
  promptTitle: string;
  quickActions: ReactNode;
  recovery?: ReactNode;
  style?: CSSProperties;
  table: ReactNode;
  turnNumber: number;
  windowLabel: string;
};

export function PlayableMatchSurface({
  actionLayer,
  canAct,
  connectionLabel,
  debugContent,
  guidance,
  matchId,
  objectTray,
  onExit,
  phaseLabel,
  promptTitle,
  quickActions,
  recovery,
  style,
  table,
  turnNumber,
  windowLabel
}: PlayableMatchSurfaceProps) {
  return (
    <div className="wire-match-page playable-match-surface" data-playable-match-surface style={style}>
      <header className="wire-topbar game-match-topbar" aria-label="对局状态">
        <Button aria-label="返回大厅" icon={<ArrowLeft size={18} />} onClick={onExit} variant="ghost" />
        <div className="game-match-title">
          <span className={canAct ? "game-turn-state is-active" : "game-turn-state"}>{canAct ? "轮到你" : "等待对手"}</span>
          <h1>{promptTitle}</h1>
          <small>房间 {matchId}</small>
        </div>
        <div className="wire-status-line game-match-status" role="group" aria-label="回合状态">
          <span>第 {turnNumber} 回合</span>
          <span>{phaseLabel}</span>
          <span>{windowLabel}</span>
          <span className="game-connection-state"><Wifi size={14} />{connectionLabel}</span>
        </div>
        <div className="wire-topbar-actions game-match-quick-actions">{quickActions}</div>
      </header>

      <div className="game-match-guidance">{guidance}</div>
      {recovery ? <div className="game-match-recovery">{recovery}</div> : null}

      <section className="wire-table-shell game-table-stage" data-game-table tabIndex={0}>
        {table}
        <div className="arena-action-layer" data-arena-action-layer>
          {objectTray ? <div className="game-object-tray">{objectTray}</div> : null}
          {actionLayer}
        </div>
      </section>

      <details className="game-debug-drawer" data-game-debug-drawer>
        <summary>连接与规则诊断</summary>
        <div className="game-debug-drawer-body">{debugContent}</div>
      </details>
    </div>
  );
}
