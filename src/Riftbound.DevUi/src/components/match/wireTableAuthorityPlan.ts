import type {
  WireBasePartitionSource,
  WireBattlefieldOccupantSplitSource,
  WireBattlefieldStandbySlotSource,
  WireTableViewModel
} from "./wireTableViewModel";
import type { WireCardFlowKind, WireCardFlowPlan } from "./wireCardFlowPlan";

export type WireTableAuthorityState = "fallback" | "missing" | "mixed" | "server";
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

export type WireTableAuthorityPlan = {
  consistencyIssueCount: number;
  consistencyRows: WireTableConsistencyRow[];
  consistencyState: WireTableConsistencyState;
  issueCount: number;
  lanes: WireTableAuthorityLaneRow[];
  metrics: WireTableAuthorityMetric[];
  players: WireTableAuthorityPlayerRow[];
  state: WireTableAuthorityState;
  stateLabel: string;
  summary: string;
};

const REQUIRED_PLAYER_COUNT = 2;
const REQUIRED_LANE_COUNT = 2;

export function buildWireTableAuthorityPlan(table: WireTableViewModel): WireTableAuthorityPlan {
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
    state,
    stateLabel: authorityStateLabel(state),
    summary: authoritySummary(state, issueCount)
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
