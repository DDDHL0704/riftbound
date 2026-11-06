import type { Choice } from "./types";

/*
 * Default answers to the server's structured prompts, shared by the table's panels (as the
 * suggestion a player starts from) and the practice opponent (as its answer).
 */

/** The first distinct choice at each index, e.g. every unit that must fight in a battle. */
export function fillByIndex(byIndex: Record<string, Choice[]> | undefined, count: number) {
  const out: string[] = [];
  for (let i = 0; i < count; i++) {
    const c = (byIndex?.[String(i)] ?? []).find((x) => !out.includes(x.id));
    if (c) out.push(c.id);
  }
  return out;
}

export type Assignment = { sourceObjectId: string; targetObjectId: string; damage: number };
export type Required = { sourceObjectId: string; damage: number; legalTargetObjectIds: string[] };

export function suggestCombat(md: Record<string, any>): Assignment[] {
  const required: Required[] = md.requiredAssignments ?? [];
  const want: Record<string, number> = { ...(md.suggestedDamageByTarget ?? {}) };
  const out: Assignment[] = [];
  for (const r of required) {
    let left = r.damage;
    const order = [...r.legalTargetObjectIds].sort((a, b) => (want[b] ?? 0) - (want[a] ?? 0));
    for (const t of order) {
      if (left <= 0) break;
      const take = Math.min(left, want[t] ?? 0);
      if (take > 0) {
        out.push({ sourceObjectId: r.sourceObjectId, targetObjectId: t, damage: take });
        want[t] -= take;
        left -= take;
      }
    }
    if (left > 0 && order[0]) {
      const existing = out.find((a) => a.sourceObjectId === r.sourceObjectId && a.targetObjectId === order[0]);
      if (existing) existing.damage += left;
      else out.push({ sourceObjectId: r.sourceObjectId, targetObjectId: order[0], damage: left });
    }
  }
  return out;
}

/** Effect damage must hit exactly `minimumAssignedTargets` targets with at least 1 each. */
export function suggestEffect(md: Record<string, any>): Assignment[] {
  const r: Required | undefined = md.requiredAssignments?.[0];
  if (!r) return [];
  const count = Math.min(Number(md.minimumAssignedTargets ?? 1), r.legalTargetObjectIds.length, r.damage);
  const out = r.legalTargetObjectIds.slice(0, count).map((t) => ({ sourceObjectId: r.sourceObjectId, targetObjectId: t, damage: 1 }));
  if (out[0]) out[0].damage += r.damage - out.length;
  return out;
}
