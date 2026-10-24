# N14 · VEN010 蚀骨诅咒

显式依赖已交付的干净PR151/head `2457f835c0f760b06e0379be92881ee89e781d7f`，其祖先为main `8ac0afba21574c94e7ce8a03ed83704524fee8e7`，不导入旧WIP祖先。精确CN37695/VEN010/166 SC，红色2法力迅捷，战场单位基数2伤；结算时当前法术控制者的废牌堆每张已知完整同名牌额外1伤，随后保留既有加成/防止流程。
2026-10-24重取官网API及完整卡图，010记录3c0278c3/图fe88da69与旧核读一致，QA/勘误空，日期UNKNOWN。原同名阳性对照CN37984/VEN086与CN37985/VEN181也重取并查看全图，均为普朗克—海上霸主；两条基础单位登记仅身份前置，C7目标替换语义仍未认证，原untracked测试均不纳入本包。跨语言映射UNKNOWN。
父包已提供原用例所需VEN008及VEN093/122，新增仅010与两GP精确来源/基础登记。总相对main新增6条，原1040及父包1043均逐项/顺序/scopedAdditions保留；总1046/功能元数据845，重复115/316/201保持。相同全名不等于合并功能身份，原分组规则不变。
共享差量只默认0的ExtraDamagePerSameNameInControllerGraveyard、ResolveDamageAmount原基数后checked增量与zero gate，以及SameKnownName查询原Names表；旧DisplayName、identity grouping、费用/时钟/死亡/协议不变。去掉本卡9行Core差量及6行name helper分别与PR151逐字节相等。两开发线当前相同方法均与父包同SHA61c6e441，未观察到该接点并发修改。
原14方法34例全部属性/数据/语义断言保留，仅两个公开初态At调用采用本地合法占用/控制夹具；真实命令驱动、当前控制者、回应后重计、同名全副标/未知拒绝、Ward/对决/加成、反制/回手代际/原日志篡改保持。旧366/366属于旧输入。
本候选构建/定向/实际L2待验；QA `var/qa/cards-direct-pr-migration-20261024/n14-main-port-v1/`，当前来源/视觉收据在n14-current-source-v1，旧原件 `var/qa/ven-010-graveyard-name-20261024/`。净整卡0/后端NOT READY。
