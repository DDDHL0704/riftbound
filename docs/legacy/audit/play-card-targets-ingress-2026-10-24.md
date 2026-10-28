# PLAY_CARD 原始目标数组的日志入口校验
- [Issue109](https://github.com/DDDHL0704/riftbound/issues/109)。独立main基底8ac0afba移植568cc715；仅SubmitAsync缓存检查后、stale/status/engine/journal之前校验required原始目标数组，保留既有Mapper及恢复守卫。
- 原raw空白值会被Mapper过滤成typed空数组，部分合法无目标单位可能接受并写入不可恢复记录；坏raw的过期提示也会留下恢复不接受的拒绝日志。缺项、非array、非规范string及重复/空白属性名均在入口零事件拒绝；合法数组的顺序/大小写/重复ID保留给规则。
- 30个新用例检查20种坏形状、stale/finished、缓存优先级、规范控制与旧卡实际费用；真实SEATING前8条原命令后，修正的原intent可作为第9条接受，原日志/冷registry检查保留。
- 本main移植先跑38例：37PASS/1原费用预览测试FAIL；后者仅提交提示信封而缺少PLAY_CARD正文。按34005396原校准保留坏raw为无变更/空日志负控，再用实际Web序列化完整请求执行原正向、stale、幂等语义；原TRX保存在var/qa/payment-prs-20261024/issue109/before-wire-test-fix-v1/。
- 固定本分支的定向/相邻结果、源输入与实际程序集身份保存在var/qa/payment-prs-20261024/issue109/；原568来源与历史278例证据仍在var/qa/play-card-targets-ingress-20261024/，不冒充当前main结果。
- L2依最新人类约定以定向和实际规则CI为准；JSON null present信封由后继Issue115处理，nullable absent raw的持久化契约缺口仍OPEN，不迁移或清洗旧日志。
