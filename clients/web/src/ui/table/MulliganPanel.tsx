import type { MouseEvent } from "react";
import { motion } from "motion/react";
import { client } from "../../net/client";
import { cardInfo, cardName } from "../../game/cards";
import type { GameObject } from "../../game/types";

/** Opening-hand mulligan: pick up to the allowed number of cards to swap, then keep or swap. */
export function MulliganPanel({
  hand,
  objects,
  mulligan,
  pending,
  onCard,
}: {
  hand: string[];
  objects: Record<string, GameObject>;
  mulligan: string[];
  pending: boolean;
  onCard: (id: string) => (e: MouseEvent) => void;
}) {
  return (
    <motion.div className="mulligan" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} onClick={(e) => e.stopPropagation()}>
      <h2 className="mulligan__title">起手调度</h2>
      <div className="mulligan__cards">
        {hand.map((id, i) => {
          const o = objects[id];
          const swap = mulligan.includes(id);
          return (
            <motion.button
              key={id}
              className={`mulligan__card${swap ? " is-swap" : ""}`}
              initial={{ opacity: 0, y: 30, rotate: (i - 1.5) * 4 }}
              animate={{ opacity: 1, y: swap ? 14 : 0, rotate: 0 }}
              transition={{ delay: i * 0.06, type: "spring", stiffness: 260, damping: 22 }}
              onClick={onCard(id)}
              aria-pressed={swap}
              aria-label={`${cardName(o?.cardNo)}${swap ? "（换掉）" : ""}`}
              data-card-no={o?.cardNo ?? undefined}
              data-object-id={id}
            >
              {cardInfo(o?.cardNo)?.image ? <img src={cardInfo(o?.cardNo)!.image} alt="" draggable={false} /> : <span>{cardName(o?.cardNo)}</span>}
              {swap && <span className="mulligan__mark">换掉</span>}
            </motion.button>
          );
        })}
      </div>
      <button className="btn btn--primary" disabled={pending} onClick={() => void client.submit({ cmdType: "MULLIGAN", handObjectIds: mulligan })}>
        {mulligan.length ? `换掉 ${mulligan.length} 张` : "保留起手"}
      </button>
    </motion.div>
  );
}
