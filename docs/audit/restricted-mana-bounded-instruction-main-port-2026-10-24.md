# 受限法力与有界结算付款的独立移植
- 本地最小依赖基底40031df2只组合已交付PR127/6fb585e0、PR131/7d5aea30及精确SP6来源e729ab7d；再选84e9ccd0、ba57eb17、007a72da、d430fa77、e77857d2。不是整条旧费用分支。对PR127的差异暂包含PR131前置，父项合入后可对main核对实际增量。
- CN固定核心PDF d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11及原受限mana/S5来源原件仍限定CN/zh-CN和具体印次，原件位于var/qa/payment-restricted-mana-20261024/source/与既有指令审计路径；不把快照当全站无新变更证明。
- 仅OGS014与VEN SP6的该资源技能将SpellOnlyMana存入符文池，剩余保留到规则规定清池。仅服务端明确的打出法术用途可以支出，单位/装配/能力/指令付款不因客户端伪造purpose获权；预览暴露实际可用受限量与余额。
- 变值符能在每次结算指令及每次Echo时选择/付款，施放不预付该指令符能；保留0与小额单色入口。取527352fc精确指令/分配源码与40例，65630619的区间二分及两个常量，不引入整包S7施加移动。数量菜单阈值64，闭区间最多31步；多钱包逐桶分配、确认或取消不枚举组合。
- 按已协调边界，仅在ResolveCoreAsync的Complete局部方法收集前及整个延续循环后各加资源范围守卫，恢复入口逐玩家校验池投影及数值；不替换战斗/死亡收集。原日志、RuleInputs与独立原日志审计保留，数值越界拒绝原状态不发生部分扣费。
- 原P7方法/Theory保留并沿e77857d2校准为结算付款后核原伤害/余额/来源，旧失败保留；25个开局partial-wire修正由独立PR134交付，不拼成本包结果。旧S6/SQL/网络数据只作历史，不代表本移植main或最新L2通过。
- 801256d6仅选网络报告的实际JSON enum序列化选项，未虚构缺失的reconnect审计调用；issue105的旋转会话/SQL证据仍单列OPEN。现有独立SQL/网络入口仅做编译检查，本机不重开真实数据库/网络/重启矩阵。
- 当前固定源码/模块身份、定向及相邻TRX、编译预检与原失败统一保留于var/qa/payment-prs-20261024/restricted-mana/。按最新约定，合并仍须实际L2 rules-suite CI；L3由夜间固定main SHA承接，本审计不关闭整个后端Goal。
- #131 已按实际L2合入 main `d713adad568999baa8703750b7893ec234c63d4c`。本分支合并该主线后的所有跟踪文件与实测源 `26e0e424e2c9fc0c83cf86007b169ad254c28259` 逐字相同，tree均为 `c1a86b087aceb42cb13feb16eee868149ffbb4bc`；发布头只再更新本审计。原生839/839、原733身份/原10个P7失败身份/40个精确分配以及三个独立入口编译结果可复用该相同源，实际MVID/RuleInputs仍归属26e，不改标新头。Complete仍只保留收集前/整个延续循环后的两处资源守卫；资源视图检查保留在已协调的非空snapshot分支、原turn/stack检查之前，后续集成保留④的null-stack保护。源/运行时证明 `post-main-source-runtime-proof-v14.json` SHA256 `4c0054892f9055c6f0b1172944494d70c9b3271709a0790b9bacc70410fa8039` 与 `post-main-merge-tree-equivalence-v15.json` 均在上述ignored目录保留，转main后仍须新head/base/synthetic的实际L2通过。
