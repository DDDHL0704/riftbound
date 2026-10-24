# 既有预构筑补牌池稳定性

当前组合固定源 `944e01e82c3b80d448f92c53950e86860c719bf6` 已干净吸收main `d713adad568999baa8703750b7893ec234c63d4c` 的入口与主动符文资源规则；ready仅更新本文。编译0警告/错误，单线程173/173、零跳过：原83例完整保留，新增main124原入口64例和main131主动资源26例，无交集。原23例中的11套双格式精确输入、5种自定义配方及3个原失败开局恢复方法展开6例均保留，源输入夹具SHA仍为下述242e8dc2。

自有PreconstructedDeckCatalog及PreconstructedFillerScopeTests与此前03dc逐字相同；PermanentPlayTargets完整文件与上述main相同。2497个编译输入/1418个运行文件全量哈希稳定，四模块InfoVersion指向受测源；Engine MVID `5f5a39f8-62b2-4587-af31-7d3ceeb046a1`，六个原未跟踪文件不变且未编入。ignored新原件 `var/qa/cards-direct-pr-migration-20261024/l2-failure-pr159-pr162-v1/pr139-maind713-v1/` 保留固定源/bin、实际TRX、清单、module-identities.json及已通过verifier；`source-runtime-proof-v1.json` SHA256 `275a3e6b4f746e7c9fe4694ae0e3f2eaa943eb8a575a722b727018e876e254ad`。下述旧83例和各版L2结果保持原输入身份，不求和或改标；最新实际head/base/merge-result的quality、targeted、rule_suite及required仍须全部通过。未新启本机full/SQL/网络/矩阵，整卡认证增量0、backend NOT READY。

A7 固定源 `da80739bd17631e231a17dccced06d306b26fa8d` 的原失败指出 `CoreRules/lillia-lowcurve` 配方变化。单FQN在A7重新复现失败，在 main `8ac0afba` 原样通过；两个输入和原失败均保留。
对A7真实程序集调用构造器并逐一比较两赛制全部11份配方，确认实际变化为4个牌组ID × 2赛制，共8份配方；自动扫描新注册的低费单位使每份加入3张 `VEN·043`，替换原来的牌。此为构造输入漂移，未据此推断151个完整对局的业务验收。
本修复仅把既有定义的自动补牌范围限制到原配方中的71个精确主牌牌号，继续执行原来的已实现单位、费用、目标、颜色、赛制、牌名张数和合法性校验。新增消费者不再自动替换既有配方，显式必选牌的配置保持。
来源为 `preconstructed-original-4fca-inputs.json`，源提交 `4fca367eaab1dd198ffde839b23461c058a2b470`，SHA256 `242e8dc256809454c82b03b11397b8b35477f1d07ff6bd13bfd7e3dfb1c610aa`。该原件、11份输入、110个配对及seed均未改写；来源指向现存输入，不建立新的官方适用性判断。
main固定源码 `7ed2547715ffd388ff650802f43edad71c15e275` 编译0警告0错误，原完整FQN单独1/1通过，核对两赛制各11份原输入；冻结1417文件。
A7原样基线再次复现原失败；仅覆盖本修复两个文件的私有固定源码 `5d55822066b3577d68bb484347746e88ca397306` 编译0警告0错误、同一FQN单独1/1通过，冻结1425文件。原输入SHA保持，A7覆盖树不作为主线PR祖先，两个输入分别记证。
实际L2 CI待通过。证据位于 `var/qa/a7-owner4-main-triage-20261024/preconstructed-filler-pin-v1/`；原ZIP与原TRX保持。未重跑110配对矩阵、SQL或网络；此修复不代表整卡或完整后端认证，后端/产品 NOT READY。

2026-10-24 L2 后续：PR159/run40077028269 与 PR162/run40079191311 的原 ZIP/TRX 发现该白名单还误限了反射调用的通用配方构造器，5 个开局恢复用例在红黄传奇 SFD·187/221 处不足40张。修复源码 `71125bf0ebeba15f3d445d0bd9ca6b2e7fc621f7` 将白名单参数只传入原11配方路径；通用构造器继续使用完整合法低费池，颜色、费用、名称上限和赛制校验保持。

固定源码编译0警告0错误，17例（原11配方两赛制精确输入、原预构筑验证、新5种自定义配方）与原3个开局恢复方法展开6例分别全过，23例无交集、零跳过。原输入SHA仍为 `242e8dc256809454c82b03b11397b8b35477f1d07ff6bd13bfd7e3dfb1c610aa`；源码/完整运行目录两轮后哈希稳定。证据：`var/qa/cards-direct-pr-migration-20261024/l2-failure-pr159-pr162-v1/filler-scope-v1/`（source-runtime-proof-v1.json、两份原TRX、固定源/bin）；失败运行ZIP和job日志在其上级目录。实际最新合并输入L2仍待执行核验；目录统计及另一个弃牌节点失败由独立后续处理，不将23例称为L2通过。

旧base的实际L2 run40085771643已通过：head5f5e52b8、basebb8b7a17、受测merge `27a30f4edb2c323a190cf47898994815da8da6a8`；quality/targeted/rule_suite/L2 required均成功。已下载四份原ZIP、核对平台digest，并用未改写的实际discovery/plan/TRX重跑相同版本门禁，结果逐项等于原result.json：focus9938例/5726方法、rule_suite14041例/7697方法，均零跳过；两组有交集，不相加。计划记录的测试程序集SHA一致，上传产物未含该程序集二进制，故只核对了记录身份与实际TRX计划覆盖。main已前移，该旧绿灯不作为新base合入依据。

新组合受测源 `0cc370e408fba506f3fc5795ff84e0435d9b83ca` 已吸收main `29367aaaa1b154d2b7843f8b8645d3cc64133c84`，无合并冲突；自有补牌生产/测试文件与旧5f5e52b8逐字相同，③PermanentPlayTargets文件与新main相同。编译0警告0错误，83/83、零跳过=原23例全部保留+main127的60个可选费用/目标时机相邻例；原11套两赛制精确输入、5种自定义配方及3个原失败开局恢复方法展开6例通过。2491源码/1418运行文件全量哈希稳定，四受查程序集InfoVersion指向新源，Engine MVID `fc2a8c9b-49f9-4c48-bd82-c4ecae07df24`。

新原件 `var/qa/cards-direct-pr-migration-20261024/l2-failure-pr159-pr162-v1/pr139-main293-v1/`，source-runtime-proof-v1.json SHA256 `401c5df4f5467812c32cfd9d59808de57edb164e80bbc140c1c32fc2db510594`；旧绿灯的四ZIP与独立门禁回验在相邻 `pr139-green-old-bb8-v1/`。本受测源之后仅更新本审计doc，新输入仍待其实际L2通过；无新full/SQL/网络/矩阵及整卡认证增量。
