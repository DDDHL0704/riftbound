import type { CardObjectView } from "../../types/protocol";
import { redactInternalText } from "../../utils/redaction";

export type WireObjectRef = {
  id: string;
  label?: string;
  role: string;
  visibility?: WireObjectRefVisibility;
};

export type WireObjectRefVisibility = "hidden" | "missing" | "visible";

export type WireObjectIndex = Record<string, CardObjectView>;

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
        const object = objects[ref.id];
        const hidden = ref.id === "HIDDEN";
        const visibility = ref.visibility ?? objectRefVisibility(ref.id, object);
        const canInspect = Boolean(object && onInspectObject && !hidden);
        const selected = selectedObjectId === ref.id;
        const label = `${ref.role} ${ref.label ?? wireObjectLabel(ref.id, objects)}`;
        const dataProps = {
          "data-object-ref": ref.id,
          "data-object-ref-inspectable": canInspect ? "true" : "false",
          "data-object-ref-role": ref.role,
          "data-object-ref-visibility": visibility,
          "data-event-object-ref": source === "event" ? ref.id : undefined,
          "data-rule-object-ref": source === "rule" ? ref.id : undefined,
          "data-candidate-object-ref": source === "candidate" ? ref.id : undefined,
          "data-selected": selected ? "true" : undefined
        };
        const classNameParts = [
          "wire-object-ref",
          `wire-${source}-object-ref`,
          `is-${visibility}`,
          selected ? "is-selected" : "",
          canInspect ? "" : "is-disabled"
        ].filter(Boolean).join(" ");

        if (!canInspect) {
          return (
            <span className={classNameParts} key={`${ref.role}-${ref.id}`} {...dataProps}>
              {label}
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
            {label}
          </button>
        );
      })}
    </div>
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
