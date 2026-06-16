import { BehaviorSpec } from "../../types/catalog";
import { CardObjectView } from "../../types/protocol";
import { conformanceLabel, conformanceTone, costText, keywordsText, objectTypeText, rulesText, statusLabel } from "../../utils/formatters";
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
      <Container aria-label="未公开卡牌" className={`card-face card-back ${selected ? "is-selected" : ""}`} {...containerProps}>
        <div className="card-frame-top">未公开</div>
        <strong>卡背</strong>
        <span>隐藏信息</span>
      </Container>
    );
  }

  const title = spec?.cardName ?? object?.cardNo ?? "未知卡牌";
  const category = objectTypeText(object, spec);
  const power = object?.effectivePower ?? object?.power ?? object?.basePower;
  const states = objectStateLabels(object);
  const frontImage = spec?.frontImage?.trim();
  const ruleCopy = rulesText(spec?.officialText);
  const keywordCopy = keywordsText(spec);

  if (frontImage) {
    const battlefield = category === "战场";
    const cost = costText(spec);
    const showCost = cost !== "无费用";

    return (
      <Container
        aria-label={`${title} ${spec?.cardNo ?? object?.cardNo ?? ""}`.trim()}
        className={`card-face card-image-only ${battlefield ? "card-battlefield-image" : ""} ${compact ? "card-compact" : ""} ${selected ? "is-selected" : ""}`}
        {...containerProps}
      >
        <img alt="" aria-hidden="true" className="card-full-image" loading="lazy" src={frontImage} />
        {showCost && <span className="card-image-cost">{cost}</span>}
        {power != null && <span className={object?.damage ? "card-image-power is-damaged" : "card-image-power"}>{power}</span>}
        <span className="card-image-title">{title}</span>
        <span className="card-zoom-preview" aria-hidden="true">
          <img alt="" loading="lazy" src={frontImage} />
        </span>
      </Container>
    );
  }

  return (
    <Container className={`card-face ${compact ? "card-compact" : ""} ${selected ? "is-selected" : ""}`} {...containerProps}>
      <div className="card-frame-top">
        <span>{category}</span>
        <span>{spec?.cardNo ?? object?.cardNo ?? "无编号"}</span>
      </div>
      <div className="card-title-row">
        <strong title={title}>{title}</strong>
        {power != null && <span className={object?.damage ? "card-power is-damaged" : "card-power"}>{power}</span>}
      </div>
      <div className="card-art-fallback">
        <span>{category}</span>
      </div>
      <div className="card-stat-row">
        <StatusPill tone="info">{costText(spec)}</StatusPill>
        {keywordCopy !== "无关键词" && <StatusPill tone="neutral">{keywordCopy}</StatusPill>}
      </div>
      {compact ? (
        <>
          <p className="card-rules card-rules-compact">{ruleCopy}</p>
          <div className="card-mini-meta">
            <span>{states.length ? states.slice(0, 2).join("、") : "状态：正常"}</span>
          </div>
        </>
      ) : (
        <>
          <p className="card-rules">{ruleCopy}</p>
          <div className="card-keywords">{keywordCopy}</div>
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
