import { useCallback, useEffect, useRef, useState } from "react";
import type { InspectedCard } from "../cards/CardFace";
import { CARD_PREVIEW_DELAY_MS, buildCardPreviewPlan } from "../../utils/cardPreviewPlan";

export function useDelayedWireCardPreview(delayMs = CARD_PREVIEW_DELAY_MS) {
  const [previewCard, setPreviewCard] = useState<InspectedCard | undefined>();
  const previewDelayRef = useRef<number | undefined>(undefined);

  const clearPreviewCard = useCallback(() => {
    if (previewDelayRef.current != null) {
      window.clearTimeout(previewDelayRef.current);
      previewDelayRef.current = undefined;
    }
    setPreviewCard(undefined);
  }, []);

  const queuePreviewCard = useCallback((card?: InspectedCard) => {
    clearPreviewCard();

    if (!card) {
      return;
    }

    previewDelayRef.current = window.setTimeout(() => {
      setPreviewCard(card);
      previewDelayRef.current = undefined;
    }, delayMs);
  }, [clearPreviewCard, delayMs]);

  useEffect(() => () => {
    if (previewDelayRef.current != null) {
      window.clearTimeout(previewDelayRef.current);
      previewDelayRef.current = undefined;
    }
  }, []);

  return { clearPreviewCard, previewCard, queuePreviewCard };
}

export function WireCardPreview({ card }: { card?: InspectedCard }) {
  const plan = buildCardPreviewPlan(card);
  if (plan.state !== "ready" || !plan.imageUrl) {
    return null;
  }

  return (
    <div
      aria-label={`预览 ${plan.title}`}
      className={`wire-card-preview ${plan.kind === "battlefield" ? "is-battlefield-preview" : ""}`}
      data-wire-card-preview-delay-ms={plan.delayMs}
      data-wire-card-preview-kind={plan.kind}
      data-wire-card-preview-object-id={plan.objectId ?? ""}
      data-wire-card-preview-orientation={plan.orientation}
      data-wire-card-preview-state={plan.state}
      role="presentation"
    >
      <img alt={plan.title} src={plan.imageUrl} />
    </div>
  );
}
