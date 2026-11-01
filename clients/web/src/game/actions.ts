import type { Candidate, Choice, Prompt } from "./types";

/**
 * One selectable step of an action, e.g. "choose target 1" or "choose where to move".
 * Choices always come from the server prompt; the client never invents legal options.
 */
export type ActionStep = {
  key: string;
  /** `move` picks where the matching target goes, for spells that move each target separately. */
  role: "target" | "destination" | "move" | "cost";
  label: string;
  required: boolean;
  choices: Choice[];
};

/** An action anchored to a source object (a hand card, unit, rune, legend…). */
export type ObjectAction = {
  key: string;
  cmdType: string;
  label: string;
  detail?: string;
  sourceObjectId: string;
  candidate: Candidate;
  requirement?: Record<string, any>;
  steps: ActionStep[];
  /** Costs the server marks as required are always sent; the rest are opt-in. */
  requiredCosts: string[];
  /** Extra costs the player may opt into; rune taps and recycles are listed apart. */
  optionalCosts: Choice[];
  /** Runes the command itself may tap for mana. */
  taps: string[];
  /** Runes the command itself may recycle for power. */
  recycles: Choice[];
  /** Mana the pool can't cover yet; the command taps runes for it. */
  manaShortfall: number;
  /** Power the pool can't cover yet; the player picks runes to recycle for it. */
  powerShortfall: number;
  /** Taps and recycles preselected to cover the shortfalls (the player may change the recycles). */
  presetCosts: string[];
  /** The prompt this action was offered in; a submission after that prompt changed is stale. */
  promptId?: string;
};

type PowerByChoice = Record<string, { power?: number; trait?: string }>;

export const isRecycle = (id: string) => id.startsWith("RECYCLE_RUNE:");
export const isTap = (id: string) => id.startsWith("TAP_RUNE:");

/** Power gained by the given payment choices that can pay a cost of the given traits. */
export function powerFrom(req: Record<string, any> | undefined, chosen: string[]) {
  const byChoice: PowerByChoice = req?.paymentResourcePowerByChoice ?? {};
  const traits: string[] = req?.printedPowerTraits ?? Object.keys(req?.powerCostByTrait ?? {});
  return chosen.reduce((n, id) => {
    const p = byChoice[id];
    return p && (!traits.length || !p.trait || traits.includes(p.trait)) ? n + (p.power ?? 0) : n;
  }, 0);
}

/** Power from recycle choices regardless of trait, for generic costs such as targeting taxes. */
export function anyPowerFrom(req: Record<string, any> | undefined, chosen: string[]) {
  const byChoice: PowerByChoice = req?.paymentResourcePowerByChoice ?? {};
  return chosen.reduce((n, id) => n + (byChoice[id]?.power ?? 0), 0);
}

/** A rune with no tap choice is already tapped, so recycling it first throws no mana away. */
export function recyclesByLoss(recycles: string[], taps: string[]) {
  const tappable = new Set(taps.map((id) => id.split(":").pop()));
  return [...recycles].sort((a, b) => Number(tappable.has(a.split(":").pop())) - Number(tappable.has(b.split(":").pop())));
}

function presetRecycles(req: Record<string, any>, recycles: Choice[], taps: string[], shortfall: number) {
  const out: string[] = [];
  for (const id of recyclesByLoss(recycles.map((c) => c.id), taps)) {
    if (powerFrom(req, out) >= shortfall) break;
    if (powerFrom(req, [id]) > 0) out.push(id);
  }
  return out;
}

function uniqueChoices(choices: Choice[]) {
  return choices.filter((c, i) => choices.findIndex((x) => x.id === c.id) === i);
}

export type Selection = {
  targets: string[];
  destination?: string;
  /** One destination per target, in target order (BASE:<player> or BATTLEFIELD:<id>). */
  movements?: string[];
  optionalCosts: string[];
};

const ANCHORED = new Set([
  "PLAY_CARD",
  "MOVE_UNIT",
  "TAP_RUNE",
  "RECYCLE_RUNE",
  "ACTIVATE_ABILITY",
  "LEGEND_ACT",
  "HIDE_CARD",
  "REVEAL_CARD",
  "ASSEMBLE_EQUIPMENT",
]);

const VERB: Record<string, string> = {
  PLAY_CARD: "打出",
  MOVE_UNIT: "移动",
  TAP_RUNE: "横置 +1 法力",
  RECYCLE_RUNE: "回收 +1 符能",
  ACTIVATE_ABILITY: "技能",
  LEGEND_ACT: "传奇技能",
  HIDE_CARD: "布置待命",
  REVEAL_CARD: "翻开待命",
  ASSEMBLE_EQUIPMENT: "装配",
};

function targetSteps(req: Record<string, any>, label: string): ActionStep[] {
  const byIndex: Record<string, Choice[]> = req.targetChoicesByIndex || {};
  const moves: Record<string, Choice[]> | null = req.movementDestinationChoicesByIndex ?? null;
  const max = Number(req.maxTargetCount ?? Object.keys(byIndex).length);
  const min = Number(req.minTargetCount ?? 0);
  const steps: ActionStep[] = [];
  for (let i = 0; i < max; i++) {
    const choices = byIndex[String(i)] || [];
    if (!choices.length && i >= min) continue;
    steps.push({
      key: `target-${i}`,
      role: "target",
      label: max > 1 ? `${label} ${i + 1}` : label,
      required: i < min,
      choices,
    });
    if (moves?.[String(i)]?.length) {
      steps.push({ key: `move-${i}`, role: "move", label: `选择第 ${i + 1} 个目标移动到哪里`, required: true, choices: moves[String(i)] });
    }
  }
  return steps;
}

function destinationStep(choices: Choice[] | undefined, label: string): ActionStep[] {
  if (!choices || choices.length === 0) return [];
  return [{ key: "destination", role: "destination", label, required: true, choices }];
}

function costLabel(req: Record<string, any>) {
  const parts: string[] = [];
  const mana = req.minimumManaCost ?? req.manaCost;
  if (typeof mana === "number" && mana > 0) parts.push(`${mana} 法力`);
  const power = req.printedPowerCost ?? req.powerCost;
  if (typeof power === "number" && power > 0) parts.push(`${power} 符能`);
  if (typeof req.experienceCost === "number" && req.experienceCost > 0) parts.push(`${req.experienceCost} 经验`);
  return parts.join(" + ");
}

function fromRequirement(candidate: Candidate, req: Record<string, any>, index: number): ObjectAction | null {
  if (req.composable === false) return null;
  const action = candidate.action;
  const sourceObjectId: string = req.sourceObjectId;
  if (!sourceObjectId) return null;
  let steps: ActionStep[] = [];
  let label = VERB[action] ?? candidate.label;
  let detail: string | undefined;

  switch (action) {
    case "PLAY_CARD":
    case "REVEAL_CARD":
      steps = [...targetSteps(req, req.targetScopeLabel ? `选择${req.targetScopeLabel}` : "选择目标")];
      if ((req.destinationChoices?.length ?? 0) > 1) steps.push(...destinationStep(req.destinationChoices, req.destinationLabel || "选择位置"));
      if (req.mode && req.modeLabel && req.modeLabel !== "默认") label = `${label} · ${req.modeLabel}`;
      detail = costLabel(req) || "免费";
      break;
    case "HIDE_CARD":
      steps = destinationStep(req.destinationChoices, "选择待命位置");
      break;
    case "MOVE_UNIT":
      steps = destinationStep(req.destinationChoices, "选择移动目的地");
      label = req.modeLabel ? `移动 · ${req.modeLabel}` : label;
      break;
    case "ACTIVATE_ABILITY":
    case "LEGEND_ACT":
      steps = targetSteps(req, req.targetScopeLabel && req.targetScopeLabel !== "无目标" ? `选择${req.targetScopeLabel}` : "选择目标");
      label = req.abilityLabel || label;
      detail = costLabel(req) || undefined;
      break;
    case "ASSEMBLE_EQUIPMENT":
      steps = req.targetChoices?.length
        ? [{ key: "target-0", role: "target", label: "选择装配单位", required: true, choices: req.targetChoices }]
        : [];
      break;
  }

  const costChoices: Choice[] = req.optionalCostChoices ?? [];
  const resources = uniqueChoices([...costChoices, ...(req.paymentResourceChoices ?? [])]);
  const taps = resources.filter((c) => isTap(c.id)).map((c) => c.id);
  const recycles = resources.filter((c) => isRecycle(c.id));
  const extras = costChoices.filter((c) => !isTap(c.id) && !isRecycle(c.id));
  // Some costs take exactly one option (the server rejects zero or several). Recycling a graveyard
  // card for an ability is one of them, though the requirement doesn't mark it yet.
  const pickOne = Number(req.requiredOptionalCostChoiceCount) === 1 || (action === "ACTIVATE_ABILITY" && extras.length > 0 && extras.every((c) => c.id.startsWith("RECYCLE_GRAVEYARD_CARD:")));
  if (pickOne && extras.length > 1) steps.push({ key: "cost", role: "cost", label: action === "HIDE_CARD" ? "选择待命费用" : "选择费用", required: true, choices: extras });
  const required: string[] = pickOne && extras.length === 1 ? [extras[0].id] : req.requiredOptionalCosts ?? [];
  // With a cost per option the shortfall is only known once the option is chosen.
  const perOption = Boolean(req.costsByChoice);
  const manaCost = Number(req.minimumManaCost ?? req.manaCost ?? 0);
  const manaShortfall = !perOption && typeof req.availableMana === "number" ? Math.min(taps.length, Math.max(0, manaCost - req.availableMana)) : 0;
  const shortfall = perOption ? 0 : Math.max(0, Number(req.minimumPrintedPowerShortfall ?? (typeof req.availablePower === "number" && req.powerCost ? req.powerCost - req.availablePower : 0)));
  if (manaShortfall > 0) detail = `${detail ? `${detail} · ` : ""}自动横置 ${manaShortfall} 枚符文`;
  return {
    key: `${action}:${sourceObjectId}:${index}`,
    cmdType: action,
    label,
    detail,
    sourceObjectId,
    candidate,
    requirement: req,
    steps,
    requiredCosts: required,
    optionalCosts: pickOne ? [] : extras.filter((c) => !required.includes(c.id)),
    taps,
    recycles,
    manaShortfall,
    powerShortfall: shortfall,
    presetCosts: [...taps.slice(0, manaShortfall), ...(shortfall > 0 ? presetRecycles(req, recycles, taps, shortfall) : [])],
  };
}

/** Every enabled, object-anchored action in the prompt, grouped by source object id. */
export function objectActions(prompt: Prompt | null): Map<string, ObjectAction[]> {
  const map = new Map<string, ObjectAction[]>();
  const add = (a: ObjectAction) => {
    const list = map.get(a.sourceObjectId) ?? [];
    list.push({ ...a, promptId: prompt?.promptId });
    map.set(a.sourceObjectId, list);
  };
  for (const candidate of prompt?.candidates ?? []) {
    if (!candidate.enabled || !ANCHORED.has(candidate.action) || !candidate.commandTemplate) continue;
    const reqs: Record<string, any>[] | undefined = candidate.metadata?.sourceRequirements;
    if (reqs?.length) {
      reqs.forEach((req, i) => {
        const a = fromRequirement(candidate, req, i);
        if (a) add(a);
      });
    } else {
      for (const source of candidate.sources ?? []) {
        add({
          key: `${candidate.action}:${source.id}`,
          cmdType: candidate.action,
          label: VERB[candidate.action] ?? candidate.label,
          sourceObjectId: source.id,
          candidate,
          steps: [],
          requiredCosts: [],
          optionalCosts: [],
          taps: [],
          recycles: [],
          manaShortfall: 0,
          powerShortfall: 0,
          presetCosts: [],
        });
      }
    }
  }
  return map;
}

/** Fills the server's command template with the player's selection. */
export function buildCommand(action: ObjectAction, sel: Selection): Record<string, unknown> {
  const template = action.candidate.commandTemplate!;
  const req = action.requirement ?? {};
  const cmd: Record<string, unknown> = { cmdType: template.cmdType };
  const destination = sel.destination ?? (req.destinationChoices?.length === 1 ? req.destinationChoices[0].id : undefined);

  for (const b of template.bindings) {
    let value: unknown;
    switch (b.source) {
      case "selectedSource":
        value = action.sourceObjectId;
        break;
      case "selectedTarget":
        value = sel.targets[0];
        break;
      case "selectedTargets":
        value = sel.targets;
        break;
      case "selectedDestination":
        value = destination;
        break;
      case "selectedMode":
        value = req.mode ?? undefined;
        break;
      case "selectedOptionalCosts": {
        // A rune may be tapped and then recycled for one payment, never the other way round.
        const costs = [...action.requiredCosts, ...sel.optionalCosts];
        value = [...costs.filter((c) => !isRecycle(c)), ...costs.filter(isRecycle)];
        break;
      }
      case "requirementMetadata":
      case "candidateMetadata": {
        const bag = b.source === "requirementMetadata" ? req : action.candidate.metadata ?? {};
        for (const k of b.metadataKeys ?? (b.metadataKey ? [b.metadataKey] : [])) {
          if (bag[k] != null) {
            value = bag[k];
            break;
          }
        }
        break;
      }
    }
    if (b.asArray && value != null && !Array.isArray(value)) value = [value];
    if (value == null || value === "") continue;
    if (Array.isArray(value) && value.length === 0 && b.omitEmpty && !b.required) continue;
    cmd[b.field] = value;
  }
  // Sequential-movement spells name a destination per target instead of one shared destination.
  if (req.movementDestinationChoicesByIndex && sel.movements?.length) {
    delete cmd.destination;
    cmd.movementDestinations = sel.movements;
  }
  return cmd;
}

/** Fill the remaining steps automatically when every one has exactly one legal choice. */
export function autoSelection(action: ObjectAction): Selection | null {
  const sel: Selection = { targets: [], optionalCosts: [] };
  for (const step of action.steps) {
    if (!step.required) continue;
    if (step.choices.length !== 1) return null;
    if (step.role === "destination") sel.destination = step.choices[0].id;
    else if (step.role === "cost") sel.optionalCosts.push(step.choices[0].id);
    else sel.targets.push(step.choices[0].id);
  }
  return action.steps.every((s) => s.required) ? sel : null;
}

export function enabledCandidate(prompt: Prompt | null, action: string) {
  return prompt?.candidates?.find((c) => c.action === action && c.enabled);
}

/** Object ids a choice refers to; destination ids look like "BATTLEFIELD:<id>". */
export function choiceObjectIds(choice: Choice): string[] {
  const ids = new Set<string>(choice.objectIds ?? []);
  ids.add(choice.id);
  const tail = choice.id.split(":").pop();
  if (tail) ids.add(tail);
  return [...ids];
}
