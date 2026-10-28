import { useEffect, useMemo, useRef, useState } from "react";
import { client, type ClientState } from "../net/client";
import { cardName, paymentChoiceLabel } from "../game/cards";
import { enabledCandidate } from "../game/actions";
import type { Candidate, Choice, GameObject, Prompt, Snapshot } from "../game/types";
import { Card, EffectText, type HoverHandler } from "./Card";
import { TEMPO_ACTIONS } from "./Overlay";
import { cardInfo } from "../game/cards";

type RailProps = {
  state: ClientState;
  snapshot: Snapshot;
  prompt: Prompt | null;
  objects: Record<string, GameObject>;
  mulligan: string[];
  onHover: HoverHandler;
};

function nameOf(id: string, objects: Record<string, GameObject>, fallback?: string) {
  const o = objects[id];
  return o?.cardNo ? cardName(o.cardNo) : fallback ?? id;
}

export function Rail({ state, snapshot, prompt, objects, mulligan, onHover }: RailProps) {
  const meId = state.handle;
  const view = prompt?.view;
  const type = view?.type ?? "WAIT";
  const myTurn = (snapshot.timing.turnPlayerId || snapshot.activePlayerId) === meId;
  const actionable = Boolean(prompt?.actionable);
  const [confirmSurrender, setConfirmSurrender] = useState(false);

  return (
    <aside className="rail" onClick={(e) => e.stopPropagation()}>
      <header className={`rail__head${actionable ? " is-active" : ""}`}>
        <p className="rail__turn">
          第 {snapshot.turnNumber} 回合，{myTurn ? "你的回合" : "对手回合"}
        </p>
        <h2 className="rail__title">{actionable || type === "MATCH_RESULT" ? view?.title ?? "行动" : "等待对手"}</h2>
        <p className="rail__msg">{view?.message || prompt?.reason}</p>
      </header>

      <div className="rail__panel">
        {type === "MULLIGAN" && actionable && <MulliganPanel prompt={prompt!} selected={mulligan} objects={objects} />}
        {/* Battles usually come from the start-battle prompt, but the server may also offer one as a main action. */}
        {enabledCandidate(prompt, "DECLARE_BATTLE") && <BattlePanel prompt={prompt} objects={objects} />}
        {type === "ASSIGN_COMBAT_DAMAGE" && <DamagePanel prompt={prompt} objects={objects} kind="combat" />}
        {enabledCandidate(prompt, "ASSIGN_EFFECT_DAMAGE") && <DamagePanel prompt={prompt} objects={objects} kind="effect" />}
        {type === "ORDER_TRIGGERS" && <TriggerPanel prompt={prompt} objects={objects} />}
        {(type === "HAND_CHOICE" || type === "CARD_CHOICE") && <ChoicePanel prompt={prompt} objects={objects} onHover={onHover} />}
        {enabledCandidate(prompt, "PAY_COST") && <PayPanel prompt={prompt} objects={objects} pool={snapshot.players[meId]?.runePool} />}
        <GenericPanel prompt={prompt} />
      </div>

      <Preview snapshot={snapshot} objects={objects} />
      <StackView snapshot={snapshot} objects={objects} meId={meId} onHover={onHover} />
      <Log entries={state.log} objects={objects} meId={state.handle} />

      <footer className="rail__foot">
        <span className="muted">房间 {state.roomId}</span>
        {snapshot.timing.roomStatus === "IN_PROGRESS" &&
          (confirmSurrender ? (
            <span className="rail__confirm">
              确定认输？
              <button className="btn btn--danger btn--small" onClick={() => void client.submit({ cmdType: "SURRENDER" })}>
                认输
              </button>
              <button className="btn btn--ghost btn--small" onClick={() => setConfirmSurrender(false)}>
                继续对战
              </button>
            </span>
          ) : (
            <button className="btn btn--ghost btn--small" onClick={() => setConfirmSurrender(true)}>
              认输
            </button>
          ))}
      </footer>
    </aside>
  );
}

function MulliganPanel({ prompt, selected, objects }: { prompt: Prompt; selected: string[]; objects: Record<string, GameObject> }) {
  const max = prompt.view?.maxSelection ?? 2;
  return (
    <div className="panel">
      <p>在桌面中央点选至多 {max} 张想换掉的牌，再点「保留起手」或「换掉」。</p>
      {selected.length > 0 && <p className="muted">已选：{selected.map((id) => nameOf(id, objects)).join("、")}</p>}
    </div>
  );
}

function fillByIndex(byIndex: Record<string, Choice[]> | undefined, count: number) {
  const out: string[] = [];
  for (let i = 0; i < count; i++) {
    const c = (byIndex?.[String(i)] ?? []).find((x) => !out.includes(x.id));
    if (c) out.push(c.id);
  }
  return out;
}

function BattlePanel({ prompt, objects }: { prompt: Prompt | null; objects: Record<string, GameObject> }) {
  const cand = enabledCandidate(prompt, "DECLARE_BATTLE");
  const req = cand?.metadata?.sourceRequirements?.[0];
  const [targets, setTargets] = useState<string[]>([]);
  const [costs, setCosts] = useState<string[]>([]);
  useEffect(() => {
    setTargets([]);
    setCosts([]);
  }, [prompt?.promptId]);
  if (!cand || !req) return null;
  // Every unit at a contested battlefield fights (the server rejects partial declarations),
  // so participants are listed, not chosen.
  const attackers = fillByIndex(req.attackerChoicesByIndex, req.maxAttackerCount ?? req.minAttackerCount);
  const defenders = fillByIndex(req.targetChoicesByIndex, req.maxDefenderCount ?? req.minDefenderCount);
  const battlefieldId: string | undefined = req.battlefieldChoices?.[0]?.id;
  const required: string[] = req.requiredOptionalCosts ?? [];
  const minTargets = Number(req.minBattlefieldTargetCount ?? 0);
  const maxTargets = Number(req.maxBattlefieldTargetCount ?? 0);
  const targetChoices: Choice[] = [];
  for (const list of Object.values((req.battlefieldTargetChoicesByIndex ?? {}) as Record<string, Choice[]>))
    for (const c of list) if (!targetChoices.some((x) => x.id === c.id)) targetChoices.push(c);
  const extraCosts: Choice[] = [];
  for (const c of [...(req.optionalCostChoices ?? []), ...(req.paymentResourceChoices ?? [])] as Choice[])
    if (!required.includes(c.id) && !extraCosts.some((x) => x.id === c.id)) extraCosts.push(c);
  const toggle = (list: string[], id: string, max = Infinity) =>
    list.includes(id) ? list.filter((x) => x !== id) : max === 1 ? [id] : list.length < max ? [...list, id] : list;

  return (
    <div className="panel panel--battle">
      <p className="panel__title">{nameOf(battlefieldId ?? "", objects, "战场")}</p>
      <div className="versus">
        <div>
          <p className="muted">进攻</p>
          <ul>{attackers.map((id) => <li key={id}>{nameOf(id, objects)}（{objects[id]?.effectivePower ?? "?"}）</li>)}</ul>
        </div>
        <div>
          <p className="muted">防守</p>
          <ul>{defenders.map((id) => <li key={id}>{nameOf(id, objects)}（{objects[id]?.effectivePower ?? "?"}）</li>)}</ul>
        </div>
      </div>
      {maxTargets > 0 && targetChoices.length > 0 && (
        <div className="panel__group">
          <p className="small">
            进攻触发目标{req.battlefieldTargetScope ? `（${req.battlefieldTargetScope}）` : ""}
            {minTargets === 0 ? "，可不选" : `，需选 ${minTargets} 个`}
          </p>
          <div className="choices">
            {targetChoices.map((c) => (
              <button
                key={c.id}
                className={`chip${targets.includes(c.id) ? " chip--on" : ""}`}
                onClick={() => setTargets((cur) => toggle(cur, c.id, maxTargets))}
              >
                {nameOf(c.id, objects, c.label)}
              </button>
            ))}
          </div>
        </div>
      )}
      {extraCosts.length > 0 && (
        <div className="panel__group">
          <p className="small">可选费用</p>
          <div className="choices">
            {extraCosts.map((c) => (
              <label key={c.id} className="chip chip--toggle">
                <input type="checkbox" checked={costs.includes(c.id)} onChange={() => setCosts((cur) => toggle(cur, c.id))} />
                {paymentChoiceLabel(c.id, c.label, objects)}
              </label>
            ))}
          </div>
        </div>
      )}
      <button
        className="btn btn--primary btn--wide"
        disabled={targets.length < minTargets}
        onClick={() =>
          void client.submit({
            cmdType: "DECLARE_BATTLE",
            battlefieldId,
            attackerObjectIds: attackers,
            defenderObjectIds: defenders,
            optionalCosts: [...required, ...costs],
            ...(targets.length ? { battlefieldTargetObjectIds: targets } : {}),
          })
        }
      >
        开战
      </button>
    </div>
  );
}

type Assignment = { sourceObjectId: string; targetObjectId: string; damage: number };
type Required = { sourceObjectId: string; damage: number; legalTargetObjectIds: string[] };

function suggestCombat(md: Record<string, any>): Assignment[] {
  const required: Required[] = md.requiredAssignments ?? [];
  const want: Record<string, number> = { ...(md.suggestedDamageByTarget ?? {}) };
  const out: Assignment[] = [];
  for (const r of required) {
    let left = r.damage;
    const order = [...r.legalTargetObjectIds].sort((a, b) => (want[b] ?? 0) - (want[a] ?? 0));
    for (const t of order) {
      if (left <= 0) break;
      const take = Math.min(left, want[t] ?? 0);
      if (take > 0) {
        out.push({ sourceObjectId: r.sourceObjectId, targetObjectId: t, damage: take });
        want[t] -= take;
        left -= take;
      }
    }
    if (left > 0 && order[0]) {
      const existing = out.find((a) => a.sourceObjectId === r.sourceObjectId && a.targetObjectId === order[0]);
      if (existing) existing.damage += left;
      else out.push({ sourceObjectId: r.sourceObjectId, targetObjectId: order[0], damage: left });
    }
  }
  return out;
}

/** Effect damage must hit exactly `minimumAssignedTargets` targets with at least 1 each. */
function suggestEffect(md: Record<string, any>): Assignment[] {
  const r: Required | undefined = md.requiredAssignments?.[0];
  if (!r) return [];
  const count = Math.min(Number(md.minimumAssignedTargets ?? 1), r.legalTargetObjectIds.length, r.damage);
  const out = r.legalTargetObjectIds.slice(0, count).map((t) => ({ sourceObjectId: r.sourceObjectId, targetObjectId: t, damage: 1 }));
  if (out[0]) out[0].damage += r.damage - out.length;
  return out;
}

function DamagePanel({ prompt, objects, kind }: { prompt: Prompt | null; objects: Record<string, GameObject>; kind: "combat" | "effect" }) {
  const action = kind === "combat" ? "ASSIGN_COMBAT_DAMAGE" : "ASSIGN_EFFECT_DAMAGE";
  const cand = enabledCandidate(prompt, action);
  const md = cand?.metadata;
  const [plan, setPlan] = useState<Assignment[]>([]);
  useEffect(() => {
    setPlan(md ? (kind === "combat" ? suggestCombat(md) : suggestEffect(md)) : []);
  }, [prompt?.promptId]); // eslint-disable-line react-hooks/exhaustive-deps
  if (!cand || !md || md.assignmentState === "WAIT") return null;
  const required: Required[] = md.requiredAssignments ?? [];
  const lethal: Record<string, number> = md.lethalDamageThreshold ?? {};
  const existing: Record<string, number> = md.existingDamage ?? {};
  const targetCount = Number(md.minimumAssignedTargets ?? 0);

  const amount = (s: string, t: string) => plan.find((a) => a.sourceObjectId === s && a.targetObjectId === t)?.damage ?? 0;
  const bump = (s: string, t: string, delta: number, total: number) => {
    setPlan((cur) => {
      const used = cur.filter((a) => a.sourceObjectId === s).reduce((n, a) => n + a.damage, 0);
      const now = cur.find((a) => a.sourceObjectId === s && a.targetObjectId === t)?.damage ?? 0;
      const next = Math.max(0, now + delta);
      if (delta > 0 && used >= total) return cur;
      const rest = cur.filter((a) => !(a.sourceObjectId === s && a.targetObjectId === t));
      return next > 0 ? [...rest, { sourceObjectId: s, targetObjectId: t, damage: next }] : rest;
    });
  };
  const used = (s: string) => plan.filter((a) => a.sourceObjectId === s).reduce((n, a) => n + a.damage, 0);
  const totalsOk = required.every((r) => used(r.sourceObjectId) === r.damage);
  const targetsOk = kind === "combat" || plan.filter((a) => a.damage > 0).length === targetCount;

  return (
    <div className="panel panel--damage">
      {md.assignmentGuidance && <p className="muted small">{md.assignmentGuidance}</p>}
      {kind === "effect" && targetCount > 0 && (
        <p className={`small${targetsOk ? "" : " warn"}`}>需要正好分给 {targetCount} 个目标，每个至少 1 点。</p>
      )}
      {required.map((r) => (
        <div key={r.sourceObjectId} className="dmg">
          <p className="dmg__src">
            {nameOf(r.sourceObjectId, objects)} 造成 <b>{r.damage}</b> 点
            {used(r.sourceObjectId) !== r.damage && <span className="warn small">（还剩 {r.damage - used(r.sourceObjectId)} 点）</span>}
          </p>
          {r.legalTargetObjectIds.map((t) => (
            <div key={t} className="dmg__row">
              <span>
                {nameOf(t, objects)}
                <span className="muted small">（致命 {Math.max(0, (lethal[t] ?? 0) - (existing[t] ?? 0))}）</span>
              </span>
              <span className="stepper">
                <button onClick={() => bump(r.sourceObjectId, t, -1, r.damage)} aria-label="减少">−</button>
                <b>{amount(r.sourceObjectId, t)}</b>
                <button onClick={() => bump(r.sourceObjectId, t, 1, r.damage)} aria-label="增加">+</button>
              </span>
            </div>
          ))}
        </div>
      ))}
      <button
        className="btn btn--primary btn--wide"
        disabled={!totalsOk || !targetsOk}
        onClick={() =>
          void client.submit(
            kind === "combat"
              ? { cmdType: action, battleId: md.battleId, battlefieldId: md.battlefieldId, assignments: plan.filter((a) => a.damage > 0) }
              : { cmdType: action, stackItemId: md.stackItemId, assignments: plan.filter((a) => a.damage > 0) },
          )
        }
      >
        确认伤害分配
      </button>
    </div>
  );
}

function TriggerPanel({ prompt, objects }: { prompt: Prompt | null; objects: Record<string, GameObject> }) {
  const cand = enabledCandidate(prompt, "ORDER_TRIGGERS");
  const md = cand?.metadata;
  const [order, setOrder] = useState<string[]>([]);
  useEffect(() => {
    setOrder(md?.orderedTriggerIds ?? md?.triggerIds ?? []);
  }, [prompt?.promptId]); // eslint-disable-line react-hooks/exhaustive-deps
  if (!cand || !md) return null;
  const triggers: Record<string, { sourceObjectId?: string; effectKind?: string }> = Object.fromEntries(
    (md.triggers ?? []).map((t: any) => [t.triggerId, t]),
  );
  const move = (i: number, d: number) =>
    setOrder((cur) => {
      const next = [...cur];
      const j = i + d;
      if (j < 0 || j >= next.length) return cur;
      [next[i], next[j]] = [next[j], next[i]];
      return next;
    });
  return (
    <div className="panel">
      <p className="muted small">从上到下依次结算。</p>
      <ol className="order">
        {order.map((id, i) => (
          <li key={id}>
            <span>{nameOf(triggers[id]?.sourceObjectId ?? "", objects, triggers[id]?.effectKind ?? id)}</span>
            <span className="order__moves">
              <button onClick={() => move(i, -1)} disabled={i === 0} aria-label="上移">↑</button>
              <button onClick={() => move(i, 1)} disabled={i === order.length - 1} aria-label="下移">↓</button>
            </span>
          </li>
        ))}
      </ol>
      <button className="btn btn--primary btn--wide" onClick={() => void client.submit({ cmdType: "ORDER_TRIGGERS", orderedTriggerIds: order })}>
        确认顺序
      </button>
    </div>
  );
}

function ChoicePanel({ prompt, objects, onHover }: { prompt: Prompt | null; objects: Record<string, GameObject>; onHover: RailProps["onHover"] }) {
  const cand: Candidate | undefined = enabledCandidate(prompt, "CHOOSE_HAND_CARDS") ?? enabledCandidate(prompt, "CHOOSE_CARDS");
  const [picked, setPicked] = useState<string[]>([]);
  useEffect(() => setPicked([]), [prompt?.promptId]);
  const choices = useMemo(() => {
    const seen = new Map<string, Choice>();
    for (const s of cand?.selectionSteps ?? []) for (const c of s.choices) if (!seen.has(c.id)) seen.set(c.id, c);
    return [...seen.values()];
  }, [cand]);
  if (!cand) return null;
  const md = cand.metadata ?? {};
  const steps = cand.selectionSteps ?? [];
  const min = Number(md.requiredCount ?? 0);
  const max = Number(md.maxCount ?? steps.length ?? 1);
  const toggle = (id: string) =>
    setPicked((cur) => (cur.includes(id) ? cur.filter((x) => x !== id) : max === 1 ? [id] : cur.length < max ? [...cur, id] : cur));

  const submit = () =>
    void client.submit({ cmdType: cand.action, choiceId: md.choiceId, choiceWindow: md.choiceWindow, chosenObjectIds: picked });

  return (
    <div className="panel">
      {md.reason && typeof md.reason === "string" && !/^[A-Z_]+$/.test(md.reason) && <p className="small">{md.reason}</p>}
      <div className={`choices${choices.some((c) => objects[c.id]) ? " choices--cards" : ""}`}>
        {choices.map((c) => {
          const o = objects[c.id];
          const on = picked.includes(c.id);
          return o ? (
            <Card
              key={c.id}
              object={o}
              size="board"
              marks={on ? ["selected"] : ["legal"]}
              onClick={() => toggle(c.id)}
              onHover={onHover}
              title={c.reason ?? undefined}
            />
          ) : (
            <button key={c.id} className={`chip${on ? " chip--on" : ""}`} onClick={() => toggle(c.id)} title={c.reason ?? undefined}>
              {c.label}
            </button>
          );
        })}
      </div>
      <button className="btn btn--primary btn--wide" disabled={picked.length < min} onClick={submit}>
        {picked.length === 0 && min === 0 ? "不选择，继续" : `确认（${picked.length}）`}
      </button>
    </div>
  );
}

/**
 * Payment windows come in two shapes. With selection steps the player picks one option
 * (pay / decline / a rule choice), optionally adding rune recycles. Without steps the player
 * pays every listed spend item and may add rune recycles to raise power.
 */
function PayPanel({ prompt, objects, pool }: { prompt: Prompt | null; objects: Record<string, GameObject>; pool?: { mana: number; power: number } }) {
  const cand = enabledCandidate(prompt, "PAY_COST");
  const [recycles, setRecycles] = useState<string[]>([]);
  useEffect(() => setRecycles([]), [prompt?.promptId]);
  if (!cand) return null;
  const md = cand.metadata ?? {};
  if (md.serverPaymentState === "WAIT") return null;
  const steps = cand.selectionSteps ?? [];
  const spend: Choice[] = md.paymentChoices ?? [];
  const recycleOptions: Choice[] = steps.length
    ? (steps.slice(1).flatMap((s) => s.choices) as Choice[]).filter((c, i, all) => all.findIndex((x) => x.id === c.id) === i)
    : md.paymentResourceChoices ?? [];
  const cost = md.cost as { mana?: number; power?: number; powerByTrait?: Record<string, number> } | undefined;
  const decline = (id: string) => client.submit({ cmdType: "PAY_COST", paymentId: md.paymentId, paymentWindow: md.paymentWindow, paymentChoiceIds: [id] });
  // Runes may be tapped inside a payment window; when the pool is short, tap first and then pay.
  const readyRunes = (enabledCandidate(prompt, "TAP_RUNE")?.sources ?? []).map((r) => r.id);
  const missingMana = Math.max(0, (cost?.mana ?? 0) - (pool?.mana ?? 0));
  const canTap = missingMana === 0 || readyRunes.length >= missingMana;
  // Power the pool lacks is covered by recycling runes in the same payment; pick the unpicked ones.
  const missingPower = Math.max(0, (cost?.power ?? 0) - (pool?.power ?? 0) - recycles.length);
  const extraRecycles = recycleOptions.map((c) => c.id).filter((id) => !recycles.includes(id)).slice(0, missingPower);
  const canPay = canTap && extraRecycles.length >= missingPower;
  const tapThenPay = async (ids: string[]) => {
    let fresh = md;
    if (missingMana > 0) {
      const tap = readyRunes.slice(0, missingMana);
      const receipt = await client.submit({ cmdType: "TAP_RUNE", sourceObjectId: tap[0], sourceObjectIds: tap });
      if (!receipt?.accepted) return;
      // Pay against the fresh prompt the tap produced.
      fresh = enabledCandidate(client.state.prompt, "PAY_COST")?.metadata ?? md;
    }
    await client.submit({ cmdType: "PAY_COST", paymentId: fresh.paymentId, paymentWindow: fresh.paymentWindow, paymentChoiceIds: [...ids, ...recycles, ...extraRecycles] });
  };
  const payLabel = (base: string) => {
    const pre = [missingMana > 0 && `横置 ${missingMana} 枚符文`, missingPower > 0 && `回收 ${missingPower} 枚符文`].filter(Boolean).join("、");
    return pre ? `${pre}并${base}` : base;
  };
  const toggle = (id: string) => setRecycles((cur) => (cur.includes(id) ? cur.filter((x) => x !== id) : [...cur, id]));

  return (
    <div className="panel">
      {cost && (cost.mana || cost.power) ? (
        <p className="small">
          需要支付
          {cost.mana ? <span className="sym sym--mana">{cost.mana}</span> : null}
          {cost.power ? ` ${cost.power} 符能` : ""}
          {pool && <span className="muted">（法力池：{pool.mana} 法力，{pool.power} 符能）</span>}
        </p>
      ) : null}
      {!canPay && <p className="small warn">{canTap ? "符能不足，可回收的符文也不够" : "法力不足，可用的未横置符文也不够"}，只能选择放弃。</p>}
      {recycleOptions.length > 0 && (
        <div className="panel__group">
          <p className="small">可回收符文来支付符能：</p>
          <div className="choices">
            {recycleOptions.map((c) => (
              <label key={c.id} className="chip chip--toggle">
                <input type="checkbox" checked={recycles.includes(c.id)} onChange={() => toggle(c.id)} />
                {paymentChoiceLabel(c.id, c.label, objects)}
              </label>
            ))}
          </div>
        </div>
      )}
      {steps.length ? (
        <div className="tempo">
          {steps[0].choices.map((c) => (
            <button
              key={c.id}
              className={`btn btn--wide${/DECLINE/.test(c.id) ? "" : " btn--primary"}`}
              disabled={!/DECLINE/.test(c.id) && !canPay}
              onClick={() => void (/DECLINE/.test(c.id) ? decline(c.id) : tapThenPay([c.id]))}
            >
              {/DECLINE/.test(c.id) ? c.label : payLabel(c.label)}
            </button>
          ))}
        </div>
      ) : (
        <button className="btn btn--primary btn--wide" disabled={!canPay} onClick={() => void tapThenPay(spend.map((c) => c.id))}>
          {payLabel(spend.length ? spend.map((c) => c.label).join("，") : "确认支付")}
        </button>
      )}
    </div>
  );
}

const HANDLED = new Set([
  "PLAY_CARD", "MOVE_UNIT", "TAP_RUNE", "RECYCLE_RUNE", "ACTIVATE_ABILITY", "LEGEND_ACT", "HIDE_CARD", "REVEAL_CARD",
  "ASSEMBLE_EQUIPMENT", "DECLARE_BATTLE", "ASSIGN_COMBAT_DAMAGE", "ORDER_TRIGGERS", "CHOOSE_HAND_CARDS", "CHOOSE_CARDS",
  "PAY_COST", "MULLIGAN", "SURRENDER", "WAIT", "SUBMIT_DECK", "READY", ...TEMPO_ACTIONS,
]);

/** Fallback for any enabled server action the table has no dedicated control for yet. */
function GenericPanel({ prompt }: { prompt: Prompt | null }) {
  const extra = (prompt?.candidates ?? []).filter((c) => c.enabled && !HANDLED.has(c.action) && c.commandTemplate);
  if (!extra.length) return null;
  return (
    <div className="panel">
      {extra.map((c) => (
        <button
          key={c.action}
          className="btn btn--wide"
          onClick={() => {
            const cmd: Record<string, unknown> = { cmdType: c.commandTemplate!.cmdType };
            for (const b of c.commandTemplate!.bindings) {
              if (b.source === "candidateMetadata") {
                const k = b.metadataKey ?? b.metadataKeys?.[0];
                if (k && c.metadata?.[k] != null) cmd[b.field] = c.metadata[k];
              }
            }
            void client.submit(cmd);
          }}
        >
          {c.label}
        </button>
      ))}
    </div>
  );
}

/** What is about to resolve, readable without hovering. Hover previews float beside the card itself. */
function Preview({ snapshot, objects }: { snapshot: Snapshot; objects: Record<string, GameObject> }) {
  const top = snapshot.stack?.length ? snapshot.stack[snapshot.stack.length - 1] : undefined;
  const cardNo = top && top.sourceVisibility !== "HIDDEN" ? ((top.cardNo as string) ?? objects[top.sourceObjectId as string]?.cardNo) : undefined;
  const info = cardInfo(cardNo);
  if (!cardNo || !info) return null;
  return (
    <section className="preview" aria-live="polite">
      {info.image && <img className="preview__art" src={info.image} alt="" />}
      <div className="preview__body">
        <p className="rail__sub">即将结算</p>
        <h3 className="preview__name">{info.name}</h3>
        <p className="preview__meta">
          {info.category}
          {info.cost != null && <span className="sym sym--mana">{info.cost}</span>}
        </p>
        {info.effect && (
          <p className="preview__effect">
            <EffectText text={info.effect} />
          </p>
        )}
      </div>
    </section>
  );
}

function StackView({ snapshot, objects, meId, onHover }: { snapshot: Snapshot; objects: Record<string, GameObject>; meId: string; onHover: RailProps["onHover"] }) {
  if (!snapshot.stack?.length) return null;
  return (
    <section className="stack" aria-label="结算链">
      <h3 className="rail__sub">结算链（上方先结算）</h3>
      <ol>
        {[...snapshot.stack].reverse().map((item, i) => {
          const hidden = item.sourceVisibility === "HIDDEN";
          const src = (item.sourceObjectId as string) ?? "";
          const no = hidden ? undefined : (item.cardNo as string) ?? objects[src]?.cardNo;
          const mine = item.controllerId === meId;
          return (
            <li
              key={(item.stackItemId as string) ?? i}
              data-stack={item.stackItemId as string}
              className={mine ? "is-me" : "is-opp"}
              onMouseEnter={(e) => onHover(no ?? null, undefined, e.currentTarget.getBoundingClientRect())}
              onMouseLeave={() => onHover(null)}
            >
              {hidden ? "未公开的效果" : no ? cardName(no) : String(item.abilityLabel ?? item.effectKind ?? "效果")}
              <span className="muted small">{mine ? "你" : "对手"}</span>
            </li>
          );
        })}
      </ol>
    </section>
  );
}

/** Server descriptions sometimes embed raw object ids; show card names instead. */
function readable(text: string, objects: Record<string, GameObject>, pattern: RegExp | null) {
  const named = pattern ? text.replace(/STACK-\d+-/g, "").replace(pattern, (id) => `「${cardName(objects[id]?.cardNo)}」`) : text;
  return orderedItem(named) ?? named;
}

// Resolution lines for queued items carry the item's internal key, e.g.
// "ordered-movement-87-1-「后巷酒吧」-BATTLEFIELD_UNIT_MOVED_AWAY_POWER_MODIFIER-「冰谷弓箭手」 结算".
// Keep the card that owns the item (named right before its ability key, else the first card named)
// and say what kind of item it was.
const ORDERED_KINDS: [RegExp, string][] = [
  [/LAST_BREATH|^TRIGGER-death-/, "绝念"],
  [/^TRIGGER-/, "触发技能"],
  [/^movement-/, "移动触发"],
  [/^conquest-/, "征服效果"],
  [/^held-/, "据守效果"],
  [/^delayed-/, "延迟效果"],
  [/^ephemeral-/, "瞬息效果"],
  [/^copy-/, "复制效果"],
  [/^(printed-)?predict-/, "预知"],
];

function orderedItem(text: string) {
  const m = /^ordered-(\S+)\s+(\S+)$/.exec(text);
  if (!m) return null;
  const [, key, verb] = m;
  const kind = ORDERED_KINDS.find(([re]) => re.test(key))?.[1] ?? "效果";
  const source = /(「[^」]+」)-[A-Z][A-Z0-9_]*(?:-|$)/.exec(key)?.[1] ?? /「[^」]+」/.exec(key)?.[0];
  return source ? `${source}的${kind}${verb}` : `${kind}${verb}`;
}

// A rune tap (or recycle) is followed by a "gained mana (power)" event saying the same thing.
const ECHOES: Record<string, string> = { MANA_GAINED: "RUNE_TAPPED", POWER_GAINED: "RUNE_RECYCLED" };

/** Drops echo lines and folds identical consecutive lines into one with a count. */
function compact(entries: ClientState["log"]) {
  const out: { entry: ClientState["log"][number]; count: number }[] = [];
  let prevKind = "";
  for (const e of entries) {
    const echo = ECHOES[e.kind] === prevKind;
    prevKind = e.kind;
    if (echo) continue;
    const last = out[out.length - 1];
    if (last && last.entry.text === e.text && last.entry.tone === e.tone) last.count++;
    else out.push({ entry: e, count: 1 });
  }
  return out;
}

// Bookkeeping the table already shows (mana in the pool, passes, phase steps); hidden unless the
// player asks for the full record.
const ROUTINE = new Set([
  "MANA_GAINED", "RUNE_TAPPED", "POWER_GAINED", "RUNE_POOL_CLEARED", "PRIORITY_PASSED", "FOCUS_PASSED",
  "TURN_START_BEGAN", "MAIN_PHASE_BEGAN", "OBJECTS_READIED", "TURN_END_CLEANUP_STARTED", "TURN_END_DECLARED",
  "PERMANENT_CONFIRMED", "TRIGGER_QUEUED", "TRIGGERS_MOVED_TO_STACK", "UNTIL_END_OF_TURN_EXPIRED",
  "CLEANUP_REPEATED", "STACK_ITEM_SUSPENDED", "COST_PAID", "RUNE_RECYCLED",
]);
const LOG_KEY = "riftbound.web.fullLog";

function Log({ entries, objects, meId }: { entries: ClientState["log"]; objects: Record<string, GameObject>; meId: string }) {
  const ref = useRef<HTMLOListElement>(null);
  const [full, setFull] = useState(() => {
    try {
      return localStorage.getItem(LOG_KEY) === "on";
    } catch {
      return false;
    }
  });
  const toggle = () =>
    setFull((on) => {
      try {
        localStorage.setItem(LOG_KEY, on ? "off" : "on");
      } catch {
        /* not remembered */
      }
      return !on;
    });
  const pattern = useMemo(() => {
    const ids = Object.keys(objects).filter((id) => objects[id]?.cardNo).sort((a, b) => b.length - a.length);
    if (!ids.length) return null;
    return new RegExp(ids.map((id) => id.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|"), "g");
  }, [objects]);
  // Player handles read as "你" for this player; the opponent keeps the name they chose.
  const me = useMemo(() => new RegExp(`(^|[^\\w])${meId.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}(?![\\w]) ?`, "g"), [meId]);
  const shown = full ? entries : entries.filter((e) => !ROUTINE.has(e.kind));
  useEffect(() => {
    ref.current?.scrollTo({ top: ref.current.scrollHeight });
  }, [shown.length, full]);
  return (
    <section className="log" aria-label="对局记录">
      <h3 className="rail__sub log__head">
        对局记录
        <button className="log__mode" onClick={toggle} title={full ? "隐藏横置符文、让过等例行记录" : "显示包括例行步骤在内的全部记录"}>
          {full ? "全部" : "关键"}
        </button>
      </h3>
      <ol ref={ref}>
        {compact(shown).map(({ entry, count }) => {
          const text = readable(entry.text, objects, pattern).replace(me, "$1你");
          // A new turn starts a new block in the record.
          if (entry.kind === "TURN_PLAYER_ADVANCED")
            return (
              <li key={entry.id} className="log__turn">
                {text.replace(/^回合推进至\s*/, "")}的回合
              </li>
            );
          return (
            <li key={entry.id} className={`log__item log__item--${entry.tone}`}>
              {text}
              {count > 1 && <span className="log__count">×{count}</span>}
            </li>
          );
        })}
      </ol>
    </section>
  );
}
