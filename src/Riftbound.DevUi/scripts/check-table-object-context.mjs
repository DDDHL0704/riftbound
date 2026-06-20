import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/tableObjectContext.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function(
  "exports",
  "module",
  "asArray",
  "asRecord",
  "asString",
  "promptReasonLabel",
  "gameEventObjectRefPlan",
  "buildPromptInteractionModel",
  "promptCommandBindingLabel",
  "promptChoiceRoleLabel",
  "promptChoiceSummaryObjectIds",
  "redactInternalText",
  "buildCardObjectIndex",
  output
)(
  moduleShim.exports,
  moduleShim,
  asArray,
  asRecord,
  asString,
  promptReasonLabel,
  gameEventObjectRefPlan,
  buildPromptInteractionModel,
  promptCommandBindingLabel,
  promptChoiceRoleLabel,
  promptChoiceSummaryObjectIds,
  redactInternalText,
  buildCardObjectIndex
);

const { buildTableObjectContextModel } = moduleShim.exports;

const model = buildTableObjectContextModel({
  perspectivePlayerId: "P1",
  snapshot: {
    activePlayerId: "P1",
    lanes: {
      battlefields: [
        {
          battlefieldObjectId: "battlefield-left",
          cardNo: "OGN-275/298",
          occupantObjectIds: ["p1-left-unit"],
          zonePlayerId: "P1"
        }
      ]
    },
    players: {
      P1: {
        objects: {
          "battlefield-left": {
            cardNo: "OGN-275/298",
            location: { playerId: "P1", zone: "BATTLEFIELD" },
            objectId: "battlefield-left",
            ownerId: "P1",
            tags: ["CARD_TYPE:BATTLEFIELD"]
          },
          "conflicting-hand-card": {
            cardNo: "OGN-001/298",
            controllerId: "P1",
            location: { playerId: "P2", zone: "GRAVEYARD" },
            objectId: "conflicting-hand-card",
            ownerId: "P1"
          },
          "p1-base-rune": {
            cardNo: "RUNE-RED",
            location: { playerId: "P1", zone: "BASE" },
            objectId: "p1-base-rune",
            ownerId: "P1",
            tags: ["CARD_TYPE:RUNE"]
          },
          "p1-left-unit": {
            cardNo: "OGN-010/298",
            controllerId: "P1",
            location: { battlefieldObjectId: "battlefield-left", playerId: "P1", zone: "BATTLEFIELD" },
            objectId: "p1-left-unit",
            ownerId: "P1"
          }
        },
        zones: {
          base: ["p1-base-rune"],
          battlefields: ["battlefield-left"],
          hand: ["conflicting-hand-card"]
        }
      },
      P2: {
        objects: {},
        zones: {
          graveyard: []
        }
      }
    },
    stack: [],
    tick: 1,
    timing: {},
    turnNumber: 1,
    turnState: "MAIN"
  }
});

assert.equal(model.byId["conflicting-hand-card"].zone.kind, "graveyard");
assert.equal(model.byId["conflicting-hand-card"].zone.label, "对方已打出牌堆");
assert.equal(model.byId["battlefield-left"].zone.kind, "battlefield-site");
assert.equal(model.byId["battlefield-left"].zone.label, "左战场牌");
assert.equal(model.byId["p1-left-unit"].zone.kind, "battlefield");
assert.equal(model.byId["p1-left-unit"].zone.battlefieldObjectId, "battlefield-left");
assert.equal(model.byId["p1-left-unit"].zone.label, "左战场 / 我方单位");
assert.equal(model.byId["p1-base-rune"].zone.kind, "rune");
assert.equal(model.byId["p1-base-rune"].zone.label, "我方已抽出符文");

console.log("Table object context check passed.");

function asRecord(value) {
  return value && typeof value === "object" && !Array.isArray(value) ? value : {};
}

function asString(value, fallback = "未提供") {
  return typeof value === "string" && value.trim().length > 0 ? value : fallback;
}

function asArray(value) {
  return Array.isArray(value) ? value : [];
}

function promptReasonLabel(reason, fallback) {
  return reason || fallback || "服务端候选";
}

function gameEventObjectRefPlan(event) {
  return { refs: event.objectRefs ?? [], source: event.objectRefs?.length ? "server" : "none" };
}

function buildPromptInteractionModel() {
  return { candidates: [], objectById: new Map() };
}

function promptCommandBindingLabel(binding) {
  return binding?.label ?? binding?.field ?? "字段";
}

function promptChoiceRoleLabel(role) {
  return role || "对象";
}

function promptChoiceSummaryObjectIds(choice) {
  return choice?.objectIds ?? [];
}

function redactInternalText(value) {
  return String(value);
}

function buildCardObjectIndex(snapshot) {
  const indexed = {};
  for (const player of Object.values(snapshot?.players ?? {})) {
    for (const [objectId, object] of Object.entries(player.objects ?? {})) {
      indexed[object.objectId ?? objectId] = { ...object, objectId: object.objectId ?? objectId };
    }
  }

  for (const battlefield of asArray(asRecord(snapshot?.lanes).battlefields)) {
    const objectId = asString(battlefield.battlefieldObjectId, "");
    if (!objectId || indexed[objectId]) {
      continue;
    }

    indexed[objectId] = {
      cardNo: battlefield.cardNo ?? null,
      location: { zone: "BATTLEFIELD" },
      objectId,
      ownerId: battlefield.zonePlayerId,
      tags: ["CARD_TYPE:BATTLEFIELD"]
    };
  }

  return indexed;
}
