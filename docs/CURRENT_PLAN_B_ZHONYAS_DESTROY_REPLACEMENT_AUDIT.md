# Plan B Zhonya's Hourglass Destroy Replacement Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: `OGN·077/298` / cardId `31291` / `FU-fb79eea7fc` / Zhonya's Hourglass / 中娅沙漏. This batch implements the public equipment replacement text: when a friendly unit would be destroyed, destroy the source equipment instead and recall that unit to base exhausted.

Official authority:

- `data/official/card-catalog.zh-CN.json`: `OGN·077/298` official card text says the next time a friendly unit is destroyed, destroy this card instead and recall that unit exhausted.
- `CORE-260330` p39-p42 rules 355-356: stack and resolution frame used by the ordinary equipment play path.
- `CORE-260330` p4-p8 rules 107-129: public / hidden information boundaries; face-down standby source identity must not be used as a public replacement source.
- `CORE-260330` p92-p105 keyword rules 800+: standby remains an open adjacent surface and is not completed by this batch.

Implementation:

- `BehaviorSpec.Replacements` now parses Zhonya's official text as `FRIENDLY_UNIT_DESTROYED_DESTROY_SOURCE_RECALL_EXHAUSTED`.
- Shared engine helper `CardReplacementSpecRules` builds replacement specs from official catalog `BehaviorSpec`, matching the existing static-ability catalog route.
- `CoreRuleEngine` state-based cleanup now reads the replacement spec from public field equipment controlled by the destroyed unit's controller. It destroys the source equipment and recalls the friendly unit to that controller's base exhausted.
- Hidden boundary: face-down standby and opponent-controlled equipment sources are ignored; no hidden card number is read for replacement application.

Representative coverage:

- Positive runtime: visible P1 Zhonya in base replaces lethal battle cleanup for a P1 unit, destroys the source equipment to graveyard, recalls the unit to base exhausted, clears damage, and emits `UNIT_RECALLED_TO_BASE` with the replacement effect id.
- Negative runtime: face-down standby Zhonya and opponent Zhonya do not apply; the friendly unit is destroyed normally and the hidden source remains unrevealed.
- Catalog guard: BehaviorSpec parser exposes the replacement kind and applies-to surface.
- Source guard: engine implementation does not use `OGN·077/298` or Zhonya-specific card-number checks outside the existing play-card behavior registry.

Validation:

- Baseline before changes: backend conformance 9183/9183 passed.
- Focused parser/runtime/source guard: 12/12 passed.
- Adjacent replacement/catalog/recovery: 2398/2398 passed.
- Backend full after implementation: 9187/9187 passed.

Remaining open:

- Full standby / reaction timing for Zhonya.
- Multiple simultaneous replacement effects and player choice ordering.
- Full equipment attach/detach/layer breadth.
- FAQ-specific adjudication for all Zhonya references.
- 1009/811 full-official matrix, formal 18-step E2E, and final READY.
