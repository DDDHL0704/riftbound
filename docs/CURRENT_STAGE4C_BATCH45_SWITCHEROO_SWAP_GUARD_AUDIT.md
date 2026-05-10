# Stage 4C-45 Switcheroo Swap Guard Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: Switcheroo / 换换乐 `SFD·145/221` / cardId `33237` / `FU-0b6332bbf0` / `SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS` ultra-narrow battlefield power-swap guard overlay.

- B added guard tests in `tests/Riftbound.ConformanceTests/SwitcherooGuardTests.cs`.
- B minimally changed `src/Riftbound.Engine/CoreRuleEngine.cs` to harden Switcheroo target guard.
- Covered: ordinary hand `PLAY_CARD` with two public battlefield unit targets -> stack / pass-pass -> this-turn power swap representative route.
- Covered invalid / dirty-resolution guards: non-public battlefield unit targets, including equipment / spell / rune / face-down standby / left-play targets, cannot enter stack or cannot create power mutation at resolution.
- Validation: A focused command `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Switcheroo|FullyQualifiedName~PowerSwap|FullyQualifiedName~Power"` passed 284/284. Backend full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 3594/3594; frontend build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed; Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed.

Remaining open: true LayerEngine, later modifier ordering, duration cleanup / EOT expiry, same-battlefield precision beyond current representative model, damage / battle math, full FAQ `SOUL-JFAQ-260114 p14`, 1009/811 full-official, final 18-step E2E.
