# Stage 4C-61 Overcharged Energy Field Unit Guard Audit

Status: **VERIFIED REPRESENTATIVE** direct-card-behavior guard baseline; project **NOT READY**; `fullOfficial=false`.

Scope: Overcharged Energy / 过载能量 `OGN·123/298` / cardId `31345` / `FU-b2e0e1d8da` / `OVERCHARGED_ENERGY_EXHAUST_ALL_FRIENDLY_DAMAGE_ALL_BATTLEFIELD_12` ordinary hand play / pay 7 / zero-target friendly public field-unit exhaust and public battlefield-unit damage guard.

- Verified coverage: ordinary hand `PLAY_CARD`, paying 7 mana, no explicit targets, stack / pass-pass resolution, friendly public field units exhausted, public battlefield units dealt 12 damage, and spell source moved to controller graveyard.
- Verified command guard: any explicit target rejects with `INVALID_TARGET` and no cost / stack / exhaust / damage mutation.
- Core repair: `CoreRuleEngine` now uses public unit enumerators for Overcharged Energy's friendly-unit exhaust and all-battlefield-unit damage effect paths, so equipment / spell / rune objects, face-down standby objects, dirty controller objects, and out-of-scope base units are not treated as battlefield damage recipients.
- Resolution exclusion evidence: friendly battlefield equipment / spell / rune, friendly face-down standby, and friendly dirty controller objects are not exhausted; battlefield equipment / spell / rune, face-down standby, and dirty controller objects are not damaged.
- Validation: focused Overcharged Energy guard filter passed 12/12; adjacent field-unit damage regression filter passed 53/53; backend full passed 3722/3722; frontend build passed; Chrome smoke passed.

Closure: 4C-61 closes only the Overcharged Energy representative field-unit guard described above. It is not a full-official card implementation or project READY signal.

Holdback: this batch does not implement or claim full-official Overcharged Energy. Damage prevention / replacement / cleanup and lethal-damage trigger interactions beyond representative guard assertions, formal multi-battlefield precision beyond current public battlefield unit filtering, standby / reaction and quick / spell-duel timing, full FEPR targeting / stack lifecycle, full PaymentEngine, LayerEngine / effective power and continuous-effect interactions, hidden-info / redaction matrix, FAQ adjudication if later evidence appears, 1009/811 full-official coverage, and formal 18-step E2E remain open.
