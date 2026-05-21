4D-03IV-E payment-cost Rage Sigil play-equipment targeting-stack blocker closure candidate.

This slice records the next E_CARD_MATRIX_READINESS row-level blocker reduction after 4D-03IU-E. It selects functionalUnit=FU-8a0d6d8013, cards=OGN·040/298 and SFD·222/221 暴怒之印, effect=OGN_RAGE_SIGIL_PLAY_EQUIPMENT;RAGE_SIGIL_PLAY_EQUIPMENT.

Matrix impact:

- payment-cost functionalUnits=360.
- payment-cost snapshotEntries=446.
- NEEDS_ENGINE_SUPPORT 271 -> 270.
- primary residual 177 -> 177.
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 459 -> 458.
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 173 -> 172.
- NEEDS_AUTOMATED_TEST_EVIDENCE residual=328.
- NEEDS_FAQ_REVIEW residual=92.
- primary NEEDS_FAQ_REVIEW residual=61.
- fullOfficialTrue=0.
- ready=false.

This is not automated-evidence disposition, full PaymentEngine closure, E_CARD_MATRIX_READINESS closure, card matrix closure, fullOfficial upgrade or READY. Runtime, frontend, Chrome/browser scripts, formal 18-step scripts, official catalog, protocol core fields and riftbound-dotnet.sln remain locked.

Prevalidation passed: RageSigil/Sigil/ActivateAbility/ResourceSkill/PaymentEngineUnificationTests/RunePool focused regression 425/425 passed; ActionPrompt/Prompt/RageSigil/Sigil/ActivateAbility/ResourceSkill/PaymentResource/SpendPower/RunePool adjacent regression 624/624 passed.

Final validation passed: jq empty passed; RageSigil/Sigil/ActivateAbility/ResourceSkill/PaymentEngineUnificationTests/RunePool focused regression 428/428 passed; ActionPrompt/Prompt/RageSigil/Sigil/ActivateAbility/ResourceSkill/PaymentResource/SpendPower/RunePool adjacent regression 627/627 passed; PaymentEngineCoverageAuditTests 478/478 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5049/5049 passed; git diff --check passed.
