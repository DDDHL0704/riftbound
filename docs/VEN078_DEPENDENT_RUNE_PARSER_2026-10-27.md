# VEN078 dependent rune parser — 2026-10-27

Status: **parser dependency verified; card gameplay NOT IMPLEMENTED/NOT CERTIFIED here**. No new catalog row, normal registry entry, Engine/Contracts change, cumulative identity/count change, PR or main publication is included.

Exact retained current CN37772 / VEN·078/166 SC 枯爪巴凯 has play cost4 mana/no power, Might4, Empower1mana/generic2, empowered self Might+2, and empowered LastBreath calling two dormant runes. The source record and whole printed face were checked: canonical record `0ec9131406d8f02798b38d79e57e1d8737e8982fe7a3cb75eac6729d215987ab`, face `e4067ebe00f6dea1e969fd37379ba1e88151b45e33e79cc82a3abcec93405813`. Publication/effective dates and full current applicability remain UNKNOWN. Original request/inventory evidence is linked by the retrievable packet.

Complete code input `3feceac3cc8469a7a32486da1f6c6ae3ec35283a` is based on the complete No128 consumer `8c535bb147f6c05f50bbe7b4aa0cffc7c9dc3024`. It changes only the rune parser and adds focused tests. It does not silently import the later cumulative source or shared death implementation.

The parser retains the actual positive rune count, recognizes the adjacent `{{已强化>}}{{>绝念>}}` condition, and populates the existing `RuneCallCount` / `RequiresSourceEmpowered` fields. Only the private parser helper is renamed. The existing public effect-kind identifier is retained; quantity comes from the scalar field. A clause boundary prevents OGN075's two-runes-plus-draw text, optional calls and unbound dependent markers from becoming an incomplete rune-only effect.

Fixed executions remain separate:

- `0a496679ffc2601345b1fdabdcfdfc2b79fe1fae`:17 cases,11P/6F/zero skip; original missing two-rune/condition expectations retained.
- `3feceac3cc8469a7a32486da1f6c6ae3ec35283a`:35/35/zero skip; exact source metadata, conditional/unconditional count shapes, compound/optional/zero rejection, adjacent minion parser and No128 source/group controls.
- Same code, independent run:8/8/zero skip; existing OGN216/UNL152 native metadata/destruction, state-based cleanup, hidden suppression, APNAP ordering and resolution regressions. These execute the old one-rune consumers, not VEN078 gameplay.

A retained-assembly comparison gives identical TriggerParser outputs for every one of the original1086 raw CardEffect strings. The compound OGN075 printing is explicitly retained. Each run's2664 measured inputs match exact Git blobs, actual five loaded module Info/MVID/SHA records and original DLLs are retained, discovery matches actual methods, and the original six own untracked drafts remain unchanged/excluded.

Evidence root: `/Volumes/ExternalSSD/MyProjects/.worktrees/riftbound-cards-20261023/var/qa/cards-ven078-dependent-rune-20261027/`. Complete `parser-mechanism-delivery-v5.json`, SHA256 `bc3d9f9c6af3b28b8e3c3338871fb0b1098ac19227bd21df7f15a80f45f01598`, contains original red/green TRX, source/face references, actual module/source provenance and the old-parser comparison. Do not sum the separate executions or rebind them to successors.

The next step is complete ordinary source/registry integration through the assigned cumulative writer, then actual native play/Empower/self/death/copy/zone/private-source consumers using the complete verified shared implementation. Positive-count acceptance in the old predicate is static evidence only; no observed VEN078 runtime result is claimed. Current compatible L2, applicable source closure, full L3 and whole-card certification remain OPEN. Certification increment:0.
