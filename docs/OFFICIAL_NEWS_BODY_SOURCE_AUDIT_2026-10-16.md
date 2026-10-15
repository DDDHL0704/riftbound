# 官方资讯正文、图片与新增赛前 FAQ 来源审计

本批审计日期为中国区 2026-10-16，语言 zh-CN；全球英文赛前资料独立记录为 GLOBAL_EN/en-US。**全量目标仍为 NOT READY**。来源取得、限定语义事实、实现、测试、固定提交 CI 和产品验收分别记录。

## 本次取得与保留

官网资讯列表重新分页取得全部 138 行，原始身份和元数据未发生增删。原 20 个候选和 118 个未分类身份完整保留。118 个新尝试中取得 113 个传统正文根、4 个静态图片帖；另一个入口是小程序内部路径，仍未取得内容。4 个图片帖通过静态字面量解析识别，没有执行外部脚本。原失败收据保留。

原来受环境验证页阻挡的《赛事规则上线，赛事详情全解析》经普通 HTTPS 重试取得 5,495 字及 11 个图片引用。原列示发布日期为 2025-08-13；本次取得时间不改变其历史适用范围。两个旧官网页面仍发生正常 TLS 失败，小程序内部路径和这些旧页面继续是获取缺口。

[118 个正文归档清单](evidence/news-body-source-observation-2026-10-16/all-118-article-archive-index.json)保存可用原始响应和文本的 gzip、原始字节哈希及各次取得时间；旧 16 个正文原档仍保留。[新增 IMG 清单](evidence/news-body-source-observation-2026-10-16/all-known-news-img-acquisition.json)的精确范围是新 118 个身份的 1,020 个引用，加上后来恢复的历史文章 11 个引用，共 1,031 个引用、986 个唯一地址。原始字节均已取得，65 个文件含动画帧，逐帧审查未完成。初始一个 SVG 被栅格解码器拒绝的记录保留，并追加原始 SVG 已取得的格式识别证据。

[原 43 个 IMG 及 CSS 补充清单](evidence/news-body-source-observation-2026-10-16/additional-prior-img-and-css-acquisition.json)另行保留原候选文章的 43 个 IMG 引用，以及 [130 个正文根的静态媒体清单](evidence/news-body-source-observation-2026-10-16/all-130-article-transitive-media-inventory.json)定位的 159 个 CSS URL 引用。其缺失原始文件已补取。721 个内联 SVG、146 个 foreignObject、5 个 iframe 和 4 个视频组件均保留待审。以上不构成完整渲染、视频、QR、动画或动态内容覆盖。

## 已读范围及规则冲突

本批实际读过的额外范围包括成都赛前提醒全部五张原图、四个静态图片帖的原图与完整说明，以及新全球赛前 FAQ 的四张配图。成都五张图为历史活动的交通、核销和场地信息；四个图片帖为历史预览、地区介绍、试玩牌组抽选或发售日历。在这些指定表面没有发现新的可执行玩法条款，不提升为整篇动态内容或整卡认证。

中国区售前现开海报要求主牌堆正好 25 张，并把限定黑默丁格赠卡列入活动卡池；现行 DOC-02 的 602.4.a.2 要求至少 25 张。DOC-02 第 21–24 页的实际读取及来源哈希见[日期与赛制对照](evidence/news-body-source-observation-2026-10-16/current-doc02-date-and-format-reconciliation.json)。地区、活动变体和权威优先级尚未闭合，未改变通用构筑或现开实现。

[新发现的全球官方赛前文章](https://playriftbound.com/en-us/news/announcements/preparing-for-the-radiance-pre-rift/)列示发布时间 2026-10-12T16:00Z，包含四条 FAQ，并要求该赛事不使用黑默丁格赠卡。此文章不在原 20 个全球规则来源中：旧抓取器只递归命中特定规则、勘误、FAQ 路径，其标题路径没有这些标记。此前“20 个既有正文未变”结论保留原范围；新来源单独登记为第 21 个，不把全球赛事要求直接作为中国区规则。

[新来源及四条 FAQ 身份](evidence/news-body-source-observation-2026-10-16/global-prerift-source-delta.json)绑定六个实际可读的英文印次号；它们均已存在于原 1,740 个来源身份中，没有新增或合并印次。四条限定事实涉及奎桑提强攻与坚守、妮蔻额外传奇数量、炫音返场不同模式选择，以及发条将装备变为单位后的装备文字。共享消费者、地区适用性、实现及各层验收分别 OPEN。

妮蔻的“最多三名”也由当前中国区 DOC-03 第 3 页独立确认。当前中文图鉴仍列“三名”，该印次仍为 preview；新证据记录卡面与勘误的差异，没有激活 RAD 标准环境或改写既有运行目录。

标题为《核心规则Q&A》中文版的旧资讯实际指向[当前规则中心](https://playloltcg.com/rules.html)。本次核对 HTML、两个请求脚本、CSS 及实际 API，映射当前 14 个下载 URL 到既有中文文档。[别名对照](evidence/news-body-source-observation-2026-10-16/cn-rules-hub-alias-reconciliation.json)只闭合当前路由映射；历史独立 Q&A 正文仍未取得或证明与某份现行文档相同。

## 固定候选与验证边界

固定 `72f8bdc2e0318938fe323ba66462a323aa02ec8c` 的 [CI 38805110458](https://github.com/DDDHL0704/riftbound/actions/runs/38805110458) 已实际成功，四个下载产物均核对 GitHub 摘要。[独立 TRX、SQL、Git 输入核验](evidence/news-body-source-observation-2026-10-16/full-ci-72f8bdc2.json)确认 12,052 用例 / 7,348 方法、69 个真实 SQL 场景通过，保留全部原 12,040 用例 / 7,341 方法、143 个完整对局方法、110 个配对及种子 424242。468 个游戏输入路径全部保留，实际只有规则来源范围和三个引擎文件改变。不同构建的实现身份及 110 个终态哈希分别保留，不声称跨构建哈希相同。

该 full 覆盖固定 CN16 来源采用及出牌来源修复；本批新增资讯语义不在其认证范围内。两个原生 CI 包含实际释放控件回归的通过标记，但当前 CN16 可见双客户端仍被原生工具报告的 Mac 锁定阻挡，尚未开局。Windows 实机独立 OPEN。旧 CN14 可见对局和数据库未迁移、未改写。

本批没有修改游戏实现、测试、运行卡牌目录或 rule-input-scope。来源记录与剩余义务合入当前 manifest，所有原审查和来源日期保留。本批唯一一次 check 实际 11,903 用例 / 7,207 方法通过，runner 总耗时 417.60 秒（测试分片 381.05 秒），原用例多重集合与方法完全相同。启动前冻结的全部 11,888 个输入在 runner 完成前无漂移；55 项 Python 检查通过。实际收据见 [batch-check-result.json](evidence/news-body-source-observation-2026-10-16/batch-check-result.json)。结果记录仅在运行完成后追加，不宣称这些后写报告参与此前冻结。治理检查为 110 个限定审查 / 16,491 个剩余义务，不提升语义。新提交 CI 与后续开发仍独立推进，任何局部结果都不完成全量 Goal。
