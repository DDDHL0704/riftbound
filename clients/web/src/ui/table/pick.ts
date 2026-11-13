import { anyPowerFrom, isRecycle, isTap, powerFrom, type ActionStep, type ObjectAction, type Selection } from "../../game/actions";
import type { Choice } from "../../game/types";

/** `group` holds extra units moving together with the source (one destination, CN 144.3). */
export type Pick = {
  action: ObjectAction;
  sel: Selection;
  step: number;
  group: string[];
  /** Total recycled power the server's cost quote says this play needs (target taxes included). */
  powerNeed?: number;
};

/** Prefix of the pseudo-choices that add or remove a unit from a group move. */
export const GROUP = "__unit:";
/** Optional costs of which a play takes at most one. */
const EXCLUSIVE_COST_PREFIXES = ["SHOWCASE:"];
export const TARGETED_SPELL_REDUCTION = "TARGETED_SPELL_REDUCTION:";
export const BOON_PLAY_COST = "BOON_PLAY_COST:";

/** Mirrors an entry of the requirement's `boonAdditionalCostGroups`: one unit whose boons may pay. */
export type BoonCostGroup = { choiceId: string; choicePrefix: string; sourceObjectId: string; maximumCount: number; maximumTotalCount?: number | null };

export function boonGroups(action: ObjectAction): BoonCostGroup[] {
  return action.requirement?.boonAdditionalCostGroups ?? [];
}

/**
 * The key shared by optional costs that exclude each other, or null. A showcase takes one card; a
 * targeted spell reduction ("TARGETED_SPELL_REDUCTION:<source>:MANA|POWER") takes one dimension per
 * source; boons that ignore the base cost may come from one unit only.
 */
export function exclusiveKey(action: ObjectAction, id: string): string | null {
  const prefix = EXCLUSIVE_COST_PREFIXES.find((p) => id.startsWith(p));
  if (prefix) return prefix;
  if (id.startsWith(TARGETED_SPELL_REDUCTION)) return id.slice(0, id.lastIndexOf(":") + 1);
  if (id.startsWith(BOON_PLAY_COST) && boonGroups(action).some((g) => g.maximumTotalCount === 1)) return BOON_PLAY_COST;
  return null;
}

/** Toggles one optional cost, dropping any it excludes. */
export function toggleCost(pick: Pick, id: string): string[] {
  const chosen = pick.sel.optionalCosts;
  if (chosen.includes(id)) return chosen.filter((c) => c !== id);
  const key = exclusiveKey(pick.action, id);
  return [...(key ? chosen.filter((c) => !c.startsWith(key)) : chosen), id];
}

/** Every unit the selection targets, in any mode. */
function chosenTargets(sel: Selection) {
  return new Set([...sel.targets, ...Object.values(sel.modeTargets ?? {}).flat()]);
}

/**
 * The optional costs worth offering once the targets are known: a targeted spell reduction only
 * applies when the spell targets its unit, and the server rejects it otherwise. A boon group with
 * more than one boon to spend gets a stepper instead of a checkbox (see `boonSteppers`).
 */
export function offeredCosts(pick: Pick): Choice[] {
  const targets = chosenTargets(pick.sel);
  const steppers = new Set(boonSteppers(pick.action).map((g) => g.choiceId));
  return pick.action.optionalCosts.filter((c) => {
    if (steppers.has(c.id)) return false;
    if (!c.id.startsWith(TARGETED_SPELL_REDUCTION)) return true;
    return (c.objectIds ?? []).some((id) => targets.has(id));
  });
}

/** Boon groups the player may spend several boons from, each one lowering the cost. */
export function boonSteppers(action: ObjectAction): BoonCostGroup[] {
  return boonGroups(action).filter((g) => g.maximumCount > 1 && g.maximumTotalCount !== 1);
}

/** Drops costs no longer offered (a reduction for a unit the player stopped targeting). */
export function offeredSelection(pick: Pick): Selection {
  const offered = new Set(offeredCosts(pick).map((c) => c.id));
  const kept = pick.sel.optionalCosts.filter((c) => !c.startsWith(TARGETED_SPELL_REDUCTION) || offered.has(c));
  return kept.length === pick.sel.optionalCosts.length ? pick.sel : { ...pick.sel, optionalCosts: kept };
}

/** Applies one choice to a pick, returning the next pick (pure, so drops and clicks share it). */
export function applyChoice(pick: Pick, step: ActionStep, choiceId: string): Pick {
  const sel: Selection = { ...pick.sel, targets: [...pick.sel.targets], movements: [...(pick.sel.movements ?? [])] };
  if (step.role === "destination") sel.destination = choiceId;
  else if (step.role === "move") sel.movements!.push(choiceId);
  // Cost steps add up when a cost takes several options; rune payments stay last.
  else if (step.role === "cost")
    sel.optionalCosts = [...sel.optionalCosts.filter((c) => !isTap(c) && !isRecycle(c)), choiceId, ...sel.optionalCosts.filter((c) => isTap(c) || isRecycle(c))];
  else if (step.mode) sel.modeTargets = { ...sel.modeTargets, [step.mode]: [...(sel.modeTargets?.[step.mode] ?? []), choiceId] };
  else sel.targets.push(choiceId);
  return { ...pick, sel, step: pick.step + 1 };
}

/** Power the play still needs from recycles: the quote's figure when we have one, else the printed shortfall. */
export function neededPower(pick: Pick) {
  return pick.powerNeed ?? pick.action.powerShortfall;
}

export function providedPower(pick: Pick, chosen: string[]) {
  return pick.powerNeed != null ? anyPowerFrom(pick.action.requirement, chosen) : powerFrom(pick.action.requirement, chosen);
}
