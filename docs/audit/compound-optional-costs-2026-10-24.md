# 获授急速与自身可选费用的组合
- [Issue122](https://github.com/DDDHL0704/riftbound/issues/122)，直接堆叠PR依赖#127的6fb585e0可选减费机制；六文件原增量来自completed-source-v19，只适配减费提示标签所在旧上下文，不导入S7或原长分支。
- 固定CN核心PDF d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11：356.2额外费用任意顺序、356.4组件减免及零费已支付、805.1/805.2急速与付款后延迟替换。精确9印次实时文本/费用/特性核对仍在var/qa/compound-optional-costs-20261024/source-v3/。
- 分离唯一规范急速选择后，完整递归校验自身费用；费用组件、下限与减免独立。具有其他可减资源费用时急速用HASTE_READY、自身仍ADDITIONAL，单项提示与组合提交身份稳定，省略选择不减费，重复/错trait/无关/缺费/重复减免原子拒绝。
- 按①同方法协调分配，仅将ValidPermanentPlayTargetStack的一处费用消费改为helper；保留其余确认、印次、独立目标、priority/pending和代际条件。原PlayKeywords特性派生抽取共用，避免用当前aura撤销已付款事实。
- helper验证已确认结构；真实授予和已付款仍由原initial/raw日志重演核定。16个协调frame负控保留；其中两个伪造已付款帧普通形状可过，但原日志终态校验必须拒绝。不加DTO、不迁移/清洗旧RIF或日志。
- 新73例覆盖7张实际费用消费者、真实法术前置、选项顺序、提示/quote/扣款、独立减费到底、零池/两符文/不足、正常及协调frame与原日志。父生产6fb+相同73测试25PASS/48FAIL，WIP最小前置73/73；首轮assert有效但逐例导出变量名错误的收据保留，最终收据另在var/qa/payment-prs-20261024/compound-costs/。
- 这些为明确中局夹具及相邻回归，不宣称新增普通出生组合费registry/SQL/API/全后端认证；正常registry原正控继续保留于既有机制测试。按最新人类约定以定向及实际规则CI构成L2。
- 当前候选已同步main #131 `d713adad568999baa8703750b7893ec234c63d4c`。实测源 `221f9926cdd8d0e046233432753b7413b51b1dfb` 为786/786、0skip，原417与父591身份均保留，含原四个正常Core空白开局/冷registry正控；73例测试源码逐字保留。共享永久物目标守卫整文件仅一处获分配费用表达式，逆向可逐字恢复main。五实际模块、230/568PDB、退出RuleInputs及运行catalog/scope核对实测源，identity `bf15f3815afef20effa7fb86ef7183ff0d0a66dc69f092b33bce8597e77e1d0a`。运行证明 `var/qa/payment-prs-20261024/compound-costs/post-resource-main-source-runtime-proof-v19.json` SHA256 `15fb3989b3d5d99e6aa041325f244eff2638d12c55ee347f6b4184a5a52e4982`；发布头仅另更新本审计，模块身份不改标。
- 新输入73份独立导出包含45份明确原日志回放、12份拒绝不变性及16份协调frame负例；12个旧单费对照的原wire值/属性顺序、quote与事件相同，22行组件选择保留。16份正常原回放真/伪造回放假，其中两份伪造普通形状可过仍由原日志终态拒绝；拒绝样本不用不存在的回放字段证明。案例证明 `post-resource-main-case-proof-v18.json` SHA256 `b533c60e403ef1789ef184661f16bbf21efbef987642ec8a3fe1f5de790a4508`，原检查器字段假设失败也保留。另有64追加入口runtime/122Core原日志冷registry收据，范围独立。旧基底L2仅作旧输入证据，当前head/base/synthetic仍须最新实际L2通过后合入。
