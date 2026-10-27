# OGN077 current correction and replacement span

CN31291 / OGN·077/298 中娅沙漏's current official API correction separates the friendly-unit replacement condition from its subsequent damage removal, exhaustion and recall instruction. The previous splitter discarded that continuation, leaving an unsupported generic replacement. The acquired printed wording remains executable and unchanged in the raw snapshot; this was not an observed live-game regression.

The parser now binds only the exact condition and immediately adjacent, same-unit continuation. It preserves the legacy grammar and cannot borrow a recall instruction from an unrelated clause. The reviewed runtime text is scoped to this exact id/cardNo. The existing generic replacement execution is reused; no engine, fee-reachability, DTO, CI or history-rewriting changes are made.

Source: [official card API](https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb), current exact record SHA256 `9e24c68a21f2856661cd54d8cacd0f8ccd8e799576cf89d50cb0357a40c0a5cd`. The whole original card face was viewed, and the complete API errata is independently retained. Publication/effective dates and full source applicability remain UNKNOWN. All1082 acquired rows remain byte-identical; the effective catalog identity changes from `774f3dde0c296496b30c354c5334bb66ee53d6a280e8fdc319967ed85c934a42` to actual observed `af4d7aa516d1c93c11ddac2bbcf931a8f344ee6069251dd016a5b18623db9cd6`. The old identity and every older rejection guard remain.

| Fixed code/test input | Actual scope | Result |
|---|---|---|
| `c2dfdd546e96bbd8eab26e53b6de9b799044d131` | Four methods/14 source, legacy and clause-boundary rows | 10 pass/4 fail,0 skipped |
| `871ab85d0cde5f536ab25ff0d84265f2712de674` | Corrected source/parser plus original Zhonya, copied/attached replacement, resource, recall and metadata neighbors | 74 methods/172 pass,0 skipped |
| `e1b45781bea1a4fb9140f33a0e64d19b01388d4a` | Catalog/authority identity, Core/Session refusal, restoration and original replay | 11 methods/24 pass,0 skipped |
| `495efe988a34c81dcfb12cd0c58d0c91a0c5c799` | Complete printed-unit/equipment target consumer, now using the current correction | One method/24 pass,0 skipped |

The first test-only input `0ac7b4f` failed compilation on a missing rune-trait namespace; no semantic result is claimed for it. The later14 baseline rows are all retained and pass within the172-row fixed run. No stage result is summed or rebound to a later head. The later identity and consumer combinations leave production/data byte-identical to871ab85d.

The complete ordinary consumer parent is `7719acc2dd698acf655a834bc24ca6829346893c`; its original74-row fixture history and original tests remain. The24 new cases use actual printed VEN058 unit/equipment targets, BASE/BATTLEFIELD, no replacement/Zhonya/real Guardian attachment, generic/typed wallets and room1/2 at the explicit turn5/MAX representation boundary. They retain current-prompt Session rejection invariance and real accepted cold recovery, duplicate suppression and replay. They do not establish a mixed UNIT replacement SOURCE, normal legal openings, VEN058's play/draw effect, SQL/network/restart or whole-card certification.

Original source, logs, actual discovery/TRX identities, five loaded module Info/SHA/MVID records, compile/input manifests and the six unchanged excluded drafts are retained in ignored `var/qa/cards-ogn077-current-errata-20261027/`. [Issue197](https://github.com/DDDHL0704/riftbound/issues/197) tracks this correction; [Issue143](https://github.com/DDDHL0704/riftbound/issues/143) retains the deeper resource/source scope. Current complete L2/main/L3 and whole-card certification remain OPEN; certification delta0. VEN035's complete consumer branch remains separately preserved.
