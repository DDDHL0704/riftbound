import { motion } from "motion/react";
import type { ObjectAction } from "../../game/actions";

/** The actions a clicked object offers, as a small menu beside it. */
export function ActionMenu({
  actions,
  hideRecycles,
  onPick,
  onClose,
}: {
  actions: ObjectAction[];
  hideRecycles: boolean;
  onPick: (a: ObjectAction) => void;
  onClose: () => void;
}) {
  return (
    <motion.div className="menu" role="menu" onClick={(e) => e.stopPropagation()} initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.12 }}>
      {actions.map((a) => (
        <button key={a.key} role="menuitem" className="menu__item" onClick={() => onPick(a)}>
          <span>{a.label}</span>
          {a.cmdType === "HIDE_CARD" && hideRecycles && a.recycles.length > 0 ? (
            <span className="menu__detail">回收 1 枚符文支付符能</span>
          ) : (
            a.detail && <span className="menu__detail">{a.detail}</span>
          )}
        </button>
      ))}
      <button className="menu__item menu__item--quiet" onClick={onClose}>
        取消
      </button>
    </motion.div>
  );
}
