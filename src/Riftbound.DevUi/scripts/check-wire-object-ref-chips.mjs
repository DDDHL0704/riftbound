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

const {
  uniqueWireObjectRefs,
  wireObjectLabel,
  wireObjectRef,
  wireObjectRefRenderPlan,
  wireObjectRefs
} = moduleShim.exports;

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

const visiblePlan = wireObjectRefRenderPlan({
  objects: { known: { cardNo: "OGN-001/298" } },
  onInspectObject: () => {},
  ref: { id: "known", role: "目标" },
  selectedObjectId: "known"
});
assert.deepEqual(
  visiblePlan,
  {
    canInspect: true,
    dataObjectId: "known",
    label: "目标 OGN-001/298",
    selected: true,
    visibility: "visible"
  },
  "visible refs must remain inspectable and selected when the object exists"
);

const hiddenPlan = wireObjectRefRenderPlan({
  objects: { "secret-object-id": { cardNo: "OGN-999/298" } },
  onInspectObject: () => {},
  ref: { id: "secret-object-id", label: "OGN-999/298", role: "来源", visibility: "hidden" },
  selectedObjectId: "secret-object-id"
});
assert.deepEqual(
  hiddenPlan,
  {
    canInspect: false,
    dataObjectId: "HIDDEN",
    label: "来源 隐藏对象",
    selected: false,
    visibility: "hidden"
  },
  "hidden refs must not expose real ids, labels, selection state, or inspect affordances"
);

const missingPlan = wireObjectRefRenderPlan({
  objects: {},
  onInspectObject: () => {},
  ref: { id: "missing-object", role: "目标" },
  selectedObjectId: "missing-object"
});
assert.equal(missingPlan.canInspect, false);
assert.equal(missingPlan.dataObjectId, "missing-object");
assert.equal(missingPlan.label, "目标 服务端对象");
assert.equal(missingPlan.selected, true);
assert.equal(missingPlan.visibility, "missing");

console.log("Wire object ref chips check passed.");
