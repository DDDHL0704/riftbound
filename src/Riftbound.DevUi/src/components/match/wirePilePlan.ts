export type WirePileKind = "banished" | "graveyard" | "library" | "runeDeck";
export type WirePileFace = "empty" | "hidden-stack" | "public-top";

export type WirePilePlan = {
  capacity: "unbounded";
  count: number;
  face: WirePileFace;
  kind: WirePileKind;
  overflowCount: number;
  topObjectId?: string;
  visibleCount: 0 | 1;
};

export function buildWirePilePlan({
  count,
  ids = [],
  kind
}: {
  count?: number;
  ids?: string[];
  kind: WirePileKind;
}): WirePilePlan {
  const effectiveCount = Math.max(0, count ?? ids.length);
  const topObjectId = ids.at(-1);
  const publicPile = kind === "banished" || kind === "graveyard";
  const visibleCount: 0 | 1 = publicPile && topObjectId ? 1 : 0;
  return {
    capacity: "unbounded",
    count: effectiveCount,
    face: faceFor({ count: effectiveCount, publicPile, topObjectId }),
    kind,
    overflowCount: Math.max(0, effectiveCount - visibleCount),
    topObjectId: publicPile ? topObjectId : undefined,
    visibleCount
  };
}

function faceFor({
  count,
  publicPile,
  topObjectId
}: {
  count: number;
  publicPile: boolean;
  topObjectId?: string;
}): WirePileFace {
  if (publicPile && topObjectId) {
    return "public-top";
  }

  if (count > 0) {
    return "hidden-stack";
  }

  return "empty";
}
