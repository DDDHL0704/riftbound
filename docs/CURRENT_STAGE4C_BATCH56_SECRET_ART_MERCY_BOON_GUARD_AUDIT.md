# Stage 4C-56 Secret Art! Mercy Boon Guard Audit

Status: **VERIFIED REPRESENTATIVE** test-only baseline; project **NOT READY**; `fullOfficial=false`.

Scope: Secret Art! Mercy / 秘奥义！慈悲度魂落 `OGN·053/298` / cardId `31265` / `FU-3461727400` / `SECRET_ART_MERCY_GRANT_BOON_NO_GLOBAL_BONUS` ordinary hand play / pay 3 / friendly unit grant Boon +1 / friendly Spellshield no-tax representative guard.

- Verified coverage: ordinary hand `PLAY_CARD` selecting a friendly public field unit, paying 3 mana, stack / pass-pass resolution, `OBJECT_TAG_ADDED` `增益`, and `BOON_GRANTED` permanent +1.
- Verified Spellshield boundary: friendly Spellshield target pays only base cost and records `spellshieldTaxMana = 0` / no tax.
- Verified invalid-target guard: friendly equipment, friendly spell, friendly rune, stale unit, face-down standby, enemy unit, invalid source, insufficient mana, and already-booned duplicate power/tag cases reject or no-op without unintended mutation.
- Core repair: `CoreRuleEngine.IsTargetObjectInScope` now routes `FriendlyUnit`, `FriendlyUnitThenFriendlyUnit`, first `FriendlyThenEnemyUnits`, and first `FriendlyThenEnemyBattlefieldUnits` through `IsPlayerControlledFieldUnitObject` / `IsVisibleFieldUnitObject`.
- Prompt parity repair: `MatchSession.IsPromptFieldUnitObject` now matches the Core visible-field-unit definition and allows legacy custom-tag public field units while excluding equipment, spell, rune, standby, and face-down objects.
- Validation: focused SecretArtMercy / Boon / Spellshield filter passed 87/87; prompt / Sand Soldiers / FriendlyUnit regression filter passed 133/133; backend full passed 3668/3668; frontend build passed; Chrome smoke passed.

Closure: 4C-56 closes only the Secret Art! Mercy representative guard described above. It is not a full-official card implementation or project READY signal.

Holdback: this batch does not implement or claim full-official Secret Art! Mercy. Standby / reaction, quick / spell-duel breadth, global all-boons extra +1 this turn, LayerEngine / duration cleanup, already-has-boon / stacking semantics unless B covers only a narrow edge, full target matrix, full Spellshield tax, PaymentEngine, FAQ adjudication, 1009/811 full-official, and formal 18-step E2E remain open.
