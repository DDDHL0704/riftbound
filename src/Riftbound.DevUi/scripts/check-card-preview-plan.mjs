import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/utils/cardPreviewPlan.ts");
const source = readFileSync(sourcePath, "utf8").replace(/^import[\s\S]*?;\n/gm, "");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { CARD_PREVIEW_DELAY_MS, buildCardPreviewPlan } = moduleShim.exports;

assert.equal(CARD_PREVIEW_DELAY_MS, 680);

const emptyPlan = buildCardPreviewPlan();
assert.equal(emptyPlan.state, "empty");
assert.equal(emptyPlan.kind, "standard");
assert.equal(emptyPlan.orientation, "portrait");
assert.equal(emptyPlan.delayMs, CARD_PREVIEW_DELAY_MS);

const unitPlan = buildCardPreviewPlan({
  objectId: "unit-1",
  object: { cardNo: "UNIT-001", objectId: "unit-1" },
  spec: {
    cardCategoryName: "单位",
    cardName: "疾风剑豪",
    frontImage: "/cards/unit.png"
  }
});

assert.equal(unitPlan.state, "ready");
assert.equal(unitPlan.kind, "standard");
assert.equal(unitPlan.orientation, "portrait");
assert.equal(unitPlan.imageUrl, "/cards/unit.png");
assert.equal(unitPlan.objectId, "unit-1");
assert.equal(unitPlan.title, "疾风剑豪");

const battlefieldPlan = buildCardPreviewPlan({
  object: {
    cardNo: "BATTLEFIELD-001",
    objectId: "battlefield-1",
    tags: ["CARD_TYPE:BATTLEFIELD"]
  },
  spec: {
    cardCategoryName: "战场",
    cardName: "大厅",
    frontImage: " /cards/battlefield.png "
  }
});

assert.equal(battlefieldPlan.state, "ready");
assert.equal(battlefieldPlan.kind, "battlefield");
assert.equal(battlefieldPlan.orientation, "landscape-counterclockwise");
assert.equal(battlefieldPlan.imageUrl, "/cards/battlefield.png");
assert.equal(battlefieldPlan.objectId, "battlefield-1");

const noImagePlan = buildCardPreviewPlan({
  object: { cardNo: "HIDDEN", objectId: "hidden-1" },
  spec: { cardName: "隐藏卡牌", frontImage: " " }
});

assert.equal(noImagePlan.state, "empty");
assert.equal(noImagePlan.title, "卡牌");

console.log("Card preview plan check passed.");
