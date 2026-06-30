# Plan B P4 Activated Ability Source Group Catalog Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data is the only authority for this slice:

- Vi: `UNL-030/219` and `UNL-030a/219`.
- Renata Glasc: `SFD·088/221` and `SFD·088a/221`.
- Azir: `SFD·050/221` and `SFD·050a/221`.
- Ezreal: `SFD·082/221`, `SFD·082a/221`, and `SFD·082b/221·P`.
- Jhin: `UNL-022/219` and `UNL-022a/219`.
- Blue Sentinel: `UNL-087/219` and `UNL-087a/219`.

These official rows preserve the same implemented activated / resource ability semantics across alternate or promo card numbers. The base rows sometimes include reminder text that alternate rows omit; one Blue Sentinel alternate also lacks region metadata. Runtime source identity therefore has to be based on normalized rules identity, not a hand-written ability-id switch or print-only metadata.

## Engine Evidence

Before this slice:

- `P4ActivatedAbilityCatalog.SourceCardNosForAbility` manually switched on ability ids for Vi, Renata Glasc, Azir, Ezreal, Jhin, and Blue Sentinel.
- Adding a new official alternate source row required editing that switch even when the official rules identity already matched.

After this slice:

- `SourceCardNosForAbility` resolves source groups from a lazy map built by shared `OfficialCardSourceIdentityGroups`.
- The map normalizes official row identity and strips parenthetical reminder text before grouping.
- Legend action source groups reuse the same helper after the follow-up catalog slice.
- Groups with multiple P4 runtime source definitions fall back to one-card groups, preventing SFD / OGN Sigil rows or UNL / SFD Gold token rows from sharing the wrong ability id.
- Existing P4 ability definitions, effect kinds, costs, payment resources, prompt actions, stack items, recovery expectations, and hidden-information boundaries are unchanged.

## Test Evidence

- `ActivatedAbilitySourceIdentityGuardTests.P4ActivatedAbilitySourceCardGroupsDoNotUseAbilityIdSwitches` blocks reintroducing the ability-id switch and requires the source map to read the official catalog.
- `ActivatedAbilitySourceIdentityGuardTests.P4ActivatedAbilitySourceCardGroupsPreserveOfficialEquivalentRows` locks the Vi, Renata Glasc, Azir, Ezreal, Jhin, and Blue Sentinel alternate / promo source groups.
- `ActivatedAbilitySourceIdentityGuardTests.P4ActivatedAbilitySourceCardGroupsDoNotMergeDistinctRuntimeDefinitionsFromSameOfficialUnit` locks the Sigil and Gold-token no-merge boundary.
- Red focused guard failed before implementation on the old ability-id switch.
- Focused gate passed `11/11`.
- Adjacent / hidden-info gate passed `3570/3570`.
- Backend full conformance passed `9048/9048` after the shared-helper follow-up.

## Non-Claims

This evidence does not claim full P4 activated ability data modeling, full official resource-skill breadth, complete payment windows, complete target-stack timing, P1 closure, or READY.
