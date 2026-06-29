import { ChevronRight, Home, RefreshCw, Swords, Trophy, UserRound } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { AppRoute } from "../app/router";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { ApiClient } from "../services/apiClient";
import { useSettings } from "../stores/settingsStore";
import { LeaderboardEntryDto, PlayerMatchDto, PlayerMatchParticipantDto, PlayerProfileDto } from "../types/protocol";

type ProfileLoadState = {
  profile?: PlayerProfileDto;
  matches: PlayerMatchDto[];
  leaderboard: LeaderboardEntryDto[];
  loading: boolean;
  error: string | null;
  checkedAt?: string;
};

const emptyProfileState: ProfileLoadState = {
  matches: [],
  leaderboard: [],
  loading: true,
  error: null
};

export function PlayerProfilePage({ handle, onNavigate }: { handle: string; onNavigate: (route: AppRoute) => void }) {
  const { settings } = useSettings();
  const api = useMemo(() => new ApiClient(settings.serverUrl), [settings.serverUrl]);
  const requestedHandle = handle.trim() || settings.playerId.trim() || "player";
  const [state, setState] = useState<ProfileLoadState>(emptyProfileState);

  const loadProfile = useCallback(
    async (signal?: AbortSignal) => {
      setState((current) => ({ ...current, loading: true, error: null }));
      try {
        const [profile, matches, leaderboard] = await Promise.all([
          api.playerProfile(requestedHandle, signal),
          api.playerMatches(requestedHandle, 50, signal),
          api.leaderboard(10, signal)
        ]);
        setState({
          profile,
          matches,
          leaderboard,
          loading: false,
          error: null,
          checkedAt: new Date().toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", second: "2-digit" })
        });
      } catch (error) {
        if (error instanceof Error && error.name === "AbortError") {
          return;
        }

        setState((current) => ({
          ...current,
          loading: false,
          error: error instanceof Error ? error.message : "资料加载失败。"
        }));
      }
    },
    [api, requestedHandle]
  );

  useEffect(() => {
    const controller = new AbortController();
    void loadProfile(controller.signal);
    return () => controller.abort();
  }, [loadProfile]);

  const profileHandle = state.profile?.handle ?? requestedHandle;
  const playerRank = state.leaderboard.find((entry) => sameHandle(entry.handle, profileHandle))?.rank;
  const totalMatches = state.profile?.totalMatches ?? 0;
  const wins = state.profile?.wins ?? 0;
  const losses = state.profile?.losses ?? 0;
  const winRate = state.profile?.winRate ?? 0;

  return (
    <div
      className="page-grid profile-page"
      data-profile-handle={profileHandle}
      data-profile-loading={state.loading ? "true" : "false"}
      data-profile-surface
      data-profile-total-matches={totalMatches}
    >
      <section className="page-header">
        <div>
          <span className="eyebrow">玩家资料</span>
          <h1>{profileHandle}</h1>
          <p>公开终局记录与最近对局。</p>
        </div>
        <div className="profile-header-actions">
          <Button icon={<RefreshCw size={16} />} onClick={() => void loadProfile()} variant="secondary">
            刷新
          </Button>
          {settings.playerId.trim() && !sameHandle(settings.playerId, profileHandle) ? (
            <Button icon={<UserRound size={16} />} onClick={() => onNavigate({ name: "profile", handle: settings.playerId.trim() })} variant="ghost">
              我的资料
            </Button>
          ) : null}
          <Button icon={<Home size={16} />} onClick={() => onNavigate({ name: "lobby" })} variant="ghost">
            大厅
          </Button>
        </div>
      </section>

      {state.error ? (
        <section className="audit-banner profile-error-banner" data-profile-error>
          <strong>资料加载失败</strong>
          <span>{state.error}</span>
          <StatusPill tone="bad">错误</StatusPill>
        </section>
      ) : null}

      <section className="status-grid profile-stats-grid">
        <article data-profile-total-matches={totalMatches}>
          <span className="eyebrow">总场次</span>
          <h2>{state.loading ? "..." : totalMatches}</h2>
          <p>{state.checkedAt ? `更新于 ${state.checkedAt}` : "等待服务端资料。"}</p>
        </article>
        <article data-profile-wins={wins}>
          <span className="eyebrow">胜场</span>
          <h2>{state.loading ? "..." : wins}</h2>
          <p>服务端终局 winner 汇总。</p>
        </article>
        <article data-profile-losses={losses}>
          <span className="eyebrow">负场</span>
          <h2>{state.loading ? "..." : losses}</h2>
          <p>公开记录中的非胜者场次。</p>
        </article>
        <article data-profile-win-rate={winRate}>
          <span className="eyebrow">胜率</span>
          <h2>{state.loading ? "..." : formatPercent(winRate)}</h2>
          <p>{playerRank ? `排行榜第 ${playerRank}` : "暂未进入前十。"}</p>
        </article>
      </section>

      <div className="profile-content-grid">
        <section className="profile-match-panel" data-profile-match-count={state.matches.length} data-profile-match-history>
          <header>
            <div>
              <span className="eyebrow">最近对局</span>
              <h2>公开终局</h2>
            </div>
            <StatusPill tone={state.matches.length > 0 ? "info" : "neutral"}>{state.matches.length} 场</StatusPill>
          </header>
          <div className="profile-match-list">
            {state.matches.map((match) => {
              const participant = findParticipant(match, profileHandle);
              const opponent = match.players.find((player) => !sameHandle(player.playerId, profileHandle));
              const outcome = participant ? (participant.won ? "win" : "loss") : "unknown";
              return (
                <article
                  className={`profile-match-row is-${outcome}`}
                  data-profile-match-outcome={outcome}
                  data-profile-match-room-id={match.roomId}
                  data-profile-match-row
                  data-profile-match-winner={match.winnerPlayerId}
                  key={`${match.roomId}-${match.finishedAt}`}
                >
                  <div className="profile-match-main">
                    <strong>{match.roomId}</strong>
                    <span>{formatDateTime(match.finishedAt)}</span>
                  </div>
                  <div className="profile-match-scoreline">
                    <StatusPill tone={outcome === "win" ? "good" : outcome === "loss" ? "bad" : "neutral"}>{outcomeLabel(outcome)}</StatusPill>
                    <span>{scoreLine(participant, opponent)}</span>
                  </div>
                  <div className="profile-match-participants">
                    {match.players.map((player) => (
                      <span data-profile-match-player={player.playerId} key={`${match.roomId}-${player.playerId}-${player.seat}`}>
                        {player.playerId} · {player.seat} · {player.score} 分
                      </span>
                    ))}
                  </div>
                  <Button icon={<Swords size={16} />} onClick={() => onNavigate({ name: "result", matchId: match.roomId })} variant="secondary">
                    结果
                  </Button>
                </article>
              );
            })}
            {state.matches.length === 0 ? <p className="empty-hint">暂无公开终局记录。</p> : null}
          </div>
        </section>

        <section className="profile-leaderboard-panel" data-profile-leaderboard>
          <header>
            <div>
              <span className="eyebrow">排行榜</span>
              <h2>前十玩家</h2>
            </div>
            <Trophy size={20} aria-hidden="true" />
          </header>
          <div className="profile-leaderboard-list">
            {state.leaderboard.map((entry) => (
              <article
                className={sameHandle(entry.handle, profileHandle) ? "profile-leaderboard-row is-current" : "profile-leaderboard-row"}
                data-profile-leaderboard-handle={entry.handle}
                data-profile-leaderboard-row
                data-profile-leaderboard-rank={entry.rank}
                key={`${entry.rank}-${entry.handle}`}
              >
                <div>
                  <strong>#{entry.rank}</strong>
                  <span>{entry.handle}</span>
                </div>
                <span>{entry.wins}/{entry.totalMatches} · {formatPercent(entry.winRate)}</span>
                <Button icon={<ChevronRight size={16} />} onClick={() => onNavigate({ name: "profile", handle: entry.handle })} variant="ghost">
                  资料
                </Button>
              </article>
            ))}
            {state.leaderboard.length === 0 ? <p className="empty-hint">暂无排行榜记录。</p> : null}
          </div>
        </section>
      </div>
    </div>
  );
}

function findParticipant(match: PlayerMatchDto, handle: string): PlayerMatchParticipantDto | undefined {
  return match.players.find((player) => sameHandle(player.playerId, handle));
}

function sameHandle(left: string, right: string): boolean {
  return left.trim().toLowerCase() === right.trim().toLowerCase();
}

function formatPercent(value: number): string {
  if (!Number.isFinite(value)) {
    return "0%";
  }

  const percent = value > 1 ? value : value * 100;
  return `${Math.round(percent)}%`;
}

function formatDateTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString("zh-CN", {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function outcomeLabel(outcome: "loss" | "unknown" | "win"): string {
  switch (outcome) {
    case "win":
      return "胜利";
    case "loss":
      return "失败";
    default:
      return "记录";
  }
}

function scoreLine(participant: PlayerMatchParticipantDto | undefined, opponent: PlayerMatchParticipantDto | undefined): string {
  if (!participant) {
    return "未列入双方记录";
  }

  if (!opponent) {
    return `${participant.score} 分`;
  }

  return `${participant.score} - ${opponent.score}`;
}
