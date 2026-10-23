import { useEffect, useMemo, useRef, useState, type CSSProperties, type MouseEvent, type ReactNode } from "react";
import { client, readableError, type ClientState } from "../net/client";
import {
  buildCommand,
  choiceObjectIds,
  objectActions,
  anyPowerFrom,
  isRecycle,
  powerFrom,
  type ActionStep,
  type ObjectAction,
  type Selection,
} from "../game/actions";
import { cardInfo, cardName, COLOR_HEX, paymentChoiceLabel } from "../game/cards";
import type { Choice, GameObject, Prompt, Snapshot, SnapshotPlayer, TableBattlefield } from "../game/types";
import { Card, type CardMark } from "./Card";
import { ChainArrows, Dock, EventFx } from "./Overlay";
import { Rail } from "./Rail";

/** `group` holds extra units moving together with the source (one destination, CN 144.3). */
type Pick = {
  action: ObjectAction;
  sel: Selection;
  step: number;
  group: string[];
  /** Total recycled power the server's cost quote says this play needs (target taxes included). */
  powerNeed?: number;
};

const GROUP = "__unit:";

/** Crowded rows overlap their cards instead of wrapping, so the table never grows past the viewport. */
function crowd(count: number, roomy: number, cardWidth: number, available: number): CSSProperties {
  if (count <= roomy) return {};
  const overlap = Math.min(cardWidth * 0.7, (count * (cardWidth + 8) - available) / Math.max(count - 1, 1));
  return { ["--overlap" as string]: `${-Math.max(0, overlap)}px` };
}
export type Hover = { cardNo: string; object?: GameObject } | null;

export function Table({ state, snapshot, prompt }: { state: ClientState; snapshot: Snapshot; prompt: Prompt | null }) {
  const meId = state.handle;
  const me = snapshot.players[meId];
  const opp = Object.values(snapshot.players).find((p) => p.id !== meId);
  const objects = useMemo(() => {
    const all: Record<string, GameObject> = {};
    for (const p of Object.values(snapshot.players)) Object.assign(all, p.objects);
    return all;
  }, [snapshot]);

  const actions = useMemo(() => objectActions(prompt), [prompt]);
  const promptType = prompt?.view?.type ?? "WAIT";
  const [menuFor, setMenuFor] = useState<string | null>(null);
  const [pick, setPick] = useState<Pick | null>(null);
  const [mulligan, setMulligan] = useState<string[]>([]);
  const [hover, setHover] = useState<Hover>(null);
  const [banner, setBanner] = useState<string | null>(null);
  const [zoneView, setZoneView] = useState<ZoneView>(null);

  // Any new prompt invalidates half-built selections.
  useEffect(() => {
    setMenuFor(null);
    setPick(null);
  }, [prompt?.promptId]);

  useEffect(() => {
    if (promptType !== "MULLIGAN") setMulligan([]);
  }, [promptType]);

  // The one orchestrated moment: announce whose turn just began.
  const lastTurn = useRef<string>("");
  useEffect(() => {
    const turnPlayer = snapshot.timing.turnPlayerId || snapshot.activePlayerId;
    const key = `${snapshot.turnNumber}:${turnPlayer}`;
    if (snapshot.timing.phase === "MULLIGAN" || lastTurn.current === key) return;
    const first = lastTurn.current === "";
    lastTurn.current = key;
    if (first && snapshot.turnNumber > 1) return;
    setBanner(turnPlayer === meId ? "你的回合" : "对手回合");
    const t = setTimeout(() => setBanner(null), 1400);
    return () => clearTimeout(t);
  }, [snapshot.turnNumber, snapshot.activePlayerId, snapshot.timing.turnPlayerId, snapshot.timing.phase, meId]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setPick(null);
        setMenuFor(null);
        setZoneView(null);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  const currentStep: ActionStep | undefined = pick ? pick.action.steps[pick.step] : undefined;
  const moveOf = (id: string) => actions.get(id)?.find((a) => a.cmdType === "MOVE_UNIT");
  const grouping = pick?.action.cmdType === "MOVE_UNIT" && currentStep?.role === "destination";
  // While moving, every other unit that can move may join; destinations narrow to shared ones.
  const destinationsOf = (id: string): string[] => (moveOf(id)?.requirement?.destinationChoices ?? []).map((d: { id: string }) => d.id);
  const stepChoices = useMemo(() => {
    const choices = currentStep?.choices ?? [];
    if (!grouping || !pick?.group.length) return choices;
    return choices.filter((c) => pick.group.every((id) => destinationsOf(id).includes(c.id)));
  }, [currentStep, grouping, pick]); // eslint-disable-line react-hooks/exhaustive-deps
  // Units already in the group can always leave it; others may join only if a shared destination remains.
  const peers = useMemo(() => {
    if (!grouping) return [];
    const shared = new Set(stepChoices.map((c) => c.id));
    return [...actions.keys()].filter(
      (id) => id !== pick!.action.sourceObjectId && moveOf(id) && (pick!.group.includes(id) || destinationsOf(id).some((d) => shared.has(d))),
    );
  }, [grouping, actions, pick, stepChoices]); // eslint-disable-line react-hooks/exhaustive-deps
  const legal = useMemo(() => {
    const map = new Map<string, string>();
    for (const choice of stepChoices) {
      if (pick?.sel.targets.includes(choice.id)) continue;
      for (const id of choiceObjectIds(choice)) map.set(id, choice.id);
    }
    for (const id of peers) map.set(id, GROUP + id);
    return map;
  }, [stepChoices, peers, pick]);

  const submitPick = (p: Pick) => {
    setPick(null);
    if (p.action.cmdType === "MOVE_UNIT" && p.group.length && p.sel.destination) {
      // A group move names its units and destination; each unit keeps its own origin.
      void client.submit({
        cmdType: "MOVE_UNIT",
        sourceObjectId: p.action.sourceObjectId,
        sourceObjectIds: [p.action.sourceObjectId, ...p.group],
        destination: p.sel.destination,
      });
      return;
    }
    const cmd = buildCommand(p.action, p.sel);
    if (p.action.cmdType !== "PLAY_CARD") {
      void client.submit(cmd);
      return;
    }
    // Targets can add costs (e.g. spellshield), so quote the exact play before sending it.
    void client.previewPlayCard(cmd).then((quote) => {
      if (!quote || (quote.isValid && quote.canPay)) {
        void client.submit(cmd);
        return;
      }
      const missingPower = quote.cost?.missingPower ?? 0;
      const recycles = p.action.optionalCosts.filter((c) => isRecycle(c.id));
      const haveRecycled = anyPowerFrom(p.action.requirement, p.sel.optionalCosts);
      if (quote.isValid && missingPower > 0 && !quote.cost?.missingMana && recycles.length) {
        setPick({ ...p, step: p.action.steps.length, powerNeed: haveRecycled + missingPower });
        return;
      }
      const missing = [
        quote.cost?.missingMana ? `${quote.cost.missingMana} 点法力` : "",
        missingPower ? `${missingPower} 点符能` : "",
        quote.cost?.missingExperience ? `${quote.cost.missingExperience} 点经验` : "",
      ].filter(Boolean);
      const hint = missingPower ? "可以先点符文选择「回收」获得符能。" : quote.cost?.missingMana ? "可以先横置符文获得法力。" : "";
      client.toast(missing.length ? `费用不足：还差 ${missing.join("、")}。${hint}` : readableError(quote.errorCode, quote.message));
    });
  };

  const advance = (p: Pick) => {
    let next = p;
    // Optional steps with nothing chosen yet still wait for the player; only finish when past the end.
    if (next.step >= next.action.steps.length) {
      if (next.action.optionalCosts.length) setPick(next);
      else submitPick(next);
      return;
    }
    setPick(next);
  };

  const choose = (choiceId: string) => {
    if (!pick || !currentStep) return;
    if (choiceId.startsWith(GROUP)) {
      const id = choiceId.slice(GROUP.length);
      setPick({ ...pick, group: pick.group.includes(id) ? pick.group.filter((x) => x !== id) : [...pick.group, id] });
      return;
    }
    const sel: Selection = { ...pick.sel, targets: [...pick.sel.targets], movements: [...(pick.sel.movements ?? [])] };
    if (currentStep.role === "destination") sel.destination = choiceId;
    else if (currentStep.role === "move") sel.movements!.push(choiceId);
    else if (currentStep.role === "cost") sel.optionalCosts = [choiceId];
    else sel.targets.push(choiceId);
    advance({ ...pick, sel, step: pick.step + 1 });
  };

  const skipStep = () => {
    if (!pick || !currentStep || currentStep.required) return;
    advance({ ...pick, step: pick.step + 1 });
  };

  const start = (action: ObjectAction) => {
    setMenuFor(null);
    const p: Pick = { action, sel: { targets: [], optionalCosts: [...action.presetCosts] }, step: 0, group: [] };
    if (action.steps.length === 0 && action.optionalCosts.length === 0) submitPick(p);
    else setPick(p);
  };

  const onObject = (id: string) => (e: MouseEvent) => {
    e.stopPropagation();
    if (promptType === "MULLIGAN") {
      if (!me.zones.hand.includes(id)) return;
      const max = prompt?.view?.maxSelection ?? 2;
      setMulligan((cur) => (cur.includes(id) ? cur.filter((x) => x !== id) : cur.length < max ? [...cur, id] : cur));
      return;
    }
    if (pick) {
      const choiceId = legal.get(id);
      if (choiceId) choose(choiceId);
      return;
    }
    const list = actions.get(id);
    setMenuFor(list?.length ? (menuFor === id ? null : id) : null);
  };

  const onZone = (zoneIds: string[]) => (e: MouseEvent) => {
    if (!pick) {
      setMenuFor(null);
      return;
    }
    e.stopPropagation();
    for (const z of zoneIds) {
      const choiceId = legal.get(z);
      if (choiceId) return choose(choiceId);
    }
  };

  const marksFor = (id: string): CardMark[] => {
    const o = objects[id];
    const marks: CardMark[] = [];
    const inGroup = Boolean(pick?.group.includes(id));
    if (menuFor === id || pick?.action.sourceObjectId === id || mulligan.includes(id) || pick?.sel.targets.includes(id) || inGroup)
      marks.push("selected");
    if (pick && legal.has(id) && !inGroup) marks.push("legal");
    if (!pick && actions.has(id)) marks.push("playable");
    if (o?.isAttacking) marks.push("attacking");
    if (o?.isDefending) marks.push("defending");
    return marks;
  };

  const hoverCard = (cardNo: string | null, object?: GameObject) => setHover(cardNo ? { cardNo, object } : null);

  const renderObject = (id: string, size: "board" | "hand" | "mini" = "board") => {
    const o = objects[id];
    const attachments = Object.values(objects).filter((x) => x.attachedToObjectId === id);
    const clickable = promptType === "MULLIGAN" ? me.zones.hand.includes(id) : pick ? legal.has(id) : actions.has(id);
    return (
      <div className="obj" key={id} data-oid={id}>
        <Card
          object={o}
          cardNo={o?.cardNo}
          size={size}
          marks={marksFor(id)}
          onClick={clickable ? onObject(id) : undefined}
          onHover={hoverCard}
        />
        {attachments.length > 0 && (
          <div className="obj__attachments">
            {attachments.map((a) => (
              <span key={a.objectId} className="obj__attachment" onMouseEnter={() => hoverCard(a.cardNo ?? null, a)} onMouseLeave={() => hoverCard(null)}>
                {cardName(a.cardNo)}
              </span>
            ))}
          </div>
        )}
        {menuFor === id && <ActionMenu actions={actions.get(id) ?? []} onPick={start} onClose={() => setMenuFor(null)} />}
      </div>
    );
  };

  const battlefields: TableBattlefield[] = snapshot.table?.battlefields ?? [];
  const zoneIds = zoneView ? snapshot.players[zoneView.playerId]?.zones[zoneView.zone] ?? [] : [];
  const winner = snapshot.timing.winnerPlayerId;

  return (
    <div className="table" onClick={() => setMenuFor(null)}>
      <ScoreSpine me={me} opp={opp} goal={snapshot.timing.winningScore || 8} />

      <div className="board">
        {opp && <OpponentHand player={opp} />}
        {opp && <Side player={opp} side="opp" render={renderObject} onZone={onZone} legal={legal} onOpenZone={setZoneView} />}

        <div className="fields">
          {battlefields.map((bf) => (
            <Battlefield
              key={bf.battlefieldObjectId}
              bf={bf}
              meId={meId}
              oppId={opp?.id}
              objects={objects}
              render={renderObject}
              onZone={onZone}
              legal={legal}
              hover={hoverCard}
              related={prompt?.view?.relatedBattlefieldId === bf.battlefieldObjectId}
            />
          ))}
        </div>

        <Side player={me} side="me" render={renderObject} onZone={onZone} legal={legal} onOpenZone={setZoneView} />

        <div className={`hand${promptType === "MULLIGAN" ? " hand--mulligan" : ""}`} aria-label="你的手牌">
          {me.zones.hand.map((id, i) => {
            const n = me.zones.hand.length;
            const spread = Math.min(5, 40 / Math.max(n, 1));
            const angle = (i - (n - 1) / 2) * spread;
            return (
              <div
                className="hand__card"
                key={id}
                style={{ transform: `rotate(${angle}deg) translateY(${Math.abs(angle) * 1.6}px)`, ["--fan" as string]: `${angle}deg` }}
              >
                {renderObject(id, "hand")}
              </div>
            );
          })}
        </div>

        {!winner && <Dock prompt={prompt} pending={state.pending} />}

        {pick && (
          <PickBar
            pick={pick}
            step={currentStep}
            choices={stepChoices}
            peers={peers.length}
            extraPower={(dest) =>
              Number(pick.action.candidate.metadata?.powerPerExtraUnitByDestination?.[dest] ?? 0) * pick.group.length
            }
            objects={objects}
            onChoose={choose}
            onSkip={skipStep}
            onToggleCost={(id) => {
              const on = pick.sel.optionalCosts.includes(id);
              let next = on ? pick.sel.optionalCosts.filter((c) => c !== id) : [...pick.sel.optionalCosts, id];
              // Without an extra cost ticked, recycles may only cover the missing printed power;
              // the server rejects payment actions that aren't needed.
              if (!next.some((c) => !isRecycle(c))) {
                const kept: string[] = [];
                for (const c of next) {
                  if (!isRecycle(c)) continue;
                  if (providedPower(pick, kept) < neededPower(pick)) kept.push(c);
                }
                next = kept;
              }
              setPick({ ...pick, sel: { ...pick.sel, optionalCosts: next } });
            }}
            onConfirm={() => submitPick(pick)}
            onCancel={() => setPick(null)}
          />
        )}

        {zoneView && (
          <div className="zoneview" role="dialog" aria-label={zoneView.title} onClick={() => setZoneView(null)}>
            <div className="zoneview__box" onClick={(e) => e.stopPropagation()}>
              <header className="zoneview__head">
                <h3>
                  {zoneView.title}（{zoneIds.length}）
                </h3>
                <button className="btn btn--ghost btn--small" onClick={() => setZoneView(null)}>
                  关闭
                </button>
              </header>
              <div className="zoneview__cards">
                {[...zoneIds].reverse().map((id) => (
                  <Card key={id} object={objects[id]} cardNo={objects[id]?.cardNo} size="board" onHover={hoverCard} />
                ))}
              </div>
              <p className="muted small">最新进入的牌排在最前。</p>
            </div>
          </div>
        )}

        {banner && !winner && (
          <div className={`banner ${banner === "你的回合" ? "banner--me" : "banner--opp"}`} role="status">
            {banner}
          </div>
        )}
        {winner && (
          <div className={`result ${winner === meId ? "result--win" : "result--loss"}`} role="status">
            <p className="result__title">{winner === meId ? "胜利" : "落败"}</p>
            <p className="result__score">
              {me.score} : {opp?.score ?? 0}
            </p>
            <button className="btn btn--primary" onClick={() => client.leaveRoom()}>
              返回大厅
            </button>
          </div>
        )}
      </div>

      <Rail
        state={state}
        snapshot={snapshot}
        prompt={prompt}
        objects={objects}
        mulligan={mulligan}
        onHover={hoverCard}
        preview={hover}
      />

      <ChainArrows snapshot={snapshot} meId={meId} />
      <EventFx meId={meId} />
      {state.connectionNote && <div className="conn">{state.connectionNote}</div>}
    </div>
  );
}

function ScoreSpine({ me, opp, goal }: { me: SnapshotPlayer; opp?: SnapshotPlayer; goal: number }) {
  const notches = Array.from({ length: goal }, (_, i) => i + 1);
  return (
    <div className="spine" aria-label={`比分：你 ${me.score}，对手 ${opp?.score ?? 0}，先到 ${goal} 分获胜`}>
      <div className="spine__score spine__score--opp" data-oid={`score:${opp?.id}`}>
        {opp?.score ?? 0}
      </div>
      <div className="spine__track">
        <div className="spine__half spine__half--opp">
          {notches.map((n) => (
            <span key={n} className={`spine__notch${(opp?.score ?? 0) >= n ? " is-on" : ""}`} />
          ))}
        </div>
        <div className="spine__goal" title={`${goal} 分获胜`}>
          {goal}
        </div>
        <div className="spine__half spine__half--me">
          {notches.map((n) => (
            <span key={n} className={`spine__notch${me.score >= n ? " is-on" : ""}`} />
          ))}
        </div>
      </div>
      <div className="spine__score spine__score--me" data-oid={`score:${me.id}`}>
        {me.score}
      </div>
    </div>
  );
}

function OpponentHand({ player }: { player: SnapshotPlayer }) {
  const n = player.zones.handHidden + player.zones.hand.length;
  return (
    <div className="opp-hand" aria-label={`对手手牌 ${n} 张`}>
      {Array.from({ length: n }, (_, i) => (
        <span key={i} className="opp-hand__card" style={{ transform: `rotate(${(i - (n - 1) / 2) * -3}deg)` }} />
      ))}
    </div>
  );
}

type SideProps = {
  player: SnapshotPlayer;
  side: "me" | "opp";
  render: (id: string, size?: "board" | "hand" | "mini") => ReactNode;
  onZone: (ids: string[]) => (e: MouseEvent) => void;
  legal: Map<string, string>;
  onOpenZone: (zone: ZoneView) => void;
};

/** Which public pile is open; its cards are read from the live snapshot. */
type ZoneView = { title: string; playerId: string; zone: "graveyard" | "banished" } | null;

function Side({ player, side, render, onZone, legal, onOpenZone }: SideProps) {
  const z = player.zones;
  const units = z.baseCards.filter((id) => !player.objects[id]?.attachedToObjectId);
  const pool = player.runePool;
  const baseLegal = legal.has(`BASE:${player.id}`) || (legal.has("BASE") && side === "me");
  const traits = Object.entries(pool.powerByTrait || {}).filter(([, v]) => v > 0);
  return (
    <section className={`side side--${side}`} aria-label={side === "me" ? "你的区域" : "对手区域"}>
      <div className="side__heroes">
        {[...z.legendZone, ...z.championZone].map((id) => render(id, "mini"))}
      </div>
      <div
        className={`side__base${baseLegal ? " is-legal" : ""}`}
        onClick={onZone(["BASE", `BASE:${player.id}`])}
        aria-label="基地"
      >
        <span className="zone-label">{side === "me" ? "我的基地" : "对手基地"}</span>
        <div className="side__units crowd" style={crowd(units.length, 6, 62, 470)}>
          {units.map((id) => render(id))}
        </div>
      </div>
      <div className="side__runes" aria-label="符文">
        <div className="runes crowd" style={crowd(z.baseRunes.length, 5, 42, 250)}>
          {z.baseRunes.map((id) => render(id, "mini"))}
        </div>
        <div className="pool" title="符文池：本回合可用的法力与符能">
          <span className="pool__mana">
            <b>{pool.mana}</b> 法力
          </span>
          <span className="pool__power">
            <b>{pool.power}</b> 符能
            {traits.map(([t, v]) => (
              <i key={t} className="pool__trait" style={{ background: COLOR_HEX[t] ?? COLOR_HEX.colorless }} title={`${t} ${v}`} />
            ))}
          </span>
        </div>
      </div>
      <div className="piles">
        <Pile label="牌库" count={z.mainDeckCount} />
        <Pile label="符文库" count={z.runeDeckCount} rune />
        <div className="piles__meta">
          <button
            className="piles__link"
            disabled={!z.graveyard.length}
            onClick={(e) => {
              e.stopPropagation();
              onOpenZone({ title: `${side === "me" ? "我的" : "对手的"}废牌堆`, playerId: player.id, zone: "graveyard" });
            }}
          >
            废牌 {z.graveyard.length}
          </button>
          {z.banished.length > 0 && (
            <button
              className="piles__link"
              onClick={(e) => {
                e.stopPropagation();
                onOpenZone({ title: `${side === "me" ? "我的" : "对手的"}放逐区`, playerId: player.id, zone: "banished" });
              }}
            >
              放逐 {z.banished.length}
            </button>
          )}
          {player.experience > 0 && <span>经验 {player.experience}</span>}
        </div>
      </div>
    </section>
  );
}

function Pile({ label, count, rune }: { label: string; count: number; rune?: boolean }) {
  return (
    <div className={`pile${rune ? " pile--rune" : ""}${count === 0 ? " pile--empty" : ""}`} title={`${label} ${count} 张`}>
      <span className="pile__stack" aria-hidden />
      <span className="pile__count">{count}</span>
      <span className="pile__label">{label}</span>
    </div>
  );
}

type BattlefieldProps = {
  bf: TableBattlefield;
  meId: string;
  oppId?: string;
  objects: Record<string, GameObject>;
  render: (id: string, size?: "board" | "hand" | "mini") => ReactNode;
  onZone: (ids: string[]) => (e: MouseEvent) => void;
  legal: Map<string, string>;
  hover: (cardNo: string | null, o?: GameObject) => void;
  related: boolean;
};

function Battlefield({ bf, meId, oppId, render, onZone, legal, hover, related }: BattlefieldProps) {
  const info = cardInfo(bf.cardNo);
  const mine = bf.unitsBySide[meId] ?? [];
  const theirs = oppId ? bf.unitsBySide[oppId] ?? [] : [];
  const owner = bf.controllerId === meId ? "me" : bf.controllerId ? "opp" : "none";
  const isLegal = legal.has(bf.battlefieldObjectId) || legal.has(`BATTLEFIELD:${bf.battlefieldObjectId}`);
  const status = bf.contested ? "争夺中" : owner === "me" ? "你控制" : owner === "opp" ? "对手控制" : "无人控制";
  const hidden = bf.faceDownStandbyCount + bf.hiddenStandbyCount;
  return (
    <section
      className={`field field--${owner}${bf.contested ? " field--contested" : ""}${isLegal ? " is-legal" : ""}${related ? " field--focus" : ""}`}
      onClick={onZone([bf.battlefieldObjectId, `BATTLEFIELD:${bf.battlefieldObjectId}`])}
      data-oid={bf.battlefieldObjectId}
      aria-label={`战场 ${info?.name ?? ""}，${status}`}
    >
      <div className="field__row field__row--opp crowd" style={crowd(theirs.length, 5, 62, 460)}>
        {theirs.map((id) => render(id))}
      </div>
      <div className="field__mid">
        <div className="field__card" onMouseEnter={() => hover(bf.cardNo ?? null)} onMouseLeave={() => hover(null)}>
          {info?.image ? <img src={info.image} alt="" className="field__art" /> : <span className="field__fallback">{info?.name ?? bf.cardNo}</span>}
        </div>
        <span className="field__status">
          {status}
          {bf.scoredThisTurn && "，本回合已得分"}
          {hidden > 0 && `，${hidden} 张待命`}
        </span>
      </div>
      <div className="field__row field__row--me crowd" style={crowd(mine.length, 5, 62, 460)}>
        {mine.map((id) => render(id))}
      </div>
    </section>
  );
}

function ActionMenu({ actions, onPick, onClose }: { actions: ObjectAction[]; onPick: (a: ObjectAction) => void; onClose: () => void }) {
  return (
    <div className="menu" role="menu" onClick={(e) => e.stopPropagation()}>
      {actions.map((a) => (
        <button key={a.key} role="menuitem" className="menu__item" onClick={() => onPick(a)}>
          <span>{a.label}</span>
          {a.detail && <span className="menu__detail">{a.detail}</span>}
        </button>
      ))}
      <button className="menu__item menu__item--quiet" onClick={onClose}>
        取消
      </button>
    </div>
  );
}

type PickBarProps = {
  pick: Pick;
  step?: ActionStep;
  choices: Choice[];
  peers: number;
  extraPower: (destinationId: string) => number;
  objects: Record<string, GameObject>;
  onChoose: (id: string) => void;
  onSkip: () => void;
  onToggleCost: (id: string) => void;
  onConfirm: () => void;
  onCancel: () => void;
};

function choiceName(id: string, label: string, objects: Record<string, GameObject>) {
  if (id === "BASE") return "基地";
  const tail = id.split(":").pop() ?? id;
  const o = objects[id] ?? objects[tail];
  return o ? cardName(o.cardNo) : label;
}

/** Power the play still needs from recycles: the quote's figure when we have one, else the printed shortfall. */
function neededPower(pick: Pick) {
  return pick.powerNeed ?? pick.action.powerShortfall;
}

function providedPower(pick: Pick, chosen: string[]) {
  return pick.powerNeed != null ? anyPowerFrom(pick.action.requirement, chosen) : powerFrom(pick.action.requirement, chosen);
}

function PickBar({ pick, step, choices, peers, extraPower, objects, onChoose, onSkip, onToggleCost, onConfirm, onCancel }: PickBarProps) {
  const src = objects[pick.action.sourceObjectId];
  return (
    <div className="pickbar" onClick={(e) => e.stopPropagation()} role="dialog" aria-label="选择">
      <p className="pickbar__title">
        <strong>{cardName(src?.cardNo)}</strong>：{pick.action.label}
      </p>
      {step ? (
        <>
          <p className="pickbar__hint">{step.label}。可直接点击场上发光的卡牌或区域。</p>
          {pick.action.cmdType === "MOVE_UNIT" && peers > 0 && (
            <p className="pickbar__hint">
              {pick.group.length
                ? `将一起移动 ${pick.group.length + 1} 个单位；再点单位可加入或取消。`
                : "也可以先点其他发光的单位，让它们一起移动到同一个位置。"}
            </p>
          )}
          <div className="pickbar__choices">
            {choices
              .filter((c) => !pick.sel.targets.includes(c.id))
              .map((c) => (
                <button key={c.id} className="chip" onClick={() => onChoose(c.id)}>
                  {choiceName(c.id, c.label, objects)}
                  {extraPower(c.id) > 0 && <span className="chip__cost">+{extraPower(c.id)} 符能</span>}
                </button>
              ))}
            {choices.length === 0 && <span className="muted small">所选单位没有共同的目的地。</span>}
            {!step.required && (
              <button className="chip chip--quiet" onClick={onSkip}>
                不选
              </button>
            )}
          </div>
        </>
      ) : (
        <>
          {neededPower(pick) > 0 ? (
            <p className={`pickbar__hint${providedPower(pick, pick.sel.optionalCosts) < neededPower(pick) ? " pickbar__hint--warn" : ""}`}>
              还差 {neededPower(pick)} 点符能，需要回收符文支付（已选可提供 {providedPower(pick, pick.sel.optionalCosts)} 点）。
            </p>
          ) : (
            <p className="pickbar__hint">可选附加费用：</p>
          )}
          <div className="pickbar__choices">
            {pick.action.optionalCosts
              .filter((c) => !isRecycle(c.id) || neededPower(pick) > 0 || pick.sel.optionalCosts.some((x) => !isRecycle(x)))
              .map((c) => (
              <label key={c.id} className="chip chip--toggle">
                <input type="checkbox" checked={pick.sel.optionalCosts.includes(c.id)} onChange={() => onToggleCost(c.id)} />
                {paymentChoiceLabel(c.id, c.label, objects)}
              </label>
              ))}
          </div>
          <button
            className="btn btn--primary btn--small"
            onClick={onConfirm}
            disabled={providedPower(pick, pick.sel.optionalCosts) < neededPower(pick)}
          >
            确认{pick.action.label}
          </button>
        </>
      )}
      <button className="pickbar__cancel" onClick={onCancel} aria-label="取消选择">
        取消
      </button>
    </div>
  );
}
