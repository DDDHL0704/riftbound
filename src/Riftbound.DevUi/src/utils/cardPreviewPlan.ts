import type { BehaviorSpec } from "../types/catalog";
import type { CardObjectView } from "../types/protocol";

export const CARD_PREVIEW_DELAY_MS = 680;

export type CardPreviewCard = {
  objectId?: string;
  object?: CardObjectView;
  spec?: BehaviorSpec;
};

export type CardPreviewKind = "battlefield" | "standard";

export type CardPreviewOrientation = "landscape-counterclockwise" | "portrait";

export type CardPreviewPlan = {
  delayMs: number;
  imageUrl?: string;
  kind: CardPreviewKind;
  objectId?: string;
  orientation: CardPreviewOrientation;
  state: "empty" | "ready";
  title: string;
};

export function buildCardPreviewPlan(card?: CardPreviewCard): CardPreviewPlan {
  const imageUrl = card?.spec?.frontImage?.trim();
  if (!card || !imageUrl) {
    return {
      delayMs: CARD_PREVIEW_DELAY_MS,
      kind: "standard",
      orientation: "portrait",
      state: "empty",
      title: "卡牌"
    };
  }

  const battlefield = isBattlefieldCard(card);
  return {
    delayMs: CARD_PREVIEW_DELAY_MS,
    imageUrl,
    kind: battlefield ? "battlefield" : "standard",
    objectId: card.objectId ?? card.object?.objectId,
    orientation: battlefield ? "landscape-counterclockwise" : "portrait",
    state: "ready",
    title: card.spec?.cardName ?? card.object?.cardNo ?? "卡牌"
  };
}

function isBattlefieldCard(card: CardPreviewCard): boolean {
  return card.spec?.cardCategoryName === "战场" || Boolean(card.object?.tags?.includes("CARD_TYPE:BATTLEFIELD"));
}
