# Stage 4C-44 Akshan Play Guard Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: Akshan / 阿克尚 `SFD·109/221` / cardId `33194` / `FU-7419ee7d9d` / `AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT` ultra-narrow ordinary hand play-unit guard representative baseline.

- B added guard tests in `tests/Riftbound.ConformanceTests/AkshanGuardTests.cs`.
- Covered: ordinary hand `PLAY_CARD` with 0 targets -> stack / pass-pass -> base unit, power 4, tags `CARD_TYPE:UNIT` + `哨兵` + `百炼`.
- Covered: no optional assemble and no orange-orange extra cost in this representative route.
- Covered invalid guards with no mutation / no leak: explicit target, wrong zone / source, opponent source, face-down standby source, insufficient mana.
- Validation: focused command `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Akshan|FullyQualifiedName~PlayUnit|FullyQualifiedName~KeywordUnit|FullyQualifiedName~Assemble"` passed for both B and A; A result 189/189. Backend full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 3582/3582; frontend build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed; Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed.

Remaining open: optional assemble, orange-orange extra play, enemy equipment move / control, weapon attach, control-until-leaves cleanup, LayerEngine / continuous effects, FAQ full behavior, 1009/811 full-official, final 18-step E2E.
