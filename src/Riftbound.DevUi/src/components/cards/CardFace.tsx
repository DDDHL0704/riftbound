import type { CSSProperties } from "react";
import { BehaviorSpec } from "../../types/catalog";
import { CardObjectView } from "../../types/protocol";
import { conformanceLabel, conformanceTone, costText, keywordsText, objectTypeText, rulesText, statusLabel } from "../../utils/formatters";
import { isHiddenObject } from "../../utils/hiddenInfo";
import type { PromptObjectState } from "../../utils/promptInteraction";
import { StatusPill } from "../ui/StatusPill";

type CardFaceProps = {
  className?: string;
  objectId?: string;
  object?: CardObjectView;
  spec?: BehaviorSpec;
  compact?: boolean;
  interactionHint?: CardFaceInteractionHint;
  interactionState?: PromptObjectState;
  timelineState?: "event" | "rule";
  selected?: boolean;
  style?: CSSProperties;
  onInspect?: (card: InspectedCard) => void;
  onPreview?: (card?: InspectedCard) => void;
};

export type CardFaceInteractionHint = {
  candidateLabels: string[];
  choiceLabels: string[];
  dataLabel: string;
  disabledCandidateCount: number;
  enabledCandidateCount: number;
  nextClickLabel: string;
  roleLabels: string[];
  semanticSummary: string;
};

export type InspectedCard = {
  objectId?: string;
  object?: CardObjectView;
  spec?: BehaviorSpec;
};

export function CardFace({ className = "", objectId, object, spec, compact = false, interactionHint, interactionState, timelineState, selected = false, style, onInspect, onPreview }: CardFaceProps) {
  const hidden = isHiddenObject(object) && !spec;
  const visualStateClasses = cardVisualStateClasses(object);
  const Container = onInspect ? "button" : "article";
  const previewCard = { objectId, object, spec };
  const dataProps = {
    "data-object-id": objectId,
    "data-prompt-candidate-count": interactionHint ? String(interactionHint.enabledCandidateCount) : undefined,
    "data-prompt-choice-labels": interactionHint?.choiceLabels.join("|") || undefined,
    "data-prompt-disabled-candidate-count": interactionHint ? String(interactionHint.disabledCandidateCount) : undefined,
    "data-prompt-next": interactionHint?.nextClickLabel,
    "data-prompt-role-labels": interactionHint?.roleLabels.join("|") || undefined,
    "data-prompt-roles": interactionHint?.dataLabel,
    "data-prompt-summary": interactionHint?.semanticSummary,
    "data-prompt-state": interactionState,
    "data-timeline-state": timelineState,
    "data-selected": selected ? "true" : undefined
  };
  const previewProps = onPreview
    ? {
        onBlur: () => onPreview(undefined),
        onFocus: () => onPreview(previewCard),
        onMouseEnter: () => onPreview(previewCard),
        onMouseLeave: () => onPreview(undefined)
      }
    : {};
  const containerProps = onInspect
    ? {
        type: "button" as const,
        onClick: () => onInspect({ objectId, object, spec }),
        ...previewProps
      }
    : previewProps;

  if (hidden) {
    return (
      <Container aria-label={cardAccessibilityLabel("未公开卡牌", undefined, interactionHint)} className={`card-face card-back ${visualStateClasses} ${selected ? "is-selected" : ""} ${interactionClass(interactionState)} ${timelineClass(timelineState)} ${className}`.trim()} style={style} title={interactionHint?.semanticSummary} {...dataProps} {...containerProps}>
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
  const cardNo = spec?.cardNo ?? object?.cardNo ?? undefined;
  const ariaLabel = cardAccessibilityLabel(title, cardNo, interactionHint);

  if (frontImage) {
    const battlefield = category === "战场";
    const cost = costText(spec);
    const showCost = cost !== "无费用";

    return (
      <Container
        aria-label={ariaLabel}
        className={`card-face card-image-only ${battlefield ? "card-battlefield-image" : ""} ${compact ? "card-compact" : ""} ${visualStateClasses} ${selected ? "is-selected" : ""} ${interactionClass(interactionState)} ${timelineClass(timelineState)} ${className}`.trim()}
        style={style}
        title={interactionHint?.semanticSummary}
        {...dataProps}
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
    <Container aria-label={ariaLabel} className={`card-face ${compact ? "card-compact" : ""} ${visualStateClasses} ${selected ? "is-selected" : ""} ${interactionClass(interactionState)} ${timelineClass(timelineState)} ${className}`.trim()} style={style} title={interactionHint?.semanticSummary} {...dataProps} {...containerProps}>
      <div className="card-frame-top">
        <span>{category}</span>
        <span>{cardNo ?? "无编号"}</span>
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

function interactionClass(state: CardFaceProps["interactionState"]): string {
  return state ? `is-prompt-${state}` : "";
}

function timelineClass(state: CardFaceProps["timelineState"]): string {
  return state ? `is-timeline-${state}` : "";
}

function cardVisualStateClasses(object?: CardObjectView): string {
  return [
    object?.isExhausted ? "is-card-exhausted" : "",
    object?.isAttacking ? "is-card-attacking" : "",
    object?.isDefending ? "is-card-defending" : "",
    (object?.damage ?? 0) > 0 ? "is-card-damaged" : ""
  ].filter(Boolean).join(" ");
}

function cardAccessibilityLabel(title: string, cardNo?: string, interactionHint?: CardFaceInteractionHint): string {
  return [
    title,
    cardNo,
    interactionHint?.semanticSummary
  ].filter(Boolean).join("。");
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
