import { anyPowerFrom, isRecycle, isTap, powerFrom, type ActionStep, type ObjectAction, type Selection } from "../../game/actions";

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
export const EXCLUSIVE_COST_PREFIXES = ["SHOWCASE:"];

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
