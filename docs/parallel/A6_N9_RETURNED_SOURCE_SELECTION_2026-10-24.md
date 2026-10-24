# A6 N9 returned-source verifier selection
Fixed input: `7e0766252c287b90b23d8b717f38d4f1ef0c8d9d`; Actions run `40049395550`, related-network job `120738821679`, original artifact `12543890797`.
The original `043-revealed` verifier fails at line 108: its whole-stack `Single()` sees both the pending Empower skill and the real crab-death reveal skill.
The approved leaf change selects the sole non-null `EmpowerActivation`; leaf SHA changes from `a44d53e586585aebdd52ac665b9d3c41042af93de9f16aed3b8e157921e812d2` to `330c55d645f01d90eadbd6b1430e42c0521b7cf8e8dd9bf05709a40727508692`.
The preceding mandatory `CheckPending` and all source, generation, payment, privacy, history and SQL checks remain intact; zero or multiple matching activations still fail.
Permanent focus: 2 methods / 9 rows pass, covering public/private/revealed real command prefixes, unchanged rejected state/views/prompts, real reveal retention, stack-order independence and zero/multiple-match rejection.
Separately, fresh local `043-revealed` v2 uses fixed-A6 core DLLs and real dual-player SignalR/PostgreSQL: play passes, original verifier exits `-6`, the changed leaf exits `0` against the untouched same history.
The actual pending source remains generation 1; the returned hand unit is generation 2 and unempowered; reverse-order passes and zero/multiple-match clone controls reject without changing the seven-table digest.
Fixed-A6 driver RIF is `e40b9017cb6fa36bb631d3938040b63df116d69eb088ee341eff651280fc31d7`; the separate current-branch permanent focus has RIF `5c3496f42c14e1c0376e8fefd78dcba1ed606ff9e1acced5a726fa436828bcee`.
Evidence: `var/qa/a6-n9-source-selection-20261024/source-test-receipt-v2.json`, actual API/driver/test module paths/MVIDs/SHAs, compiled-source manifests, original failures, private local frame, SQL audits and runtime freezes.
The v1 startup-hook receipt was overwritten by a child driver; it is retained as an instrumentation error. V2 scopes the hook to API PID `93206` and confirms its Engine MVID/SHA equals both actual verifier runtimes.
Only owned loopback API/PostgreSQL processes were stopped; both local attempts, database data, dumps and logs are retained. No gameplay/catalog/registry change, CI dispatch or candidate-tag move is included.
This is single-prefix before-restart evidence: no local restart/recover/tamper/full-network pass or original-CI private-frame reconstruction. Backend remains NOT READY, net whole-card certification 0; 1740 printings / 14293 clauses and applicability/functional denominator UNKNOWN remain unchanged.
