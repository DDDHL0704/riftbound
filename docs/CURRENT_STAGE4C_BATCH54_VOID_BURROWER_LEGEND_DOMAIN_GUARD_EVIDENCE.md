# Stage 4C-54 Void Burrower Legend Domain Guard Evidence

Representative automated evidence only; project **NOT READY**; `fullOfficial=false`; full-official remains **NO-GO**.

Official anchors:

- `CATALOG` `SFD·187/221` / cardId `33285` / Void Burrower / 虚空遁地兽 / `FU-6e7d0dba2c`: when you conquer a battlefield, you may make this legend dormant to reveal the top two cards of your main deck; you may play one of those cards and recycle the rest.
- `CATALOG` `SFD·243/221` / cardId `33354`: same official text as `SFD·187/221`.
- Existing FAQ refs remain open: `SOUL-JFAQ-260114 p14` and `SOUL-OFAQ-260114 p4`.

Implementation evidence anchors:

- Existing automated reveal / play / recycle evidence: `P79LegendTriggerReksaiRevealsTopTwoPlaysUnitAndRecyclesRestOnConquer`.
- Existing automated no-unit recycle evidence: `P79LegendTriggerReksaiRecyclesBothRevealedCardsWhenNoTopUnit`.
- Existing active legend guard evidence: `P79LegendTriggerReksaiRequiresActiveLegend`.
- Validation numbers: focused 32/32 passed; backend full 3650/3650 passed; frontend build passed; Chrome smoke passed.

Boundary: closes only Void Burrower `FU-6e7d0dba2c` representative automated evidence gap. It does not close direct runtime implementation, full-official NO-GO, LegendActivePredicate, LegendOptionalTrigger, RevealChoice, shared oracle mapping, hidden / reveal redaction matrix, optional trigger prompt / decline, free-play official semantics, recycle remainder official semantics, unit destination / zone ownership details, `ORDER_TRIGGERS` / battle lifecycle full matrix, FAQ adjudication, 1009/811 full-official, or formal E2E.
