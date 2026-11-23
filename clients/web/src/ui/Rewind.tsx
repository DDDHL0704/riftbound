import { client } from "../net/client";
import type { Prompt, RewindTarget, Snapshot } from "../game/types";

/*
 * Rewinding the current game by mutual consent (ADR 0007). Either player picks a step to go back before;
 * the other agrees or refuses. The offers come in `prompt.rewindCandidates`, apart from the rule candidates,
 * so a rewind can be asked or answered in any window without disturbing it.
 */

const offered = (prompt: Prompt | null, action: string) => prompt?.rewindCandidates?.find((c) => c.action === action);

function where(t: RewindTarget | undefined, meId: string) {
  if (!t) return "";
  return `第 ${t.turnNumber} 回合 · ${t.playerId === meId ? "你" : "对手"}${t.description}`;
}

/** The pending request: the asker can take it back, the other player answers it. */
export function RewindRequestBar({ snapshot, prompt, meId }: { snapshot: Snapshot; prompt: Prompt | null; meId: string }) {
  const req = snapshot.rewindRequest;
  if (!req) return null;
  const target = snapshot.rewindTargets?.find((t) => t.step === req.targetStep);
  const mine = req.requesterPlayerId === meId;
  const send = (cmdType: string) => void client.submit({ cmdType, requestStep: req.requestStep });
  return (
    <div className="panel rewind-bar">
      <p className="panel__title">{mine ? "等待对手同意回退" : "对手请求回退"}</p>
      <p className="rewind-bar__to">回到 {where(target, meId)} 之前</p>
      <div className="tempo">
        {mine
          ? offered(prompt, "CANCEL_REWIND") && (
              <button className="btn btn--ghost btn--small" onClick={() => send("CANCEL_REWIND")}>
                撤回
              </button>
            )
          : (
              <>
                {offered(prompt, "ACCEPT_REWIND") && (
                  <button className="btn btn--primary btn--small" onClick={() => send("ACCEPT_REWIND")}>
                    同意
                  </button>
                )}
                {offered(prompt, "REJECT_REWIND") && (
                  <button className="btn btn--ghost btn--small" onClick={() => send("REJECT_REWIND")}>
                    拒绝
                  </button>
                )}
              </>
            )}
      </div>
    </div>
  );
}

/** Whether the footer should offer to pick a step to rewind to. */
export function canRequestRewind(snapshot: Snapshot, prompt: Prompt | null) {
  return Boolean(snapshot.rewindAllowed && !snapshot.rewindRequest && snapshot.rewindTargets?.length && offered(prompt, "REQUEST_REWIND"));
}

/** The steps of this game, newest first; choosing one asks the other player to go back before it. */
export function RewindPicker({ snapshot, meId, onClose }: { snapshot: Snapshot; meId: string; onClose: () => void }) {
  const targets = [...(snapshot.rewindTargets ?? [])].reverse();
  const ask = (step: number) => {
    void client.submit({ cmdType: "REQUEST_REWIND", targetStep: step });
    onClose();
  };
  return (
    <div className="settings rewind" role="dialog" aria-label="回退到">
      <h3>回到哪一步之前</h3>
      <ol className="rewind__list">
        {targets.map((t) => (
          <li key={t.step}>
            <button className={`rewind__step${t.playerId === meId ? " is-mine" : ""}`} onClick={() => ask(t.step)}>
              <span className="rewind__turn">第 {t.turnNumber} 回合</span>
              <span>
                {t.playerId === meId ? "你" : "对手"}
                {t.description}
              </span>
            </button>
          </li>
        ))}
      </ol>
      <div className="guide__actions">
        <button className="btn btn--ghost btn--small" onClick={onClose}>
          关闭
        </button>
      </div>
    </div>
  );
}
