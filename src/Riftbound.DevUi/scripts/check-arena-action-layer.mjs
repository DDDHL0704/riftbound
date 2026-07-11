import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const planPath = resolve(srcRoot, "utils/arenaPromptPresentation.ts");
const layerPath = resolve(srcRoot, "components/match/ArenaActionLayer.tsx");
const surfacePath = resolve(srcRoot, "components/match/PlayableMatchSurface.tsx");

assert(existsSync(planPath), "arena prompt presentation plan must exist");
assert(existsSync(layerPath), "arena action layer component must exist");

const { buildArenaPromptPresentation } = loadTsModule(planPath).exports;
assert.deepEqual(
  buildArenaPromptPresentation({ actionable: false, promptType: "MAIN_ACTION", selectedObjectId: "unit-1" }),
  { mode: "hidden", anchorObjectId: undefined }
);
assert.deepEqual(
  buildArenaPromptPresentation({ actionable: true, promptType: "MAIN_ACTION", selectedObjectId: "unit-1" }),
  { mode: "context", anchorObjectId: "unit-1" }
);
for (const promptType of ["MULLIGAN", "ASSIGN_COMBAT_DAMAGE", "ORDER_TRIGGERS"]) {
  assert.deepEqual(
    buildArenaPromptPresentation({ actionable: true, promptType, selectedObjectId: "unit-1" }),
    { mode: "modal", anchorObjectId: undefined },
    `${promptType} must use the dedicated modal presentation`
  );
}

const layerSource = readFileSync(layerPath, "utf8");
const surfaceSource = readFileSync(surfacePath, "utf8");
assert(layerSource.includes("data-arena-action-mode"), "action layer must expose its presentation mode");
assert(layerSource.includes("data-object-id"), "context mode must anchor from the selected card DOM");
assert(!surfaceSource.includes("game-action-dock"), "legacy action dock must not return");

console.log("Arena action-layer check passed.");

function loadTsModule(filename) {
  const source = readFileSync(filename, "utf8");
  const output = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
  }).outputText;
  const module = { exports: {} };
  new Function("exports", "module", "require", output)(module.exports, module, () => ({}));
  return module;
}
