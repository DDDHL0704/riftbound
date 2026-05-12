# Stage 4C-60 Firestorm Enemy Battlefield Damage Guard Audit

Status: **VERIFIED REPRESENTATIVE** direct-card-behavior guard baseline; project **NOT READY**; `fullOfficial=false`.

Scope: Firestorm / 烈火风暴 `OGS·002/024` / cardId `31581` / `FU-fe9dbeea3d` / `FIRESTORM_DAMAGE_ALL_ENEMY_BATTLEFIELD_UNITS_3` ordinary hand play / pay 6 / zero-target enemy public battlefield unit damage guard.

- Verified coverage: ordinary hand `PLAY_CARD`, paying 6 mana, no explicit targets, stack / pass-pass resolution, 3 damage applied to each enemy public battlefield unit, and spell source moved to controller graveyard.
- Verified command guard: any explicit target rejects with `INVALID_TARGET` and no cost / stack / damage mutation.
- Core repair: `CoreRuleEngine` now uses an enemy battlefield unit enumerator for enemy battlefield unit damage resolution, so battlefield equipment / spell / rune objects, face-down standby objects, and dirty controller objects are not damaged by this effect path.
- Resolution exclusion evidence: friendly battlefield units and enemy base units remain outside Firestorm's damage set; enemy battlefield equipment / spell / rune, face-down standby, and dirty controller battlefield unit-shaped objects remain undamaged.
- Validation: focused Firestorm guard filter passed 13/13; enemy battlefield damage regression filter passed 36/36; backend full passed 3711/3711; frontend build passed; Chrome smoke passed.

Closure: 4C-60 closes only the Firestorm representative enemy battlefield unit damage guard described above. It is not a full-official card implementation or project READY signal.

Holdback: this batch does not implement or claim full-official Firestorm. Damage prevention / replacement / cleanup and lethal-damage trigger interactions beyond the representative nonlethal damage set, formal multi-battlefield precision beyond current public battlefield unit filtering, standby / reaction and quick / spell-duel timing, full FEPR targeting / stack lifecycle, full PaymentEngine, LayerEngine / effective power and continuous-effect interactions, hidden-info / redaction matrix, FAQ adjudication if later evidence appears, 1009/811 full-official coverage, and formal 18-step E2E remain open.
