import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/components/match/wirePilePlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { buildWirePilePlan } = moduleShim.exports;

for (const kind of ["banished", "graveyard", "library", "runeDeck"]) {
  const emptyPlan = buildWirePilePlan({ kind });
  assert.equal(emptyPlan.capacity, "unbounded");
  assert.equal(emptyPlan.count, 0);
  assert.equal(emptyPlan.face, "empty");
  assert.equal(emptyPlan.overflowCount, 0);
  assert.equal(emptyPlan.visibleCount, 0);
}

const libraryPlan = buildWirePilePlan({ count: 31, kind: "library" });
assert.equal(libraryPlan.face, "hidden-stack");
assert.equal(libraryPlan.count, 31);
assert.equal(libraryPlan.visibleCount, 0);
assert.equal(libraryPlan.overflowCount, 31);
assert.equal(libraryPlan.topObjectId, undefined);

const runeDeckPlan = buildWirePilePlan({ count: 12, kind: "runeDeck" });
assert.equal(runeDeckPlan.face, "hidden-stack");
assert.equal(runeDeckPlan.count, 12);
assert.equal(runeDeckPlan.visibleCount, 0);
assert.equal(runeDeckPlan.overflowCount, 12);

const graveyardPlan = buildWirePilePlan({ ids: ["spell-1", "spell-2"], kind: "graveyard" });
assert.equal(graveyardPlan.face, "public-top");
assert.equal(graveyardPlan.count, 2);
assert.equal(graveyardPlan.visibleCount, 1);
assert.equal(graveyardPlan.overflowCount, 1);
assert.equal(graveyardPlan.topObjectId, "spell-2");

const banishedPlan = buildWirePilePlan({ ids: ["unit-1"], kind: "banished" });
assert.equal(banishedPlan.face, "public-top");
assert.equal(banishedPlan.count, 1);
assert.equal(banishedPlan.visibleCount, 1);
assert.equal(banishedPlan.overflowCount, 0);
assert.equal(banishedPlan.topObjectId, "unit-1");

const publicCountWithoutIds = buildWirePilePlan({ count: 4, kind: "graveyard" });
assert.equal(publicCountWithoutIds.face, "hidden-stack", "public piles need object ids before exposing a top card");
assert.equal(publicCountWithoutIds.visibleCount, 0);
assert.equal(publicCountWithoutIds.overflowCount, 4);
assert.equal(publicCountWithoutIds.topObjectId, undefined);

const clampedCount = buildWirePilePlan({ count: -3, kind: "library" });
assert.equal(clampedCount.count, 0);
assert.equal(clampedCount.face, "empty");

console.log("Wire pile plan check passed.");
