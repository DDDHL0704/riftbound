# Drawings 来源并行核查：韩文 T1 预览印次

本批接续 #94 的原 17 个来源分组。在既有六个英文图像印次之外，取得并实际阅读 **五个韩文 T1 Player Bundle 预览卡面**：原图底部逐张印有 `T1A-001/005` 至 `T1A-005/005` 和语言标记 `KR`。这五个韩文观察附属于原五个 T1 分组；不增加或合并公共卡牌账本，不把 17 组当作 17 张新卡。

## 新证据

当前 [Drawings 英文公告](https://playriftbound.com/en-us/news/announcements/riftbounds-2026-drawings/) 条件请求返回 200，完整正文与既有记录逐字相同，五张正文原图 URL 相同。原五图哈希已复核并直接复用。其官网明确列出的简体、繁体及韩文 alternate 链接均实际返回 404；首次异常记录和后续取得的完整 404 原响应分别保存，不能根据链接存在推定对应语言卡面已发布。

从 [Products and Sets into 2027](https://playriftbound.com/en-us/news/announcements/products-and-sets-into-2027/) 的正文链接，进一步读取 [T1 英文原公告](https://playriftbound.com/en-us/news/announcements/the-riftbound-x-t1-2025-worlds-champion-collection/) 和 [T1 韩文原公告](https://playriftbound.com/ko-kr/news/announcements/the-riftbound-x-t1-2025-worlds-champion-collection/)。后者实际可访问，Player Bundle 段引用了独立韩文原图，SHA-256 为 `cf7e005036884a494275955cf3ab025768c91ed8e8262985f43b9b5061c85e3a`。五张完整卡面上的韩文名称、编号和 `KR` 均由原图直接阅读，未从英文版推断。

| 原分组 | 原图编号 | 卡面语言标记 | 韩文名称 / 副标题 |
|---|---|---|---|
| T1_PLAYER_AMBESSA | T1A-001/005 | KR | 암베사 / 늑대 |
| T1_PLAYER_XIN_ZHAO | T1A-002/005 | KR | 신 짜오 / 경계 |
| T1_PLAYER_GALIO | T1A-003/005 | KR | 갈리오 / 불굴 |
| T1_PLAYER_MISS_FORTUNE | T1A-004/005 | KR | 미스 포츈 / 해적단 |
| T1_PLAYER_SERAPHINE | T1A-005/005 | KR | 세라핀 / 유대 |

两个 T1 原公告明确说明 Player Bundle 与 Signature Edition 为同五位选定英雄、采用不同插画。这仅作为官方产品关系保留，T1A / T1S、EN / KR 和尚未取得的中文印次不合并。原公告还明确称卡图为渲染预览，实物可能略有不同；本批不认证最终实物，也不认证跨语言规则语义等价。旧英文 Player Bundle 原图字节不同于当前公告所用图，实际再次读取可确认相同五个 EN 编号，不据此新增五个身份。

## 仍开放的范围

T1 原公告将 Seraphine 的合法性与 Radiance 发售挂钩。本批记录这个官方条件，但未独立解决各地区发售与适用日期，`gameEffectiveDate` 仍为空。旧产品文声明发布于 2026-08-03，T1 文声明发布于 2026-07-03；这些网页在 10-23 实际取得，不能据声明日期倒推出当前图片的首次发布时间。购买、抽签和发货日期也不代替游戏生效日期。

更正：本窗口初次记录将 Allen 战场误读为 `Victory Circle`；再次实际读取相同原图后更正为 `Hunters' Circle`，没有变更原件。原 Worlds 合成图以原尺寸读取，能辨认三个战场标题和城市副标题：`Cosmic Vista / LOS ANGELES`、`Hunters' Circle / ALLEN`、`Black Market / BROOKLYN`。这些只是局部卡面观察；微小印次号、完整卡文与具体适用性仍未认证，原四个 Worlds 分组不关闭。

Gift 的旧产品图实际为关闭的收纳盒，没有凯南或符文卡面；旧文章复用了已确认的英文 Akali 原图。中文凯南和六类闪符文的精确语言/印次仍未知。T1 中文 alternate 页同样实际 404，不将 GLOBAL 中文路径的失败推及整个大陆官方来源范围。

**原 17 个门禁全部 OPEN，原六个英文观察保留。** 原 1740 卡牌、14293 规则分母与英文八条来源差额不变；没有运行目录采用或游戏实现修改。下一步继续查证官方中文地区实际发布渠道、Worlds 清晰卡面和逐语言闪符文原图。

## 验证与归档

15 份请求收据全部保留，包括三份首次异常；另外 12 个实际 HTTP 原响应/图片已逐一校验原文字节数及 SHA-256。原五图哈希与原 17 个门禁 ID 也已复核。33 个归档成员全部重新解包验字节/哈希，新增原件归档约 974 KiB。仅新增本报告与独立证据，不改既有 Drawings 文档、公共 manifest、规则实现或 SQL；未运行游戏 check/full 或原生验收。

入口：[source-manifest.json](evidence/drawings-parallel-source-2026-10-23/source-manifest.json)。`report.json` 按原门禁 ID 记录已补证据及仍缺事项，完整卡文保留在官方原始卡面中；来源、发布时间字段、实际取得时间、HTTP 失败和不同预览图均保留。
