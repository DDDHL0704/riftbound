import { BehaviorSpec } from "../../types/catalog";
import { CardObjectView } from "../../types/protocol";
import { costText, keywordsText, statusLabel } from "../../utils/formatters";
import { isHiddenObject } from "../../utils/hiddenInfo";
import { StatusPill } from "../ui/StatusPill";

type CardFaceProps = {
  objectId?: string;
  object?: CardObjectView;
  spec?: BehaviorSpec;
  compact?: boolean;
  selected?: boolean;
};

export function CardFace({ objectId, object, spec, compact = false, selected = false }: CardFaceProps) {
  const hidden = isHiddenObject(object) && !spec;
  if (hidden) {
    return (
      <article className={`card-face card-back ${selected ? "is-selected" : ""}`}>
        <div className="card-frame-top">未公开</div>
        <strong>卡背</strong>
        <span>{objectId ?? "隐藏对象"}</span>
      </article>
    );
  }

  const title = spec?.cardName ?? object?.cardNo ?? objectId ?? "未知卡牌";
  const power = object?.effectivePower ?? object?.power ?? object?.basePower;

  return (
    <article className={`card-face ${compact ? "card-compact" : ""} ${selected ? "is-selected" : ""}`}>
      <div className="card-frame-top">
        <span>{spec?.cardCategoryName ?? "对象"}</span>
        <span>{spec?.cardNo ?? object?.cardNo ?? objectId}</span>
      </div>
      <strong>{title}</strong>
      <div className="card-stat-row">
        <StatusPill tone="info">{costText(spec)}</StatusPill>
        {power != null && <StatusPill tone={object?.damage ? "warn" : "neutral"}>战力 {power}</StatusPill>}
      </div>
      {!compact && (
        <>
          <p className="card-rules">{spec?.officialText || "服务端未提供卡面规则文本。"}</p>
          <div className="card-keywords">{keywordsText(spec)}</div>
          <div className="card-meta-line">
            <span>所属：{object?.ownerId ?? "未知"}</span>
            <span>控制：{object?.controllerId ?? "未知"}</span>
            {object?.damage != null && <span>伤害：{object.damage}</span>}
          </div>
          <StatusPill tone={spec?.conformanceTier === "full-official-rule-pass" ? "good" : "warn"}>
            {statusLabel(spec?.status)}
          </StatusPill>
        </>
      )}
    </article>
  );
}
