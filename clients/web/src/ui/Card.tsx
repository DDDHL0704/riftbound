import type { CSSProperties, MouseEvent, PointerEvent } from "react";
import { motion } from "motion/react";
import { cardInfo, COLOR_HEX, tokenizeEffect } from "../game/cards";
import type { GameObject } from "../game/types";

export type CardMark = "selected" | "legal" | "playable" | "autopay" | "attacking" | "defending" | "dragging" | null;

export type HoverHandler = (cardNo: string | null, object?: GameObject, rect?: DOMRect) => void;

type CardProps = {
  cardNo?: string | null;
  object?: GameObject;
  size?: "hand" | "board" | "hero" | "mini" | "rune";
  faceDown?: boolean;
  /** Face-down to everyone else, but this viewer knows the card (own standby). */
  peek?: boolean;
  marks?: CardMark[];
  onClick?: (e: MouseEvent) => void;
  onPointerDown?: (e: PointerEvent) => void;
  onHover?: HoverHandler;
  style?: CSSProperties;
  title?: string;
  /** Lets the same object animate between zones (hand → base → battlefield). */
  animate?: boolean;
};

const SPRING = { type: "spring", stiffness: 520, damping: 42, mass: 0.8 } as const;

export function Card({ cardNo, object, size = "board", faceDown, peek, marks = [], onClick, onPointerDown, onHover, style, title, animate = true }: CardProps) {
  const no = cardNo ?? object?.cardNo ?? null;
  const info = cardInfo(no);
  const hidden = !peek && (faceDown || object?.isFaceDown || !no);
  const exhausted = Boolean(object?.isExhausted);
  const power = object?.effectivePower ?? info?.power;
  const showStats = size !== "mini" && size !== "rune" && object && object.tags?.includes("CARD_TYPE:UNIT");
  const buffed = object && object.effectivePower !== object.basePower;
  const cls = [
    "card",
    `card--${size}`,
    hidden ? "card--back" : "",
    peek ? "card--peek" : "",
    exhausted ? "card--exhausted" : "",
    onClick ? "card--interactive" : "",
    ...marks.filter(Boolean).map((m) => `card--${m}`),
  ].join(" ");

  return (
    <motion.div
      layout={animate ? "position" : false}
      layoutId={animate && object ? object.objectId : undefined}
      transition={SPRING}
      className={`card-slot card-slot--${size}${exhausted ? " card-slot--exhausted" : ""}`}
      style={style}
    >
      <button
        type="button"
        className={cls}
        onClick={onClick}
        onPointerDown={onPointerDown}
        disabled={!onClick && !onPointerDown}
        title={title ?? (hidden ? "背面朝上" : undefined)}
        onMouseEnter={(e) => onHover?.(hidden ? null : no, object, e.currentTarget.getBoundingClientRect())}
        onMouseLeave={() => onHover?.(null)}
        aria-label={hidden ? "背面朝上的卡牌" : info?.name ?? no ?? "卡牌"}
      >
        {hidden ? (
          <span className="card__back" />
        ) : info?.image ? (
          <img className="card__art" src={info.image} alt="" loading="lazy" draggable={false} />
        ) : (
          <span className="card__fallback">
            <span className="card__fallback-name">{info?.name ?? no}</span>
            {info?.cost != null && <span className="card__fallback-cost">{info.cost}</span>}
          </span>
        )}
        {peek && <span className="card__ribbon">待命</span>}
        {showStats && !hidden && <span className={`card__power${buffed ? " card__power--changed" : ""}`}>{power}</span>}
        {object && object.damage > 0 && <span className="card__damage">{object.damage}</span>}
      </button>
    </motion.div>
  );
}

export function EffectText({ text }: { text: string }) {
  return (
    <span className="effect">
      {tokenizeEffect(text).map((t, i) => {
        switch (t.kind) {
          case "text":
            return <span key={i}>{t.text}</span>;
          case "mana":
            return (
              <span key={i} className="sym sym--mana">
                {t.value}
              </span>
            );
          case "might":
            return (
              <span key={i} className="sym sym--might" title="战力">
                战力
              </span>
            );
          case "power":
            return <span key={i} className="sym sym--power" title="符能" style={{ background: t.color ? COLOR_HEX[t.color] : undefined }} />;
          case "exhaust":
            return (
              <span key={i} className="sym sym--exhaust" title="横置">
                ⟳
              </span>
            );
          case "keyword":
            return (
              <span key={i} className="sym sym--keyword">
                {t.text}
              </span>
            );
        }
      })}
    </span>
  );
}

/** Large card image beside the hovered card, kept inside the viewport. */
export function HoverPreview({ cardNo, object, rect }: { cardNo: string; object?: GameObject; rect?: DOMRect }) {
  const info = cardInfo(cardNo);
  if (!info || !rect) return null;
  const width = 280;
  const height = Math.round(width * 1.398);
  const gap = 18;
  const vw = window.innerWidth;
  const vh = window.innerHeight;
  const left = rect.right + gap + width < vw - 8 ? rect.right + gap : Math.max(8, rect.left - gap - width);
  const top = Math.min(Math.max(8, rect.top + rect.height / 2 - height / 2), vh - height - 8);
  const unit = object?.tags?.includes("CARD_TYPE:UNIT");
  return (
    <motion.aside
      className="hoverpreview"
      style={{ left, top, width }}
      initial={{ opacity: 0, scale: 0.94, y: 6 }}
      animate={{ opacity: 1, scale: 1, y: 0 }}
      transition={{ duration: 0.14, ease: "easeOut" }}
      aria-hidden
    >
      {info.image ? <img src={info.image} alt="" /> : <div className="hoverpreview__fallback">{info.name}</div>}
      {object && (unit || object.damage > 0 || object.isExhausted) && (
        <p className="hoverpreview__state">
          {unit && (
            <span>
              战力 <b>{object.effectivePower}</b>
              {object.effectivePower !== object.basePower && <i>（基础 {object.basePower}）</i>}
            </span>
          )}
          {object.damage > 0 && <span className="hoverpreview__dmg">已受 {object.damage} 点伤害</span>}
          {object.isExhausted && <span>已横置</span>}
        </p>
      )}
    </motion.aside>
  );
}
