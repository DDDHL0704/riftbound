import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const contractExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireTableContract.ts"));
const planExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireCardFlowPlan.ts"), contractExports);
const viewModelExports = loadTsModule(
  resolve(scriptDir, "../src/components/match/wireTableViewModel.ts"),
  { buildWireCardFlowPlan: planExports.buildWireCardFlowPlan }
);

const {
  buildWireBattlefieldModel,
  buildWirePlayerEntries,
  buildWireTableViewModel,
  isRuneCard,
  ownerOrController
} = viewModelExports;

const specs = {
  "BASE-001": { cardCategoryName: "单位" },
  "RUNE-001": { cardCategoryName: "符文" },
  "RUNE-002": { cardCategoryName: "法术" },
  "UNIT-001": { cardCategoryName: "单位" },
  "UNIT-002": { cardCategoryName: "单位" },
  "UNIT-003": { cardCategoryName: "单位" },
  "UNIT-004": { cardCategoryName: "单位" }
};

const snapshot = {
  activePlayerId: "P1",
  lanes: {
    battlefields: [
      {
        battlefieldObjectId: "battlefield-left",
        cardNo: "SITE-LEFT",
        controllerId: "P1",
        hiddenStandbyCount: 1,
        occupantObjectIds: ["p1-left-1", "p1-left-2", "p2-left-1"],
        standbySlotCount: 2,
        standbySlots: [
          {
            battlefieldObjectId: "battlefield-left",
            controllerId: "P1",
            isFaceDown: true,
            objectId: "p1-left-standby",
            sidePlayerId: "P1",
            slotId: "battlefield-left:standby:1",
            state: "VISIBLE",
            visible: true
          },
          {
            battlefieldObjectId: "battlefield-left",
            controllerId: "P2",
            isFaceDown: true,
            objectId: "p2-hidden-standby",
            sidePlayerId: "P2",
            slotId: "battlefield-left:standby:2",
            state: "HIDDEN",
            visible: false
          }
        ],
        unitsBySide: {
          P1: ["p1-left-1", "p1-left-2"],
          P2: ["p2-left-1"]
        },
        zonePlayerId: "P1"
      },
      {
        battlefieldObjectId: "battlefield-right",
        cardNo: "SITE-RIGHT",
        controllerId: "P2",
        hiddenStandbyCount: 1,
        occupantObjectIds: ["p2-right-1", "p2-right-2", "p1-right-1"],
        scoredThisTurnPlayerIds: ["P2"],
        standbySlotCount: 2,
        standbySlots: [
          {
            battlefieldObjectId: "battlefield-right",
            controllerId: "P2",
            isFaceDown: false,
            objectId: "p2-right-standby",
            sidePlayerId: "P2",
            slotId: "battlefield-right:standby:1",
            state: "VISIBLE",
            visible: true
          },
          {
            battlefieldObjectId: "battlefield-right",
            controllerId: "P1",
            isFaceDown: true,
            sidePlayerId: "P1",
            slotId: "battlefield-right:standby:2",
            state: "HIDDEN",
            visible: false
          }
        ],
        unitsBySide: {
          P1: ["p1-right-1"],
          P2: ["p2-right-1", "p2-right-2"]
        },
        zonePlayerId: "P2"
      }
    ]
  },
  players: {
    P1: {
      handSize: 2,
      name: "Bottom",
      objects: {
        "p1-base-1": object("p1-base-1", "BASE-001", "P1"),
        "p1-rune-1": object("p1-rune-1", "RUNE-001", "P1"),
        "p1-rune-tagged": object("p1-rune-tagged", "RUNE-002", "P1"),
        "p1-hand-1": object("p1-hand-1", "UNIT-001", "P1"),
        "p1-left-1": object("p1-left-1", "UNIT-001", "P1"),
        "p1-left-2": object("p1-left-2", "UNIT-002", "P1"),
        "p1-left-standby": object("p1-left-standby", "UNIT-004", "P1"),
        "p1-right-1": object("p1-right-1", "UNIT-003", "P1")
      },
      zones: {
        base: ["p1-base-1", "p1-rune-1", "p1-rune-tagged"],
        baseCards: ["p1-base-1"],
        baseRunes: ["p1-rune-1", "p1-rune-tagged"],
        battlefieldHiddenStandbyCount: 0,
        hand: ["p1-hand-1"],
        handHidden: 9,
        mainDeckCount: 31,
        runeDeckCount: 9
      }
    },
    P2: {
      handSize: 4,
      name: "Top",
      objects: {
        "p2-base-1": object("p2-base-1", "BASE-001", "P2"),
        "p2-rune-1": object("p2-rune-1", "RUNE-001", "P2"),
        "p2-hidden-standby": object("p2-hidden-standby", "UNIT-002", "P2"),
        "p2-left-1": { ...object("p2-left-1", "UNIT-001", "P2"), controllerId: "P1" },
        "p2-right-1": object("p2-right-1", "UNIT-001", "P2"),
        "p2-right-2": object("p2-right-2", "UNIT-004", "P2"),
        "p2-right-standby": object("p2-right-standby", "UNIT-003", "P2")
      },
      zones: {
        base: ["p2-rune-1", "p2-base-1"],
        baseCards: ["p2-base-1"],
        baseRunes: ["p2-rune-1"],
        battlefieldHiddenStandbyCount: 2,
        handHidden: 4,
        mainDeckCount: 28,
        runeDeckCount: 10
      }
    }
  },
  stack: [],
  tick: 1,
  timing: {},
  turnNumber: 1,
  turnState: "MAIN"
};

const table = buildWireTableViewModel({ perspectivePlayerId: "P1", snapshot, specs });
assert.equal(table.players.length, 2);
assert.equal(table.players[0].side, "opponent", "opponent must render first in the centered tabletop model");
assert.equal(table.players[1].side, "self", "self must render last in the centered tabletop model");
assert.equal(table.self.id, "P1");
assert.equal(table.opponent.id, "P2");

assert.deepEqual(table.self.baseObjectIds, ["p1-base-1"]);
assert.equal(table.self.basePartitionSource, "server");
assert.deepEqual(table.self.runeIds, ["p1-rune-1", "p1-rune-tagged"]);
assert.deepEqual(table.self.handIds, ["p1-hand-1"]);
assert.deepEqual(table.self.hiddenHandIds, []);
assert.equal(table.self.hiddenBattlefieldStandbyCount, 0);
assert.deepEqual(table.opponent.baseObjectIds, ["p2-base-1"]);
assert.equal(table.opponent.basePartitionSource, "server");
assert.deepEqual(table.opponent.runeIds, ["p2-rune-1"]);
assert.deepEqual(table.opponent.hiddenHandIds, ["hidden-P2-0", "hidden-P2-1", "hidden-P2-2", "hidden-P2-3"]);
assert.equal(table.opponent.hiddenBattlefieldStandbyCount, 2);
assert.equal(table.playerPlans.basePlan.kind, "base");
assert.equal(table.playerPlans.basePlan.itemCount, 1);
assert.equal(table.playerPlans.basePlan.minSlots, 4);
assert.equal(table.playerPlans.handPlan.kind, "hand");
assert.equal(table.playerPlans.handPlan.itemCount, 4);
assert.equal(table.playerPlans.handPlan.cardWidth, 86);

const battlefield = buildWireBattlefieldModel(snapshot, "P1");
assert.equal(battlefield.lanes.length, 2);
assert.equal(battlefield.lanes[0].occupantSplitSource, "server-unitsBySide");
assert.equal(battlefield.lanes[0].standbySlotSource, "server-standbySlots");
assert.equal(battlefield.lanes[0].hiddenStandbyCount, 1);
assert.equal(battlefield.lanes[0].standbySlotCount, 2);
assert.deepEqual(
  battlefield.lanes[0].standbySlots.map((slot) => [slot.slotId, slot.objectId ?? "hidden", slot.side, slot.visible]),
  [
    ["battlefield-left:standby:1", "p1-left-standby", "self", true],
    ["battlefield-left:standby:2", "hidden", "opponent", false]
  ]
);
assert.deepEqual(
  battlefield.lanes[0].standbySlotsBySide.self.map((slot) => slot.slotId),
  ["battlefield-left:standby:1"],
  "left battlefield self standby rail must be model-partitioned from sidePlayerId"
);
assert.deepEqual(
  battlefield.lanes[0].standbySlotsBySide.opponent.map((slot) => slot.slotId),
  ["battlefield-left:standby:2"],
  "left battlefield opponent standby rail must be model-partitioned from sidePlayerId"
);
assert.equal(battlefield.lanes[0].standbySlots[0].isFaceDown, true, "visible own standby may remain face-down but must keep its real object id");
assert.equal(battlefield.lanes[0].standbySlots[0].objectId, "p1-left-standby");
assert.equal(battlefield.lanes[0].standbySlots[1].objectId, undefined, "hidden opponent standby must not leak its real object id");
assert.equal(battlefield.lanes[0].standbySlots[1].slotId, "battlefield-left:standby:2", "hidden opponent standby keeps only a public slot id");
assert.deepEqual(battlefield.lanes[0].ownOccupants, ["p1-left-1", "p1-left-2"]);
assert.deepEqual(battlefield.lanes[0].opposingOccupants, ["p2-left-1"]);
assert.equal(
  ownerOrController(snapshot.players.P2.objects["p2-left-1"]),
  "P1",
  "fixture must prove server unitsBySide wins over local controller fallback"
);
assert.equal(battlefield.lanes[1].occupantSplitSource, "server-unitsBySide");
assert.equal(battlefield.lanes[1].standbySlotSource, "server-standbySlots");
assert.equal(battlefield.lanes[1].hiddenStandbyCount, 1);
assert.equal(battlefield.lanes[1].standbySlotCount, 2);
assert.deepEqual(battlefield.lanes[1].scoredThisTurnPlayerIds, ["P2"]);
assert.deepEqual(
  battlefield.lanes[1].standbySlots.map((slot) => [slot.slotId, slot.objectId ?? "hidden", slot.side, slot.visible]),
  [
    ["battlefield-right:standby:1", "p2-right-standby", "opponent", true],
    ["battlefield-right:standby:2", "hidden", "self", false]
  ]
);
assert.deepEqual(
  battlefield.lanes[1].standbySlotsBySide.self.map((slot) => slot.slotId),
  ["battlefield-right:standby:2"],
  "right battlefield self standby rail must keep hidden self-side public slot"
);
assert.deepEqual(
  battlefield.lanes[1].standbySlotsBySide.opponent.map((slot) => slot.slotId),
  ["battlefield-right:standby:1"],
  "right battlefield opponent standby rail must keep visible opponent-side slot"
);
assert.deepEqual(battlefield.lanes[1].ownOccupants, ["p1-right-1"]);
assert.deepEqual(battlefield.lanes[1].opposingOccupants, ["p2-right-1", "p2-right-2"]);
assert.deepEqual(table.battlefield.unitPlan, battlefield.unitPlan, "table and battlefield builders must share one unit plan");
assert.deepEqual(table.battlefield.standbyPlan, battlefield.standbyPlan, "table and battlefield builders must share one standby plan");
assert.equal(table.battlefield.unitPlan.itemCount, 2, "same max occupancy should drive every battlefield quadrant size");
assert.equal(table.battlefield.unitPlan.slotCount, 3, "empty space should remain visible for low-count unit zones");
assert.equal(table.battlefield.standbyPlan.itemCount, 1, "same per-side max standby count should drive every battlefield standby rail");
assert.equal(table.battlefield.standbyPlan.slotCount, 1, "single-standby half-zones should not reserve the opposite half-zone slot");
assert.equal(table.battlefield.standbyPlan.kind, "standby");
assert.equal(table.battlefield.unitPlan.kind, "battlefield-unit");

const hiddenStandbySlots = battlefield.lanes.flatMap((lane) => lane.standbySlots.filter((slot) => !slot.visible));
assert.equal(hiddenStandbySlots.length, 2, "fixture should prove hidden standby on both battlefield lanes");
for (const slot of hiddenStandbySlots) {
  assert.equal(slot.objectId, undefined, "hidden standby must not expose objectId");
  assert.equal("cardNo" in slot, false, "hidden standby must not expose cardNo");
  assert.equal(slot.isFaceDown, true, "hidden standby must stay face down");
}

const unitRenderPlansByCount = groupRenderPlans([
  ...battlefield.lanes.flatMap((lane) => [
    { count: lane.ownOccupants.length, minSlots: 3, plan: battlefield.unitPlan, renderEmptySlots: true },
    { count: lane.opposingOccupants.length, minSlots: 3, plan: battlefield.unitPlan, renderEmptySlots: true }
  ])
]);
assert.equal(unitRenderPlansByCount.get(1)?.size, 1, "same one-unit battlefield zones must reuse the same render plan");
assert.equal(unitRenderPlansByCount.get(2)?.size, 1, "same two-unit battlefield zones must reuse the same render plan");

const standbyRenderPlansByCount = groupRenderPlans(
  battlefield.lanes.flatMap((lane) => [
    {
      count: lane.standbySlotsBySide.self.length,
      minSlots: battlefield.standbyPlan.minSlots,
      plan: battlefield.standbyPlan,
      renderEmptySlots: false
    },
    {
      count: lane.standbySlotsBySide.opponent.length,
      minSlots: battlefield.standbyPlan.minSlots,
      plan: battlefield.standbyPlan,
      renderEmptySlots: false
    }
  ])
);
assert.equal(standbyRenderPlansByCount.get(1)?.size, 1, "same one-card standby half-rails must reuse the same render plan");

const p2Perspective = buildWireBattlefieldModel(snapshot, "P2");
assert.deepEqual(p2Perspective.lanes[0].ownOccupants, ["p2-left-1"]);
assert.deepEqual(p2Perspective.lanes[0].opposingOccupants, ["p1-left-1", "p1-left-2"]);
assert.deepEqual(p2Perspective.unitPlan, battlefield.unitPlan, "perspective flips ownership but not card sizing");

const tableProjectionSnapshot = {
  ...snapshot,
  table: {
    source: "server-snapshot",
    viewerPlayerId: "P1",
    runeDeckSize: 12,
    players: [
      {
        isViewer: false,
        perspective: "opponent",
        playerId: "P2",
        seat: "P2",
        zones: {
          ...snapshot.players.P2.zones,
          base: ["p2-base-1", "p2-rune-1"],
          baseCards: ["p2-base-1"],
          baseRunes: ["p2-rune-1"],
          battlefieldHiddenStandbyCount: 5,
          handHidden: 2
        }
      },
      {
        isViewer: true,
        perspective: "self",
        playerId: "P1",
        seat: "P1",
        zones: {
          ...snapshot.players.P1.zones,
          battlefieldHiddenStandbyCount: 0,
          hand: ["p1-hand-1", "p1-table-only-hand"],
          handHidden: 0
        }
      }
    ],
    battlefields: [
      {
        ...snapshot.lanes.battlefields[1],
        index: 0
      },
      {
        ...snapshot.lanes.battlefields[0],
        index: 1
      }
    ]
  }
};
const tableProjection = buildWireTableViewModel({ perspectivePlayerId: "P1", snapshot: tableProjectionSnapshot, specs });
assert.deepEqual(tableProjection.self.handIds, ["p1-hand-1", "p1-table-only-hand"], "table projection player zones must override legacy player zones");
assert.deepEqual(tableProjection.opponent.hiddenHandIds, ["hidden-P2-0", "hidden-P2-1"], "table projection hidden hand count must drive opponent hand rail");
assert.equal(tableProjection.opponent.hiddenBattlefieldStandbyCount, 5, "table projection hidden standby count must drive opponent battlefield boundary summary");
assert.equal(tableProjection.battlefield.lanes[0].battlefieldId, "battlefield-right", "table projection battlefield index must override legacy lane order");
assert.equal(tableProjection.battlefield.lanes[1].battlefieldId, "battlefield-left", "table projection battlefield index must preserve explicit server order");

const tableAuthorityNoLegacyZoneLeakSnapshot = {
  ...tableProjectionSnapshot,
  players: {
    ...tableProjectionSnapshot.players,
    P2: {
      ...tableProjectionSnapshot.players.P2,
      zones: {
        ...tableProjectionSnapshot.players.P2.zones,
        base: ["legacy-wrong-base"],
        baseCards: ["legacy-wrong-base"],
        baseRunes: ["legacy-wrong-rune"],
        hand: ["legacy-secret-hand"],
        handHidden: 99,
        mainDeck: ["legacy-secret-main"],
        runeDeck: ["legacy-secret-rune"]
      }
    }
  },
  table: {
    ...tableProjectionSnapshot.table,
    players: tableProjectionSnapshot.table.players.map((player) => player.playerId === "P2"
      ? {
        ...player,
        zones: {
          base: ["p2-rune-1", "p2-base-1"],
          baseCards: ["p2-base-1"],
          baseRunes: ["p2-rune-1"],
          handHidden: 2,
          mainDeckCount: 28,
          runeDeckCount: 10
        }
      }
      : player)
  }
};
const tableAuthorityNoLegacyZoneLeak = buildWireTableViewModel({
  perspectivePlayerId: "P1",
  snapshot: tableAuthorityNoLegacyZoneLeakSnapshot,
  specs
});
assert.deepEqual(
  tableAuthorityNoLegacyZoneLeak.opponent.handIds,
  [],
  "server table projection must not inherit legacy opponent hand zone"
);
assert.deepEqual(
  tableAuthorityNoLegacyZoneLeak.opponent.hiddenHandIds,
  ["hidden-P2-0", "hidden-P2-1"],
  "server table projection handHidden must beat legacy handHidden"
);
assert.deepEqual(
  tableAuthorityNoLegacyZoneLeak.opponent.baseObjectIds,
  ["p2-base-1"],
  "server table projection baseCards must beat legacy base zone"
);
assert.deepEqual(
  tableAuthorityNoLegacyZoneLeak.opponent.runeIds,
  ["p2-rune-1"],
  "server table projection baseRunes must beat legacy rune zone"
);
assert.equal(
  JSON.stringify(tableAuthorityNoLegacyZoneLeak).includes("legacy-secret-hand"),
  false,
  "wire table model must not retain leaked legacy hand ids when table projection is present"
);
assert.equal(
  JSON.stringify(tableAuthorityNoLegacyZoneLeak).includes("legacy-secret-main"),
  false,
  "wire table model must not retain leaked legacy deck ids when table projection is present"
);

const legacySnapshot = {
  ...snapshot,
  lanes: {
    battlefields: [
      {
        ...snapshot.lanes.battlefields[0],
        unitsBySide: undefined
      }
    ]
  },
  players: {
    P1: {
      ...snapshot.players.P1,
      zones: {
        ...snapshot.players.P1.zones,
        baseCards: undefined,
        baseRunes: undefined
      }
    },
    P2: snapshot.players.P2
  }
};
const legacyEntries = buildWirePlayerEntries(legacySnapshot, "P1", specs);
assert.equal(legacyEntries[1].basePartitionSource, "catalog-fallback");
assert.deepEqual(legacyEntries[1].baseObjectIds, ["p1-base-1", "p1-rune-tagged"]);
assert.deepEqual(legacyEntries[1].runeIds, ["p1-rune-1"]);
const legacyBattlefield = buildWireBattlefieldModel(legacySnapshot, "P1");
assert.equal(legacyBattlefield.lanes[0].occupantSplitSource, "controller-fallback");
assert.equal(legacyBattlefield.lanes[0].standbySlotSource, "server-standbySlots");
assert.deepEqual(legacyBattlefield.lanes[0].ownOccupants, ["p1-left-1", "p1-left-2", "p2-left-1"]);
assert.deepEqual(legacyBattlefield.lanes[0].opposingOccupants, []);

const fallbackStandbySnapshot = {
  ...snapshot,
  lanes: {
    battlefields: [
      {
        ...snapshot.lanes.battlefields[0],
        standbyObjectIds: ["p1-left-standby"],
        standbySlots: undefined
      }
    ]
  }
};
const fallbackStandby = buildWireBattlefieldModel(fallbackStandbySnapshot, "P1");
assert.equal(fallbackStandby.lanes[0].standbySlotSource, "standbyObjectIds-fallback");
assert.deepEqual(fallbackStandby.lanes[0].standbySlots.map((slot) => slot.objectId), ["p1-left-standby"]);
assert.deepEqual(fallbackStandby.lanes[0].standbySlotsBySide.self.map((slot) => slot.objectId), ["p1-left-standby"]);

const locationPartitionSnapshot = {
  ...legacySnapshot,
  players: {
    ...legacySnapshot.players,
    P1: {
      ...legacySnapshot.players.P1,
      objects: {
        ...legacySnapshot.players.P1.objects,
        "p1-base-1": {
          ...legacySnapshot.players.P1.objects["p1-base-1"],
          location: { playerId: "P1", zone: "BASE" },
          tags: ["CARD_TYPE:UNIT"]
        },
        "p1-rune-1": {
          ...legacySnapshot.players.P1.objects["p1-rune-1"],
          location: { playerId: "P1", zone: "BASE" },
          tags: ["CARD_TYPE:RUNE"]
        },
        "p1-rune-tagged": {
          ...legacySnapshot.players.P1.objects["p1-rune-tagged"],
          location: { playerId: "P1", zone: "BASE" },
          tags: ["CARD_TYPE:RUNE"]
        }
      }
    }
  }
};
const locationEntries = buildWirePlayerEntries(locationPartitionSnapshot, "P1", specs);
assert.equal(locationEntries[1].basePartitionSource, "server-location");
assert.deepEqual(locationEntries[1].baseObjectIds, ["p1-base-1"]);
assert.deepEqual(
  locationEntries[1].runeIds,
  ["p1-rune-1", "p1-rune-tagged"],
  "server object location and tags must beat catalog category fallback"
);

assert.equal(isRuneCard(snapshot.players.P1.objects["p1-rune-1"], specs["RUNE-001"]), true);
assert.equal(isRuneCard(snapshot.players.P1.objects["p1-rune-tagged"], specs["RUNE-002"]), false);
assert.equal(isRuneCard(snapshot.players.P1.objects["p1-base-1"], specs["BASE-001"]), false);
assert.equal(ownerOrController({ ownerId: "OWNER", controllerId: "CTRL" }), "CTRL");
assert.equal(ownerOrController({ ownerId: "OWNER" }), "OWNER");

const entries = buildWirePlayerEntries(snapshot, "P2", specs);
assert.equal(entries[0].id, "P1", "from P2 perspective P1 must be the opponent row");
assert.equal(entries[1].id, "P2", "from P2 perspective P2 must be the self row");

console.log("Wire table view model check passed.");

function object(objectId, cardNo, ownerId, tags = []) {
  return { cardNo, controllerId: ownerId, objectId, ownerId, tags };
}

function groupRenderPlans(rows) {
  const groups = new Map();
  for (const row of rows) {
    const slotCount = row.renderEmptySlots
      ? Math.max(row.plan.slotCount, row.count, row.minSlots)
      : Math.max(row.count, row.minSlots);
    const renderPlan = planExports.resolveWireCardFlowRenderPlan({
      itemCount: row.count,
      minSlots: row.minSlots,
      sizingPlan: row.plan,
      slotCount
    });
    const group = groups.get(row.count) ?? new Set();
    group.add(renderPlanKey(renderPlan));
    groups.set(row.count, group);
  }
  return groups;
}

function renderPlanKey(plan) {
  return [
    plan.kind,
    plan.layout,
    plan.density,
    plan.fit,
    plan.cardWidth,
    plan.cardHeight,
    plan.scrollAfter,
    plan.minSlots,
    plan.slotCount,
    plan.visibleSlotCount
  ].join("|");
}

function loadTsModule(sourcePath, injectedValues = {}) {
  const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const moduleShim = { exports: {} };
  const names = Object.keys(injectedValues);
  const values = Object.values(injectedValues);

  new Function("exports", "module", ...names, output)(moduleShim.exports, moduleShim, ...values);
  return moduleShim.exports;
}
