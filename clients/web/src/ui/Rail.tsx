import { useEffect, useMemo, useRef, useState } from "react";
import { client, type ClientState } from "../net/client";
import { cardName } from "../game/cards";
import { enabledCandidate } from "../game/actions";
import type { Candidate, Choice, GameObject, Prompt, Snapshot } from "../game/types";
import { Card, EffectText } from "./Card";
import { TEMPO_ACTIONS } from "./Overlay";
import type { Hover } from "./Table";
import { cardInfo } from "../game/cards";

type RailProps = {
  state: ClientState;
  snapshot: Snapshot;
  prompt: Prompt | null;
  objects: Record<string, GameObject>;
  mulligan: string[];
  onHover: (cardNo: string | null, o?: GameObject) => void;
  preview: Hover;
};

function nameOf(id: string, objects: Record<string, GameObject>, fallback?: string) {
  const o = objects[id];
  return o?.cardNo ? cardName(o.cardNo) : fallback ?? id;
}

export function Rail({ state, snapshot, prompt, objects, mulligan, onHover, preview }: RailProps) {
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
        {type === "BATTLE_DECLARATION" && <BattlePanel prompt={prompt} objects={objects} />}
        {type === "ASSIGN_COMBAT_DAMAGE" && <DamagePanel prompt={prompt} objects={objects} />}
        {type === "ORDER_TRIGGERS" && <TriggerPanel prompt={prompt} objects={objects} />}
        {(type === "HAND_CHOICE" || type === "CARD_CHOICE" || type === "PAY_COST") && (
          <ChoicePanel prompt={prompt} objects={objects} onHover={onHover} />
        )}
        <GenericPanel prompt={prompt} />
      </div>

      <Preview preview={preview} snapshot={snapshot} objects={objects} />
      <StackView snapshot={snapshot} objects={objects} meId={meId} onHover={onHover} />
      <Log entries={state.log} objects={objects} />

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
      <p>点击手牌，选出至多 {max} 张放回牌库底并重抽。</p>
      {selected.length > 0 && (
        <p className="muted">已选：{selected.map((id) => nameOf(id, objects)).join("、")}</p>
      )}
      <button
        className="btn btn--primary btn--wide"
        onClick={() => void client.submit({ cmdType: "MULLIGAN", handObjectIds: selected })}
      >
        {selected.length ? `换掉 ${selected.length} 张` : "保留起手"}
      </button>
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
  if (!cand || !req) return null;
  const attackers = fillByIndex(req.attackerChoicesByIndex, req.maxAttackerCount ?? req.minAttackerCount);
  const defenders = fillByIndex(req.targetChoicesByIndex, req.maxDefenderCount ?? req.minDefenderCount);
  const battlefieldId: string | undefined = req.battlefieldChoices?.[0]?.id;
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
      <button
        className="btn btn--primary btn--wide"
        onClick={() =>
          void client.submit({
            cmdType: "DECLARE_BATTLE",
            battlefieldId,
            attackerObjectIds: attackers,
            defenderObjectIds: defenders,
            optionalCosts: req.requiredOptionalCosts ?? [],
          })
        }
      >
        开战
      </button>
    </div>
  );
}

type Assignment = { sourceObjectId: string; targetObjectId: string; damage: number };

function suggestAssignments(md: Record<string, any>): Assignment[] {
  const required: { sourceObjectId: string; damage: number; legalTargetObjectIds: string[] }[] = md.requiredAssignments ?? [];
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

function DamagePanel({ prompt, objects }: { prompt: Prompt | null; objects: Record<string, GameObject> }) {
  const cand = enabledCandidate(prompt, "ASSIGN_COMBAT_DAMAGE");
  const md = cand?.metadata;
  const [plan, setPlan] = useState<Assignment[]>([]);
  useEffect(() => {
    setPlan(md ? suggestAssignments(md) : []);
  }, [prompt?.promptId]); // eslint-disable-line react-hooks/exhaustive-deps
  if (!cand || !md) return null;
  const required: { sourceObjectId: string; damage: number; legalTargetObjectIds: string[] }[] = md.requiredAssignments ?? [];
  const lethal: Record<string, number> = md.lethalDamageThreshold ?? {};
  const existing: Record<string, number> = md.existingDamage ?? {};

  const amount = (s: string, t: string) => plan.find((a) => a.sourceObjectId === s && a.targetObjectId === t)?.damage ?? 0;
  const bump = (s: string, t: string, delta: number, total: number) => {
    setPlan((cur) => {
      const used = cur.filter((a) => a.sourceObjectId === s).reduce((n, a) => n + a.damage, 0);
      const now = amount(s, t);
      const next = Math.max(0, now + delta);
      if (delta > 0 && used >= total) return cur;
      const rest = cur.filter((a) => !(a.sourceObjectId === s && a.targetObjectId === t));
      return next > 0 ? [...rest, { sourceObjectId: s, targetObjectId: t, damage: next }] : rest;
    });
  };
  const complete = required.every((r) => plan.filter((a) => a.sourceObjectId === r.sourceObjectId).reduce((n, a) => n + a.damage, 0) === r.damage);

  return (
    <div className="panel panel--damage">
      {md.assignmentGuidance && <p className="muted small">{md.assignmentGuidance}</p>}
      {required.map((r) => (
        <div key={r.sourceObjectId} className="dmg">
          <p className="dmg__src">
            {nameOf(r.sourceObjectId, objects)} 造成 <b>{r.damage}</b> 点
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
        disabled={!complete}
        onClick={() =>
          void client.submit({
            cmdType: "ASSIGN_COMBAT_DAMAGE",
            battleId: md.battleId,
            battlefieldId: md.battlefieldId,
            assignments: plan.filter((a) => a.damage > 0),
          })
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
  const cand: Candidate | undefined =
    enabledCandidate(prompt, "CHOOSE_HAND_CARDS") ?? enabledCandidate(prompt, "CHOOSE_CARDS") ?? enabledCandidate(prompt, "PAY_COST");
  const [picked, setPicked] = useState<string[]>([]);
  useEffect(() => setPicked([]), [prompt?.promptId]);
  const choices = useMemo(() => {
    const seen = new Map<string, Choice>();
    for (const s of cand?.selectionSteps ?? []) for (const c of s.choices) if (!seen.has(c.id)) seen.set(c.id, c);
    return [...seen.values()];
  }, [cand]);
  if (!cand) return null;
  const md = cand.metadata ?? {};
  const isPay = cand.action === "PAY_COST";
  const steps = cand.selectionSteps ?? [];
  const min = isPay ? (steps.some((s) => s.required) ? 1 : 0) : Number(md.requiredCount ?? 0);
  const max = isPay ? 1 : Number(md.maxCount ?? steps.length ?? 1);
  const toggle = (id: string) =>
    setPicked((cur) => (cur.includes(id) ? cur.filter((x) => x !== id) : max === 1 ? [id] : cur.length < max ? [...cur, id] : cur));

  const submit = () => {
    if (isPay) {
      void client.submit({ cmdType: "PAY_COST", paymentId: md.paymentId, paymentWindow: md.paymentWindow, paymentChoiceIds: picked });
    } else {
      void client.submit({ cmdType: cand.action, choiceId: md.choiceId, choiceWindow: md.choiceWindow, chosenObjectIds: picked });
    }
  };

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

/** A steady place to read cards: the hovered card, else whatever is about to resolve. */
function Preview({ preview, snapshot, objects }: { preview: Hover; snapshot: Snapshot; objects: Record<string, GameObject> }) {
  const top = snapshot.stack?.length ? snapshot.stack[snapshot.stack.length - 1] : undefined;
  const topNo = top && top.sourceVisibility !== "HIDDEN" ? ((top.cardNo as string) ?? objects[top.sourceObjectId as string]?.cardNo) : undefined;
  const cardNo = preview?.cardNo ?? topNo;
  const object = preview?.object ?? (top ? objects[top.sourceObjectId as string] : undefined);
  const info = cardInfo(cardNo);
  if (!cardNo || !info) {
    return (
      <section className="preview preview--empty">
        <p className="muted small">把鼠标移到卡牌上查看详情。</p>
      </section>
    );
  }
  return (
    <section className="preview" aria-live="polite">
      {info.image && <img className="preview__art" src={info.image} alt="" />}
      <div className="preview__body">
        <h3 className="preview__name">{info.name}</h3>
        <p className="preview__meta">
          {info.category}
          {info.cost != null && <span className="sym sym--mana">{info.cost}</span>}
          {object?.tags?.includes("CARD_TYPE:UNIT") && (
            <span>
              战力 {object.effectivePower}
              {object.damage > 0 && `，伤 ${object.damage}`}
            </span>
          )}
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
              onMouseEnter={() => onHover(no ?? null)}
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
  if (!pattern) return text;
  return text.replace(/STACK-\d+-/g, "").replace(pattern, (id) => `「${cardName(objects[id]?.cardNo)}」`);
}

function Log({ entries, objects }: { entries: ClientState["log"]; objects: Record<string, GameObject> }) {
  const ref = useRef<HTMLOListElement>(null);
  const pattern = useMemo(() => {
    const ids = Object.keys(objects).filter((id) => objects[id]?.cardNo).sort((a, b) => b.length - a.length);
    if (!ids.length) return null;
    return new RegExp(ids.map((id) => id.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|"), "g");
  }, [objects]);
  useEffect(() => {
    ref.current?.scrollTo({ top: ref.current.scrollHeight });
  }, [entries.length]);
  return (
    <section className="log" aria-label="对局记录">
      <h3 className="rail__sub">对局记录</h3>
      <ol ref={ref}>
        {entries.map((e) => (
          <li key={e.id} className={`log__item log__item--${e.tone}`}>
            {readable(e.text, objects, pattern)}
          </li>
        ))}
      </ol>
    </section>
  );
}
