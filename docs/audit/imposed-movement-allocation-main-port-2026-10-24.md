# 施加移动费用的实际符能分配 · 2026-10-24

- Refs issue114；依赖PR138/62224b07，代码提交c545830558ecd8d6d2b43e574510fd0cb1db073a。仅改变CoreRuleEngine.ImposedMovementPayment.cs并新增测试，原测试逐文件不变。
- 固定CN核心PDF `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11` 的135.2.e.5.a/b、204.4、430.3、445.1-2；[原PDF](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf)。2026-10-24T06:43Z实际POST官方API复核id34711/UNL-163及34583/UNL-054，精确记录与前包一致，status1/非preview/QA=null/errata空；未提供的发布、生效日期保持UNKNOWN，不推断全站无新来源。
- 原父源622的6例为2P4F：唯一分配控制通过；两条标准移动缺少支付窗口，两条效果移动缺少分配项。保留原件与两个方法/六行语义断言，不把四红称为四次错误扣款。
- 固定费用有多种实际分配时，标准移动进入已有IMPOSED_MOVEMENT_POWER_PAYMENT；唯一分配仍直接完成。保留PAY/DECLINE/资源，增加ALLOCATE_IMPOSED_MOVEMENT_POWER、CONFIRM_IMPOSED_MOVEMENT_POWER、CANCEL_IMPOSED_MOVEMENT_POWER。逐桶有界选择，确认前不扣费、不移动；可返回父菜单。
- 不新增DTO、命令、Frame、clock或通用付款算法；数量选择锚定公开移动单位及代数，费用由服务端重算。手动分配复用现有genericPower/powerByTrait事件字段，固定总费、真实付款人、来源、群组和目的地均保留。
- 新模块59/59与同一不可变DLL的相邻回归342/342互不重叠，共401/401、0skip；包括五种两点组合的两条移动路径、七类符能、65个实际巡管实例、取消18例、非法选择13例、恢复篡改3例、原命令/事件完整重放及真实trusted Restore/重复不写入。
- 蓝红四例及唯一分配两例从空池经真实命令获取资源；其他钱包为显式中盘边界。最大池的标准移动预览为int.MaxValue，效果移动先付原法术费后为int.MaxValue-1；没有声称新增普通出生、整卡或完整后端验收。
- 原件根 `var/qa/payment-prs-20261024/movement-allocation-114/`：parent138-baseline6-v1、fixed-new-module-v2、same-dll-affected-v3与source-refresh-v1；完整索引fixed-source-runtime-proof-v5.json。实际加载路径/SHA/MVID/RIF均保留；native构建时HEAD为622+WIP，3059个输入逐字等于c545提交，Info未改写。
- 既有冷拒绝码缺口仍OPEN：12个INVALID_TARGET和1个PROMPT_EXPIRED在恢复后变为UNSUPPORTED_COMMAND；状态、事件、扣款及重试写入边界成立。其他费用窗口issue118、实际L2规则CI与main集成仍待；净整卡认证增量0，Goal保持active。

- 当前同步父 #138 `656aff9f95361a960b263af662e7e9d7f7e443a3`（含CI主线 `fb426513181d45551241de17eee86ab9a6702ba8`），固定实测源 `d30199944b1afb3481731721994ef2eae2202fbb` 为1638/1638、0skip，原59与342两个互不重叠集合共401身份、父1516身份全部保留。生产仍仅原ImposedMovementPayment模块；两个新增测试文件及实现与旧头逐字一致，所有父既有测试/FullGame/DTO/clock未改。
- 五实际模块、239/590PDB、退出RuleInputs、运行catalog/scope以及59个分配场景的实际runtime均核对源，identity `9ce22bd9d212a7640d988c0b3568f3067b556905971af32f4d0e8619c7fc26c4`。64追加入口runtime/122Core原日志冷registry收据与分配案例分开；原六行基线2P4F及旧622+WIP运行身份/3059源绑定完整保留，不改标当前父。
- 当前13份原始cold-rejection导出仍为12个INVALID_TARGET和1个PROMPT_EXPIRED在cold变UNSUPPORTED_COMMAND；都仍拒绝且0事件，原语义/状态/重试断言保留，但拒绝码恢复仍等待#157，未作为完整冷恢复通过。没有新增普通组牌出生/SQL/网络/进程重启或整卡认证。
- 证明 `var/qa/payment-prs-20261024/movement-allocation-114/post-parent-source-runtime-proof-v9.json` SHA256 `7886c164469eac2f38d1da72ffa8fccd6540b896237296fcdaf0edc2cdd94fed`，含原TRX、401身份逐项保留、归属、实际模块/数据与13份已知缺口原件。发布头仅更新本审计，MVID/RIF仍归实测源；父链合main后仍须最新实际L2。
