import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const authorityExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireTableAuthorityPlan.ts"));
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

console.log("Wire table authority plan check passed.");

function table({ laneSources, playerSources, standbySources, plans = {} }) {
  return {
    battlefield: {
      lanes: laneSources.map((source, index) => ({
        battlefieldId: `battlefield-${index}`,
        cardNo: `SITE-${index}`,
        controllerId: index === 0 ? "P1" : "P2",
        hiddenStandbyCount: index,
        index,
        occupantSplitSource: source,
        opposingOccupants: index === 0 ? ["p2-unit-1"] : ["p2-unit-2", "p2-unit-3"],
        ownOccupants: index === 0 ? ["p1-unit-1", "p1-unit-2"] : ["p1-unit-3"],
        scoredThisTurnPlayerIds: [],
        standbySlotCount: 2,
        standbySlotSource: standbySources[index],
        standbySlots: [{ slotId: `standby-${index}-1` }, { slotId: `standby-${index}-2` }],
        zonePlayerId: index === 0 ? "P1" : "P2"
      })),
      objects: {},
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
      objects: {},
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

function plan(kind, itemCount, slotCount = itemCount, visibleSlotCount = Math.min(slotCount, 12), cardWidth) {
  const widths = {
    base: 86,
    "battlefield-unit": 68,
    hand: 74,
    standby: 52
  };
  const width = cardWidth ?? widths[kind] ?? 0;
  return {
    capacity: "unbounded",
    cardHeight: Math.round(width / (744 / 1039)),
    cardWidth: width,
    density: itemCount <= 3 ? "sparse" : "normal",
    fit: "elastic-rail",
    gap: 4,
    itemCount,
    kind,
    layout: "rail",
    minSlots: 0,
    overflow: "none",
    overflowCount: 0,
    scrollAfter: 12,
    slotCount,
    visibleSlotCount
  };
}

function loadTsModule(sourcePath) {
  const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const moduleShim = { exports: {} };
  new Function("exports", "module", output)(moduleShim.exports, moduleShim);
  return moduleShim.exports;
}
