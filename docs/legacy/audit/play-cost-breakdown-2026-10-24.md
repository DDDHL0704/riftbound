# 待命与替代费用的预览明细
- 问题：[Issue116](https://github.com/DDDHL0704/riftbound/issues/116)。独立PR以main 8ac0afba为基底，只移植7d5f5994的三个文件，不合入原费用长分支。
- 只补PreviewPlayCard的待命基础法力/符能减免、效果免除基础符能与SourceReplay替代符能说明；应付、扣款、候选、DTO形状、时序及恢复实现不改。
- 来源沿用固定CN核心PDF d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11 的356.1.a–b、811.1.b；手雷来源docs/evidence/dancing-grenade-2026-10-14/source-current.json。不是新来源全集或整卡认证。
- 新4方法10例包含真实待命跨回合、手雷再次打出/法盾/不足、观察后的替代或免费打出、普通/急速对照；原340生产3PASS/7明细FAIL及首次夹具错误均保留在var/qa/play-cost-breakdown-20261024/，不把夹具错误算生产缺陷。
- 新main基底移植2a4b9701已重新构建10/10、0skip；新分支的后续定向/相邻结果、原始请求与程序集身份保存在var/qa/payment-prs-20261024/issue116/，旧长分支90例仅作历史证据。
- 10个中局场景逐步检查正常帧、恢复、冷重复和完整原日志；属于明确中局夹具，不声称普通出生或真实网络/数据库验收。
- 按2026-10-24最新人类约定：定向与规则套件构成L2，实际PR规则CI未绿前不合入；真实网络/数据库/重启等L3归夜间固定main SHA，不阻塞无关日常修复。
- 当前候选已同步 main #131 `d713adad568999baa8703750b7893ec234c63d4c`，生产增量仍仅预览文件10+/1−。固定实测源 `e5b3196ecb29853d58bb4dbd97709d054885ac24` 组合666/666、0skip，原237与父591身份均保留；发布头仅再更新本审计，实际模块/PDB/RuleInputs仍归属实测源。229/568个PDB文档、五实际模块、退出RuleInputs与运行catalog/scope核对该源，identity `823970c129ac1bc66392062f88070aa6b7291c7284659961ae5f834598b95623`。十个明确中局场景的原日志回放、64个追加入口runtime与122份Core原日志冷registry收据均保留；来源与整卡边界不扩大。证明 `var/qa/payment-prs-20261024/issue116/post-resource-main-source-runtime-proof-v14.json` SHA256 `f43bf9a554ea178f3fc40e64da71cfe1e08c19cc04921bb67cc98d21d453f0bb`。旧ceda绿色L2只对应旧基底，当前head/base/synthetic仍须最新实际L2通过后合入。
