# A7 开局提示验收的 PLAY_CARD 原始载荷
- 历史 A7 da80739b/run40055102708 的25个开局失败条目共享旧载荷构造；OfficialOpeningTests源码SHA256 02cf6303e825771e620e5de9c1a803a8cb3505b82c524d093da507147433073b与本移植基底PR121/d9a53da逐字节相同。
- 原载荷只有cmdType与部分promptId/snapshotTick，没有实际PLAY_CARD目标数组；目标结构校验先于旧提示判定，因此先报INVALID_PAYLOAD。基底最小三个族21例为19过2败，原预览族已由PR121通过，不另改其源码。
- 仅在18个相关调用处补入实际typed PLAY_CARD序列化载荷；保留每个原始promptId/snapshotTick的存在性和值以及其余raw字段。其他命令直接返回原载荷。
- 全部原Fact/Theory/InlineData及逐行Assert保留；不更改错误码、生产入口、恢复规则或已接受历史。缺失/恶意目标载荷仍由原PlayCardTargetsIngressTests验证拒绝。
- 当前定向族、原始失败、输入快照、模块SHA/MVID及TRX保留于var/qa/payment-prs-20261024/a7-opening-wire/；只复现和修复本边界，不以历史A7或本机定向替代新L2规则CI。
