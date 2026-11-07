import { useEffect, useMemo, useState, type MouseEvent, type PointerEvent as ReactPointerEvent } from "react";
import { AnimatePresence, LayoutGroup } from "motion/react";
import { client, readableError, type ClientState } from "../net/client";
import {
  anyPowerFrom,
  buildCommand,
  choiceObjectIds,
  enabledCandidate,
  isRecycle,
  isTap,
  objectActions,
  recyclesByLoss,
  type ActionStep,
  type ObjectAction,
  type Selection,
} from "../game/actions";
import { cardName } from "../game/cards";
import { preloadArt } from "../game/art";
import type { Choice, GameObject, Prompt, Snapshot, TableBattlefield } from "../game/types";
import { Ambient, tiltHandlers } from "./Ambient";
import { Card, HoverPreview, type CardMark, type HoverHandler } from "./Card";
import { ChainArrows, Dock, EventFx, Spotlight } from "./Overlay";
import { Stage, StackShow } from "./Stage";
import { Rail } from "./Rail";
import { Battlefield, OpponentHand, ScoreSpine, Side, type RenderObject, type ZoneView } from "./Zones";
import { ActionMenu } from "./table/ActionMenu";
import { useCardDrag, type Drag } from "./table/drag";
import {
  useAutoPass,
  useAutoPassPreference,
  useCardNameHover,
  useHoverAutoClear,
  useOpponentArrivals,
  useResultSounds,
  useTableHotkeys,
  useTurnBanner,
  useViewportFit,
} from "./table/hooks";
import { MulliganPanel } from "./table/MulliganPanel";
import { applyChoice, EXCLUSIVE_COST_PREFIXES, GROUP, type Pick } from "./table/pick";
import { PickBar } from "./table/PickBar";
import { ResultPanel } from "./table/ResultPanel";
import { RuneBar } from "./table/RuneBar";
import { TurnBanner } from "./table/TurnBanner";
import { ZoneViewer } from "./table/ZoneViewer";

export type Hover = { cardNo: string; object?: GameObject; rect?: DOMRect } | null;

export function Table({ state, snapshot, prompt }: { state: ClientState; snapshot: Snapshot; prompt: Prompt | null }) {
  const meId = state.handle;
  const me = snapshot.players[meId];
  const opp = Object.values(snapshot.players).find((p) => p.id !== meId);
  const objects = useMemo(() => {
    const all: Record<string, GameObject> = {};
    for (const p of Object.values(snapshot.players)) Object.assign(all, p.objects);
    return all;
  }, [snapshot]);

  // Everything visible (including the opponent's revealed cards) is fetched ahead of its first hover.
  useEffect(() => preloadArt(Object.values(objects).map((o) => o.cardNo)), [objects]);

  const actions = useMemo(() => objectActions(prompt), [prompt]);
  const promptType = prompt?.view?.type ?? "WAIT";
  const [menuFor, setMenuFor] = useState<string | null>(null);
  const [pick, setPick] = useState<Pick | null>(null);
  const [mulligan, setMulligan] = useState<string[]>([]);
  const [hover, setHover] = useState<Hover>(null);
  const [zoneView, setZoneView] = useState<ZoneView>(null);
  const [runeSel, setRuneSel] = useState<string[]>([]);
  const [autoPass, toggleAutoPass] = useAutoPassPreference();
  const recent = useOpponentArrivals(snapshot, meId);
  const banner = useTurnBanner(snapshot, meId);

  // Drops act through the latest handlers: auto-pay quotes and prompts change after the drag starts.
  const { drag, dragRef, suppressClick, begin: beginCardDrag } = useCardDrag(
    (d, ids, _x, y) => {
      const handTop = document.querySelector(".hand")?.getBoundingClientRect().top ?? window.innerHeight;
      if (d.kind === "hand") {
        if (y < handTop - 20) void playCard(d.id, ids);
      } else if (ids.some((i) => i === "BASE" || i.startsWith("BATTLEFIELD:"))) {
        moveUnitsTo([d.id], ids);
      }
    },
    () => setHover(null),
  );

  // Any new prompt invalidates half-built selections.
  useEffect(() => {
    setMenuFor(null);
    setPick(null);
    setRuneSel([]);
  }, [prompt?.promptId]);

  useEffect(() => {
    if (promptType !== "MULLIGAN") setMulligan([]);
  }, [promptType]);

  useViewportFit();
  useTableHotkeys(() => {
    setPick(null);
    setMenuFor(null);
    setZoneView(null);
    setRuneSel([]);
  });

  // ---------- resources ----------
  const tapIds = useMemo(() => new Set((enabledCandidate(prompt, "TAP_RUNE")?.sources ?? []).map((s) => s.id)), [prompt]);
  const recycleIds = useMemo(() => new Set((enabledCandidate(prompt, "RECYCLE_RUNE")?.sources ?? []).map((s) => s.id)), [prompt]);
  const isRune = (id: string) => me.zones.baseRunes.includes(id);

  // Mana a play would tap runes for; the server already offers every play tapping can afford.
  const autoTapOf = (id: string): number | null => {
    const plays = actions.get(id)?.filter((a) => a.cmdType === "PLAY_CARD") ?? [];
    return plays.length && plays.every((a) => a.manaShortfall > 0) ? Math.min(...plays.map((a) => a.manaShortfall)) : null;
  };

  // Auto-pass: in a priority or focus window where this player has nothing at all to respond with,
  // pass after a short beat. Any playable card or ability stops it.
  const respondable = [...actions.values()].flat().some((a) => a.cmdType !== "TAP_RUNE" && a.cmdType !== "RECYCLE_RUNE");
  useAutoPass(autoPass, prompt, respondable, Boolean(pick || menuFor));

  const runRuneCommand = async (cmdType: "TAP_RUNE" | "RECYCLE_RUNE", ids: string[]) => {
    if (!ids.length) return null;
    return client.submit({ cmdType, sourceObjectId: ids[0], sourceObjectIds: ids.slice(0, 12) });
  };

  /**
   * A rune can be exhausted for mana and still be recycled for power. When a payment recycles a rune that is still
   * ready, exhaust it first so its mana stays in the pool for later this turn instead of being lost with the rune.
   * The play's own taps shrink by the mana this adds. Returns the selection to send and whether anything was tapped.
   */
  const tapBeforeRecycle = async (sel: Selection): Promise<{ sel: Selection; tapped: boolean }> => {
    const objectOf = (id: string) => id.split(":").pop() ?? id;
    const tappedByPay = new Set(sel.optionalCosts.filter(isTap).map(objectOf));
    const ready = sel.optionalCosts
      .filter(isRecycle)
      .map(objectOf)
      .filter((id) => tapIds.has(id) && !objects[id]?.isExhausted && !tappedByPay.has(id));
    if (!ready.length) return { sel, tapped: false };
    const before = client.state.prompt?.promptId;
    const receipt = await runRuneCommand("TAP_RUNE", ready);
    if (!receipt?.accepted) return { sel, tapped: false };
    for (let i = 0; i < 30 && client.state.prompt?.promptId === before; i++) await new Promise((r) => setTimeout(r, 100));
    const drop = new Set(sel.optionalCosts.filter(isTap).slice(0, ready.length));
    return { sel: { ...sel, optionalCosts: sel.optionalCosts.filter((c) => !drop.has(c)) }, tapped: true };
  };

  // ---------- picking ----------
  const currentStep: ActionStep | undefined = pick ? pick.action.steps[pick.step] : undefined;
  const moveOf = (id: string) => actions.get(id)?.find((a) => a.cmdType === "MOVE_UNIT");
  const grouping = pick?.action.cmdType === "MOVE_UNIT" && currentStep?.role === "destination";
  const destinationsOf = (id: string): string[] => (moveOf(id)?.requirement?.destinationChoices ?? []).map((d: { id: string }) => d.id);
  const stepChoices = useMemo(() => {
    const choices = currentStep?.choices ?? [];
    // Spells that move their targets to one chosen place list each target's legal destinations (#478):
    // the chosen place must suit every moving target, so later targets must still share one with the earlier ones.
    const perTarget: Record<string, Choice[]> | null = pick?.action.requirement?.destinationChoicesByTargetObjectId ?? null;
    if (perTarget && pick && (currentStep?.role === "destination" || currentStep?.role === "target")) {
      const moving = pick.sel.targets.filter((id) => perTarget[id]);
      const shared = (ids: string[]) =>
        ids.reduce<Set<string> | null>((acc, id) => {
          const own = new Set(perTarget[id].map((d) => d.id));
          return acc ? new Set([...acc].filter((d) => own.has(d))) : own;
        }, null);
      if (currentStep.role === "destination") {
        const ok = shared(moving);
        return ok ? choices.filter((c) => ok.has(c.id)) : choices;
      }
      return choices.filter((c) => !perTarget[c.id] || (shared([...moving, c.id])?.size ?? 0) > 0);
    }
    // Older prompts without per-target data: a target's own location is never a legal destination (CN 355.4).
    if (currentStep?.role === "destination" && pick?.action.requirement?.destinationLabel === "移动目的地" && pick.sel.targets.length) {
      const at = pick.sel.targets.map((id) => objects[id]?.location).filter(Boolean);
      return choices.filter((c) =>
        c.id.startsWith("BATTLEFIELD:")
          ? at.every((l) => l!.battlefieldObjectId !== c.id.slice(12))
          : c.id.startsWith("BASE:")
            ? at.every((l) => l!.playerId === c.id.slice(5) && !/BASE/i.test(l!.zone))
            : true,
      );
    }
    if (!grouping || !pick?.group.length) return choices;
    return choices.filter((c) => pick.group.every((id) => destinationsOf(id).includes(c.id)));
  }, [currentStep, grouping, pick, objects]); // eslint-disable-line react-hooks/exhaustive-deps
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
      if (pick?.sel.targets.includes(choice.id) || pick?.sel.optionalCosts.includes(choice.id)) continue;
      for (const id of choiceObjectIds(choice)) map.set(id, choice.id);
    }
    for (const id of peers) map.set(id, GROUP + id);
    return map;
  }, [stepChoices, peers, pick]);

  const submitPick = (p: Pick) => {
    setPick(null);
    if (p.action.cmdType === "MOVE_UNIT" && p.group.length && p.sel.destination) {
      // A group move names its units and destination; each unit keeps its own origin.
      void client.submit(
        {
          cmdType: "MOVE_UNIT",
          sourceObjectId: p.action.sourceObjectId,
          sourceObjectIds: [p.action.sourceObjectId, ...p.group],
          destination: p.sel.destination,
        },
        p.action.promptId,
      );
      return;
    }
    if (p.action.cmdType !== "PLAY_CARD") {
      void (async () => {
        const { sel, tapped } = await tapBeforeRecycle(withOptionPayment(p));
        void client.submit(buildCommand(p.action, sel), tapped ? undefined : p.action.promptId);
      })();
      return;
    }
    void submitPlay(p);
  };

  /** Pays the chosen option's cost from runes in the same command when the pool is short. */
  const withOptionPayment = ({ action, sel }: Pick): Selection => {
    const option = [...action.requiredCosts, ...sel.optionalCosts].find((c) => !isTap(c) && !isRecycle(c));
    const cost = action.requirement?.costsByChoice?.[option ?? ""];
    if (!cost) return sel;
    const taps = action.taps.slice(0, Math.max(0, Number(cost.mana ?? 0) - (me.runePool.mana ?? 0)));
    const recycles = recyclesByLoss(action.recycles.map((c) => c.id), action.taps).slice(0, Math.max(0, Number(cost.power ?? 0) - (me.runePool.power ?? 0)));
    return { ...sel, optionalCosts: [...sel.optionalCosts, ...taps, ...recycles] };
  };

  // Targets can add costs (e.g. spellshield), so quote the exact play before sending it.
  const submitPlay = async (p: Pick) => {
    let sel = p.sel;
    let quote = await client.previewPlayCard(buildCommand(p.action, sel));
    const missingMana = quote?.isValid ? quote.cost?.missingMana ?? 0 : 0;
    const taps = p.action.taps.filter((t) => !sel.optionalCosts.includes(t)).slice(0, missingMana);
    if (missingMana > 0 && taps.length === missingMana) {
      sel = { ...sel, optionalCosts: [...sel.optionalCosts, ...taps] };
      quote = await client.previewPlayCard(buildCommand(p.action, sel));
    }
    if (!quote || (quote.isValid && quote.canPay)) {
      const ready = await tapBeforeRecycle(sel);
      // After the tap the prompt has moved on; the play itself is unchanged, so send it against the new one.
      void client.submit(buildCommand(p.action, ready.sel), ready.tapped ? undefined : p.action.promptId);
      return;
    }
    const missingPower = quote.cost?.missingPower ?? 0;
    const haveRecycled = anyPowerFrom(p.action.requirement, sel.optionalCosts);
    if (quote.isValid && missingPower > 0 && !quote.cost?.missingMana && p.action.recycles.length) {
      setPick({ ...p, sel, step: p.action.steps.length, powerNeed: haveRecycled + missingPower });
      return;
    }
    const missing = [
      quote.cost?.missingMana ? `${quote.cost.missingMana} 点法力` : "",
      missingPower ? `${missingPower} 点符能` : "",
      quote.cost?.missingExperience ? `${quote.cost.missingExperience} 点经验` : "",
    ].filter(Boolean);
    client.toast(missing.length ? `费用不足：还差 ${missing.join("、")}。` : readableError(quote.errorCode, quote.message));
  };

  const advance = (p: Pick) => {
    if (p.step >= p.action.steps.length) {
      if (p.action.optionalCosts.length || p.action.powerShortfall > 0 || p.action.variablePower) setPick(p);
      else submitPick(p);
      return;
    }
    setPick(p);
  };

  const choose = (choiceId: string) => {
    if (!pick || !currentStep) return;
    if (choiceId.startsWith(GROUP)) {
      const id = choiceId.slice(GROUP.length);
      setPick({ ...pick, group: pick.group.includes(id) ? pick.group.filter((x) => x !== id) : [...pick.group, id] });
      return;
    }
    advance(applyChoice(pick, currentStep, choiceId));
  };

  const skipStep = () => {
    if (!pick || !currentStep || currentStep.required) return;
    advance({ ...pick, step: pick.step + 1 });
  };

  /** Starts an action; a drop location that answers the first step is applied right away. */
  const start = (action: ObjectAction, dropIds: string[] = []) => {
    setMenuFor(null);
    setRuneSel([]);
    let p: Pick = { action, sel: { targets: [], optionalCosts: [...action.presetCosts] }, step: 0, group: [] };
    const first = action.steps[0];
    if (first && dropIds.length) {
      const hit = first.choices.find((c) => choiceObjectIds(c).some((id) => dropIds.includes(id)));
      if (hit) p = applyChoice(p, first, hit.id);
    }
    advance(p);
  };

  const playCard = (id: string, dropIds: string[] = []) => {
    const list = actions.get(id)?.filter((a) => a.cmdType === "PLAY_CARD") ?? [];
    if (!list.length) client.toast("这张牌现在还不能打出。");
    else if (list.length > 1) setMenuFor(id);
    else start(list[0], dropIds);
  };

  const moveUnitsTo = (unitIds: string[], dropIds: string[]) => {
    const move = moveOf(unitIds[0]);
    const dest = (move?.requirement?.destinationChoices ?? []).find((d: Choice) => choiceObjectIds(d).some((x) => dropIds.includes(x)) || dropIds.includes(d.id));
    if (!move || !dest) {
      client.toast("这个单位不能移动到那里。");
      return;
    }
    const others = unitIds.slice(1).filter((u) => destinationsOf(u).includes(dest.id));
    submitPick({ action: move, sel: { targets: [], optionalCosts: [], destination: dest.id }, step: move.steps.length, group: others });
  };

  // ---------- clicks ----------
  const onObject = (id: string) => (e: MouseEvent) => {
    e.stopPropagation();
    if (suppressClick.current) {
      suppressClick.current = false;
      return;
    }
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
    if (isRune(id) && (tapIds.has(id) || recycleIds.has(id))) {
      setRuneSel((cur) => (cur.includes(id) ? cur.filter((x) => x !== id) : [...cur, id]));
      return;
    }
    const list = actions.get(id) ?? [];
    // Units go straight into moving: destinations light up and other units can join.
    const move = list.find((a) => a.cmdType === "MOVE_UNIT");
    if (move) {
      start(move);
      return;
    }
    if (list.length) setMenuFor(menuFor === id ? null : id);
    else setMenuFor(null);
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

  // ---------- drag ----------
  const canDragHand = (id: string) => !pick && promptType !== "MULLIGAN" && Boolean(actions.get(id)?.some((a) => a.cmdType === "PLAY_CARD"));
  const canDragUnit = (id: string) => !pick && Boolean(moveOf(id));
  const beginDrag = (id: string, kind: Drag["kind"]) => (e: ReactPointerEvent) => {
    if (e.button !== 0) return;
    if (kind === "hand" ? !canDragHand(id) : !canDragUnit(id)) return;
    beginCardDrag(id, kind, e);
  };

  // ---------- rendering ----------
  const marksFor = (id: string): CardMark[] => {
    const o = objects[id];
    const marks: CardMark[] = [];
    const inGroup = Boolean(pick?.group.includes(id));
    if (menuFor === id || pick?.action.sourceObjectId === id || mulligan.includes(id) || pick?.sel.targets.includes(id) || inGroup || runeSel.includes(id))
      marks.push("selected");
    if (pick && legal.has(id) && !inGroup) marks.push("legal");
    if (!pick && actions.has(id)) marks.push(autoTapOf(id) != null ? "autopay" : "playable");
    if (o?.isAttacking) marks.push("attacking");
    if (o?.isDefending) marks.push("defending");
    if (drag?.active && drag.id === id) marks.push("dragging");
    if (recent.has(id)) marks.push("recent");
    return marks;
  };

  const hoverCard: HoverHandler = (cardNo, object, rect) => {
    if (dragRef.current?.active) return;
    setHover(cardNo ? { cardNo, object, rect } : null);
  };
  useCardNameHover(hoverCard, objects);
  useHoverAutoClear(Boolean(hover), () => setHover(null));

  const renderObject: RenderObject = (id, size = "board", opts) => {
    const o = objects[id];
    const attachments = Object.values(objects).filter((x) => x.attachedToObjectId === id);
    const inHand = me.zones.hand.includes(id);
    const clickable =
      promptType === "MULLIGAN"
        ? inHand
        : pick
          ? legal.has(id)
          : actions.has(id) || (isRune(id) && (tapIds.has(id) || recycleIds.has(id)));
    const draggable = inHand ? canDragHand(id) : canDragUnit(id);
    return (
      <div className="obj" key={id} data-oid={id}>
        <Card
          object={o}
          cardNo={o?.cardNo}
          size={size}
          peek={opts?.peek}
          marks={marksFor(id)}
          onClick={clickable ? onObject(id) : undefined}
          onPointerDown={draggable ? beginDrag(id, inHand ? "hand" : "unit") : undefined}
          onHover={hoverCard}
        />
        {attachments.length > 0 && (
          <div className="obj__attachments">
            {attachments.map((a) => (
              <span
                key={a.objectId}
                className="obj__attachment"
                onMouseEnter={(e) => hoverCard(a.cardNo ?? null, a, e.currentTarget.getBoundingClientRect())}
                onMouseLeave={() => hoverCard(null)}
              >
                {cardName(a.cardNo)}
              </span>
            ))}
          </div>
        )}
        {menuFor === id && (
          <ActionMenu
            actions={actions.get(id) ?? []}
            hideRecycles={(me.runePool.power ?? 0) < 1}
            onPick={(a) => start(a)}
            onClose={() => setMenuFor(null)}
          />
        )}
      </div>
    );
  };

  const battlefields: TableBattlefield[] = snapshot.table?.battlefields ?? [];
  const zoneIds = zoneView ? snapshot.players[zoneView.playerId]?.zones[zoneView.zone] ?? [] : [];
  const winner = snapshot.timing.winnerPlayerId;
  const series = snapshot.series;
  const matchContinues = Boolean(series && series.bestOf > 1 && series.phase !== "finished");
  useResultSounds(winner, meId);
  const tilt = tiltHandlers();
  const myMainTurn = promptType === "MAIN_ACTION" && (snapshot.timing.turnPlayerId || snapshot.activePlayerId) === meId;
  const heroActions = (id: string) => (actions.get(id) ?? []).filter((a) => a.cmdType !== "MOVE_UNIT");
  const pickOtherActions = pick ? (actions.get(pick.action.sourceObjectId) ?? []).filter((a) => a.key !== pick.action.key) : [];
  const dragObject = drag?.active ? objects[drag.id] : undefined;

  return (
    <LayoutGroup>
      <div className={`table${drag?.active ? " is-dragging" : ""}`} onClick={() => setMenuFor(null)}>
        <ScoreSpine me={me} opp={opp} goal={snapshot.timing.winningScore || 8} />

        <div className="board">
          <Ambient variant="table" />
          <div className="board__rift" aria-hidden />
          {opp && <OpponentHand player={opp} />}
          {opp && (
            <Side
              player={opp}
              side="opp"
              render={renderObject}
              onZone={onZone}
              legal={legal}
              onOpenZone={setZoneView}
              heroActions={() => []}
              onHeroAction={() => {}}
            />
          )}

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
            {myMainTurn && !pick && !runeSel.length && (
              <p className="tips">拖出手牌即可打出（法力不足会自动横置符文）· 点单位再点战场来移动，可连点多个单位一起移动 · 点符文可多选横置或回收 · 快捷键：空格 让过/结束回合，T 全部横置，Enter 确认，Esc 取消</p>
            )}
          </div>

          <Side
            player={me}
            side="me"
            render={renderObject}
            onZone={onZone}
            legal={legal}
            onOpenZone={setZoneView}
            heroActions={heroActions}
            onHeroAction={(a) => (a.cmdType === "PLAY_CARD" ? void playCard(a.sourceObjectId) : start(a))}
            runeBar={
              runeSel.length > 0 ? (
                <RuneBar
                  selected={runeSel}
                  tapIds={tapIds}
                  recycleIds={recycleIds}
                  pending={state.pending}
                  onSelectAll={() => setRuneSel([...new Set([...runeSel, ...tapIds])])}
                  onCancel={() => setRuneSel([])}
                  onRun={async (mode) => {
                    const ready = runeSel.filter((r) => tapIds.has(r));
                    const recyclable = runeSel.filter((r) => recycleIds.has(r));
                    setRuneSel([]);
                    if (mode === "tap") await runRuneCommand("TAP_RUNE", ready);
                    else if (mode === "recycle") await runRuneCommand("RECYCLE_RUNE", recyclable);
                    else {
                      await runRuneCommand("TAP_RUNE", ready);
                      // Re-read the server's recyclable list after tapping; it decides what is legal now.
                      const fresh = new Set((enabledCandidate(client.state.prompt, "RECYCLE_RUNE")?.sources ?? []).map((s) => s.id));
                      await runRuneCommand("RECYCLE_RUNE", recyclable.filter((r) => fresh.has(r)));
                    }
                  }}
                />
              ) : null
            }
          />

          <AnimatePresence>
            {promptType === "MULLIGAN" && prompt?.actionable && (
              <MulliganPanel prompt={prompt} hand={me.zones.hand} objects={objects} mulligan={mulligan} pending={state.pending} onCard={onObject} />
            )}
          </AnimatePresence>

          <div className={`hand${promptType === "MULLIGAN" ? " hand--mulligan" : ""}`} aria-label="你的手牌">
            {me.zones.hand.map((id, i) => {
              const n = me.zones.hand.length;
              const spread = Math.min(5, 40 / Math.max(n, 1));
              const angle = (i - (n - 1) / 2) * spread;
              return (
                <div
                  className="hand__card"
                  key={id}
                  style={{ transform: `rotate(${angle}deg) translateY(${Math.abs(angle) * 1.6}px)`, ["--fan" as string]: `${angle}deg`, ["--i" as string]: i }}
                  {...tilt}
                >
                  {renderObject(id, "hand")}
                </div>
              );
            })}
          </div>


          {!winner && <StackShow snapshot={snapshot} meId={meId} />}
          {!winner && (
            <Dock
              prompt={prompt}
              pending={state.pending}
              autoPass={autoPass}
              unused={myMainTurn ? [...actions].filter(([id, list]) => !isRune(id) && list.some((a) => a.cmdType !== "TAP_RUNE" && a.cmdType !== "RECYCLE_RUNE")).length : 0}
              onToggleAutoPass={toggleAutoPass}
            />
          )}

          <AnimatePresence>
            {pick && (
              <PickBar
                key="pickbar"
                pick={pick}
                step={currentStep}
                choices={stepChoices}
                peers={peers.length}
                otherActions={pickOtherActions}
                onOtherAction={(a) => start(a)}
                extraPower={(dest) => Number(pick.action.candidate.metadata?.powerPerExtraUnitByDestination?.[dest] ?? 0) * pick.group.length}
                objects={objects}
                onChoose={choose}
                onSkip={skipStep}
                onToggleCost={(id) => {
                  const on = pick.sel.optionalCosts.includes(id);
                  // At most one card may be showcased (shown) per play.
                  const exclusive = EXCLUSIVE_COST_PREFIXES.find((p) => id.startsWith(p));
                  const kept = exclusive ? pick.sel.optionalCosts.filter((c) => !c.startsWith(exclusive)) : pick.sel.optionalCosts;
                  const next = on ? pick.sel.optionalCosts.filter((c) => c !== id) : [...kept, id];
                  setPick({ ...pick, sel: { ...pick.sel, optionalCosts: next } });
                }}
                onSetCosts={(costs) => setPick({ ...pick, sel: { ...pick.sel, optionalCosts: costs } })}
                onConfirm={() => submitPick(pick)}
                onCancel={() => setPick(null)}
              />
            )}
          </AnimatePresence>

          {zoneView && (
            <ZoneViewer
              zoneView={zoneView}
              zoneIds={zoneIds}
              meId={meId}
              objects={objects}
              heroActions={heroActions}
              hoverCard={hoverCard}
              onClose={() => setZoneView(null)}
              onAction={(a) => {
                setZoneView(null);
                if (a.cmdType === "PLAY_CARD") void playCard(a.sourceObjectId);
                else start(a);
              }}
            />
          )}

          <TurnBanner banner={winner ? null : banner} turnNumber={snapshot.turnNumber} />

          {winner && (
            <ResultPanel
              winner={winner}
              meId={meId}
              me={me}
              opp={opp}
              series={series}
              tally={state.tally}
              turns={snapshot.turnNumber}
              matchContinues={matchContinues}
            />
          )}
        </div>

        <Rail state={state} snapshot={snapshot} prompt={prompt} objects={objects} mulligan={mulligan} onHover={hoverCard} />

        <ChainArrows snapshot={snapshot} meId={meId} />
        <EventFx meId={meId} />
        <Stage meId={meId} />
        <Spotlight meId={meId} />
        <AnimatePresence>{hover && !drag?.active && <HoverPreview key={hover.cardNo + (hover.object?.objectId ?? "")} {...hover} />}</AnimatePresence>
        {drag?.active && (
          <div className="dragghost" style={{ left: drag.x - drag.ox, top: drag.y - drag.oy }} aria-hidden>
            <Card object={dragObject} cardNo={dragObject?.cardNo} size={drag.kind === "hand" ? "hand" : "board"} animate={false} />
            <span className="dragghost__hint">{drag.kind === "hand" ? (autoTapOf(drag.id) ? `松手打出 · 自动横置 ${autoTapOf(drag.id)} 枚符文` : "松手打出") : "拖到战场或基地"}</span>
          </div>
        )}
        {state.connectionNote && <div className="conn">{state.connectionNote}</div>}
      </div>
    </LayoutGroup>
  );
}
