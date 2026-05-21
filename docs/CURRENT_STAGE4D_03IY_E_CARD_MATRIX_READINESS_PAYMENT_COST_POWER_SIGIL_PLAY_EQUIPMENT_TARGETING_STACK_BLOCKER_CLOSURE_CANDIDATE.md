4D-03IY-E payment-cost Power Sigil play-equipment targeting-stack blocker closure candidate.

This slice records the next E_CARD_MATRIX_READINESS row-level blocker reduction after 4D-03IX-E. It selects functionalUnit=FU-73e97a749b, cards=OGN·163/298 and SFD·231/221 力量之印, effect=OGN_POWER_SIGIL_PLAY_EQUIPMENT;POWER_SIGIL_PLAY_EQUIPMENT.

Scope:

- Matrix-only blocker reduction for one shared-oracle play-equipment representative.
- Runtime, frontend, Chrome/browser scripts, formal 18-step scripts, official catalog, protocol core fields, fullOfficial status and final readiness flags remain locked.
- The selected row keeps freezeStatus=SHARED_ORACLE_IMPLEMENTATION and fullOfficial=false.

Count continuity:

- snapshotEntries=1009.
- functionalUnits=811.
- payment-cost functionalUnits=360.
- payment-cost snapshotEntries=446.
- NEEDS_ENGINE_SUPPORT 268 -> 267.
- primary residual 177 -> 177.
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 456 -> 455.
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 170 -> 169.
- NEEDS_AUTOMATED_TEST_EVIDENCE residual=328.
- NEEDS_FAQ_REVIEW residual=92.
- primary NEEDS_FAQ_REVIEW residual=61.
- fullOfficialTrue=0.
- ready=false.
- fullOfficialTrue 0 -> 0.
- ready false -> false.

Non-closure:

- Power Sigil automated evidence disposition remains open.
- Complete typed resource skill family matrix remains open.
- Complete equipment activated skill matrix remains open.
- Complete FEPR target / stack lifecycle matrix remains open.
- Full PaymentEngine / PAY_COST matrix remains open.
- Card matrix remains open.
- READY remains open.

Prevalidation passed: PowerSigil/Sigil/ActivateAbility/ResourceSkill/PaymentEngineUnificationTests/RunePool focused regression 434/434 passed; ActionPrompt/Prompt/PowerSigil/Sigil/ActivateAbility/ResourceSkill/PaymentResource/SpendPower/RunePool adjacent regression 633/633 passed.

Final validation passed: jq empty passed; PowerSigil/Sigil/ActivateAbility/ResourceSkill/PaymentEngineUnificationTests/RunePool focused regression 434/434 passed; ActionPrompt/Prompt/PowerSigil/Sigil/ActivateAbility/ResourceSkill/PaymentResource/SpendPower/RunePool adjacent regression 633/633 passed; PaymentEngineCoverageAuditTests 484/484 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5055/5055 passed; git diff --check passed.
