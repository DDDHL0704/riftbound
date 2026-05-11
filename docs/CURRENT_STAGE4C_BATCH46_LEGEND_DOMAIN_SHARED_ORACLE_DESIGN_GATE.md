# Stage 4C-46 Legend Domain / Shared Oracle Design Gate

Status: design gate only; project **NOT READY**; `fullOfficial=false`.

Decision: B / C / D / E read-only gates agree that Void Burrower / 虚空遁地兽 `SFD·187/221` / `FU-6e7d0dba2c` and Sett / 腕豪 `OGN·269/298` / `FU-6308c2db01` are **NO-GO for direct 4C-46 runtime implementation**.

This batch records the legend-domain / shared-oracle design gate. It does not add runtime implementation. A ran full checkpoint verification before commit: backend full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 3594/3594, frontend build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed, and Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed.

Current representative routes:

- Void Burrower: the service currently has a representative path that automatically handles conquest trigger, reveals top two, plays one, recycles the remainder, and makes the legend dormant.
- Sett: the service currently has a representative path that automatically replaces destruction, handles payment, recalls dormant, and readies on conquest.

Not officialized:

- Void Burrower official optional trigger / hidden reveal choice / shared oracle mapping.
- Sett official optional replacement / payment / boon consume / dormant recall cleanup.
- Shared legend-domain predicates and shared-oracle reprint mapping.

Remaining P0/P1:

- LegendActivePredicate.
- LegendOptionalTrigger.
- RevealChoice.
- ReplacementPayment.
- shared oracle reprint mapping.
- hidden redaction.
- `PAY_COST` / cleanup queue interactions.
- FAQ `SOUL-JFAQ-260114 p14` / `SOUL-OFAQ-260114 p4`.
- 1009/811 full-official coverage.
- final 18-step E2E.

Boundary: do not claim READY / READY-CANDIDATE. Do not use existing representative routes to close full legend trigger/action, hidden reveal / choice / recycle, replacement / payment, boon, dormant recall cleanup, or shared oracle official coverage.
