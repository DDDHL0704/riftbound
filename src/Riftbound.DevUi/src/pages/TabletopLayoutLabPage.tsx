import { type CSSProperties, useEffect, useMemo, useState } from "react";
import { Check, Clipboard, Download, RefreshCcw, Ruler, SlidersHorizontal, TriangleAlert } from "lucide-react";
import {
  BATTLEFIELD_TABLE_LAYOUT,
  PLAYER_TABLE_LAYOUT,
  RUNE_DECK_SIZE,
  type LayoutBox,
  type PlayerTableLayout,
  type TableSide,
  type TabletopLayoutData
} from "../components/match/tabletopLayout";
import { Button } from "../components/ui/Button";

const DRAFT_STORAGE_KEY = "riftbound.tabletopLayoutDraft.v1";

const PLAYER_ZONE_KEYS = ["legend", "champion", "score", "piles", "base", "runeBank", "hand"] as const;
type PlayerZoneKey = (typeof PLAYER_ZONE_KEYS)[number];

type PlayerElementRef = {
  kind: "player";
  side: TableSide;
  key: PlayerZoneKey;
  id: string;
  group: string;
  title: string;
};

type BattlefieldElementRef = {
  kind: "battlefield";
  index: number;
  id: string;
  group: string;
  title: string;
};

type LayoutElementRef = PlayerElementRef | BattlefieldElementRef;
type CopyState = "idle" | "copied" | "failed";

const sideLabels: Record<TableSide, string> = {
  self: "P1 我方",
  opponent: "P2 对手"
};

const zoneLabels: Record<PlayerZoneKey, string> = {
  legend: "传奇槽",
  champion: "英雄槽",
  score: "分数 / 主牌堆",
  piles: "牌堆 / 弃牌",
  base: "基地",
  runeBank: "符文轨",
  hand: "手牌"
};

function cloneBox(box: LayoutBox): LayoutBox {
  return { ...box };
}

function clonePlayerLayout(layout: PlayerTableLayout): PlayerTableLayout {
  return {
    legend: cloneBox(layout.legend),
    champion: cloneBox(layout.champion),
    score: cloneBox(layout.score),
    piles: cloneBox(layout.piles),
    base: cloneBox(layout.base),
    runeBank: cloneBox(layout.runeBank),
    hand: cloneBox(layout.hand)
  };
}

function baseLayout(): TabletopLayoutData {
  return {
    runeDeckSize: RUNE_DECK_SIZE,
    players: {
      self: clonePlayerLayout(PLAYER_TABLE_LAYOUT.self),
      opponent: clonePlayerLayout(PLAYER_TABLE_LAYOUT.opponent)
    },
    battlefields: BATTLEFIELD_TABLE_LAYOUT.map(cloneBox)
  };
}

function cloneLayout(layout: TabletopLayoutData): TabletopLayoutData {
  return {
    runeDeckSize: layout.runeDeckSize,
    players: {
      self: clonePlayerLayout(layout.players.self),
      opponent: clonePlayerLayout(layout.players.opponent)
    },
    battlefields: layout.battlefields.map(cloneBox)
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function isLayoutBox(value: unknown): value is LayoutBox {
  if (!isRecord(value)) {
    return false;
  }

  return (
    typeof value.id === "string" &&
    typeof value.label === "string" &&
    typeof value.x === "number" &&
    typeof value.y === "number" &&
    typeof value.width === "number" &&
    typeof value.height === "number"
  );
}

function isPlayerLayout(value: unknown): value is PlayerTableLayout {
  if (!isRecord(value)) {
    return false;
  }

  return PLAYER_ZONE_KEYS.every((key) => isLayoutBox(value[key]));
}

function isLayoutData(value: unknown): value is TabletopLayoutData {
  if (!isRecord(value) || !isRecord(value.players) || !Array.isArray(value.battlefields)) {
    return false;
  }

  return typeof value.runeDeckSize === "number" && isPlayerLayout(value.players.self) && isPlayerLayout(value.players.opponent) && value.battlefields.every(isLayoutBox);
}

function loadInitialLayout(): TabletopLayoutData {
  if (typeof window === "undefined") {
    return baseLayout();
  }

  const rawDraft = window.localStorage.getItem(DRAFT_STORAGE_KEY);
  if (!rawDraft) {
    return baseLayout();
  }

  try {
    const parsed = JSON.parse(rawDraft) as unknown;
    return isLayoutData(parsed) ? parsed : baseLayout();
  } catch {
    return baseLayout();
  }
}

function buildElementRefs(layout: TabletopLayoutData): LayoutElementRef[] {
  const playerRefs = (["opponent", "self"] as const).flatMap((side) =>
    PLAYER_ZONE_KEYS.map((key): PlayerElementRef => {
      const box = layout.players[side][key];
      return {
        kind: "player",
        side,
        key,
        id: box.id,
        group: sideLabels[side],
        title: zoneLabels[key]
      };
    })
  );

  const battlefieldRefs = layout.battlefields.map(
    (box, index): BattlefieldElementRef => ({
      kind: "battlefield",
      index,
      id: box.id,
      group: "公共战场",
      title: box.label
    })
  );

  return [...playerRefs.slice(0, PLAYER_ZONE_KEYS.length), ...battlefieldRefs, ...playerRefs.slice(PLAYER_ZONE_KEYS.length)];
}

function getBox(layout: TabletopLayoutData, ref: LayoutElementRef): LayoutBox {
  if (ref.kind === "battlefield") {
    return layout.battlefields[ref.index];
  }

  return layout.players[ref.side][ref.key];
}

function updateBox(layout: TabletopLayoutData, ref: LayoutElementRef, nextBox: LayoutBox): TabletopLayoutData {
  const nextLayout = cloneLayout(layout);
  if (ref.kind === "battlefield") {
    nextLayout.battlefields[ref.index] = nextBox;
    return nextLayout;
  }

  nextLayout.players[ref.side][ref.key] = nextBox;
  return nextLayout;
}

function clampCoordinate(value: number): number {
  if (!Number.isFinite(value)) {
    return 0;
  }

  return Math.max(-20, Math.min(120, Math.round(value * 10) / 10));
}

function formatCoordinate(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}

function boxStyle(box: LayoutBox): CSSProperties {
  return {
    left: `${box.x}%`,
    top: `${box.y}%`,
    width: `${box.width}%`,
    height: `${box.height}%`
  };
}

function boxWarnings(ref: LayoutElementRef, box: LayoutBox): string[] {
  const warnings: string[] = [];
  if (box.width <= 0 || box.height <= 0) {
    warnings.push(`${ref.title} 尺寸必须大于 0`);
  }
  if (box.x < 0 || box.y < 0 || box.x + box.width > 100 || box.y + box.height > 100) {
    warnings.push(`${ref.group} / ${ref.title} 超出 100% 桌面边界`);
  }
  return warnings;
}

function buildLayoutWarnings(layout: TabletopLayoutData): string[] {
  const refs = buildElementRefs(layout);
  const warnings = refs.flatMap((ref) => boxWarnings(ref, getBox(layout, ref)));
  if (layout.runeDeckSize !== 12) {
    warnings.unshift("符文堆按规则应固定为 12 张");
  }
  return warnings;
}

function downloadText(filename: string, text: string) {
  const blob = new Blob([text], { type: "application/json;charset=utf-8" });
  const href = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = href;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(href);
}

export function TabletopLayoutLabPage() {
  const [layout, setLayout] = useState<TabletopLayoutData>(() => loadInitialLayout());
  const [selectedId, setSelectedId] = useState(() => loadInitialLayout().players.self.legend.id);
  const [copyState, setCopyState] = useState<CopyState>("idle");

  const refs = useMemo(() => buildElementRefs(layout), [layout]);
  const selectedRef = refs.find((ref) => ref.id === selectedId) ?? refs[0];
  const selectedBox = selectedRef ? getBox(layout, selectedRef) : null;
  const exportedJson = useMemo(() => `${JSON.stringify(layout, null, 2)}\n`, [layout]);
  const warnings = useMemo(() => buildLayoutWarnings(layout), [layout]);

  useEffect(() => {
    if (!selectedRef && refs[0]) {
      setSelectedId(refs[0].id);
    }
  }, [refs, selectedRef]);

  useEffect(() => {
    window.localStorage.setItem(DRAFT_STORAGE_KEY, exportedJson);
  }, [exportedJson]);

  function patchSelectedBox(patch: Partial<LayoutBox>) {
    if (!selectedRef) {
      return;
    }

    setLayout((current) => {
      const currentBox = getBox(current, selectedRef);
      return updateBox(current, selectedRef, { ...currentBox, ...patch });
    });
  }

  async function copyJson() {
    try {
      await navigator.clipboard.writeText(exportedJson);
      setCopyState("copied");
      window.setTimeout(() => setCopyState("idle"), 1800);
    } catch {
      setCopyState("failed");
    }
  }

  function resetLayout() {
    const next = baseLayout();
    setLayout(next);
    setSelectedId(next.players.self.legend.id);
    setCopyState("idle");
  }

  return (
    <section className="layout-lab-page">
      <header className="layout-lab-header">
        <div>
          <span className="eyebrow">Tabletop Layout Lab</span>
          <h1>对战桌面布局手调台</h1>
          <p>所有坐标都来自运行时布局 JSON。你可以逐个修改区域位置，右侧导出后我再写回正式桌面。</p>
        </div>
        <div className="layout-lab-header-actions">
          <Button icon={<RefreshCcw size={16} />} onClick={resetLayout} variant="secondary">
            重置
          </Button>
          <Button icon={<Clipboard size={16} />} onClick={copyJson} variant="primary">
            {copyState === "copied" ? "已复制" : copyState === "failed" ? "复制失败" : "复制 JSON"}
          </Button>
        </div>
      </header>

      <div className="layout-lab-workbench">
        <aside className="layout-lab-panel layout-lab-list-panel">
          <div className="layout-lab-panel-title">
            <Ruler size={17} />
            <span>元素清单</span>
            <strong>{refs.length}</strong>
          </div>
          <div className="layout-element-list">
            {refs.map((ref) => {
              const box = getBox(layout, ref);
              const itemWarnings = boxWarnings(ref, box);
              const active = selectedRef?.id === ref.id;
              return (
                <button className={`layout-element-row ${active ? "is-active" : ""}`} key={ref.id} onClick={() => setSelectedId(ref.id)} type="button">
                  <span className={`layout-element-chip layout-element-chip-${ref.kind === "battlefield" ? "battlefield" : ref.side}`}>{ref.group}</span>
                  <span className="layout-element-name">{box.label || ref.title}</span>
                  <span className="layout-element-metrics">
                    x {formatCoordinate(box.x)} · y {formatCoordinate(box.y)} · {formatCoordinate(box.width)} x {formatCoordinate(box.height)}
                  </span>
                  {itemWarnings.length > 0 && <TriangleAlert aria-label="有边界警告" size={15} />}
                </button>
              );
            })}
          </div>
        </aside>

        <section className="layout-lab-preview-panel">
          <div className="layout-lab-preview-toolbar">
            <div>
              <span>实时桌面预览</span>
              <strong>百分比坐标 / 16:9</strong>
            </div>
            <span className={`layout-lab-status ${warnings.length > 0 ? "is-warning" : "is-ok"}`}>
              {warnings.length > 0 ? `${warnings.length} 个边界提醒` : "坐标有效"}
            </span>
          </div>
          <div className="layout-lab-surface" role="img" aria-label="当前对战桌面布局预览">
            <div className="layout-lab-grid" />
            {refs.map((ref) => {
              const box = getBox(layout, ref);
              const active = selectedRef?.id === ref.id;
              const sideClass = ref.kind === "battlefield" ? "battlefield" : ref.side;
              return (
                <button
                  aria-label={`${ref.group} ${box.label || ref.title}`}
                  className={`layout-lab-box layout-lab-box-${sideClass} ${active ? "is-active" : ""}`}
                  key={ref.id}
                  onClick={() => setSelectedId(ref.id)}
                  style={boxStyle(box)}
                  type="button"
                >
                  <span>{box.label || ref.title}</span>
                  <small>{ref.group}</small>
                </button>
              );
            })}
          </div>
        </section>

        <aside className="layout-lab-panel layout-lab-editor-panel">
          <div className="layout-lab-panel-title">
            <SlidersHorizontal size={17} />
            <span>坐标编辑</span>
          </div>

          {selectedRef && selectedBox && (
            <>
              <div className="layout-selected-card">
                <span className={`layout-element-chip layout-element-chip-${selectedRef.kind === "battlefield" ? "battlefield" : selectedRef.side}`}>{selectedRef.group}</span>
                <strong>{selectedBox.label || selectedRef.title}</strong>
                <small>{selectedBox.id}</small>
              </div>

              <label className="layout-field layout-field-wide">
                <span>显示名称</span>
                <input value={selectedBox.label} onChange={(event) => patchSelectedBox({ label: event.currentTarget.value })} />
              </label>

              <div className="layout-field-grid">
                <CoordinateField label="X" value={selectedBox.x} onChange={(value) => patchSelectedBox({ x: value })} />
                <CoordinateField label="Y" value={selectedBox.y} onChange={(value) => patchSelectedBox({ y: value })} />
                <CoordinateField label="宽" value={selectedBox.width} onChange={(value) => patchSelectedBox({ width: value })} />
                <CoordinateField label="高" value={selectedBox.height} onChange={(value) => patchSelectedBox({ height: value })} />
              </div>
            </>
          )}

          <div className="layout-warning-box">
            <div>
              {warnings.length > 0 ? <TriangleAlert size={16} /> : <Check size={16} />}
              <strong>{warnings.length > 0 ? "需要留意" : "布局检查"}</strong>
            </div>
            {warnings.length > 0 ? (
              <ul>
                {warnings.slice(0, 5).map((warning) => (
                  <li key={warning}>{warning}</li>
                ))}
              </ul>
            ) : (
              <p>符文堆数量和所有区域边界通过基础检查。</p>
            )}
          </div>

          <div className="layout-json-card">
            <div className="layout-json-card-header">
              <strong>导出布局 JSON</strong>
              <Button icon={<Download size={15} />} onClick={() => downloadText("tabletopLayoutData.json", exportedJson)} variant="secondary">
                下载
              </Button>
            </div>
            <textarea aria-label="布局 JSON 导出" readOnly value={exportedJson} />
          </div>
        </aside>
      </div>
    </section>
  );
}

function CoordinateField({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) {
  return (
    <label className="layout-field">
      <span>{label}</span>
      <input
        inputMode="decimal"
        max={120}
        min={-20}
        onChange={(event) => onChange(clampCoordinate(Number(event.currentTarget.value)))}
        step={0.5}
        type="number"
        value={formatCoordinate(value)}
      />
    </label>
  );
}
