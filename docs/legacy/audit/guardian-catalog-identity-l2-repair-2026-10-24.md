# 守护天使目录身份 L2 校正 · 2026-10-24

#140 的 [L2 run 40076531361](https://github.com/DDDHL0704/riftbound/actions/runs/40076531361) 定向 9992/9992、规则套件 14022 通过 / 1 失败；唯一失败是原 `Cn16RuleInputSelectionTests` 仍固定新增宿主替代属性之前的目录指纹。两个实际 TRX、ZIP 摘要及失败消息保留在本线 `var/qa/c11-l2-repair-20261024/l2-run-40076531361/`。

核对 #137 `daa3b935` 到 #140 `7231bbb9`：1040 张卡牌目录与来源范围字节未变；装备记录只为 SFD·051/221 添加 `replacesHostDestruction: true`，目录身份投影同时加入此属性。依据既有[中文下框](https://cdn.playloltcg.com/lol/card/20251217/cbbc137ed7644a529456560f55cce23e.png)（SHA256 `fde3a9e1ec0824a952d9ab0f073c214a3a3ee62d60d20223f9939b809327c30f`），现在固定新目录 `6b15cda26c2f69587c9c14331a5fa30d48fd607188b237be47bb3351db734524`，独立断言唯一替代装备、原战力与复制属性，并把原目录 `3fcf1613…` 加入拒绝、会话拒绝和完整日志回放不匹配检查。原方法、来源日期及全部旧目录拒绝项保留，无生产代码修改。

在合并 main `29367aaa` 后的测试提交 `ae74b77e`，目录选择、运行时身份与守护天使共 66/66 通过、0 skip。扩展首跑 434 通过 / 2 失败：两个旧源码检查使用固定五层父目录，本机隔离产物更深；同一字节程序集移至正确层级后，两条原失败与两条重复守护天使对照 4/4 通过。两次计数不相加，原路径失败及先前编译失败保留。实际 Engine SHA/MVID/RIF、原 TRX、3025 源文件及两份 1418 文件程序集清单、五份外来 WIP 校验见 `var/qa/c9-input-fingerprint-l2-repair-20261024/green-final-receipt.json`，SHA256 `ac2b257284b973d1261017366f3d9ac72a301c785b33b813ba5070eef5ea9b40`。

本校正不认证整卡、L3 或完整后端；合入仍须当前最新 PR 合并输入的 L2 通过。
