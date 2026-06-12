# Local 2P Regular Legend Batch Smoke - 2026-06-12

Status: NOT READY. This is an independent local playability smoke record only.

## Scope

- Branch/worktree: `codex/local-2p-smoke-20260612` in `/Users/dinghaolin/MyProjects/riftbound-local-2p-smoke`.
- Main sync at smoke start: `origin/main` `45f95ccad569dba6735ff59970843e2b3c9ac52e`.
- Raw official legend cards: 106.
- Tested scope after user clarification: regular version only, same legend name only one representative.
- Representative legends generated: 40 legal official decks.
- Deck rules used: current `OfficialDeckValidator` constraints, with 40-card main deck, 12 runes, 3 non-duplicate battlefields, selected champion in main deck, legend color legality, and copy limits.
- Battlefields were rotated across generated decks from 54 regular battlefield representatives.

## Services

- Backend: `http://127.0.0.1:5088`, health returned `{"status":"ok","service":"riftbound-dotnet","role":"migration-skeleton","dotnet":"10.0.0"}`.
- Frontend: `http://127.0.0.1:5173`, root returned HTTP 200.
- Existing detached sessions during this smoke:
  - `81203.riftbound-smoke-backend`
  - `81205.riftbound-smoke-frontend`

## Harness

- Added batch harness: `src/Riftbound.DevUi/scripts/local-legend-batch-smoke.mjs`.
- Harness path:
  - loads `data/official/card-catalog.zh-CN.json`
  - fetches `/catalog/behavior-specs`
  - generates legal regular-version legend decks
  - opens two local SignalR clients per room
  - submits both decks, readies both players, confirms mulligans
  - drives legal prompt actions: `TAP_RUNE`, `PLAY_CARD`, `PASS_PRIORITY`, `DECLARE_BATTLE`, `END_TURN`
  - retries with alternate seat, opponent offset, and target active/passive strategy until the target legend wins by score

Evidence files:

- `/tmp/riftbound-regular-legend-batch-smoke-full-matrix.json`
- `/tmp/riftbound-regular-legend-batch-smoke-from12-v2.json`
- `/tmp/riftbound-regular-legend-batch-smoke-aggregate.json`

## Aggregate Result

- Unique regular legend target wins: 40/40.
- Accepted target-win failures: 0.
- Total score-finish attempts: 84.
- Accepted target-win command count: min 113, max 235, total 5736.
- Accepted target-win aggregate events:
  - `CARD_DRAWN`: 3040
  - `RUNE_TAPPED`: 1168
  - `MANA_GAINED`: 1168
  - `CARD_PLAYED`: 379
  - `COST_PAID`: 379
  - `BATTLE_DECLARED`: 154
  - `TURN_PLAYER_ADVANCED`: 3037
  - `BURNOUT_APPLIED`: 358
  - `MATCH_WON`: 40

Notes:

- Two accepted target wins had zero `BATTLE_DECLARED` in the accepted room (`黑暗之女`, `虚空之女`), but still completed real local 2P score wins with draw/play/pay/pass/turn progression.
- Several early fixed-pair attempts showed strong target/opponent bias. The harness was adjusted to retry legal rooms with alternate seat, opponent offset, and target active/passive strategy. No product code was changed for those strategy issues.
- No unresolved local 2P P0/P1 blocker was found in this batch for backend start, frontend start, two-client room entry, deck submit, opening flow, prompt action execution, or score-finish match completion.

## Target Win Table

`legacy` means the earlier target-active matrix before the active/passive strategy toggle was added. `aggr` means the target side was allowed to declare battles. `passive` means the opponent was the battle-declaring side while the target still played legal prompt actions.

| # | Legend | Hero | Legend card | Opponent | Score | Accepted attempt | Seat/strategy | Commands | Battles |
|---|---|---|---|---|---:|---:|---|---:|---:|
| 1 | 傲之追猎者 | 雷恩加尔 | UNL-183/219 | 奥术先驱 | 9:1 | 1 | P1/legacy | 134 | 3 |
| 2 | 奥术先驱 | 维克托 | OGN·265/298 | 暴走萝莉 | 8:1 | 1 | P1/legacy | 126 | 3 |
| 3 | 暴走萝莉 | 金克丝 | OGN·251/298 | 奥术先驱 | 8:1 | 8 | P2/legacy | 124 | 1 |
| 4 | 不灭狂雷 | 沃利贝尔 | OGN·249/298 | 愁云使者 | 8:1 | 1 | P1/legacy | 146 | 6 |
| 5 | 愁云使者 | 薇古丝 | UNL-193/219 | 暴走萝莉 | 8:1 | 4 | P2/legacy | 150 | 6 |
| 6 | 翠神 | 艾翁 | UNL-195/219 | 不灭狂雷 | 8:2 | 4 | P2/legacy | 148 | 5 |
| 7 | 刀锋舞者 | 艾瑞莉娅 | SFD·195/221 | 德玛西亚之力 | 8:1 | 2 | P2/legacy | 146 | 5 |
| 8 | 德玛西亚之力 | 盖伦 | OGS·023/024 | 翠神 | 8:2 | 3 | P1/legacy | 152 | 7 |
| 9 | 光辉女郎 | 拉克丝 | OGS·021/024 | 刀锋舞者 | 8:1 | 3 | P1/legacy | 143 | 4 |
| 10 | 诡术妖姬 | 乐芙兰 | UNL-199/219 | 含羞蓓蕾 | 8:1 | 2 | P2/legacy | 142 | 5 |
| 11 | 含羞蓓蕾 | 莉莉娅 | UNL-189/219 | 黑暗之女 | 8:1 | 1 | P1/legacy | 235 | 5 |
| 12 | 黑暗之女 | 安妮 | OGS·017/024 | 机械公敌 | 8:0 | 4 | P2/passive | 201 | 0 |
| 13 | 机械公敌 | 兰博 | SFD·181/221 | 疾风剑豪 | 8:1 | 1 | P1/aggr | 134 | 3 |
| 14 | 疾风剑豪 | 亚索 | OGN·259/298 | 黑暗之女 | 8:1 | 5 | P1/aggr | 228 | 4 |
| 15 | 皎月女神 | 黛安娜 | UNL-197/219 | 九尾妖狐 | 8:1 | 3 | P1/passive | 128 | 2 |
| 16 | 九尾妖狐 | 阿狸 | OGN·255/298 | 炼金男爵 | 8:1 | 2 | P2/aggr | 138 | 3 |
| 17 | 炼金男爵 | 烈娜塔·戈拉斯克 | SFD·201/221 | 盲僧 | 8:1 | 1 | P1/aggr | 156 | 6 |
| 18 | 盲僧 | 李青 | OGN·257/298 | 诺克萨斯之手 | 8:1 | 1 | P1/aggr | 135 | 7 |
| 19 | 诺克萨斯之手 | 德莱厄斯 | OGN·253/298 | 皮城执法官 | 8:1 | 2 | P2/aggr | 124 | 3 |
| 20 | 皮城执法官 | 蔚 | UNL-187/219 | 荣耀行刑官 | 8:1 | 3 | P1/passive | 123 | 3 |
| 21 | 荣耀行刑官 | 德莱文 | SFD·185/221 | 沙漠皇帝 | 8:1 | 1 | P1/aggr | 127 | 2 |
| 22 | 沙漠皇帝 | 阿兹尔 | SFD·197/221 | 山隐之焰 | 8:1 | 1 | P1/aggr | 148 | 5 |
| 23 | 山隐之焰 | 奥恩 | SFD·189/221 | 赏金猎人 | 8:1 | 2 | P2/aggr | 140 | 5 |
| 24 | 赏金猎人 | 厄运小姐 | OGN·267/298 | 圣锤之毅 | 8:1 | 1 | P1/aggr | 148 | 5 |
| 25 | 圣锤之毅 | 波比 | UNL-203/219 | 圣枪游侠 | 8:1 | 4 | P2/passive | 139 | 4 |
| 26 | 圣枪游侠 | 卢锡安 | SFD·183/221 | 曙光女神 | 8:1 | 1 | P1/aggr | 132 | 3 |
| 27 | 曙光女神 | 蕾欧娜 | OGN·261/298 | 探险家 | 8:1 | 1 | P1/aggr | 132 | 3 |
| 28 | 探险家 | 伊泽瑞尔 | SFD·199/221 | 腕豪 | 8:1 | 1 | P1/aggr | 132 | 3 |
| 29 | 腕豪 | 瑟提 | OGN·269/298 | 无极剑圣 | 8:1 | 1 | P1/aggr | 148 | 6 |
| 30 | 无极剑圣 | 易 | OGS·019/024 | 无极宗师 | 8:1 | 2 | P2/aggr | 148 | 6 |
| 31 | 无极宗师 | 易 | UNL-191/219 | 无双剑姬 | 8:1 | 1 | P1/aggr | 149 | 6 |
| 32 | 无双剑姬 | 菲奥娜 | SFD·205/221 | 武器大师 | 8:1 | 2 | P2/aggr | 149 | 6 |
| 33 | 武器大师 | 贾克斯 | SFD·193/221 | 戏命师 | 8:1 | 1 | P1/aggr | 124 | 1 |
| 34 | 戏命师 | 烬 | UNL-181/219 | 虚空遁地兽 | 8:1 | 1 | P1/aggr | 126 | 1 |
| 35 | 虚空遁地兽 | 雷克塞 | SFD·187/221 | 虚空掠夺者 | 8:1 | 1 | P1/aggr | 128 | 3 |
| 36 | 虚空掠夺者 | 卡兹克 | UNL-201/219 | 虚空之女 | 8:1 | 1 | P1/aggr | 146 | 5 |
| 37 | 虚空之女 | 卡莎 | OGN·247/298 | 血港鬼影 | 8:1 | 1 | P1/aggr | 113 | 0 |
| 38 | 血港鬼影 | 派克 | UNL-185/219 | 迅捷斥候 | 8:1 | 4 | P2/passive | 121 | 2 |
| 39 | 迅捷斥候 | 提莫 | OGN·263/298 | 战争女神 | 8:1 | 3 | P1/passive | 140 | 4 |
| 40 | 战争女神 | 希维尔 | SFD·203/221 | 傲之追猎者 | 8:1 | 2 | P2/aggr | 133 | 3 |
