import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const planExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireCardFlowPlan.ts"));
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
        occupantObjectIds: ["p1-left-1", "p1-left-2", "p2-left-1"],
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
        occupantObjectIds: ["p2-right-1", "p2-right-2", "p1-right-1"],
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
        "p1-right-1": object("p1-right-1", "UNIT-003", "P1")
      },
      zones: {
        base: ["p1-base-1", "p1-rune-1", "p1-rune-tagged"],
        baseCards: ["p1-base-1"],
        baseRunes: ["p1-rune-1", "p1-rune-tagged"],
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
        "p2-left-1": { ...object("p2-left-1", "UNIT-001", "P2"), controllerId: "P1" },
        "p2-right-1": object("p2-right-1", "UNIT-001", "P2"),
        "p2-right-2": object("p2-right-2", "UNIT-004", "P2")
      },
      zones: {
        base: ["p2-rune-1", "p2-base-1"],
        baseCards: ["p2-base-1"],
        baseRunes: ["p2-rune-1"],
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
assert.deepEqual(table.self.hiddenHandIds, ["hidden-P1-0", "hidden-P1-1"]);
assert.deepEqual(table.opponent.baseObjectIds, ["p2-base-1"]);
assert.equal(table.opponent.basePartitionSource, "server");
assert.deepEqual(table.opponent.runeIds, ["p2-rune-1"]);
assert.deepEqual(table.opponent.hiddenHandIds, ["hidden-P2-0", "hidden-P2-1", "hidden-P2-2", "hidden-P2-3"]);

const battlefield = buildWireBattlefieldModel(snapshot, "P1");
assert.equal(battlefield.lanes.length, 2);
assert.equal(battlefield.lanes[0].occupantSplitSource, "server-unitsBySide");
assert.deepEqual(battlefield.lanes[0].ownOccupants, ["p1-left-1", "p1-left-2"]);
assert.deepEqual(battlefield.lanes[0].opposingOccupants, ["p2-left-1"]);
assert.equal(
  ownerOrController(snapshot.players.P2.objects["p2-left-1"]),
  "P1",
  "fixture must prove server unitsBySide wins over local controller fallback"
);
assert.equal(battlefield.lanes[1].occupantSplitSource, "server-unitsBySide");
assert.deepEqual(battlefield.lanes[1].ownOccupants, ["p1-right-1"]);
assert.deepEqual(battlefield.lanes[1].opposingOccupants, ["p2-right-1", "p2-right-2"]);
assert.deepEqual(table.battlefield.unitPlan, battlefield.unitPlan, "table and battlefield builders must share one unit plan");
assert.equal(table.battlefield.unitPlan.itemCount, 2, "same max occupancy should drive every battlefield quadrant size");
assert.equal(table.battlefield.unitPlan.slotCount, 3, "empty space should remain visible for low-count unit zones");

const p2Perspective = buildWireBattlefieldModel(snapshot, "P2");
assert.deepEqual(p2Perspective.lanes[0].ownOccupants, ["p2-left-1"]);
assert.deepEqual(p2Perspective.lanes[0].opposingOccupants, ["p1-left-1", "p1-left-2"]);
assert.deepEqual(p2Perspective.unitPlan, battlefield.unitPlan, "perspective flips ownership but not card sizing");

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
assert.deepEqual(legacyBattlefield.lanes[0].ownOccupants, ["p1-left-1", "p1-left-2", "p2-left-1"]);
assert.deepEqual(legacyBattlefield.lanes[0].opposingOccupants, []);

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
