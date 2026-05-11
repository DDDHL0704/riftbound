# Stage 4C-50 Draven Keyword Unit Guard Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: Draven / 德莱文 `SFD·148/221` / cardId `33240` and `SFD·148a/221` / cardId `33241` / `FU-104211dbbc` keyword-unit combat text ordinary hand play-unit + `法盾` tag guard representative baseline.

- Covered only: ordinary hand `PLAY_CARD` with 0 targets -> stack / pass-pass -> base unit, power 6, tags `CARD_TYPE:UNIT` + `法盾`, two printings covered.
- Covered guard: invalid target, wrong zone-source, opponent source, face-down standby source, insufficient mana rejected with no mutation / no leak.
- Validation: focused 17/17 passed; backend full 3625/3625 passed; frontend build passed; Chrome smoke passed.

Holdback: this batch does not implement or claim battle / scoring runtime. Battle win scoring, destroyed-in-battle opponent scoring, Spellshield target tax, battle cleanup / score once-per-turn matrix, PaymentEngine, FAQ refs, 1009/811 full-official, and formal 18-step E2E remain open.
