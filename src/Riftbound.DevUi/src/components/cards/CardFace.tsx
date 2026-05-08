import { BehaviorSpec } from "../../types/catalog";
import { CardObjectView } from "../../types/protocol";
import { conformanceLabel, conformanceTone, costText, keywordsText, objectTypeText, statusLabel } from "../../utils/formatters";
import { isHiddenObject } from "../../utils/hiddenInfo";
import { StatusPill } from "../ui/StatusPill";

type CardFaceProps = {
  objectId?: string;
  object?: CardObjectView;
  spec?: BehaviorSpec;
  compact?: boolean;
  selected?: boolean;
  onInspect?: (card: InspectedCard) => void;
};

export type InspectedCard = {
  objectId?: string;
  object?: CardObjectView;
  spec?: BehaviorSpec;
};

export function CardFace({ objectId, object, spec, compact = false, selected = false, onInspect }: CardFaceProps) {
  const hidden = isHiddenObject(object) && !spec;
  const Container = onInspect ? "button" : "article";
  const containerProps = onInspect
    ? {
        type: "button" as const,
        onClick: () => onInspect({ objectId, object, spec })
      }
    : {};

  if (hidden) {
    return (
      <Container className={`card-face card-back ${selected ? "is-selected" : ""}`} {...containerProps}>
        <div className="card-frame-top">未公开</div>
        <strong>卡背</strong>
        <span>隐藏信息</span>
      </Container>
    );
  }

  const title = spec?.cardName ?? object?.cardNo ?? objectId ?? "未知卡牌";
  const power = object?.effectivePower ?? object?.power ?? object?.basePower;
  const states = objectStateLabels(object);

  return (
    <Container className={`card-face ${compact ? "card-compact" : ""} ${selected ? "is-selected" : ""}`} {...containerProps}>
      <div className="card-frame-top">
        <span>{objectTypeText(object, spec)}</span>
        <span>{spec?.cardNo ?? object?.cardNo ?? objectId}</span>
      </div>
      <strong>{title}</strong>
      <div className="card-stat-row">
        <StatusPill tone="info">{costText(spec)}</StatusPill>
        {power != null && <StatusPill tone={object?.damage ? "warn" : "neutral"}>战力 {power}</StatusPill>}
      </div>
      {compact ? (
        <div className="card-mini-meta">
          <span>属 {object?.ownerId ?? "?"}</span>
          <span>控 {object?.controllerId ?? "?"}</span>
          <span>{states.length ? states.slice(0, 2).join("、") : "正常"}</span>
        </div>
      ) : (
        <>
          <p className="card-rules">{spec?.officialText || "服务端未提供卡面规则文本。"}</p>
          <div className="card-keywords">{keywordsText(spec)}</div>
          <div className="card-meta-line">
            <span>所属：{object?.ownerId ?? "未知"}</span>
            <span>控制：{object?.controllerId ?? "未知"}</span>
            {object?.damage != null && <span>伤害：{object.damage}</span>}
            {states.map((state) => <span key={state}>{state}</span>)}
          </div>
          <StatusPill tone={conformanceTone(spec?.conformanceTier)}>
            {conformanceLabel(spec?.conformanceTier)}
          </StatusPill>
          <StatusPill tone={spec?.status === "implemented" ? "info" : "warn"}>
            {statusLabel(spec?.status)}
          </StatusPill>
        </>
      )}
    </Container>
  );
}

export function objectStateLabels(object?: CardObjectView): string[] {
  if (!object) {
    return [];
  }

  return [
    object.isExhausted ? "横置" : "",
    object.isAttacking ? "攻击中" : "",
    object.isDefending ? "防守中" : "",
    object.isFaceDown ? "面朝下" : "",
    object.attachedToObjectId ? "已贴附" : "",
    object.damage != null && object.damage > 0 ? `${object.damage} 伤害` : "",
    object.basePower != null && object.effectivePower != null && object.basePower !== object.effectivePower
      ? `基础 ${object.basePower} / 有效 ${object.effectivePower}`
      : ""
  ].filter(Boolean);
}
