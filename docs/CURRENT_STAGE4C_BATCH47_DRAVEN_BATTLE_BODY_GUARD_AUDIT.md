# Stage 4C-47 Draven Battle Body Guard Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: Draven / 德莱文 `SFD·020/221` / cardId `33092` and `SFD·020a/221` / cardId `33093` / `FU-964b214448` battle body / play-unit guard representative slice.

- B added guard tests in `tests/Riftbound.ConformanceTests/DravenVanillaGuardTests.cs`.
- Core / frontend / protocol unchanged.
- Covered: ordinary hand `PLAY_CARD` with 0 targets -> stack / pass-pass -> base unit, power 4, tag `CARD_TYPE:UNIT`.
- Covered invalid guards with no mutation / no leak: invalid target, wrong zone, opponent source, face-down standby source, insufficient mana.
- Validation: A/B focused command `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Draven|FullyQualifiedName~SFD020|FullyQualifiedName~VanillaPlayUnit|FullyQualifiedName~PlayUnit"` passed 14/14; backend full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 3601/3601; frontend build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed; Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed.

Remaining open: battle win dormant Gold, attack / defense optional red payment, +2 until EOT, full PaymentEngine, Layer / duration cleanup, FAQ refs, 1009/811 full-official, final 18-step E2E.
