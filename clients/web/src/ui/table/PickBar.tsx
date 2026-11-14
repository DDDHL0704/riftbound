import { motion, useIsPresent } from "motion/react";
import type { ActionStep, ObjectAction } from "../../game/actions";
import { cardInfo, cardName, paymentChoiceLabel } from "../../game/cards";
import type { Choice, GameObject } from "../../game/types";
import { boonSteppers, neededPower, offeredCosts, providedPower, type BoonCostGroup, type Pick } from "./pick";

type PickBarProps = {
  pick: Pick;
  step?: ActionStep;
  choices: Choice[];
  otherActions: ObjectAction[];
  onOtherAction: (a: ObjectAction) => void;
  extraPower: (destinationId: string) => number;
  objects: Record<string, GameObject>;
  onChoose: (id: string) => void;
  onSkip: () => void;
  onToggleCost: (id: string) => void;
  onSetCosts: (costs: string[]) => void;
  onConfirm: () => void;
  onCancel: () => void;
};

/** Hover-preview attributes for a choice that stands for a card on the table. */
function cardRefAttrs(id: string, objects: Record<string, GameObject>) {
  const o = objects[id] ?? objects[id.split(":").pop() ?? id];
  return o?.cardNo ? { "data-card-no": o.cardNo, "data-object-id": o.objectId } : {};
}

function choiceName(id: string, label: string, objects: Record<string, GameObject>) {
  if (id === "BASE" || id.startsWith("BASE:")) return id === "BASE" ? "基地" : label;
  const tail = id.split(":").pop() ?? id;
  const o = objects[id] ?? objects[tail];
  return o ? cardName(o.cardNo) : label;
}

/** Declares how much extra power to pay; the play's cost quote then asks for runes if the pool is short. */
function VariablePower({ prefix, chosen, onChange }: { prefix: string; chosen: string[]; onChange: (costs: string[]) => void }) {
  const current = chosen.find((c) => c.startsWith(prefix));
  const amount = current ? Number(current.slice(prefix.length)) : 0;
  const set = (n: number) => onChange([...chosen.filter((c) => !c.startsWith(prefix)), ...(n > 0 ? [prefix + n] : [])]);
  return (
    <p className="pickbar__hint pickbar__stepper">
      额外支付符能：
      <button className="chip" onClick={() => set(Math.max(0, amount - 1))} disabled={amount === 0} aria-label="减少">
        −
      </button>
      <b>{amount}</b>
      <button className="chip" onClick={() => set(amount + 1)} aria-label="增加">
        +
      </button>
    </p>
  );
}

/** Spends 0 to `maximumCount` boons of one unit ("BOON_PLAY_COST:<unit>:<count>"). */
function BoonStepper({ group, chosen, objects, onChange }: { group: BoonCostGroup; chosen: string[]; objects: Record<string, GameObject>; onChange: (costs: string[]) => void }) {
  const current = chosen.find((c) => c.startsWith(group.choicePrefix));
  const amount = current ? Number(current.slice(group.choicePrefix.length)) : 0;
  const set = (n: number) => {
    const rest = chosen.filter((c) => !c.startsWith(group.choicePrefix));
    onChange(n > 0 ? [...rest.filter((c) => !c.startsWith("TAP_RUNE:") && !c.startsWith("RECYCLE_RUNE:")), group.choicePrefix + n, ...rest.filter((c) => c.startsWith("TAP_RUNE:") || c.startsWith("RECYCLE_RUNE:"))] : rest);
  };
  return (
    <p className="pickbar__hint pickbar__stepper" {...cardRefAttrs(group.sourceObjectId, objects)}>
      消耗 {cardName(objects[group.sourceObjectId]?.cardNo)} 的增益：
      <button className="chip" onClick={() => set(Math.max(0, amount - 1))} disabled={amount === 0} aria-label="减少">
        −
      </button>
      <b>{amount}</b>
      <button className="chip" onClick={() => set(Math.min(group.maximumCount, amount + 1))} disabled={amount >= group.maximumCount} aria-label="增加">
        +
      </button>
      <span className="muted small">最多 {group.maximumCount}</span>
    </p>
  );
}

export function PickBar({ pick, step, choices, otherActions, onOtherAction, extraPower, objects, onChoose, onSkip, onToggleCost, onSetCosts, onConfirm, onCancel }: PickBarProps) {
  const src = objects[pick.action.sourceObjectId];
  const info = cardInfo(src?.cardNo);
  const moving = pick.action.cmdType === "MOVE_UNIT";
  // While it fades out after the prompt changed, its choices belong to a prompt that is gone.
  const leaving = !useIsPresent();
  return (
    <motion.div
      className={leaving ? "pickbar pickbar--leaving" : "pickbar"}
      onClick={(e) => e.stopPropagation()}
      role="dialog"
      aria-label="选择"
      initial={{ opacity: 0, y: -10 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -10 }}
      transition={{ duration: 0.16 }}
    >
      {info?.image && <img className="pickbar__thumb" src={info.image} alt="" data-card-no={src?.cardNo} data-object-id={src?.objectId} />}
      <div className="pickbar__body">
        <p className="pickbar__title">
          <strong>{info?.name ?? cardName(src?.cardNo)}</strong>
          <span>{pick.action.label}</span>
        </p>
        {step ? (
          <>
            {!moving && <p className="pickbar__hint">{step.label}</p>}
            {moving && pick.group.length > 0 && <p className="pickbar__hint pickbar__hint--group">{pick.group.length + 1} 个单位一起移动</p>}
            <div className="pickbar__choices">
              {choices
                .filter((c) => !pick.sel.targets.includes(c.id) && !(step.role === "cost" && pick.sel.optionalCosts.includes(c.id)))
                .map((c) => (
                  <button key={c.id} className="chip" onClick={() => onChoose(c.id)} {...cardRefAttrs(c.id, objects)}>
                    {choiceName(c.id, c.label, objects)}
                    {extraPower(c.id) > 0 && <span className="chip__cost">+{extraPower(c.id)} 符能</span>}
                  </button>
                ))}
              {choices.length === 0 && <span className="muted small">所选单位没有共同的目的地。</span>}
              {!step.required && (
                <button className="chip chip--quiet" onClick={onSkip}>
                  不选
                </button>
              )}
            </div>
            {otherActions.length > 0 && (
              <div className="pickbar__others">
                <span className="muted small">其他操作</span>
                {otherActions.map((a) => (
                  <button key={a.key} className="chip chip--quiet" onClick={() => onOtherAction(a)}>
                    {a.label}
                    {a.detail && <span className="chip__cost">{a.detail}</span>}
                  </button>
                ))}
              </div>
            )}
          </>
        ) : (
          <>
            {neededPower(pick) > 0 ? (
              <p className={`pickbar__hint${providedPower(pick, pick.sel.optionalCosts) < neededPower(pick) ? " pickbar__hint--warn" : ""}`}>
                需回收符文支付 {neededPower(pick)} 符能 · 已选 {providedPower(pick, pick.sel.optionalCosts)}
              </p>
            ) : null}
            {pick.action.variablePower && (
              <VariablePower prefix={pick.action.variablePower} chosen={pick.sel.optionalCosts} onChange={onSetCosts} />
            )}
            {boonSteppers(pick.action).map((g) => (
              <BoonStepper key={g.choicePrefix} group={g} chosen={pick.sel.optionalCosts} objects={objects} onChange={onSetCosts} />
            ))}
            <div className="pickbar__choices">
              {[...offeredCosts(pick), ...(neededPower(pick) > 0 ? pick.action.recycles : [])]
                .map((c) => (
                  <label key={c.id} className="chip chip--toggle">
                    <input type="checkbox" checked={pick.sel.optionalCosts.includes(c.id)} onChange={() => onToggleCost(c.id)} />
                    {paymentChoiceLabel(c.id, c.label, objects)}
                  </label>
                ))}
            </div>
            <button className="btn btn--primary btn--small" onClick={onConfirm} disabled={providedPower(pick, pick.sel.optionalCosts) < neededPower(pick)}>
              确认{pick.action.label}
            </button>
          </>
        )}
      </div>
      <button className="pickbar__cancel" onClick={onCancel} aria-label="取消选择">
        取消
      </button>
    </motion.div>
  );
}
