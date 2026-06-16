import { BattlefieldResolutionView, SnapshotDto } from "../types/protocol";
import { asArray, asNumber, asRecord, asString } from "./collections";
import { redactInternalText } from "./redaction";

export type BattlefieldTimelineItem = {
  id: string;
  battlefieldObjectId: string;
  kind: string;
  label: string;
  detail: string;
  tick: number;
};

const resolutionLabels: Record<string, string> = {
  CONQUERED: "征服",
  CONTROL_RESOLVED: "控制结算",
  HELD: "据守"
};

export function battlefieldResolutionTimeline(snapshot?: SnapshotDto): BattlefieldTimelineItem[] {
  return battlefieldResolutions(snapshot).map((resolution, index) => {
    const kind = resolution.kind ?? "";
    const battlefieldObjectId = resolution.battlefieldObjectId ?? "";
    return {
      id: resolution.resolutionId ?? `battlefield-resolution-${resolution.tick ?? 0}-${index}-${kind}-${battlefieldObjectId}`,
      battlefieldObjectId,
      kind,
      label: battlefieldResolutionLabel(kind),
      detail: battlefieldResolutionDetail(resolution),
      tick: resolution.tick ?? 0
    };
  });
}

export function battlefieldResolutionLabel(kind: string | undefined): string {
  if (!kind) {
    return "战场结果";
  }

  return resolutionLabels[kind] ?? (isProtocolToken(kind) ? "战场结果" : redactInternalText(kind));
}

function battlefieldResolutions(snapshot?: SnapshotDto): BattlefieldResolutionView[] {
  const timing = asRecord(snapshot?.timing);
  return asArray<Record<string, unknown>>(timing.battlefieldResolutions)
    .map((entry) => {
      const participantObjectIds = asArray<string>(entry.participantObjectIds).filter(isString);
      const relatedEventKinds = asArray<string>(entry.relatedEventKinds).filter(isString);
      return {
        resolutionId: asString(entry.resolutionId, ""),
        tick: asNumber(entry.tick, 0),
        kind: asString(entry.kind, ""),
        reason: asString(entry.reason, ""),
        battlefieldObjectId: asString(entry.battlefieldObjectId, ""),
        playerId: nullableString(entry.playerId),
        previousControllerId: nullableString(entry.previousControllerId),
        controllerId: nullableString(entry.controllerId),
        sourceObjectId: nullableString(entry.sourceObjectId),
        participantObjectIds,
        relatedEventKinds
      };
    })
    .filter((entry) => Boolean(entry.kind && entry.battlefieldObjectId));
}

function battlefieldResolutionDetail(resolution: BattlefieldResolutionView): string {
  const playerId = resolution.playerId || resolution.controllerId || "";
  const previousControllerId = resolution.previousControllerId || "";
  const controllerId = resolution.controllerId || "";

  switch (resolution.kind) {
    case "CONQUERED":
      return playerId ? `${playerId} 征服并进入得分检查` : "征服并进入得分检查";
    case "HELD":
      return playerId ? `${playerId} 据守并进入得分检查` : "据守并进入得分检查";
    case "CONTROL_RESOLVED":
      if (!controllerId) {
        return "战场变为无人控制";
      }
      if (previousControllerId && previousControllerId !== controllerId) {
        return `${previousControllerId} -> ${controllerId}`;
      }
      return `${controllerId} 确认控制`;
    default:
      return resolution.reason ? redactInternalText(resolution.reason) : "服务端战场结算";
  }
}

function nullableString(value: unknown): string | null {
  return typeof value === "string" && value.trim().length > 0 ? value : null;
}

function isString(value: unknown): value is string {
  return typeof value === "string" && value.trim().length > 0;
}

function isProtocolToken(value: string): boolean {
  return /^[A-Z0-9_:-]+$/.test(value);
}
