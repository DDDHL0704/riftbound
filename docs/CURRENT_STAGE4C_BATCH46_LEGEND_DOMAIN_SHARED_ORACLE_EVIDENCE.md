# Stage 4C-46 Legend Domain / Shared Oracle Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·187/221` / Void Burrower / 虚空遁地兽 / `FU-6e7d0dba2c`: when you conquer a battlefield, you may make this legend dormant to reveal the top two cards of your main deck, play one of them, and recycle the rest.
- `CATALOG` `OGN·269/298` / Sett / 腕豪 / `FU-6308c2db01`: whenever a buffed unit you control would be destroyed, you may pay `[A]` and make this legend dormant to consume the unit's boons and recall it dormant instead; when you conquer a battlefield, ready this legend.
- FAQ refs still require adjudication: `SOUL-JFAQ-260114 p14` and `SOUL-OFAQ-260114 p4`.

Current service evidence:

- Void Burrower has a service representative route that automatically handles conquest trigger, reveal top two, play one, recycle remainder, and dormant legend.
- Sett has service representative routes that automatically handle destroy replacement, payment, recall, and conquest ready. Prior Sett cleanup/payment plumbing appears in existing audit history, but it is not a full official shared-oracle closure.

Design blockers:

- LegendActivePredicate: what counts as active / dormant / controlled / eligible legend source across trigger and action surfaces.
- LegendOptionalTrigger: how optional legend triggers are offered, declined, hidden, and replayed without leaking information.
- RevealChoice: top-deck reveal, choose-one, free-play permission, recycle remainder, and redaction / replay boundaries.
- ReplacementPayment: replacement timing, payment quote / authorize / commit, decline behavior, and no-mutation rollback.
- shared oracle reprint mapping: how Void Burrower / Sett variants share or split official behavior by card entry, functional unit, and oracle/effect id.
- hidden redaction and `PAY_COST` / cleanup queue interactions.

Boundary: 4C-46 is a design gate, not implementation evidence. It does not close 1009/811 full-official, FAQ officialization, final 18-step E2E, or READY status.
