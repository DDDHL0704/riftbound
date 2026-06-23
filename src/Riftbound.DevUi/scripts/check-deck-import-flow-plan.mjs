import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildDeckImportFlowPlan } = loadTsModule(resolve(srcRoot, "utils/deckImportFlowPlan.ts")).exports;
const { defaultStarterDeck, parseDeckImport, summarizeStarterDeck } = loadTsModule(resolve(srcRoot, "utils/starterDeck.ts")).exports;

const empty = buildDeckImportFlowPlan({
  importResult: undefined,
  previewSummary: undefined
});
assert.equal(empty.state, "empty");
assert.equal(empty.statusLabel, "等待粘贴");
assert.equal(empty.statusTone, "neutral");
assert.equal(empty.canApplyImport, false);
assert.equal(empty.feedbackIcon, "invalid");
assert.equal(empty.nextStep, "粘贴 JSON 或分区文本；前端只生成 SUBMIT_DECK 结构。");
assert.equal(empty.authorityBoundary, "前端不判定卡牌数量、同名上限、颜色或规则合法性。");
assert.deepEqual(empty.metrics.map((metric) => metric.label), ["主牌堆", "符文", "战场", "格式"]);
assert.ok(empty.issueRows.some((row) => row.message.includes("粘贴后会在这里显示结构校验结果")));

const invalidResult = parseDeckImport("{");
const invalid = buildDeckImportFlowPlan({
  importResult: invalidResult,
  previewSummary: undefined
});
assert.equal(invalid.state, "invalid");
assert.equal(invalid.statusLabel, "结构无效");
assert.equal(invalid.statusTone, "bad");
assert.equal(invalid.canApplyImport, false);
assert.equal(invalid.feedbackIcon, "invalid");
assert.equal(invalid.nextStep, "修正结构错误；服务端合法性仍未验证。");
assert.ok(invalid.issueRows.some((row) => row.field === "format" && row.message.includes("JSON 无法解析")));
assert.deepEqual(invalid.metrics.map((metric) => metric.value), ["-", "-", "-", "-"]);

const parsed = parseDeckImport(JSON.stringify(defaultStarterDeck()));
assert.equal(parsed.ok, true);
const valid = buildDeckImportFlowPlan({
  importResult: parsed,
  previewSummary: summarizeStarterDeck(parsed.deck)
});
assert.equal(valid.state, "valid");
assert.equal(valid.statusLabel, "结构可导入");
assert.equal(valid.statusTone, "good");
assert.equal(valid.canApplyImport, true);
assert.equal(valid.feedbackIcon, "valid");
assert.equal(valid.nextStep, "导入为当前构筑后，到房间提交给服务端权威验证。");
assert.equal(valid.issueRows.length, 0);
assert.deepEqual(valid.metrics.map((metric) => metric.value), ["40 张 / 14 种", "12 张 / 12 种", "3 张 / 3 种", "JSON"]);

console.log("Deck import flow plan check passed.");

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

    throw new Error(`Unexpected import in deck import flow plan check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}
