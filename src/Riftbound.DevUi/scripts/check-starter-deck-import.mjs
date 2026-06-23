import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const starterDeckSourcePath = resolve(scriptDir, "../src/utils/starterDeck.ts");
const decksPageSourcePath = resolve(scriptDir, "../src/pages/DecksPage.tsx");
const deckImportFlowPlanSourcePath = resolve(scriptDir, "../src/utils/deckImportFlowPlan.ts");
const starterDeckSource = readFileSync(starterDeckSourcePath, "utf8");
const output = ts.transpileModule(starterDeckSource, {
  compilerOptions: {
    esModuleInterop: true,
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

function requireShim(id) {
  throw new Error(`Unexpected starter deck import dependency: ${id}`);
}

new Function("exports", "module", "require", output)(moduleShim.exports, moduleShim, requireShim);

const {
  deckToImportText,
  defaultStarterDeck,
  parseDeckImport,
  parseStarterDeckOverride,
  serializeStarterDeck,
  summarizeStarterDeck
} = moduleShim.exports;

const defaultDeck = defaultStarterDeck();

const jsonRoundtrip = parseDeckImport(serializeStarterDeck(defaultDeck));
assert.equal(jsonRoundtrip.ok, true);
assert.equal(jsonRoundtrip.format, "json");
assert.deepEqual(jsonRoundtrip.deck, defaultDeck);

const compactJsonRoundtrip = parseDeckImport(JSON.stringify({
  battlefields: ["FAKE-BF-1"],
  championCardNo: "FAKE-CHAMPION",
  legendCardNo: "FAKE-LEGEND",
  mainDeck: ["FAKE-MAIN-1"],
  runeDeck: ["FAKE-RUNE-1"]
}));
assert.equal(compactJsonRoundtrip.ok, true);
assert.equal(compactJsonRoundtrip.deck.cmdType, "SUBMIT_DECK");
assert.deepEqual(summarizeStarterDeck(compactJsonRoundtrip.deck), {
  battlefields: 1,
  championCardNo: "FAKE-CHAMPION",
  distinctBattlefields: 1,
  distinctMainDeck: 1,
  distinctRuneDeck: 1,
  legendCardNo: "FAKE-LEGEND",
  mainDeck: 1,
  runeDeck: 1
});

const textRoundtrip = parseDeckImport(deckToImportText(defaultDeck));
assert.equal(textRoundtrip.ok, true);
assert.equal(textRoundtrip.format, "text");
assert.deepEqual(textRoundtrip.deck, defaultDeck);

const mixedText = parseDeckImport([
  "# local structure-only fixture",
  "传奇： FAKE-LEGEND",
  "英雄: FAKE-CHAMPION",
  "main:",
  "- 2 FAKE-MAIN-1",
  "- FAKE-MAIN-2 x 3",
  "runes: 1 FAKE-RUNE-1, 2x FAKE-RUNE-2",
  "battlefields:",
  "* FAKE-BF-1"
].join("\n"));
assert.equal(mixedText.ok, true);
assert.equal(mixedText.format, "text");
assert.deepEqual(mixedText.deck.mainDeck, [
  "FAKE-MAIN-1",
  "FAKE-MAIN-1",
  "FAKE-MAIN-2",
  "FAKE-MAIN-2",
  "FAKE-MAIN-2"
]);
assert.deepEqual(mixedText.deck.runeDeck, ["FAKE-RUNE-1", "FAKE-RUNE-2", "FAKE-RUNE-2"]);
assert.deepEqual(mixedText.deck.battlefields, ["FAKE-BF-1"]);

const emptyImport = parseDeckImport("   ");
assert.equal(emptyImport.ok, false);
assert.equal(emptyImport.issues[0].field, "format");

const invalidJson = parseDeckImport("{");
assert.equal(invalidJson.ok, false);
assert.equal(invalidJson.issues[0].field, "format");
assert.match(invalidJson.issues[0].message, /JSON 无法解析/);

const invalidStructure = parseDeckImport(JSON.stringify({
  cmdType: "PLAY_CARD",
  legendCardNo: "",
  championCardNo: "FAKE-CHAMPION",
  mainDeck: "FAKE-MAIN-1",
  runeDeck: [],
  battlefields: [""]
}));
assert.equal(invalidStructure.ok, false);
assert.deepEqual(
  invalidStructure.issues.map((issue) => issue.field).sort(),
  ["battlefields", "cmdType", "legendCardNo", "mainDeck", "runeDeck"].sort()
);

const invalidText = parseDeckImport([
  "legend: FAKE-LEGEND",
  "champion: FAKE-CHAMPION",
  "2 FAKE-MAIN-1",
  "main:",
  "0 FAKE-MAIN-2",
  "runes:",
  "FAKE-RUNE-1",
  "battlefields:",
  "FAKE-BF-1"
].join("\n"));
assert.equal(invalidText.ok, false);
assert.ok(invalidText.issues.some((issue) => issue.message.includes("不在 main/runes/battlefields 区段内")));
assert.ok(invalidText.issues.some((issue) => issue.message.includes("数量或编号无效")));

assert.deepEqual(parseStarterDeckOverride(serializeStarterDeck(defaultDeck)), defaultDeck);
assert.equal(parseStarterDeckOverride(JSON.stringify({ ...defaultDeck, cmdType: undefined })), undefined);
assert.equal(parseStarterDeckOverride(JSON.stringify({ ...defaultDeck, cmdType: "READY" })), undefined);

assert.ok(!starterDeckSource.includes("card-catalog"));
assert.ok(!starterDeckSource.includes("useCatalog"));
assert.ok(!starterDeckSource.includes("conformance"));

const decksPageSource = readFileSync(decksPageSourcePath, "utf8");
const deckImportFlowPlanSource = readFileSync(deckImportFlowPlanSourcePath, "utf8");
assert.ok(decksPageSource.includes("服务端权威"));
assert.ok(deckImportFlowPlanSource.includes("前端不判定卡牌数量、同名上限、颜色或规则合法性。"));
assert.ok(decksPageSource.includes("前端不本地判定卡组是否合法，只展示待提交内容。"));

console.log("Starter deck import contract check passed.");
