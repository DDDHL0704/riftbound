# Stage 4C-62 Hunt Ready Friendly Units Guard Audit

Status: **VERIFIED REPRESENTATIVE** direct-card-behavior guard baseline; project **NOT READY**; `fullOfficial=false`.

Scope: Hunt / 狩猎 `SFD·204/221` / cardId `33303` / `FU-f877e60407` / `HUNT_READY_ALL_FRIENDLY_UNITS` ordinary hand play / pay 1 / zero-target friendly public field-unit ready guard.

- Verified coverage: ordinary hand `PLAY_CARD`, paying 1 mana, no explicit targets, stack / pass-pass resolution, friendly public field units readied, and spell source moved to controller graveyard.
- Verified command guard: any explicit target rejects with `INVALID_TARGET` and no cost / stack / ready mutation.
- Core repair: `CoreRuleEngine` now uses the public field-unit enumerator for all-friendly ready effect paths, so friendly battlefield equipment / spell / rune objects, face-down standby objects, dirty controller objects, and enemy units are not readied by Hunt.
- Resolution exclusion evidence: friendly battlefield equipment / spell / rune, friendly face-down standby, friendly dirty controller objects, and enemy battlefield units remain exhausted.
- Validation: focused Hunt guard filter passed 10/10; adjacent ready / field-unit regression filter passed 121/121; backend full passed 3731/3731; frontend build passed; Chrome smoke passed.

Closure: 4C-62 closes only the Hunt representative ready-all-friendly-units guard described above. It is not a full-official card implementation or project READY signal.

Holdback: this batch does not implement or claim full-official Hunt. Formal multi-battlefield precision beyond current public field-unit filtering, standby / reaction and quick / spell-duel timing, full FEPR targeting / stack lifecycle, full PaymentEngine, readiness duration / replacement / prevention interactions outside the representative ready set, LayerEngine / effective power and continuous-effect interactions, hidden-info / redaction matrix, FAQ adjudication if later evidence appears, 1009/811 full-official coverage, and formal 18-step E2E remain open.
