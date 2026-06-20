import type {
  WireBasePartitionSource,
  WireBattlefieldOccupantSplitSource,
  WireBattlefieldStandbySlotSource,
  WireTableViewModel
} from "./wireTableViewModel";

export type WireTableAuthorityState = "fallback" | "missing" | "mixed" | "server";

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

export type WireTableAuthorityPlan = {
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
