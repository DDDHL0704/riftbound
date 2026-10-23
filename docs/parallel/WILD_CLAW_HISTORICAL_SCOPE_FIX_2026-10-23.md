# Wild Claw 历史目录比较边界

- 原唯一C1 check：冻结 `32547ff2` /1040目录，原TRX14,068过/1失败；唯一失败将后续印次带入N46的1035→1036比较。冻结源、bin、原控制台/TRX及FAIL不改、不重跑。
- 以N46源 `5edb12e57576358f3ef4a9f9d6da92c313f12437` 的目录文件SHA256 `e9e574d6651ec0e4bcde68618438559336872371ffbdc5931283cea77ff9204e` 生成1036个精确印次清单，固定原测试边界；不随当前目录总数上调原断言。
- 原Fact名、1035/1036数量、834/835功能身份数量、每项身份/成员序列化相等及恰好一个新增身份断言全部保留；新增清单唯一性和完整成员断言。后续目录增量由各批保留测试和完整目录测试继续校验。
- 独立证明N46仅新增VEN089且原1035整行/顺序保留；当前1043目录中的原1036整行/相对顺序完全相同。证明在①树 `var/qa/wild-claw-historical-scope-20261023-v1/scope-proof-v1.json`。
- 新候选定向 `WildClawSourceIdentityTests` 与 `CardCatalogBaselineTests` 共374/374通过，原TRX见①同目录 `fixed-focus-v1/focus/results.trx`。只归新固定输入；旧check不转绿，完整CI/受影响后端层和整卡认证仍开放，净新增整卡认证0。
