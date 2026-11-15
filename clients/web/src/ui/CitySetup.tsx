import { useMemo, useState } from "react";
import { client } from "../net/client";
import { cardInfo, cardName, COLOR_HEX, useCards } from "../game/cards";
import { citySplit, type OpeningRule } from "../game/opening";
import type { OpeningSetup, PreconstructedDeck } from "../game/types";
import { DeckPreview } from "./Lobby";

/** What the second player gets under each test mode, said once on their side of the screen. */
const COMPENSATION: Partial<Record<OpeningRule, string>> = {
  S5_CITY_MODE1: "第一次抽牌阶段额外抽一张",
  S5_CITY_MODE2: "第一次抽牌前查看牌库顶一张，可以回收",
  S5_CITY_MODE3: "以 1 分开始",
};

/** Removes one copy of each listed card from `pool`, keeping the rest in order. */
function without(pool: string[], take: string[]) {
  const left = [...pool];
  for (const no of take) {
    const i = left.indexOf(no);
    if (i >= 0) left.splice(i, 1);
  }
  return left;
}

function grouped(cards: string[]) {
  const counts = new Map<string, number>();
  for (const no of cards) counts.set(no, (counts.get(no) ?? 0) + 1);
  return [...counts]
    .map(([no, n]) => ({ no, n, info: cardInfo(no) }))
    .sort((a, b) => (a.info?.cost ?? 99) - (b.info?.cost ?? 99) || (a.info?.name ?? a.no).localeCompare(b.info?.name ?? b.no, "zh"));
}

type Props = {
  deck: PreconstructedDeck;
  setup: OpeningSetup;
  me: string;
  rule: OpeningRule;
  ready: boolean;
  canSubmit: boolean;
  canReady: boolean;
  pending: boolean;
};

/*
 * City-challenge sideboarding (CN tournament 403.3–4): play order and each side's random battlefield are
 * settled, every player sees only their own field, and may swap main and sideboard cards one for one
 * before confirming. The opponent's field stays hidden until both confirm.
 */
export function CitySetup({ deck, setup, me, rule, ready, canSubmit, canReady, pending }: Props) {
  useCards();
  const pool = useMemo(() => [...citySplit(deck).mainDeck, ...citySplit(deck).sideboard], [deck]);
  const registered = setup.sideboard ?? citySplit(deck).sideboard;
  const [side, setSide] = useState<string[]>(registered);
  const [out, setOut] = useState<string | null>(null);
  const main = useMemo(() => without(pool, side), [pool, side]);
  const changed = [...side].sort().join() !== [...registered].sort().join();
  const second = setup.secondPlayerId === me;
  const field = setup.ownBattlefieldCardNo;

  const swapIn = (no: string) => {
    if (!out) return;
    setSide((cur) => [...without(cur, [no]), out]);
    setOut(null);
  };

  const confirm = async () => {
    if (changed && canSubmit) {
      const receipt = await client.submit({
        cmdType: "SUBMIT_DECK",
        legendCardNo: deck.legendCardNo,
        championCardNo: deck.championCardNo,
        mainDeck: main,
        sideboard: side,
        runeDeck: deck.runeDeck,
        battlefields: deck.battlefields,
        ...(deck.additionalLegendCardNos?.length ? { additionalLegendCardNos: deck.additionalLegendCardNos } : {}),
      });
      if (!receipt?.accepted) return;
    }
    await client.submit({ cmdType: "READY" });
  };

  const list = (title: string, cards: string[], onPick: (no: string) => void, picked?: string | null, enabled = true) => (
    <div className="dp__section city__list">
      <h4>
        {title} <span className="muted">{cards.length}</span>
      </h4>
      <ul>
        {grouped(cards).map((r) => (
          <li key={r.no}>
            <button className={`city__card${picked === r.no ? " city__card--on" : ""}`} disabled={!enabled || ready} onClick={() => onPick(r.no)}>
              <span className="dp__cost">{r.info?.cost ?? "–"}</span>
              <span className="dp__dots">
                {(r.info?.colors ?? []).map((c) => (
                  <i key={c} style={{ background: COLOR_HEX[c] ?? COLOR_HEX.colorless }} />
                ))}
              </span>
              <span className="dp__name">{r.info?.name ?? r.no}</span>
              <span className="dp__count">×{r.n}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );

  return (
    <section className="setup__body">
      <div className="setup__pick city">
        <h2>{second ? "后手" : "先手"}</h2>
        {second && COMPENSATION[rule] && <p className="muted">{COMPENSATION[rule]}</p>}
        {field && (
          <figure className="city__field">
            {cardInfo(field)?.image ? <div className="city__art"><img src={cardInfo(field)!.image} alt={cardName(field)} /></div> : cardName(field)}
          </figure>
        )}
        {side.length > 0 && (
          <div className="city__swap">
            {list("主牌堆", main, (no) => setOut(out === no ? null : no), out)}
            {list("备牌", side, swapIn, null, !!out)}
          </div>
        )}
        <div className="setup__actions">
          {canReady && !ready ? (
            <button className="btn btn--primary" disabled={pending} onClick={() => void confirm()}>
              准备开战
            </button>
          ) : (
            <p className="muted">等待对手确认</p>
          )}
          {changed && !ready && (
            <button className="btn btn--ghost" onClick={() => setSide(registered)}>
              还原
            </button>
          )}
        </div>
      </div>
      <DeckPreview deck={{ ...deck, mainDeck: main }} />
    </section>
  );
}
