import { motion } from "motion/react";
import type { ObjectAction } from "../../game/actions";
import type { GameObject } from "../../game/types";
import { Card, type HoverHandler } from "../Card";
import type { ZoneView } from "../Zones";

/** A player's graveyard or banished pile, newest first; the viewer's own cards offer what they can do from there. */
export function ZoneViewer({
  zoneView,
  zoneIds,
  meId,
  objects,
  heroActions,
  hoverCard,
  onClose,
  onAction,
}: {
  zoneView: NonNullable<ZoneView>;
  zoneIds: string[];
  meId: string;
  objects: Record<string, GameObject>;
  heroActions: (id: string) => ObjectAction[];
  hoverCard: HoverHandler;
  onClose: () => void;
  onAction: (a: ObjectAction) => void;
}) {
  return (
    <div className="zoneview" role="dialog" aria-label={zoneView.title} onClick={onClose}>
      <motion.div className="zoneview__box" onClick={(e) => e.stopPropagation()} initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
        <header className="zoneview__head">
          <h3>
            {zoneView.title}（{zoneIds.length}）
          </h3>
          <button className="btn btn--ghost btn--small" onClick={onClose}>
            关闭
          </button>
        </header>
        <div className="zoneview__cards">
          {[...zoneIds].reverse().map((id) => {
            const acts = zoneView.playerId === meId ? heroActions(id) : [];
            return (
              <div key={id} className={`zoneview__card${acts.length ? " is-live" : ""}`}>
                <Card object={objects[id]} cardNo={objects[id]?.cardNo} size="board" marks={acts.length ? ["playable"] : []} onHover={hoverCard} animate={false} />
                {acts.map((a) => (
                  <button
                    key={a.key}
                    className="btn btn--primary btn--small"
                    onClick={() => onAction(a)}
                  >
                    {a.label}
                    {a.detail && <small> · {a.detail}</small>}
                  </button>
                ))}
              </div>
            );
          })}
        </div>
        <p className="muted small">最新进入的牌排在最前。</p>
      </motion.div>
    </div>
  );
}
