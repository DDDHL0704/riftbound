import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const flowPlanExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireCardFlowPlan.ts"));
const authorityExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireTableAuthorityPlan.ts"), flowPlanExports);
const projectionExports = loadTsModule(resolve(scriptDir, "../src/components/match/wireActionLayoutProjectionPlan.ts"), {
  buildWireTableSelectedLayoutPlan: authorityExports.buildWireTableSelectedLayoutPlan
});

const { buildWireActionLayoutProjectionPlan } = projectionExports;

const actionMap = {
  blockedObjectEntries: [
    {
      disabledCandidateCount: 1,
      enabledCandidateCount: 0,
      label: "OGN-002/298",
      objectId: "blocked-unit",
      selected: false
    }
  ],
  candidatePlans: [
    {
      candidateLabel: "打出手牌",
      enabled: true,
      stepRows: [
        {
          objectRefs: [
            { key: "source:p1-hand-1", label: "手牌法术", objectId: "p1-hand-1", roleLabel: "来源" }
          ]
        },
        {
          objectRefs: [
            { key: "target:p2-unit-1", label: "敌方单位", objectId: "p2-unit-1", roleLabel: "目标" }
          ]
        }
      ]
    },
    {
      candidateLabel: "移动单位",
      enabled: false,
      stepRows: [
        {
          objectRefs: [
            { key: "source:blocked-unit", label: "疲劳单位", objectId: "blocked-unit", roleLabel: "来源" }
          ]
        }
      ]
    }
  ],
  focus: {
    relatedCandidates: [
      {
        enabled: true,
        label: "打出手牌",
        nextObjectRefs: [
          { key: "next:p2-unit-1", label: "敌方单位", objectId: "p2-unit-1", roleLabel: "目标" }
        ]
      }
    ]
  },
  objectEntries: [
    {
      disabledCandidateCount: 0,
      enabledCandidateCount: 1,
      label: "OGN-001/298",
      objectId: "p1-hand-1",
      selected: true
    }
  ]
};
const table = tableView();
const projection = buildWireActionLayoutProjectionPlan({
  actionMap,
  selectedObjectId: "p1-hand-1",
  table
});
const rows = new Map(projection.rows.map((row) => [row.key, row]));

assert.equal(projection.state, "ready");
assert.equal(projection.readyCount, 4);
assert.equal(projection.blockedCount, 2);
assert.equal(projection.locatedCount, 6);
assert.equal(projection.selectedCount, 2);
assert.equal(projection.totalCount, 6);
assert.equal(projection.overflowCount, 0);
assert.equal(rows.get("enabled-entry:可操作对象:p1-hand-1:1 个可提交候选")?.zoneKey, "self:hand");
assert.equal(rows.get("enabled-entry:可操作对象:p1-hand-1:1 个可提交候选")?.capacityRowKey, "self:hand");
assert.equal(rows.get("candidate-step:目标:p2-unit-1:打出手牌")?.zoneKey, "battlefield:1:opponent");
assert.equal(rows.get("candidate-step:目标:p2-unit-1:打出手牌")?.capacityRowKey, "battlefield:1:opponent");
assert.equal(rows.get("blocked-entry:阻断对象:blocked-unit:1 个阻断候选")?.zoneKey, "battlefield:0:self");
assert.equal(rows.get("candidate-step:来源:blocked-unit:移动单位")?.actionState, "blocked");

const unknownProjection = buildWireActionLayoutProjectionPlan({
  actionMap: {
    ...actionMap,
    objectEntries: [
      {
        disabledCandidateCount: 0,
        enabledCandidateCount: 1,
        label: "未定位对象",
        objectId: "unknown-object",
        selected: false
      }
    ]
  },
  table
});
const unknownRow = unknownProjection.rows.find((row) => row.objectId === "unknown-object");
assert.equal(unknownRow?.layoutState, "unknown");
assert.equal(unknownRow?.actionState, "unknown");

const limitedProjection = buildWireActionLayoutProjectionPlan({
  actionMap,
  maxRows: 2,
  selectedObjectId: "p1-hand-1",
  table
});
assert.equal(limitedProjection.rows.length, 2);
assert.equal(limitedProjection.overflowCount, 4);

console.log("Wire action layout projection plan check passed.");

function tableView() {
  return {
    battlefield: {
      lanes: [
        lane(0, {
          own: ["blocked-unit"],
          opposing: [],
          standby: []
        }),
        lane(1, {
          own: [],
          opposing: ["p2-unit-1"],
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
        handIds: ["p1-hand-1"],
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

function lane(index, { own, opposing, standby }) {
  return {
    battlefieldId: `battlefield-${index}`,
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
