import { buildCommand, enabledCandidate, isRecycle, isTap, objectActions, powerFrom, type ObjectAction, type Selection } from "../game/actions";
import { cardInfo } from "../game/cards";
import { fillByIndex, suggestCombat, suggestEffect } from "../game/decisions";
import type { Choice, GameObject, Prompt, Snapshot } from "../game/types";

/*
 * The practice opponent's choices. It only ever answers the prompt the server sent it, with
 * commands built from that prompt's candidates, so the server still decides what is legal.
 * "easy" plays whatever it can in random order; "normal" ranks plays, moves to battlefields it
 * can win, aims harmful effects at the player and helpful ones at itself.
 */

export type Level = "easy" | "normal";

export type Option = {
  /** Stable within a prompt, so a refused option is not tried again. */
  key: string;
  cmd: Record<string, unknown>;
  /** A card play is quoted first; runes it may tap cover mana the quote says is missing. */
  play?: { taps: string[] };
  /** How long to wait before acting, so the player can follow the opponent's turn. */
  pause: number;
};

const TEMPO = ["END_TURN", "PASS_PRIORITY", "PASS_FOCUS", "PASS"];
const HARMFUL = /伤害|摧毁|击晕|眩晕|返回.{0,6}手|放逐|弃置|−\d|-\d|战力减|不能/;
const HELPFUL = /\+\d|增益|强化|治疗|活跃|获得|抽/;

type Ctx = { prompt: Prompt; snapshot: Snapshot; me: string; level: Level; objects: Record<string, GameObject> };

function allObjects(snapshot: Snapshot) {
  const out: Record<string, GameObject> = {};
  for (const p of Object.values(snapshot.players)) Object.assign(out, p.objects);
  return out;
}

const controllerOf = (o?: GameObject) => o?.controllerId ?? o?.ownerId;
const shuffle = <T,>(list: T[]) => list.map((x) => [Math.random(), x] as const).sort((a, b) => a[0] - b[0]).map(([, x]) => x);

/** Picks one choice for a step: harmful effects aim at the opponent's strongest, helpful ones at our own. */
function preferChoice(ctx: Ctx, choices: Choice[], sourceText: string, taken: string[]): Choice | undefined {
  const open = choices.filter((c) => !taken.includes(c.id));
  if (!open.length) return undefined;
  if (ctx.level === "easy") return open[Math.floor(Math.random() * open.length)];
  const harmful = HARMFUL.test(sourceText) && !HELPFUL.test(sourceText.replace(HARMFUL, ""));
  const helpful = !harmful && HELPFUL.test(sourceText);
  const score = (c: Choice) => {
    const o = ctx.objects[c.id] ?? ctx.objects[c.id.split(":").pop() ?? ""];
    if (!o) return 0;
    const mine = controllerOf(o) === ctx.me;
    const power = o.effectivePower ?? 0;
    if (harmful) return (mine ? -100 : 100) + power;
    if (helpful) return (mine ? 100 : -100) + power;
    return 0;
  };
  return [...open].sort((a, b) => score(b) - score(a))[0];
}

type Field = { id: string; mine: number; theirs: number; controller?: string | null };

function fields(ctx: Ctx): Field[] {
  const opp = Object.keys(ctx.snapshot.players).find((id) => id !== ctx.me) ?? "";
  const power = (ids: string[] = []) => ids.reduce((n, id) => n + (ctx.objects[id]?.effectivePower ?? 0), 0);
  return (ctx.snapshot.table?.battlefields ?? []).map((b) => ({
    id: b.battlefieldObjectId,
    mine: power(b.unitsBySide?.[ctx.me]),
    theirs: power(b.unitsBySide?.[opp]),
    controller: b.controllerId,
  }));
}

/** How much moving a unit of the given might to a destination is worth; 0 or less means stay. */
function moveValue(ctx: Ctx, destination: string, might: number) {
  if (!destination.startsWith("BATTLEFIELD:")) return -1;
  const f = fields(ctx).find((x) => x.id === destination.slice("BATTLEFIELD:".length));
  if (!f) return 1;
  if (ctx.level === "easy") return Math.random() < 0.6 ? 1 : 0;
  const after = f.mine + might;
  if (f.controller === ctx.me && f.theirs === 0) return f.mine === 0 ? 2 : 0;
  if (f.theirs === 0) return 8;
  if (after > f.theirs) return 6 + (f.controller === ctx.me ? 0 : 2);
  return 0;
}

function sourceText(ctx: Ctx, action: ObjectAction) {
  return cardInfo(ctx.objects[action.sourceObjectId]?.cardNo)?.effect ?? "";
}

/** Fills every required step; optional ones are left empty. Returns null when a required step has no choice. */
function select(ctx: Ctx, action: ObjectAction): Selection | null {
  const sel: Selection = { targets: [], optionalCosts: [...action.presetCosts] };
  const text = sourceText(ctx, action);
  const might = ctx.objects[action.sourceObjectId]?.effectivePower ?? 0;
  for (const step of action.steps) {
    if (!step.required) continue;
    if (step.role === "destination") {
      const ranked = action.cmdType === "MOVE_UNIT"
        ? [...step.choices].sort((a, b) => moveValue(ctx, b.id, might) - moveValue(ctx, a.id, might))
        : [...step.choices].sort((a, b) => Number(b.id.startsWith("BASE:")) - Number(a.id.startsWith("BASE:")));
      if (!ranked[0]) return null;
      sel.destination = ranked[0].id;
    } else if (step.role === "move") {
      const c = step.choices[0];
      if (!c) return null;
      (sel.movements ??= []).push(c.id);
    } else if (step.role === "cost") {
      const c = step.choices.find((x) => !sel.optionalCosts.includes(x.id));
      if (!c) return null;
      sel.optionalCosts.push(c.id);
    } else {
      const taken = step.mode ? sel.modeTargets?.[step.mode] ?? [] : sel.targets;
      const c = preferChoice(ctx, step.choices, text, taken);
      if (!c) return null;
      if (step.mode) (sel.modeTargets ??= {})[step.mode] = [...taken, c.id];
      else sel.targets.push(c.id);
    }
  }
  return sel;
}

/** Ranks one board action; 0 or less means "don't". */
function actionValue(ctx: Ctx, action: ObjectAction, sel: Selection) {
  const o = ctx.objects[action.sourceObjectId];
  const info = cardInfo(o?.cardNo);
  if (ctx.level === "easy") return action.cmdType === "HIDE_CARD" ? 0.2 : Math.random() + 0.5;
  switch (action.cmdType) {
    case "PLAY_CARD": {
      const cost = o?.manaCost ?? info?.cost ?? 0;
      const unit = /单位/.test(info?.category ?? "");
      if (unit) return 10 + cost;
      // Spells and gear: only when every target they took points the right way.
      const text = info?.effect ?? "";
      const harmful = HARMFUL.test(text);
      const wrong = sel.targets.some((t) => {
        const target = ctx.objects[t] ?? ctx.objects[t.split(":").pop() ?? ""];
        return target && (controllerOf(target) === ctx.me) === harmful;
      });
      return wrong ? 0 : 6 + cost;
    }
    case "MOVE_UNIT":
      return sel.destination ? moveValue(ctx, sel.destination, o?.effectivePower ?? 0) : 0;
    case "ACTIVATE_ABILITY":
    case "LEGEND_ACT":
      return 4;
    case "REVEAL_CARD":
    case "ASSEMBLE_EQUIPMENT":
      return 3;
    case "HIDE_CARD":
      return 1;
    default:
      return 0;
  }
}

function boardOptions(ctx: Ctx): Option[] {
  const out: { value: number; option: Option }[] = [];
  for (const list of objectActions(ctx.prompt).values()) {
    for (const action of list) {
      if (action.cmdType === "TAP_RUNE" || action.cmdType === "RECYCLE_RUNE") continue;
      const sel = select(ctx, action);
      if (!sel) continue;
      // Power the preset recycles cannot cover makes the play unaffordable for an unattended opponent.
      if (action.powerShortfall > 0 && powerFrom(action.requirement, sel.optionalCosts) < action.powerShortfall) continue;
      const value = actionValue(ctx, action, sel);
      if (value <= 0) continue;
      const cmd = buildCommand(action, sel);
      out.push({
        value,
        option: {
          key: `${action.key}|${sel.targets.join(",")}|${sel.destination ?? ""}`,
          cmd,
          play: action.cmdType === "PLAY_CARD" ? { taps: action.taps.filter((t) => !sel.optionalCosts.includes(t)) } : undefined,
          pause: 900,
        },
      });
    }
  }
  return out.sort((a, b) => b.value - a.value).map((x) => x.option);
}

function payOptions(ctx: Ctx): Option[] {
  const cand = enabledCandidate(ctx.prompt, "PAY_COST");
  const md = cand?.metadata ?? {};
  if (!cand || md.serverPaymentState === "WAIT") return [];
  const pool = ctx.snapshot.players[ctx.me]?.runePool;
  const steps = cand.selectionSteps ?? [];
  const spend: Choice[] = md.paymentChoices ?? [];
  const resources: Choice[] = steps.length ? steps.slice(1).flatMap((s) => s.choices) : md.paymentResourceChoices ?? [];
  const taps = [...resources, ...((md.paymentResourceChoices ?? []) as Choice[])].map((c) => c.id).filter((id, i, all) => isTap(id) && all.indexOf(id) === i);
  const recycles = resources.map((c) => c.id).filter((id, i, all) => isRecycle(id) && all.indexOf(id) === i);
  const cost = md.cost as { mana?: number; power?: number } | undefined;
  const missingMana = Math.max(0, (cost?.mana ?? 0) - (pool?.mana ?? 0));
  const missingPower = Math.max(0, (cost?.power ?? 0) - (pool?.power ?? 0));
  const canPay = taps.length >= missingMana && recycles.length >= missingPower;
  const base = { cmdType: "PAY_COST", paymentId: md.paymentId, paymentWindow: md.paymentWindow };
  const pay = (ids: string[]) => ({ ...base, paymentChoiceIds: [...taps.slice(0, missingMana), ...ids, ...recycles.slice(0, missingPower)] });
  const first = steps[0]?.choices ?? [];
  const decline = first.find((c) => /DECLINE/.test(c.id));
  const accept = first.find((c) => !/DECLINE/.test(c.id));
  const out: Option[] = [];
  if (canPay) out.push({ key: "pay", cmd: steps.length ? pay(accept ? [accept.id] : []) : pay(spend.map((c) => c.id)), pause: 500 });
  if (decline) out.push({ key: "decline", cmd: { ...base, paymentChoiceIds: [decline.id] }, pause: 500 });
  return out;
}

function promptOptions(ctx: Ctx): Option[] {
  const { prompt, snapshot, me, level } = ctx;
  const out: Option[] = [];
  const add = (key: string, cmd: Record<string, unknown>, pause = 600) => out.push({ key, cmd, pause });

  if (enabledCandidate(prompt, "MULLIGAN")) {
    const hand = snapshot.players[me]?.zones.hand ?? [];
    const max = Number(prompt.view?.maxSelection ?? 2);
    const swap = level === "normal" ? hand.filter((id) => (ctx.objects[id]?.manaCost ?? 0) >= 5).slice(0, max) : [];
    add("mulligan", { cmdType: "MULLIGAN", handObjectIds: swap }, 1200);
  }
  if (enabledCandidate(prompt, "CHOOSE_PLAY_ORDER")) add("order", { cmdType: "CHOOSE_PLAY_ORDER", goFirst: true });
  if (enabledCandidate(prompt, "READY")) add("ready", { cmdType: "READY" });

  const triggers = enabledCandidate(prompt, "ORDER_TRIGGERS");
  if (triggers?.metadata) add("triggers", { cmdType: "ORDER_TRIGGERS", orderedTriggerIds: triggers.metadata.orderedTriggerIds ?? triggers.metadata.triggerIds ?? [] });

  const choose = enabledCandidate(prompt, "CHOOSE_HAND_CARDS") ?? enabledCandidate(prompt, "CHOOSE_CARDS");
  if (choose) {
    const md = choose.metadata ?? {};
    const seen = new Map<string, Choice>();
    for (const s of choose.selectionSteps ?? []) for (const c of s.choices) if (!seen.has(c.id)) seen.set(c.id, c);
    const min = Number(md.requiredCount ?? 0);
    const max = Number(md.maxCount ?? 1);
    // An optional pick that gains a card is worth taking; anything else is left alone.
    const wants = min === 0 && max > 0 && /加入|抽|获得|打出|返回你的手/.test(`${prompt.view?.message ?? ""}${prompt.view?.title ?? ""}`);
    const picked = shuffle([...seen.keys()]).slice(0, Math.max(min, wants ? 1 : 0));
    add("choose", { cmdType: choose.action, choiceId: md.choiceId, choiceWindow: md.choiceWindow, chosenObjectIds: picked });
  }

  out.push(...payOptions(ctx));

  const combat = enabledCandidate(prompt, "ASSIGN_COMBAT_DAMAGE");
  if (combat?.metadata && combat.metadata.assignmentState !== "WAIT") {
    const md = combat.metadata;
    add("combat", { cmdType: "ASSIGN_COMBAT_DAMAGE", battleId: md.battleId, battlefieldId: md.battlefieldId, assignments: suggestCombat(md).filter((a) => a.damage > 0) });
  }
  const effect = enabledCandidate(prompt, "ASSIGN_EFFECT_DAMAGE");
  if (effect?.metadata && effect.metadata.assignmentState !== "WAIT") {
    const md = effect.metadata;
    // Enemy targets first, so the default split lands on the player's side.
    const enemyFirst = (ids: string[]) => [...ids].sort((a, b) => Number(controllerOf(ctx.objects[a]) === me) - Number(controllerOf(ctx.objects[b]) === me));
    const ordered = { ...md, requiredAssignments: (md.requiredAssignments ?? []).map((r: any) => ({ ...r, legalTargetObjectIds: enemyFirst(r.legalTargetObjectIds ?? []) })) };
    add("effect", { cmdType: "ASSIGN_EFFECT_DAMAGE", stackItemId: md.stackItemId, assignments: suggestEffect(ordered).filter((a) => a.damage > 0) });
  }

  const battle = enabledCandidate(prompt, "DECLARE_BATTLE");
  const req = battle?.metadata?.sourceRequirements?.[0];
  if (req) {
    const targets: Choice[] = [];
    for (const list of Object.values((req.battlefieldTargetChoicesByIndex ?? {}) as Record<string, Choice[]>))
      for (const c of list) if (!targets.some((x) => x.id === c.id)) targets.push(c);
    const chosen: string[] = [];
    for (let i = 0; i < Number(req.minBattlefieldTargetCount ?? 0); i++) {
      const c = preferChoice(ctx, targets, "伤害", chosen);
      if (c) chosen.push(c.id);
    }
    add("battle", {
      cmdType: "DECLARE_BATTLE",
      battlefieldId: req.battlefieldChoices?.[0]?.id,
      attackerObjectIds: fillByIndex(req.attackerChoicesByIndex, req.maxAttackerCount ?? req.minAttackerCount),
      defenderObjectIds: fillByIndex(req.targetChoicesByIndex, req.maxDefenderCount ?? req.minDefenderCount),
      optionalCosts: req.requiredOptionalCosts ?? [],
      ...(chosen.length ? { battlefieldTargetObjectIds: chosen } : {}),
    });
  }
  return out;
}

/** Everything worth trying for this prompt, best first, ending with passing or ending the turn. */
export function decide(prompt: Prompt, snapshot: Snapshot, me: string, level: Level): Option[] {
  const ctx: Ctx = { prompt, snapshot, me, level, objects: allObjects(snapshot) };
  const out = promptOptions(ctx);
  // Board actions only on our own main phase; in reaction windows the opponent simply passes.
  const type = prompt.view?.type ?? "";
  const myTurn = (snapshot.timing.turnPlayerId || snapshot.activePlayerId) === me;
  if (type === "MAIN_ACTION" && myTurn) out.push(...boardOptions(ctx));
  for (const action of TEMPO) if (enabledCandidate(prompt, action)) out.push({ key: action, cmd: { cmdType: action }, pause: action === "END_TURN" ? 900 : 450 });
  return out;
}
