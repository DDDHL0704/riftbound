# 费用阶段 nightly 接线

本接线显式依赖③的 `64ca80cded8c31dd313461c180cf9e6a49c936d7`，包含 #158、#161 及 S3 standalone driver；CI 分支基于该提交，只改工作流、Python 编排/检查和短文档，不移入旧集成长分支积压。父规则包需先按各自最新 L2 合 main，再将本 PR 更新到 main 执行实际 L2。

`scripts/run-payment-reconnect-ci.py` 固定四个现存入口：PaymentResourceSurplusAcceptance（S3）、OptionalCostReductionAcceptance（S4）、InstructionPaymentAcceptance（S5）、RestrictedManaAcceptance（S6）。每条保留 sql、prepare、API1 play/verify、真实停止、API2 recover/verify、停止及 tamper；S5/S6 另保留 finish/final。现有基础房间进程重启冒烟保留在同一 `related_network` job。任一步失败仍使已有 SQL/full 汇总失败，不加入日常 L2 依赖。

SQL/API/网络 selector 显式继承同一专用 loopback PostgreSQL；全部构建和 checkout 用 scope 一次固定的完整 main SHA。比较 API/driver 的 Engine、Contracts、CardCatalog、Persistence 及三份数据 sidecar；每阶段核对实际 Engine path/SHA/源码版本，startup hook 绑定真实 API PID/MVID。保留每阶段原始 runtime 收据，要求每房间两玩家实际 RECONNECT Joined、新阶段新 trace，并复用原领域断言检查令牌、私有视图和原历史。

`payment-reconnect-results` 保存原始日志、SQL/网络输出、实际 PID、runtime 身份和最终收据，配置保留90天；实际到期仍按治理要求核验。编排沿用历史 A7 的真实生命周期及认证证据检查，适配当前四个独立 CLI；原八例矩阵仍是独立范围，不能由四条费用阶段替代认证。

本地146项 Python 工具检查及治理检查通过，格式检查通过；源码入口已核对。未运行真实 SQL、SignalR、API 重启或新全量验收，也未派发新 L3。首次实际运行留给父依赖合 main 后的固定 main nightly；本 PR 不授予当前 L3、整卡、客户端或完整后端认证。
