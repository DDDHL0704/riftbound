import { AnimatePresence, motion } from "motion/react";

/** The strip that announces whose turn just began. */
export function TurnBanner({ banner, turnNumber }: { banner: string | null; turnNumber: number }) {
  return (
    <AnimatePresence>
      {banner && (
        <motion.div
          key={banner + turnNumber}
          className={`banner ${banner === "你的回合" ? "banner--me" : "banner--opp"}`}
          role="status"
          initial={{ opacity: 0, scaleX: 0.6 }}
          animate={{ opacity: 1, scaleX: 1 }}
          exit={{ opacity: 0, y: -16 }}
          transition={{ duration: 0.35, ease: [0.2, 0.8, 0.2, 1] }}
        >
          <span>{banner}</span>
          <small>第 {turnNumber} 回合</small>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
