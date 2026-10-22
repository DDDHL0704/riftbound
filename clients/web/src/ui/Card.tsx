import type { CSSProperties, MouseEvent } from "react";
import { cardInfo, COLOR_HEX, tokenizeEffect } from "../game/cards";
import type { GameObject } from "../game/types";

export type CardMark = "selected" | "legal" | "playable" | "attacking" | "defending" | null;

type CardProps = {
  cardNo?: string | null;
  object?: GameObject;
  size?: "hand" | "board" | "mini" | "field";
  faceDown?: boolean;
  marks?: CardMark[];
  onClick?: (e: MouseEvent) => void;
  onHover?: (cardNo: string | null, object?: GameObject) => void;
  style?: CSSProperties;
  title?: string;
};

export function Card({ cardNo, object, size = "board", faceDown, marks = [], onClick, onHover, style, title }: CardProps) {
  const no = cardNo ?? object?.cardNo ?? null;
  const info = cardInfo(no);
  const hidden = faceDown || object?.isFaceDown || !no;
  const exhausted = Boolean(object?.isExhausted);
  const power = object?.effectivePower ?? info?.power;
  const showStats = size !== "mini" && object && object.tags?.includes("CARD_TYPE:UNIT");
  const buffed = object && object.effectivePower !== object.basePower;
  const cls = [
    "card",
    `card--${size}`,
    hidden ? "card--back" : "",
    exhausted ? "card--exhausted" : "",
    onClick ? "card--interactive" : "",
    ...marks.filter(Boolean).map((m) => `card--${m}`),
  ].join(" ");

  return (
    <div className={`card-slot card-slot--${size}${exhausted ? " card-slot--exhausted" : ""}`} style={style}>
      <button
        type="button"
        className={cls}
        onClick={onClick}
        disabled={!onClick}
        title={title ?? (hidden ? "背面朝上" : info?.name)}
        onMouseEnter={() => onHover?.(hidden ? null : no, object)}
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
        {showStats && !hidden && (
          <span className={`card__power${buffed ? " card__power--changed" : ""}`}>{power}</span>
        )}
        {object && object.damage > 0 && <span className="card__damage">{object.damage}</span>}
      </button>
    </div>
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
            return (
              <span
                key={i}
                className="sym sym--power"
                title="符能"
                style={{ background: t.color ? COLOR_HEX[t.color] : undefined }}
              />
            );
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
