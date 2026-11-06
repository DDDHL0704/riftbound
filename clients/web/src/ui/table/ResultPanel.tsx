import { motion } from "motion/react";
import { client, type ClientState } from "../../net/client";
import type { MatchSeries, SnapshotPlayer } from "../../game/types";
import { Confetti } from "../Ambient";

/** The end-of-game screen: result, score (and series score), what each side did, and the way out. */
export function ResultPanel({
  winner,
  meId,
  me,
  opp,
  series,
  tally,
  turns,
  matchContinues,
}: {
  winner: string;
  meId: string;
  me: SnapshotPlayer;
  opp?: SnapshotPlayer;
  series?: MatchSeries | null;
  tally: ClientState["tally"];
  turns: number;
  matchContinues: boolean;
}) {
  return (
    <motion.div className={`result ${winner === meId ? "result--win" : "result--loss"}`} role="status" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
      <span className="result__rays" aria-hidden />
      {winner === meId && <Confetti />}
      <motion.p className="result__title" initial={{ scale: 1.4, opacity: 0 }} animate={{ scale: 1, opacity: 1 }} transition={{ type: "spring", stiffness: 260, damping: 18 }}>
        {winner === meId ? "胜利" : "落败"}
      </motion.p>
      {series && series.bestOf > 1 ? (
        <>
          <p className="result__score">
            {series.wins[meId] ?? 0} : {opp ? (series.wins[opp.id] ?? 0) : 0}
          </p>
          <p className="result__series">
            三局两胜 · 第 {series.gameNumber} 局 {me.score} : {opp?.score ?? 0}
          </p>
        </>
      ) : (
        <p className="result__score">
          {me.score} : {opp?.score ?? 0}
        </p>
      )}
      <ResultStats tally={tally} meId={meId} oppId={opp?.id} turns={turns} />
      {matchContinues ? (
        <p className="muted">等待下一局…</p>
      ) : (
        <button className="btn btn--primary" onClick={() => client.leaveRoom()}>
          返回大厅
        </button>
      )}
    </motion.div>
  );
}

const STAT_ROWS: [string, string][] = [
  ["CARD_PLAYED", "打出卡牌"],
  ["BATTLEFIELD_CONQUERED", "征服战场"],
  ["BATTLEFIELD_HELD", "据守战场"],
  ["CARD_DRAWN", "抽牌"],
];

/** What each side did this match, as far as this tab saw it (a reload starts the count over). */
function ResultStats({ tally, meId, oppId, turns }: { tally: ClientState["tally"]; meId: string; oppId?: string; turns: number }) {
  const rows = STAT_ROWS.filter(([kind]) => tally[meId]?.[kind] || (oppId && tally[oppId]?.[kind]));
  return (
    <div className="result__stats">
      <p className="muted">共 {turns} 回合</p>
      {rows.length > 0 && (
        <table>
          <thead>
            <tr>
              <th />
              <th>你</th>
              <th>对手</th>
            </tr>
          </thead>
          <tbody>
            {rows.map(([kind, label]) => (
              <tr key={kind}>
                <th>{label}</th>
                <td>{tally[meId]?.[kind] ?? 0}</td>
                <td>{(oppId && tally[oppId]?.[kind]) ?? 0}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
