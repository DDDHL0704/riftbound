import { motion } from "motion/react";

export type RuneMode = "tap" | "recycle" | "tap-recycle";

/**
 * Multi-select rune actions. The rules allow recycling a ready rune, but that throws away its
 * mana, so "tap then recycle" is offered first and a bare recycle of ready runes says so.
 */
export function RuneBar({
  selected,
  tapIds,
  recycleIds,
  pending,
  onRun,
  onSelectAll,
  onCancel,
}: {
  selected: string[];
  tapIds: Set<string>;
  recycleIds: Set<string>;
  pending: boolean;
  onRun: (mode: RuneMode) => void;
  onSelectAll: () => void;
  onCancel: () => void;
}) {
  const ready = selected.filter((r) => tapIds.has(r)).length;
  const recyclable = selected.filter((r) => recycleIds.has(r)).length;
  const readyRecyclable = selected.filter((r) => tapIds.has(r) && recycleIds.has(r)).length;
  return (
    <motion.div className="runebar" onClick={(e) => e.stopPropagation()} initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}>
      <span className="runebar__count">已选 {selected.length} 枚符文</span>
      {ready > 0 && (
        <button className="btn btn--small btn--primary" disabled={pending} onClick={() => onRun("tap")}>
          横置 <b>+{ready} 法力</b>
        </button>
      )}
      {readyRecyclable > 0 && (
        <button className="btn btn--small" disabled={pending} onClick={() => onRun("tap-recycle")} title="先横置拿法力，再回收拿符能">
          横置并回收 <b>+{ready} 法力 +{recyclable} 符能</b>
        </button>
      )}
      {recyclable > 0 && (
        <button
          className="btn btn--small btn--ghost"
          disabled={pending}
          onClick={() => onRun("recycle")}
          title={readyRecyclable ? "所选符文中有未横置的：直接回收会损失这次的法力" : "回收符文获得符能"}
        >
          {readyRecyclable ? "仅回收（损失法力）" : `回收 +${recyclable} 符能`}
        </button>
      )}
      {tapIds.size > ready && (
        <button className="btn btn--small btn--ghost" onClick={onSelectAll}>
          全选未横置
        </button>
      )}
      <button className="runebar__close" onClick={onCancel} aria-label="取消选择">
        ✕
      </button>
    </motion.div>
  );
}
