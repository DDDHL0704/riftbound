# 获授急速与自身可选费用的组合
- [Issue122](https://github.com/DDDHL0704/riftbound/issues/122)，直接堆叠PR依赖#127的6fb585e0可选减费机制；六文件原增量来自completed-source-v19，只适配减费提示标签所在旧上下文，不导入S7或原长分支。
- 固定CN核心PDF d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11：356.2额外费用任意顺序、356.4组件减免及零费已支付、805.1/805.2急速与付款后延迟替换。精确9印次实时文本/费用/特性核对仍在var/qa/compound-optional-costs-20261024/source-v3/。
- 分离唯一规范急速选择后，完整递归校验自身费用；费用组件、下限与减免独立。具有其他可减资源费用时急速用HASTE_READY、自身仍ADDITIONAL，单项提示与组合提交身份稳定，省略选择不减费，重复/错trait/无关/缺费/重复减免原子拒绝。
- 按①同方法协调分配，仅将ValidPermanentPlayTargetStack的一处费用消费改为helper；保留其余确认、印次、独立目标、priority/pending和代际条件。原PlayKeywords特性派生抽取共用，避免用当前aura撤销已付款事实。
- helper验证已确认结构；真实授予和已付款仍由原initial/raw日志重演核定。16个协调frame负控保留；其中两个伪造已付款帧普通形状可过，但原日志终态校验必须拒绝。不加DTO、不迁移/清洗旧RIF或日志。
- 新73例覆盖7张实际费用消费者、真实法术前置、选项顺序、提示/quote/扣款、独立减费到底、零池/两符文/不足、正常及协调frame与原日志。父生产6fb+相同73测试25PASS/48FAIL，WIP最小前置73/73；首轮assert有效但逐例导出变量名错误的收据保留，最终收据另在var/qa/payment-prs-20261024/compound-costs/。
- 这些为明确中局夹具及相邻回归，不宣称新增普通出生组合费registry/SQL/API/全后端认证；正常registry原正控继续保留于既有机制测试。按最新人类约定以定向及实际规则CI构成L2。
