import type { ActionPromptDto } from "../types/protocol";

export type WireServerFlowProjectionState = "empty" | "linked";

export type WireServerFlowProjectionPlan = {
  objectIds: string[];
  relatedObjectCount: number;
  state: WireServerFlowProjectionState;
  summary: string;
  timelineByObjectId: Record<string, "rule" | undefined>;
};

export function buildWireServerFlowProjectionPlan(prompt?: ActionPromptDto): WireServerFlowProjectionPlan {
  const serverFlow = prompt?.serverFlow;
  const semanticObjectIds = serverFlow?.relatedObjects?.map((ref) => ref.objectId) ?? [];
  const objectIds = visibleRelatedObjectIds([
    ...(semanticObjectIds.length > 0 ? semanticObjectIds : serverFlow?.relatedObjectIds ?? []),
    serverFlow?.relatedBattlefieldId ?? ""
  ]);
  return {
    objectIds,
    relatedObjectCount: objectIds.length,
    state: objectIds.length > 0 ? "linked" : "empty",
    summary: objectIds.length > 0 ? `${objectIds.length} 个服务端关联对象` : "无服务端关联对象",
    timelineByObjectId: Object.fromEntries(objectIds.map((objectId) => [objectId, "rule" as const]))
  };
}

function visibleRelatedObjectIds(ids: readonly string[]): string[] {
  const objectIds: string[] = [];
  const seen = new Set<string>();
  for (const rawId of ids) {
    const objectId = rawId.trim();
    if (!objectId || objectId.toUpperCase() === "HIDDEN" || seen.has(objectId)) {
      continue;
    }

    seen.add(objectId);
    objectIds.push(objectId);
  }

  return objectIds;
}
