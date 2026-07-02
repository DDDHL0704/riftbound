# Plan B Sigil Typed Resource Activated Ability Spec Audit

Date: 2026-07-02

Project status: **NOT READY**.

## Scope

This slice migrates the existing SFD / OGN Sigil typed-resource activated ability profile source from a hand-written `P4SigilTypedResourceProfile[]` table to structured `BehaviorSpec.ActivatedAbilities`.

Runtime behavior is intentionally unchanged:

- existing ability ids and effect kinds remain compatible;
- existing payment-only temporary resource restrictions remain compatible;
- prompt, command, audit, recovery, replay, and hidden-information behavior are not broadened;
- adding another Sigil with the same official activated text should only require official catalog / BehaviorSpec data, not an engine table row.

## Authority

Official card data:

- `data/official/card-catalog.zh-CN.json` rows `SFD·222/221`, `SFD·226/221`, `SFD·229/221`, `SFD·231/221`, `SFD·234/221`, and `SFD·238/221`.
- `data/official/card-catalog.zh-CN.json` rows `OGN·040/298`, `OGN·081/298`, `OGN·120/298`, `OGN·163/298`, `OGN·204/298`, and `OGN·245/298`.
- `CORE-260330` p10-p13 rules 131 and 135.2.e cover card costs, mana costs, rune costs, `[A]`, and typed rune power.
- `CORE-260330` p20 rules 162-167 cover rune-produced mana / rune power and the distinction between typed and generic rune power in the rune pool.

The official Sigil text shape for this slice is the activated resource skill with `{{横置}}` cost, `{{反应}}` timing, and `{{获得}}{{颜色}}，用以支付符能费用...` effect text.

## Implementation

- `ActivatedAbilitySpec` now exposes minimal structured fields for typed resource skills: `Kind`, source-exhaust cost, reaction speed, resource-skill marker, payment-only marker, generated trait, and generated amount.
- `RuleTextParsers.ActivatedAbilityParser` recognizes the official Sigil text pattern and emits `ActivatedAbilityKinds.TypedResourceSkill`.
- `P4ActivatedAbilityCatalog` lazily builds Sigil typed-resource profiles from `BehaviorSpecCatalogBuilder.Build(...)` instead of maintaining a hand-written `P4SigilTypedResourceProfile[]`.
- Existing public Sigil constants, profile shape, ability ids, effect kinds, resource restriction ids, and SFD / OGN split remain preserved for compatibility.
- Dev UI catalog typing was extended only to deserialize the new optional activated-ability fields.

## Validation

- Red focused guard: `BehaviorSpecCatalogParsesSigilTypedResourceActivatedAbilities` failed before implementation because `ActivatedAbilitySpec.Kind` and `ActivatedAbilityKinds` did not exist.
- Focused gate: `BehaviorSpecCatalogParsesSigilTypedResourceActivatedAbilities|P4SigilTypedResourceProfilesAreDerivedFromBehaviorSpecs` passed `2/2`.
- Adjacent / hidden-info gate: `SigilResourceSkill|RageSigil|OgnSigil|SfdSigil|ActivatedAbilitySourceIdentityGuard|CardCatalogBaselineTests|PaymentEngine|MatchRecovery|FullGameEndToEnd` passed `3382/3382`.
- Backend full conformance passed `9145/9145`.
- Dev UI build passed after the shared catalog type update.

## Holdbacks

This does not migrate the full P4 activated ability catalog into BehaviorSpec, does not complete the official resource-skill family, does not broaden target / timing / payment semantics, does not update card-matrix `fullOfficial` rows, and does not close P0, P1, or READY.
