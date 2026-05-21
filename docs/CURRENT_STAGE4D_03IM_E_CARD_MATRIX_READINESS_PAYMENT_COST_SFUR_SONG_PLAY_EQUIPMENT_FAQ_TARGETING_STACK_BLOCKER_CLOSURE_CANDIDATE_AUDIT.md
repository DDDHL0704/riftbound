# 4D-03IM-E Sfur Song Play Equipment FAQ Blocker Closure Audit

Stage: 4D-03IM-E

Scope: E_CARD_MATRIX_READINESS row-level blocker reduction for `SFD·059/221` 斯弗尔尚歌 / `FU-9a623b3185` / `SFUR_SONG_PLAY_EQUIPMENT`.

Evidence basis:

- Existing Stage 4C-43 Sfur Song play-equipment guard evidence.
- Existing catalog fixture coverage for the representative Sfur Song play-equipment route.
- Existing target-rejection fixture coverage for the Sfur Song invalid-target path.
- Existing rules evidence index and P2 rules preflight rows for Sfur Song.
- Focused SfurSong / Equipment / PlayEquipment regression and adjacent ActionPrompt / Reveal / Hide regression were run before the matrix transition.

Accepted matrix transition:

- `stage4B.freezeStatus`: `NEEDS_FAQ_REVIEW -> NEEDS_FAQ_REVIEW`.
- `stage4B.statusFlags`: remove `NEEDS_ENGINE_SUPPORT`; keep `IMPLEMENTED_UNTESTED` and `NEEDS_FAQ_REVIEW`.
- `stage4B.fullOfficialBlockers`: `NEEDS_ENGINE_SUPPORT+NEEDS_FAQ_REVIEW+NEEDS_AUTOMATED_TEST_EVIDENCE -> NEEDS_FAQ_REVIEW+NEEDS_AUTOMATED_TEST_EVIDENCE`.
- `fullOfficial=false` remains unchanged.
- `ready=false` remains unchanged.

Prevalidation:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SfurSong|FullyQualifiedName~Equipment|FullyQualifiedName~PlayEquipment"` passed 357/357.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~HideCard|FullyQualifiedName~RevealCard|FullyQualifiedName~SfurSong"` passed 294/294.

Final validation passed: jq empty passed; SfurSong/Equipment/PlayEquipment focused regression 359/359 passed; ActionPrompt/Prompt/HideCard/RevealCard/SfurSong focused regression 297/297 passed; PaymentEngineCoverageAuditTests 460/460 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5031/5031 passed; git diff --check passed.

Holdbacks: Sfur Song automated evidence disposition, Sfur Song FAQ adjudication, copied host skill text, continuous text / LayerEngine semantics, complete assemble / equipment attach lifecycle, equipment control / zone movement matrix, FEPR target / stack lifecycle, full PaymentEngine / PAY_COST matrix, 1009/811 full-official coverage, formal 18-step E2E, and READY remain open.
