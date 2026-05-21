# 4D-03IK-E Akshan No Optional Assemble Orange Extra FAQ Blocker Closure Audit

Stage: 4D-03IK-E

Scope: E_CARD_MATRIX_READINESS row-level blocker reduction for `SFD·109/221` 阿克尚 / `FU-7419ee7d9d` / `AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT`.

Evidence basis:

- Existing Stage 4C-44 Akshan play guard evidence.
- Existing Stage 4D-04F Akshan orange-extra equipment steal representative evidence.
- Existing catalog fixture coverage for the representative Akshan no-optional-assemble route.
- Existing rules evidence index and P2 rules preflight rows for Akshan.
- Focused Akshan / PlayUnit / KeywordUnit / Assemble regression and adjacent ActionPrompt / Reveal / Hide regression were run before the matrix transition.

Accepted matrix transition:

- `stage4B.freezeStatus`: `NEEDS_FAQ_REVIEW -> NEEDS_FAQ_REVIEW`.
- `stage4B.statusFlags`: remove `NEEDS_ENGINE_SUPPORT`; keep `IMPLEMENTED_UNTESTED` and `NEEDS_FAQ_REVIEW`.
- `stage4B.fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT+NEEDS_FAQ_REVIEW+NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_FAQ_REVIEW+NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false` remains unchanged.
- `ready=false` remains unchanged.

Prevalidation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Akshan|FullyQualifiedName~PlayUnit|FullyQualifiedName~KeywordUnit|FullyQualifiedName~Assemble"` passed 246/246.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~HideCard|FullyQualifiedName~RevealCard|FullyQualifiedName~Akshan"` passed 310/310.

Final validation passed: jq empty passed; Akshan/PlayUnit/KeywordUnit/Assemble focused regression 248/248 passed; ActionPrompt/Prompt/HideCard/RevealCard/Akshan focused regression 313/313 passed; PaymentEngineCoverageAuditTests 456/456 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5027/5027 passed; git diff --check passed.

Holdbacks: Akshan automated evidence disposition, Akshan FAQ adjudication, complete optional assemble matrix, orange-extra enemy equipment move / control matrix, weapon attach and control-until-leaves cleanup matrix, LayerEngine / continuous effects matrix, movement / control-zone matrix, FEPR target / stack lifecycle, full PaymentEngine / PAY_COST matrix, 1009/811 full-official coverage, formal 18-step E2E, and READY remain open.
