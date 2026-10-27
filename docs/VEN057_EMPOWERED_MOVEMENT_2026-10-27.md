# VEN057 dependent movement draw

CN37748 / VEN·057 秘密线人 is added as the exact ordinary unit printing: 3 mana plus one blue power, printed Might4, Empower3 mana, and a dependent self-movement draw. The complete shared copied-movement producer and clause-local empowerment capture are ordinary-merged here at `66f24137607f9ccb6dd249124ad3c890a3b729d5`. Existing1084 raw records, scoped source additions and functional groups remain unchanged. Current1085 entries/866 functional units and classified draw/move/trigger counts are metadata, not semantic certification.

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
| `66f24137607f9ccb6dd249124ad3c890a3b729d5` | Full16-method/30-row class after complete shared production merge | 28 pass,2 newly added multi-instance ordering input fixture failures;0 skipped |
| `cc836ab12c38d159fae726719a90b4633d35efae` | Same2 multi-instance rows with explicit `OrderedTriggerIds` | 2 pass,0 skipped;2/3 independent draws from1 move; production unchanged |

The early missing-skill failures remain original fixed-input evidence. After the shared merge, native empowered movement in both directions, two actual moves in the same turn, both canonical openings, both takeover directions, and an earned draw after actual source return all pass in the fixed30-row run. Its only2 failures are the new ordering fixtures described below; no30-pass result is synthesized across heads.

Both canonical openings begin from empty seating with seed424242, submit valid decks, naturally draw/channel, pay actual blue power and mana, and pass normal production cold recovery and a coordinated3-mana-refund forgery rejection before the movement failure. P1 retains26 accepted commands/112 events; P2 retains24/101. Both physical-rune takeover paths retain25 accepted commands/65 events, original owner/current controller and actual ready/move actions. No state edit substitutes for movement or removal of empowerment.

The new copy rows separately empower the original unit or leave it unempowered, then really assemble/copy with VEN137 and choose the host's acquired Empower by its prompt instance ID. The host remains initially unempowered, pays3 mana without power, and its pending activation resolves even if SFD011 removes the copy first. Each then moves the empowered, restored physical host after copy removal and verifies no extra draw. Fresh API and whole-card readback confirm SFD011 itself draws1; its schema uses `cardCategoryList` where the runtime snapshot uses `cardCategory`. The corrected four rows retain13/36 or16/43 accepted commands/events, one support draw, and zero draws after movement.

Seven further rows were first compile/discovery-only at `3c0edca7ee013018dfb5372b34e1e3d302639a22`. In the later fixed30-row execution, the four original/host empowerment combinations and earned copied draw after actual copy removal pass. The2 multi-instance cases first earn the correct2/3 skills, then fail because a positional `OrderTriggersCommand` argument serializes the legacy `triggerIds` alongside null `orderedTriggerIds`. The mapper prefers the present new field, so this is an ingress fixture error. The one-line named-argument correction retains every row/assertion and then passes both cases. The rejected requests were not journaled; their original TRX, fixed source and pre-order states remain available without claiming a captured rejected wire.

Original logs, TRX, requested-versus-discovered methods, actual compile inputs and five loaded module hashes remain in the ignored QA directory. The original six untracked drafts remain unchanged and excluded from compile. Results are not rebound to later documentation or composition commits.

## Shared dependency and remaining work

[Issue190](https://github.com/DDDHL0704/riftbound/issues/190) remains OPEN for delivery/acceptance. Shared production `10858d2442862caf7a5ead8e1ed67329301e33fe` reuses the complete copied-movement producer `acbbce7759f6fe42c7fd56d99e6b6a1f13325ebb`, including its production ancestor. It captures the current host's clause-local empowered condition and controller per RuleTextInstance at the movement event, retaining the earned skill independently of later source/condition changes. There is no CardNo exception or whole-card text-substring gate.

A read-only preflight identified four projection-method conflicts: both `MatchRecovery.CapturedBattleSourceIsPrivate` overloads, `MatchSession.OrderTriggerSourceObjectIdForViewer`, and `MatchSession.BuildStackItemSnapshotView`. Integration assigned them to the shared owner, who ordinary-combined both empowered-banish and movement source privacy; the complete result is imported here. This consumer line did not edit those shared methods.

Actual same-generation removal of empowerment remains OPEN. Fresh official VEN035 image/text confirms **3 mana plus one green power**, correcting an earlier blue-cost planning note; that separate modal Reaction consumer is not registered or executed here. Full final L2/L3 and whole-card certification remain OPEN; certification delta0. No push or PR is claimed for this consumer package.
