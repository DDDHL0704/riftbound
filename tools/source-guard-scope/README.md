# 来源守卫范围参考

这是供①统一计划/影响选择接入的来源范围清单和只读核对工具，不修改现有CI、测试方法、断言或执行等级。

| 固定源码及程序集输入 | 全部发现方法 / 源码声明 | 来源守卫与元数据候选 | 保留原语义套件的来源相关方法 |
| --- | --- | --- | --- |
| No128 `6250f4e86ec5ebc393f8222d459c01d584f5157c` | 8651 / 8651，缺失0、额外0 | 550，全部既有L2 | 4810，其中既有L3归属保持 |
| No078 `d089541889cbf335ddb9c859c828fbf46dedc0e2` | 8666 / 8666，缺失0、额外0 | 559，全部既有L2 | 4816，其中既有L3归属保持 |

范围由Roslyn读取固定Git源码的Fact/Theory、成员和同类/跨类可解析helper依赖，再与未重建的冻结DLL实际`--list-tests`逐名相等核对。来源入口包括目录、正常注册、来源身份、规则文本解析、历史与功能组比较；含游戏操作的依赖明确保留在原语义套件。候选分类采取保守语法/命名和可解析调用关系，不声称完成语义证明。未分类或新增方法继续原规则套件，不能因不在参考清单而跳过。

两套清单覆盖各自现有311/313方法来源批的全部成员；另发现239/246个候选方法。显式FunctionalUnitBuilder守卫各175项，历史/逐组角色各185项，均逐方法列在完整原件中。550/559是方法选择范围，不是通过数；此轮仅发现与源码分析，没有执行新语义测试。既有L3精确身份不移入L2。

`reference.json`保存共同550项和No078新增9项，按实际输入的完整发现集合哈希核对；不得固定为永远适用的378、385、550或559。源码或发现变化必须重新生成，缺方法、错SHA、重复、空发现或新方法集合均失败。①接入时对同一固定输入按方法身份合并source/targeted/rule集合，再统一分片执行一次；不按整类删除语义或缩减原规则套件。

`generate_inventory.py`可对新输入重新生成逐方法范围。先用仓库.NET 10 SDK构建小型Roslyn扫描器，再传入完整Git SHA及该输入的真实发现；可用上次生成的syntax-cache，仅复用扫描器相同且Git blob未变的文件。工具核对全部源码测试声明与发现方法相等，保留每个方法原L2/L3归属，拒绝覆盖旧输出。候选中的L3方法仍归夜间L3，不得因候选身份移入L2。依赖图是语法与可解析helper关系，不是完整C#语义调用图；所有未选方法仍在原规则计划。

```sh
dotnet build tools/source-guard-scope/scanner/Scanner.csproj \
  --artifacts-path /absolute/path/to/ignored/scanner-build -m:1
python3 tools/source-guard-scope/generate_inventory.py \
  --repo /absolute/path/to/repository --source-head FULL_IMMUTABLE_SHA \
  --discovery /absolute/path/to/real-discovery.log \
  --scanner /absolute/path/to/ignored/scanner-build/bin/Scanner/debug/Scanner.dll \
  --output /absolute/path/to/new-ignored-output
```

生成器只读取Git和发现记录，不构建/执行游戏，不自行证明发现记录的来源绑定。实际源码/程序集/发现的绑定由调用方的固定输入回执验证；不能只把`--source-head`参数当成运行时证明。

```sh
python3 tools/source-guard-scope/validate_reference.py --profile no078 \
  --source-head d089541889cbf335ddb9c859c828fbf46dedc0e2 \
  --discovery /absolute/path/to/real-discovery.log
```

大原件在payment树ignored `var/qa/source-guard-inventory-20261027/`：两个profile各有完整逐方法理由、源码文件/行号、已核源码哈希、helper依赖、是否涉及游戏操作、既有L2/L3、真实发现和模块收据。仅做发现的进程未构造规则引擎，未产出运行时RuleInputs；原固定执行的RuleInputs仅作为缓存出处，未改称本次发现观察。源码语法复用727个相同Git blob对应文件，仅额外解析20个差异文件，未复制源码树或重新全审2730/2736输入、40个DLL。可移植生成器与完整原件逐方法相等，11项核对覆盖两份真实发现、两份完整范围重建及7种拒绝路径；记录见`validation-v1/receipt.json`。

④最新PR205 `b3d67c8aa89346d2a8598a4d9ebcc04a86450edc`相对D089另有4个C#文件、10个新增方法，其中1个直接元数据方法。增量已在`latest-no078-static-delta/delta.json`列出，当前559清单不冒充b3的最终发现匹配；须以新synthetic的实际发现重新生成。源/数量九守卫原红、385/387新绿及No078游戏30/旧机制8仍各归其原输入，清单不提升整卡、完整L2/L3或后端认证。
