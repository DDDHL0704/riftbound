import type { BehaviorSpec } from "../../types/catalog";
import type { BattlefieldSnapshotView, CardObjectView, PlayerSnapshotView, SnapshotDto, ZoneView } from "../../types/protocol";
import { buildWireCardFlowPlan, type WireCardFlowPlan } from "./wireCardFlowPlan";

export type WirePlayerSide = "self" | "opponent";
export type WireZoneObjects = Record<string, CardObjectView | undefined>;
export type WireBasePartitionSource = "catalog-fallback" | "mixed" | "server";
export type WireBattlefieldOccupantSplitSource = "controller-fallback" | "server-unitsBySide";

export type WirePlayerEntry = {
  basePartitionSource: WireBasePartitionSource;
  baseObjectIds: string[];
  handIds: string[];
  hiddenHandIds: string[];
  id: string;
  label: string;
  objects: WireZoneObjects;
  player: PlayerSnapshotView;
  runeIds: string[];
  side: WirePlayerSide;
  zones: ZoneView;
};

export type WireBattlefieldLane = {
  battlefield?: BattlefieldSnapshotView;
  battlefieldId: string;
  cardNo: string;
  controllerId: string;
  index: number;
  occupantSplitSource: WireBattlefieldOccupantSplitSource;
  ownOccupants: string[];
  opposingOccupants: string[];
  zonePlayerId: string;
};

export type WireBattlefieldModel = {
  lanes: WireBattlefieldLane[];
  objects: WireZoneObjects;
  unitPlan: WireCardFlowPlan;
};

export type WireTableViewModel = {
  battlefield: WireBattlefieldModel;
  opponent?: WirePlayerEntry;
  players: WirePlayerEntry[];
  self?: WirePlayerEntry;
};

export function buildWireTableViewModel({
  perspectivePlayerId,
  snapshot,
  specs
}: {
  perspectivePlayerId: string;
  snapshot?: SnapshotDto;
  specs: Record<string, BehaviorSpec | undefined>;
}): WireTableViewModel {
  const players = buildWirePlayerEntries(snapshot, perspectivePlayerId, specs);
  return {
    battlefield: buildWireBattlefieldModel(snapshot, perspectivePlayerId),
    opponent: players.find((entry) => entry.side === "opponent"),
    players,
    self: players.find((entry) => entry.side === "self")
  };
}

export function buildWirePlayerEntries(
  snapshot: SnapshotDto | undefined,
  perspectivePlayerId: string,
  specs: Record<string, BehaviorSpec | undefined>
): WirePlayerEntry[] {
  return Object.entries(snapshot?.players ?? {})
    .map(([id, player]) => buildWirePlayerEntry(id, player, perspectivePlayerId, specs))
    .sort((left, right) => sideOrder(left.side) - sideOrder(right.side));
}

export function buildWireBattlefieldModel(
  snapshot: SnapshotDto | undefined,
  perspectivePlayerId: string
): WireBattlefieldModel {
  const battlefields = asArray<BattlefieldSnapshotView>(asRecord(snapshot?.lanes).battlefields);
  const objects = buildWireObjectIndex(snapshot);
  const lanes = [0, 1].map((index) => buildWireBattlefieldLane(battlefields[index], index, objects, perspectivePlayerId));
  const maxOccupants = Math.max(...lanes.flatMap((lane) => [lane.ownOccupants.length, lane.opposingOccupants.length]), 0);

  return {
    lanes,
    objects,
    unitPlan: buildWireCardFlowPlan({
      itemCount: maxOccupants,
      kind: "battlefield-unit",
      minSlots: 3
    })
  };
}

export function playerLabel(entry: WirePlayerEntry): string {
  return `${entry.side === "self" ? "P1 我方" : "P2 对手"} · ${entry.player.name ?? entry.id}`;
}

export function isRuneCard(object?: CardObjectView, spec?: Pick<BehaviorSpec, "cardCategoryName">): boolean {
  return Boolean(object?.tags?.includes("CARD_TYPE:RUNE") || spec?.cardCategoryName === "符文");
}

export function ownerOrController(object?: CardObjectView): string {
  return object?.controllerId || object?.ownerId || "";
}

function buildWirePlayerEntry(
  id: string,
  player: PlayerSnapshotView,
  perspectivePlayerId: string,
  specs: Record<string, BehaviorSpec | undefined>
): WirePlayerEntry {
  const side: WirePlayerSide = id === perspectivePlayerId ? "self" : "opponent";
  const zones = player.zones ?? {};
  const objects = player.objects ?? {};
  const baseIds = zones.base ?? [];
  const serverRuneIds = zonePartitionIds(zones.baseRunes, baseIds);
  const runeIds = serverRuneIds ?? baseIds.filter((objectId) => isRuneCard(objects[objectId], specs[objects[objectId]?.cardNo ?? ""]));
  const runeSet = new Set(runeIds);
  const serverBaseCardIds = zonePartitionIds(zones.baseCards, baseIds);
  const baseObjectIds = serverBaseCardIds ?? baseIds.filter((objectId) => !runeSet.has(objectId));

  const entry: WirePlayerEntry = {
    baseObjectIds,
    basePartitionSource: basePartitionSource(serverBaseCardIds, serverRuneIds),
    handIds: zones.hand ?? [],
    hiddenHandIds: hiddenCards(player.handSize ?? zones.handHidden ?? 0, id),
    id,
    label: "",
    objects,
    player,
    runeIds,
    side,
    zones
  };

  return {
    ...entry,
    label: playerLabel(entry)
  };
}

function buildWireBattlefieldLane(
  battlefield: BattlefieldSnapshotView | undefined,
  index: number,
  objects: WireZoneObjects,
  perspectivePlayerId: string
): WireBattlefieldLane {
  const occupants = asArray<string>(battlefield?.occupantObjectIds);
  const splitOccupants = splitBattlefieldOccupants(battlefield?.unitsBySide, occupants, objects, perspectivePlayerId);
  return {
    battlefield,
    battlefieldId: asString(battlefield?.battlefieldObjectId, `empty-battlefield-${index}`),
    cardNo: asString(battlefield?.cardNo, ""),
    controllerId: asString(battlefield?.controllerId, ""),
    index,
    occupantSplitSource: splitOccupants.source,
    ownOccupants: splitOccupants.own,
    opposingOccupants: splitOccupants.opposing,
    zonePlayerId: asString(battlefield?.zonePlayerId, "")
  };
}

function splitBattlefieldOccupants(
  unitsBySide: Record<string, string[]> | undefined,
  occupants: string[],
  objects: WireZoneObjects,
  perspectivePlayerId: string
): { own: string[]; opposing: string[]; source: WireBattlefieldOccupantSplitSource } {
  const sideMap = asStringArrayRecord(unitsBySide);
  if (sideMap) {
    const occupantSet = new Set(occupants);
    const ownSet = new Set((sideMap[perspectivePlayerId] ?? []).filter((id) => occupantSet.has(id)));
    return {
      own: occupants.filter((id) => ownSet.has(id)),
      opposing: occupants.filter((id) => !ownSet.has(id)),
      source: "server-unitsBySide"
    };
  }

  return {
    own: occupants.filter((id) => ownerOrController(objects[id]) === perspectivePlayerId),
    opposing: occupants.filter((id) => ownerOrController(objects[id]) !== perspectivePlayerId),
    source: "controller-fallback"
  };
}

function basePartitionSource(baseCardIds: string[] | undefined, runeIds: string[] | undefined): WireBasePartitionSource {
  if (baseCardIds && runeIds) {
    return "server";
  }

  if (baseCardIds || runeIds) {
    return "mixed";
  }

  return "catalog-fallback";
}

function buildWireObjectIndex(snapshot?: SnapshotDto): WireZoneObjects {
  const index: WireZoneObjects = {};
  for (const player of Object.values(snapshot?.players ?? {})) {
    for (const [objectId, object] of Object.entries(player.objects ?? {})) {
      index[objectId] = object;
    }
  }
  return index;
}

function hiddenCards(count: number, playerId: string): string[] {
  return Array.from({ length: count }, (_, index) => `hidden-${playerId}-${index}`);
}

function zonePartitionIds(ids: string[] | undefined, parentIds: string[]): string[] | undefined {
  if (!ids) {
    return undefined;
  }

  const parentSet = new Set(parentIds);
  return ids.filter((id) => parentSet.has(id));
}

function sideOrder(side: WirePlayerSide): number {
  return side === "opponent" ? 0 : 1;
}

function asArray<T>(value: unknown): T[] {
  return Array.isArray(value) ? value as T[] : [];
}

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

function asStringArrayRecord(value: unknown): Record<string, string[]> | undefined {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return undefined;
  }

  const result: Record<string, string[]> = {};
  for (const [key, rawValue] of Object.entries(value)) {
    if (typeof key !== "string" || !Array.isArray(rawValue)) {
      return undefined;
    }

    result[key] = rawValue.filter((item): item is string => typeof item === "string");
  }

  return result;
}

function asString(value: unknown, fallback = ""): string {
  return typeof value === "string" ? value : fallback;
}
