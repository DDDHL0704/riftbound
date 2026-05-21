# 4D-03IP-E Immortal Phoenix Keyword Unit FAQ Blocker Closure Audit

Stage: 4D-03IP-E

Scope:

- Matrix-only E_CARD_MATRIX_READINESS blocker disposition for `FU-fb6b5139e7`.
- Selected card: `OGN·037/298` 不朽凤凰.
- Selected effect: `IMMORTAL_PHOENIX_PLAY_KEYWORD_UNIT`.
- No runtime, frontend, Chrome-script, protocol, official catalog, `fullOfficial` or READY change.

Evidence:

- Existing P2 keyword-unit fixture records payment, zero-target stack play, source-unit entry, `灵体` and `强攻2` tags.
- `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` bind the fixture to catalog and core rule evidence.
- 03IP prevalidation confirmed focused keyword-unit and adjacent prompt paths remain green.

Matrix transition:

- `NEEDS_ENGINE_SUPPORT` removed from the selected functional unit and one snapshot entry.
- Functional-unit freeze status remains `NEEDS_FAQ_REVIEW`.
- `NEEDS_FAQ_REVIEW` and `NEEDS_AUTOMATED_TEST_EVIDENCE` remain blockers.
- `fullOfficial=false` and `ready=false` remain unchanged.

Validation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ImmortalPhoenix|FullyQualifiedName~Phoenix|FullyQualifiedName~KeywordUnit"` passed 86/86.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~ImmortalPhoenix|FullyQualifiedName~Phoenix|FullyQualifiedName~KeywordUnit"` passed 297/297.

Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; `ImmortalPhoenix|Phoenix|KeywordUnit` focused regression 89/89 passed; `ActionPrompt|Prompt|ImmortalPhoenix|Phoenix|KeywordUnit` adjacent regression 300/300 passed; `PaymentEngineCoverageAuditTests` 466/466 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5037/5037 passed; `git diff --check` passed.

Open blockers:

- Immortal Phoenix automated evidence disposition remains open.
- Immortal Phoenix FAQ adjudication remains open.
- Complete discard/recycle play-from-discard breadth remains open.
- Complete Assault battle damage modifier matrix remains open.
- Complete Spirit / hidden-info / random-zone visibility matrix remains open.
- Complete cleanup / replacement duration interactions remain open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix and READY remain open.
