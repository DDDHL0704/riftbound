# C10 · 空技能队列的战斗结果阶段与征服来源
基线 `606bb8d44726131061ba8fd3dff1261be650cb8b`；L2，共享时序及既有事件适用路径扩展；本地定向完成，真实SQL/网络与远端门禁待验，净整卡认证增量0。
依据：[CN核心](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf) SHA256 `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`，373.2/468.1–468.7/473.1.b.1/474/808.1.d.1；精确卡面/雷恩加尔权限来源沿用C9与N55的限定证据。
阶段修复：所有已确定胜方复用既有清伤→结果→控制/征服→结束续体，空技能队列也产生既有 `BATTLE_RESULT_DETERMINED`/控制阶段事件；保留原无胜方分支及普通胜利即时截断，无新增DTO、事件种类或全局完成时钟。
来源修复：仅同场、同胜方续体优先原声明顺序中的当前公开代际/占场/控制者，再回退全部当前占场者；不丢其他单位的征服技能，非战斗控制流程不变。
有限QA：私有 `BuildBattleWinnerPhaseScenario` 七个固定id为 `battle-winner-phase-{plain,guardians,final-conquest,result-burnout,defender,skipped-damage,control-observer}`；战斗单位/装备/法术从手牌真实打出，真实符文付款；最后一分另有非战斗占场者支持实际守住得分，烧牌模式有显式分数/牌库例外。
夹具校准：三个原方法恢复所断言的新控制权及决斗结束/真实位置前提，原方法、Theory行和最终断言保留；原7项相邻失败、独立反序来源红测、初始错误预期均留存，不把所有异控防御状态判为非法。
最终本地定向 **82/82（34方法）**，含C10、原声明/所有征服者/薇恩费用、德莱文死亡及结果顺序、原结果恢复、重复征服与最后一分；实际加载Engine与冻结输入一致。原143完整方法/151例、11牌组110配对及seed424242未修改，未在本地重跑。
编译叶证明 **12流程/211命令**，每命令JSON冷恢复/幂等/显式初态原日志回放，实际支付/弃牌元数据、四个实际终局伪造配方及待选控制技能完成；另 **20/20** 纯留证/拒绝层/隐藏身份守卫，实际加载路径、SHA/MVID可取回；net8网络叶编译0警告/0错误。
SQL叶7房/网络叶5房；真实Joined令牌与预登记Error严格等待、所有RPC/SQL/视图等待受回复故障守卫，Dispose后最终审计与trace才准许PASS；终局当前新意图MATCH_FINISHED不落库，旧提示PROMPT_EXPIRED另记；RR只读七表采样，逐mode先留实际拒绝/异常/还原，再严格断言，原历史不改。
边界：真实雷恩加尔返回/重打证明旧来源离场/换代；错场及变控仅表示层选择器反例。两符文正常召唤导致的PRIVATE前缀误判已按实际私有区/已接受回合动作修正，原失败保留；尚未执行ROOM_SEAT出生审计、真实PG、网络RPC或API重启。
原件：本工作树 ignored `var/qa/guardian-combat-continuation-c10-20261024/complete-delivery-receipt.json`，含四项真实阶段原红、来源顺序独立红、获批精确patch、实际source/bin、最终TRX与叶子原件索引；先前较弱断言的7绿不替代真实红测。
①待办：统一接七个有限DEV入口、`BattleWinnerPhaseSqlAcceptance`/`BattleWinnerPhaseNetworkAcceptance` 的Program/manifest/runner及既有QaNetworkReplyAudit依赖；按后续固定候选补L2机制/相邻/受影响原完整局、真实PG/双玩家API重启/原日志与安全门禁，未知路径按实际规划升级full；不混入在途A4，不结束后端Goal。
