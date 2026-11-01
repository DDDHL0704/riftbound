import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent, type PointerEvent as ReactPointerEvent } from "react";
import { AnimatePresence, LayoutGroup, motion, useIsPresent } from "motion/react";
import { client, readableError, type ClientState } from "../net/client";
import {
  anyPowerFrom,
  buildCommand,
  choiceObjectIds,
  enabledCandidate,
  isRecycle,
  isTap,
  objectActions,
  powerFrom,
  recyclesByLoss,
  type ActionStep,
  type ObjectAction,
  type Selection,
} from "../game/actions";
import { cardInfo, cardName, paymentChoiceLabel } from "../game/cards";
import { cue } from "../game/sound";
import type { Choice, GameObject, Prompt, Snapshot, TableBattlefield } from "../game/types";
import { Card, HoverPreview, type CardMark, type HoverHandler } from "./Card";
import { ChainArrows, Dock, EventFx, Spotlight } from "./Overlay";
import { Rail } from "./Rail";
import { Battlefield, OpponentHand, ScoreSpine, Side, type RenderObject, type ZoneView } from "./Zones";

/** `group` holds extra units moving together with the source (one destination, CN 144.3). */
type Pick = {
  action: ObjectAction;
  sel: Selection;
  step: number;
  group: string[];
  /** Total recycled power the server's cost quote says this play needs (target taxes included). */
  powerNeed?: number;
};

type Drag = { id: string; kind: "hand" | "unit"; x: number; y: number; ox: number; oy: number; active: boolean };
type HoverState = { cardNo: string; object?: GameObject; rect?: DOMRect } | null;

const GROUP = "__unit:";
const DRAG_THRESHOLD = 6;

export type Hover = HoverState;

/** Applies one choice to a pick, returning the next pick (pure, so drops and clicks share it). */
function applyChoice(pick: Pick, step: ActionStep, choiceId: string): Pick {
  const sel: Selection = { ...pick.sel, targets: [...pick.sel.targets], movements: [...(pick.sel.movements ?? [])] };
  if (step.role === "destination") sel.destination = choiceId;
  else if (step.role === "move") sel.movements!.push(choiceId);
  else if (step.role === "cost") sel.optionalCosts = [choiceId, ...sel.optionalCosts.filter((c) => isTap(c) || isRecycle(c))];
  else sel.targets.push(choiceId);
  return { ...pick, sel, step: pick.step + 1 };
}

/** Ids under a screen point that a drop can mean: drop zones first, then cards. */
function dropIdsAt(x: number, y: number): string[] {
  const ids: string[] = [];
  for (const el of document.elementsFromPoint(x, y)) {
    const h = el as HTMLElement;
    if (h.dataset?.drop) {
      ids.push(h.dataset.drop);
      if (h.dataset.drop === "BASE" && h.dataset.dropOwner) ids.push(`BASE:${h.dataset.dropOwner}`);
      if (h.dataset.drop.startsWith("BATTLEFIELD:")) ids.push(h.dataset.drop.slice(12));
    }
    if (h.dataset?.oid) ids.push(h.dataset.oid);
  }
  return [...new Set(ids)];
}

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
  const [hover, setHover] = useState<HoverState>(null);
  const [banner, setBanner] = useState<string | null>(null);
  const [zoneView, setZoneView] = useState<ZoneView>(null);
  const [runeSel, setRuneSel] = useState<string[]>([]);
  const [drag, setDrag] = useState<Drag | null>(null);
  const [autoPass, setAutoPass] = useState(() => {
    try {
      return localStorage.getItem("riftbound.web.autoPass") !== "off";
    } catch {
      return true;
    }
  });
  const suppressClick = useRef(false);

  // Any new prompt invalidates half-built selections.
  useEffect(() => {
    setMenuFor(null);
    setPick(null);
    setRuneSel([]);
  }, [prompt?.promptId]);

  useEffect(() => {
    if (promptType !== "MULLIGAN") setMulligan([]);
  }, [promptType]);

  // The table needs about 1100 CSS px across. On a narrower touch screen (a tablet held upright, a
  // phone) lay the page out at that width and let the browser scale it to fit, instead of
  // reflowing the board; the lobby keeps the device width.
  useEffect(() => {
    const meta = document.querySelector<HTMLMetaElement>('meta[name="viewport"]');
    if (!meta || Math.min(screen.width, screen.height) >= TABLE_MIN_WIDTH || !matchMedia("(pointer: coarse)").matches) return;
    const before = meta.content;
    // Name the scale too: browsers keep the current zoom when only the layout width changes.
    const scale = Math.min(1, document.documentElement.clientWidth / TABLE_MIN_WIDTH).toFixed(3);
    meta.content = `width=${TABLE_MIN_WIDTH}, initial-scale=${scale}, minimum-scale=${scale}`;
    return () => {
      meta.content = before;
    };
  }, []);

  // Where each of the opponent's board objects sits; when one arrives somewhere new (played,
  // moved, returned) it glows for a moment so the change is easy to spot.
  const [recent, setRecent] = useState<Set<string>>(new Set());
  const oppPlaces = useRef<Map<string, string> | null>(null);
  useEffect(() => {
    const oppId = Object.keys(snapshot.players).find((id) => id !== meId);
    if (!oppId) return;
    const places = new Map<string, string>();
    for (const id of snapshot.players[oppId]?.zones.base ?? []) places.set(id, "base");
    for (const bf of snapshot.table?.battlefields ?? []) for (const id of bf.unitsBySide?.[oppId] ?? []) places.set(id, bf.battlefieldObjectId);
    const before = oppPlaces.current;
    oppPlaces.current = places;
    if (!before) return;
    const moved = [...places].filter(([id, at]) => before.get(id) !== at).map(([id]) => id);
    if (!moved.length) return;
    setRecent((cur) => new Set([...cur, ...moved]));
    // Not cancelled by the next snapshot: each arrival clears only its own glow.
    setTimeout(() => setRecent((cur) => new Set([...cur].filter((id) => !moved.includes(id)))), 2500);
  }, [snapshot, meId]);

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
    if (turnPlayer === meId) cue("turn");
    // Short: a start-of-turn prompt often needs an answer right away and the strip sits over the fields.
    const t = setTimeout(() => setBanner(null), 1100);
    return () => clearTimeout(t);
  }, [snapshot.turnNumber, snapshot.activePlayerId, snapshot.timing.turnPlayerId, snapshot.timing.phase, meId]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setPick(null);
        setMenuFor(null);
        setZoneView(null);
        setRuneSel([]);
      }
      // Space presses the dock's main button (pass / pass focus / end turn), outside text fields
      // and never while a selection is half built.
      if (e.target instanceof HTMLInputElement || e.repeat) return;
      // Enter confirms a half-built selection once it is complete.
      if (e.key === "Enter") {
        const confirm = [...document.querySelectorAll<HTMLButtonElement>(".pickbar:not(.pickbar--leaving) button")].find((b) => b.textContent?.startsWith("确认") && !b.disabled);
        if (confirm) {
          e.preventDefault();
          confirm.click();
        }
      }
      // T taps every ready rune.
      if (e.key === "t" || e.key === "T") document.querySelector<HTMLButtonElement>(".dock__runes:not(:disabled)")?.click();
      if (e.code === "Space" && !document.querySelector(".pickbar, .menu, .runebar, .mulligan")) {
        const button = document.querySelector<HTMLButtonElement>('.dock [data-hotkey="space"]:not(:disabled)');
        if (button) {
          e.preventDefault();
          button.click();
        }
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

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
  useEffect(() => {
    if (!autoPass || !prompt?.actionable || pick || menuFor) return;
    const type = prompt.view?.type;
    if (type !== "STACK_PRIORITY" && type !== "SPELL_DUEL_FOCUS") return;
    if (respondable) return;
    const passAction = enabledCandidate(prompt, "PASS_PRIORITY") ? "PASS_PRIORITY" : enabledCandidate(prompt, "PASS_FOCUS") ? "PASS_FOCUS" : null;
    if (!passAction) return;
    const t = setTimeout(() => {
      if (client.state.prompt?.promptId === prompt.promptId) void client.submit({ cmdType: passAction });
    }, 700);
    return () => clearTimeout(t);
  }, [autoPass, prompt, respondable, pick, menuFor]);

  const runRuneCommand = async (cmdType: "TAP_RUNE" | "RECYCLE_RUNE", ids: string[]) => {
    if (!ids.length) return null;
    return client.submit({ cmdType, sourceObjectId: ids[0], sourceObjectIds: ids.slice(0, 12) });
  };

  // ---------- picking ----------
  const currentStep: ActionStep | undefined = pick ? pick.action.steps[pick.step] : undefined;
  const moveOf = (id: string) => actions.get(id)?.find((a) => a.cmdType === "MOVE_UNIT");
  const grouping = pick?.action.cmdType === "MOVE_UNIT" && currentStep?.role === "destination";
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
      void client.submit(buildCommand(p.action, withOptionPayment(p)), p.action.promptId);
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
      void client.submit(buildCommand(p.action, sel), p.action.promptId);
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
      if (p.action.optionalCosts.length || p.action.powerShortfall > 0) setPick(p);
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
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    setDrag({ id, kind, x: e.clientX, y: e.clientY, ox: e.clientX - rect.left, oy: e.clientY - rect.top, active: false });
  };
  const dragRef = useRef<Drag | null>(null);
  dragRef.current = drag;
  // Drops act through the latest handlers: auto-pay quotes and prompts change after the drag starts.
  const latest = useRef({ playCard, moveUnitsTo });
  latest.current = { playCard, moveUnitsTo };
  const finishDrag = useCallback(
    (x: number, y: number) => {
      const d = dragRef.current;
      setDrag(null);
      if (!d?.active) return;
      suppressClick.current = true;
      setTimeout(() => (suppressClick.current = false), 0);
      const ids = dropIdsAt(x, y);
      const handTop = document.querySelector(".hand")?.getBoundingClientRect().top ?? window.innerHeight;
      if (d.kind === "hand") {
        if (y < handTop - 20) void latest.current.playCard(d.id, ids);
      } else if (ids.some((i) => i === "BASE" || i.startsWith("BATTLEFIELD:"))) {
        latest.current.moveUnitsTo([d.id], ids);
      }
    },
    [],
  );
  useEffect(() => {
    if (!drag) return;
    const move = (e: PointerEvent) => {
      setDrag((cur) => {
        if (!cur) return cur;
        const active = cur.active || Math.hypot(e.clientX - cur.x, e.clientY - cur.y) > DRAG_THRESHOLD;
        return { ...cur, x: active ? e.clientX : cur.x, y: active ? e.clientY : cur.y, active };
      });
      if (dragRef.current?.active) setHover(null);
    };
    const up = (e: PointerEvent) => finishDrag(e.clientX, e.clientY);
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", up, { once: true });
    return () => {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", up);
    };
  }, [drag?.id, finishDrag]); // eslint-disable-line react-hooks/exhaustive-deps

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
    // Runes need no large preview; their art says nothing new and would cover the table.
    if (cardNo && object && me.zones.baseRunes.includes(object.objectId)) return setHover(null);
    setHover(cardNo ? { cardNo, object, rect } : null);
  };

  // A hovered card can move or vanish without a mouseleave; drop the preview once nothing is under the pointer.
  useEffect(() => {
    if (!hover) return;
    let frame = 0;
    const check = (e: PointerEvent) => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        const el = document.elementFromPoint(e.clientX, e.clientY);
        if (!el?.closest(".card, .field__card, .obj__attachment, .stack li")) setHover(null);
      });
    };
    window.addEventListener("pointermove", check);
    window.addEventListener("pointerdown", check);
    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener("pointermove", check);
      window.removeEventListener("pointerdown", check);
    };
  }, [hover]);

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
  // The closing cue plays once, when the result arrives during this visit to the table.
  const seenWinner = useRef(winner);
  useEffect(() => {
    if (winner && !seenWinner.current) cue(winner === meId ? "win" : "loss");
    seenWinner.current = winner;
  }, [winner, meId]);
  const myMainTurn = promptType === "MAIN_ACTION" && (snapshot.timing.turnPlayerId || snapshot.activePlayerId) === meId;
  const heroActions = (id: string) => (actions.get(id) ?? []).filter((a) => a.cmdType !== "MOVE_UNIT");
  const pickOtherActions = pick ? (actions.get(pick.action.sourceObjectId) ?? []).filter((a) => a.key !== pick.action.key) : [];
  const dragObject = drag?.active ? objects[drag.id] : undefined;

  return (
    <LayoutGroup>
      <div className={`table${drag?.active ? " is-dragging" : ""}`} onClick={() => setMenuFor(null)}>
        <ScoreSpine me={me} opp={opp} goal={snapshot.timing.winningScore || 8} />

        <div className="board">
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
              <motion.div className="mulligan" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} onClick={(e) => e.stopPropagation()}>
                <h2 className="mulligan__title">起手调度</h2>
                <p className="mulligan__hint">点选最多 {prompt.view?.maxSelection ?? 2} 张想换掉的牌，它们会放回牌库底并重新抽取。</p>
                <div className="mulligan__cards">
                  {me.zones.hand.map((id, i) => {
                    const o = objects[id];
                    const swap = mulligan.includes(id);
                    return (
                      <motion.button
                        key={id}
                        className={`mulligan__card${swap ? " is-swap" : ""}`}
                        initial={{ opacity: 0, y: 30, rotate: (i - 1.5) * 4 }}
                        animate={{ opacity: 1, y: swap ? 14 : 0, rotate: 0 }}
                        transition={{ delay: i * 0.06, type: "spring", stiffness: 260, damping: 22 }}
                        onClick={onObject(id)}
                        aria-pressed={swap}
                        aria-label={`${cardName(o?.cardNo)}${swap ? "（换掉）" : ""}`}
                      >
                        {cardInfo(o?.cardNo)?.image ? <img src={cardInfo(o?.cardNo)!.image} alt="" draggable={false} /> : <span>{cardName(o?.cardNo)}</span>}
                        {swap && <span className="mulligan__mark">换掉</span>}
                      </motion.button>
                    );
                  })}
                </div>
                <button className="btn btn--primary" disabled={state.pending} onClick={() => void client.submit({ cmdType: "MULLIGAN", handObjectIds: mulligan })}>
                  {mulligan.length ? `换掉 ${mulligan.length} 张` : "保留起手"}
                </button>
              </motion.div>
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
                  style={{ transform: `rotate(${angle}deg) translateY(${Math.abs(angle) * 1.6}px)`, ["--fan" as string]: `${angle}deg` }}
                >
                  {renderObject(id, "hand")}
                </div>
              );
            })}
          </div>


          {!winner && (
            <Dock
              prompt={prompt}
              pending={state.pending}
              autoPass={autoPass}
              unused={myMainTurn ? [...actions].filter(([id, list]) => !isRune(id) && list.some((a) => a.cmdType !== "TAP_RUNE" && a.cmdType !== "RECYCLE_RUNE")).length : 0}
              onToggleAutoPass={() => {
                const next = !autoPass;
                setAutoPass(next);
                try {
                  localStorage.setItem("riftbound.web.autoPass", next ? "on" : "off");
                } catch {
                  /* preference simply isn't remembered */
                }
              }}
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
                  const next = on ? pick.sel.optionalCosts.filter((c) => c !== id) : [...pick.sel.optionalCosts, id];
                  setPick({ ...pick, sel: { ...pick.sel, optionalCosts: next } });
                }}
                onConfirm={() => submitPick(pick)}
                onCancel={() => setPick(null)}
              />
            )}
          </AnimatePresence>

          {zoneView && (
            <div className="zoneview" role="dialog" aria-label={zoneView.title} onClick={() => setZoneView(null)}>
              <motion.div className="zoneview__box" onClick={(e) => e.stopPropagation()} initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
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
                    <Card key={id} object={objects[id]} cardNo={objects[id]?.cardNo} size="board" onHover={hoverCard} animate={false} />
                  ))}
                </div>
                <p className="muted small">最新进入的牌排在最前。</p>
              </motion.div>
            </div>
          )}

          <AnimatePresence>
            {banner && !winner && (
              <motion.div
                key={banner + snapshot.turnNumber}
                className={`banner ${banner === "你的回合" ? "banner--me" : "banner--opp"}`}
                role="status"
                initial={{ opacity: 0, scaleX: 0.6 }}
                animate={{ opacity: 1, scaleX: 1 }}
                exit={{ opacity: 0, y: -16 }}
                transition={{ duration: 0.35, ease: [0.2, 0.8, 0.2, 1] }}
              >
                <span>{banner}</span>
                <small>第 {snapshot.turnNumber} 回合</small>
              </motion.div>
            )}
          </AnimatePresence>

          {winner && (
            <motion.div className={`result ${winner === meId ? "result--win" : "result--loss"}`} role="status" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
              <motion.p className="result__title" initial={{ scale: 1.4, opacity: 0 }} animate={{ scale: 1, opacity: 1 }} transition={{ type: "spring", stiffness: 260, damping: 18 }}>
                {winner === meId ? "胜利" : "落败"}
              </motion.p>
              <p className="result__score">
                {me.score} : {opp?.score ?? 0}
              </p>
              <ResultStats tally={state.tally} meId={meId} oppId={opp?.id} turns={snapshot.turnNumber} />
              <button className="btn btn--primary" onClick={() => client.leaveRoom()}>
                返回大厅
              </button>
            </motion.div>
          )}
        </div>

        <Rail state={state} snapshot={snapshot} prompt={prompt} objects={objects} mulligan={mulligan} onHover={hoverCard} />

        <ChainArrows snapshot={snapshot} meId={meId} />
        <EventFx meId={meId} />
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

const TABLE_MIN_WIDTH = 1100;

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

function ActionMenu({
  actions,
  hideRecycles,
  onPick,
  onClose,
}: {
  actions: ObjectAction[];
  hideRecycles: boolean;
  onPick: (a: ObjectAction) => void;
  onClose: () => void;
}) {
  return (
    <motion.div className="menu" role="menu" onClick={(e) => e.stopPropagation()} initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.12 }}>
      {actions.map((a) => (
        <button key={a.key} role="menuitem" className="menu__item" onClick={() => onPick(a)}>
          <span>{a.label}</span>
          {a.cmdType === "HIDE_CARD" && hideRecycles && a.recycles.length > 0 ? (
            <span className="menu__detail">回收 1 枚符文支付符能</span>
          ) : (
            a.detail && <span className="menu__detail">{a.detail}</span>
          )}
        </button>
      ))}
      <button className="menu__item menu__item--quiet" onClick={onClose}>
        取消
      </button>
    </motion.div>
  );
}

type RuneMode = "tap" | "recycle" | "tap-recycle";

/**
 * Multi-select rune actions. The rules allow recycling a ready rune, but that throws away its
 * mana, so "tap then recycle" is offered first and a bare recycle of ready runes says so.
 */
function RuneBar({
  selected,
  tapIds,
  recycleIds,
  pending,
  onRun,
  onSelectAll,
  onCancel,
}: {
  selected: string[];
  tapIds: Set<string>;
  recycleIds: Set<string>;
  pending: boolean;
  onRun: (mode: RuneMode) => void;
  onSelectAll: () => void;
  onCancel: () => void;
}) {
  const ready = selected.filter((r) => tapIds.has(r)).length;
  const recyclable = selected.filter((r) => recycleIds.has(r)).length;
  const readyRecyclable = selected.filter((r) => tapIds.has(r) && recycleIds.has(r)).length;
  return (
    <motion.div className="runebar" onClick={(e) => e.stopPropagation()} initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}>
      <span className="runebar__count">已选 {selected.length} 枚符文</span>
      {ready > 0 && (
        <button className="btn btn--small btn--primary" disabled={pending} onClick={() => onRun("tap")}>
          横置 <b>+{ready} 法力</b>
        </button>
      )}
      {readyRecyclable > 0 && (
        <button className="btn btn--small" disabled={pending} onClick={() => onRun("tap-recycle")} title="先横置拿法力，再回收拿符能">
          横置并回收 <b>+{ready} 法力 +{recyclable} 符能</b>
        </button>
      )}
      {recyclable > 0 && (
        <button
          className="btn btn--small btn--ghost"
          disabled={pending}
          onClick={() => onRun("recycle")}
          title={readyRecyclable ? "所选符文中有未横置的：直接回收会损失这次的法力" : "回收符文获得符能"}
        >
          {readyRecyclable ? "仅回收（损失法力）" : `回收 +${recyclable} 符能`}
        </button>
      )}
      {tapIds.size > ready && (
        <button className="btn btn--small btn--ghost" onClick={onSelectAll}>
          全选未横置
        </button>
      )}
      <button className="runebar__close" onClick={onCancel} aria-label="取消选择">
        ✕
      </button>
    </motion.div>
  );
}

type PickBarProps = {
  pick: Pick;
  step?: ActionStep;
  choices: Choice[];
  peers: number;
  otherActions: ObjectAction[];
  onOtherAction: (a: ObjectAction) => void;
  extraPower: (destinationId: string) => number;
  objects: Record<string, GameObject>;
  onChoose: (id: string) => void;
  onSkip: () => void;
  onToggleCost: (id: string) => void;
  onConfirm: () => void;
  onCancel: () => void;
};

function choiceName(id: string, label: string, objects: Record<string, GameObject>) {
  if (id === "BASE" || id.startsWith("BASE:")) return id === "BASE" ? "基地" : label;
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

function PickBar({ pick, step, choices, peers, otherActions, onOtherAction, extraPower, objects, onChoose, onSkip, onToggleCost, onConfirm, onCancel }: PickBarProps) {
  const src = objects[pick.action.sourceObjectId];
  const info = cardInfo(src?.cardNo);
  const moving = pick.action.cmdType === "MOVE_UNIT";
  // While it fades out after the prompt changed, its choices belong to a prompt that is gone.
  const leaving = !useIsPresent();
  return (
    <motion.div
      className={leaving ? "pickbar pickbar--leaving" : "pickbar"}
      onClick={(e) => e.stopPropagation()}
      role="dialog"
      aria-label="选择"
      initial={{ opacity: 0, y: -10 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -10 }}
      transition={{ duration: 0.16 }}
    >
      {info?.image && <img className="pickbar__thumb" src={info.image} alt="" />}
      <div className="pickbar__body">
        <p className="pickbar__title">
          <strong>{info?.name ?? cardName(src?.cardNo)}</strong>
          <span>{pick.action.label}</span>
        </p>
        {step ? (
          <>
            <p className="pickbar__hint">{moving ? "点发光的战场或基地完成移动，也可以把单位直接拖过去。" : `${step.label}：点场上发光的卡牌或区域，或下方选项。`}</p>
            {moving && peers > 0 && (
              <p className="pickbar__hint pickbar__hint--group">
                {pick.group.length ? `将一起移动 ${pick.group.length + 1} 个单位，再点单位可加入或取消。` : `一起移动：再点其他发光的单位（还有 ${peers} 个可以一起走）。`}
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
            {otherActions.length > 0 && (
              <div className="pickbar__others">
                <span className="muted small">其他操作</span>
                {otherActions.map((a) => (
                  <button key={a.key} className="chip chip--quiet" onClick={() => onOtherAction(a)}>
                    {a.label}
                    {a.detail && <span className="chip__cost">{a.detail}</span>}
                  </button>
                ))}
              </div>
            )}
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
              {[...pick.action.optionalCosts, ...(neededPower(pick) > 0 ? pick.action.recycles : [])]
                .map((c) => (
                  <label key={c.id} className="chip chip--toggle">
                    <input type="checkbox" checked={pick.sel.optionalCosts.includes(c.id)} onChange={() => onToggleCost(c.id)} />
                    {paymentChoiceLabel(c.id, c.label, objects)}
                  </label>
                ))}
            </div>
            <button className="btn btn--primary btn--small" onClick={onConfirm} disabled={providedPower(pick, pick.sel.optionalCosts) < neededPower(pick)}>
              确认{pick.action.label}
            </button>
          </>
        )}
      </div>
      <button className="pickbar__cancel" onClick={onCancel} aria-label="取消选择">
        取消
      </button>
    </motion.div>
  );
}
