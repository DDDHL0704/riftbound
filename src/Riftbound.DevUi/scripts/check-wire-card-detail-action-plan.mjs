import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildWireCardDetailActionPlan } = loadTsModule(resolve(srcRoot, "utils/wireCardDetailActionPlan.ts")).exports;

const sourceObjectId = "p1-hand-spell";
const playCandidate = {
  action: "PLAY_CARD",
  commandTemplate: {
    bindings: [
      { field: "sourceObjectId", required: true, roleLabel: "来源", source: "selectedSource" },
      { asArray: true, field: "targetObjectIds", required: false, roleLabel: "目标", source: "selectedTargets" }
    ],
    cmdType: "PLAY_CARD"
  },
  enabled: true,
  label: "打出手牌",
  reason: "需要目标",
  sources: [{ id: sourceObjectId, label: "手牌法术", objectIds: [sourceObjectId] }],
  targets: [{ id: "p2-unit-1", label: "敌方单位", objectIds: ["p2-unit-1"] }]
};
const tapCandidate = {
  action: "TAP_RUNE",
  enabled: true,
  label: "横置符文",
  reason: "可支付",
  sources: [{ id: sourceObjectId, label: "手牌法术", objectIds: [sourceObjectId] }]
};

const readyPlan = buildWireCardDetailActionPlan({
  canSubmitCommands: true,
  detailPlan: {
    actionCandidates: [playCandidate, tapCandidate],
    actionEmptyLabel: "当前服务端行动提示没有给这张牌可提交的操作。",
    sourceObjectId
  },
  disabledByConnection: false
});

assert.equal(readyPlan.sourceObjectId, sourceObjectId);
assert.equal(readyPlan.state, "ready");
assert.equal(readyPlan.stateLabel, "服务端候选可提交");
assert.equal(readyPlan.entries.length, 2);
assert.equal(readyPlan.entries.find((entry) => entry.candidate.action === "PLAY_CARD")?.mode, "composer");
assert.equal(readyPlan.entries.find((entry) => entry.candidate.action === "TAP_RUNE")?.mode, "button");
assert.equal(readyPlan.entries.find((entry) => entry.candidate.action === "TAP_RUNE")?.actionPlan.command?.cmdType, "TAP_RUNE");
assert.equal(new Set(readyPlan.entries.map((entry) => entry.key)).size, readyPlan.entries.length);

const readOnlyPlan = buildWireCardDetailActionPlan({
  canSubmitCommands: false,
  detailPlan: {
    actionCandidates: [playCandidate],
    actionEmptyLabel: "无候选。",
    sourceObjectId
  },
  disabledByConnection: false
});

assert.equal(readOnlyPlan.state, "readonly");
assert.equal(readOnlyPlan.stateLabel, "当前视图仅可查看");
assert.equal(readOnlyPlan.entries[0].mode, "button");
assert.equal(readOnlyPlan.entries[0].actionPlan.disabled, true);

const disconnectedPlan = buildWireCardDetailActionPlan({
  canSubmitCommands: true,
  detailPlan: {
    actionCandidates: [tapCandidate],
    actionEmptyLabel: "无候选。",
    sourceObjectId
  },
  disabledByConnection: true
});

assert.equal(disconnectedPlan.state, "readonly");
assert.equal(disconnectedPlan.stateLabel, "连接恢复前仅可查看");
assert.equal(disconnectedPlan.entries[0].actionPlan.disabled, true);

const emptyPlan = buildWireCardDetailActionPlan({
  canSubmitCommands: true,
  detailPlan: {
    actionCandidates: [],
    actionEmptyLabel: "隐藏对象不会展示或提交任何前端推断操作。",
    sourceObjectId: "hidden-card"
  },
  disabledByConnection: false
});

assert.equal(emptyPlan.state, "empty");
assert.equal(emptyPlan.stateLabel, "无服务端候选");
assert.equal(emptyPlan.emptyLabel, "隐藏对象不会展示或提交任何前端推断操作。");
assert.equal(emptyPlan.entries.length, 0);

console.log("Wire card detail action plan check passed.");

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

    throw new Error(`Unexpected import in card detail action plan check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}
