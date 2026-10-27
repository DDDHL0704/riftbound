# VEN035 modal status consumers

CN37723 / VEN·035 念化盈虚 is a3-mana, one-green-power Reaction spell. Its modes empower a unit now and remove empowerment at this turn's end, or remove an empowered unit's status now and empower it at this turn's end. Exact current API record and previously viewed whole-card image are byte-identical to the prior scoped intake. Publication/effective dates and the complete applicable source universe remain UNKNOWN.

This branch adds the exact raw record, two ordinary mode registrations and `Ven035DelayedEmpowerConsumerTests`. It does not implement the missing shared status/delayed mechanisms. The full published VEN057 dependency `c03cba67f2e5ccc2ef1499d70ac6b9580935c007` is an ordinary ancestor. All1085 preceding records/scoped additions remain unchanged;1086 entries/867 functional units are metadata only. The new actual catalog identity is `4aafafd814d176c6281e84f706dc068678b1de0e3bdf415b3f40ed1008dd577e`; prior VEN057 and all older input rejection guards remain.

| Fixed input | Scope | Actual result |
|---|---|---|
| `fbcc5eb095140718278120dd5448fd6ac1b9f322` | 5 methods/11 rows; real costs, modes, turns and VEN057 interaction | 2 source/metadata pass;9 semantic failures;0 skipped |
| `7a1eee76a35c4b7d18aec83e70f0b7da288bff63` | 29 source/catalog/native controls plus one instrumented direct engine probe | 29 pass; the target probe still fails;0 skipped |

The six valid-mode paths use actual unit play/native empowerment and physical P1 rune taps/recycles. All ordinary commands are accepted. First mode leaves unempowered friendly/enemy units unchanged; removal mode leaves empowered units unchanged. First mode on already-empowered units reaches the next player's turn without the required removal. These are missing-effect observations, not protocol fixture rejections. Later inverse-status assertions remain unexecuted where the immediate-state assertion fails.

Both same-generation VEN057 interactions use actual play, native Empower, two ordinary turns to ready, physical resources and a real Reaction. One moves before removal and retains an earned draw; the other attempts removal before moving. They currently stop because VEN035 never removes the status; future trigger/lifetime assertions remain present and OPEN. No direct status mutation or departure substitutes for removal.

The illegal-target probe is a separate direct `CoreRuleEngine` call using a state reached through real commands. It accepts an unempowered target, spends3 mana/one green, and creates the spell stack item. Both the preceding state and the returned result/events are retained. It is not an API transport or journaled command proof.

Ignored original TRX, requested/discovered methods, five actual loaded module identities/hashes, actual compile inputs and full command/event exports are under `var/qa/cards-ven035-delayed-empower-20261027/`. The original six untracked drafts are unchanged and excluded from compile. No results are summed across heads or rebound to later commits.

[Issue192](https://github.com/DDDHL0704/riftbound/issues/192) tracks the missing generic immediate/delayed state actions and second-mode target restriction. Required follow-up includes status transition observers, current-turn expired-window handling, independent created delays, target generation/lifetime, source departure, countered spells, target revalidation, normal opening/recovery and the real interaction with [Issue190](https://github.com/DDDHL0704/riftbound/issues/190). Complete final L2/L3/main and whole-card certification remain OPEN; certification delta0. This consumer branch has no push or PR.
