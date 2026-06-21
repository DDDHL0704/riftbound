import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const flowPlanExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireCardFlowPlan.ts"));
const authorityExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireTableAuthorityPlan.ts"), flowPlanExports);
const { buildWireTableAuthorityPlan } = authorityExports;

const serverPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  playerSources: ["server", "server"],
  standbySources: ["server-standbySlots", "server-standbySlots"]
}));
assert.equal(serverPlan.state, "server");
assert.equal(serverPlan.issueCount, 0);
assert.equal(serverPlan.consistencyState, "consistent");
assert.equal(serverPlan.consistencyIssueCount, 0);
assert.deepEqual(serverPlan.metrics.map((metric) => metric.value), ["2/2", "2/2", "2/2", "4/4", "0"]);
assert.deepEqual(
  serverPlan.consistencyRows.map((row) => [row.key, row.expectedKind, row.state, row.cardWidth]),
  [
    ["base", "base", "consistent", 86],
    ["hand", "hand", "consistent", 74],
    ["battlefieldUnit", "battlefield-unit", "consistent", 68],
    ["standby", "standby", "consistent", 52]
  ]
);
assert.equal(serverPlan.capacityRows.length, 10);
const serverCapacityRows = new Map(serverPlan.capacityRows.map((row) => [row.key, row]));
assert.deepEqual(
  Array.from(serverCapacityRows.keys()),
  [
    "opponent:base",
    "opponent:hand",
    "self:base",
    "self:hand",
    "battlefield:0:opponent",
    "battlefield:0:self",
    "battlefield:0:standby",
    "battlefield:1:opponent",
    "battlefield:1:self",
    "battlefield:1:standby"
  ]
);
assert.deepEqual(
  [
    serverCapacityRows.get("battlefield:0:opponent")?.itemCount,
    serverCapacityRows.get("battlefield:0:self")?.itemCount,
    serverCapacityRows.get("battlefield:1:opponent")?.itemCount,
    serverCapacityRows.get("battlefield:1:self")?.itemCount
  ],
  [1, 2, 2, 1]
);
assert.equal(serverCapacityRows.get("battlefield:0:self")?.cardWidth, serverCapacityRows.get("battlefield:1:opponent")?.cardWidth);
assert.equal(serverCapacityRows.get("battlefield:0:self")?.slotCount, 3);
assert.equal(serverCapacityRows.get("opponent:hand")?.state, "empty");
assert.equal(serverPlan.selectedLayout.state, "empty");

const selectedBasePlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  playerSources: ["server", "server"],
  standbySources: ["server-standbySlots", "server-standbySlots"]
}), { selectedObjectId: "p1-base" });
assert.equal(selectedBasePlan.selectedLayout.state, "located");
assert.equal(selectedBasePlan.selectedLayout.kind, "base");
assert.equal(selectedBasePlan.selectedLayout.capacityRowKey, "opponent:base");
assert.equal(selectedBasePlan.selectedLayout.capacity?.state, "stable");
assert.equal(selectedBasePlan.selectedLayout.capacity?.label, "P1 基地流");
assert.equal(selectedBasePlan.selectedLayout.capacity?.slotCount, 1);

const selectedUnitPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  playerSources: ["server", "server"],
  standbySources: ["server-standbySlots", "server-standbySlots"]
}), { selectedObjectId: "p1-unit-1" });
assert.equal(selectedUnitPlan.selectedLayout.state, "located");
assert.equal(selectedUnitPlan.selectedLayout.kind, "battlefield-unit");
assert.equal(selectedUnitPlan.selectedLayout.capacityRowKey, "battlefield:0:self");
assert.equal(selectedUnitPlan.selectedLayout.capacity?.state, "stable");
assert.equal(selectedUnitPlan.selectedLayout.capacity?.visibleSlotCount, 3);
assert.match(selectedUnitPlan.selectedLayout.capacity?.summary ?? "", /左战场 我方单位/);

const selectedStandbyPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  playerSources: ["server", "server"],
  standbySources: ["server-standbySlots", "server-standbySlots"]
}), { selectedObjectId: "standby-0-1" });
assert.equal(selectedStandbyPlan.selectedLayout.state, "located");
assert.equal(selectedStandbyPlan.selectedLayout.kind, "standby");
assert.equal(selectedStandbyPlan.selectedLayout.capacityRowKey, "battlefield:0:standby");
assert.equal(selectedStandbyPlan.selectedLayout.capacity?.state, "stable");

const serverLocationUnitPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  objects: {
    "late-self-unit": object("late-self-unit", ["CARD_TYPE:UNIT"], {
      battlefieldObjectId: "battlefield-0",
      playerId: "P2",
      zone: "BATTLEFIELD",
      zoneKind: "battlefield",
      zoneLabel: "战场"
    })
  },
  playerSources: ["server", "server"],
  standbySources: ["server-standbySlots", "server-standbySlots"]
}), { selectedObjectId: "late-self-unit" });
assert.equal(serverLocationUnitPlan.selectedLayout.state, "located");
assert.equal(serverLocationUnitPlan.selectedLayout.kind, "battlefield-unit");
assert.equal(serverLocationUnitPlan.selectedLayout.source, "server-location-battlefield-unit");
assert.equal(serverLocationUnitPlan.selectedLayout.capacityRowKey, "battlefield:0:self");
assert.equal(serverLocationUnitPlan.selectedLayout.capacity?.state, "stable");

const serverLocationStandbyPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  objects: {
    "late-standby": object("late-standby", ["CARD_TYPE:UNIT", "待命"], {
      battlefieldObjectId: "battlefield-0",
      playerId: "P2",
      zone: "BATTLEFIELD",
      zoneKind: "battlefield",
      zoneLabel: "战场"
    })
  },
  playerSources: ["server", "server"],
  standbySources: ["server-standbySlots", "server-standbySlots"]
}), { selectedObjectId: "late-standby" });
assert.equal(serverLocationStandbyPlan.selectedLayout.state, "located");
assert.equal(serverLocationStandbyPlan.selectedLayout.kind, "standby");
assert.equal(serverLocationStandbyPlan.selectedLayout.source, "server-location-battlefield-standby");
assert.equal(serverLocationStandbyPlan.selectedLayout.capacityRowKey, "battlefield:0:standby");

const serverLocationRunePlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  objects: {
    "late-rune": object("late-rune", ["CARD_TYPE:RUNE"], {
      playerId: "P2",
      zone: "BASE",
      zoneKind: "rune",
      zoneLabel: "已抽出符文"
    })
  },
  playerSources: ["server", "server"],
  standbySources: ["server-standbySlots", "server-standbySlots"]
}), { selectedObjectId: "late-rune" });
assert.equal(serverLocationRunePlan.selectedLayout.state, "located");
assert.equal(serverLocationRunePlan.selectedLayout.kind, "rune-track");
assert.equal(serverLocationRunePlan.selectedLayout.source, "server-location-rune-track");
assert.equal(serverLocationRunePlan.selectedLayout.zoneKey, "self:rune-track");

const selectedUnknownPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  playerSources: ["server", "server"],
  standbySources: ["server-standbySlots", "server-standbySlots"]
}), { selectedObjectId: "unknown-object" });
assert.equal(selectedUnknownPlan.selectedLayout.state, "unknown");
assert.equal(selectedUnknownPlan.selectedLayout.kind, "none");

const serverLocationPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  playerSources: ["server-location", "server-location"],
  standbySources: ["server-standbySlots", "server-standbySlots"]
}));
assert.equal(serverLocationPlan.state, "server");
assert.equal(serverLocationPlan.issueCount, 0);
assert.equal(serverLocationPlan.players[0].sourceLabel, "服务端 location/tags");

const mixedPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "controller-fallback"],
  playerSources: ["server", "catalog-fallback"],
  standbySources: ["server-standbySlots", "standbyObjectIds-fallback"]
}));
assert.equal(mixedPlan.state, "mixed");
assert.equal(mixedPlan.issueCount, 3);
assert.equal(mixedPlan.players[1].sourceLabel, "目录识别兜底");
assert.equal(mixedPlan.lanes[1].sourceLabel, "控制权兜底");
assert.equal(mixedPlan.lanes[1].standbySourceLabel, "待命对象兜底");
assert.match(mixedPlan.summary, /后端快照补齐/);

const fallbackPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["controller-fallback", "controller-fallback"],
  playerSources: ["catalog-fallback", "catalog-fallback"],
  standbySources: ["standbyObjectIds-fallback", "standbyObjectIds-fallback"]
}));
assert.equal(fallbackPlan.state, "fallback");
assert.equal(fallbackPlan.issueCount, 6);

const missingPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide"],
  playerSources: ["server"],
  standbySources: ["server-standbySlots"]
}));
assert.equal(missingPlan.state, "missing");
assert.equal(missingPlan.issueCount, 3);
assert.equal(missingPlan.metrics[0].state, "missing");
assert.equal(missingPlan.metrics[1].state, "missing");
assert.equal(missingPlan.metrics[2].state, "missing");

const driftPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  playerSources: ["server", "server"],
  standbySources: ["server-standbySlots", "server-standbySlots"],
  plans: {
    basePlan: plan("base", 1, 1, 1, 0),
    handPlan: plan("hand", 2),
    standbyPlan: plan("standby", 2),
    unitPlan: plan("hand", 3)
  }
}));
assert.equal(driftPlan.state, "server", "layout drift must not masquerade as missing backend authority fields");
assert.equal(driftPlan.issueCount, 0);
assert.equal(driftPlan.consistencyState, "drift");
assert.equal(driftPlan.consistencyIssueCount, 2);
assert.equal(driftPlan.metrics.find((metric) => metric.key === "layoutPlans").state, "mixed");
assert.deepEqual(
  driftPlan.consistencyRows.filter((row) => row.state !== "consistent").map((row) => [row.key, row.state]),
  [["base", "drift"], ["battlefieldUnit", "drift"]]
);

const scrollPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  playerSources: ["server", "server"],
  standbySources: ["server-standbySlots", "server-standbySlots"],
  laneObjects: {
    0: {
      own: range("p1-left", 14),
      opposing: range("p2-left", 14),
      standby: range("left-standby", 9)
    },
    1: {
      own: range("p1-right", 2),
      opposing: range("p2-right", 2),
      standby: range("right-standby", 2)
    }
  },
  plans: {
    standbyPlan: plan("standby", 9, 9, 8, 40),
    unitPlan: plan("battlefield-unit", 14, 14, 12, 42)
  }
}));
const scrollCapacityRows = new Map(scrollPlan.capacityRows.map((row) => [row.key, row]));
assert.equal(scrollCapacityRows.get("battlefield:0:self")?.state, "scroll");
assert.equal(scrollCapacityRows.get("battlefield:0:self")?.overflowCount, 2);
assert.equal(scrollCapacityRows.get("battlefield:0:standby")?.state, "scroll");
assert.equal(scrollCapacityRows.get("battlefield:0:standby")?.overflowCount, 1);
assert.equal(scrollCapacityRows.get("battlefield:1:self")?.state, "stable");

const selectedScrollPlan = buildWireTableAuthorityPlan(table({
  laneSources: ["server-unitsBySide", "server-unitsBySide"],
  playerSources: ["server", "server"],
  standbySources: ["server-standbySlots", "server-standbySlots"],
  laneObjects: {
    0: {
      own: range("p1-left", 14),
      opposing: range("p2-left", 14),
      standby: range("left-standby", 9)
    },
    1: {
      own: range("p1-right", 2),
      opposing: range("p2-right", 2),
      standby: range("right-standby", 2)
    }
  },
  plans: {
    standbyPlan: plan("standby", 9, 9, 8, 40),
    unitPlan: plan("battlefield-unit", 14, 14, 12, 42)
  }
}), { selectedObjectId: "p1-left-14" });
assert.equal(selectedScrollPlan.selectedLayout.capacityRowKey, "battlefield:0:self");
assert.equal(selectedScrollPlan.selectedLayout.capacity?.state, "scroll");
assert.equal(selectedScrollPlan.selectedLayout.capacity?.overflowCount, 2);
assert.equal(selectedScrollPlan.selectedLayout.capacity?.visibleSlotCount, 12);
assert.match(selectedScrollPlan.selectedLayout.capacity?.summary ?? "", /溢出 2/);

console.log("Wire table authority plan check passed.");

function table({ laneObjects = {}, laneSources, objects = {}, playerSources, standbySources, plans = {} }) {
  return {
    battlefield: {
      lanes: laneSources.map((source, index) => ({
        battlefieldId: `battlefield-${index}`,
        cardNo: `SITE-${index}`,
        controllerId: index === 0 ? "P1" : "P2",
        hiddenStandbyCount: index,
        index,
        occupantSplitSource: source,
        opposingOccupants: laneObjects[index]?.opposing ?? (index === 0 ? ["p2-unit-1"] : ["p2-unit-2", "p2-unit-3"]),
        ownOccupants: laneObjects[index]?.own ?? (index === 0 ? ["p1-unit-1", "p1-unit-2"] : ["p1-unit-3"]),
        scoredThisTurnPlayerIds: [],
        standbySlotCount: 2,
        standbySlotSource: standbySources[index],
        standbySlots: (laneObjects[index]?.standby ?? [`standby-${index}-1`, `standby-${index}-2`]).map((slotId) => ({ slotId })),
        zonePlayerId: index === 0 ? "P1" : "P2"
      })),
      objects,
      standbyPlan: plans.standbyPlan ?? plan("standby", 2),
      unitPlan: plans.unitPlan ?? plan("battlefield-unit", 3)
    },
    players: playerSources.map((source, index) => ({
      baseObjectIds: [`p${index + 1}-base`],
      basePartitionSource: source,
      handIds: [],
      hiddenHandIds: [],
      id: `P${index + 1}`,
      label: `P${index + 1}`,
      objects,
      player: {},
      runeIds: [`p${index + 1}-rune-a`, `p${index + 1}-rune-b`],
      side: index === 0 ? "opponent" : "self",
      zones: {}
    })),
    playerPlans: {
      basePlan: plans.basePlan ?? plan("base", 1, 1, 1, 86),
      handPlan: plans.handPlan ?? plan("hand", 2)
    }
  };
}

function object(objectId, tags, location) {
  return {
    controllerId: location.playerId,
    location,
    objectId,
    ownerId: location.playerId,
    tags
  };
}

function plan(kind, itemCount, slotCount = itemCount, visibleSlotCount = Math.min(slotCount, 12), cardWidth) {
  const widths = {
    base: 86,
    "battlefield-unit": 68,
    hand: 74,
    standby: 52
  };
  const scrollAfterByKind = {
    base: 10,
    "battlefield-unit": 12,
    hand: 12,
    standby: 8
  };
  const width = cardWidth ?? widths[kind] ?? 0;
  const scrollAfter = scrollAfterByKind[kind] ?? 12;
  const visibleSlots = Math.min(slotCount, visibleSlotCount, scrollAfter);
  const overflowCount = Math.max(0, slotCount - visibleSlots);
  return {
    capacity: "unbounded",
    cardHeight: Math.round(width / (744 / 1039)),
    cardWidth: width,
    density: itemCount <= 3 ? "sparse" : "normal",
    fit: overflowCount > 0 ? "overflow-rail" : "elastic-rail",
    gap: 4,
    itemCount,
    kind,
    layout: "rail",
    minSlots: 0,
    overflow: overflowCount > 0 ? "scroll" : "none",
    overflowCount,
    scrollAfter,
    slotCount,
    visibleSlotCount: visibleSlots
  };
}

function range(prefix, count) {
  return Array.from({ length: count }, (_, index) => `${prefix}-${index + 1}`);
}

function loadTsModule(sourcePath, globals = {}) {
  const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const moduleShim = { exports: {} };
  const globalNames = Object.keys(globals);
  new Function("exports", "module", ...globalNames, output)(moduleShim.exports, moduleShim, ...Object.values(globals));
  return moduleShim.exports;
}
