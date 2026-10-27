# VEN057 dependent movement draw

CN37748 / VEN·057 秘密线人 is added as the exact ordinary unit printing: 3 mana plus one blue power, printed Might4, Empower3 mana, and a dependent self-movement draw. Existing1084 raw records, scoped source additions and functional groups remain unchanged. Current1085 entries/866 functional units and classified draw/move/trigger counts are metadata, not semantic certification.

The fresh official API and personally viewed whole card face are retained under `var/qa/cards-ven057-empowered-movement-20261027/source/`. The source contract, rule PDF identity and established battlefield-withdrawal protocol correction are preserved separately. Source-universe applicability remains UNKNOWN.

## Fixed observations

| Fixed head | Exact scope | Result |
|---|---|---|
| `73cef7874b0ec961f7100f8453780a62f58056f3` | Original13 consumer rows | 9 pass;2 missing-trigger failures;2 precise-origin protocol fixture rejections |
| `65fd70ae2b46af809daad80dc52c18f7d382ea2c` | Corrected standard movement in both directions, empowered/unempowered | 2 pass,2 missing-trigger failures; all actual movement commands accepted |
| `b6381c04b062e47ad8e7329338ca0d78788f6be0` | 25 catalog/source guards and2 strengthened recall/return controls | 24 pass;3 stale classifier-count failures; all requested methods executed |
| `507bafb5b6e46d4bf7ae5e1c283deaf89f937f97` | 3 corrected classifier guards and6 new consumer rows | 4 pass,5 missing-trigger failures;0 skipped |
| `e6aea678ea488fae042fe5a113ff2ed13606d208` | 4 new copied-Empower/detachment rows | 4 fixture failures: omitted SFD011's mandatory draw1; original results retained |
| `6cd9e314f921c3954b6c980f134f173558547925` | Same4 rows after exact support-source correction | 4 pass,0 skipped; copied Empower costs only3 mana and survives actual copy removal |

The latest passing consumer performs two actual standard moves in the same turn without empowerment and draws no card. The five new failures preserve actual empowered movement after readying, both legal canonical openings and both takeover directions. Each stops at missing source-skill birth; later draw and lifetime assertions remain present and unexecuted.

Both canonical openings begin from empty seating with seed424242, submit valid decks, naturally draw/channel, pay actual blue power and mana, and pass normal production cold recovery and a coordinated3-mana-refund forgery rejection before the movement failure. P1 retains26 accepted commands/112 events; P2 retains24/101. Both physical-rune takeover paths retain25 accepted commands/65 events, original owner/current controller and actual ready/move actions. No state edit substitutes for movement or removal of empowerment.

The new copy rows separately empower the original unit or leave it unempowered, then really assemble/copy with VEN137 and choose the host's acquired Empower by its prompt instance ID. The host remains initially unempowered, pays3 mana without power, and its pending activation resolves even if SFD011 removes the copy first. Each then moves the empowered, restored physical host after copy removal and verifies no extra draw. Fresh API and whole-card readback confirm SFD011 itself draws1; its schema uses `cardCategoryList` where the runtime snapshot uses `cardCategory`. The corrected four rows retain13/36 or16/43 accepted commands/events, one support draw, and zero draws after movement. They do not verify the still-missing positive copied movement trigger.

Seven further rows at `3c0edca7ee013018dfb5372b34e1e3d302639a22` specify the original/host empowerment cross-product, earned copied draw after actual copy removal, and one draw per native/one-or-two attached text instances while physical movement increments only once. Compile and discovery succeed for all16 methods/30 rows. These7 rows have **not executed**: the complete copied movement producer and conditional birth implementation are not yet combined here. No semantic TRX or green result is attributed to that compile-only input.

Original logs, TRX, requested-versus-discovered methods, actual compile inputs and five loaded module hashes remain in the ignored QA directory. The original six untracked drafts remain unchanged and excluded from compile. Results are not rebound to later documentation or composition commits.

## Shared dependency and remaining work

[Issue190](https://github.com/DDDHL0704/riftbound/issues/190) tracks the actual missing conditional movement skill. Reuse the complete copied-movement producer `acbbce7759f6fe42c7fd56d99e6b6a1f13325ebb`, including its production ancestor, and the ordinary RuleTextInstance movement framework. Check the clause's host condition at the event; retain the earned skill after the condition or source later disappears. Do not add a CardNo exception or gate the whole card by a text substring.

A read-only ordinary-merge preflight against the complete empowered-unit-aura descendant found four actual projection-method conflicts: both `MatchRecovery.CapturedBattleSourceIsPrivate` overloads, `MatchSession.OrderTriggerSourceObjectIdForViewer`, and `MatchSession.BuildStackItemSnapshotView`. Integration must preserve both empowered-banish and movement source privacy. No conflicted source/index merge was started here.

Copied text and multiple instances, the empowered second move, earned-skill source departure, and actual same-generation removal of empowerment remain OPEN. Fresh official VEN035 graph/text confirms **3 mana plus one green power**, correcting an earlier blue-cost planning note; that separate modal Reaction consumer is not registered or executed here. Full final L2/L3 and whole-card certification remain OPEN; certification delta0. No push or PR is claimed for this consumer package.
