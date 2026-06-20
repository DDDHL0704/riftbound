import type { CardObjectView, GameEvent, GameEventObjectRef, ZoneView } from "../../types/protocol";
import type { WirePlayerEntry, WireTableViewModel } from "./wireTableViewModel";

export type WireInformationBoundaryState = "leak" | "missing" | "mixed" | "safe";

export type WireInformationBoundaryMetric = {
  key: string;
  label: string;
  state: WireInformationBoundaryState;
  value: string;
};

export type WireInformationBoundaryRow = {
  detail: string;
  key: string;
  label: string;
  state: WireInformationBoundaryState;
  stateLabel: string;
  value: string;
};

export type WireInformationBoundaryPlan = {
  issueCount: number;
  metrics: WireInformationBoundaryMetric[];
  rows: WireInformationBoundaryRow[];
  state: WireInformationBoundaryState;
  stateLabel: string;
  summary: string;
};

export function buildWireInformationBoundaryPlan({
  events = [],
  table
}: {
  events?: GameEvent[];
  table: WireTableViewModel;
}): WireInformationBoundaryPlan {
  const playerRows = table.players.flatMap((entry) => [
    handBoundaryRow(entry),
    deckBoundaryRow(entry)
  ]);
  const rows = [
    ...playerRows,
    faceDownBoundaryRow(table),
    eventRefBoundaryRow(events)
  ];
  const issueCount = rows.filter((row) => row.state !== "safe").length;
  const state = aggregateState(rows.map((row) => row.state));

  return {
    issueCount,
    metrics: [
      metric("players", "玩家", "safe", `${table.players.length}`),
      metric("hands", "隐藏手牌", handMetricState(table.players), `${hiddenHandCount(table.players)}`),
      metric("decks", "牌堆计数", deckMetricState(table.players), `${deckCountRows(table.players)} / ${table.players.length}`),
      metric("faceDown", "盖放对象", faceDownMetricState(table), `${faceDownObjects(table).length}`),
      metric("eventRefs", "隐藏引用", eventMetricState(events), `${hiddenEventRefs(events).length}`),
      metric("issues", "待处理", issueCount === 0 ? "safe" : state, String(issueCount))
    ],
    rows,
    state,
    stateLabel: stateLabel(state),
    summary: summary(state, issueCount)
  };
}

function handBoundaryRow(entry: WirePlayerEntry): WireInformationBoundaryRow {
  const visible = entry.handIds.length;
  const hidden = entry.hiddenHandIds.length;
  const declaredHidden = numberValue(entry.zones.handHidden ?? (entry.side === "opponent" ? entry.player.handSize : undefined));

  if (entry.side === "self") {
    if (hidden > 0 && visible === 0) {
      return row(
        `hand:${entry.id}`,
        `${entry.label} 手牌`,
        "missing",
        "己方手牌被遮蔽",
        `可见 ${visible} / 隐藏 ${hidden}`,
        "己方客户端应收到自己的手牌对象。"
      );
    }

    return row(
      `hand:${entry.id}`,
      `${entry.label} 手牌`,
      "safe",
      "己方可见",
      `可见 ${visible}`,
      "己方手牌属于有权可见信息。"
    );
  }

  if (visible > 0) {
    return row(
      `hand:${entry.id}`,
      `${entry.label} 手牌`,
      "leak",
      "泄漏手牌对象",
      `可见 ${visible} / 隐藏 ${hidden}`,
      "对手手牌不应以对象列表发送给当前客户端。"
    );
  }

  if (hidden > 0 || declaredHidden != null) {
    return row(
      `hand:${entry.id}`,
      `${entry.label} 手牌`,
      "safe",
      "仅公开数量",
      `隐藏 ${hidden || declaredHidden || 0}`,
      "对手手牌只显示数量，不暴露身份或顺序。"
    );
  }

  return row(
    `hand:${entry.id}`,
    `${entry.label} 手牌`,
    "missing",
    "缺少数量",
    "无计数",
    "服务端应至少提供对手手牌数量。"
  );
}

function deckBoundaryRow(entry: WirePlayerEntry): WireInformationBoundaryRow {
  const zones = asZoneRecord(entry.zones);
  const mainDeckIds = stringArray(zones.mainDeck);
  const runeDeckIds = stringArray(zones.runeDeck);
  const mainCount = numberValue(entry.zones.mainDeckCount);
  const runeCount = numberValue(entry.zones.runeDeckCount);
  const leakedCount = mainDeckIds.length + runeDeckIds.length;

  if (leakedCount > 0) {
    return row(
      `deck:${entry.id}`,
      `${entry.label} 牌堆`,
      "leak",
      "泄漏牌堆顺序",
      `主牌 ${mainDeckIds.length} / 符文 ${runeDeckIds.length}`,
      "主牌堆和符文牌堆顺序是隐秘信息，客户端只能拿到数量。"
    );
  }

  if (mainCount != null && runeCount != null) {
    return row(
      `deck:${entry.id}`,
      `${entry.label} 牌堆`,
      "safe",
      "仅公开数量",
      `主牌 ${mainCount} / 符文 ${runeCount}`,
      "牌堆身份和顺序未进入当前视角快照。"
    );
  }

  return row(
    `deck:${entry.id}`,
    `${entry.label} 牌堆`,
    "missing",
    "缺少计数",
    `主牌 ${mainCount ?? "?"} / 符文 ${runeCount ?? "?"}`,
    "桌面需要主牌堆和符文牌堆数量来渲染隐藏牌堆。"
  );
}

function faceDownBoundaryRow(table: WireTableViewModel): WireInformationBoundaryRow {
  const faceDown = faceDownObjects(table);
  const leaked = faceDown.filter((object) => typeof object.cardNo === "string" && object.cardNo.trim().length > 0);

  if (leaked.length > 0) {
    return row(
      "faceDown",
      "盖放 / 隐藏对象",
      "leak",
      "泄漏牌号",
      `${leaked.length} / ${faceDown.length}`,
      "正面朝下对象不应向无权视角公开 cardNo。"
    );
  }

  return row(
    "faceDown",
    "盖放 / 隐藏对象",
    "safe",
    faceDown.length > 0 ? "已遮蔽牌号" : "无盖放对象",
    `${faceDown.length}`,
    "当前快照未公开盖放对象牌号。"
  );
}

function eventRefBoundaryRow(events: GameEvent[]): WireInformationBoundaryRow {
  const hiddenRefs = hiddenEventRefs(events);
  const cardNoLeaks = hiddenRefs.filter(hasCardNo);
  const realObjectIds = hiddenRefs.filter((ref) => ref.objectId && ref.objectId !== "HIDDEN");
  const visibleFaceDownObjectIds = realObjectIds.filter((ref) => ref.isFaceDown && !hasCardNo(ref));
  const unresolvedRealObjectIds = realObjectIds.filter((ref) => !ref.isFaceDown || hasCardNo(ref));

  if (cardNoLeaks.length > 0) {
    return row(
      "eventRefs",
      "事件隐藏引用",
      "leak",
      "泄漏隐藏牌号",
      `${cardNoLeaks.length} / ${hiddenRefs.length}`,
      "隐藏事件引用不能携带真实 cardNo。"
    );
  }

  if (unresolvedRealObjectIds.length > 0) {
    return row(
      "eventRefs",
      "事件隐藏引用",
      "mixed",
      "隐藏对象带真实 ID",
      `${unresolvedRealObjectIds.length} / ${hiddenRefs.length}`,
      "隐藏引用已遮蔽牌号，但仍携带真实 objectId，后端应确认该 ID 是否允许公开。"
    );
  }

  return row(
    "eventRefs",
    "事件隐藏引用",
    "safe",
    visibleFaceDownObjectIds.length > 0 ? "盖放身份遮蔽" : hiddenRefs.length > 0 ? "隐藏占位" : "无隐藏引用",
    `${hiddenRefs.length}`,
    visibleFaceDownObjectIds.length > 0
      ? "事件引用保留可定位对象 ID，但未公开盖放对象牌号。"
      : "事件引用未公开隐藏对象身份。"
  );
}

function metric(
  key: string,
  label: string,
  state: WireInformationBoundaryState,
  value: string
): WireInformationBoundaryMetric {
  return { key, label, state, value };
}

function row(
  key: string,
  label: string,
  state: WireInformationBoundaryState,
  stateLabel: string,
  value: string,
  detail: string
): WireInformationBoundaryRow {
  return { detail, key, label, state, stateLabel, value };
}

function aggregateState(states: WireInformationBoundaryState[]): WireInformationBoundaryState {
  if (states.some((state) => state === "leak")) {
    return "leak";
  }

  if (states.some((state) => state === "mixed")) {
    return "mixed";
  }

  if (states.some((state) => state === "missing")) {
    return "missing";
  }

  return "safe";
}

function stateLabel(state: WireInformationBoundaryState): string {
  switch (state) {
    case "safe":
      return "边界安全";
    case "mixed":
      return "需确认";
    case "missing":
      return "材料缺失";
    case "leak":
      return "存在泄漏";
  }
}

function summary(state: WireInformationBoundaryState, issueCount: number): string {
  switch (state) {
    case "safe":
      return "当前视角只接收公开信息和允许公开的数量。";
    case "mixed":
      return `仍有 ${issueCount} 项隐藏信息边界需要确认。`;
    case "missing":
      return `仍有 ${issueCount} 项公开计数或视角材料缺失。`;
    case "leak":
      return `发现 ${issueCount} 项潜在隐藏信息泄漏，必须优先修复。`;
  }
}

function handMetricState(players: WirePlayerEntry[]): WireInformationBoundaryState {
  return aggregateState(players.map((entry) => handBoundaryRow(entry).state));
}

function deckMetricState(players: WirePlayerEntry[]): WireInformationBoundaryState {
  return aggregateState(players.map((entry) => deckBoundaryRow(entry).state));
}

function eventMetricState(events: GameEvent[]): WireInformationBoundaryState {
  return eventRefBoundaryRow(events).state;
}

function faceDownMetricState(table: WireTableViewModel): WireInformationBoundaryState {
  return faceDownBoundaryRow(table).state;
}

function hiddenHandCount(players: WirePlayerEntry[]): number {
  return players.reduce((sum, entry) => sum + entry.hiddenHandIds.length, 0);
}

function deckCountRows(players: WirePlayerEntry[]): number {
  return players.filter((entry) =>
    numberValue(entry.zones.mainDeckCount) != null
    && numberValue(entry.zones.runeDeckCount) != null
    && stringArray(asZoneRecord(entry.zones).mainDeck).length === 0
    && stringArray(asZoneRecord(entry.zones).runeDeck).length === 0).length;
}

function faceDownObjects(table: WireTableViewModel): CardObjectView[] {
  return table.players
    .flatMap((entry) => Object.values(entry.objects))
    .filter((object): object is CardObjectView => Boolean(object?.isFaceDown));
}

function hiddenEventRefs(events: GameEvent[]): GameEventObjectRef[] {
  return events.flatMap((event) =>
    (event.objectRefs ?? []).filter((ref) => ref.isHidden || ref.objectId === "HIDDEN"));
}

function hasCardNo(ref: GameEventObjectRef): boolean {
  return typeof ref.cardNo === "string" && ref.cardNo.trim().length > 0;
}

function asZoneRecord(zones: ZoneView): Record<string, unknown> {
  return zones as Record<string, unknown>;
}

function stringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === "string") : [];
}

function numberValue(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}
