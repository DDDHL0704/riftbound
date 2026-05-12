# Stage 4C-58 Spirit Fire Total Power Target Guard Audit

Status: **VERIFIED REPRESENTATIVE** direct-card-behavior guard baseline; project **NOT READY**; `fullOfficial=false`.

Scope: Spirit Fire / 妖异狐火 `OGN·256/298` / cardId `31498` / `FU-a9dc3495e1` / `SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4` ordinary hand play / pay 3 / public battlefield unit targets / total printed power <= 4 representative guard.

- Verified coverage: ordinary hand `PLAY_CARD`, paying 3 mana, selecting public battlefield unit targets with total target power no more than 4, stack / pass-pass resolution, selected units destroyed to owner graveyard, and spell source moved to controller graveyard.
- Verified command guard: total target power above 4, battlefield equipment / spell / rune objects, face-down standby objects, stale objects, base units, hand cards, and dirty controller targets reject with `INVALID_TARGET` and no cost / stack / destroy mutation.
- Core repair: `CoreRuleEngine` now applies a Spirit Fire effect-specific guard so the default `BattlefieldUnit` scope cannot accept non-unit battlefield objects for this effect.
- Prompt parity repair: `MatchSession` now filters Spirit Fire `targetChoicesByIndex` and `legalTargetSelections` through the same public battlefield unit / controller-consistency guard while preserving total-power filtering in server legal selections.
- Validation: focused Spirit Fire / TargetGuard / Spellshield filter passed 48/48; ActionPrompt / Prompt / SpiritFire regression filter passed 112/112; backend full passed 3690/3690; frontend build passed; Chrome smoke passed.

Closure: 4C-58 closes only the Spirit Fire representative total-power target guard described above. It is not a full-official card implementation or project READY signal.

Holdback: this batch does not implement or claim full-official Spirit Fire. Same-battlefield precision, standby / reaction and quick / spell-duel timing, full FEPR targeting / stack lifecycle, full destroy / cleanup / replacement / prevention / Last Breath interactions, full Spellshield tax matrix, full PaymentEngine, LayerEngine / effective power, hidden-info / redaction matrix, FAQ adjudication, 1009/811 full-official coverage, and formal 18-step E2E remain open.
