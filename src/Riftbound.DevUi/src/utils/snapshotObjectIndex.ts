import type { BattlefieldSnapshotView, CardObjectView, SnapshotDto } from "../types/protocol";
import { asArray, asRecord, asString } from "./collections";

export type SnapshotObjectIndex = Record<string, CardObjectView>;

export function buildCardObjectIndex(snapshot?: SnapshotDto): SnapshotObjectIndex {
  const indexed: SnapshotObjectIndex = {};

  for (const player of Object.values(snapshot?.players ?? {})) {
    for (const [objectId, object] of Object.entries(player.objects ?? {})) {
      indexed[object.objectId ?? objectId] = { ...object, objectId: object.objectId ?? objectId };
    }
  }

  for (const battlefield of asArray<BattlefieldSnapshotView>(asRecord(snapshot?.lanes).battlefields)) {
    const objectId = asString(battlefield.battlefieldObjectId, "");
    if (!objectId) {
      continue;
    }

    indexed[objectId] = {
      cardNo: battlefield.cardNo ?? null,
      controllerId: battlefield.controllerId ?? undefined,
      location: { kind: "BATTLEFIELD_SITE" },
      objectId,
      ownerId: battlefield.zonePlayerId,
      tags: ["CARD_TYPE:BATTLEFIELD"]
    };
  }

  return indexed;
}
