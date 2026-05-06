import { CardObjectView } from "../types/protocol";

export function isHiddenObject(object?: CardObjectView): boolean {
  return !object || object.isFaceDown === true || !object.cardNo;
}

export function publicObjectLabel(objectId: string, object?: CardObjectView): string {
  if (isHiddenObject(object)) {
    return "未公开卡牌";
  }

  return object?.cardNo ?? objectId;
}
