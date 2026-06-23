import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const deckPageSource = readFileSync(resolve(srcRoot, "pages/DecksPage.tsx"), "utf8");
const packageJson = JSON.parse(readFileSync(resolve(scriptDir, "../package.json"), "utf8"));
const { buildDeckImportHandoffPlan } = loadTsModule(resolve(srcRoot, "utils/deckImportHandoffPlan.ts")).exports;

const baseSummary = {
  battlefields: 3,
  championCardNo: "UNL-022/219",
  distinctBattlefields: 3,
  distinctMainDeck: 14,
  distinctRuneDeck: 12,
  legendCardNo: "UNL-181/219",
  mainDeck: 40,
  runeDeck: 12
};

const emptyStarter = buildDeckImportHandoffPlan({
  canApplyImport: false,
  commandPreviewLength: 1200,
  currentSummary: baseSummary,
  deckSource: "starter",
  importState: "empty"
});
assert.deepEqual(emptyStarter.sections.map((section) => section.id), ["intake", "recovery", "current", "command", "server"]);
assert.equal(emptyStarter.activeSectionId, "intake");
assert.equal(emptyStarter.sections.find((section) => section.id === "intake")?.state, "waiting");
assert.equal(emptyStarter.sections.find((section) => section.id === "recovery")?.value, "默认 starter");
assert.equal(emptyStarter.sections.find((section) => section.id === "server")?.source, "server-authority");
assert.equal(emptyStarter.summary, "导入：等待粘贴 / 来源：默认 starter / 命令：40/12/3");

const invalidCached = buildDeckImportHandoffPlan({
  canApplyImport: false,
  commandPreviewLength: 2500,
  currentSummary: baseSummary,
  deckSource: "storage",
  importState: "invalid"
});
assert.equal(invalidCached.activeSectionId, "intake");
assert.equal(invalidCached.sections.find((section) => section.id === "intake")?.state, "blocking");
assert.equal(invalidCached.sections.find((section) => section.id === "recovery")?.state, "ready");
assert.equal(invalidCached.sections.find((section) => section.id === "recovery")?.value, "本地缓存");

const validImport = buildDeckImportHandoffPlan({
  canApplyImport: true,
  commandPreviewLength: 1800,
  currentSummary: baseSummary,
  deckSource: "query",
  importState: "valid",
  previewSummary: {
    ...baseSummary,
    battlefields: 2,
    distinctBattlefields: 2,
    distinctMainDeck: 13,
    mainDeck: 41
  }
});
assert.equal(validImport.activeSectionId, "command");
assert.equal(validImport.sections.find((section) => section.id === "intake")?.value, "结构可导入");
assert.equal(validImport.sections.find((section) => section.id === "current")?.value, "40 主 / 12 符文 / 3 战场");
assert.equal(validImport.sections.find((section) => section.id === "command")?.state, "ready");
assert.equal(validImport.sections.find((section) => section.id === "server")?.state, "authority");
assert.ok(validImport.sections.find((section) => section.id === "intake")?.detail.includes("41 主 / 12 符文 / 2 战场"));

assert.ok(deckPageSource.includes("buildDeckImportHandoffPlan"), "DecksPage must build the deck import handoff plan.");
assert.ok(deckPageSource.includes("data-deck-import-handoff"), "DecksPage must expose the handoff surface for browser smoke.");
assert.ok(deckPageSource.includes("data-deck-import-handoff-section"), "DecksPage must expose each handoff section.");
assert.ok(deckPageSource.includes("data-deck-import-handoff-source"), "DecksPage must expose handoff authority sources.");
assert.ok(deckPageSource.includes("data-deck-import-source"), "DecksPage must expose the current deck source.");
assert.ok(packageJson.scripts["check:deck-import-handoff-plan"], "Package scripts must expose the deck import handoff check.");
assert.ok(packageJson.scripts.build.includes("check:deck-import-handoff-plan"), "Build must run the deck import handoff check.");

console.log("Deck import handoff plan check passed.");

function loadTsModule(filename) {
  const resolved = resolve(filename);
  const cached = moduleCache.get(resolved);
  if (cached) {
    return cached;
  }

  const source = readFileSync(resolved, "utf8");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      esModuleInterop: true,
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText;
  const module = { exports: {} };
  moduleCache.set(resolved, module);

  const requireShim = (id) => {
    if (id.startsWith(".")) {
      const target = resolve(dirname(resolved), id);
      if (target.endsWith("/types/protocol") || target.endsWith("/types/catalog")) {
        return {};
      }

      return loadTsModule(`${target}.ts`).exports;
    }

    throw new Error(`Unexpected import in deck import handoff check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}
