import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/components/match/wireInformationBoundaryPlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };
new Function("exports", "module", output)(moduleShim.exports, moduleShim);
const { buildWireInformationBoundaryPlan } = moduleShim.exports;

const safePlan = buildWireInformationBoundaryPlan({
  events: [{
    description: "hidden trigger source",
    kind: "TRIGGER",
    objectRefs: [{ isHidden: true, objectId: "HIDDEN", role: "来源" }],
    payload: {}
  }],
  table: table()
});
assert.equal(safePlan.state, "safe");
assert.equal(safePlan.issueCount, 0);
assert.equal(safePlan.rows.find((row) => row.key === "hand:P2").stateLabel, "仅公开数量");
assert.equal(safePlan.rows.find((row) => row.key === "deck:P2").state, "safe");
assert.equal(safePlan.rows.find((row) => row.key === "standby").stateLabel, "无隐藏待命");
assert.equal(safePlan.metrics.find((metric) => metric.key === "standby").state, "safe");
assert.equal(safePlan.rows.find((row) => row.key === "eventRefs").state, "safe");
assert.deepEqual(safePlan.metrics.map((metric) => metric.value), ["2", "5", "2 / 2", "0", "0", "1", "0"]);

const handLeakPlan = buildWireInformationBoundaryPlan({
  table: table({
    opponentZones: {
      hand: ["p2-secret-hand"],
      mainDeckCount: 30,
      runeDeckCount: 8
    }
  })
});
assert.equal(handLeakPlan.state, "leak");
assert.equal(handLeakPlan.rows.find((row) => row.key === "hand:P2").state, "leak");
assert.match(handLeakPlan.summary, /泄漏/);

const deckLeakPlan = buildWireInformationBoundaryPlan({
  table: table({
    opponentZones: {
      handHidden: 5,
      mainDeck: ["top-secret-main"],
      mainDeckCount: 30,
      runeDeck: ["top-secret-rune"],
      runeDeckCount: 8
    }
  })
});
assert.equal(deckLeakPlan.state, "leak");
assert.equal(deckLeakPlan.rows.find((row) => row.key === "deck:P2").stateLabel, "泄漏牌堆顺序");

const faceDownLeakPlan = buildWireInformationBoundaryPlan({
  table: table({
    selfObjects: {
      "face-down-1": { cardNo: "OGN-001/298", isFaceDown: true, objectId: "face-down-1", ownerId: "P1" }
    }
  })
});
assert.equal(faceDownLeakPlan.state, "leak");
assert.equal(faceDownLeakPlan.rows.find((row) => row.key === "faceDown").state, "leak");

const hiddenStandbySafePlan = buildWireInformationBoundaryPlan({
  table: table({
    standbySlots: [{
      battlefieldObjectId: "battlefield-left",
      side: "opponent",
      sidePlayerId: "P2",
      slotId: "battlefield-left:standby:1",
      state: "HIDDEN",
      visible: false
    }]
  })
});
assert.equal(hiddenStandbySafePlan.state, "safe");
assert.equal(hiddenStandbySafePlan.rows.find((row) => row.key === "standby").stateLabel, "仅占位/数量");

const hiddenStandbyIdLeakPlan = buildWireInformationBoundaryPlan({
  table: table({
    standbySlots: [{
      battlefieldObjectId: "battlefield-left",
      objectId: "p2-hidden-standby",
      side: "opponent",
      sidePlayerId: "P2",
      slotId: "battlefield-left:standby:1",
      state: "HIDDEN",
      visible: false
    }]
  })
});
assert.equal(hiddenStandbyIdLeakPlan.state, "leak");
assert.equal(hiddenStandbyIdLeakPlan.rows.find((row) => row.key === "standby").stateLabel, "泄漏隐藏待命标识");
assert.equal(hiddenStandbyIdLeakPlan.metrics.find((metric) => metric.key === "standby").state, "leak");
assert.equal(
  JSON.stringify(hiddenStandbyIdLeakPlan.rows.find((row) => row.key === "standby")).includes("p2-hidden-standby"),
  false,
  "standby boundary UI summary must not echo hidden standby object id"
);

const hiddenStandbyCardNoLeakPlan = buildWireInformationBoundaryPlan({
  table: table({
    battlefieldObjects: {
      "p2-hidden-standby": {
        cardNo: "SECRET-001",
        isFaceDown: true,
        objectId: "p2-hidden-standby",
        ownerId: "P2"
      }
    },
    standbySlots: [{
      battlefieldObjectId: "battlefield-left",
      objectId: "p2-hidden-standby",
      side: "opponent",
      sidePlayerId: "P2",
      slotId: "battlefield-left:standby:1",
      state: "HIDDEN",
      visible: false
    }]
  })
});
assert.equal(hiddenStandbyCardNoLeakPlan.state, "leak");
assert.equal(hiddenStandbyCardNoLeakPlan.rows.find((row) => row.key === "standby").stateLabel, "泄漏待命牌号");
assert.equal(hiddenStandbyCardNoLeakPlan.metrics.find((metric) => metric.key === "standby").state, "leak");
assert.equal(
  JSON.stringify(hiddenStandbyCardNoLeakPlan.rows.find((row) => row.key === "standby")).includes("SECRET-001"),
  false,
  "standby boundary UI summary must not echo leaked cardNo"
);

const hiddenRefIdLeakPlan = buildWireInformationBoundaryPlan({
  events: [{
    description: "hidden source with real object id",
    kind: "TRIGGER",
    objectRefs: [{ isHidden: true, objectId: "real-hidden-object", role: "来源" }],
    payload: {}
  }],
  table: table()
});
assert.equal(hiddenRefIdLeakPlan.state, "leak");
assert.equal(hiddenRefIdLeakPlan.rows.find((row) => row.key === "eventRefs").state, "leak");
assert.equal(
  JSON.stringify(hiddenRefIdLeakPlan.rows.find((row) => row.key === "eventRefs")).includes("real-hidden-object"),
  false,
  "hidden event boundary UI summary must not echo hidden object id"
);

const faceDownHiddenRefPlan = buildWireInformationBoundaryPlan({
  events: [{
    description: "face-down source with visible object id",
    kind: "TRIGGER",
    objectRefs: [{ isFaceDown: true, isHidden: true, objectId: "face-down-object", role: "来源" }],
    payload: {}
  }],
  table: table()
});
assert.equal(faceDownHiddenRefPlan.state, "leak");
assert.equal(faceDownHiddenRefPlan.rows.find((row) => row.key === "eventRefs").stateLabel, "泄漏隐藏对象标识");

const missingCountPlan = buildWireInformationBoundaryPlan({
  table: table({
    opponentZones: {
      mainDeckCount: 30,
      runeDeckCount: 8
    }
  })
});
assert.equal(missingCountPlan.state, "missing");
assert.equal(missingCountPlan.rows.find((row) => row.key === "hand:P2").stateLabel, "缺少数量");

console.log("Wire information boundary plan check passed.");

function table({
  battlefieldObjects = {},
  opponentZones = {
    handHidden: 5,
    mainDeckCount: 30,
    runeDeckCount: 8
  },
  selfObjects = {},
  standbySlots = []
} = {}) {
  const p1Objects = {
    "p1-hand-1": { cardNo: "OGN-001/298", objectId: "p1-hand-1", ownerId: "P1" },
    ...selfObjects
  };
  return {
    battlefield: {
      lanes: standbySlots.length > 0
        ? [{
          battlefieldId: "battlefield-left",
          hiddenStandbyCount: standbySlots.length,
          index: 0,
          standbySlots
        }]
        : [],
      objects: battlefieldObjects,
      unitPlan: {}
    },
    opponent: undefined,
    players: [
      {
        baseObjectIds: [],
        basePartitionSource: "server",
        handIds: Array.isArray(opponentZones.hand) ? opponentZones.hand : [],
        hiddenHandIds: Array.from({ length: Number(opponentZones.handHidden ?? 0) }, (_, index) => `hidden-P2-${index}`),
        id: "P2",
        label: "P2 对手",
        objects: {},
        player: { handSize: opponentZones.handHidden, objects: {}, zones: opponentZones },
        runeIds: [],
        side: "opponent",
        zones: opponentZones
      },
      {
        baseObjectIds: [],
        basePartitionSource: "server",
        handIds: ["p1-hand-1"],
        hiddenHandIds: [],
        id: "P1",
        label: "P1 我方",
        objects: p1Objects,
        player: { objects: p1Objects, zones: { hand: ["p1-hand-1"], mainDeckCount: 31, runeDeckCount: 7 } },
        runeIds: [],
        side: "self",
        zones: { hand: ["p1-hand-1"], mainDeckCount: 31, runeDeckCount: 7 }
      }
    ],
    self: undefined
  };
}
