import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/components/match/WireObjectRefChips.tsx");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    esModuleInterop: true,
    jsx: ts.JsxEmit.ReactJSX,
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

function requireShim(id) {
  if (id === "react/jsx-runtime") {
    return {
      Fragment: Symbol("Fragment"),
      jsx: () => ({}),
      jsxs: () => ({})
    };
  }

  if (id === "../../utils/redaction") {
    return { redactInternalText: (value) => String(value) };
  }

  throw new Error(`Unexpected wire object ref chips import: ${id}`);
}

new Function("exports", "module", "require", output)(moduleShim.exports, moduleShim, requireShim);

const { uniqueWireObjectRefs, wireObjectLabel, wireObjectRef, wireObjectRefs } = moduleShim.exports;

const refs = uniqueWireObjectRefs([
  { id: "shared-object", role: "来源" },
  { id: "shared-object", role: "目标" },
  { id: "shared-object", role: "目标" },
  { id: "", role: "空" },
  { id: "HIDDEN", role: "来源" }
]);

assert.deepEqual(
  refs.map((ref) => `${ref.role}:${ref.id}`),
  ["来源:shared-object", "目标:shared-object", "来源:HIDDEN"],
  "object chips must dedupe by role and id, not by id alone"
);
assert.equal(wireObjectLabel("HIDDEN", {}), "隐藏对象");
assert.equal(wireObjectLabel("known", { known: { cardNo: "OGN-001/298" } }), "OGN-001/298");
assert.deepEqual(wireObjectRef("来源", " object-1 "), { id: "object-1", role: "来源" });
assert.deepEqual(wireObjectRefs("目标", ["a", "b"]), [{ id: "a", role: "目标" }, { id: "b", role: "目标" }]);

console.log("Wire object ref chips check passed.");
