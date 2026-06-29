import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const srcRoot = resolve(scriptDir, "../src");
const moduleCache = new Map();

const { buildMatchGuidancePlan } = loadTsModule(resolve(srcRoot, "utils/matchGuidancePlan.ts")).exports;

const offline = buildMatchGuidancePlan({
  connectionStatus: "idle",
  playerId: "alice",
  prompt: undefined,
  winnerPlayerId: undefined
});
assert.equal(offline.turnState, "offline");
assert.equal(offline.headline, "未连接服务端");
assert.deepEqual(offline.youCanLabels, []);

const reconnecting = buildMatchGuidancePlan({
  connectionStatus: "reconnecting",
  playerId: "alice",
  prompt: undefined
});
assert.equal(reconnecting.turnState, "offline");
assert.equal(reconnecting.headline, "正在重新连接…");

const yours = buildMatchGuidancePlan({
  connectionStatus: "connected",
  playerId: "alice",
  prompt: {
    playerId: "alice",
    actionable: true,
    reason: "当前玩家普通开环行动",
    actions: ["PLAY_CARD", "TAP_RUNE", "END_TURN", "SURRENDER", "WAIT"]
  },
  winnerPlayerId: ""
});
assert.equal(yours.turnState, "yours");
assert.equal(yours.headline, "轮到你了");
assert.equal(yours.detail, "当前玩家普通开环行动");
assert.equal(yours.tone, "good");
assert.deepEqual(yours.youCanLabels, ["打出卡牌", "横置符文", "结束回合"]);

const opponent = buildMatchGuidancePlan({
  connectionStatus: "connected",
  playerId: "alice",
  prompt: {
    playerId: "alice",
    actionable: false,
    reason: "等待对手行动",
    actions: ["WAIT", "SURRENDER"]
  }
});
assert.equal(opponent.turnState, "opponent");
assert.equal(opponent.headline, "等待对手行动");
assert.equal(opponent.detail, "等待对手行动");
assert.deepEqual(opponent.youCanLabels, []);

const won = buildMatchGuidancePlan({
  connectionStatus: "connected",
  playerId: "alice",
  prompt: undefined,
  winnerPlayerId: "alice"
});
assert.equal(won.turnState, "over");
assert.equal(won.tone, "good");
assert.ok(won.headline.includes("你赢了"));

const lost = buildMatchGuidancePlan({
  connectionStatus: "connected",
  playerId: "alice",
  prompt: undefined,
  winnerPlayerId: "bob"
});
assert.equal(lost.turnState, "over");
assert.equal(lost.tone, "bad");
assert.ok(lost.headline.includes("你输了"));

console.log("Match guidance plan check passed.");

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

    throw new Error(`Unexpected import in match guidance plan check: ${id}`);
  };

  new Function("exports", "module", "require", output)(module.exports, module, requireShim);
  return module;
}
