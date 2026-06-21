import { useEffect, useRef } from "react";
import type { ActionPromptDto } from "../../types/protocol";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import { Button } from "../ui/Button";
import { WireTimelineDetailPanel, type WireTimelineDetail } from "./WireTimelineDetailPanel";
import type { WireObjectIndex } from "./WireObjectRefChips";

type WireTimelineDetailLayerProps = {
  detail?: WireTimelineDetail;
  disabledByConnection?: boolean;
  objectContextById?: Record<string, TableObjectContext>;
  objectIndex: WireObjectIndex;
  onChooseObject?: (objectId: string) => void;
  onClear: () => void;
  onClose: () => void;
  onInspectObject?: (objectId: string) => void;
  onOpenObjectDetail?: (objectId: string) => void;
  open: boolean;
  prompt?: ActionPromptDto;
  selectionDraft?: CandidateSelectionDraft;
  selectedObjectContext?: TableObjectContext;
  selectedObjectId?: string;
};

export function WireTimelineDetailLayer({
  detail,
  disabledByConnection = false,
  objectContextById,
  objectIndex,
  onChooseObject,
  onClear,
  onClose,
  onInspectObject,
  onOpenObjectDetail,
  open,
  prompt,
  selectionDraft,
  selectedObjectContext,
  selectedObjectId
}: WireTimelineDetailLayerProps) {
  const closeButtonRef = useRef<HTMLButtonElement | null>(null);
  const dialogRef = useRef<HTMLElement | null>(null);
  const onCloseRef = useRef(onClose);
  const previousActiveElementRef = useRef<HTMLElement | null>(null);
  onCloseRef.current = onClose;

  useEffect(() => {
    if (!open) {
      return undefined;
    }

    previousActiveElementRef.current = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.setTimeout(() => closeButtonRef.current?.focus(), 0);

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onCloseRef.current();
        return;
      }

      if (event.key === "Tab") {
        trapDialogFocus(event, dialogRef.current);
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = previousOverflow;
      previousActiveElementRef.current?.focus();
      previousActiveElementRef.current = null;
    };
  }, [open]);

  if (!open || (!detail && !selectedObjectContext)) {
    return null;
  }

  const source = detail?.source ?? "object";

  return (
    <div
      aria-labelledby="wire-timeline-detail-layer-title"
      aria-modal="true"
      className="wire-timeline-detail-layer"
      data-wire-timeline-detail-layer-detail-id={detail?.id ?? ""}
      data-wire-timeline-detail-layer-source={source}
      data-wire-timeline-detail-layer-state="open"
      id="wire-timeline-detail-layer"
      role="dialog"
    >
      <button aria-label="关闭规则事件检查层" className="wire-timeline-detail-layer-scrim" onClick={onClose} type="button" />
      <aside className="wire-timeline-detail-dialog" ref={dialogRef} tabIndex={-1}>
        <header className="wire-timeline-detail-layer-header">
          <div>
            <span>规则事件检查层</span>
            <h2 id="wire-timeline-detail-layer-title">{detail?.title ?? "焦点对象规则上下文"}</h2>
          </div>
          <Button onClick={onClose} ref={closeButtonRef} variant="ghost">关闭检查层</Button>
        </header>
        <WireTimelineDetailPanel
          bodyId="wire-timeline-detail-layer-body"
          detail={detail}
          disabledByConnection={disabledByConnection}
          objectContextById={objectContextById}
          objectIndex={objectIndex}
          onChooseObject={onChooseObject}
          onClear={onClear}
          onInspectObject={onInspectObject}
          onOpenObjectDetail={onOpenObjectDetail}
          prompt={prompt}
          selectionDraft={selectionDraft}
          selectedObjectContext={selectedObjectContext}
          selectedObjectId={selectedObjectId}
        />
      </aside>
    </div>
  );
}

function trapDialogFocus(event: KeyboardEvent, root: HTMLElement | null) {
  if (!root) {
    return;
  }

  const focusable = Array.from(root.querySelectorAll<HTMLElement>(
    "a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex='-1'])"
  )).filter((element) => !element.hasAttribute("hidden") && element.offsetParent !== null);

  if (focusable.length === 0) {
    event.preventDefault();
    root.focus();
    return;
  }

  const first = focusable[0];
  const last = focusable[focusable.length - 1];
  const active = document.activeElement;

  if (event.shiftKey && active === first) {
    event.preventDefault();
    last.focus();
    return;
  }

  if (!event.shiftKey && active === last) {
    event.preventDefault();
    first.focus();
  }
}
