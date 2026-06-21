import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const flowPlanExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireCardFlowPlan.ts"));
const authorityExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireTableAuthorityPlan.ts"), flowPlanExports);
const projectionExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireCommandFollowupLayoutProjectionPlan.ts"), {
  buildWireTableSelectedLayoutPlan: authorityExports.buildWireTableSelectedLayoutPlan
});

const { buildWireCommandFollowupLayoutProjectionPlan } = projectionExports;
const table = tableView();

const plan = followupPlan([
  {
    kind: "CARD_PLAYED",
    key: "10:CARD_PLAYED:0",
    refs: [
      publicRef("source", "p1-hand-spell"),
      publicRef("target", "p2-right-1"),
      hiddenRef("choice")
    ],
    title: "打出卡牌"
  },
  {
    kind: "BATTLEFIELD_CONTROL_RESOLVED",
    key: "10:BATTLEFIELD_CONTROL_RESOLVED:1",
    refs: [publicRef("site", "fixture-right-battlefield")],
    title: "据守结算"
  }
]);
const projection = buildWireCommandFollowupLayoutProjectionPlan({ plan, table });
const rows = new Map(projection.rows.map((row) => [row.objectId, row]));

assert.equal(projection.state, "linked");
assert.equal(projection.publicRefCount, 3);
assert.equal(projection.hiddenRefCount, 1);
assert.equal(projection.locatedCount, 3);
assert.equal(projection.unknownCount, 0);
assert.equal(projection.totalRefCount, 4);
assert.equal(rows.get("p1-hand-spell")?.zoneKey, "self:hand");
assert.equal(rows.get("p1-hand-spell")?.capacityRowKey, "self:hand");
assert.equal(rows.get("p2-right-1")?.zoneKey, "battlefield:1:opponent");
assert.equal(rows.get("p2-right-1")?.layoutKind, "battlefield-unit");
assert.equal(rows.get("fixture-right-battlefield")?.zoneKey, "battlefield:1:site");
assert.equal(rows.get("fixture-right-battlefield")?.layoutKind, "site");

const hiddenOnlyProjection = buildWireCommandFollowupLayoutProjectionPlan({
  plan: followupPlan([
    {
      kind: "CARD_DRAWN",
      key: "11:CARD_DRAWN:0",
      refs: [hiddenRef("card")],
      title: "抽牌"
    }
  ]),
  table
});
assert.equal(hiddenOnlyProjection.state, "hidden-only");
assert.equal(hiddenOnlyProjection.rows.length, 0);
assert.equal(hiddenOnlyProjection.hiddenRefCount, 1);
assert.equal(hiddenOnlyProjection.summary.includes("不投影身份或区域"), true);

const unknownProjection = buildWireCommandFollowupLayoutProjectionPlan({
  plan: followupPlan([
    {
      kind: "CARD_MOVED",
      key: "12:CARD_MOVED:0",
      refs: [publicRef("object", "missing-object")],
      title: "移动卡牌"
    }
  ]),
  table
});
assert.equal(unknownProjection.state, "unknown");
assert.equal(unknownProjection.rows[0].layoutState, "unknown");
assert.equal(unknownProjection.rows[0].zoneLabel, "未定位");

const limitedProjection = buildWireCommandFollowupLayoutProjectionPlan({ maxRows: 1, plan, table });
assert.equal(limitedProjection.rows.length, 1);
assert.equal(limitedProjection.overflowCount, 2);

console.log("Wire command followup layout projection plan check passed.");

function followupPlan(events) {
  return {
    bridge: {
      headline: "已收到同 tick 事件",
      nextStepLabel: "查看事件引用",
      rows: [],
      serverStateLabel: "事件",
      state: "ready",
      stateLabel: "已同步",
      summary: "已收到同 tick 事件"
    },
    events: events.map((event) => ({
      description: event.title,
      kind: event.kind,
      key: event.key,
      refCount: event.refs.length,
      refs: event.refs,
      title: event.title
    })),
    hiddenEventCount: 0,
    metrics: [],
    serverFollowupState: "events",
    serverFollowupStateLabel: "事件",
    state: "accepted-events",
    summary: "同 tick 事件已进入前端。"
  };
}

function publicRef(role, objectId) {
  return {
    hidden: false,
    key: `${role}:${objectId}`,
    label: `${role}:${objectId}`,
    objectId,
    role
  };
}

function hiddenRef(role) {
  return {
    hidden: true,
    key: `${role}:hidden`,
    label: `${role}:隐藏对象`,
    role
  };
}

function tableView() {
  return {
    battlefield: {
      lanes: [
        lane(0, {
          own: [],
          opposing: [],
          standby: []
        }),
        lane(1, {
          battlefieldId: "fixture-right-battlefield",
          own: [],
          opposing: ["p2-right-1"],
          standby: []
        })
      ],
      objects: {},
      standbyPlan: flowPlanExports.buildWireCardFlowPlan({ itemCount: 0, kind: "standby", minSlots: 1 }),
      unitPlan: flowPlanExports.buildWireCardFlowPlan({ itemCount: 1, kind: "battlefield-unit", minSlots: 3 })
    },
    players: [
      {
        baseObjectIds: [],
        basePartitionSource: "server",
        handIds: [],
        hiddenHandIds: [],
        id: "P2",
        label: "P2 对手",
        objects: {},
        player: {},
        runeIds: [],
        side: "opponent",
        zones: {}
      },
      {
        baseObjectIds: [],
        basePartitionSource: "server",
        handIds: ["p1-hand-spell"],
        hiddenHandIds: [],
        id: "P1",
        label: "P1 我方",
        objects: {},
        player: {},
        runeIds: [],
        side: "self",
        zones: {}
      }
    ],
    playerPlans: {
      basePlan: flowPlanExports.buildWireCardFlowPlan({ itemCount: 0, kind: "base", minSlots: 1 }),
      handPlan: flowPlanExports.buildWireCardFlowPlan({ itemCount: 1, kind: "hand" })
    }
  };
}

function lane(index, { battlefieldId, own, opposing, standby }) {
  return {
    battlefieldId: battlefieldId ?? `battlefield-${index}`,
    cardNo: `SITE-${index}`,
    controllerId: index === 0 ? "P1" : "P2",
    hiddenStandbyCount: 0,
    index,
    occupantSplitSource: "server-unitsBySide",
    opposingOccupants: opposing,
    ownOccupants: own,
    scoredThisTurnPlayerIds: [],
    standbySlotCount: standby.length,
    standbySlotSource: "server-standbySlots",
    standbySlots: standby.map((slotId) => ({ slotId })),
    zonePlayerId: index === 0 ? "P1" : "P2"
  };
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
