import { useLayoutEffect, useState } from "react";
import { cue, setSoundEnabled, setSoundVolume, useSoundEnabled, useSoundVolume } from "../game/sound";

// A first-visit tour of the table: each region is outlined where it actually is on screen, with a
// short note on what it is for. It opens by itself once per browser and from the rail's "?" button.

const STEPS: { sel: string; title: string; text: string }[] = [
  { sel: ".spine", title: "计分轨", text: "上方是对手、下方是你的分数，先到 8 分获胜。征服或据守战场都能得分。" },
  { sel: ".side--opp", title: "对手区域", text: "对手的传奇与英雄、基地、符文和牌堆。点「废牌」「放逐」可以查看公开的牌。" },
  { sel: ".fields", title: "战场", text: "双方争夺的战场。把单位移动过去即可争夺；你控制的战场每回合可以据守得分。" },
  { sel: ".side--me", title: "你的区域", text: "传奇与英雄的技能按钮在旁边；基地放打出的单位；点符文横置得法力，或回收得符能。" },
  { sel: ".hand", title: "手牌", text: "把牌拖到桌面上打出，或点一下选择操作。发光的牌现在就能打出，带浅金边的会自动横置符文付费。" },
  { sel: ".dock", title: "行动按钮", text: "让过、结束回合（空格），以及一键横置全部符文（T）。没有可响应的牌时会自动让过。" },
  { sel: ".rail", title: "提示与记录", text: "顶部说明现在该做什么，中间是结算链（上方先结算），下方是对局记录。" },
];

const SEEN_KEY = "riftbound.web.guideSeen";

export function guideSeen() {
  try {
    return localStorage.getItem(SEEN_KEY) === "1";
  } catch {
    return true;
  }
}

export function TableGuide({ onClose }: { onClose: () => void }) {
  const [step, setStep] = useState(0);
  const [box, setBox] = useState<DOMRect | null>(null);
  const current = STEPS[step];

  useLayoutEffect(() => {
    const measure = () => setBox(document.querySelector(current.sel)?.getBoundingClientRect() ?? null);
    measure();
    // Regions can still be settling (cards animating in); look again once they have.
    const later = setTimeout(measure, 400);
    window.addEventListener("resize", measure);
    return () => {
      clearTimeout(later);
      window.removeEventListener("resize", measure);
    };
  }, [current.sel]);

  const close = () => {
    try {
      localStorage.setItem(SEEN_KEY, "1");
    } catch {
      /* shown again next time */
    }
    onClose();
  };
  const last = step === STEPS.length - 1;
  // The note sits below the outlined region when there is room, otherwise above it.
  const below = box ? box.bottom + 170 < window.innerHeight : true;
  const noteLeft = box ? Math.min(Math.max(12, box.left + box.width / 2 - 170), window.innerWidth - 352) : 12;
  const noteTop = box ? (below ? box.bottom + 12 : Math.max(12, box.top - 160)) : 12;

  return (
    <div className="guide" role="dialog" aria-label="桌面导览" onClick={(e) => e.stopPropagation()}>
      {box && <div className="guide__hole" style={{ left: box.left - 6, top: box.top - 6, width: box.width + 12, height: box.height + 12 }} />}
      <div className="guide__note" style={{ left: noteLeft, top: noteTop }}>
        <p className="guide__step">
          {step + 1} / {STEPS.length}
        </p>
        <h3>{current.title}</h3>
        <p>{current.text}</p>
        <div className="guide__actions">
          <button className="btn btn--ghost btn--small" onClick={close}>
            跳过
          </button>
          {step > 0 && (
            <button className="btn btn--small" onClick={() => setStep(step - 1)}>
              上一步
            </button>
          )}
          <button className="btn btn--primary btn--small" onClick={() => (last ? close() : setStep(step + 1))}>
            {last ? "开始对战" : "下一步"}
          </button>
        </div>
      </div>
    </div>
  );
}

/** Small popover in the rail footer: sound, volume and the keyboard shortcuts. */
export function SettingsPanel({ onClose, onGuide }: { onClose: () => void; onGuide: () => void }) {
  const on = useSoundEnabled();
  const volume = useSoundVolume();
  return (
    <div className="settings" role="dialog" aria-label="设置" onClick={(e) => e.stopPropagation()}>
      <h3>设置</h3>
      <label className="settings__row">
        <input type="checkbox" checked={on} onChange={(e) => setSoundEnabled(e.target.checked)} />
        音效
      </label>
      <label className="settings__row">
        音量
        <input
          type="range"
          min={5}
          max={100}
          value={Math.round(volume * 100)}
          disabled={!on}
          onChange={(e) => setSoundVolume(Number(e.target.value) / 100)}
          onPointerUp={() => cue("score-me")}
        />
        <span className="muted">{Math.round(volume * 100)}%</span>
      </label>
      <h4>快捷键</h4>
      <dl className="settings__keys">
        <dt>空格</dt>
        <dd>让过 / 结束回合（还有可行动的牌时按两次）</dd>
        <dt>T</dt>
        <dd>横置全部符文</dd>
        <dt>Enter</dt>
        <dd>确认当前选择</dd>
        <dt>Esc</dt>
        <dd>取消选择、关闭菜单</dd>
      </dl>
      <div className="guide__actions">
        <button className="btn btn--small" onClick={onGuide}>
          桌面导览
        </button>
        <button className="btn btn--primary btn--small" onClick={onClose}>
          完成
        </button>
      </div>
    </div>
  );
}
