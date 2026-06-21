import type { CardObjectView } from "../../types/protocol";
import { redactInternalText } from "../../utils/redaction";

export type WireObjectRef = {
  battlefieldObjectId?: string | null;
  id: string;
  label?: string;
  role: string;
  visibility?: WireObjectRefVisibility;
  zone?: string | null;
};

export type WireObjectRefVisibility = "hidden" | "missing" | "visible";

export type WireObjectIndex = Record<string, CardObjectView>;

export type WireObjectRefRenderPlan = {
  battlefieldObjectId?: string;
  canInspect: boolean;
  dataObjectId: string;
  label: string;
  selected: boolean;
  visibility: WireObjectRefVisibility;
  zone?: string;
  zoneLabel: string;
};

export function WireObjectRefChips({
  className = "",
  objects,
  onInspectObject,
  refs,
  selectedObjectId,
  source
}: {
  className?: string;
  objects: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  refs: WireObjectRef[];
  selectedObjectId?: string;
  source: "candidate" | "event" | "rule";
}) {
  const visibleRefs = uniqueWireObjectRefs(refs);
  if (visibleRefs.length === 0) {
    return null;
  }

  return (
    <div className={`wire-object-refs wire-${source}-object-refs ${className}`.trim()} role="group" aria-label="关联桌面对象">
      {visibleRefs.map((ref) => {
        const plan = wireObjectRefRenderPlan({ objects, onInspectObject, ref, selectedObjectId });
        const dataProps = {
          "data-object-ref": plan.dataObjectId,
          "data-object-ref-inspectable": plan.canInspect ? "true" : "false",
          "data-object-ref-role": ref.role,
          "data-object-ref-visibility": plan.visibility,
          "data-object-ref-zone": plan.zone,
          "data-object-ref-zone-label": plan.zoneLabel,
          "data-object-ref-battlefield": plan.battlefieldObjectId,
          "data-event-object-ref": source === "event" ? plan.dataObjectId : undefined,
          "data-rule-object-ref": source === "rule" ? plan.dataObjectId : undefined,
          "data-candidate-object-ref": source === "candidate" ? plan.dataObjectId : undefined,
          "data-selected": plan.selected ? "true" : undefined
        };
        const classNameParts = [
          "wire-object-ref",
          `wire-${source}-object-ref`,
          `is-${plan.visibility}`,
          plan.selected ? "is-selected" : "",
          plan.canInspect ? "" : "is-disabled"
        ].filter(Boolean).join(" ");

        if (!plan.canInspect) {
          return (
            <span className={classNameParts} key={`${ref.role}-${ref.id}`} {...dataProps}>
              <ObjectRefChipContent plan={plan} />
            </span>
          );
        }

        return (
          <button
            className={classNameParts}
            key={`${ref.role}-${ref.id}`}
            onClick={() => onInspectObject?.(ref.id)}
            type="button"
            {...dataProps}
          >
            <ObjectRefChipContent plan={plan} />
          </button>
        );
      })}
    </div>
  );
}

export function wireObjectRefRenderPlan({
  objects,
  onInspectObject,
  ref,
  selectedObjectId
}: {
  objects: WireObjectIndex;
  onInspectObject?: (objectId: string) => void;
  ref: WireObjectRef;
  selectedObjectId?: string;
}): WireObjectRefRenderPlan {
  const hidden = ref.visibility === "hidden" || ref.id === "HIDDEN";
  const object = hidden ? undefined : objects[ref.id];
  const visibility = hidden ? "hidden" : ref.visibility ?? objectRefVisibility(ref.id, object);
  const canInspect = visibility === "visible" && Boolean(object && onInspectObject);
  const dataObjectId = hidden ? "HIDDEN" : ref.id;
  const selected = !hidden && selectedObjectId === ref.id;
  const objectLabel = hidden ? "隐藏对象" : ref.label ?? wireObjectLabel(ref.id, objects);
  const zone = hidden ? undefined : normalizeText(ref.zone) ?? normalizeText(object?.location?.zone);
  const battlefieldObjectId = hidden
    ? undefined
    : normalizeText(ref.battlefieldObjectId) ?? normalizeText(object?.location?.battlefieldObjectId);
  const zoneLabel = hidden ? "" : wireObjectZoneLabel(zone, battlefieldObjectId);

  return {
    battlefieldObjectId,
    canInspect,
    dataObjectId,
    label: `${ref.role} ${objectLabel}`,
    selected,
    visibility,
    zone,
    zoneLabel
  };
}

function ObjectRefChipContent({ plan }: { plan: WireObjectRefRenderPlan }) {
  return (
    <>
      <span className="wire-object-ref-main">{plan.label}</span>
      {plan.zoneLabel && <small className="wire-object-ref-zone">{plan.zoneLabel}</small>}
    </>
  );
}

export function wireObjectLabel(objectId: string | null | undefined, objects: WireObjectIndex): string {
  if (!objectId) {
    return "无";
  }

  if (objectId === "HIDDEN") {
    return "隐藏对象";
  }

  const object = objects[objectId];
  return object?.cardNo ? `${object.cardNo}` : idLabel(objectId);
}

export function wireObjectRef(role: string, id: string | null | undefined): WireObjectRef {
  return { id: id?.trim() ?? "", role };
}

export function wireObjectRefs(role: string, ids: string[] | undefined): WireObjectRef[] {
  return (ids ?? []).map((id) => wireObjectRef(role, id));
}

function objectRefVisibility(objectId: string, object: CardObjectView | undefined): WireObjectRefVisibility {
  if (objectId === "HIDDEN") {
    return "hidden";
  }

  return object ? "visible" : "missing";
}

function wireObjectZoneLabel(zone: string | undefined, battlefieldObjectId: string | undefined): string {
  if (!zone && !battlefieldObjectId) {
    return "";
  }

  const zoneLabel = zone ? zoneLabelFor(zone) : "未知区域";
  return battlefieldObjectId ? `${zoneLabel} / ${battlefieldObjectId}` : zoneLabel;
}

function zoneLabelFor(zone: string): string {
  switch (zone.toUpperCase()) {
    case "BASE":
      return "基地";
    case "BATTLEFIELD":
      return "战场";
    case "BANISHED":
      return "放逐区";
    case "DECK":
      return "牌库";
    case "GRAVEYARD":
      return "已打出";
    case "HAND":
      return "手牌";
    case "LEGEND":
    case "LEGEND_ZONE":
      return "传奇";
    case "CHAMPION":
    case "CHAMPION_ZONE":
      return "英雄";
    default:
      return zone;
  }
}

function normalizeText(value: string | null | undefined): string | undefined {
  const normalized = value?.trim();
  return normalized ? normalized : undefined;
}

export function uniqueWireObjectRefs(refs: WireObjectRef[]): WireObjectRef[] {
  const seen = new Set<string>();
  const unique: WireObjectRef[] = [];
  for (const item of refs) {
    const key = `${item.role}:${item.id}`;
    if (!item.id || seen.has(key)) {
      continue;
    }

    seen.add(key);
    unique.push(item);
  }

  return unique;
}

function idLabel(value: string): string {
  return isProtocolToken(value) ? "服务端对象" : redactInternalText(value);
}

function isProtocolToken(value: string): boolean {
  return /^[A-Z0-9_:-]+$/.test(value) || /^[a-z0-9]+(?:[-_:][a-z0-9]+)+$/.test(value);
}
