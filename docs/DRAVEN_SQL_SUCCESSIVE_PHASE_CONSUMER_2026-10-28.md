# Draven SQL successive-phase consumer

Related shared-result-phase issue: [#101](https://github.com/DDDHL0704/riftbound/issues/101). Its other obligations remain OPEN.

The fixed C25 nightly [40591520309, partition 0](https://github.com/DDDHL0704/riftbound/actions/runs/40591520309/job/122497179898) failed `DravenBattleDeathSqlAcceptance` in `mixed-defender`. Its old SQL assertion expected the later battle-result draw to resolve before the earlier death score. The existing Conformance mixed-role rows already require death score, result determination, draw and close in that order. Both source files are byte-identical between frozen C25 and main `1a8edcfc`.

The adopted [CN core snapshot](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf), rules 468.2/468.3/468.4/468.5, completes damage/death/cleanup items before determining the result, and completes result items before establishing control. These are successive phases.

## Original evidence

Original artifact `12778719161`, `postgres-partition-0`, is retained with its platform size/digest and all 1,851 original files. ZIP SHA-256: `0d7abb7ba11c63656007ef48682df1a955f4aef83de0fbf7ca2d0452410b9d35`.

Seven original Draven branches completed successfully. The failed mixed branch retains twelve real SQL-loaded prefixes, each consistent with no validation errors. The final original events include:

| Event | Original sequence | Tick |
| --- | --- | --- |
| Death score | 24 | 10 |
| Battle result determined | 25 | 10 |
| Draw | 32 | 12 |
| Battle closed | 36 | 12 |

Conquest occurs between the draw and close. The failed branch has no passing final receipt or final seven-table dump, because the old assertion aborts before those writes. Its twelve original prefix frames remain available. Recycle and three subsequent forgery branches did not execute after the abort. No actual loaded-DLL MVID or retained DLL bytes are inferred for that original SQL run.

## Correction and local checks

The SQL consumer now checks death score before result determination, then draw, conquest and battle close. It saves the original loaded frame and seven SQL tables before checking this order. Original recipients, score counts, winning cutoff, every-prefix cold recovery, original-history replay, replacement, recycling and forgery checks remain. No producer, source/profile, catalog, registry, existing method identity or case is changed.

Actual input `030cd8df2a229b0692c3be88d59653bac5b15f23` builds the SQL project with zero warnings/errors and passes **two existing Conformance methods / six rows / zero failed or skipped**. These cover both mixed roles, both Draven printings and normal/winning score boundaries. Their retained evidence contains 68 original command prefixes with replay and cold recovery, including four expected rejected ordering commands. Four loaded test-host DLLs retain matching Info/MVID/SHA and bytes; all 3,346 Git inputs match, 809 tracked Conformance C# inputs are included in 822 actual Compile items, and five foreign drafts remain unchanged and excluded.

Actual focused TRX SHA-256: `c9ca0018c944318cd751fa86ff7f91cc8254dd58a8f383abf0976355d33cb4ff`. Retrievable local evidence is under `var/qa/draven-sql-order-consumer-20261028/`. Original SQL evidence and the complete 1,862-reference failure packet remain under `var/qa/nightly-c25-40591520309/partition0-v7/`; packet SHA-256 `28a226b2d1110ec635d5a21ee66635dfd439f37e67e9a6bb601893acbba7133c`.

The corrected SQL consumer has not executed against PostgreSQL in this local batch. Its latest merge-result L2 and relevant fixed-input SQL/L3 remain OPEN. The original C25 failure, seven completed branches and missing later branches retain their original outcomes. These controlled positions do not certify ordinary legal opening history, whole cards or the complete backend.
