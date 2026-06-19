import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const sourcePath = resolve(scriptDir, "../src/components/match/wireCardFlowPlan.ts");
const source = readFileSync(sourcePath, "utf8");
const output = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022
  }
}).outputText;
const moduleShim = { exports: {} };

new Function("exports", "module", output)(moduleShim.exports, moduleShim);

const { buildWireCardFlowPlan } = moduleShim.exports;
const cardAspect = 744 / 1039;

for (const kind of ["battlefield-unit", "base", "hand", "signature"]) {
  for (const itemCount of [0, 1, 3, 5, 8, 12, 20, 40]) {
    const plan = buildWireCardFlowPlan({ itemCount, kind });
    assert.equal(plan.kind, kind);
    assert.equal(plan.itemCount, itemCount);
    assert.equal(plan.overflowCount, Math.max(0, plan.slotCount - plan.visibleSlotCount));
    assert.ok(plan.slotCount >= itemCount, `${kind} ${itemCount} slotCount must cover itemCount`);
    assert.ok(plan.visibleSlotCount <= plan.slotCount, `${kind} ${itemCount} visible slots must not exceed slot count`);
    assert.ok(plan.visibleSlotCount <= plan.scrollAfter, `${kind} ${itemCount} visible slots must honor scroll threshold`);
    assert.ok(plan.cardWidth > 0, `${kind} ${itemCount} cardWidth must be positive`);
    assert.ok(plan.cardHeight > 0, `${kind} ${itemCount} cardHeight must be positive`);
    assert.ok(Math.abs((plan.cardWidth / plan.cardHeight) - cardAspect) < 0.01, `${kind} ${itemCount} card ratio drifted`);
    if (kind === "signature") {
      assert.equal(plan.capacity, 1, "signature slots are fixed one-card rule zones");
      assert.equal(plan.scrollAfter, 1, "signature slots must declare one visible card");
    } else {
      assert.equal(plan.capacity, "unbounded", `${kind} must be modeled as an unbounded zone`);
      assert.equal(plan.layout, "rail", `${kind} should use the rail layout`);
    }
    if (plan.overflowCount > 0) {
      assert.equal(plan.overflow, "scroll", `${kind} ${itemCount} overflowing plan must scroll`);
      assert.equal(plan.fit, "overflow-rail", `${kind} ${itemCount} overflowing plan must use overflow fit`);
    } else {
      assert.equal(plan.overflow, "none", `${kind} ${itemCount} non-overflowing plan should not scroll`);
    }
  }
}

for (const kind of ["battlefield-unit", "base", "hand"]) {
  assertNonIncreasingWidths(kind, [0, 1, 3, 5, 8, 12, 20, 40]);
}

assert.deepEqual(
  buildWireCardFlowPlan({ itemCount: 4, kind: "battlefield-unit", minSlots: 0 }),
  buildWireCardFlowPlan({ itemCount: 4, kind: "battlefield-unit", minSlots: 0 }),
  "same kind/count must produce identical plan for every mirrored battlefield quadrant"
);

const emptyBattlefield = buildWireCardFlowPlan({ itemCount: 0, kind: "battlefield-unit", minSlots: 3 });
assert.equal(emptyBattlefield.slotCount, 3);
assert.equal(emptyBattlefield.visibleSlotCount, 3);
assert.equal(emptyBattlefield.overflow, "none");
assert.equal(emptyBattlefield.density, "sparse");
assert.equal(emptyBattlefield.cardWidth, 74);

const crowdedBattlefield = buildWireCardFlowPlan({ itemCount: 40, kind: "battlefield-unit" });
assert.equal(crowdedBattlefield.density, "packed");
assert.equal(crowdedBattlefield.layout, "rail");
assert.equal(crowdedBattlefield.cardWidth, 42);
assert.equal(crowdedBattlefield.slotCount, 40);
assert.equal(crowdedBattlefield.scrollAfter, 12);
assert.equal(crowdedBattlefield.visibleSlotCount, 12);
assert.equal(crowdedBattlefield.overflowCount, 28);
assert.equal(crowdedBattlefield.overflow, "scroll");

const crowdedBase = buildWireCardFlowPlan({ itemCount: 40, kind: "base" });
assert.equal(crowdedBase.density, "packed");
assert.equal(crowdedBase.cardWidth, 52);
assert.equal(crowdedBase.slotCount, 40);
assert.equal(crowdedBase.scrollAfter, 10);
assert.equal(crowdedBase.visibleSlotCount, 10);
assert.equal(crowdedBase.overflowCount, 30);
assert.equal(crowdedBase.overflow, "scroll");

const crowdedHand = buildWireCardFlowPlan({ itemCount: 40, kind: "hand" });
assert.equal(crowdedHand.density, "packed");
assert.equal(crowdedHand.cardWidth, 52);
assert.equal(crowdedHand.slotCount, 40);
assert.equal(crowdedHand.scrollAfter, 12);
assert.equal(crowdedHand.visibleSlotCount, 12);
assert.equal(crowdedHand.overflowCount, 28);
assert.equal(crowdedHand.overflow, "scroll");

const signature = buildWireCardFlowPlan({ itemCount: 1, kind: "signature" });
assert.equal(signature.density, "single");
assert.equal(signature.fit, "fixed-slot");
assert.equal(signature.layout, "grid");
assert.equal(signature.cardWidth, 100);
assert.equal(signature.visibleSlotCount, 1);
assert.equal(signature.overflow, "none");

const invalidSignatureOverflow = buildWireCardFlowPlan({ itemCount: 2, kind: "signature" });
assert.equal(invalidSignatureOverflow.capacity, 1);
assert.equal(invalidSignatureOverflow.overflowCount, 1, "signature overflow should remain explicit if backend emits invalid data");
assert.equal(invalidSignatureOverflow.overflow, "scroll");

console.log("Wire card flow plan check passed.");

function assertNonIncreasingWidths(kind, counts) {
  const widths = counts.map((itemCount) => buildWireCardFlowPlan({ itemCount, kind }).cardWidth);
  for (let index = 1; index < widths.length; index += 1) {
    assert.ok(
      widths[index] <= widths[index - 1],
      `${kind} card width should not increase as item count grows: ${widths.join(" -> ")}`
    );
  }
}
