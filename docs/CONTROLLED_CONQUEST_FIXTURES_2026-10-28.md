# Controlled conquest fixture correction

Refs [#227](https://github.com/DDDHL0704/riftbound/issues/227).

The Minefield, Moonveil Altar, Zaun Sump, Shirana Monastery and Vayne full-game consumers expected a new P1 conquest after placing P1 attackers and a P2 defender on a battlefield already controlled by P1. Four builders explicitly assigned P1 control; Vayne inherited it from its established opening.

The original fixed-main nightly [40591520309](https://github.com/DDDHL0704/riftbound/actions/runs/40591520309/job/122497179879), attempt 1 at `c25e77803aba57e4fac51939f5558d18a0462215`, failed all five consumers. The four battlefield failures record previous/current controller P1, `changed=false`, and `CONTROL_CONFIRMED`. Vayne reaches its original assertion without a pending payment; its archived failure does not contain an authoritative pending frame.

The adopted [CN core snapshot, updated 2026-10-10](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf), rules 190.4, 468.5 and 471.1, distinguishes establishing new control from maintaining existing control. A defender-controlled battlefield gives these controlled fixtures the intended capture condition.

## Change

The five builders now assign P2 control. The common controlled-midgame helper clears the cards-played counter to an empty dictionary, matching the engine's existing representation for no cards played. Its old two zero-valued entries fail current component recovery validation. Shirana's appended Boon tags are sorted ordinally, matching card-object constructor normalization and preserving snapshot hash equality.

The five original complete-game methods, consumer/payment assertions, victory continuations and original action/event replay remain. Production rules, catalog, registry, source/profile and CI are unchanged. All original test bytes through the last public test body in the root full-game file remain identical to main `1a8edcfc`; every other original test file is unchanged. The original 143-method/151-case policy, 110 ordered deck pairs, eleven decks and seed 424242 remain in place.

## Actual focused evidence

| Actual input | Selected scope | Result |
| --- | --- | --- |
| `fec98bf2d4c9887fb4cd0e7f96895d13bfedf262` | Original controlled setup, five bounded consumer prefixes | 0 passed, 5 failed |
| `8a8b0c47f7835eb68b5d1e76d0c8676ce0cc45da` | Defender control and empty counters | 4 passed, 1 failed at Shirana initial snapshot hash equality |
| `2cf2ac096d6bad73bea2592bce472c224cbf6d63` | Canonical tags, five bounded consumer prefixes | 5 passed, 0 failed, 0 skipped |
| Same `2cf2ac09` and same four DLLs | Original conquest lifecycle, legend conquest and final conquest point guards | 44 passed, 0 failed, 0 skipped |

The final two serial test hosts execute **28 methods / 49 unique passed rows**. Their actual four loaded DLLs have matching Info/MVID/SHA and retained bytes. All 3,347 source/build inputs match Git blobs; 810 tracked Conformance C# files are included in 823 actual Compile items. Five foreign drafts remain unchanged and excluded.

The five prefixes preserve original consumer/payment assertions and verify raw component state, cold snapshot hash equality, projections and original-command/event replay to the final prefix state. Twenty-one component/cold/replay frames are healthy. Their retained journals contain 37 accepted commands and 158 events across five independent controlled scenes, each with one actual conquest. Original failed TRXs and frames remain under their original inputs.

Actual final TRX SHA-256 values:

- Five prefixes: `f465ba614e6d297bb60498951727ea61bb54ef75959bcdfccf93d54f4bc0f35c`.
- Original 44 guard rows: `cc13ddb22cd9f223f698933be2718ba618182d709e68a56adb07da7c833980f5`.

Retrievable local evidence: `var/qa/conquest-fixture-consumers-20261028/`, including `same-dll-49-actual-verification-v9.json` (SHA-256 `ad05923278cafaf6a7e8fe5858fc7cb933dc3de3fff176a5be5f795883bb506d`). The original nightly archive is artifact `12778706596`, retained with its platform size/digest and eleven original files in `var/qa/nightly-c25-40591520309/rule-suite-v4/`.

## Remaining gates

These checks start from controlled midgame state. They do not certify accepted opening history or the original five complete victory continuations at this input. The branch still requires its own latest merge-result L2 and relevant complete fixed-input L3. The frozen C25 nightly remains failed: its two available rule-suite TRXs contain 96 passed and five failed rows, while three other shard TRXs are missing. Whole-card and complete-backend acceptance remain OPEN.
