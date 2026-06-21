import type { ActionPromptDto, GameCommand } from "../../types/protocol";
import type { CandidateSelectionDraft } from "../../utils/candidateSelectionDraft";
import type { TableObjectContext } from "../../utils/tableObjectContext";
import { Button } from "../ui/Button";
import { useWireDialogFocus } from "./useWireDialogFocus";
import { WireTimelineDetailPanel, type WireTimelineDetail } from "./WireTimelineDetailPanel";
import type { WireObjectIndex } from "./WireObjectRefChips";

type WireTimelineDetailLayerProps = {
  detail?: WireTimelineDetail;
  disabledByConnection?: boolean;
  objectContextById?: Record<string, TableObjectContext>;
  objectIndex: WireObjectIndex;
  onChooseObject?: (objectId: string) => void;
  onCommand?: (command: GameCommand) => void;
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
  onCommand,
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
  const { closeButtonRef, dialogRef } = useWireDialogFocus(onClose, open);

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
          onCommand={onCommand}
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
