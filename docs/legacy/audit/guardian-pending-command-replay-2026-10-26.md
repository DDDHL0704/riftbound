# Guardian pending-command replay repair

This fixes one continuation checkpoint boundary on parent `100a8689adcc6e00112f23f833382ba3283acd9f`. It does not certify the whole card or the complete backend. Existing Guardian source scope remains in [the catalog identity audit](guardian-catalog-identity-l2-repair-2026-10-24.md).

## Actual failure and change

A match generated through legal seating, deck submission, readiness, mulligan, play, assembly and combat commands reached two physical SFD051 Guardians' destruction-replacement choice. Seed was 424242; the original journal contained 187 accepted commands and 526 events. Ordinary recovery validation passed, but the production action-log audit rejected the cold frame with final-state hashes `658e0b18…` / `6d725493…`. Surrender had not executed.

The single-command counterexample differs only at `PendingRuleChoice.Command.battlefieldTargetObjectIds`. The original typed null is omitted by `JsonIgnore(WhenWritingNull)`; replay through `GameCommandJsonMapper` returns an empty array, which is then captured as a property. This is an authoritative state difference, separate from the earlier prompt-metadata serialization diagnostic.

`CoreRuleEngine.ResolveAsync` now captures the command through the same mapper already used to resume a continuation. It keeps the original intent, origin, answers, request, accepted inputs and execution unchanged. Global state/event hashes, formal replay, source-birth guards and rejection policy are unchanged. No compatibility fallback for older development matches is introduced.

## Fixed local evidence

- **28 methods / 54 distinct cases passed, zero skipped.** `fixed-v3-one` and `fixed-v3-adjacent` use identical 2460 source inputs and one unchanged 1420-file bundle. The first case and remaining cases are disjoint. This is L1, not L2.
- Four new cases regenerate the legal opening from an input-only recipe with expected object identifiers. Raw JSON versus absent raw/typed input is crossed with chooser P1 versus nonchooser P2 surrender. Both pending and finished frames pass the actual registry's mandatory original-log audit; wire views and live duplicate replies/journal counts match.
- Deleting the captured array property remains shape-valid but fails that same registry's formal audit against the unchanged accepted history. No null/empty comparison is relaxed.
- An additional direct-bundle next-command probe consumes only the selected Guardian, heals/exhausts and recalls the host group, preserves host identity and the other attachment's state, then performs cleanup before conquest. Its 188-command / 538-event cold frame passes ordinary and formal replay. This probe is separate from the 54 TRX cases. Recall expectations follow CN 457–460 / 719.3–719.4.
- All 651 original Conformance source files and all nine original full-game partials are unchanged; 143 original full-game methods / 151 cases remain. All 652 tracked Conformance sources compile. Only the five pre-existing foreign untracked files are locally excluded and preserved. The old 1419-file frozen100a bundle is byte unchanged.

Actual local Engine SHA256: `c23c31ac2bc5102075c217efd94d623cb19550236c8d5d5bf89e9c3989f7877d`; MVID: `e89c82a3-a702-43b2-9926-bd423ffa214e`; RIF: `a6d4bc0ea3e18dbb46ca1eeb1393038185a12902f1af0039cf6e56cf147e3d21`. Catalog identity remains `db1bd394788127ba21b219ec475800fcb996f8098305081bcbe96019aaab0ede` / 1077 rows. These identities describe this local fixed input only.

Accessible evidence on the shared host, relative to the combat worktree:

- `var/qa/combat-final-backlog-prep-20261026/guardian-choice-surrender-v2/` and `pending-choice-one-step-v1.json`: original producer failure and single-step counterexample.
- `var/qa/combat-pending-choice-repair-20261026/`: `validation-summary.json`, both fixed-v3 TRXs/source/bin manifests, `runtime-identity.json`, `current-pending-choice-one-step.json`, the four gzip cold journals, and `selected-choice-continuation-v2.json` plus probe source/logs.
- Early local analyzer/zone-assertion failures and the probe's incorrect generation-increment expectation are retained as diagnostics; they are not production failures or passing evidence.

Integration must ordinary-merge this repair and run the current complete L2 on the actual final combination. No new PR, push to PR165 or CI dispatch was performed here. SQL, API/process restart, full matrix, L3 and whole-card certification remain unclaimed.
