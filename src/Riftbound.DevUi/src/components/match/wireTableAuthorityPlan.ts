import type {
  WireBasePartitionSource,
  WireBattlefieldOccupantSplitSource,
  WireBattlefieldStandbySlotSource,
  WireTableViewModel
} from "./wireTableViewModel";
import {
  buildWireCardFlowPlan,
  resolveWireCardFlowRenderPlan,
  type WireCardFlowKind,
  type WireCardFlowPlan
} from "./wireCardFlowPlan";

export type WireTableAuthorityState = "fallback" | "missing" | "mixed" | "server";
export type WireTableCapacityState = "empty" | "stable" | "scroll";
export type WireTableConsistencyState = "consistent" | "drift" | "missing";

export type WireTableAuthorityMetric = {
  key: string;
  label: string;
  state: WireTableAuthorityState;
  value: string;
};

export type WireTableAuthorityPlayerRow = {
  baseCount: number;
  key: string;
  label: string;
  runeCount: number;
  source: WireBasePartitionSource | "missing";
  sourceLabel: string;
  state: WireTableAuthorityState;
};

export type WireTableAuthorityLaneRow = {
  battlefieldId: string;
  hiddenStandbyCount: number;
  key: string;
  label: string;
  opposingCount: number;
  ownCount: number;
  standbyCount: number;
  standbySource: WireBattlefieldStandbySlotSource | "missing";
  standbySourceLabel: string;
  standbyState: WireTableAuthorityState;
  source: WireBattlefieldOccupantSplitSource | "missing";
  sourceLabel: string;
  state: WireTableAuthorityState;
};

export type WireTableConsistencyRow = {
  cardHeight: number;
  cardWidth: number;
  density: string;
  expectedKind: WireCardFlowKind;
  fit: string;
  itemCount: number;
  key: string;
  label: string;
  layout: string;
  overflow: string;
  slotCount: number;
  state: WireTableConsistencyState;
  stateLabel: string;
  visibleSlotCount: number;
};

export type WireTableCapacityRow = {
  cardHeight: number;
  cardWidth: number;
  density: string;
  fit: string;
  itemCount: number;
  key: string;
  kind: WireCardFlowKind;
  label: string;
  overflow: string;
  overflowCount: number;
  slotCount: number;
  state: WireTableCapacityState;
  stateLabel: string;
  visibleSlotCount: number;
};

export type WireTableSelectedLayoutKind =
  | WireCardFlowKind
  | "fixed-pile"
  | "none"
  | "rune-track"
  | "signature"
  | "site";

export type WireTableSelectedLayoutState = "empty" | "located" | "unknown";

export type WireTableSelectedLayoutPlan = {
  capacityRowKey?: string;
  kind: WireTableSelectedLayoutKind;
  objectId?: string;
  source: string;
  state: WireTableSelectedLayoutState;
  stateLabel: string;
  summary: string;
  zoneKey?: string;
  zoneLabel: string;
};

export type WireTableAuthorityPlan = {
  capacityRows: WireTableCapacityRow[];
  consistencyIssueCount: number;
  consistencyRows: WireTableConsistencyRow[];
  consistencyState: WireTableConsistencyState;
  issueCount: number;
  lanes: WireTableAuthorityLaneRow[];
  metrics: WireTableAuthorityMetric[];
  players: WireTableAuthorityPlayerRow[];
  selectedLayout: WireTableSelectedLayoutPlan;
  state: WireTableAuthorityState;
  stateLabel: string;
  summary: string;
};

const REQUIRED_PLAYER_COUNT = 2;
const REQUIRED_LANE_COUNT = 2;

export function buildWireTableAuthorityPlan(
  table: WireTableViewModel,
  options: { selectedObjectId?: string } = {}
): WireTableAuthorityPlan {
  const players = table.players.map((entry): WireTableAuthorityPlayerRow => ({
    baseCount: entry.baseObjectIds.length,
    key: entry.id,
    label: entry.label,
    runeCount: entry.runeIds.length,
    source: entry.basePartitionSource,
    sourceLabel: basePartitionSourceLabel(entry.basePartitionSource),
    state: basePartitionState(entry.basePartitionSource)
  }));
  const lanes = table.battlefield.lanes.map((lane): WireTableAuthorityLaneRow => ({
    battlefieldId: lane.battlefieldId,
    key: `${lane.index}:${lane.battlefieldId}`,
    label: lane.index === 0 ? "左战场" : "右战场",
    hiddenStandbyCount: lane.hiddenStandbyCount,
    opposingCount: lane.opposingOccupants.length,
    ownCount: lane.ownOccupants.length,
    standbyCount: lane.standbySlots.length,
    standbySource: lane.standbySlotSource,
    standbySourceLabel: standbySlotSourceLabel(lane.standbySlotSource),
    standbyState: standbySlotState(lane.standbySlotSource),
    source: lane.occupantSplitSource,
    sourceLabel: battlefieldSplitSourceLabel(lane.occupantSplitSource),
    state: battlefieldSplitState(lane.occupantSplitSource)
  }));

  const missingPlayerCount = Math.max(0, REQUIRED_PLAYER_COUNT - players.length);
  const missingLaneCount = Math.max(0, REQUIRED_LANE_COUNT - lanes.length);
  const capacityRows = buildCapacityRows(table);
  const selectedLayout = buildSelectedLayoutPlan(table, capacityRows, options.selectedObjectId);
  const consistencyRows = buildConsistencyRows(table);
  const consistencyIssueCount = consistencyRows.filter((row) => row.state !== "consistent").length;
  const consistencyState = resolveConsistencyState(consistencyRows.map((row) => row.state));
  const rows = [
    ...players,
    ...lanes.flatMap((lane) => [
      { key: `${lane.key}:units`, state: lane.state },
      { key: `${lane.key}:standby`, state: lane.standbyState }
    ]),
    ...Array.from({ length: missingPlayerCount + missingLaneCount * 2 }, (_, index) => ({ state: "missing" as const, key: `missing-${index}` }))
  ];
  const issueCount = rows.filter((row) => row.state !== "server").length;
  const state = resolveAuthorityState(rows.map((row) => row.state));

  return {
    capacityRows,
    consistencyIssueCount,
    consistencyRows,
    consistencyState,
    issueCount,
    lanes,
    metrics: [
      {
        key: "players",
        label: "玩家基础区",
        state: missingPlayerCount > 0 ? "missing" : aggregateRows(players.map((row) => row.state)),
        value: `${players.filter((row) => row.state === "server").length}/${REQUIRED_PLAYER_COUNT}`
      },
      {
        key: "battlefields",
        label: "战场分边",
        state: missingLaneCount > 0 ? "missing" : aggregateRows(lanes.map((row) => row.state)),
        value: `${lanes.filter((row) => row.state === "server").length}/${REQUIRED_LANE_COUNT}`
      },
      {
        key: "standbySlots",
        label: "待命槽位",
        state: missingLaneCount > 0 ? "missing" : aggregateRows(lanes.map((row) => row.standbyState)),
        value: `${lanes.filter((row) => row.standbyState === "server").length}/${REQUIRED_LANE_COUNT}`
      },
      {
        key: "layoutPlans",
        label: "共享布局计划",
        state: consistencyMetricState(consistencyState),
        value: `${consistencyRows.filter((row) => row.state === "consistent").length}/${consistencyRows.length}`
      },
      {
        key: "issues",
        label: "待后端补齐",
        state: issueCount === 0 ? "server" : state,
        value: String(issueCount)
      }
    ],
    players,
    selectedLayout,
    state,
    stateLabel: authorityStateLabel(state),
    summary: authoritySummary(state, issueCount)
  };
}

function buildCapacityRows(table: WireTableViewModel): WireTableCapacityRow[] {
  return [
    ...table.players.flatMap((player) => [
      capacityRow({
        itemCount: player.baseObjectIds.length,
        key: `${player.side}:base`,
        kind: "base",
        label: `${player.label} 基地流`,
        minSlots: 1,
        sizingPlan: table.playerPlans.basePlan
      }),
      capacityRow({
        itemCount: player.side === "opponent" ? player.hiddenHandIds.length : player.handIds.length,
        key: `${player.side}:hand`,
        kind: "hand",
        label: `${player.label} 手牌流`,
        sizingPlan: table.playerPlans.handPlan
      })
    ]),
    ...table.battlefield.lanes.flatMap((lane) => [
      capacityRow({
        itemCount: lane.opposingOccupants.length,
        key: `battlefield:${lane.index}:opponent`,
        kind: "battlefield-unit",
        label: `${lane.index === 0 ? "左战场" : "右战场"} 对方单位`,
        minSlots: 3,
        sizingPlan: table.battlefield.unitPlan
      }),
      capacityRow({
        itemCount: lane.ownOccupants.length,
        key: `battlefield:${lane.index}:self`,
        kind: "battlefield-unit",
        label: `${lane.index === 0 ? "左战场" : "右战场"} 我方单位`,
        minSlots: 3,
        sizingPlan: table.battlefield.unitPlan
      }),
      capacityRow({
        itemCount: lane.standbySlots.length,
        key: `battlefield:${lane.index}:standby`,
        kind: "standby",
        label: `${lane.index === 0 ? "左战场" : "右战场"} 待命槽`,
        minSlots: 1,
        sizingPlan: table.battlefield.standbyPlan
      })
    ])
  ];
}

function capacityRow({
  itemCount,
  key,
  kind,
  label,
  minSlots = 0,
  sizingPlan
}: {
  itemCount: number;
  key: string;
  kind: WireCardFlowKind;
  label: string;
  minSlots?: number;
  sizingPlan?: WireCardFlowPlan;
}): WireTableCapacityRow {
  const safeItemCount = Math.max(0, itemCount);
  const safeMinSlots = Math.max(0, minSlots);
  const slotCount = Math.max(safeItemCount, safeMinSlots);
  const plan = resolveWireCardFlowRenderPlan({
    itemCount: safeItemCount,
    minSlots: safeMinSlots,
    sizingPlan: sizingPlan ?? buildWireCardFlowPlan({ itemCount: safeItemCount, kind, minSlots: safeMinSlots }),
    slotCount
  });
  const state = capacityState(plan);
  return {
    cardHeight: plan.cardHeight,
    cardWidth: plan.cardWidth,
    density: plan.density,
    fit: plan.fit,
    itemCount: plan.itemCount,
    key,
    kind,
    label,
    overflow: plan.overflow,
    overflowCount: plan.overflowCount,
    slotCount: plan.slotCount,
    state,
    stateLabel: capacityStateLabel(state),
    visibleSlotCount: plan.visibleSlotCount
  };
}

function buildSelectedLayoutPlan(
  table: WireTableViewModel,
  capacityRows: WireTableCapacityRow[],
  selectedObjectId?: string
): WireTableSelectedLayoutPlan {
  const objectId = selectedObjectId?.trim();
  if (!objectId) {
    return selectedLayoutPlan({
      kind: "none",
      source: "empty-selection",
      state: "empty",
      summary: "未选择桌面对象。",
      zoneLabel: "无焦点"
    });
  }

  const capacityKeys = new Set(capacityRows.map((row) => row.key));
  for (const player of table.players) {
    const playerLabel = player.side === "self" ? "我方" : "对手";
    const capacityRowKey = `${player.side}:base`;
    if (player.baseObjectIds.includes(objectId)) {
      return selectedLayoutPlan({
        capacityRowKey,
        kind: "base",
        objectId,
        source: "player-base-flow",
        state: "located",
        zoneKey: `${player.side}:base`,
        zoneLabel: `${playerLabel}基地`
      });
    }

    if (player.runeIds.includes(objectId)) {
      return selectedLayoutPlan({
        kind: "rune-track",
        objectId,
        source: "player-rune-track",
        state: "located",
        zoneKey: `${player.side}:rune-track`,
        zoneLabel: `${playerLabel}符文轨`
      });
    }

    const handRowKey = `${player.side}:hand`;
    if (player.handIds.includes(objectId) || player.hiddenHandIds.includes(objectId)) {
      return selectedLayoutPlan({
        capacityRowKey: capacityKeys.has(handRowKey) ? handRowKey : undefined,
        kind: "hand",
        objectId,
        source: "player-hand-flow",
        state: "located",
        zoneKey: handRowKey,
        zoneLabel: `${playerLabel}手牌`
      });
    }

    if (player.zones.graveyard?.includes(objectId)) {
      return selectedLayoutPlan({
        kind: "fixed-pile",
        objectId,
        source: "player-graveyard-pile",
        state: "located",
        zoneKey: `${player.side}:graveyard`,
        zoneLabel: `${playerLabel}已打出牌堆`
      });
    }

    if (player.zones.banished?.includes(objectId)) {
      return selectedLayoutPlan({
        kind: "fixed-pile",
        objectId,
        source: "player-banished-pile",
        state: "located",
        zoneKey: `${player.side}:banished`,
        zoneLabel: `${playerLabel}放逐区`
      });
    }

    if (player.zones.legendZone?.includes(objectId)) {
      return selectedLayoutPlan({
        kind: "signature",
        objectId,
        source: "player-legend-slot",
        state: "located",
        zoneKey: `${player.side}:legend`,
        zoneLabel: `${playerLabel}传奇`
      });
    }

    if (player.zones.championZone?.includes(objectId)) {
      return selectedLayoutPlan({
        kind: "signature",
        objectId,
        source: "player-champion-slot",
        state: "located",
        zoneKey: `${player.side}:champion`,
        zoneLabel: `${playerLabel}英雄`
      });
    }
  }

  for (const lane of table.battlefield.lanes) {
    const laneLabel = lane.index === 0 ? "左战场" : "右战场";
    if (lane.battlefieldId === objectId) {
      return selectedLayoutPlan({
        kind: "site",
        objectId,
        source: "battlefield-site-slot",
        state: "located",
        zoneKey: `battlefield:${lane.index}:site`,
        zoneLabel: `${laneLabel}牌`
      });
    }

    const opposingRowKey = `battlefield:${lane.index}:opponent`;
    if (lane.opposingOccupants.includes(objectId)) {
      return selectedLayoutPlan({
        capacityRowKey: capacityKeys.has(opposingRowKey) ? opposingRowKey : undefined,
        kind: "battlefield-unit",
        objectId,
        source: "battlefield-opponent-flow",
        state: "located",
        zoneKey: opposingRowKey,
        zoneLabel: `${laneLabel}对方单位`
      });
    }

    const ownRowKey = `battlefield:${lane.index}:self`;
    if (lane.ownOccupants.includes(objectId)) {
      return selectedLayoutPlan({
        capacityRowKey: capacityKeys.has(ownRowKey) ? ownRowKey : undefined,
        kind: "battlefield-unit",
        objectId,
        source: "battlefield-self-flow",
        state: "located",
        zoneKey: ownRowKey,
        zoneLabel: `${laneLabel}我方单位`
      });
    }

    const standbyRowKey = `battlefield:${lane.index}:standby`;
    if (lane.standbySlots.some((slot) => slot.objectId === objectId || slot.slotId === objectId)) {
      return selectedLayoutPlan({
        capacityRowKey: capacityKeys.has(standbyRowKey) ? standbyRowKey : undefined,
        kind: "standby",
        objectId,
        source: "battlefield-standby-flow",
        state: "located",
        zoneKey: standbyRowKey,
        zoneLabel: `${laneLabel}待命槽`
      });
    }
  }

  return selectedLayoutPlan({
    kind: "none",
    objectId,
    source: "not-in-wire-table",
    state: "unknown",
    summary: `${objectId} 未进入当前线框桌面区域索引。`,
    zoneLabel: "未定位"
  });
}

function selectedLayoutPlan({
  capacityRowKey,
  kind,
  objectId,
  source,
  state,
  summary,
  zoneKey,
  zoneLabel
}: {
  capacityRowKey?: string;
  kind: WireTableSelectedLayoutKind;
  objectId?: string;
  source: string;
  state: WireTableSelectedLayoutState;
  summary?: string;
  zoneKey?: string;
  zoneLabel: string;
}): WireTableSelectedLayoutPlan {
  return {
    capacityRowKey,
    kind,
    objectId,
    source,
    state,
    stateLabel: selectedLayoutStateLabel(state),
    summary: summary ?? `${objectId} 位于${zoneLabel}${capacityRowKey ? `，受 ${capacityRowKey} 容量行约束。` : "固定槽位。"}。`,
    zoneKey,
    zoneLabel
  };
}

function buildConsistencyRows(table: WireTableViewModel): WireTableConsistencyRow[] {
  return [
    consistencyRow("base", "双方基地流", table.playerPlans.basePlan, "base"),
    consistencyRow("hand", "双方手牌流", table.playerPlans.handPlan, "hand"),
    consistencyRow("battlefieldUnit", "四格战场单位流", table.battlefield.unitPlan, "battlefield-unit"),
    consistencyRow("standby", "战场待命槽流", table.battlefield.standbyPlan, "standby")
  ];
}

function consistencyRow(
  key: string,
  label: string,
  plan: Partial<WireCardFlowPlan> | undefined,
  expectedKind: WireCardFlowKind
): WireTableConsistencyRow {
  const state = consistencyRowState(plan, expectedKind);
  return {
    cardHeight: numberValue(plan?.cardHeight),
    cardWidth: numberValue(plan?.cardWidth),
    density: stringValue(plan?.density),
    expectedKind,
    fit: stringValue(plan?.fit),
    itemCount: numberValue(plan?.itemCount),
    key,
    label,
    layout: stringValue(plan?.layout),
    overflow: stringValue(plan?.overflow),
    slotCount: numberValue(plan?.slotCount),
    state,
    stateLabel: consistencyStateLabel(state),
    visibleSlotCount: numberValue(plan?.visibleSlotCount)
  };
}

function consistencyRowState(
  plan: Partial<WireCardFlowPlan> | undefined,
  expectedKind: WireCardFlowKind
): WireTableConsistencyState {
  if (!plan) {
    return "missing";
  }

  if (plan.kind !== expectedKind) {
    return "drift";
  }

  if (!positiveNumber(plan.cardWidth)
      || !positiveNumber(plan.cardHeight)
      || !nonNegativeNumber(plan.itemCount)
      || !nonNegativeNumber(plan.slotCount)
      || !nonNegativeNumber(plan.visibleSlotCount)
      || plan.slotCount < plan.itemCount
      || plan.visibleSlotCount > plan.slotCount
      || !plan.density
      || !plan.fit
      || !plan.layout
      || !plan.overflow) {
    return "drift";
  }

  return "consistent";
}

function capacityState(plan: WireCardFlowPlan): WireTableCapacityState {
  if (plan.overflow === "scroll") {
    return "scroll";
  }

  if (plan.itemCount === 0) {
    return "empty";
  }

  return "stable";
}

function capacityStateLabel(state: WireTableCapacityState): string {
  switch (state) {
    case "empty":
      return "空槽";
    case "stable":
      return "稳定";
    case "scroll":
      return "滚动";
  }
}

function selectedLayoutStateLabel(state: WireTableSelectedLayoutState): string {
  switch (state) {
    case "empty":
      return "未选择";
    case "located":
      return "已定位";
    case "unknown":
      return "未定位";
  }
}

function resolveConsistencyState(states: WireTableConsistencyState[]): WireTableConsistencyState {
  if (states.length === 0 || states.includes("missing")) {
    return "missing";
  }

  if (states.every((state) => state === "consistent")) {
    return "consistent";
  }

  return "drift";
}

function resolveAuthorityState(states: WireTableAuthorityState[]): WireTableAuthorityState {
  if (states.length === 0 || states.includes("missing")) {
    return "missing";
  }

  if (states.every((state) => state === "server")) {
    return "server";
  }

  if (states.some((state) => state === "server" || state === "mixed")) {
    return "mixed";
  }

  return "fallback";
}

function aggregateRows(states: WireTableAuthorityState[]): WireTableAuthorityState {
  return resolveAuthorityState(states);
}

function consistencyMetricState(state: WireTableConsistencyState): WireTableAuthorityState {
  switch (state) {
    case "consistent":
      return "server";
    case "drift":
      return "mixed";
    case "missing":
      return "missing";
  }
}

function basePartitionState(source: WireBasePartitionSource): WireTableAuthorityState {
  if (source === "server" || source === "server-location") {
    return "server";
  }

  if (source === "mixed") {
    return "mixed";
  }

  return "fallback";
}

function battlefieldSplitState(source: WireBattlefieldOccupantSplitSource): WireTableAuthorityState {
  return source === "server-unitsBySide" ? "server" : "fallback";
}

function standbySlotState(source: WireBattlefieldStandbySlotSource): WireTableAuthorityState {
  return source === "server-standbySlots" ? "server" : "fallback";
}

function basePartitionSourceLabel(source: WireBasePartitionSource | "missing"): string {
  switch (source) {
    case "server":
      return "服务端 baseCards/baseRunes";
    case "server-location":
      return "服务端 location/tags";
    case "mixed":
      return "部分服务端分区";
    case "catalog-fallback":
      return "目录识别兜底";
    case "missing":
      return "缺少玩家快照";
  }
}

function battlefieldSplitSourceLabel(source: WireBattlefieldOccupantSplitSource | "missing"): string {
  switch (source) {
    case "server-unitsBySide":
      return "服务端 unitsBySide";
    case "controller-fallback":
      return "控制权兜底";
    case "missing":
      return "缺少战场快照";
  }
}

function standbySlotSourceLabel(source: WireBattlefieldStandbySlotSource | "missing"): string {
  switch (source) {
    case "server-standbySlots":
      return "服务端 standbySlots";
    case "standbyObjectIds-fallback":
      return "待命对象兜底";
    case "missing":
      return "缺少战场快照";
  }
}

function authorityStateLabel(state: WireTableAuthorityState): string {
  switch (state) {
    case "server":
      return "服务端权威";
    case "mixed":
      return "部分兜底";
    case "fallback":
      return "前端兜底";
    case "missing":
      return "快照缺失";
  }
}

function consistencyStateLabel(state: WireTableConsistencyState): string {
  switch (state) {
    case "consistent":
      return "同源计划";
    case "drift":
      return "计划漂移";
    case "missing":
      return "缺少计划";
  }
}

function authoritySummary(state: WireTableAuthorityState, issueCount: number): string {
  switch (state) {
    case "server":
      return "桌面布局由服务端快照提供，可以继续叠加交互与视觉。";
    case "mixed":
      return `仍有 ${issueCount} 项布局信息依赖前端兜底，需要后端快照补齐。`;
    case "fallback":
      return "桌面布局主要依赖前端推断，不适合作为最终对战桌面。";
    case "missing":
      return "缺少完整玩家或战场快照，当前桌面只能显示结构占位。";
  }
}

function numberValue(value: unknown): number {
  return typeof value === "number" && Number.isFinite(value) ? value : 0;
}

function stringValue(value: unknown): string {
  return typeof value === "string" ? value : "";
}

function positiveNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value) && value > 0;
}

function nonNegativeNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value) && value >= 0;
}
