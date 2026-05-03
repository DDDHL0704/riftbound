# Current P4 Status

更新时间：2026-05-04

这是 P4 高频关键词与基础卡牌的短状态文件。P3 的卡牌数据与 BehaviorSpec 只读骨架完成状态仍以 `docs/CURRENT_P3_STATUS.md` 为准；P2 core rules preflight 与 P2.5 开发期测试 UI 状态分别以 `docs/CURRENT_P2_STATUS.md` 和 `docs/CURRENT_P2_5_STATUS.md` 为准。

## Goal

完成 P4 高频关键词与基础卡牌：按风险分层小批次实现权限关键词、战斗关键词、生命周期关键词、资源关键词、互动关键词、装备关键词和基础动作模板，复用 P3 BehaviorSpec/template skeleton，保持 P2/P2.5/P3 绿色，补测试、文档、状态文件并提交。

## P4.0 Scope

本阶段只做审计与候选分层：

- 读取 P2/P2.5/P3 状态、P4 主计划、README、START_HERE、BehaviorSpec contracts、BehaviorSpec catalog、规则文本 parser、template executor、CardBehaviorRegistry、CoreRuleEngine、catalog baseline tests、conformance runner tests 和官方卡表快照。
- 通过本地 API `/catalog/p3-status` 与 `/catalog/behavior-specs` 只读复核 P3 BehaviorSpec 与 template report。
- 建立 P4 关键词和基础动作模板候选清单、风险分层、推荐小批次顺序和验证门禁。
- 不改 `CoreRuleEngine` 主路径，不启用 P3 template executor 写状态，不迁移全卡牌。

本阶段明确不做：

- 不进入 P5 装备/控制权/触发替换大系统。
- 不进入 P6 全卡牌批量实现。
- 不进入 P7 最终产品 UI。
- 不提交规则 PDF/FAQ。
- 不提交未跟踪的 `riftbound-dotnet.sln`。

## Baseline

- P3 完成提交：`4a3b45f feat: complete p3 card behavior specs`
- P4.0 提交：`fb19570 docs: add p4 status audit`
- P4.1 提交：`506ca89 feat: add p4 template delegation bridge`
- P4.2 提交：`9ed58b0 feat: add p4 permission keyword model`
- P4.3 提交：`64a96fe feat: add p4 ephemeral turn start cleanup`
- P4.4 提交：`4c72486 feat: add p4 echo keyword model`
- P4.5 提交：`209ad75 feat: add p4 primitive template plans`
- P4.6 提交：`0693e27 feat: add p4 combat keyword profiles`
- P4.7 提交：`c93cb87 feat: add p4 resource keyword profiles`
- P4.8 提交：`c376b94 feat: add p4 equipment keyword profiles`
- P4.9 提交：`60396c5 feat: add p4 remaining profile audit`
- P4.10 提交：`af40d9f feat: add p4 fixed experience gain`
- P4.11 提交：`10a2256 feat: add p4 experience optional cost`
- P4.12 提交：`fc6046c feat: add p4 spellshield target tax`
- P4.13 提交：`1f2347d feat: add p4 haste ready optional cost`
- P4.14 提交：`64e26c2 feat: add p4 encourage cost reduction`
- P4.15 提交：`3bc7060 feat: add p4 level threshold source unit`
- P4.16 提交：`df6f5b3 feat: add p4 windrunner level roam`
- P4.17 提交：`c7c8aa7 feat: add p4 wuji level draw`
- P4.18 提交：`36723be feat: add p4 baby shark haste ready`
- P4.19 提交：`55a7c43 feat: add p4 dynamic experience gain`
- P4.20 提交：`38d7d74 feat: add p4 legion rearguard haste ready`
- P4.21 提交：`812c7bc feat: add p4 encourage self boon`
- P4.22 提交：`0d4ada5 feat: add p4 dangerous duo encourage`
- P4.23 提交：`9da6828 feat: add p4 junkyard bully encourage`
- P4.24 提交：`6b4bd28 feat: add p4 vanguard captain encourage`
- P4.25 提交：`b381a15 feat: add p4 mr root haste ready`
- P4.26 提交：`5926729 feat: add p4 mech maniac haste ready`
- P4.27 提交：`80a1520 feat: add p4 xersai fish haste ready`
- P4.28 提交：`72c6bac feat: add p4 yi level6 keywords`
- P4.29 提交：`af80f41 feat: add p4 yi alt level6 keywords`
- P4.30 提交：`bc6cf63 feat: add p4 karina haste ready`
- P4.31 提交：`530a559 feat: add p4 crimson signet haste ready`
- P4.32 提交：`b0054d6 feat: add p4 tasty faerie haste ready`
- P4.33 提交：`20a28d6 feat: add p4 ekko haste ready`
- P4.34 提交：`22a5fd4 feat: add p4 armed assaulter haste ready`
- P4.35 提交：`3fabda6 feat: add p4 ancient berserker haste ready`
- P4.36 提交：`59f9a7e feat: add p4 kraken hunter haste ready`
- P4.37 提交：`44ca3ff feat: add p4 lee sin haste ready`
- P4.38 提交：`77f2694 feat: add p4 lee sin alt haste ready`
- P4.39 提交：`3069e1e feat: add p4 thousand tailed haste ready`
- P4.40 提交：`823d230 feat: add p4 kaisa haste ready`
- P4.41 提交：`ff1ffc3 feat: add p4 reksai haste ready`
- P4.42 提交：`bbe3b9f feat: add p4 reksai alt haste ready`
- P4.43 提交：`4cadac4 feat: add p4 kaisa alt haste ready`
- P4.44 提交：`87309de feat: add p4 crimson signet treant alt haste ready`
- P4.45 提交：`265bd9c feat: add p4 nilah haste ready`
- P4.46 提交：`4fdbb36 feat: add p4 rengar haste ready`
- P4.47 提交：`ab65f9b feat: add p4 rengar alt haste ready`
- P4.48 提交：`2f6a29d feat: add p4 miss fortune haste ready`
- P4.49 提交：`1596987 feat: add p4 miss fortune alt haste ready`
- P4.50 提交：`d838556 feat: add p4 sivir haste ready`
- P4.51 提交：`51b7af5 feat: add p4 sivir alt haste ready`
- P4.52 提交：`b198135 feat: add p4 lillia haste ready`
- P4.53 提交：`52a9a0e feat: add p4 lillia alt haste ready`
- P4.54 提交：`db753ad feat: add p4 azir haste ready`
- P4.55 提交：`7179a36 feat: add p4 azir alt haste ready`
- P4.56 提交：`0b0334a feat: add p4 jinx haste ready`
- P4.57 提交：`d789bbf feat: add p4 jinx alt haste ready`
- P4.58 提交：`1044523 feat: add p4 equipment attachment audit`
- P4.59 提交：`126bde5 feat: add p4 spellshield multi target tax`
- P4.60 提交：`997dfa7 feat: add p4 recycle banish boon templates`
- P4.61 提交：`d012402 feat: add p4 spellshield friendly target no tax`
- P4.62 提交：`b6f4d3e feat: add p4 activate ability premodel`
- P4.63 提交：`ff64f8a feat: add p4 hide card premodel`
- P4.64 提交：`fb61728 feat: add p4 ambush play premodel`
- P4.65 提交：`7e4a110 feat: add p4 move unit premodel`
- P4.66 提交：`aa488fc feat: add p4 assemble equipment premodel`
- P4.67 提交：`df8c2f9 feat: add p4 declare battle premodel`
- P4.68 提交：`7a1e8c4 feat: add p4 reveal card premodel`
- P4.69 提交：`318f270 feat: add p4 face down snapshot redaction`
- P4.70 提交：`5e0b1f2 feat: add p4 standby hide execution`
- P4.71 提交：`9fcb14d feat: add p4 standby reveal execution`
- P4.72 提交：`47b03cd feat: add p4 guerrilla standby hide permission`
- P4.73 提交：`92465f3 feat: add p4 vi activated skill`
- P4.74 提交：`06e4a69 feat: add p4 card object identity`
- P4.75 提交：`68c836a test: add p4 objective coverage audit`
- P4.76 提交：`a10f694 feat: add p4 standby reaction stack`
- P4.77 提交：`ebf0cbc feat: add p4 xerath skill target tax`
- P4.78 提交：`70d66fe test: add p4 xerath friendly spellshield no tax`
- P4.79 提交：`2cc41dc test: add p4 xerath exhausted source rejection`
- P4.80 提交：`39f9f35 test: add p4 xerath missing target rejection`
- P4.81 提交：`f74efb4 test: add p4 xerath target count rejection`
- P4.82 提交：`3f5a8dd test: add p4 xerath optional cost rejection`
- P4.83 提交：`1e697a7 test: add p4 xerath source identity rejection`
- P4.84 提交：`58e3ed3 test: add p4 xerath target type rejection`
- P4.85 提交：`0db3535 test: add p4 xerath source zone rejection`
- P4.86 提交：`71bd409 test: add p4 xerath opponent source rejection`
- P4.87 提交：`75a11c1 test: add p4 vi opponent source rejection`
- P4.88 提交：`413660e test: add p4 vi source zone rejection`
- P4.89 提交：`8100b5d test: add p4 vi target rejection fixture`
- P4.90 提交：`94d659c test: add p4 vi optional cost rejection`
- P4.91 提交：`f4301d0 test: add p4 vi resource rejection fixture`
- P4.92 提交：`e2a9d94 test: add p4 vi source identity fixture`
- P4.93 提交：`1c2870a test: add p4 xerath tax rejection fixture`
- P4.94 提交：`744b6a0 test: add p4 standby cost rejection fixture`
- P4.95 提交：`abc3d8c test: add p4 standby reaction rejection fixture`
- P4.96 提交：`10e2207 test: add p4 free standby rejection fixture`
- P4.97 提交：`ba34031 test: add p4 ambush rejection fixture`
- P4.98 提交：`6bbbdbb test: add p4 move rejection fixture`
- P4.99 提交：`169eb2d test: add p4 equipment rejection fixture`
- P4.100 提交：`84195e8 test: add p4 combat rejection fixture`
- P4.101 提交：`f5de73d test: add p4 predict rejection fixture`
- P4.102 提交：`40b5b22 test: add p4 guerrilla rejection fixture`
- P4.103 提交：`c063203 test: add p4 haste rejection fixture`
- P4.104 提交：`e41bce0 test: add p4 spellshield rejection fixture`
- P4.105 提交：`344907a test: add p4 spellshield multi rejection fixture`
- P4.106 提交：`20ffb7d test: add p4 spirit fire target rejection fixture`
- P4.107 提交：`4779c3d test: add p4 playful tentacles target rejection fixture`
- P4.108 提交：`c8977b0 test: add p4 hunt target rejection fixture`
- P4.109 提交：`83b1f2a test: add p4 gust target rejection fixture`
- P4.110 提交：`5d08cf0 test: add p4 highway robbery rejection fixture`
- P4.111 提交：`d84378b test: add p4 deadly flourish rejection fixture`
- P4.112 提交：`13eb7b6 test: add p4 blood money rejection fixture`
- P4.113 提交：`f61f78f test: add p4 punishment target rejection fixture`
- P4.114 提交：`8b04ef8 test: add p4 kerplunk target rejection fixture`
- P4.115 提交：`e84986b test: add p4 zenith blade target rejection fixture`
- P4.116 提交：`319eb3f test: add p4 zenith blade friendly rejection fixture`
- P4.117 提交：`115547b test: add p4 last breath target rejection fixture`
- P4.118 提交：`b30d2d2 test: add p4 last breath target order fixture`
- P4.119 提交：`c6854c8 test: add p4 convergent mutation target fixture`
- P4.120 提交：`0d656df test: add p4 convergent mutation duplicate fixture`
- P4.121 提交：`b51176c test: add p4 existential dread target fixture`
- P4.122 提交：`265d815 test: add p4 thundering sky cost fixture`
- P4.123 提交：`f67acfc test: add p4 mind and balance cost fixture`
- P4.124 提交：`de61a97 test: add p4 piercing light target fixture`
- P4.125 提交：`072a962 test: add p4 bellows breath target fixture`
- P4.126 提交：`7639cd1 test: add p4 firestorm target fixture`
- P4.127 提交：`32cf167 test: add p4 crescent strike target fixture`
- P4.128 提交：`06f4261 test: add p4 switcheroo target fixture`
- P4.129 提交：`ae3bb93 test: add p4 switcheroo base target fixture`
- P4.130 提交：`7709cda test: add p4 crescent strike base target fixture`
- P4.131 提交：`2e5357c test: add p4 bellows breath repeated target fixture`
- P4.132 提交：`5be9ade test: add p4 center stage echo cost fixture`
- P4.133 提交：`0681fcf test: add p4 illegal echo fixture`
- P4.134 提交：`666f708 test: add p4 rocket barrage mode fixture`
- P4.135 提交：`82e5b19 test: add p4 rocket barrage target fixture`
- P4.136 提交：`4e3ce0a test: add p4 emergency recall target fixture`
- P4.137 提交：`a470200 test: add p4 poro snax target fixture`
- P4.138 提交：`67d4ee9 test: add p4 shurelyas target fixture`
- P4.139 提交：`c8aa448 test: add p4 future forge target fixture`
- P4.140 提交：`d8aba11 test: add p4 scrap heap target fixture`
- P4.141 提交：`23610af test: add p4 sprite lantern target fixture`
- P4.142 提交：`9ca1325 test: add p4 sumpworks map target fixture`
- P4.143 提交：`f8882c7 test: add p4 scrying blossom target fixture`
- P4.144 提交：`bee29b7 test: add p4 magic beans target fixture`
- P4.145 提交：`ee20dc8 test: add p4 steel ballista target fixture`
- P4.146 提交：`4f9dff1 test: add p4 heart of ice target fixture`
- P4.147 提交：`3a869b8 test: add p4 remorse orb target fixture`
- P4.148 提交：`d30c207 test: add p4 soul sword target fixture`
- P4.149 提交：`680804f test: add p4 jagged dirk target fixture`
- P4.150 提交：`465f9c4 test: add p4 dorans shield target fixture`
- P4.151 提交：`8cf9934 test: add p4 hextech bulwark target fixture`
- P4.152 提交：`5b6fbd4 test: add p4 dorans blade target fixture`
- P4.153 提交：`d2964fb test: add p4 dorans ring target fixture`
- P4.154 提交：`329eeb0 test: add p4 marching orders target fixture`
- P4.155 提交：`8b184ac test: add p4 duel target fixture`
- P4.156 提交：`b0a12e0 test: add p4 battle command target fixture`
- P4.157 提交：`c582fb1 test: add p4 void assault target fixture`
- P4.158 提交：`f234ddb test: add p4 vanguards eye target fixture`
- P4.159 提交：`b110c36 test: add p4 recurve bow target fixture`
- P4.160 提交：`391ee56 test: add p4 long sword target fixture`
- P4.161 提交：`ebd51c4 test: add p4 cloth armor target fixture`
- P4.162 提交：`983375a test: add p4 steraks target fixture`
- P4.163 提交：`66eb646 test: add p4 spinning axe target fixture`
- P4.164 提交：`aa836cb test: add p4 shepherds heirloom target fixture`
- P4.165 提交：`314cb4c test: add p4 brutalizer target fixture`
- P4.166 提交：`e16f47c test: add p4 guardian angel target fixture`
- P4.167 提交：`598507a test: add p4 hexdrinker target fixture`
- P4.168 提交：`ff1c42e test: add p4 warmogs armor target fixture`
- P4.169 提交：`5a1e95e test: add p4 trinity force target fixture`
- P4.170 提交：`d2c7dcd test: add p4 boots target fixture`
- P4.171 提交：`169bb3d test: add p4 cull target fixture`
- P4.172 提交：`bfb5795 test: add p4 sacred shears target fixture`
- P4.173 提交：`e3aa7e7 test: add p4 bf sword target fixture`
- P4.174 提交：`eafb8e4 test: add p4 guidebook target fixture`
- P4.175 提交：`24edba7 test: add p4 arions fall target fixture`
- P4.176 提交：`e0c1d43 test: add p4 hunters machete target fixture`
- P4.177 提交：`13fede9 test: add p4 withered battleaxe target fixture`
- P4.178 提交：`a34b3d4 test: add p4 bone club target fixture`
- P4.179 提交：`b1132cb test: add p4 ancient stele target fixture`
- P4.180 提交：`274ba9b test: add p4 hextech anomaly target fixture`
- P4.181 提交：`e6770c2 test: add p4 energy channel target fixture`
- P4.182 提交：`c9cefc0 test: add p4 time gate target fixture`
- P4.183 提交：`0a13f6d test: add p4 raven tome target fixture`
- P4.184 提交：`0df3dd1 test: add p4 sun disc target fixture`
- P4.185 提交：`6ab9702 test: add p4 foresight mask target fixture`
- P4.186 提交：`e95104b test: add p4 solari altar target fixture`
- P4.187 提交：`600a881 test: add p4 chemtech barrel target fixture`
- P4.188 提交：`6d43bc5 test: add p4 soul wheel target fixture`
- P4.189 提交：`a4d6253 test: add p4 mushroom bag target fixture`
- P4.190 提交：`26f3d75 test: add p4 arena bar target fixture`
- P4.191 提交：`531e418 test: add p4 pirate hideout target fixture`
- P4.192 提交：`4c97811 test: add p4 forgotten signpost target fixture`
- P4.193 提交：`7e5e560 test: add p4 frozen gem target fixture`
- P4.194 提交：`86bae73 test: add p4 crumbling palace target fixture`
- P4.195 提交：`63c52b6 test: add p4 scarlet rose target fixture`
- P4.196 提交：`12f0ccd test: add p4 reversal shard target fixture`
- P4.197 提交：`f2b1ac3 test: add p4 assembly rack target fixture`
- P4.198 提交：`247d788 test: add p4 sfur song target fixture`
- P4.199 提交：`1138ddc test: add p4 z drive target fixture`
- P4.200 提交：`32f9019 test: add p4 vanguard armory target fixture`
- P4.201 提交：`ff84323 test: add p4 remembrance altar target fixture`
- P4.202 提交：`471b533 test: add p4 rage sigil target fixture`
- P4.203 提交：`4be3789 test: add p4 focus sigil target fixture`
- P4.204 提交：`da9bc7e test: add p4 insight sigil target fixture`
- P4.205 提交：`373723c test: add p4 power sigil target fixture`
- P4.206 提交：`0438ef6 test: add p4 discord sigil target fixture`
- P4.207 提交：`259e172 test: add p4 unity sigil target fixture`
- P4.208 提交：`94f0583 test: add p4 ogn rage sigil target fixture`
- P4.209 提交：`3e38ad7 test: add p4 ogn focus sigil target fixture`
- P4.210 提交：`381460c test: add p4 ogn insight sigil target fixture`
- P4.211 提交：`1a9f1bc test: add p4 ogn power sigil target fixture`
- P4.212 提交：`38b2e23 test: add p4 ogn discord sigil target fixture`
- P4.213 提交：`0889264 test: add p4 ogn unity sigil target fixture`
- P4.214 提交：`4976337 test: add p4 wondrous pack target fixture`
- P4.215 提交：`0100fa8 test: add p4 siren target fixture`
- P4.216 提交：`744b65e test: add p4 ownerless treasure target fixture`
- P4.217 提交：`d1d8048 test: add p4 scavenging whiz target fixture`
- P4.218 提交：`83d68db test: add p4 mistfall bladeyard target fixture`
- P4.219 提交：`ef78397 test: add p4 shimmering aurora target fixture`
- P4.220 提交：`1112ec9 test: add p4 solari emblem target fixture`
- P4.221 提交：`5042c31 test: add p4 vanguard helm target fixture`
- P4.222 提交：`e14f731 test: add p4 honeyfruit target fixture`
- P4.223 提交：`087c5d8 test: add p4 last rites target fixture`
- P4.224 提交：`e92a6bf test: add p4 blade of ruined king target fixture`
- P4.225 提交：`860dea8 test: add p4 mysterious weapon target fixture`
- P4.226 提交：`e94a279 test: add p4 sea monster hook target fixture`
- P4.227 提交：`82a4970 test: add p4 petricite monument target fixture`
- P4.228 提交：`c585168 test: add p4 zhonyas hourglass target fixture`
- P4.229 提交：`4394cdd test: add p4 edge of night target fixture`
- P4.230 提交：`6123be9 test: add p4 hearthfire cloak target fixture`
- P4.231 提交：`f87b3ac test: add p4 rabadons deathcap target fixture`
- P4.232 提交：`04184b2 test: add p4 blast cone target fixture`
- P4.233 提交：`afaf6ba test: add p4 death list target fixture`
- P4.234 提交：`263109a test: add p4 cursed sarcophagus target fixture`
- P4.235 提交：`a2bb064 test: add p4 boneclub promo target fixture`
- P4.236 提交：`1d0489f test: add p4 hextech gauntlet target fixture`
- P4.237 提交：`5e5ed84 test: add p4 treasure golem target fixture`
- P4.238 提交：`c7f974b test: add p4 xerath target fixture`
- P4.239 提交：`e5f0153 test: add p4 dragon soul sage target fixture`
- P4.240 提交：`9a9e90b test: add p4 fluft poro target fixture`
- P4.241 提交：`7c57ee5 test: add p4 renata glasc target fixture`
- P4.242 提交：`585f27d test: add p4 renata glasc alt target fixture`
- P4.243 提交：`609d48b test: add p4 ogn draven target fixture`
- P4.244 提交：`e2df150 test: add p4 sfd fiora target fixture`
- P4.245 提交：`66ff040 test: add p4 sfd fiora alt target fixture`
- P4.246 提交：`63edffc test: add p4 sfd irelia target fixture`
- P4.247 提交：`550384e test: add p4 sfd irelia alt target fixture`
- P4.248 提交：`d4ed4ef test: add p4 dragon caller target fixture`
- P4.249 提交：`caadb28 test: add p4 waterbender target fixture`
- P4.250 提交：`5f205f2 test: add p4 wise elder target fixture`
- P4.251 提交：`69f97fa test: add p4 eager apprentice target fixture`
- P4.252 提交：`5f8d3bd test: add p4 arena service crew target fixture`
- P4.253 提交：`149b577 test: add p4 poro herder target fixture`
- P4.254 提交：`a9a4a6a test: add p4 ravenbloom student target fixture`
- P4.255 提交：`1c0f58d test: add p4 resonant soul target fixture`
- P4.256 提交：`c6b2cc6 test: add p4 bilgewater bully target fixture`
- P4.257 提交：`e8bf5d7 test: add p4 sharpshooter pirate target fixture`
- P4.258 提交：`286bb06 test: add p4 dune drake target fixture`
- P4.259 提交：`0d93a89 test: add p4 ember monk target fixture`
- P4.260 提交：`a059ef7 test: add p4 hidden tracker target fixture`
- P4.261 提交：`4f78dcb test: add p4 undercover agent target fixture`
- P4.262 提交：`4af5bb3 test: add p4 traveling merchant target fixture`
- P4.263 提交：`de809fe test: add p4 ogn kogmaw target fixture`
- P4.264 提交：`7561bc0 test: add p4 noxian drummer target fixture`
- P4.265 提交：`ff18865 test: add p4 tide caller target fixture`
- P4.266 提交：`837fee7 test: add p4 ogn 202 jinx target fixture`
- P4.267 提交：`972e219 test: add p4 ghost matron target fixture`
- P4.268 提交：`5b3bde9 test: add p4 albus ferros target fixture`
- P4.269 提交：`6a9c18e test: add p4 ogn karthus target fixture`
- P4.270 提交：`aadc0ab test: add p4 dunehorn beast target fixture`
- P4.271 提交：`46949a3 test: add p4 gloompath guard target fixture`
- P4.272 提交：`a8e7b28 test: add p4 apprentice blacksmith target fixture`
- P4.273 提交：本提交 `test: add p4 mountain ape elder target fixture`
- 官方快照：`data/official/card-catalog.zh-CN.json`
- 快照日期：`2026-04-27`
- 官方条目：`1009`
- Functional units：`811`
- P2 core rules preflight：`811/811 = 100.0%`
- 最小 card behavior registry：`794/811 = 97.9%`
- 可打出官方牌差集：已清空
- P3 schema validation：`1009/1009`，violations `0`
- P3 BehaviorSpec：`1009/1009`
- P3 BehaviorSpec status counts：`implemented 785`、`manual-rule-required 211`、`unimplemented 13`
- P3 missing reason：`0`
- 工作区预期：只剩未跟踪 `riftbound-dotnet.sln`

## P4.0 Audit Snapshot

复核命令：

```bash
source scripts/dev-env.sh && ASPNETCORE_URLS=http://127.0.0.1:5091 dotnet run --no-restore --project src/Riftbound.Api/Riftbound.Api.csproj
curl -s http://127.0.0.1:5091/catalog/p3-status
curl -s http://127.0.0.1:5091/catalog/behavior-specs
```

`/catalog/p3-status` 结果：

```json
{
  "officialEntries": 1009,
  "total": 1009,
  "schemaValid": true,
  "schemaViolationCount": 0,
  "functionalUnits": 811,
  "idsAreUnique": true,
  "behaviorSpecs": 1009,
  "statusCounts": {
    "implemented": 785,
    "manual-rule-required": 211,
    "unimplemented": 13
  },
  "missingReasonCardNos": []
}
```

解释：

- `implemented` 仍表示 P3 spec 能映射到现有 P2 手写 registry 或同 functional unit 映射，不表示 P4 keyword/template 已规则化执行。
- `manual-rule-required` 当前主要是 `传奇 106`、`战场 57`、`符文 48`，需要独立规则域。
- `unimplemented 13` 均为指示物/指示物战场/指示物装备，需要 token factory 或非 `PLAY_CARD` 绑定。
- `BehaviorTemplateExecutor` 当前只生成 plan，不改 `MatchState`；P4.1 只能先做安全桥接测试，不能替换 `CoreRuleEngine`。

## Template Candidates

以下统计按 P3 `BehaviorSpec.TemplateIds` 的 distinct card count 计算。

| Template | Total | Implemented | Manual | Unimplemented | P4 risk | P4.0 decision |
|---|---:|---:|---:|---:|---|---|
| `temp_might` | 292 | 255 | 36 | 1 | Low/Medium | P4.5 已有 primitive plan；真实状态写入仍由 P2 `POWER_MODIFIED_UNTIL_END_OF_TURN` 与清理负责。 |
| `damage` | 148 | 141 | 7 | 0 | Low/Medium | P4.5 已有基础固定伤害 primitive plan；动态伤害和替代效果继续委托 P2。 |
| `move` | 136 | 116 | 19 | 1 | Medium | 当前只桥接/委托 P2；P4.65 已新增 `MOVE_UNIT` command envelope 与 Core 显式拒绝前置模型；P4.98 已补 `MOVE_UNIT` 前置拒绝 fixture；精确多战场/游走/此处目的地仍需后续模型。 |
| `draw` | 131 | 105 | 26 | 0 | Low | P4.5 已有固定抽牌 primitive plan；抽牌与燃尽状态写入仍由 P2 覆盖。 |
| `destroy` | 127 | 115 | 8 | 4 | Low/Medium | P4.5 已有单目标摧毁 primitive plan；替代/触发导致的摧毁仍分层处理。 |
| `boon` | 66 | 51 | 15 | 0 | Medium | P4.60 已注册为 P3/P4 template skeleton，并验证《秘奥义！慈悲度魂落》可安全委托到 P2 增益代表路径；全局增益加成、消耗增益和触发增益仍 deferred。 |
| `recycle` | 63 | 55 | 8 | 0 | Medium | P4.60 已注册为 P3/P4 template skeleton，并验证《暗中破坏》/已有回收 fixture 可安全委托到 P2；隐藏信息、多玩家选择和触发回收仍 deferred。 |
| `assemble` | 55 | 53 | 2 | 0 | High | P4.58 已把《取放自如》武装贴附/卸除作为 P2 手写代表路径纳入 P4 证据；P4.66 已新增 `ASSEMBLE_EQUIPMENT` command envelope 与 Core 显式拒绝前置模型；P4.99 已补 `ASSEMBLE_EQUIPMENT` 前置拒绝 fixture；装配费用、owner/controller、灵便自动贴附、百炼 optional attach 仍属高风险边界。 |
| `gain_experience` | 51 | 43 | 8 | 0 | Medium/High | P4.10 已接入固定数值“打出时获得经验”；P4.11 已接入固定经验额外费用减费代表路径；P4.19 已接入《严厉军士》按友方场上单位数量获得经验代表路径；经验激活技能和等级/装配联动仍 deferred。 |
| `recall` | 49 | 39 | 10 | 0 | Medium | 当前只桥接/委托 P2；召回到基地/手牌已有 P2 原语，精确时序分层。 |
| `stun` | 33 | 30 | 3 | 0 | Low | P4.5 已有 `STUNNED` primitive plan；P3 parser 的眩晕 reminder damage 噪声不会阻断该 primitive。 |
| `echo` | 24 | 22 | 2 | 0 | Medium | P4.4 已将 mana-only `ECHO` optional cost/repeat 抽成互动关键词模型；有色/弃牌/授予回响仍延后。 |
| `ambush` | 18 | 18 | 0 | 0 | High | P4.9 已识别 profile；待命/反应/战场目的地和 face-down 交互仍 deferred。 |
| `banish` | 11 | 8 | 3 | 0 | Medium | P4.60 已注册为 P3/P4 template skeleton，并验证《传送门大营救》可安全委托到 P2 放逐并重新打出代表路径；替代放逐、来源放逐和复杂重新打出目的地仍分层。 |

## Keyword Candidates

以下统计按 P3 `BehaviorSpec.Keywords` 的 distinct card count 计算。

| Keyword | Implemented | Manual | Unimplemented | P4 risk | P4.0 decision |
|---|---:|---:|---:|---|---|
| 迅捷 | 82 | 0 | 0 | Medium | P4.2 候选；需把普通回合/法术对决时机从卡牌特例提升为关键词模型。 |
| 反应 | 136 | 14 | 2 | Medium/High | P4.2 候选；P2 已有 `CanPlayDuringPriority`，但符文/装备/指示物反应需分域。 |
| 急速 | 34 | 0 | 0 | Medium | P4.2 已识别 profile；P4.13 已给《灼焰飞龙》接入 `HASTE_READY` 代表可选费用，P4.103 已补同一路径缺少 power 费用拒绝 fixture；P4.18 已给《小鲨鱼》接入第二条 `HASTE_READY` 代表可选费用，P4.20 已给《军团后卫》接入第三条 `HASTE_READY` 代表可选费用，P4.25 已给《树根先生》接入第四条 `HASTE_READY` 代表可选费用，P4.26 已给《机械迷》接入第五条 `HASTE_READY` 代表可选费用，P4.27 已给《琢珥鱼》接入第六条 `HASTE_READY` 代表可选费用，P4.30 已给《卡银娜·薇蕊泽》接入第七条 `HASTE_READY` 代表可选费用，P4.31 已给《绯红印记树怪》接入第八条 `HASTE_READY` 代表可选费用，P4.44 已给《绯红印记树怪》A 版本接入第二十一条 `HASTE_READY` 代表可选费用，P4.45 已给《尼菈》接入第二十二条 `HASTE_READY` 代表可选费用，P4.46 已给《雷恩加尔》接入第二十三条 `HASTE_READY` 代表可选费用，P4.47 已给《雷恩加尔》A 版本接入第二十四条 `HASTE_READY` 代表可选费用，P4.48 已给《厄运小姐》接入第二十五条 `HASTE_READY` 代表可选费用，P4.49 已给《厄运小姐》A 版本接入第二十六条 `HASTE_READY` 代表可选费用，P4.50 已给《希维尔》接入第二十七条 `HASTE_READY` 代表可选费用，P4.51 已给《希维尔》A 版本接入第二十八条 `HASTE_READY` 代表可选费用，P4.52 已给《莉莉娅》接入第二十九条 `HASTE_READY` 代表可选费用，P4.53 已给《莉莉娅》A 版本接入第三十条 `HASTE_READY` 代表可选费用，P4.54 已给《阿兹尔》接入第三十一条 `HASTE_READY` 代表可选费用，P4.55 已给《阿兹尔》A 版本接入第三十二条 `HASTE_READY` 代表可选费用，P4.56 已给《金克丝》接入第三十三条 `HASTE_READY` 代表可选费用，P4.57 已给《金克丝》A 版本接入第三十四条 `HASTE_READY` 代表可选费用，P4.32 已给《美味仙灵》接入第九条 `HASTE_READY` 代表可选费用，P4.33 已给《艾克》接入第十条 `HASTE_READY` 代表可选费用，P4.34 已给《武装强袭者》接入第十一条 `HASTE_READY` 代表可选费用，P4.35 已给《远古战狂》接入第十二条 `HASTE_READY` 代表可选费用，P4.36 已给《海妖猎手》接入第十三条 `HASTE_READY` 代表可选费用，P4.37 已给《李青》接入第十四条 `HASTE_READY` 代表可选费用，P4.38 已给《李青》A 版本接入第十五条 `HASTE_READY` 代表可选费用，P4.39 已给《千尾监视者》接入第十六条 `HASTE_READY` 代表可选费用，P4.40 已给《卡莎》接入第十七条 `HASTE_READY` 代表可选费用，P4.41 已给《雷克塞》接入第十八条 `HASTE_READY` 代表可选费用，P4.42 已给《雷克塞》A 版本接入第十九条 `HASTE_READY` 代表可选费用，P4.43 已给《卡莎》A 版本接入第二十条 `HASTE_READY` 代表可选费用，其他急速彩色资源精确匹配/活跃分支仍 deferred。 |
| 强攻 | 37 | 2 | 0 | High | P4.6 已识别 profile 和数值；P4.67 已新增 `DECLARE_BATTLE` typed command 与 Core 显式拒绝前置模型；P4.100 已补同一路径拒绝 fixture；完整进攻战力修正仍 deferred。 |
| 坚守 | 24 | 4 | 0 | High | P4.6 已识别 profile 和数值；P4.67 已新增 `DECLARE_BATTLE` typed command 与 Core 显式拒绝前置模型；P4.100 已补同一路径拒绝 fixture；完整防守战力修正仍 deferred。 |
| 壁垒 | 26 | 0 | 0 | High | P4.6 已识别 profile；P4.67 已新增 `DECLARE_BATTLE` typed command 与 Core 显式拒绝前置模型；P4.100 已补同一路径拒绝 fixture；完整承伤顺序和同优先级选择仍 deferred。 |
| 后排 | 6 | 0 | 0 | High | P4.6 已识别 profile；P4.67 已新增 `DECLARE_BATTLE` typed command 与 Core 显式拒绝前置模型；P4.100 已补同一路径拒绝 fixture；完整承伤顺序仍 deferred。 |
| 游走 | 38 | 4 | 0 | Medium/High | P4.6 已识别 profile；P4.65 已新增 `MOVE_UNIT` typed command 与 Core 显式拒绝前置模型；P4.98 已补 `MOVE_UNIT` 前置拒绝 fixture；真实跨战场移动需要多战场目的地、移动权限和移动触发。 |
| 瞬息 | 21 | 7 | 2 | Medium | P4.3 候选；P2 已记录标签，缺“控制者下个回合开始、得分前摧毁”。 |
| 绝念 | 25 | 0 | 0 | High | P4.9 已识别 profile；离场触发队列和摧毁来源时序仍 deferred。 |
| 预知 | 12 | 0 | 0 | Medium | P4.9 已识别 profile，并标注已审计顶牌回收/不回收代表路径 delegated to P2；P4.101 已把《占卜贝壳》选择非顶部牌的拒绝边界纳入 fixture；广义授予与隐藏信息仍 deferred。 |
| 狩猎 | 14 | 0 | 0 | Medium/High | P4.7 已识别 profile 和数值；P4.10 只覆盖固定打出获得经验，征服/据守事件经验仍 deferred。 |
| 等级 | 15 | 3 | 0 | Medium/High | P4.7 已识别 profile 和阈值；P4.15 已给《踏苔蜥》接入 `等级3` 入场 +1 战力与法盾代表路径，P4.16 已给《风行狐》接入 `等级3` 入场 +1 战力与游走代表路径，P4.17 已给《无极学徒》接入 `等级6` 打出抽 1 代表路径，P4.28 已给《易》接入 `等级6` 法盾/游走代表路径，P4.29 已给《易》A 版本接入 `等级6` 法盾/游走代表路径，其他等级条件仍 deferred。 |
| 鼓舞 | 12 | 3 | 0 | Medium | P4.7 已识别 profile；P4.14 已给《诺克萨斯新兵》接入本回合已打出其他卡牌记忆和费用 -2 代表路径，P4.21 已给《崔法利求战者》接入同回合鼓舞自增益代表路径，P4.22 已给《危险二人组》接入同回合鼓舞目标临时战力代表路径，P4.23 已给《垃圾场小霸王》接入同回合鼓舞弃 2 抽 2 代表路径，P4.24 已给《先锋队长》接入同回合鼓舞创建两名 1 战力随从代表路径，其他鼓舞分支仍 deferred。 |
| 法盾 | 47 | 1 | 1 | Medium/High | P4.7 已识别 profile 和税值；P4.12 已接入法术选择敌方场上对象的单目标税，P4.59 已用《妖异狐火》覆盖多目标法术对 `法盾` + `法盾2` 的费用聚合，P4.61 已用《秘奥义！慈悲度魂落》覆盖友方法盾目标 no-tax 边界；P4.62 已新增 `ACTIVATE_ABILITY` typed command，P4.73 已执行《蔚》无目标付费技能入栈/结算代表路径，P4.74 已要求来源对象 `cardNo` 匹配《蔚》，P4.77 已执行《泽拉斯》带目标技能对敌方 `法盾` 单位的目标税 + 横置 + 3 点伤害代表路径，P4.93 已补同一技能敌方法盾目标税 mana 不足时的拒绝 fixture，P4.78 已补同一技能选择己方 `法盾` 单位时 `spellshieldTaxMana = 0` 的 no-tax 边界，P4.79 已补同一技能来源已横置时的拒绝边界，P4.80 已补同一技能缺少目标时的拒绝边界，P4.81 已补同一技能提供两个目标时的拒绝边界，P4.82 已补同一技能携带未支持 optional cost 时的拒绝边界，P4.83 已补非《泽拉斯》来源伪造同一 ability id 时的拒绝边界，P4.84 已补同一技能选择装备等非单位目标时的拒绝边界，P4.85 已补同一技能来源不在战场时的拒绝边界，P4.86 已补同一技能来源由对手控制时的拒绝边界，并把 P4.79-P4.86/P4.93 拒绝 fixtures 纳入资源关键词聚合回放；授予/静态法盾、更多带目标技能和更复杂 FAQ 细节仍 deferred。 |
| 待命 | 47 | 7 | 0 | High | P4.9 已识别 profile；P4.63 已新增 `HIDE_CARD` typed command；P4.68 已新增 `REVEAL_CARD` typed command；P4.69 已补对手视角正面朝下对象 snapshot redaction；P4.70 已接入 `STANDBY_A` 最小正面朝下放置代表路径；P4.94 已补同一路径费用不足拒绝 fixture；P4.71 已接入 `STANDBY_REVEAL` 基地显露代表路径；P4.72 已接入《游击战》`FREE_STANDBY_HIDE` / `STANDBY_FREE` 免费暗置代表路径；P4.96 已补 `STANDBY_FREE` 无权限拒绝 fixture；P4.102 已补《游击战》非待命废牌堆目标拒绝 fixture；P4.76 已接入 `STANDBY_REACTION` 优先权窗口无目标反应入栈代表路径；P4.95 已补同一路径无优先权窗口拒绝 fixture；待命触发、战场/完整隐藏区位置限制和目标伤害仍 deferred。 |
| 回响 | 22 | 2 | 0 | Medium | P4.4 已完成 mana-only optional cost/repeat 模型；复杂额外费用、授予回响和模式重复仍后续拆分。 |
| 伏击 | 18 | 0 | 0 | High | P4.9 已识别 profile；P4.64 已新增 `PLAY_CARD mode=AMBUSH` + `destination` 前置模型并在 Core 显式拒绝；P4.97 已把该显式拒绝边界纳入 conformance fixture 和互动关键词聚合回放；反应战场打出和战场目的地结算仍 deferred。 |
| 装配 | 51 | 0 | 0 | High | P4.8 已识别 profile；P4.58 已审计并锁定《取放自如》贴附/卸除武装的代表执行路径；P4.66 已新增 `ASSEMBLE_EQUIPMENT` typed command 与 Core 显式拒绝前置模型；P4.99 已补同一路径拒绝 fixture；装配费用、装备未激活文本和自动贴附仍 deferred。 |
| 灵便 | 6 | 0 | 0 | High | P4.8 已识别 profile；P4.58 的代表路径只覆盖已有反应法术《取放自如》；P4.66 已为灵便装备后续自动贴附保留 `ASSEMBLE_EQUIPMENT` envelope，P4.99 已补前置拒绝 fixture；真实反应打出和自动贴附仍 deferred。 |
| 百炼 | 16 | 2 | 0 | High | P4.8 已识别 profile；P4.66 已为百炼 optional attach 保留 `ASSEMBLE_EQUIPMENT` envelope，P4.99 已补前置拒绝 fixture；FAQ 指明的可选装配和贴附边界仍 deferred，P4.58/P4.66 不改变该边界。 |

## Official Text Anchors

P4.0 选出下一批最小代表，不代表已完成规则执行。

| Candidate | Official card text anchor | Existing evidence/tests | Next action |
|---|---|---|---|
| Draw | `SFD·087/221 先知之兆`：抽三张牌。 | `p2-preflight-play-prophets-omen-draw-stack`；P2 抽牌/燃尽规则已覆盖。 | P4.5 已生成 fixed draw primitive plan；状态写入仍由 P2。 |
| Damage | `OGS·003/024 焚烧`：对一名单位造成 2 点伤害。 | `p2-preflight-play-incinerate-damage-stack` 与致命伤害清理族。 | P4.5 已生成 fixed damage primitive plan；动态伤害继续委托 P2。 |
| Destroy | `OGN·229/298 复仇`：摧毁一名单位。 | `p2-preflight-play-vengeance-destroy-unit-stack`；已有摧毁/放逐替代代表路径。 | P4.5 已生成 destroy target primitive plan；替代/触发另拆。 |
| Stun | `OGN·050/298 符文禁锢`：眩晕一名单位。 | `p2-preflight-play-rune-prison-stun-stack` 与 end-turn expiry fixture。 | P4.5 已生成 `STUNNED` primitive plan；状态写入和到期仍由 P2。 |
| Temp might | `OGN·004/298 顺劈`：让一名单位本回合内获得强攻 3。 | `p2-preflight-play-cleave-overwhelm-attacking-power`；P2 已有 `POWER_MODIFIED_UNTIL_END_OF_TURN` 和清理。 | P4.5 已生成 until-end-of-turn power primitive plan；完整战斗强攻仍另拆。 |
| Move | `OGN·043/298 魅惑妖术` / `OGN·168/298 战或逃`：将战场单位移动到基地；`SFD·235/221 亚索`：游走、单回合第三次移动得分。 | P2 已有 `UNIT_MOVED_TO_BASE` 原语；P4.65 新增 `P4MoveUnitCommandIsExplicitlyRejectedUntilRoamMovementExists` / `GameCommandMapperParsesMoveUnitPayload`；P4.98 新增 `P4MoveUnitCommandRejectionFixture` / `p4-move-unit-command-premodel-rejected.fixture.json`。 | P4.5 明确继续 `delegated-to-p2`；P4.65 已建立 `MOVE_UNIT` command envelope 并显式拒绝执行；P4.98 已把该拒绝边界纳入 conformance fixture；多战场目的地、游走权限与移动触发后续建模。 |
| Recall | `OGN·188/298 祖安保镖`：让战场单位返回所属者手牌。 | P2 已有 `UNIT_RETURNED_TO_HAND` / `EQUIPMENT_RETURNED_TO_HAND`。 | P4.5 明确继续 `delegated-to-p2`；隐藏/控制权边界另拆。 |
| Echo | `SFD·031/221 点沙成兵` / `UNL-061/219 台前作秀`：回响 2，重复法术效果。 | P2 已有 `ECHO` optional cost 和 repeat count 样例。 | P4.4 已把 mana-only 回响接入显式 profile/helper；复杂费用与授予回响继续 deferred。 |
| Ephemeral | `UNL-149/219 蒙面侍者` / `OGN·094/298 精灵召唤`：瞬息会在控制者开始阶段开始时摧毁。 | P2 已记录 `瞬息` 标签；P4.3 新增 turn-start 到期摧毁 fixture。 | 已完成最小到期路径；绝念/贴附/战斗触发另拆。 |
| Swift/Reaction/Haste | `OGN·004/298 顺劈`、`OGN·064/298 风之障壁`、`OGN·001/298 灼焰飞龙`、`UNL-006/219 小鲨鱼`、`OGN·010/298 军团后卫`、`OGN·030/298 金克丝`、`OGN·030a/298 金克丝`、`UNL-127/219 树根先生`、`SFD·068/221 机械迷`、`SFD·103/221 琢珥鱼`、`SFD·179/221 卡银娜·薇蕊泽`、`UNL-029/219 绯红印记树怪`、`UNL-029a/219 绯红印记树怪`、`UNL-115/219 尼菈`、`UNL-024/219 雷恩加尔`、`UNL-024a/219 雷恩加尔`、`OGN·162/298 厄运小姐`、`OGN·162a/298 厄运小姐`、`SFD·143/221 希维尔`、`SFD·143a/221 希维尔`、`UNL-082/219 莉莉娅`、`UNL-082a/219 莉莉娅`、`SFD·177/221 阿兹尔`、`SFD·177a/221 阿兹尔`、`OGN·075/298 美味仙灵`、`OGN·110/298 艾克`、`SFD·002/221 武装强袭者`、`SFD·131/221 远古战狂`、`OGN·150/298 海妖猎手`、`OGN·151/298 李青`、`OGN·151a/298 李青`、`OGN·116/298 千尾监视者`、`OGN·039/298 卡莎`、`OGN·039a/298 卡莎`、`SFD·029/221 雷克塞`、`SFD·029a/221 雷克塞`。 | P2 已有反应优先权窗口和急速不支付额外费用入场路径；P4.13 新增 `p4-play-blazing-drake-haste-ready`，P4.18 新增 `p4-play-baby-shark-haste-ready`，P4.20 新增 `p4-play-legion-rearguard-haste-ready`，P4.56 新增 `p4-play-jinx-haste-ready`，P4.57 新增 `p4-play-jinx-alt-a-haste-ready`，P4.25 新增 `p4-play-mr-root-haste-ready`，P4.26 新增 `p4-play-mech-maniac-haste-ready`，P4.27 新增 `p4-play-xersai-fish-haste-ready`，P4.30 新增 `p4-play-karina-veraze-haste-ready`，P4.31 新增 `p4-play-crimson-signet-treant-haste-ready`，P4.44 新增 `p4-play-crimson-signet-treant-alt-a-haste-ready`，P4.45 新增 `p4-play-nilah-haste-ready`，P4.46 新增 `p4-play-rengar-haste-ready`，P4.47 新增 `p4-play-rengar-alt-a-haste-ready`，P4.48 新增 `p4-play-miss-fortune-haste-ready`，P4.49 新增 `p4-play-miss-fortune-alt-a-haste-ready`，P4.50 新增 `p4-play-sivir-haste-ready`，P4.51 新增 `p4-play-sivir-alt-a-haste-ready`，P4.52 新增 `p4-play-lillia-haste-ready`，P4.53 新增 `p4-play-lillia-alt-a-haste-ready`，P4.54 新增 `p4-play-azir-haste-ready`，P4.55 新增 `p4-play-azir-alt-a-haste-ready`，P4.32 新增 `p4-play-tasty-faerie-haste-ready`，P4.33 新增 `p4-play-ekko-haste-ready`，P4.34 新增 `p4-play-armed-assaulter-haste-ready`，P4.35 新增 `p4-play-ancient-berserker-haste-ready`，P4.36 新增 `p4-play-kraken-hunter-haste-ready`，P4.37 新增 `p4-play-lee-sin-haste-ready`，P4.38 新增 `p4-play-lee-sin-alt-a-haste-ready`，P4.39 新增 `p4-play-thousand-tailed-watcher-haste-ready`，P4.40 新增 `p4-play-kaisa-haste-ready`，P4.43 新增 `p4-play-kaisa-alt-a-haste-ready`，P4.41 新增 `p4-play-reksai-haste-ready`，P4.42 新增 `p4-play-reksai-alt-a-haste-ready`。 | P4.2 已建立权限关键词 profile/timing model；已接入 `顺劈` 法术对决焦点窗口、《灼焰飞龙》《小鲨鱼》《军团后卫》《金克丝》《金克丝》A 版本、《树根先生》《机械迷》《琢珥鱼》《卡银娜·薇蕊泽》《绯红印记树怪》《绯红印记树怪》A 版本、《尼菈》《雷恩加尔》《雷恩加尔》A 版本、《厄运小姐》《厄运小姐》A 版本、《希维尔》《希维尔》A 版本、《莉莉娅》、《莉莉娅》A 版本、《阿兹尔》、《阿兹尔》A 版本、《美味仙灵》《艾克》《武装强袭者》《远古战狂》《海妖猎手》《李青》《李青》A 版本、《千尾监视者》《卡莎》《卡莎》A 版本、《雷克塞》和《雷克塞》A 版本 `HASTE_READY` 代表路径，其他急速牌的彩色资源/活跃分支仍 deferred。 |
| Combat keywords | `OGS·007/024 盖伦`：强攻2、坚守2；`UNL-036/219 变异猫咪`：坚守2、壁垒；`UNL-090/219 乐芙兰`：后排；`SFD·096/221 劳伦特护刃者`：游走。 | P2 已有大量 keyword-unit fixture 记录标签；P4.65 新增 `P4MoveUnitCommandIsExplicitlyRejectedUntilRoamMovementExists` / `P4MoveUnitCommandRejectionFixture` / `GameCommandMapperParsesMoveUnitPayload` / `p4-move-unit-command-premodel-rejected.fixture.json`；P4.67 新增 `P4DeclareBattleCommandIsExplicitlyRejectedUntilCombatSystemExists` / `GameCommandMapperParsesDeclareBattlePayload`；P4.100 新增 `P4DeclareBattleCommandRejectionFixture` / `p4-declare-battle-command-premodel-rejected.fixture.json`。 | P4.6 已建立 combat keyword profile；P4.65 已建立游走/基础移动 command envelope；P4.98 已把 `MOVE_UNIT` 显式拒绝边界纳入 conformance fixture 和战斗关键词聚合回放；P4.67 已建立战斗声明 command envelope 并显式拒绝执行，P4.100 已把 `DECLARE_BATTLE` 显式拒绝边界纳入 conformance fixture 和战斗关键词聚合回放；完整战斗/移动执行仍 deferred。 |
| Resource keywords | `UNL-100/219 贪食魔沼蛙`：狩猎3；`UNL-047/219 踏苔蜥`：狩猎2、等级3；`UNL-075/219 风行狐`：狩猎2、等级3；`UNL-040/219 无极学徒`：狩猎、等级6；`UNL-113/219 易`：狩猎2、等级6；`UNL-113a/219 易`：狩猎2、等级6；`OGN·012/298 诺克萨斯新兵`：鼓舞费用；`OGN·016/298 危险二人组`：鼓舞目标临时战力；`OGN·020/298 垃圾场小霸王`：鼓舞弃 2 抽 2；`OGN·217/298 崔法利求战者`：鼓舞自增益；`OGN·218/298 先锋队长`：鼓舞创建两名随从；`OGN·013/298 呸呸魄罗`：法盾；`SFD·085/221 奥恩`：法盾2；`OGN·256/298 妖异狐火`：多目标法盾税代表法术；`OGN·053/298 秘奥义！慈悲度魂落`：友方法盾目标 no-tax 代表法术；`UNL-030/219 蔚`：法盾与“支付2和红色”激活技能文本；`UNL-026/219 泽拉斯`：红色+横置对一名单位造成 3 点伤害。 | P2 已有 keyword-unit fixture 记录标签或 no-optional 分支；P4.12 新增 `p4-play-incinerate-spellshield-tax`；P4.104 新增 `p4-play-incinerate-spellshield-tax-insufficient-rejected`；P4.14 新增 `p4-play-noxian-recruit-encourage-cost-reduction`；P4.15 新增 `p4-play-moss-stepper-level3-spellshield`；P4.16 新增 `p4-play-windrunner-fox-level3-roam`；P4.17 新增 `p4-play-wuji-apprentice-level6-draw`；P4.21 新增 `p4-play-trifarian-gloryseeker-encourage-self-boon`；P4.22 新增 `p4-play-dangerous-duo-encourage-target-temp-might`；P4.23 新增 `p4-play-junkyard-bully-encourage-discard-draw`；P4.24 新增 `p4-play-vanguard-captain-encourage-create-minions`；P4.28 新增 `p4-play-unl-yi-level6-spellshield-roam`；P4.29 新增 `p4-play-unl-yi-alt-a-level6-spellshield-roam`；P4.59 新增 `p4-play-spirit-fire-multiple-spellshield-tax`；P4.61 新增 `p4-play-secret-art-mercy-friendly-spellshield-no-tax`；P4.62 新增 `GameCommandMapperParsesActivateAbilityPayload`；P4.73/P4.74 新增 `P4ActivateAbilityCommandAddsViDoublePowerSkillToStack` / `P4ActivateAbilityCommandRejectsViDoublePowerSkillWithTargets` / `P4ActivateAbilityCommandRejectsViDoublePowerSkillWhenPowerIsMissing` / `P4ActivateAbilityCommandRejectsViDoublePowerSkillFromNonViSource` / `P4ActivateAbilityCommandResolvesViDoublePowerSkillOnStack` / `P4ActivateAbilityCommandUsesCardIdentityAfterViIsPlayed` / `p4-activate-vi-double-power-skill` / `p4-play-then-activate-vi-double-power-skill`；P4.87-P4.92 新增 `P4ActivateAbilityCommandRejectsViDoublePowerSkillFromOpponentSource` / `P4ActivateAbilityCommandRejectsViDoublePowerSkillOpponentSourceFixture` / `P4ActivateAbilityCommandRejectsViDoublePowerSkillWhenSourceIsNotField` / `P4ActivateAbilityCommandRejectsViDoublePowerSkillSourceNotFieldFixture` / `P4ActivateAbilityCommandRejectsViDoublePowerSkillTargetFixture` / `P4ActivateAbilityCommandRejectsViDoublePowerSkillWithOptionalCosts` / `P4ActivateAbilityCommandRejectsViDoublePowerSkillOptionalCostFixture` / `p4-activate-vi-double-power-skill-opponent-source-rejected` / `p4-activate-vi-double-power-skill-source-not-field-rejected` / `p4-activate-vi-double-power-skill-target-rejected` / `p4-activate-vi-double-power-skill-optional-cost-rejected` / `P4ActivateAbilityCommandRejectsViDoublePowerSkillPowerMissingFixture` / `p4-activate-vi-double-power-skill-power-missing-rejected` / `P4ActivateAbilityCommandRejectsViDoublePowerSkillNonViSourceFixture` / `p4-activate-vi-double-power-skill-non-vi-source-rejected`；P4.77-P4.86/P4.93 新增 `P4ActivateAbilityCommandAddsXerathDamageSkillWithSpellshieldTaxToStack` / `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSpellshieldTaxManaIsMissing` / `P4ActivateAbilityCommandRejectsXerathDamageSkillSpellshieldTaxInsufficientFixture` / `P4ActivateAbilityCommandResolvesXerathDamageSkillWithSpellshieldTax` / `P4ActivateAbilityCommandDoesNotTaxXerathDamageSkillForFriendlySpellshieldTarget` / `P4ActivateAbilityCommandResolvesXerathDamageSkillWithoutTaxForFriendlySpellshield` / `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSourceIsExhausted` / `P4ActivateAbilityCommandRejectsXerathDamageSkillFromExhaustedSourceFixture` / `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsMissing` / `P4ActivateAbilityCommandRejectsXerathDamageSkillMissingTargetFixture` / `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTooManyTargetsAreProvided` / `P4ActivateAbilityCommandRejectsXerathDamageSkillTooManyTargetsFixture` / `P4ActivateAbilityCommandRejectsXerathDamageSkillWithOptionalCosts` / `P4ActivateAbilityCommandRejectsXerathDamageSkillOptionalCostFixture` / `P4ActivateAbilityCommandRejectsXerathDamageSkillFromNonXerathSource` / `P4ActivateAbilityCommandRejectsXerathDamageSkillNonXerathSourceFixture` / `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsNotUnit` / `P4ActivateAbilityCommandRejectsXerathDamageSkillNonUnitTargetFixture` / `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSourceIsNotBattlefield` / `P4ActivateAbilityCommandRejectsXerathDamageSkillSourceNotBattlefieldFixture` / `p4-activate-xerath-damage-skill-spellshield-tax` / `p4-activate-xerath-damage-skill-spellshield-tax-insufficient-rejected` / `p4-activate-xerath-damage-skill-friendly-spellshield-no-tax` / `p4-activate-xerath-damage-skill-exhausted-source-rejected` / `p4-activate-xerath-damage-skill-missing-target-rejected` / `p4-activate-xerath-damage-skill-too-many-targets-rejected` / `p4-activate-xerath-damage-skill-optional-cost-rejected` / `p4-activate-xerath-damage-skill-non-xerath-source-rejected` / `p4-activate-xerath-damage-skill-non-unit-target-rejected` / `p4-activate-xerath-damage-skill-source-not-battlefield-rejected`。 | P4.7 已建立 resource keyword profile；P4.12 已执行法术选择敌方场上法盾对象的 mana 目标税，P4.104 已把《焚烧》敌方法盾目标税费用不足拒绝边界纳入 fixture 和资源关键词聚合回放；P4.14 已执行《诺克萨斯新兵》鼓舞费用 -2 代表路径；P4.15 已执行《踏苔蜥》`等级3` 入场 +1 与法盾代表路径；P4.16 已执行《风行狐》`等级3` 入场 +1 与游走代表路径；P4.17 已执行《无极学徒》`等级6` 打出抽 1 代表路径；P4.21 已执行《崔法利求战者》鼓舞自增益代表路径；P4.22 已执行《危险二人组》鼓舞目标临时战力代表路径；P4.23 已执行《垃圾场小霸王》鼓舞弃 2 抽 2 代表路径；P4.24 已执行《先锋队长》鼓舞创建两名 1 战力随从代表路径；P4.28 已执行《易》`等级6` 法盾/游走代表路径；P4.29 已执行《易》A 版本 `等级6` 法盾/游走代表路径；P4.59 已执行《妖异狐火》同时选择 `法盾` 与 `法盾2` 敌方场上单位时的目标税聚合；P4.61 已执行《秘奥义！慈悲度魂落》选择己方 `法盾` 单位时 `spellshieldTaxMana = 0` 的友方目标边界；P4.73 已执行《蔚》`PAY_2_RED_DOUBLE_POWER` 无目标付费技能入栈并结算自身本回合战力翻倍，P4.74 已补场上对象 `cardNo` 身份校验并拒绝非《蔚》来源伪造 ability id，P4.87-P4.92 已锁定同一技能的对手控制来源、来源不在场上、带目标、额外费用、费用不足和非《蔚》来源拒绝边界且不改状态，并由资源关键词聚合回放覆盖；P4.77 已执行《泽拉斯》`PAY_RED_EXHAUST_DAMAGE_3` 带目标技能，对敌方 `法盾` 单位支付 1 mana 目标税 + 1 power、横置来源并结算 3 点伤害；P4.78 已执行同一技能选择己方 `法盾` 单位时 `spellshieldTaxMana = 0` 且仍支付 1 power/横置/造成 3 点伤害的边界；P4.79-P4.86/P4.93 已锁定同一技能的敌方法盾目标税费用不足、已横置来源、缺目标、多目标、额外费用、非《泽拉斯》来源、非单位目标和来源不在战场拒绝、对手控制来源拒绝边界且不改状态，并由资源关键词聚合回放覆盖；狩猎征服/据守经验、其他等级条件、其他鼓舞效果、更多技能目标税和授予/静态法盾仍 deferred。 |
| Equipment keywords | `SFD·033/221 多兰之盾`：装配绿色；`SFD·022/221 长剑`：灵便、装配红色；`SFD·008/221 哨兵好手`：百炼；`SFD·085/221 奥恩`：法盾2、百炼；`SFD·011/221 取放自如`：选择一名单位和其控制者的一件武装，为该单位贴附或卸除该武装，抽一张牌。 | P2 已有装备打出和 no-optional 百炼 fixture，记录装备/武装/灵便/百炼标签；P4.58 复用 `p2-preflight-play-take-up-attach-weapon-draw` 与 `p2-preflight-play-take-up-detach-weapon-draw` 作为武装贴附/卸除代表执行证据；P4.66 新增 `P4AssembleEquipmentCommandIsExplicitlyRejectedUntilEquipmentSystemExists` / `GameCommandMapperParsesAssembleEquipmentPayload`；P4.99 新增 `P4AssembleEquipmentCommandRejectionFixture` / `p4-assemble-equipment-command-premodel-rejected.fixture.json`。 | P4.8 已建立 equipment keyword profile；P4.58 已锁定《取放自如》贴附/卸除代表路径；P4.66 已建立 `ASSEMBLE_EQUIPMENT` command envelope 并显式拒绝执行；P4.99 已把该拒绝边界纳入 conformance fixture；装配费用、灵便自动贴附、百炼 optional attach、owner/controller 和未激活文本仍 deferred。 |
| Lifecycle remaining | `UNL-081/219 赐面守侍`：待命、瞬息；`UNL-161/219 占卜贝壳`：预知；`OGN·190/298 克格莫`：绝念。 | P4.3 瞬息 fixture；P2 已有预知回收/no-recycle fixture 与绝念静态 fixture；P4.101 新增 `P4ScryingShellPredictOutsideTopCardRejectionFixture` / `p4-scrying-shell-predict-outside-top-card-rejected.fixture.json`。 | P4.9 已建立 lifecycle keyword profile；P4.101 已把预知非顶部目标拒绝纳入 conformance fixture 和生命周期关键词聚合回放；绝念 trigger queue 和广义预知授予仍 deferred。 |
| Interaction remaining | `OGN·121/298 提莫`：待命；`OGN·199/298 控潮者`：待命；`OGN·264/298 游击战`：待命免费暗置权限；`UNL-021/219 阴森药剂师`：伏击；`UNL-176a/219 蔚`：伏击。 | P2 已有普通打出/静态 fixture；`回响` 已有 P4.4 mana-only 执行路径；P4.63 新增 `GameCommandMapperParsesHideCardPayload`；P4.64 新增 `P4AmbushPlayCardModeIsExplicitlyRejectedUntilBattlefieldReactionPlayExists` / `GameCommandMapperParsesAmbushPlayCardDestination`；P4.97 新增 `P4AmbushPlayCardModeRejectionFixture` / `p4-ambush-play-card-premodel-rejected.fixture.json`；P4.68 新增 `GameCommandMapperParsesRevealCardPayload`；P4.69 新增 `SnapshotsRedactOpponentFaceDownObjects`；P4.70 新增 `P4HideCardCommandPlacesStandbyCardFaceDown` / `P4HideCardCommandRejectsInsufficientStandbyCost` / `p4-hide-card-standby-face-down.fixture.json`；P4.94 新增 `P4HideCardCommandRejectsInsufficientStandbyCostFixture` / `p4-hide-card-standby-insufficient-cost-rejected.fixture.json`；P4.71 新增 `P4RevealCardCommandRevealsStandbyCardInBase` / `p4-reveal-card-standby-base.fixture.json`；P4.72 新增 `P4HideCardCommandUsesGuerrillaWarfareFreeStandbyPermission` / `P4HideCardCommandRejectsFreeStandbyWithoutGuerrillaWarfarePermission` / `p4-guerrilla-warfare-free-standby-hide.fixture.json`；P4.96 新增 `P4HideCardCommandRejectsFreeStandbyWithoutGuerrillaWarfarePermissionFixture` / `p4-hide-card-standby-free-without-permission-rejected.fixture.json`；P4.102 新增 `P4GuerrillaWarfareNonStandbyTargetRejectionFixture` / `p4-guerrilla-warfare-non-standby-target-rejected.fixture.json`；P4.76 新增 `P4RevealCardCommandPlaysStandbyReactionToStack` / `P4RevealCardCommandRejectsReactionPlayWithoutPriorityWindow` / `p4-reveal-card-standby-reaction-stack.fixture.json`；P4.95 新增 `P4RevealCardCommandRejectsReactionPlayWithoutPriorityWindowFixture` / `p4-reveal-card-standby-reaction-without-priority-rejected.fixture.json`。 | P4.9 已建立 interaction keyword profile；P4.70 已执行 `HIDE_CARD destination=STANDBY optionalCosts=["STANDBY_A"]` 最小待命正面朝下放置；P4.94 已把同一路径 mana 不足拒绝边界纳入 conformance fixture 和互动关键词聚合回放；P4.71 已执行 `REVEAL_CARD mode=STANDBY_REVEAL destination=BASE optionalCosts=["STANDBY_REVEAL_0"]` 基地显露；P4.72 已执行《游击战》授予 `FREE_STANDBY_HIDE:{playerId}` 后的 `HIDE_CARD optionalCosts=["STANDBY_FREE"]` 免费暗置；P4.96 已把 `STANDBY_FREE` 无权限拒绝边界纳入 conformance fixture 和互动关键词聚合回放；P4.102 已把《游击战》非待命废牌堆目标拒绝边界纳入 conformance fixture 和互动关键词聚合回放；P4.76 已执行 `REVEAL_CARD mode=STANDBY_REACTION destination=STACK optionalCosts=["STANDBY_REVEAL_0"]` 的无目标反应入栈代表路径；P4.95 已把同一反应路径无优先权窗口拒绝边界纳入 conformance fixture 和互动关键词聚合回放；P4.64 已建立 `PLAY_CARD mode=AMBUSH` + `destination` envelope 并显式拒绝执行，P4.97 已把该伏击拒绝边界纳入 conformance fixture 和互动关键词聚合回放；待命触发/完整隐藏区/目标伤害和伏击 reaction battlefield play 仍 deferred。 |
| Basic action remaining | `UNL-103/219 处置命令`：回收；`OGN·156/298 暗中破坏`：回收；`OGN·102/298 传送门大营救`：放逐并重新打出；`OGN·053/298 秘奥义！慈悲度魂落`：增益；`UNL-158/219 牧人的传家宝`：经验；`UNL-040/219 无极学徒`：等级打出抽牌；`UNL-157/219 严厉军士`：按友方场上单位获得经验。 | P2 已有回收/放逐/增益代表路径；P4.10 新增固定打出获得经验 fixture；P4.17 新增 `p4-play-wuji-apprentice-level6-draw`；P4.19 新增 `p4-play-stern-sergeant-dynamic-experience`；P4.60 新增 `recycle` / `banish` / `boon` template id、parser、registry 与 safe delegation tests。 | P4.10 已执行 `UNL-092/219`、`UNL-034/219`、`UNL-158/219` 的固定获得经验；P4.17 已执行《无极学徒》等级 6 打出抽 1；P4.19 已执行《严厉军士》按友方场上单位数量获得经验；P4.60 已把回收/放逐/增益纳入 P3 template skeleton 并继续委托 P2 状态写入；经验消耗/激活技能、条件经验和复杂隐藏信息/多玩家选择仍 deferred。 |

P4.80 更新：Resource keywords 行在 P4.79 基础上追加 `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsMissing`、`P4ActivateAbilityCommandRejectsXerathDamageSkillMissingTargetFixture` 和 `p4-activate-xerath-damage-skill-missing-target-rejected`，只锁定《泽拉斯》缺少“一名单位”目标时拒绝且不改状态。

P4.81 更新：Resource keywords 行在 P4.80 基础上追加 `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTooManyTargetsAreProvided`、`P4ActivateAbilityCommandRejectsXerathDamageSkillTooManyTargetsFixture` 和 `p4-activate-xerath-damage-skill-too-many-targets-rejected`，只锁定《泽拉斯》提供两个目标时拒绝且不改状态。

P4.82 更新：Resource keywords 行在 P4.81 基础上追加 `P4ActivateAbilityCommandRejectsXerathDamageSkillWithOptionalCosts`、`P4ActivateAbilityCommandRejectsXerathDamageSkillOptionalCostFixture` 和 `p4-activate-xerath-damage-skill-optional-cost-rejected`，只锁定《泽拉斯》携带未支持 optional cost 时拒绝且不改状态。

P4.83 更新：Resource keywords 行在 P4.82 基础上追加 `P4ActivateAbilityCommandRejectsXerathDamageSkillFromNonXerathSource`、`P4ActivateAbilityCommandRejectsXerathDamageSkillNonXerathSourceFixture` 和 `p4-activate-xerath-damage-skill-non-xerath-source-rejected`，只锁定非《泽拉斯》来源伪造同一 ability id 时拒绝且不改状态。

P4.84 更新：Resource keywords 行在 P4.83 基础上追加 `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsNotUnit`、`P4ActivateAbilityCommandRejectsXerathDamageSkillNonUnitTargetFixture` 和 `p4-activate-xerath-damage-skill-non-unit-target-rejected`，只锁定《泽拉斯》技能选择场上装备等非单位目标时拒绝且不改状态；同时把 P4.79-P4.84 的《泽拉斯》拒绝 fixtures 加入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.85 更新：Resource keywords 行在 P4.84 基础上追加 `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSourceIsNotBattlefield`、`P4ActivateAbilityCommandRejectsXerathDamageSkillSourceNotBattlefieldFixture` 和 `p4-activate-xerath-damage-skill-source-not-battlefield-rejected`，只锁定《泽拉斯》技能来源位于基地而非战场时拒绝且不改状态；当时把 P4.79-P4.85 的《泽拉斯》拒绝 fixtures 加入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.86 更新：Resource keywords 行在 P4.85 基础上追加 `P4ActivateAbilityCommandRejectsXerathDamageSkillFromOpponentSource`、`P4ActivateAbilityCommandRejectsXerathDamageSkillOpponentSourceFixture` 和 `p4-activate-xerath-damage-skill-opponent-source-rejected`，只锁定《泽拉斯》技能来源由对手控制时拒绝且不改状态；同时把 P4.79-P4.86 的《泽拉斯》拒绝 fixtures 加入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.87 更新：Resource keywords 行在 P4.86 基础上追加 `P4ActivateAbilityCommandRejectsViDoublePowerSkillFromOpponentSource`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillOpponentSourceFixture` 和 `p4-activate-vi-double-power-skill-opponent-source-rejected`，只锁定《蔚》技能来源由对手控制时拒绝且不改状态；同时把该 fixture 加入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.88 更新：Resource keywords 行在 P4.87 基础上追加 `P4ActivateAbilityCommandRejectsViDoublePowerSkillWhenSourceIsNotField`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillSourceNotFieldFixture` 和 `p4-activate-vi-double-power-skill-source-not-field-rejected`，只锁定《蔚》技能来源仍在手牌、未进入场上时拒绝且不改状态；同时把该 fixture 加入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.89 更新：Resource keywords 行在 P4.88 基础上追加 `P4ActivateAbilityCommandRejectsViDoublePowerSkillTargetFixture` 和 `p4-activate-vi-double-power-skill-target-rejected`，把已有直接测试覆盖的《蔚》无目标技能携带目标拒绝边界纳入 conformance fixture 和资源关键词聚合回放。

P4.90 更新：Resource keywords 行在 P4.89 基础上追加 `P4ActivateAbilityCommandRejectsViDoublePowerSkillWithOptionalCosts`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillOptionalCostFixture` 和 `p4-activate-vi-double-power-skill-optional-cost-rejected`，把已有 Core 拒绝模型的《蔚》无目标技能携带未支持 optional cost 边界纳入直接测试、conformance fixture 和资源关键词聚合回放。

P4.91 更新：Resource keywords 行在 P4.90 基础上追加 `P4ActivateAbilityCommandRejectsViDoublePowerSkillPowerMissingFixture` 和 `p4-activate-vi-double-power-skill-power-missing-rejected`，把已有直接测试覆盖的《蔚》无目标技能缺少 power 费用拒绝边界纳入 conformance fixture 和资源关键词聚合回放。

P4.92 更新：Resource keywords 行在 P4.91 基础上追加 `P4ActivateAbilityCommandRejectsViDoublePowerSkillNonViSourceFixture` 和 `p4-activate-vi-double-power-skill-non-vi-source-rejected`，把已有直接测试覆盖的非《蔚》来源伪造同一 ability id 拒绝边界纳入 conformance fixture 和资源关键词聚合回放。

P4.93 更新：Resource keywords 行在 P4.92 基础上追加 `P4ActivateAbilityCommandRejectsXerathDamageSkillSpellshieldTaxInsufficientFixture` 和 `p4-activate-xerath-damage-skill-spellshield-tax-insufficient-rejected`，把已有直接测试覆盖的《泽拉斯》技能敌方法盾目标税 mana 不足拒绝边界纳入 conformance fixture 和资源关键词聚合回放。

P4.94 更新：Interaction remaining 行在 P4.93 基础上追加 `P4HideCardCommandRejectsInsufficientStandbyCostFixture` 和 `p4-hide-card-standby-insufficient-cost-rejected.fixture.json`，把已有直接测试覆盖的待命 `HIDE_CARD` / `STANDBY_A` 费用不足拒绝边界纳入 conformance fixture 和互动关键词聚合回放。

P4.95 更新：Interaction remaining 行在 P4.94 基础上追加 `P4RevealCardCommandRejectsReactionPlayWithoutPriorityWindowFixture` 和 `p4-reveal-card-standby-reaction-without-priority-rejected.fixture.json`，把已有直接测试覆盖的待命 `REVEAL_CARD` / `STANDBY_REACTION` 无优先权窗口拒绝边界纳入 conformance fixture 和互动关键词聚合回放。

P4.96 更新：Interaction remaining 行在 P4.95 基础上追加 `P4HideCardCommandRejectsFreeStandbyWithoutGuerrillaWarfarePermissionFixture` 和 `p4-hide-card-standby-free-without-permission-rejected.fixture.json`，把已有直接测试覆盖的待命 `HIDE_CARD` / `STANDBY_FREE` 无《游击战》免费暗置权限拒绝边界纳入 conformance fixture 和互动关键词聚合回放。

P4.97 更新：Interaction remaining 行在 P4.96 基础上追加 `P4AmbushPlayCardModeRejectionFixture` 和 `p4-ambush-play-card-premodel-rejected.fixture.json`，把已有直接测试覆盖的伏击 `PLAY_CARD mode=AMBUSH` 前置模型拒绝边界纳入 conformance fixture 和互动关键词聚合回放。

P4.98 更新：Move / Combat / Basic action 行在 P4.97 基础上追加 `P4MoveUnitCommandRejectionFixture` 和 `p4-move-unit-command-premodel-rejected.fixture.json`，把已有直接测试覆盖的游走 `MOVE_UNIT` 前置模型拒绝边界纳入 conformance fixture、战斗关键词聚合和基础动作聚合回放。

P4.99 更新：Equipment keywords 行在 P4.98 基础上追加 `P4AssembleEquipmentCommandRejectionFixture` 和 `p4-assemble-equipment-command-premodel-rejected.fixture.json`，把已有直接测试覆盖的装备 `ASSEMBLE_EQUIPMENT` 前置模型拒绝边界纳入 conformance fixture 和装备关键词聚合回放。

P4.100 更新：Combat keywords 行在 P4.99 基础上追加 `P4DeclareBattleCommandRejectionFixture` 和 `p4-declare-battle-command-premodel-rejected.fixture.json`，把已有直接测试覆盖的战斗 `DECLARE_BATTLE` 前置模型拒绝边界纳入 conformance fixture 和战斗关键词聚合回放。

P4.101 更新：Lifecycle remaining 行在 P4.100 基础上追加 `P4ScryingShellPredictOutsideTopCardRejectionFixture` 和 `p4-scrying-shell-predict-outside-top-card-rejected.fixture.json`，把已有直接测试覆盖的《占卜贝壳》`预知` 非顶部牌目标拒绝边界纳入 conformance fixture 和生命周期关键词聚合回放。

P4.102 更新：Interaction remaining 行在 P4.101 基础上追加 `P4GuerrillaWarfareNonStandbyTargetRejectionFixture` 和 `p4-guerrilla-warfare-non-standby-target-rejected.fixture.json`，把已有直接测试覆盖的《游击战》非待命废牌堆目标拒绝边界纳入 conformance fixture 和互动关键词聚合回放。

P4.103 更新：Permission keywords 行在 P4.102 基础上追加 `P4HasteOptionalReadyBranchRejectsInsufficientPowerFixture` 和 `p4-play-blazing-drake-haste-ready-insufficient-power-rejected.fixture.json`，把已有直接测试覆盖的《灼焰飞龙》`HASTE_READY` power 不足拒绝边界纳入 conformance fixture 和权限关键词聚合回放。

P4.104 更新：Resource keywords 行在 P4.103 基础上追加 `P4IncinerateSpellshieldTaxInsufficientFixture` 和 `p4-play-incinerate-spellshield-tax-insufficient-rejected.fixture.json`，把已有直接测试覆盖的《焚烧》选择敌方 `法盾` 单位但无法支付目标税的拒绝边界纳入 conformance fixture 和资源关键词聚合回放。

P4.105 更新：Resource keywords 行在 P4.104 基础上追加 `P4MultipleSpellshieldTaxInsufficientFixture` 和 `p4-play-spirit-fire-multiple-spellshield-tax-insufficient-rejected.fixture.json`，把已有直接测试覆盖的《妖异狐火》同时选择敌方 `法盾` 与 `法盾2` 单位但无法支付聚合目标税的拒绝边界纳入 conformance fixture 和资源关键词聚合回放。

P4.106 更新：Basic action 行在 P4.105 基础上追加 `P4SpiritFireTotalPowerTooHighFixture` 和 `p4-play-spirit-fire-total-power-too-high-rejected.fixture.json`，把已有直接测试覆盖的《妖异狐火》目标总战力超过 4 拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.107 更新：Basic action 行在 P4.106 基础上追加 `P4PlayfulTentaclesTotalPowerTooHighFixture` 和 `p4-play-playful-tentacles-total-power-too-high-rejected.fixture.json`，把已有直接测试覆盖的《顽皮触手》目标总战力超过 8 拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.108 更新：Basic action 行在 P4.107 基础上追加 `P4HuntTheWeakTargetPowerTooHighFixture` 和 `p4-play-hunt-the-weak-target-power-too-high-rejected.fixture.json`，把已有直接测试覆盖的《狩魂》4 战力目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.109 更新：Basic action 行在 P4.108 基础上追加 `P4GustTargetPowerTooHighFixture` 和 `p4-play-gust-target-power-too-high-rejected.fixture.json`，把已有直接测试覆盖的《罡风》4 战力目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.110 更新：Basic action 行在 P4.109 基础上追加 `P4HighwayRobberyOffFieldTargetRejectedFixture` 和 `p4-play-highway-robbery-off-field-target-rejected.fixture.json`，把已有直接测试覆盖的《巧取豪夺》离场敌方目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.111 更新：Basic action 行在 P4.110 基础上追加 `P4DeadlyFlourishFriendlyTargetRejectedFixture` 和 `p4-play-deadly-flourish-friendly-target-rejected.fixture.json`，把已有直接测试覆盖的《致命华彩》友方目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.112 更新：Basic action 行在 P4.111 基础上追加 `P4BloodMoneyTargetPowerTooHighRejectedFixture` 和 `p4-play-blood-money-target-power-too-high-rejected.fixture.json`，把已有直接测试覆盖的《血钱》3 战力目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.113 更新：Basic action 行在 P4.112 基础上追加 `P4PunishmentBaseUnitTargetRejectedFixture` 和 `p4-play-punishment-base-unit-target-rejected.fixture.json`，把已有直接测试覆盖的《惩戒》基地单位目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.114 更新：Basic action 行在 P4.113 基础上追加 `P4KerplunkNonAttackingTargetRejectedFixture` 和 `p4-play-kerplunk-non-attacking-target-rejected.fixture.json`，把已有直接测试覆盖的《扑咚！》非进攻方目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.115 更新：Basic action 行在 P4.114 基础上追加 `P4ZenithBladeBaseUnitTargetRejectedFixture` 和 `p4-play-zenith-blade-base-unit-target-rejected.fixture.json`，把已有直接测试覆盖的《天顶之刃》敌方基地单位目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.116 更新：Basic action 行在 P4.115 基础上追加 `P4ZenithBladeFriendlyTargetRejectedFixture` 和 `p4-play-zenith-blade-friendly-target-rejected.fixture.json`，把已有直接测试覆盖的《天顶之刃》友方单位目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.117 更新：Basic action 行在 P4.116 基础上追加 `P4LastBreathEnemyBaseTargetRejectedFixture` 和 `p4-play-last-breath-enemy-base-target-rejected.fixture.json`，把已有直接测试覆盖的《狂风绝息斩》敌方基地单位第二目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.118 更新：Basic action 行在 P4.117 基础上追加 `P4LastBreathTargetOrderRejectedFixture` 和 `p4-play-last-breath-target-order-rejected.fixture.json`，把已有直接测试覆盖的《狂风绝息斩》目标顺序错误拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.119 更新：Basic action 行在 P4.118 基础上追加 `P4ConvergentMutationEnemyTargetRejectedFixture` 和 `p4-play-convergent-mutation-enemy-target-rejected.fixture.json`，把已有直接测试覆盖的《聚合变异》第二目标选择敌方单位拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.120 更新：Basic action 行在 P4.119 基础上追加 `P4ConvergentMutationDuplicateTargetRejectedFixture` 和 `p4-play-convergent-mutation-duplicate-target-rejected.fixture.json`，把已有直接测试覆盖的《聚合变异》重复目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.121 更新：Interaction 行在 P4.120 基础上追加 `P4ExistentialDreadFriendlyAttackingTargetRejectedFixture` 和 `p4-play-existential-dread-friendly-attacking-target-rejected.fixture.json`，把已有直接测试覆盖的《存在焦虑》友方进攻单位目标拒绝边界纳入 conformance fixture 和互动关键词聚合回放。

P4.122 更新：Basic action 行在 P4.121 基础上追加 `P4ThunderingSkyInsufficientReducedCostRejectedFixture` 和 `p4-play-thundering-sky-insufficient-reduced-cost-rejected.fixture.json`，把已有直接测试覆盖的《霹天雳地》费用不足拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.123 更新：Basic action 行在 P4.122 基础上追加 `P4MindAndBalanceInsufficientUnreducedCostRejectedFixture` 和 `p4-play-mind-and-balance-insufficient-unreduced-cost-rejected.fixture.json`，把已有直接测试覆盖的《御衡守念》未满足减费条件且费用不足拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.124 更新：Basic action 行在 P4.123 基础上追加 `P4PiercingLightRepeatedTargetRejectedFixture` 和 `p4-play-piercing-light-repeated-target-rejected.fixture.json`，把已有直接测试覆盖的《透体圣光》重复选择同一目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.125 更新：Basic action 行在 P4.124 基础上追加 `P4BellowsBreathFourthTargetRejectedFixture` 和 `p4-play-bellows-breath-fourth-target-rejected.fixture.json`，把已有直接测试覆盖的《风箱炎息》选择第四个目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.126 更新：Basic action 行在 P4.125 基础上追加 `P4FirestormExplicitUnitTargetRejectedFixture` 和 `p4-play-firestorm-explicit-unit-target-rejected.fixture.json`，把已有直接测试覆盖的《烈火风暴》携带显式单位目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.127 更新：Basic action 行在 P4.126 基础上追加 `P4CrescentStrikeFriendlyTargetRejectedFixture` 和 `p4-play-crescent-strike-friendly-target-rejected.fixture.json`，把已有直接测试覆盖的《新月打击》选择友方目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.128 更新：Basic action 行在 P4.127 基础上追加 `P4SwitcherooDuplicateTargetRejectedFixture` 和 `p4-play-switcheroo-duplicate-target-rejected.fixture.json`，把已有直接测试覆盖的《换换乐》重复选择同一目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.129 更新：Basic action 行在 P4.128 基础上追加 `P4SwitcherooBaseTargetRejectedFixture` 和 `p4-play-switcheroo-base-target-rejected.fixture.json`，把已有直接测试覆盖的《换换乐》选择基地单位目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.130 更新：Basic action 行在 P4.129 基础上追加 `P4CrescentStrikeBaseTargetRejectedFixture` 和 `p4-play-crescent-strike-base-target-rejected.fixture.json`，把已有直接测试覆盖的《新月打击》选择敌方基地单位目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.131 更新：Basic action 行在 P4.130 基础上追加 `P4BellowsBreathRepeatedTargetRejectedFixture` 和 `p4-play-bellows-breath-repeated-target-rejected.fixture.json`，把已有直接测试覆盖的《风箱炎息》重复选择同一目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.132 更新：Interaction 行在 P4.131 基础上追加 `P4CenterStageEchoInsufficientManaRejectedFixture` 和 `p4-play-center-stage-echo-insufficient-mana-rejected.fixture.json`，把已有直接测试覆盖的《台前作秀》支付 `ECHO` 但总费用不足拒绝边界纳入 conformance fixture、回响关键词聚合和互动关键词聚合回放。

P4.133 更新：Interaction 行在 P4.132 基础上追加 `P4PunishmentIllegalEchoRejectedFixture` 和 `p4-play-punishment-illegal-echo-rejected.fixture.json`，把已有直接测试覆盖的非回响法术《惩戒》携带 `ECHO` optional cost 拒绝边界纳入 conformance fixture、回响关键词聚合和互动关键词聚合回放。

P4.134 更新：Basic action 行在 P4.133 基础上追加 `P4RocketBarrageMissingModeRejectedFixture` 和 `p4-play-rocket-barrage-missing-mode-rejected.fixture.json`，把已有直接测试覆盖的《火箭轰击》缺失模式拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.135 更新：Basic action 行在 P4.134 基础上追加 `P4RocketBarrageDestroyEquipmentUnitTargetRejectedFixture` 和 `p4-play-rocket-barrage-destroy-equipment-unit-target-rejected.fixture.json`，把已有直接测试覆盖的《火箭轰击》摧毁装备模式指定单位目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.136 更新：Basic action 行在 P4.135 基础上追加 `P4EmergencyRecallUnitTargetRejectedFixture` 和 `p4-play-emergency-recall-unit-target-rejected.fixture.json`，把已有直接测试覆盖的《紧急召回》指定单位目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.137 更新：Basic action 行在 P4.136 基础上追加 `P4PoroSnaxTargetRejectedFixture` 和 `p4-play-poro-snax-target-rejected.fixture.json`，把已有直接测试覆盖的《魄罗佳肴》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.138 更新：Basic action 行在 P4.137 基础上追加 `P4ShurelyasRequiemTargetRejectedFixture` 和 `p4-play-shurelyas-requiem-target-rejected.fixture.json`，把已有直接测试覆盖的《舒瑞娅的安魂曲》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.139 更新：Basic action 行在 P4.138 基础上追加 `P4FutureForgeTargetRejectedFixture` 和 `p4-play-future-forge-target-rejected.fixture.json`，把已有直接测试覆盖的《未来熔炉》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.140 更新：Basic action 行在 P4.139 基础上追加 `P4ScrapHeapTargetRejectedFixture` 和 `p4-play-scrap-heap-target-rejected.fixture.json`，把已有直接测试覆盖的《废料堆》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.141 更新：Basic action 行在 P4.140 基础上追加 `P4SpriteLanternTargetRejectedFixture` 和 `p4-play-sprite-lantern-target-rejected.fixture.json`，把已有直接测试覆盖的《精灵提灯》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.142 更新：Basic action 行在 P4.141 基础上追加 `P4SumpworksMapTargetRejectedFixture` 和 `p4-play-sumpworks-map-target-rejected.fixture.json`，把已有直接测试覆盖的《地沟区地图》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.143 更新：Basic action 行在 P4.142 基础上追加 `P4ScryingBlossomTargetRejectedFixture` 和 `p4-play-scrying-blossom-target-rejected.fixture.json`，把已有直接测试覆盖的《占卜花朵》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.144 更新：Basic action 行在 P4.143 基础上追加 `P4MagicBeansTargetRejectedFixture` 和 `p4-play-magic-beans-target-rejected.fixture.json`，把已有直接测试覆盖的《魔法鲜豆》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.145 更新：Basic action 行在 P4.144 基础上追加 `P4SteelBallistaTargetRejectedFixture` 和 `p4-play-steel-ballista-target-rejected.fixture.json`，把已有直接测试覆盖的《钢铁弩炮》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.146 更新：Basic action 行在 P4.145 基础上追加 `P4HeartOfIceTargetRejectedFixture` 和 `p4-play-heart-of-ice-target-rejected.fixture.json`，把已有直接测试覆盖的《玄冰之心》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.147 更新：Basic action 行在 P4.146 基础上追加 `P4RemorseOrbTargetRejectedFixture` 和 `p4-play-remorse-orb-target-rejected.fixture.json`，把已有直接测试覆盖的《懊悔法球》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.148 更新：Basic action 行在 P4.147 基础上追加 `P4SoulSwordTargetRejectedFixture` 和 `p4-play-soul-sword-target-rejected.fixture.json`，把已有直接测试覆盖的《灵魂之剑》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.149 更新：Basic action 行在 P4.148 基础上追加 `P4JaggedDirkTargetRejectedFixture` 和 `p4-play-jagged-dirk-target-rejected.fixture.json`，把已有直接测试覆盖的《锯齿短匕》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.150 更新：Basic action 行在 P4.149 基础上追加 `P4DoransShieldTargetRejectedFixture` 和 `p4-play-dorans-shield-target-rejected.fixture.json`，把已有直接测试覆盖的《多兰之盾》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.151 更新：Basic action 行在 P4.150 基础上追加 `P4HextechInfusedBulwarkTargetRejectedFixture` 和 `p4-play-hextech-infused-bulwark-target-rejected.fixture.json`，把已有直接测试覆盖的《海克斯注力刚壁》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.152 更新：Basic action 行在 P4.151 基础上追加 `P4DoransBladeTargetRejectedFixture` 和 `p4-play-dorans-blade-target-rejected.fixture.json`，把已有直接测试覆盖的《多兰之刃》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.153 更新：Basic action 行在 P4.152 基础上追加 `P4DoransRingTargetRejectedFixture` 和 `p4-play-dorans-ring-target-rejected.fixture.json`，把已有直接测试覆盖的《多兰之戒》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.154 更新：Basic action 行在 P4.153 基础上追加 `P4MarchingOrdersEnemyBaseTargetRejectedFixture` 和 `p4-play-marching-orders-enemy-base-target-rejected.fixture.json`，把已有直接测试覆盖的《行军号令》敌方第二目标位于基地时拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.155 更新：Basic action 行在 P4.154 基础上追加 `P4DuelTargetOrderRejectedFixture` 和 `p4-play-duel-target-order-rejected.fixture.json`，把已有直接测试覆盖的《决斗》目标顺序反转拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.156 更新：Basic action 行在 P4.155 基础上追加 `P4BattleCommandTargetOrderRejectedFixture` 和 `p4-play-battle-command-target-order-rejected.fixture.json`，把已有直接测试覆盖的《战斗号令》目标顺序反转拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.157 更新：Basic action 行在 P4.156 基础上追加 `P4VoidAssaultTargetOrderRejectedFixture` 和 `p4-play-void-assault-target-order-rejected.fixture.json`，把已有直接测试覆盖的《虚空来袭》目标顺序反转拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.158 更新：Basic action 行在 P4.157 基础上追加 `P4VanguardsEyeTargetRejectedFixture` 和 `p4-play-vanguards-eye-target-rejected.fixture.json`，把已有直接测试覆盖的《先锋之眼》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.159 更新：Basic action 行在 P4.158 基础上追加 `P4RecurveBowTargetRejectedFixture` 和 `p4-play-recurve-bow-target-rejected.fixture.json`，把已有直接测试覆盖的《反曲之弓》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.160 更新：Basic action 行在 P4.159 基础上追加 `P4LongSwordTargetRejectedFixture` 和 `p4-play-long-sword-target-rejected.fixture.json`，把已有直接测试覆盖的《长剑》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.161 更新：Basic action 行在 P4.160 基础上追加 `P4ClothArmorTargetRejectedFixture` 和 `p4-play-cloth-armor-target-rejected.fixture.json`，把已有直接测试覆盖的《布甲》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.162 更新：Basic action 行在 P4.161 基础上追加 `P4SteraksGageTargetRejectedFixture` 和 `p4-play-steraks-gage-target-rejected.fixture.json`，把已有直接测试覆盖的《斯特拉克的挑战护手》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.163 更新：Basic action 行在 P4.162 基础上追加 `P4SpinningAxeTargetRejectedFixture` 和 `p4-play-spinning-axe-target-rejected.fixture.json`，把已有直接测试覆盖的《旋转飞斧》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.164 更新：Basic action 行在 P4.163 基础上追加 `P4ShepherdsHeirloomTargetRejectedFixture` 和 `p4-play-shepherds-heirloom-target-rejected.fixture.json`，把已有直接测试覆盖的《牧人的传家宝》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放，并锁定拒绝时不会获得经验。

P4.165 更新：Basic action 行在 P4.164 基础上追加 `P4BrutalizerTargetRejectedFixture` 和 `p4-play-brutalizer-target-rejected.fixture.json`，把已有直接测试覆盖的《残暴之力》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.166 更新：Basic action 行在 P4.165 基础上追加 `P4GuardianAngelTargetRejectedFixture` 和 `p4-play-guardian-angel-target-rejected.fixture.json`，把已有直接测试覆盖的《守护天使》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.167 更新：Basic action 行在 P4.166 基础上追加 `P4HexdrinkerTargetRejectedFixture` 和 `p4-play-hexdrinker-target-rejected.fixture.json`，把已有直接测试覆盖的《海克斯饮魔刀》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.168 更新：Basic action 行在 P4.167 基础上追加 `P4WarmogsArmorTargetRejectedFixture` 和 `p4-play-warmogs-armor-target-rejected.fixture.json`，把已有直接测试覆盖的《狂徒铠甲》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.169 更新：Basic action 行在 P4.168 基础上追加 `P4TrinityForceTargetRejectedFixture` 和 `p4-play-trinity-force-target-rejected.fixture.json`，把已有直接测试覆盖的《三相之力》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.170 更新：Basic action 行在 P4.169 基础上追加 `P4BootsOfSwiftnessTargetRejectedFixture` 和 `p4-play-boots-of-swiftness-target-rejected.fixture.json`，把已有直接测试覆盖的《轻灵之靴》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.171 更新：Basic action 行在 P4.170 基础上追加 `P4CullTargetRejectedFixture` 和 `p4-play-cull-target-rejected.fixture.json`，把已有直接测试覆盖的《萃取》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.172 更新：Basic action 行在 P4.171 基础上追加 `P4SacredShearsTargetRejectedFixture` 和 `p4-play-sacred-shears-target-rejected.fixture.json`，把已有直接测试覆盖的《神圣剪刀》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.173 更新：Basic action 行在 P4.172 基础上追加 `P4BfSwordTargetRejectedFixture` 和 `p4-play-bf-sword-target-rejected.fixture.json`，把已有直接测试覆盖的《暴风大剑》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.174 更新：Basic action 行在 P4.173 基础上追加 `P4WanderersGuidebookTargetRejectedFixture` 和 `p4-play-wanderers-guidebook-target-rejected.fixture.json`，把已有直接测试覆盖的《云游图鉴》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.175 更新：Basic action 行在 P4.174 基础上追加 `P4ArionsFallTargetRejectedFixture` 和 `p4-play-arions-fall-target-rejected.fixture.json`，把已有直接测试覆盖的《阿瑞昂的陨落》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.176 更新：Basic action 行在 P4.175 基础上追加 `P4HuntersMacheteTargetRejectedFixture` 和 `p4-play-hunters-machete-target-rejected.fixture.json`，把已有直接测试覆盖的《猎人的宽刃刀》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.177 更新：Basic action 行在 P4.176 基础上追加 `P4WitheredBattleaxeTargetRejectedFixture` 和 `p4-play-withered-battleaxe-target-rejected.fixture.json`，把已有直接测试覆盖的《枯萎战斧》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.178 更新：Basic action 行在 P4.177 基础上追加 `P4BoneClubTargetRejectedFixture` 和 `p4-play-bone-club-target-rejected.fixture.json`，把已有直接测试覆盖的《碎骨棒》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.179 更新：Basic action 行在 P4.178 基础上追加 `P4AncientSteleTargetRejectedFixture` 和 `p4-play-ancient-stele-target-rejected.fixture.json`，把已有直接测试覆盖的《远古簇碑》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.180 更新：Basic action 行在 P4.179 基础上追加 `P4HextechAnomalyTargetRejectedFixture` 和 `p4-play-hextech-anomaly-target-rejected.fixture.json`，把已有直接测试覆盖的《海克斯异常体》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.181 更新：Basic action 行在 P4.180 基础上追加 `P4EnergyChannelTargetRejectedFixture` 和 `p4-play-energy-channel-target-rejected.fixture.json`，把已有直接测试覆盖的《能量通道》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.182 更新：Basic action 行在 P4.181 基础上追加 `P4TimeGateTargetRejectedFixture` 和 `p4-play-time-gate-target-rejected.fixture.json`，把已有直接测试覆盖的《预时之门》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.183 更新：Basic action 行在 P4.182 基础上追加 `P4RavenTomeTargetRejectedFixture` 和 `p4-play-raven-tome-target-rejected.fixture.json`，把已有直接测试覆盖的《邪鸦魔典》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.184 更新：Basic action 行在 P4.183 基础上追加 `P4SunDiscTargetRejectedFixture` 和 `p4-play-sun-disc-target-rejected.fixture.json`，把已有直接测试覆盖的《太阳圆盘》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.185 更新：Basic action 行在 P4.184 基础上追加 `P4ForesightMaskTargetRejectedFixture` 和 `p4-play-foresight-mask-target-rejected.fixture.json`，把已有直接测试覆盖的《远见面具》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.186 更新：Basic action 行在 P4.185 基础上追加 `P4SolariAltarTargetRejectedFixture` 和 `p4-play-solari-altar-target-rejected.fixture.json`，把已有直接测试覆盖的《烈阳圣坛》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.187 更新：Basic action 行在 P4.186 基础上追加 `P4ChemtechBarrelTargetRejectedFixture` 和 `p4-play-chemtech-barrel-target-rejected.fixture.json`，把已有直接测试覆盖的《炼金科技桶》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.188 更新：Basic action 行在 P4.187 基础上追加 `P4SoulWheelTargetRejectedFixture` 和 `p4-play-soul-wheel-target-rejected.fixture.json`，把已有直接测试覆盖的《灵魂之轮》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.189 更新：Basic action 行在 P4.188 基础上追加 `P4MushroomBagTargetRejectedFixture` 和 `p4-play-mushroom-bag-target-rejected.fixture.json`，把已有直接测试覆盖的《蘑菇袋》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.190 更新：Basic action 行在 P4.189 基础上追加 `P4ArenaBarTargetRejectedFixture` 和 `p4-play-arena-bar-target-rejected.fixture.json`，把已有直接测试覆盖的《竞技场酒吧》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.191 更新：Basic action 行在 P4.190 基础上追加 `P4PirateHideoutTargetRejectedFixture` 和 `p4-play-pirate-hideout-target-rejected.fixture.json`，把已有直接测试覆盖的《海盗避风港》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.192 更新：Basic action 行在 P4.191 基础上追加 `P4ForgottenSignpostTargetRejectedFixture` 和 `p4-play-forgotten-signpost-target-rejected.fixture.json`，把已有直接测试覆盖的《被遗忘的路标》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.193 更新：Basic action 行在 P4.192 基础上追加 `P4FrozenGemTargetRejectedFixture` 和 `p4-play-frozen-gem-target-rejected.fixture.json`，把已有直接测试覆盖的《冰封宝石》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.194 更新：Basic action 行在 P4.193 基础上追加 `P4CrumblingPalaceTargetRejectedFixture` 和 `p4-play-crumbling-palace-target-rejected.fixture.json`，把已有直接测试覆盖的《倾颓宫殿》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.195 更新：Basic action 行在 P4.194 基础上追加 `P4ScarletRoseTargetRejectedFixture` 和 `p4-play-scarlet-rose-target-rejected.fixture.json`，把已有直接测试覆盖的《猩红玫瑰》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.196 更新：Basic action 行在 P4.195 基础上追加 `P4ReversalShardTargetRejectedFixture` 和 `p4-play-reversal-shard-target-rejected.fixture.json`，把已有直接测试覆盖的《逆转碎片》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.197 更新：Basic action 行在 P4.196 基础上追加 `P4AssemblyRackTargetRejectedFixture` 和 `p4-play-assembly-rack-target-rejected.fixture.json`，把已有直接测试覆盖的《装配架》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.198 更新：Basic action 行在 P4.197 基础上追加 `P4SfurSongTargetRejectedFixture` 和 `p4-play-sfur-song-target-rejected.fixture.json`，把已有直接测试覆盖的《斯弗尔尚歌》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.199 更新：Basic action 行在 P4.198 基础上追加 `P4ZDriveTargetRejectedFixture` 和 `p4-play-z-drive-target-rejected.fixture.json`，把已有直接测试覆盖的《Z型驱动》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.200 更新：Basic action 行在 P4.199 基础上追加 `P4VanguardArmoryTargetRejectedFixture` 和 `p4-play-vanguard-armory-target-rejected.fixture.json`，把已有直接测试覆盖的《先锋军备》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.201 更新：Basic action 行在 P4.200 基础上追加 `P4RemembranceAltarTargetRejectedFixture` 和 `p4-play-remembrance-altar-target-rejected.fixture.json`，把已有直接测试覆盖的《追忆祭坛》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.202 更新：Basic action 行在 P4.201 基础上追加 `P4RageSigilTargetRejectedFixture` 和 `p4-play-rage-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的《暴怒之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.203 更新：Basic action 行在 P4.202 基础上追加 `P4FocusSigilTargetRejectedFixture` 和 `p4-play-focus-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的《专注之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.204 更新：Basic action 行在 P4.203 基础上追加 `P4InsightSigilTargetRejectedFixture` 和 `p4-play-insight-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的《洞察之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.205 更新：Basic action 行在 P4.204 基础上追加 `P4PowerSigilTargetRejectedFixture` 和 `p4-play-power-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的《力量之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.206 更新：Basic action 行在 P4.205 基础上追加 `P4DiscordSigilTargetRejectedFixture` 和 `p4-play-discord-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的《不和之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.207 更新：Basic action 行在 P4.206 基础上追加 `P4UnitySigilTargetRejectedFixture` 和 `p4-play-unity-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的《团结之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.208 更新：Basic action 行在 P4.207 基础上追加 `P4OgnRageSigilTargetRejectedFixture` 和 `p4-play-ogn-rage-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN 版《暴怒之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.209 更新：Basic action 行在 P4.208 基础上追加 `P4OgnFocusSigilTargetRejectedFixture` 和 `p4-play-ogn-focus-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN 版《专注之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.210 更新：Basic action 行在 P4.209 基础上追加 `P4OgnInsightSigilTargetRejectedFixture` 和 `p4-play-ogn-insight-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN 版《洞察之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.211 更新：Basic action 行在 P4.210 基础上追加 `P4OgnPowerSigilTargetRejectedFixture` 和 `p4-play-ogn-power-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN 版《力量之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.212 更新：Basic action 行在 P4.211 基础上追加 `P4OgnDiscordSigilTargetRejectedFixture` 和 `p4-play-ogn-discord-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN 版《不和之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.213 更新：Basic action 行在 P4.212 基础上追加 `P4OgnUnitySigilTargetRejectedFixture` 和 `p4-play-ogn-unity-sigil-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN 版《团结之印》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.214 更新：Basic action 行在 P4.213 基础上追加 `P4WondrousPackTargetRejectedFixture` 和 `p4-play-wondrous-pack-target-rejected.fixture.json`，把已有直接测试覆盖的《奇妙行囊》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.215 更新：Basic action 行在 P4.214 基础上追加 `P4SirenTargetRejectedFixture` 和 `p4-play-siren-target-rejected.fixture.json`，把已有直接测试覆盖的《塞壬号》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.216 更新：Basic action 行在 P4.215 基础上追加 `P4OwnerlessTreasureTargetRejectedFixture` 和 `p4-play-ownerless-treasure-target-rejected.fixture.json`，把已有直接测试覆盖的《无主宝藏》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.217 更新：Basic action 行在 P4.216 基础上追加 `P4ScavengingWhizTargetRejectedFixture` 和 `p4-play-scavenging-whiz-target-rejected.fixture.json`，把已有直接测试覆盖的《拾荒小能手》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.218 更新：Basic action 行在 P4.217 基础上追加 `P4MistfallBladeyardTargetRejectedFixture` 和 `p4-play-mistfall-bladeyard-target-rejected.fixture.json`，把已有直接测试覆盖的《雾临剑冢》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.219 更新：Basic action 行在 P4.218 基础上追加 `P4ShimmeringAuroraTargetRejectedFixture` 和 `p4-play-shimmering-aurora-target-rejected.fixture.json`，把已有直接测试覆盖的《闪耀极光》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.220 更新：Basic action 行在 P4.219 基础上追加 `P4SolariEmblemTargetRejectedFixture` 和 `p4-play-solari-emblem-target-rejected.fixture.json`，把已有直接测试覆盖的《烈阳徽记》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.221 更新：Basic action 行在 P4.220 基础上追加 `P4VanguardHelmTargetRejectedFixture` 和 `p4-play-vanguard-helm-target-rejected.fixture.json`，把已有直接测试覆盖的《先锋之盔》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.222 更新：Basic action 行在 P4.221 基础上追加 `P4HoneyfruitTargetRejectedFixture` 和 `p4-play-honeyfruit-target-rejected.fixture.json`，把已有直接测试覆盖的《蜜糖果实》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.223 更新：Basic action 行在 P4.222 基础上追加 `P4LastRitesTargetRejectedFixture` 和 `p4-play-last-rites-target-rejected.fixture.json`，把已有直接测试覆盖的《临终仪式》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.224 更新：Basic action 行在 P4.223 基础上追加 `P4BladeOfRuinedKingTargetRejectedFixture` 和 `p4-play-blade-of-ruined-king-target-rejected.fixture.json`，把已有直接测试覆盖的《破败王者之刃》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.225 更新：Basic action 行在 P4.224 基础上追加 `P4MysteriousWeaponTargetRejectedFixture` 和 `p4-play-mysterious-weapon-target-rejected.fixture.json`，把已有直接测试覆盖的《来路不明的武器》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.226 更新：Basic action 行在 P4.225 基础上追加 `P4SeaMonsterHookTargetRejectedFixture` 和 `p4-play-sea-monster-hook-target-rejected.fixture.json`，把已有直接测试覆盖的《海兽钓钩》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.227 更新：Basic action 行在 P4.226 基础上追加 `P4PetriciteMonumentTargetRejectedFixture` 和 `p4-play-petricite-monument-target-rejected.fixture.json`，把已有直接测试覆盖的《禁魔石丰碑》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.228 更新：Basic action 行在 P4.227 基础上追加 `P4ZhonyasHourglassTargetRejectedFixture` 和 `p4-play-zhonyas-hourglass-target-rejected.fixture.json`，把已有直接测试覆盖的《中娅沙漏》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.229 更新：Basic action 行在 P4.228 基础上追加 `P4EdgeOfNightTargetRejectedFixture` 和 `p4-play-edge-of-night-target-rejected.fixture.json`，把已有直接测试覆盖的《夜之锋刃》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.230 更新：Basic action 行在 P4.229 基础上追加 `P4HearthfireCloakTargetRejectedFixture` 和 `p4-play-hearthfire-cloak-target-rejected.fixture.json`，把已有直接测试覆盖的《炉火斗篷》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.231 更新：Basic action 行在 P4.230 基础上追加 `P4RabadonsDeathcapTargetRejectedFixture` 和 `p4-play-rabadons-deathcap-target-rejected.fixture.json`，把已有直接测试覆盖的《灭世者的死亡之冠》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.232 更新：Basic action 行在 P4.231 基础上追加 `P4BlastConeTargetRejectedFixture` 和 `p4-play-blast-cone-target-rejected.fixture.json`，把已有直接测试覆盖的《喷射球果》当前 no-move 路径带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.233 更新：Basic action 行在 P4.232 基础上追加 `P4DeathListTargetRejectedFixture` 和 `p4-play-death-list-target-rejected.fixture.json`，把已有直接测试覆盖的《夺命名单》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.234 更新：Basic action 行在 P4.233 基础上追加 `P4CursedSarcophagusTargetRejectedFixture` 和 `p4-play-cursed-sarcophagus-target-rejected.fixture.json`，把已有直接测试覆盖的《受诅咒的石棺》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.235 更新：Basic action 行在 P4.234 基础上追加 `P4BoneclubPromoTargetRejectedFixture` 和 `p4-play-boneclub-promo-target-rejected.fixture.json`，把已有直接测试覆盖的 promo 编号《碎骨棒》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.236 更新：Basic action 行在 P4.235 基础上追加 `P4HextechGauntletTargetRejectedFixture` 和 `p4-play-hextech-gauntlet-target-rejected.fixture.json`，把已有直接测试覆盖的《海克斯科技护手》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.237 更新：Basic action 行在 P4.236 基础上追加 `P4TreasureGolemTargetRejectedFixture` 和 `p4-play-treasure-golem-target-rejected.fixture.json`，把已有直接测试覆盖的《宝藏魔像》带目标打出拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.238 更新：Basic action 行在 P4.237 基础上追加 `P4XerathTargetRejectedFixture` 和 `p4-play-xerath-target-rejected.fixture.json`，把已有直接测试覆盖的《泽拉斯》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.239 更新：Basic action 行在 P4.238 基础上追加 `P4DragonSoulSageTargetRejectedFixture` 和 `p4-play-dragon-soul-sage-target-rejected.fixture.json`，把已有直接测试覆盖的《龙魂贤者》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.240 更新：Basic action 行在 P4.239 基础上追加 `P4FluftPoroTargetRejectedFixture` 和 `p4-play-fluft-poro-target-rejected.fixture.json`，把已有直接测试覆盖的《绵绵魄罗》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.241 更新：Basic action 行在 P4.240 基础上追加 `P4Sfd088RenataGlascTargetRejectedFixture` 和 `p4-play-sfd-088-renata-glasc-target-rejected.fixture.json`，把已有直接测试覆盖的 SFD·088《烈娜塔·戈拉斯克》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.242 更新：Basic action 行在 P4.241 基础上追加 `P4Sfd088aRenataGlascTargetRejectedFixture` 和 `p4-play-sfd-088a-renata-glasc-target-rejected.fixture.json`，把已有直接测试覆盖的 SFD·088a《烈娜塔·戈拉斯克》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.243 更新：Basic action 行在 P4.242 基础上追加 `P4Ogn028DravenTargetRejectedFixture` 和 `p4-play-ogn-028-draven-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·028《德莱文》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.244 更新：Basic action 行在 P4.243 基础上追加 `P4Sfd110FioraTargetRejectedFixture` 和 `p4-play-sfd-110-fiora-target-rejected.fixture.json`，把已有直接测试覆盖的 SFD·110《菲奥娜》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.245 更新：Basic action 行在 P4.244 基础上追加 `P4Sfd110aFioraTargetRejectedFixture` 和 `p4-play-sfd-110a-fiora-target-rejected.fixture.json`，把已有直接测试覆盖的 SFD·110a《菲奥娜》异画 A 普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.246 更新：Basic action 行在 P4.245 基础上追加 `P4Sfd141IreliaTargetRejectedFixture` 和 `p4-play-sfd-141-irelia-target-rejected.fixture.json`，把已有直接测试覆盖的 SFD·141《艾瑞莉娅》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.247 更新：Basic action 行在 P4.246 基础上追加 `P4Sfd141aIreliaTargetRejectedFixture` 和 `p4-play-sfd-141a-irelia-target-rejected.fixture.json`，把已有直接测试覆盖的 SFD·141a《艾瑞莉娅》异画 A 普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.248 更新：Basic action 行在 P4.247 基础上追加 `P4DragonCallerTargetRejectedFixture` 和 `p4-play-dragon-caller-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·140《唤龙使者》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.249 更新：Basic action 行在 P4.248 基础上追加 `P4WaterbenderTargetRejectedFixture` 和 `p4-play-waterbender-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·055《驭水者》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.250 更新：Basic action 行在 P4.249 基础上追加 `P4WiseElderTargetRejectedFixture` 和 `p4-play-wise-elder-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·065《睿智长者》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.251 更新：Basic action 行在 P4.250 基础上追加 `P4EagerApprenticeTargetRejectedFixture` 和 `p4-play-eager-apprentice-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·084《踊跃的学徒》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.252 更新：Basic action 行在 P4.251 基础上追加 `P4ArenaServiceCrewTargetRejectedFixture` 和 `p4-play-arena-service-crew-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·091《竞技场勤务小队》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.253 更新：Basic action 行在 P4.252 基础上追加 `P4PoroHerderTargetRejectedFixture` 和 `p4-play-poro-herder-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·061《魄罗牧者》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.254 更新：Basic action 行在 P4.253 基础上追加 `P4RavenbloomStudentTargetRejectedFixture` 和 `p4-play-ravenbloom-student-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·103《拉文布鲁姆学生》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.255 更新：Basic action 行在 P4.254 基础上追加 `P4ResonantSoulTargetRejectedFixture` 和 `p4-play-resonant-soul-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·118《残响之魂》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.256 更新：Basic action 行在 P4.255 基础上追加 `P4BilgewaterBullyTargetRejectedFixture` 和 `p4-play-bilgewater-bully-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·125《比尔吉沃特恶霸》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.257 更新：Basic action 行在 P4.256 基础上追加 `P4SharpshooterPirateTargetRejectedFixture` 和 `p4-play-sharpshooter-pirate-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·130《神射海盗》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.258 更新：Basic action 行在 P4.257 基础上追加 `P4DuneDrakeTargetRejectedFixture` 和 `p4-play-dune-drake-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·131《沙丘亚龙》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.259 更新：Basic action 行在 P4.258 基础上追加 `P4EmberMonkTargetRejectedFixture` 和 `p4-play-ember-monk-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·167《余火修士》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.260 更新：Basic action 行在 P4.259 基础上追加 `P4HiddenTrackerTargetRejectedFixture` 和 `p4-play-hidden-tracker-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·177《隐秘追踪者》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.261 更新：Basic action 行在 P4.260 基础上追加 `P4UndercoverAgentTargetRejectedFixture` 和 `p4-play-undercover-agent-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·178《卧底特工》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.262 更新：Basic action 行在 P4.261 基础上追加 `P4TravelingMerchantTargetRejectedFixture` 和 `p4-play-traveling-merchant-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·185《旅行商人》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.263 更新：Basic action 行在 P4.262 基础上追加 `P4OgnKogmawTargetRejectedFixture` 和 `p4-play-ogn-kogmaw-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·190《克格莫》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.264 更新：Basic action 行在 P4.263 基础上追加 `P4NoxianDrummerTargetRejectedFixture` 和 `p4-play-noxian-drummer-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·222《诺克萨斯鼓手》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.265 更新：Basic action 行在 P4.264 基础上追加 `P4TideCallerTargetRejectedFixture` 和 `p4-play-tide-caller-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·199《控潮者》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.266 更新：Basic action 行在 P4.265 基础上追加 `P4Ogn202JinxTargetRejectedFixture` 和 `p4-play-ogn-202-jinx-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·202《金克丝》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.267 更新：Basic action 行在 P4.266 基础上追加 `P4GhostMatronTargetRejectedFixture` 和 `p4-play-ghost-matron-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·226《幽灵主母》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.268 更新：Basic action 行在 P4.267 基础上追加 `P4AlbusFerrosTargetRejectedFixture` 和 `p4-play-albus-ferros-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·230《阿不思·菲罗斯》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.269 更新：Basic action 行在 P4.268 基础上追加 `P4OgnKarthusTargetRejectedFixture` 和 `p4-play-ogn-karthus-target-rejected.fixture.json`，把已有直接测试覆盖的 OGN·236《卡尔萨斯》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.270 更新：Basic action 行在 P4.269 基础上追加 `P4DunehornBeastTargetRejectedFixture` 和 `p4-play-dunehorn-beast-target-rejected.fixture.json`，把已有直接测试覆盖的 SFD·027《穿沙角兽》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.271 更新：Basic action 行在 P4.270 基础上追加 `P4GloompathGuardTargetRejectedFixture` 和 `p4-play-gloompath-guard-target-rejected.fixture.json`，把已有直接测试覆盖的 SFD·035《幽径守卫》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.272 更新：Basic action 行在 P4.271 基础上追加 `P4ApprenticeBlacksmithTargetRejectedFixture` 和 `p4-play-apprentice-blacksmith-target-rejected.fixture.json`，把已有直接测试覆盖的 SFD·041《学徒铁匠》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

P4.273 更新：Basic action 行在 P4.272 基础上追加 `P4MountainApeElderTargetRejectedFixture` 和 `p4-play-mountain-ape-elder-target-rejected.fixture.json`，把已有直接测试覆盖的 SFD·047《山猿老祖》普通手牌打出带目标拒绝边界纳入 conformance fixture 和基础动作聚合回放。

## P4.2 Permission Keyword Batch

本阶段完成权限关键词的最小规则化模型，保持小批次接入：

- 新增 `CardPermissionKeywordRules`，把出牌时机判定抽成可单测的 `CardPlayTimingDecision`，并提供 `CardPermissionKeywordProfile` 显式识别 `迅捷` / `反应` / `急速`。
- `反应`：沿用 P2 已验证 `CardBehaviorDefinition.CanPlayDuringPriority` 优先权窗口路径，新增 profile 断言并用 `p2-preflight-play-wind-wall-counter-spell.fixture.json` 保持 conformance 绿色；符文/装备激活反应技能仍不在 P4.2 范围。
- `迅捷`：新增 `CanPlayDuringSpellDuel` registry 开关，只给已验证代表 `OGN·004/298 顺劈` 打开；焦点玩家在 `SPELL_DUEL_OPEN` 且无 stack item 时可打出，之后进入现有 P2 结算链路径。新增 fixture `p4-play-swift-cleave-in-spell-duel-focus.fixture.json`。
- `急速`：从 source unit tags 识别 `急速`，并保留现有不支付额外费用的单位入场路径；P4.13 已将《灼焰飞龙》`HASTE_READY` 代表路径接入现有 `mana + power` 费用模型，P4.18 已将《小鲨鱼》接入同一代表路径，P4.20 已将《军团后卫》接入同一代表路径，P4.56 已将《金克丝》接入同一代表路径，P4.57 已将《金克丝》A 版本接入同一代表路径，P4.25 已将《树根先生》接入同一代表路径，P4.26 已将《机械迷》接入同一代表路径，P4.27 已将《琢珥鱼》接入同一代表路径，P4.30 已将《卡银娜·薇蕊泽》接入同一代表路径，P4.31 已将《绯红印记树怪》接入同一代表路径，P4.44 已将《绯红印记树怪》A 版本接入同一代表路径，P4.45 已将《尼菈》接入同一代表路径，P4.46 已将《雷恩加尔》接入同一代表路径，P4.47 已将《雷恩加尔》A 版本接入同一代表路径，P4.48 已将《厄运小姐》接入同一代表路径，P4.49 已将《厄运小姐》A 版本接入同一代表路径，P4.50 已将《希维尔》接入同一代表路径，P4.51 已将《希维尔》A 版本接入同一代表路径，P4.52 已将《莉莉娅》接入同一代表路径，P4.53 已将《莉莉娅》A 版本接入同一代表路径，P4.54 已将《阿兹尔》接入同一代表路径，P4.55 已将《阿兹尔》A 版本接入同一代表路径，P4.32 已将《美味仙灵》接入同一代表路径，P4.33 已将《艾克》接入同一代表路径，P4.34 已将《武装强袭者》接入同一代表路径，P4.35 已将《远古战狂》接入同一代表路径，P4.36 已将《海妖猎手》接入同一代表路径，P4.37 已将《李青》接入同一代表路径，P4.38 已将《李青》A 版本接入同一代表路径，P4.39 已将《千尾监视者》接入同一代表路径，P4.40 已将《卡莎》接入同一代表路径，P4.43 已将《卡莎》A 版本接入同一代表路径，P4.41 已将《雷克塞》接入同一代表路径，P4.42 已将《雷克塞》A 版本接入同一代表路径。
- 本批次没有批量启用全部 `迅捷` 牌，没有改动战斗关键词、待命/伏击、装备激活技能或急速额外费用结算。

## P4.3 Lifecycle Keyword Batch

本阶段完成生命周期关键词的最小执行路径：

- `瞬息`：在 `ResolveTurnStart` 入口中新增开始阶段前置清理，只销毁当前 turn player 控制的 base/battlefield `瞬息` 对象，事件排在 `TURN_START_BEGAN` 后、`RUNES_CALLED` 前。
- 新增 fixture `p4-ephemeral-destroys-controlled-objects-turn-start.fixture.json`，验证当前玩家控制的 `瞬息` 基地/战场对象进入废牌堆，对手控制的 `瞬息` 对象不在本次开始阶段被清理。
- `DestroyedUnitOwnerIdsThisTurn` 会记录本次因 `瞬息` 摧毁的单位拥有者，供现有“本回合单位被摧毁”条件继续复用。
- 本批次没有实现 `绝念` 离场触发、`预知` 新路径、装备贴附下的瞬息卸除顺序、得分触发或战斗清理。

## P4.4 Interaction Keyword Batch

本阶段完成互动关键词的最小小批次，选择风险最低的 `回响` mana-only optional cost/repeat 路径：

- 新增 `CardInteractionKeywordRules`、`CardEchoKeywordProfile`、`EchoOptionalCostNames.Echo`，把“P2 registry 暴露 `EchoManaCost > 0` 且命令选择 `optionalCosts = ["ECHO"]`”建模为显式互动关键词 profile。
- `CoreRuleEngine.TryBuildOptionalCostPlan` 不再内联判断 `"ECHO"`，改为调用 `CardInteractionKeywordRules.TryBuildEchoOptionalCost`；当前行为保持不变：额外支付 `EchoManaCost`，`effectRepeatCount = 2`。
- `UNL-061/219 台前作秀` 用官网卡面 `{{回响}}`、P3 `BehaviorSpec.TemplateIds = echo`、P3 `Cost.OptionalCosts = echo` 和 P2 registry `EchoManaCost = 2` 串起只读规格到执行路径。
- `UNL-007/219 惩戒` 继续作为非回响法术拒绝 `ECHO` optional cost 的负例；P4.132 已把《台前作秀》支付 `ECHO` 但 mana 不足的费用校验拒绝边界提升为 fixture，P4.133 已把《惩戒》非法回响 optional cost 拒绝边界提升为 fixture。
- 复用并重新锁定三条已审计 conformance fixture：`p2-preflight-play-center-stage-echo-draw-stack`、`p2-preflight-play-the-curtain-rises-echo-ready-unit`、`p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base`；P4.132/P4.133 额外锁定 `p4-play-center-stage-echo-insufficient-mana-rejected` 和 `p4-play-punishment-illegal-echo-rejected`。
- 本批次没有实现有色回响、弃牌/横置/其他非 mana 回响、装备或单位授予回响、可选择模式的“重复指示”细节，也没有进入待命/伏击 face-down 和隐藏信息系统。

## P4.5 Basic Action Template Primitive Batch

本阶段补齐基础动作 template executor 的小批次测试与边界说明：

- 新增 `BehaviorTemplatePrimitiveExecutor`，在 P3 `BehaviorSpec` + P4.1 `BehaviorTemplateDelegationBridge` 之后，生成只读 primitive plan；该 executor 不修改 `MatchState`，不替换 `CoreRuleEngine`。
- 已支持 primitive plan 的低风险模板：`draw`、`damage`、`destroy`、`stun`、`temp_might`。
- 每个 primitive 都从既有 P2 `CardBehaviorDefinition` 读取参数：抽牌张数、固定伤害、目标范围、摧毁目标、`STUNNED` 状态、临时战力修正和条件。
- `move` 与 `recall` 仍明确返回 `delegated-to-p2`：P2 已有 `UNIT_MOVED_TO_BASE`、`UNIT_RETURNED_TO_HAND` 等真实状态写入，但 P4.5 暂不抽象多战场目的地、隐藏信息、owner/controller 边界。
- `gain_experience`、`assemble`、`echo`、`ambush` 以及动态伤害/替代/触发分支仍不生成 primitive plan，继续按各自关键词或后续系统拆分。
- 对 `眩晕` 卡面 reminder text 造成的 P3 `damage` 粗解析噪声增加保护：`STUNNED` 牌上的零伤害 `damage` 模板不会阻断 `stun` primitive plan。
- 新增 `P4PrimitiveExecutorBuildsBasicActionPlansAndLeavesComplexRoutesDelegated` 覆盖 primitive 与 delegated-only 边界；新增 `P4PrimitiveExecutorRepresentativeFixturesStayGreen` 复用 5 条已审计 fixture 锁定 P2 状态写入继续绿色。

## P4.6 Completion Audit And Combat Keyword Profile

本阶段先做完成审计，结论是 P4 不能标记完成：战斗、资源和装备关键词仍有明显 deferred 项。因此本批次继续做最低风险的战斗关键词 profile，不进入完整战斗模型：

- 新增 `CardCombatKeywordRules` 与 `CardCombatKeywordProfile`，从 P2 source object tags 显式识别 `强攻`、`坚守`、`壁垒`、`后排`、`游走`。
- `强攻` / `坚守` 支持无数字默认 `1`，以及 `强攻2`、`坚守5` 等数值后缀。
- profile status 为 `recognized-deferred`：只表示官方文本、P3 keyword parser 和 P2 registry tag 已对齐；不表示完整战斗伤害、承伤顺序或游走移动权限已完成。
- 代表卡验证：`OGN·210/298 莽莽魄罗`、`OGN·052/298 强强魄罗`、`OGS·007/024 盖伦`、`UNL-036/219 变异猫咪`、`UNL-090/219 乐芙兰`、`SFD·096/221 劳伦特护刃者`。
- 新增 `P4CombatKeywordProfilesMapOfficialTextToRegistryTags` 锁定官方卡面文本到 registry profile；新增 `P4CombatKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 复用 6 条 keyword-unit fixture 保持 P2 入场标签路径绿色。
- 本批次没有实现进攻/防守战力修正结算、壁垒/后排承伤顺序、游走 MOVE_UNIT 合法性、移动触发得分、战场位置精确选择或战斗伤害分配。

## P4.7 Resource Keyword Profile

本阶段继续资源关键词的低风险 profile 小批次：

- 新增 `CardResourceKeywordRules` 与 `CardResourceKeywordProfile`，从 P3 `BehaviorSpec` 官方文本和 P2 source object tags 识别 `狩猎`、`等级`、`鼓舞`、`法盾`。
- `狩猎` / `法盾` 支持无数字默认 `1`，以及 `狩猎3`、`法盾2` 等数值后缀；`等级` 解析 `等级3>`、`等级6>` 等阈值列表。
- profile status 为 `recognized-deferred`：只表示 P3 parser、官方文本和 P2 registry/tag 已对齐；P4.12 已把法术选择敌方场上对象的法盾目标税接入费用计划，P4.59 已补多目标法术对 `法盾` 与 `法盾2` 的税值聚合，P4.61 已补友方法盾目标 no-tax 边界，P4.62 已补 `ACTIVATE_ABILITY` command 前置模型，P4.73 已补《蔚》无目标付费技能入栈/结算代表路径，P4.74 已补场上对象 `cardNo` 身份校验，P4.77 已补《泽拉斯》带目标技能对敌方法盾单位的目标税、横置和伤害结算代表路径，P4.93 已补同一技能敌方法盾目标税 mana 不足时拒绝且不改状态的边界，P4.78 已补同一技能选择己方法盾单位时不缴目标税的边界，P4.79 已补同一技能来源已横置时拒绝且不改状态的边界，P4.80 已补同一技能缺少目标时拒绝且不改状态的边界，P4.81 已补同一技能提供两个目标时拒绝且不改状态的边界，P4.82 已补同一技能携带未支持 optional cost 时拒绝且不改状态的边界，P4.83 已补非《泽拉斯》来源伪造同一 ability id 时拒绝且不改状态的边界，P4.84 已补同一技能选择非单位目标时拒绝且不改状态，P4.85 已补同一技能来源不在战场时拒绝且不改状态，P4.86 已补同一技能来源由对手控制时拒绝且不改状态，并把 P4.79-P4.86/P4.93 拒绝 fixtures 纳入资源关键词聚合回放，P4.14 已把《诺克萨斯新兵》本回合已打出其他卡牌的鼓舞费用减免接入费用计划，P4.15 已把《踏苔蜥》`等级3` 入场 +1 战力与法盾接入单位入场计划，P4.16 已把《风行狐》`等级3` 入场 +1 战力与游走接入单位入场计划，P4.17 已把《无极学徒》`等级6` 打出抽 1 接入单位结算计划，P4.21 已把《崔法利求战者》同回合鼓舞自增益接入单位结算计划，P4.22 已把《危险二人组》同回合鼓舞目标临时战力接入结算计划，P4.23 已把《垃圾场小霸王》同回合鼓舞弃 2 抽 2 接入结算计划，P4.24 已把《先锋队长》同回合鼓舞创建两名 1 战力随从接入结算计划，P4.28/P4.29 已把《易》及 A 版本 `等级6` 法盾/游走接入单位入场计划，但经验获得/消耗、其他等级条件、其他鼓舞效果、更多技能目标税执行和授予/静态法盾仍未完整执行。
- 代表卡验证：`UNL-100/219 贪食魔沼蛙`、`UNL-047/219 踏苔蜥`、`UNL-075/219 风行狐`、`UNL-040/219 无极学徒`、`UNL-113/219 易`、`UNL-113a/219 易`、`OGN·012/298 诺克萨斯新兵`、`OGN·016/298 危险二人组`、`OGN·020/298 垃圾场小霸王`、`OGN·217/298 崔法利求战者`、`OGN·218/298 先锋队长`、`OGN·013/298 呸呸魄罗`、`SFD·085/221 奥恩`。
- 新增 `P4ResourceKeywordProfilesMapOfficialTextToRegistryTags` 锁定官方卡面文本到 profile；`P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 复用已审计 fixture 保持 P2 入场/标签/no-optional 路径绿色，并在 P4.14/P4.15/P4.16/P4.17/P4.21/P4.22/P4.23/P4.24/P4.28/P4.29/P4.59/P4.61/P4.62/P4.73/P4.74/P4.77/P4.78/P4.79/P4.80/P4.81/P4.82/P4.83/P4.84 纳入鼓舞费用减免、自增益、目标临时战力、弃牌抽牌、随从创建、等级入场/抽牌/标签、多目标法盾税、友方法术目标 no-tax、技能 command envelope、《蔚》无目标付费技能执行、场上对象 `cardNo` 身份校验、《泽拉斯》带目标技能敌方法盾税、己方法盾 no-tax、已横置来源拒绝、缺目标拒绝、多目标拒绝、额外费用拒绝、来源身份拒绝和非单位目标拒绝证据。
- P4.7 当时没有实现法盾目标税支付、每次被选为目标的 FAQ 细节、狩猎征服/据守经验获得、经验消耗、等级阈值动态生效、鼓舞本回合记忆或相关触发；P4.12 随后只补法术目标税的单目标代表执行切片，P4.59 随后只补《妖异狐火》多目标法盾税聚合切片，P4.61 随后只补《秘奥义！慈悲度魂落》友方法盾目标 no-tax 边界，P4.62 随后只补技能激活 command 前置模型，P4.73 随后只补《蔚》无目标付费技能代表执行切片，P4.74 随后只补场上对象 `cardNo` 身份校验，P4.77 随后只补《泽拉斯》单目标技能税、横置和 3 点伤害代表执行切片，P4.93 随后只补同一技能敌方法盾目标税 mana 不足拒绝 fixture，P4.78 随后只补同一《泽拉斯》技能选择己方法盾单位时不缴目标税的边界，P4.79 随后只补同一《泽拉斯》技能来源已横置时拒绝且不改状态的边界，P4.80 随后只补同一《泽拉斯》技能缺少目标时拒绝且不改状态的边界，P4.81 随后只补同一《泽拉斯》技能提供两个目标时拒绝且不改状态的边界，P4.82 随后只补同一《泽拉斯》技能携带未支持 optional cost 时拒绝且不改状态的边界，P4.83 随后只补非《泽拉斯》来源伪造同一 ability id 时拒绝且不改状态的边界，P4.84 随后只补同一《泽拉斯》技能选择非单位目标时拒绝且不改状态，P4.85 随后只补同一《泽拉斯》技能来源不在战场时拒绝且不改状态，P4.86 随后只补同一《泽拉斯》技能来源由对手控制时拒绝且不改状态，并把 P4.79-P4.86 拒绝 fixtures 纳入资源关键词聚合回放，P4.93 随后只补同一技能敌方法盾目标税 mana 不足拒绝 fixture 并纳入聚合回放，P4.14 随后只补《诺克萨斯新兵》鼓舞费用减免代表执行切片，P4.15 随后只补《踏苔蜥》`等级3` 入场 +1/法盾代表执行切片，P4.16 随后只补《风行狐》`等级3` 入场 +1/游走代表执行切片，P4.17 随后只补《无极学徒》`等级6` 打出抽 1 代表执行切片，P4.21 随后只补《崔法利求战者》鼓舞自增益代表执行切片，P4.22 随后只补《危险二人组》鼓舞目标临时战力代表执行切片，P4.23 随后只补《垃圾场小霸王》鼓舞弃 2 抽 2 代表执行切片，P4.24 随后只补《先锋队长》鼓舞创建随从代表执行切片，P4.28/P4.29 随后只补《易》及 A 版本 `等级6` 法盾/游走代表执行切片。

## P4.8 Equipment Keyword Profile

本阶段继续装备关键词的低风险 profile 小批次，只做识别和边界锁定，不进入 P5 装备大系统：

- 新增 `CardEquipmentKeywordRules` 与 `CardEquipmentKeywordProfile`，从 P3 `BehaviorSpec` 官方文本和 P2 source object tags 识别 `装配`、`灵便`、`百炼`，并同步暴露 `武装` 标签。
- `装配` 只在装备牌自身卡面出现装配关键词时识别；`灵便` 只从装备自身关键词或 P2 equipment tag 识别；`百炼` 从单位 source tag 或卡面行首关键词识别，避免把“授予其他对象关键词”的文本误当成自身关键词。
- profile status 为 `recognized-deferred`：只表示 P3 parser、官方文本和 P2 registry/tag 已对齐；不表示装配费用、贴附/卸除、灵便反应打出自动贴附、百炼可选贴附、控制权或未激活装备文本已完整执行。
- P4.58 追加 `CardEquipmentAttachmentProfile`：只把已由 P2 手写规则验证的《取放自如》武装贴附/卸除视为 P4 代表路径，不改变 P4.8 对装配/灵便/百炼完整系统的 deferred 判定。
- 代表卡验证：`SFD·033/221 多兰之盾`、`SFD·022/221 长剑`、`SFD·008/221 哨兵好手`、`SFD·085/221 奥恩`。
- 新增 `P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags` 锁定官方卡面文本到 profile；新增 `P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen` 复用 5 条已审计 fixture 保持 P2 装备打出/no-optional 百炼路径绿色。
- P4.58 新增 `P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach` 和 `P4EquipmentAttachmentRepresentativeKeepsTakeUpAttachDetachFixturesGreen`，锁定《取放自如》贴附/卸除 `武装` 与抽 1 代表路径。
- 本批次没有实现装备战力持续修正、装配费用支付、灵便自动贴附、百炼 optional branch、owner/controller 变更或贴附状态下的文本开关。

## P4.9 Completion Audit And Remaining Profiles

本阶段按 active goal 做完成审计。结论：P4 仍不能标记为 goal-complete，因为多个显式能力只有 profile/delegated 边界，还没有真实执行路径；但 P4.9 补齐了之前未覆盖的剩余关键词和基础动作 profile，避免状态文件只靠人工备注。

Prompt-to-artifact checklist：

| Requirement | Evidence | Status |
|---|---|---|
| 权限关键词：迅捷、反应、急速 | `CardPermissionKeywordRules`、`P4PermissionKeywordProfilesMapOfficialTextToRegistryFlags`、`P4PermissionKeywordProfileIncludesJinxHasteReadyDiscardBranch`、`P4PermissionKeywordProfileIncludesJinxAltAHasteReadyDiscardBranch`、`P4SwiftKeywordAllowsCleaveInSpellDuelFocusWindow`、`P4HasteOptionalReadyBranchPaysManaAndPowerForRepresentative`、`P4HasteOptionalReadyBranchRejectsInsufficientPowerFixture`、`P4HasteOptionalReadyBranchPaysManaAndPowerForBabyShark`、`P4HasteOptionalReadyBranchPaysManaAndPowerForLegionRearguard`、`P4HasteOptionalReadyBranchPaysManaAndPowerForJinxDiscardBranch`、`P4HasteOptionalReadyBranchPaysManaAndPowerForJinxAltADiscardBranch`、`P4HasteOptionalReadyBranchPaysManaAndPowerForMrRoot`、`P4HasteOptionalReadyBranchPaysManaAndPowerForMechManiac`、`P4HasteOptionalReadyBranchPaysManaAndPowerForXersaiFish`、`P4HasteOptionalReadyBranchPaysManaAndPowerForKarinaVeraze`、`P4HasteOptionalReadyBranchPaysManaAndPowerForCrimsonSignetTreant`、`P4HasteOptionalReadyBranchPaysManaAndPowerForCrimsonSignetTreantAltA`、`P4HasteOptionalReadyBranchPaysManaAndPowerForNilah`、`P4HasteOptionalReadyBranchPaysManaAndPowerForRengar`、`P4HasteOptionalReadyBranchPaysManaAndPowerForRengarAltA`、`P4HasteOptionalReadyBranchPaysManaAndPowerForMissFortune`、`P4HasteOptionalReadyBranchPaysManaAndPowerForMissFortuneAltA`、`P4HasteOptionalReadyBranchPaysManaAndPowerForSivir`、`P4HasteOptionalReadyBranchPaysManaAndPowerForSivirAltA`、`P4HasteOptionalReadyBranchPaysManaAndPowerForLillia`、`P4HasteOptionalReadyBranchPaysManaAndPowerForLilliaAltA`、`P4HasteOptionalReadyBranchPaysManaAndPowerForAzir`、`P4HasteOptionalReadyBranchPaysManaAndPowerForAzirAltA`、`P4HasteOptionalReadyBranchPaysManaAndPowerForTastyFaerie`、`P4HasteOptionalReadyBranchPaysManaAndPowerForEkko`、`P4HasteOptionalReadyBranchPaysManaAndPowerForArmedAssaulter`、`P4HasteOptionalReadyBranchPaysManaAndPowerForAncientBerserker`、`P4HasteOptionalReadyBranchPaysManaAndPowerForKrakenHunter`、`P4HasteOptionalReadyBranchPaysManaAndPowerForLeeSin`、`P4HasteOptionalReadyBranchPaysManaAndPowerForLeeSinAltA`、`P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher`、`P4HasteOptionalReadyBranchPaysManaAndPowerForKaisa`、`P4HasteOptionalReadyBranchPaysManaAndPowerForKaisaAltA`、`P4HasteOptionalReadyBranchPaysManaAndPowerForReksai`、`P4HasteOptionalReadyBranchPaysManaAndPowerForReksaiAltA`、`p4-play-blazing-drake-haste-ready-insufficient-power-rejected`、`P4PermissionKeywordsKeepExistingP2FixturesGreen` | Partial：迅捷代表路径可玩，反应 P2 path 可玩，《灼焰飞龙》《小鲨鱼》《军团后卫》《金克丝》《金克丝》A 版本、《树根先生》《机械迷》《琢珥鱼》《卡银娜·薇蕊泽》《绯红印记树怪》《绯红印记树怪》A 版本、《尼菈》《雷恩加尔》《雷恩加尔》A 版本、《厄运小姐》《厄运小姐》A 版本、《希维尔》《希维尔》A 版本、《莉莉娅》、《莉莉娅》A 版本、《阿兹尔》、《阿兹尔》A 版本、《美味仙灵》《艾克》《武装强袭者》《远古战狂》《海妖猎手》《李青》《李青》A 版本、《千尾监视者》《卡莎》《卡莎》A 版本、《雷克塞》和《雷克塞》A 版本 `HASTE_READY` 代表路径可玩；P4.103 已补《灼焰飞龙》`HASTE_READY` power 不足拒绝 fixture；其他急速彩色资源精确匹配/活跃分支仍 deferred。 |
| 战斗关键词：强攻、坚守、壁垒、后排、游走 | `CardCombatKeywordRules`、`P4CombatKeywordProfilesMapOfficialTextToRegistryTags`、`P4MoveUnitCommandIsExplicitlyRejectedUntilRoamMovementExists`、`P4MoveUnitCommandRejectionFixture`、`GameCommandMapperParsesMoveUnitPayload`、`p4-move-unit-command-premodel-rejected`、`P4DeclareBattleCommandIsExplicitlyRejectedUntilCombatSystemExists`、`P4DeclareBattleCommandRejectionFixture`、`GameCommandMapperParsesDeclareBattlePayload`、`p4-declare-battle-command-premodel-rejected`、6 条 keyword-unit fixture | Partial：P4.65 已建立 `MOVE_UNIT` command envelope 且显式拒绝执行；P4.98 已补 `MOVE_UNIT` 拒绝 fixture 并纳入战斗关键词聚合；P4.67 已建立 `DECLARE_BATTLE` command envelope 且显式拒绝执行；P4.100 已补 `DECLARE_BATTLE` 拒绝 fixture 并纳入战斗关键词聚合；完整战斗伤害/承伤/游走真实移动 deferred。 |
| 生命周期关键词：瞬息、绝念、预知 | `CardLifecycleKeywordRules`、`P4LifecycleKeywordProfilesMapOfficialTextToRegistryTags`、`P4ScryingShellPredictOutsideTopCardRejectionFixture`、`p4-scrying-shell-predict-outside-top-card-rejected`、4 条 representative fixture | Partial：瞬息到期可玩，预知顶牌回收代表路径 delegated to P2，P4.101 已补预知非顶部目标拒绝 fixture；绝念 trigger queue deferred。 |
| 资源关键词：狩猎、等级、鼓舞、法盾 | `CardResourceKeywordRules`、`P4ResourceKeywordProfilesMapOfficialTextToRegistryTags`、`P4SpellshieldTaxAddsManaForEnemySpellTarget`、`P4SpellshieldTaxAggregatesMultipleEnemySpellTargets`、`P4SpellshieldTaxDoesNotApplyToFriendlySpellTarget`、`GameCommandMapperParsesActivateAbilityPayload`、`P4ActivateAbilityCommandAddsViDoublePowerSkillToStack`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillWithTargets`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillWhenPowerIsMissing`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillFromNonViSource`、`P4ActivateAbilityCommandResolvesViDoublePowerSkillOnStack`、`P4ActivateAbilityCommandUsesCardIdentityAfterViIsPlayed`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillWithOptionalCosts`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillTargetFixture`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillOptionalCostFixture`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillPowerMissingFixture`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillNonViSourceFixture`、`P4ActivateAbilityCommandAddsXerathDamageSkillWithSpellshieldTaxToStack`、`P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSpellshieldTaxManaIsMissing`、`P4ActivateAbilityCommandRejectsXerathDamageSkillSpellshieldTaxInsufficientFixture`、`P4ActivateAbilityCommandResolvesXerathDamageSkillWithSpellshieldTax`、`P4ActivateAbilityCommandDoesNotTaxXerathDamageSkillForFriendlySpellshieldTarget`、`P4ActivateAbilityCommandResolvesXerathDamageSkillWithoutTaxForFriendlySpellshield`、`P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSourceIsExhausted`、`P4ActivateAbilityCommandRejectsXerathDamageSkillFromExhaustedSourceFixture`、`P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsMissing`、`P4ActivateAbilityCommandRejectsXerathDamageSkillMissingTargetFixture`、`P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTooManyTargetsAreProvided`、`P4ActivateAbilityCommandRejectsXerathDamageSkillTooManyTargetsFixture`、`P4ActivateAbilityCommandRejectsXerathDamageSkillWithOptionalCosts`、`P4ActivateAbilityCommandRejectsXerathDamageSkillOptionalCostFixture`、`P4ActivateAbilityCommandRejectsXerathDamageSkillFromNonXerathSource`、`P4ActivateAbilityCommandRejectsXerathDamageSkillNonXerathSourceFixture`、`P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsNotUnit`、`P4ActivateAbilityCommandRejectsXerathDamageSkillNonUnitTargetFixture`、`P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSourceIsNotBattlefield`、`P4ActivateAbilityCommandRejectsXerathDamageSkillSourceNotBattlefieldFixture`、`CoreRuleEngineRejectsMultipleSpellshieldTaxWhenManaIsInsufficient`、`P4EncourageCostReductionPaysReducedManaAfterAnotherCardThisTurn`、`P4EncourageSelfBoonGrantsBoonAfterAnotherCardThisTurn`、`P4EncourageTargetTempMightRequiresPriorCardAndTarget`、`P4EncourageDiscardDrawRequiresPriorCardAndTwoHandTargets`、`P4EncourageMinionCreationRequiresPriorCard`、`P4LevelThresholdAppliesMossStepperPowerAndSpellshieldAtThreeExperience`、`P4LevelThresholdAppliesWindrunnerFoxPowerAndRoamAtThreeExperience`、`P4LevelThresholdDrawsCardForWujiApprenticeAtSixExperience`、`P4LevelThresholdAppliesYiSpellshieldAndRoamAtSixExperience`、`P4LevelThresholdAppliesYiAltASpellshieldAndRoamAtSixExperience`、`p4-play-incinerate-spellshield-tax`、`p4-play-spirit-fire-multiple-spellshield-tax`、`p4-play-secret-art-mercy-friendly-spellshield-no-tax`、`p4-activate-vi-double-power-skill`、`p4-play-then-activate-vi-double-power-skill`、`p4-activate-vi-double-power-skill-target-rejected`、`p4-activate-vi-double-power-skill-optional-cost-rejected`、`p4-activate-vi-double-power-skill-power-missing-rejected`、`p4-activate-vi-double-power-skill-non-vi-source-rejected`、`p4-activate-vi-double-power-skill-opponent-source-rejected`、`p4-activate-vi-double-power-skill-source-not-field-rejected`、`p4-activate-xerath-damage-skill-spellshield-tax`、`p4-activate-xerath-damage-skill-spellshield-tax-insufficient-rejected`、`p4-activate-xerath-damage-skill-friendly-spellshield-no-tax`、`p4-activate-xerath-damage-skill-exhausted-source-rejected`、`p4-activate-xerath-damage-skill-missing-target-rejected`、`p4-activate-xerath-damage-skill-too-many-targets-rejected`、`p4-activate-xerath-damage-skill-optional-cost-rejected`、`p4-activate-xerath-damage-skill-non-xerath-source-rejected`、`p4-activate-xerath-damage-skill-non-unit-target-rejected`、`p4-activate-xerath-damage-skill-source-not-battlefield-rejected`、`p4-play-noxian-recruit-encourage-cost-reduction`、`p4-play-trifarian-gloryseeker-encourage-self-boon`、`p4-play-dangerous-duo-encourage-target-temp-might`、`p4-play-junkyard-bully-encourage-discard-draw`、`p4-play-vanguard-captain-encourage-create-minions`、`p4-play-moss-stepper-level3-spellshield`、`p4-play-windrunner-fox-level3-roam`、`p4-play-wuji-apprentice-level6-draw`、`p4-play-unl-yi-level6-spellshield-roam`、`p4-play-unl-yi-alt-a-level6-spellshield-roam`、代表 fixture | Partial：法术选择敌方场上法盾对象的 mana 目标税可玩，P4.59 已覆盖同一法术同时选择 `法盾` 与 `法盾2` 敌方目标时的税值聚合，P4.61 已覆盖友方法术选择己方 `法盾` 目标时不缴税，P4.73/P4.74 已覆盖《蔚》无目标付费技能入栈、来源身份校验并结算自身本回合战力翻倍，P4.87-P4.92 已覆盖同一技能对手控制来源、来源不在场上、带目标、额外费用、费用不足和非《蔚》来源拒绝边界且不改状态，并由资源关键词聚合回放覆盖，P4.77 已覆盖《泽拉斯》带目标技能对敌方法盾单位的目标税、横置来源和 3 点伤害结算，P4.78 已覆盖同一技能选择己方法盾单位时 `spellshieldTaxMana = 0` 且仍支付 1 power/横置/造成 3 点伤害，P4.79-P4.86/P4.93 已覆盖同一技能敌方法盾目标税费用不足、已横置来源、缺目标、多目标、额外费用、非《泽拉斯》来源、非单位目标和来源不在战场拒绝、对手控制来源拒绝且不改状态，并由资源关键词聚合回放覆盖；《诺克萨斯新兵》鼓舞费用 -2 代表路径可玩；《崔法利求战者》鼓舞自增益代表路径可玩；《危险二人组》鼓舞目标临时战力代表路径可玩；《垃圾场小霸王》鼓舞弃 2 抽 2 代表路径可玩；《先锋队长》鼓舞创建两名 1 战力随从代表路径可玩；《踏苔蜥》`等级3` 入场 +1/法盾代表路径可玩；《风行狐》`等级3` 入场 +1/游走代表路径可玩；《无极学徒》`等级6` 打出抽 1 代表路径可玩；《易》及 A 版本 `等级6` 法盾/游走代表路径可玩；狩猎征服/据守经验、其他等级条件、其他鼓舞效果、更多技能目标税和授予/静态法盾 deferred。 |
| 互动关键词：待命、回响、伏击 | `CardInteractionKeywordRules`、`P4InteractionKeywordProfilesMapOfficialTextToRegistryTags`、`P4EchoKeywordKeepsExistingP2FixturesGreen`、`P4CenterStageEchoInsufficientManaRejectedFixture`、`P4PunishmentIllegalEchoRejectedFixture`、`P4HideCardCommandPlacesStandbyCardFaceDown`、`P4HideCardCommandRejectsInsufficientStandbyCost`、`P4HideCardCommandRejectsInsufficientStandbyCostFixture`、`P4HideCardCommandUsesGuerrillaWarfareFreeStandbyPermission`、`P4HideCardCommandRejectsFreeStandbyWithoutGuerrillaWarfarePermission`、`P4HideCardCommandRejectsFreeStandbyWithoutGuerrillaWarfarePermissionFixture`、`P4GuerrillaWarfareNonStandbyTargetRejectionFixture`、`P4ExistentialDreadFriendlyAttackingTargetRejectedFixture`、`GameCommandMapperParsesHideCardPayload`、`P4RevealCardCommandRevealsStandbyCardInBase`、`P4RevealCardCommandPlaysStandbyReactionToStack`、`P4RevealCardCommandRejectsReactionPlayWithoutPriorityWindow`、`P4RevealCardCommandRejectsReactionPlayWithoutPriorityWindowFixture`、`GameCommandMapperParsesRevealCardPayload`、`SnapshotsRedactOpponentFaceDownObjects`、`P4AmbushPlayCardModeIsExplicitlyRejectedUntilBattlefieldReactionPlayExists`、`P4AmbushPlayCardModeRejectionFixture`、`GameCommandMapperParsesAmbushPlayCardDestination`、`p4-play-center-stage-echo-insufficient-mana-rejected`、`p4-play-punishment-illegal-echo-rejected`、`p4-hide-card-standby-face-down`、`p4-hide-card-standby-insufficient-cost-rejected`、`p4-guerrilla-warfare-free-standby-hide`、`p4-hide-card-standby-free-without-permission-rejected`、`p4-guerrilla-warfare-non-standby-target-rejected`、`p4-play-existential-dread-friendly-attacking-target-rejected`、`p4-reveal-card-standby-base`、`p4-reveal-card-standby-reaction-stack`、`p4-reveal-card-standby-reaction-without-priority-rejected`、`p4-ambush-play-card-premodel-rejected`、3 条 remaining fixture | Partial：mana-only 回响可玩，P4.132 已补《台前作秀》支付 `ECHO` 但 mana 不足拒绝 fixture，P4.133 已补非回响法术《惩戒》携带 `ECHO` 拒绝 fixture，P4.70 已执行待命 `HIDE_CARD` + `STANDBY_A` 最小正面朝下放置，P4.94 已补同一路径费用不足拒绝 fixture，P4.72 已执行《游击战》`FREE_STANDBY_HIDE` / `STANDBY_FREE` 免费待命暗置路径，P4.96 已补 `STANDBY_FREE` 无权限拒绝 fixture，P4.102 已补《游击战》非待命废牌堆目标拒绝 fixture，P4.121 已补《存在焦虑》选择正在进攻的友方单位时拒绝 fixture，P4.71 已执行待命 `REVEAL_CARD` + `STANDBY_REVEAL_0` 基地显露，P4.76 已执行待命 `STANDBY_REACTION` / `STACK` 无目标反应入栈路径，P4.95 已补同一路径无优先权窗口拒绝 fixture，P4.69 已补对手视角正面朝下对象 redaction，P4.64 已建立伏击 `PLAY_CARD mode=AMBUSH` + `destination` envelope 且显式拒绝执行，P4.97 已补该伏击拒绝 fixture，待命触发/完整隐藏区/目标伤害与伏击 reaction battlefield play deferred。 |
| 装备关键词：装配、灵便、百炼 | `CardEquipmentKeywordRules`、`CardEquipmentAttachmentProfile`、`P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags`、`P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach`、`P4AssembleEquipmentCommandIsExplicitlyRejectedUntilEquipmentSystemExists`、`P4AssembleEquipmentCommandRejectionFixture`、`GameCommandMapperParsesAssembleEquipmentPayload`、`p4-assemble-equipment-command-premodel-rejected`、5 条 no-attach fixture、2 条《取放自如》attach/detach fixture | Partial：P4.58 已覆盖《取放自如》武装贴附/卸除代表执行；P4.66 已建立 `ASSEMBLE_EQUIPMENT` command envelope 且显式拒绝执行，P4.99 已补同一路径 fixture 并纳入装备关键词聚合；装配费用、灵便自动贴附、百炼 optional attach、owner/controller deferred。 |
| 基础动作模板：抽牌、伤害、摧毁、眩晕、移动、召回、回收、放逐、临时战力、增益、经验 | `BehaviorTemplatePrimitiveExecutor`、`BehaviorTemplateIds.Recycle/Banish/Boon`、`CardBasicActionRules`、`P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen`、`P4MoveUnitCommandRejectionFixture`、`P4FixedExperienceGainOnPlayUpdatesControllerExperience`、`P4DynamicExperienceGainOnPlayCountsFriendlyFieldUnits`、`P4ExperienceOptionalCostReducesManaAndSpendsExperience`、`P4LevelThresholdDrawsCardForWujiApprenticeAtSixExperience`、P4.106-P4.273 target/cost/order rejection fixtures、`p4-play-chemtech-barrel-target-rejected`、`p4-play-soul-wheel-target-rejected`、`p4-play-mushroom-bag-target-rejected`、`p4-play-arena-bar-target-rejected`、`p4-play-pirate-hideout-target-rejected`、`p4-play-forgotten-signpost-target-rejected`、`p4-play-frozen-gem-target-rejected`、`p4-play-crumbling-palace-target-rejected`、`p4-play-scarlet-rose-target-rejected`、`p4-play-reversal-shard-target-rejected`、`p4-play-assembly-rack-target-rejected`、`p4-play-sfur-song-target-rejected`、`p4-play-z-drive-target-rejected`、`p4-play-vanguard-armory-target-rejected`、`p4-play-remembrance-altar-target-rejected`、`p4-play-rage-sigil-target-rejected`、`p4-play-focus-sigil-target-rejected`、`p4-play-insight-sigil-target-rejected`、`p4-play-power-sigil-target-rejected`、`p4-play-discord-sigil-target-rejected`、`p4-play-unity-sigil-target-rejected`、`p4-play-ogn-rage-sigil-target-rejected`、`p4-play-ogn-focus-sigil-target-rejected`、`p4-play-ogn-insight-sigil-target-rejected`、`p4-play-ogn-power-sigil-target-rejected`、`p4-play-ogn-discord-sigil-target-rejected`、`p4-play-ogn-unity-sigil-target-rejected`、`p4-play-wondrous-pack-target-rejected`、`p4-play-siren-target-rejected`、`p4-play-ownerless-treasure-target-rejected`、`p4-play-scavenging-whiz-target-rejected`、`p4-play-mistfall-bladeyard-target-rejected`、`p4-play-shimmering-aurora-target-rejected`、`p4-play-solari-emblem-target-rejected`、`p4-play-vanguard-helm-target-rejected`、`p4-play-honeyfruit-target-rejected`、`p4-play-last-rites-target-rejected`、`p4-play-blade-of-ruined-king-target-rejected`、`p4-play-mysterious-weapon-target-rejected`、`p4-play-sea-monster-hook-target-rejected`、`p4-play-petricite-monument-target-rejected`、`p4-play-zhonyas-hourglass-target-rejected`、`p4-play-edge-of-night-target-rejected`、`p4-play-hearthfire-cloak-target-rejected`、`p4-play-rabadons-deathcap-target-rejected`、`p4-play-blast-cone-target-rejected`、`p4-play-death-list-target-rejected`、`p4-play-cursed-sarcophagus-target-rejected`、`p4-play-boneclub-promo-target-rejected`、`p4-play-hextech-gauntlet-target-rejected`、`p4-play-treasure-golem-target-rejected`、`p4-play-xerath-target-rejected`、`p4-play-dragon-soul-sage-target-rejected`、`p4-play-fluft-poro-target-rejected`、`p4-play-sfd-088-renata-glasc-target-rejected`、`p4-play-sfd-088a-renata-glasc-target-rejected`、`p4-play-ogn-028-draven-target-rejected`、`p4-play-sfd-110-fiora-target-rejected`、`p4-play-sfd-110a-fiora-target-rejected`、`p4-play-sfd-141-irelia-target-rejected`、`p4-play-sfd-141a-irelia-target-rejected`、`p4-play-dragon-caller-target-rejected`、`p4-play-waterbender-target-rejected`、`p4-play-wise-elder-target-rejected`、`p4-play-eager-apprentice-target-rejected`、`p4-play-arena-service-crew-target-rejected`、`p4-play-poro-herder-target-rejected`、`p4-play-ravenbloom-student-target-rejected`、`p4-play-resonant-soul-target-rejected`、`p4-play-bilgewater-bully-target-rejected`、`p4-play-sharpshooter-pirate-target-rejected`、`p4-play-dune-drake-target-rejected`、`p4-play-ember-monk-target-rejected`、`p4-play-hidden-tracker-target-rejected`、`p4-play-undercover-agent-target-rejected`、`p4-play-traveling-merchant-target-rejected`、`p4-play-ogn-kogmaw-target-rejected`、`p4-play-noxian-drummer-target-rejected`、`p4-play-tide-caller-target-rejected`、`p4-play-ogn-202-jinx-target-rejected`、`p4-play-ghost-matron-target-rejected`、`p4-play-albus-ferros-target-rejected`、`p4-play-ogn-karthus-target-rejected`、`p4-play-dunehorn-beast-target-rejected`、`p4-play-gloompath-guard-target-rejected`、`p4-play-apprentice-blacksmith-target-rejected`、`p4-play-mountain-ape-elder-target-rejected`、代表 fixture | Partial：draw/damage/destroy/stun/temp_might primitive；move/recall/recycle/banish/boon template skeleton 均可安全定位到 P2 代表路径；P4.187 已补《炼金科技桶》带目标拒绝 fixture，P4.188 已补《灵魂之轮》带目标拒绝 fixture，P4.189 已补《蘑菇袋》带目标拒绝 fixture，P4.190 已补《竞技场酒吧》带目标拒绝 fixture，P4.191 已补《海盗避风港》带目标拒绝 fixture，P4.192 已补《被遗忘的路标》带目标拒绝 fixture，P4.193 已补《冰封宝石》带目标拒绝 fixture，P4.194 已补《倾颓宫殿》带目标拒绝 fixture，P4.195 已补《猩红玫瑰》带目标拒绝 fixture，P4.196 已补《逆转碎片》带目标拒绝 fixture，P4.197 已补《装配架》带目标拒绝 fixture，P4.198 已补《斯弗尔尚歌》带目标拒绝 fixture，P4.199 已补《Z型驱动》带目标拒绝 fixture，P4.200 已补《先锋军备》带目标拒绝 fixture，P4.201 已补《追忆祭坛》带目标拒绝 fixture，P4.202 已补《暴怒之印》带目标拒绝 fixture，P4.203 已补《专注之印》带目标拒绝 fixture，P4.204 已补《洞察之印》带目标拒绝 fixture，P4.205 已补《力量之印》带目标拒绝 fixture，P4.206 已补《不和之印》带目标拒绝 fixture，P4.207 已补《团结之印》带目标拒绝 fixture，P4.208 已补 OGN 版《暴怒之印》带目标拒绝 fixture，P4.209 已补 OGN 版《专注之印》带目标拒绝 fixture，P4.210 已补 OGN 版《洞察之印》带目标拒绝 fixture，P4.211 已补 OGN 版《力量之印》带目标拒绝 fixture，P4.212 已补 OGN 版《不和之印》带目标拒绝 fixture，P4.213 已补 OGN 版《团结之印》带目标拒绝 fixture，P4.214 已补《奇妙行囊》带目标拒绝 fixture，P4.215 已补《塞壬号》带目标拒绝 fixture，P4.216 已补《无主宝藏》带目标拒绝 fixture，P4.217 已补《拾荒小能手》带目标拒绝 fixture，P4.218 已补《雾临剑冢》带目标拒绝 fixture，P4.219 已补《闪耀极光》带目标拒绝 fixture，P4.220 已补《烈阳徽记》带目标拒绝 fixture，P4.221 已补《先锋之盔》带目标拒绝 fixture，P4.222 已补《蜜糖果实》带目标拒绝 fixture，P4.223 已补《临终仪式》带目标拒绝 fixture，P4.224 已补《破败王者之刃》带目标拒绝 fixture，P4.225 已补《来路不明的武器》带目标拒绝 fixture，P4.226 已补《海兽钓钩》带目标拒绝 fixture，P4.227 已补《禁魔石丰碑》带目标拒绝 fixture，P4.228 已补《中娅沙漏》带目标拒绝 fixture，P4.229 已补《夜之锋刃》带目标拒绝 fixture，P4.230 已补《炉火斗篷》带目标拒绝 fixture，P4.231 已补《灭世者的死亡之冠》带目标拒绝 fixture，P4.232 已补《喷射球果》带目标拒绝 fixture，P4.233 已补《夺命名单》带目标拒绝 fixture，P4.234 已补《受诅咒的石棺》带目标拒绝 fixture，P4.235 已补 promo 编号《碎骨棒》带目标拒绝 fixture，P4.236 已补《海克斯科技护手》带目标拒绝 fixture，P4.237 已补《宝藏魔像》带目标拒绝 fixture，P4.238 已补《泽拉斯》普通手牌打出带目标拒绝 fixture，P4.239 已补《龙魂贤者》普通手牌打出带目标拒绝 fixture，P4.240 已补《绵绵魄罗》普通手牌打出带目标拒绝 fixture，P4.241 已补 SFD·088《烈娜塔·戈拉斯克》普通手牌打出带目标拒绝 fixture，P4.242 已补 SFD·088a《烈娜塔·戈拉斯克》普通手牌打出带目标拒绝 fixture，P4.243 已补 OGN·028《德莱文》普通手牌打出带目标拒绝 fixture，P4.244 已补 SFD·110《菲奥娜》普通手牌打出带目标拒绝 fixture，P4.245 已补 SFD·110a《菲奥娜》普通手牌打出带目标拒绝 fixture，P4.246 已补 SFD·141《艾瑞莉娅》普通手牌打出带目标拒绝 fixture，P4.247 已补 SFD·141a《艾瑞莉娅》普通手牌打出带目标拒绝 fixture，P4.248 已补《唤龙使者》普通手牌打出带目标拒绝 fixture，P4.249 已补《驭水者》普通手牌打出带目标拒绝 fixture，P4.250 已补《睿智长者》普通手牌打出带目标拒绝 fixture，P4.251 已补《踊跃的学徒》普通手牌打出带目标拒绝 fixture，P4.252 已补《竞技场勤务小队》普通手牌打出带目标拒绝 fixture，P4.253 已补《魄罗牧者》普通手牌打出带目标拒绝 fixture，P4.254 已补《拉文布鲁姆学生》普通手牌打出带目标拒绝 fixture，P4.255 已补《残响之魂》普通手牌打出带目标拒绝 fixture，P4.256 已补《比尔吉沃特恶霸》普通手牌打出带目标拒绝 fixture，P4.257 已补《神射海盗》普通手牌打出带目标拒绝 fixture，P4.258 已补《沙丘亚龙》普通手牌打出带目标拒绝 fixture，P4.259 已补《余火修士》普通手牌打出带目标拒绝 fixture，P4.260 已补《隐秘追踪者》普通手牌打出带目标拒绝 fixture，P4.261 已补《卧底特工》普通手牌打出带目标拒绝 fixture，P4.262 已补《旅行商人》普通手牌打出带目标拒绝 fixture，P4.263 已补《克格莫》普通手牌打出带目标拒绝 fixture，P4.264 已补《诺克萨斯鼓手》普通手牌打出带目标拒绝 fixture，P4.265 已补《控潮者》普通手牌打出带目标拒绝 fixture，P4.266 已补 OGN·202《金克丝》普通手牌打出带目标拒绝 fixture，P4.267 已补 OGN·226《幽灵主母》普通手牌打出带目标拒绝 fixture，P4.268 已补 OGN·230《阿不思·菲罗斯》普通手牌打出带目标拒绝 fixture，P4.269 已补 OGN·236《卡尔萨斯》普通手牌打出带目标拒绝 fixture，P4.270 已补 SFD·027《穿沙角兽》普通手牌打出带目标拒绝 fixture，P4.271 已补 SFD·035《幽径守卫》普通手牌打出带目标拒绝 fixture，P4.272 已补 SFD·041《学徒铁匠》普通手牌打出带目标拒绝 fixture，P4.273 已补 SFD·047《山猿老祖》普通手牌打出带目标拒绝 fixture；173 条均纳入基础动作聚合。固定/动态经验、经验费用和《无极学徒》等级条件抽牌代表路径可玩；激活/条件经验和更多动态分支 deferred。 |
| 复用 P3 BehaviorSpec/template skeleton | `BehaviorTemplateDelegationBridge`、`BehaviorTemplatePrimitiveExecutor`、baseline tests、`P4ObjectiveNamedSurfacesHaveRepresentativeCoverage` | Covered for registered templates and representative P2 bridges; P4.75 adds a prompt-to-artifact coverage audit across every named P4 keyword/action surface. |
| 保持 P2/P2.5/P3 绿色 | Latest Validation below | Covered by build/full/conformance/catalog/P4 narrow tests after this batch. |
| 补测试/文档/状态文件并提交 | `CardCatalogBaselineTests`、`ConformanceFixtureRunnerTests`、README、本文件、git commit | Covered for P4.273 once committed. |

P4.80 追加证据：`P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsMissing`、`P4ActivateAbilityCommandRejectsXerathDamageSkillMissingTargetFixture` 和 `p4-activate-xerath-damage-skill-missing-target-rejected.fixture.json` 锁定《泽拉斯》带目标技能缺少“一名单位”目标时拒绝且不改状态。

P4.81 追加证据：`P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTooManyTargetsAreProvided`、`P4ActivateAbilityCommandRejectsXerathDamageSkillTooManyTargetsFixture` 和 `p4-activate-xerath-damage-skill-too-many-targets-rejected.fixture.json` 锁定同一技能提供两个目标时拒绝且不改状态。

P4.82 追加证据：`P4ActivateAbilityCommandRejectsXerathDamageSkillWithOptionalCosts`、`P4ActivateAbilityCommandRejectsXerathDamageSkillOptionalCostFixture` 和 `p4-activate-xerath-damage-skill-optional-cost-rejected.fixture.json` 锁定同一技能携带未支持 optional cost 时拒绝且不改状态。

P4.83 追加证据：`P4ActivateAbilityCommandRejectsXerathDamageSkillFromNonXerathSource`、`P4ActivateAbilityCommandRejectsXerathDamageSkillNonXerathSourceFixture` 和 `p4-activate-xerath-damage-skill-non-xerath-source-rejected.fixture.json` 锁定同一技能由非《泽拉斯》来源伪造 ability id 时拒绝且不改状态。

P4.84 追加证据：`P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsNotUnit`、`P4ActivateAbilityCommandRejectsXerathDamageSkillNonUnitTargetFixture` 和 `p4-activate-xerath-damage-skill-non-unit-target-rejected.fixture.json` 锁定同一技能选择场上装备等非单位目标时拒绝且不改状态；同批次把 P4.79-P4.84 的《泽拉斯》拒绝 fixtures 纳入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.85 追加证据：`P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSourceIsNotBattlefield`、`P4ActivateAbilityCommandRejectsXerathDamageSkillSourceNotBattlefieldFixture` 和 `p4-activate-xerath-damage-skill-source-not-battlefield-rejected.fixture.json` 锁定同一技能来源位于基地而非战场时拒绝且不改状态；同批次把 P4.79-P4.86 的《泽拉斯》拒绝 fixtures 纳入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.87 追加证据：`P4ActivateAbilityCommandRejectsViDoublePowerSkillFromOpponentSource`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillOpponentSourceFixture` 和 `p4-activate-vi-double-power-skill-opponent-source-rejected.fixture.json` 锁定《蔚》技能来源由对手控制时拒绝且不改状态；同批次把该 fixture 纳入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.88 追加证据：`P4ActivateAbilityCommandRejectsViDoublePowerSkillWhenSourceIsNotField`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillSourceNotFieldFixture` 和 `p4-activate-vi-double-power-skill-source-not-field-rejected.fixture.json` 锁定《蔚》技能来源不在场上时拒绝且不改状态；同批次把该 fixture 纳入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.89 追加证据：`P4ActivateAbilityCommandRejectsViDoublePowerSkillTargetFixture` 和 `p4-activate-vi-double-power-skill-target-rejected.fixture.json` 锁定《蔚》无目标技能携带目标时拒绝且不改状态；同批次把该 fixture 纳入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.90 追加证据：`P4ActivateAbilityCommandRejectsViDoublePowerSkillWithOptionalCosts`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillOptionalCostFixture` 和 `p4-activate-vi-double-power-skill-optional-cost-rejected.fixture.json` 锁定《蔚》无目标技能携带未支持 optional cost 时拒绝且不改状态；同批次把该 fixture 纳入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.91 追加证据：`P4ActivateAbilityCommandRejectsViDoublePowerSkillPowerMissingFixture` 和 `p4-activate-vi-double-power-skill-power-missing-rejected.fixture.json` 锁定《蔚》无目标技能缺少 power 费用时拒绝且不改状态；同批次把该 fixture 纳入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.92 追加证据：`P4ActivateAbilityCommandRejectsViDoublePowerSkillNonViSourceFixture` 和 `p4-activate-vi-double-power-skill-non-vi-source-rejected.fixture.json` 锁定非《蔚》来源伪造 `PAY_2_RED_DOUBLE_POWER` 时拒绝且不改状态；同批次把该 fixture 纳入 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 聚合回放。

P4.9 新增内容：

- 新增 `CardLifecycleKeywordRules` 与 `CardLifecycleKeywordProfile`，覆盖 `瞬息`、`绝念`、`预知` 的实现/委托/暂缓状态。
- 扩展 `CardInteractionKeywordRules`，在既有 `回响` profile 之外补 `待命`、`伏击` 的 interaction profile。
- 新增 `CardBasicActionRules` 与 `CardBasicActionProfile`，把基础动作分为 primitive、delegated-to-P2 和 deferred 三类，补齐 `回收`、`放逐`、`增益`、`经验` 的 P4 状态表达。
- 新增 `P4LifecycleKeywordProfilesMapOfficialTextToRegistryTags`、`P4InteractionKeywordProfilesMapOfficialTextToRegistryTags`、`P4BasicActionProfilesCoverPrimitiveDelegatedAndDeferredActions` 三个 CardCatalog baseline tests。
- 新增 `P4LifecycleKeywordProfilesKeepExistingRepresentativeFixturesGreen`、`P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen`、`P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen`，复用 10 条已审计 fixture。
- 本批次没有实现 `绝念` 触发队列、广义 `预知` 授予、`待命` 正面朝下/翻开、`伏击` 反应战场打出、经验获得/消耗状态、完整战斗或装备贴附系统；P4.10 随后只补固定打出获得经验的低风险切片。

## P4.10 Fixed Experience Gain On Play

本阶段补一个低风险真实执行切片，仍不进入等级、狩猎征服/据守、装配贴附或经验消耗大系统：

- `MatchState` 新增 `PlayerExperience`，conformance fixture schema 新增 `initialState.experience` / `expected.finalState.experience`，玩家 snapshot 暴露 `experience`。
- `CardBehaviorDefinition.GainExperienceOnPlay` 只表示固定数值的“当你打出此牌时获得 N 经验”，在 stack item 结算、源牌入场后追加 `EXPERIENCE_GAINED` 事件并更新控制者经验。
- 已接入代表卡：`UNL-092/219 德玛西亚使节` 获得 1 经验，`UNL-034/219 暖春之使` 获得 2 经验，`UNL-158/219 牧人的传家宝` 获得 1 经验。
- `CardBasicActionRules` 将这些固定经验路径标记为 `delegated-to-P2` / `recognized-covered`；P4.19 随后将 `UNL-157/219 严厉军士` 这类“按友方场上单位数量获得经验”的动态计数代表路径接入。
- 已更新三条已审计 fixture：`p2-preflight-play-demacia-envoy-experience-static`、`p2-preflight-play-spring-messenger-experience-static`、`p2-preflight-play-shepherds-heirloom-weapon-equipment`。
- 本批次没有实现经验消耗、等级阈值、狩猎征服/据守经验、装配消耗经验、获得经验记忆条件、动态友方单位计数或任何 P5 装备贴附系统。

## P4.11 Experience Optional Cost Slice

本阶段继续沿 P4.10 的玩家经验状态补一个低风险费用切片，只覆盖“支付固定经验作为打出额外费用来减少法力费用”的代表路径：

- `CoreRuleEngine` 新增 `SPEND_EXPERIENCE:n` optional cost，先只在 registry 显式配置了 `OptionalExperienceCost` 与 `ManaReductionIfExperiencePaid` 的牌上接受。
- `CardBehaviorDefinition` 新增 `OptionalExperienceCost` / `ManaReductionIfExperiencePaid`，当前只接入 `UNL-178/219 波比` 与 `UNL-178a/219 波比`：支付 3 经验，使打出费用减少 3。
- `COST_PAID` payload 暴露 `experience` 与 `optionalCostManaReduction`，`MatchState.PlayerExperience` 在打出时扣减；经验不足时以 `InsufficientCost` 拒绝且不改变状态。
- 新增 fixture `p4-play-poppy-spend-experience-reduce-cost.fixture.json`，验证 P1 以 3 mana + 3 experience 打出《波比》，结算后 P1 experience 为 0，单位进入基地。
- 新增负向 engine test `CoreRuleEngineRejectsPoppyExperienceOptionalCostWhenExperienceIsInsufficient`。
- `CardBasicActionRules` 将固定经验获得和固定经验额外费用都视为已委托/覆盖的 experience 行为，但仍把 `UNL-164/219 安全检查员` 这类会改变效果分支的经验额外费用保持 deferred。
- 本批次没有实现经验激活技能、经验额外费用改变目标范围、装配消耗经验、等级阈值、获得经验记忆、伏击反应战场打出或壁垒战斗承伤。

## P4.12 Spellshield Target Tax Slice

本阶段补一个资源关键词的真实费用切片，只覆盖“法术选择敌方场上对象”的最小 `法盾` 目标税：

- `CardResourceKeywordRules.SpellshieldTaxFromTags` 复用 P4.7 的 `法盾` / `法盾N` 标签解析，供 `CoreRuleEngine` 费用计划调用。
- `CoreRuleEngine` 在 `PLAY_CARD` 目标合法后、费用支付前计算 `spellshieldTaxMana`：只有非单位/非装备的法术式来源，且目标为敌方 base/battlefield 对象并带 `法盾`/`法盾N` 标签时，才把税值加入总 mana cost。
- `COST_PAID` payload 新增 `spellshieldTaxMana` 与 `spellshieldTaxTargetObjectIds`，代表路径可回放具体由哪些目标触发额外费用。
- 新增 fixture `p4-play-incinerate-spellshield-tax.fixture.json`：`OGS·003/024 焚烧` 选择敌方带 `法盾` 的场上单位时，P1 支付基础 2 mana + 1 mana 目标税，结算后造成 2 点伤害。
- 新增负向 engine test `CoreRuleEngineRejectsSpellshieldTaxWhenManaIsInsufficient`，验证只有 2 mana 时不能对敌方 `法盾` 单位打出《焚烧》，且不改状态；P4.104 已把同一边界提升为 `p4-play-incinerate-spellshield-tax-insufficient-rejected.fixture.json`。
- 本批次没有实现技能选择目标、同一次效果多次选取的 FAQ 全细节、授予/静态法盾、目标税可选支付 UI、法盾与反制/控制权/技能结算链的交互，也不进入 P5 触发替换或 P6 全卡牌批量迁移。

## P4.13 Haste Ready Optional Cost Slice

本阶段回到权限关键词里的 `急速`，先补一个官方代表的可选额外费用分支，不改变全局单位默认入场状态，也不批量启用全部急速牌：

- `CardBehaviorDefinition` 新增 `HasteReadyManaCost` / `HasteReadyPowerCost`，P4.13 先给 `OGN·001/298 灼焰飞龙` 配置 `1 mana + 1 power`，对应官方“额外支付 1 和红色”文本在现有资源模型下的最小代表。
- `CardPermissionKeywordRules` 新增 `HASTE_READY` optional cost helper，并把配置了代表费用的急速 profile 状态改为 `implemented-representative`；其他带 `急速` 标签但未配置费用的牌仍保持 `recognized-deferred`。
- `CoreRuleEngine` 在打出费用计划中接受 `optionalCosts: ["HASTE_READY"]`，将额外 mana/power 加入 `COST_PAID`，并把 optional cost 带到 stack item，供结算时记录本次单位入场来自急速活跃分支。
- 新增 fixture `p4-play-blazing-drake-haste-ready.fixture.json`：P1 以 6 mana + 1 power 打出《灼焰飞龙》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- 新增负向 engine test `P4HasteOptionalReadyBranchRejectsInsufficientPower`，验证缺少 power 时不能支付 `HASTE_READY`，且不改手牌、符文池或结算链；P4.103 已把同一边界提升为 `p4-play-blazing-drake-haste-ready-insufficient-power-rejected.fixture.json`。
- 本批次没有重写普通单位入场的活跃/休眠默认规则，没有处理全部彩色符能精确匹配、急速授予、从手牌以外打出获得急速、战场目的地、游走/战斗联动或 P5 触发替换系统。

## P4.14 Encourage Cost Reduction Slice

本阶段继续资源关键词里的 `鼓舞`，只补一个官方代表的费用减免分支，不批量启用所有鼓舞效果：

- `MatchState` 新增 `PlayerCardsPlayedThisTurn`，在成功 `PLAY_CARD` 时递增，并在 `END_TURN` 进入下一回合前清空；snapshot player view 暴露 `cardsPlayedThisTurn`，便于开发期 UI/调试观察。
- `CardCostReductionConditionKinds` 新增 `CONTROLLER_PLAYED_ANOTHER_CARD_THIS_TURN`，由 `CoreRuleEngine.ResolveCostReductionMana` 在费用支付前判断；当前被打出的牌尚未计入，因此 `count > 0` 就表示本回合已打出过其他卡牌。
- `OGN·012/298 诺克萨斯新兵` registry 接入 `CostReductionMana: 2`，对应官方“鼓舞—我的费用减少 2”文本；未满足鼓舞条件时仍按原 P2 fixture 支付基础 4 mana。
- 新增 fixture `p4-play-noxian-recruit-encourage-cost-reduction.fixture.json`：P1 先以 0 mana 打出《暴怒之印》，双方让过后再以 2 mana 打出《诺克萨斯新兵》，`COST_PAID` payload 记录 `baseMana: 4` 与 `costReductionMana: 2`。
- 新增负向 engine test `CoreRuleEngineRejectsNoxianRecruitEncourageReductionWithoutPriorCardThisTurn`，验证没有同回合先前打牌记忆且只有 2 mana 时不能打出《诺克萨斯新兵》，且不改手牌、符文池或结算链。
- 新增 `P4CardsPlayedThisTurnMemoryResetsWhenTurnEnds`，锁定“本回合”记忆在回合结束后清空。
- 本批次没有实现鼓舞的目标选择、活跃入场、弃牌抽牌、额外打出随从、从废牌堆打出、有色费用、技能上的鼓舞条件或 P5/P6 触发队列；这些仍按更小批次拆分。

## P4.15 Level Threshold Source Unit Slice

本阶段继续资源关键词里的 `等级`，只补一个官方代表的入场静态修正，不进入等级费用阶梯、战斗、技能或全局静态系统：

- `CardBehaviorDefinition` 新增 `LevelExperienceThreshold` / `LevelSourceUnitPowerBonus` / `LevelSourceUnitTags`，作为“控制者经验不少于阈值时，源单位入场获得固定战力和标签”的窄配置。
- `CoreRuleEngine.PlaySourceUnitToBase` 在单位入场时读取控制者 `PlayerExperience`；阈值满足时把固定战力加到源单位，并追加配置标签。
- 当前只给 `UNL-047/219 踏苔蜥` 配置 `等级3`：P1 experience >= 3 时入场 `+1` 战力并获得 `法盾` 标签；experience 低于 3 时旧 P2 fixture 仍保持 3 战力、`犬形|狩猎2` 标签。
- 新增 fixture `p4-play-moss-stepper-level3-spellshield.fixture.json`：P1 以 3 experience 打出《踏苔蜥》，双方让过后源单位进入基地成为 4 战力，标签为 `CARD_TYPE:UNIT|法盾|犬形|狩猎2`。
- 新增 `P4LevelThresholdAppliesMossStepperPowerAndSpellshieldAtThreeExperience`，并扩展 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 与 `P4ResourceKeywordProfilesMapOfficialTextToRegistryTags`，锁定 P3 official text -> registry -> engine fixture 的证据链。
- 本批次没有实现等级费用减少阶梯、等级改变法术效果、等级激活技能、等级给予全局静态效果、狩猎获得经验、法盾技能目标税、或其他牌的等级分支。

## P4.16 Level Threshold Roam Source Unit Slice

本阶段复用 P4.15 的窄模型，只补另一个官方等级 3 代表，验证等级入场修正不只适用于法盾标签：

- 当前给 `UNL-075/219 风行狐` 配置 `等级3`：P1 experience >= 3 时入场 `+1` 战力并获得 `游走` 标签；experience 低于 3 时旧 P2 fixture 仍保持 3 战力、`犬形|狩猎2` 标签。
- 新增 fixture `p4-play-windrunner-fox-level3-roam.fixture.json`：P1 以 3 experience 打出《风行狐》，双方让过后源单位进入基地成为 4 战力，标签为 `CARD_TYPE:UNIT|游走|犬形|狩猎2`。
- 新增 `P4LevelThresholdAppliesWindrunnerFoxPowerAndRoamAtThreeExperience`，并扩展 `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 与 `P4ResourceKeywordProfilesMapOfficialTextToRegistryTags`，锁定 P3 official text -> registry -> engine fixture 的证据链。
- 本批次没有实现游走跨战场移动、等级活跃进场、等级费用减少阶梯、等级改变法术效果、等级激活技能、狩猎获得经验或其他牌的等级分支。

## P4.17 Level Threshold Draw Slice

本阶段继续资源关键词和基础动作模板的交界，只补一个“等级满足时打出抽牌”代表，不进入狩猎经验事件、动态经验或广义等级系统：

- `CardBehaviorDefinition` 新增 `LevelDrawOnPlayCount`，表示控制者满足 `LevelExperienceThreshold` 时，在源牌结算过程中让控制者抽固定张数。
- `CoreRuleEngine.ResolveStackItemEffect` 在源牌入场和固定获得经验后检查等级阈值；满足时复用现有 `ApplyDrawToPlayer`，因此燃尽、胜利检查和 `CARD_DRAWN` 事件仍走 P2 已验证抽牌路径。
- 当前给 `UNL-040/219 无极学徒` 配置 `等级6`：P1 experience >= 6 时打出后抽 1；experience 低于 6 时旧 P2 fixture 仍保持 2 战力、`狩猎` 标签且不抽牌。
- 新增 fixture `p4-play-wuji-apprentice-level6-draw.fixture.json`：P1 以 6 experience 打出《无极学徒》，双方让过后源单位进入基地，随后抽 1 张主牌堆顶牌。
- 新增 `P4LevelThresholdDrawsCardForWujiApprenticeAtSixExperience`，并扩展 resource/basic-action profile tests 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现狩猎征服/据守获得经验、其他等级阈值、等级费用减少阶梯、等级改变法术效果、等级激活技能或动态条件抽牌。

## P4.18 Haste Ready Second Representative Slice

本阶段继续权限关键词里的 `急速`，补第二个低风险代表，验证 `HASTE_READY` 不是单卡特例，同时不进入彩色符能总系统或完整战斗：

- `UNL-006/219 小鲨鱼` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，复用 P4.13 的 `HASTE_READY` 费用计划。
- 新增 fixture `p4-play-baby-shark-haste-ready.fixture.json`：P1 以 4 mana + 1 power 打出《小鲨鱼》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- 新增 `P4HasteOptionalReadyBranchPaysManaAndPowerForBabyShark`，并扩展 permission profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现彩色资源精确匹配、急速授予、从手牌以外打出获得急速、战场目的地、强攻战斗修正、游走/战斗联动或 P5 触发替换系统。

## P4.19 Dynamic Experience Gain Slice

本阶段继续基础动作模板里的经验获得，只补一个仍处于 `PLAY_CARD` 结算内的动态计数代表，不进入战斗胜利、移动触发或技能激活：

- `CardBehaviorDefinition` 新增 `GainExperienceOnPlayPerFriendlyFieldUnit`，当前只给 `UNL-157/219 严厉军士` 配置 `1`，对应官方“场上每有一名友方单位，便获得 1 经验”。
- `CoreRuleEngine` 在源单位入场后统计控制者基地和战场中的友方 `CARD_TYPE:UNIT` 对象，乘以配置值后复用既有 `GainExperience` / `EXPERIENCE_GAINED` 事件；友方装备、敌方单位和非单位对象不计入。
- 更新原 P2 fixture `p2-preflight-play-stern-sergeant-experience-static`：无其他友方单位时，《严厉军士》自身入场后计为 1 名友方场上单位，因此获得 1 经验。
- 新增 fixture `p4-play-stern-sergeant-dynamic-experience.fixture.json`：P1 已有 2 名友方场上单位和 1 件友方装备，打出《严厉军士》后共 3 名友方单位，获得 3 经验；敌方单位和友方装备不计入。
- 新增 `P4DynamicExperienceGainOnPlayCountsFriendlyFieldUnits`，并扩展 basic-action profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现狩猎征服/据守经验、战斗/移动触发经验、经验激活技能、经验改变效果或目标范围、装备装配经验消耗、更多动态经验卡牌和 P5 触发队列。

## P4.20 Haste Ready Third Representative Slice

本阶段继续权限关键词里的 `急速`，补第三个低风险代表，验证 `HASTE_READY` 能覆盖同样“额外支付 1 和红色”的不同基础费用单位：

- `OGN·010/298 军团后卫` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“急速（你可以选择额外支付 1 和红色，让我以活跃状态进场。）”。
- 新增 fixture `p4-play-legion-rearguard-haste-ready.fixture.json`：P1 以 3 mana + 1 power 打出《军团后卫》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- 新增 `P4HasteOptionalReadyBranchPaysManaAndPowerForLegionRearguard`，并扩展 permission profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现彩色资源精确匹配、急速授予、从手牌以外打出获得急速、战场目的地、战斗关键词或 P5 触发替换系统。

## P4.21 Encourage Self-Boon Slice

本阶段继续资源关键词里的 `鼓舞`，补一个低风险的“打出时自身增益”代表，不进入目标选择、随从指示物、废牌堆打出或技能鼓舞：

- `StackItemState` 新增 `PlayedAfterAnotherCardThisTurn` 快照位，在 `PLAY_CARD` 入栈时记录控制者打出此牌前是否已打出过其他牌，避免结算时用当前牌自身错误触发鼓舞。
- `OGN·217/298 崔法利求战者` registry 接入 `GrantsBoonToSourceUnit`，并用 `SourceBoonConditionKind: PLAYED_AFTER_ANOTHER_CARD_THIS_TURN` 限定只在鼓舞条件满足时自增益。
- 新增 fixture `p4-play-trifarian-gloryseeker-encourage-self-boon.fixture.json`：P1 先打出 0 费《暴怒之印》，再打出《崔法利求战者》，双方让过后源单位进入基地并获得 `增益` 标签和 +1 战力。
- `p2-preflight-play-trifarian-gloryseeker-no-encourage-unit.fixture.json` 继续覆盖未触发鼓舞时只成为 2 战力 `崔法利` 单位，不获得增益。
- 新增 `P4EncourageSelfBoonGrantsBoonAfterAnotherCardThisTurn`，并扩展 resource/basic-action profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现《危险二人组》目标战力修正、《垃圾场小霸王》弃牌抽牌、《先锋队长》随从指示物、《不死军团》废牌堆打出、德莱厄斯活跃/光环、技能鼓舞或 P5/P6 触发队列。

## P4.22 Encourage Target Temp Might Slice

本阶段继续资源关键词里的 `鼓舞`，补一个低风险的“打出时选择单位并给予本回合战力修正”代表，不进入弃牌抽牌、指示物、废牌堆打出或技能鼓舞：

- `CardBehaviorDefinition` 新增 `TargetCountConditionKind`，当前只接入 `PLAYED_AFTER_ANOTHER_CARD_THIS_TURN`，用于让同一张源牌在鼓舞未触发时要求 0 目标、鼓舞触发时要求 1 目标。
- `CoreRuleEngine` 的目标数量校验与结算前目标数量复核均复用同一个条件；结算阶段使用 `StackItemState.PlayedAfterAnotherCardThisTurn` 快照，避免当前牌自身错误触发鼓舞。
- `OGN·016/298 危险二人组` registry 接入条件目标数、`AnyUnit` + `CARD_TYPE:UNIT` 目标限制和 `PowerModifierAmount: 2`，复用既有 `POWER_MODIFIED_UNTIL_END_OF_TURN` 状态写入。
- 新增 fixture `p4-play-dangerous-duo-encourage-target-temp-might.fixture.json`：P1 先打出 0 费《暴怒之印》，再以一名己方基地单位为目标打出《危险二人组》，双方让过后目标本回合内战力 +2。
- `p2-preflight-play-dangerous-duo-no-encourage-mechanical-unit.fixture.json` 继续覆盖未触发鼓舞时 0 目标入场；`CoreRuleEngineRejectsDangerousDuoEncourageWhenPriorCardButMissingTarget` 覆盖鼓舞已触发但缺少目标时的拒绝路径。
- 新增 `P4EncourageTargetTempMightRequiresPriorCardAndTarget`，并扩展 resource/basic-action profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现《垃圾场小霸王》弃牌抽牌、《先锋队长》随从指示物、《不死军团》废牌堆打出、德莱厄斯活跃/光环、技能鼓舞、目标选择 UI 或 P5/P6 触发队列；其中《垃圾场小霸王》弃牌抽牌已由 P4.23 后续切片覆盖。

## P4.23 Encourage Discard Draw Slice

本阶段先做 completion audit，结论仍是 P4 不能标记 goal complete：技能目标税、待命/伏击、完整战斗、装备贴附、战斗/移动触发经验、《先锋队长》随从指示物、《不死军团》废牌堆打出、德莱厄斯活跃/光环、其他急速牌彩色资源/活跃分支仍有明确 deferred 项。继续推进时选择最低风险的鼓舞弃牌抽牌代表，不进入指示物/废牌堆打出/战斗/装备系统：

- `CardDrawConditionKinds.PlayedAfterAnotherCardThisTurn` 让抽牌判断读取 `StackItemState.PlayedAfterAnotherCardThisTurn` 快照，避免当前牌自身错误触发鼓舞。
- `OGN·020/298 垃圾场小霸王` registry 接入条件目标数、`FriendlyHandCard` 目标限制、弃置 2 张己方手牌和抽 2 张牌；鼓舞未触发时仍要求 0 目标且不抽牌。
- 新增 fixture `p4-play-junkyard-bully-encourage-discard-draw.fixture.json`：P1 先打出 0 费《暴怒之印》，再打出《垃圾场小霸王》并选择两张己方手牌，双方让过后源单位入场，所选手牌进入废牌堆，P1 抽 2。
- `p2-preflight-play-junkyard-bully-no-encourage-mechanical-unit.fixture.json` 继续覆盖未触发鼓舞时 0 目标入场；`CoreRuleEngineRejectsJunkyardBullyEncourageWhenPriorCardButMissingDiscardTargets` 覆盖鼓舞已触发但缺少两个弃牌目标时的拒绝路径。
- 新增 `P4EncourageDiscardDrawRequiresPriorCardAndTwoHandTargets`，并扩展 resource/basic-action profile baseline 与代表 fixture theory，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现《先锋队长》随从指示物、《不死军团》废牌堆打出、德莱厄斯活跃/光环、技能鼓舞、弃牌不足时的 UI 提示细节或 P5/P6 触发队列。

## P4.24 Encourage Minion Creation Slice

本阶段先复核 completion audit，结论仍是 P4 不能标记 goal complete：技能目标税、待命/伏击、完整战斗、装备贴附、战斗/移动触发经验、《不死军团》废牌堆打出、德莱厄斯活跃/光环、其他急速牌彩色资源/活跃分支仍有明确 deferred 项。继续推进时选择低风险的鼓舞随从创建代表，不进入精确战场目的地、触发队列或全卡牌迁移：

- `CardBehaviorDefinition` 新增 `CreatedBaseUnitTokenConditionKind`，当前只接入 `PLAYED_AFTER_ANOTHER_CARD_THIS_TURN`，让 token 创建读取 `StackItemState.PlayedAfterAnotherCardThisTurn` 快照，避免当前牌自身错误触发鼓舞。
- `OGN·218/298 先锋队长` registry 接入创建两名 1 战力 `随从`、带 `CARD_TYPE:UNIT` 标签的基础单位指示物；鼓舞未触发时仍只作为 3 战力 `精锐` 单位入场。
- 新增 fixture `p4-play-vanguard-captain-encourage-create-minions.fixture.json`：P1 先打出 0 费《暴怒之印》，再打出《先锋队长》，双方让过后源单位入场，并创建两名 1 战力随从指示物到控制者基地。
- `p2-preflight-play-vanguard-captain-no-encourage-elite-unit.fixture.json` 继续覆盖未触发鼓舞时不创建随从；`P4EncourageMinionCreationRequiresPriorCard` 覆盖触发路径，resource profile baseline 与代表 fixture theory 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现卡面“此处”的精确战场目的地、随从作为完整牌组来源、技能鼓舞、《不死军团》废牌堆打出、德莱厄斯活跃/光环或 P5/P6 触发队列。

## P4.25 Haste Ready Fourth Representative Slice

本阶段先复核 completion audit，结论仍是 P4 不能标记 goal complete：技能目标税、待命/伏击、完整战斗、装备贴附、战斗/移动触发经验、《不死军团》废牌堆打出、德莱厄斯活跃/光环、其他急速牌彩色资源/活跃分支仍有明确 deferred 项。继续推进时选择低风险的急速可选费用第四代表，不进入移动触发经验或彩色符能总系统：

- `UNL-127/219 树根先生` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和紫色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-mr-root-haste-ready.fixture.json`：P1 以基础 2 mana + 额外 1 mana + 1 power 打出《树根先生》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-mr-root-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 1 战力 `仙灵|急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForMrRoot` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现紫色资源精确匹配、移动至战场时获得 2 经验、游走/战斗联动、从手牌以外打出获得急速或 P5/P6 触发队列。

## P4.26 Haste Ready Fifth Representative Slice

本阶段先复核 completion audit，结论仍是 P4 不能标记 goal complete：技能目标税、待命/伏击、完整战斗、装备贴附、战斗/移动触发经验、《不死军团》废牌堆打出、德莱厄斯活跃/光环、其他急速牌彩色资源/活跃分支仍有明确 deferred 项。继续推进时选择低风险的急速可选费用第五代表，不进入装备贴附或静态战力系统：

- `SFD·068/221 机械迷` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和蓝色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-mech-maniac-haste-ready.fixture.json`：P1 以基础 5 mana + 额外 1 mana + 1 power 打出《机械迷》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-mech-maniac-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 3 战力 `急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForMechManiac` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现蓝色资源精确匹配、武装贴附、贴附武装双倍基础战力加成、装备持续修正、从手牌以外打出获得急速或 P5/P6 触发队列。

## P4.27 Haste Ready Sixth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete，缺口仍包括技能目标税、待命/伏击、完整战斗、装备贴附、战斗/移动触发经验、其他急速牌彩色资源/活跃分支等。选择《琢珥鱼》作为第六个低风险急速代表，只接入可选费用，不进入强力单位计数减费或完整战斗：

- `SFD·103/221 琢珥鱼` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和橙色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-xersai-fish-haste-ready.fixture.json`：P1 以基础 7 mana + 额外 1 mana + 1 power 打出《琢珥鱼》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-xersai-fish-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 6 战力 `急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForXersaiFish` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现橙色资源精确匹配、按强力单位减少费用、强力单位定义的费用动态计数、从手牌以外打出获得急速或 P5/P6 触发队列。

## P4.28 Level Threshold Spellshield/Roam Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `UNL-113/219 易` 作为低风险等级阈值代表，只复用 P4.15/P4.16 已有的 `LevelExperienceThreshold` / `LevelSourceUnitTags` 入场标签模型：

- `UNL-113/219 易` registry 接入 `LevelExperienceThreshold: 6` 和 `LevelSourceUnitTags: 法盾|游走`，对应官方“等级6以上时获得法盾和游走”文本；基础 4 战力与 `狩猎2` 标签继续沿用 P2/P3 registry。
- 新增 fixture `p4-play-unl-yi-level6-spellshield-roam.fixture.json`：P1 拥有 6 经验时支付 4 mana 打出《易》，双方让过后源牌进入基地，成为 4 战力并带 `CARD_TYPE:UNIT`、`法盾`、`游走`、`狩猎2` 标签。
- `p2-preflight-play-unl-yi-hunt-keyword-unit.fixture.json` 继续覆盖低等级/普通 keyword-only 入场路径；`P4LevelThresholdAppliesYiSpellshieldAndRoamAtSixExperience` 和 resource profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现狩猎征服/据守经验获得、法盾技能目标税、游走移动权限、`UNL-113a/219` A 版本或其他等级条件；A 版本随后由 P4.29 单独覆盖。

## P4.29 Level Threshold Spellshield/Roam Alt Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `UNL-113a/219 易` A 版本，复用 P4.28 同一等级阈值标签模型，只补已审计同文本异画分支：

- `UNL-113a/219 易` registry 接入 `LevelExperienceThreshold: 6` 和 `LevelSourceUnitTags: 法盾|游走`，对应官方“等级6以上时获得法盾和游走”文本；基础 4 战力与 `狩猎2` 标签继续沿用 P2/P3 registry。
- 新增 fixture `p4-play-unl-yi-alt-a-level6-spellshield-roam.fixture.json`：P1 拥有 6 经验时支付 4 mana 打出《易》A 版本，双方让过后源牌进入基地，成为 4 战力并带 `CARD_TYPE:UNIT`、`法盾`、`游走`、`狩猎2` 标签。
- `p2-preflight-play-unl-yi-alt-a-hunt-keyword-unit.fixture.json` 继续覆盖低等级/普通 keyword-only 入场路径；`P4LevelThresholdAppliesYiAltASpellshieldAndRoamAtSixExperience` 和 resource profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现狩猎征服/据守经验获得、法盾技能目标税、游走移动权限或其他等级条件。

## P4.30 Haste Ready Seventh Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `SFD·179/221 卡银娜·薇蕊泽` 作为第七个低风险急速代表，只接入可选费用，不进入移动触发、随从指示物创建或完整战场移动：

- `SFD·179/221 卡银娜·薇蕊泽` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和黄色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-karina-veraze-haste-ready.fixture.json`：P1 以基础 7 mana + 额外 1 mana + 1 power 打出《卡银娜·薇蕊泽》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-karina-veraze-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 6 战力 `急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForKarinaVeraze` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现黄色资源精确匹配、移动到战场触发、三名随从指示物创建、从手牌以外打出获得急速或 P5/P6 触发队列。

## P4.31 Haste Ready Eighth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。先审计 `法盾` 技能目标税，确认当前公开 command model 只有 `PLAY_CARD` / pass / ready 等路径，尚无激活技能命令或技能入栈路径可安全挂接；因此本批次不伪造技能目标税，转向 `UNL-029/219 绯红印记树怪` 作为第八个低风险急速代表：

- `UNL-029/219 绯红印记树怪` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和红色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-crimson-signet-treant-haste-ready.fixture.json`：P1 以基础 4 mana + 额外 1 mana + 1 power 打出《绯红印记树怪》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-crimson-signet-treant-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForCrimsonSignetTreant` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现红色资源精确匹配、征服额外触发、征服后增益、技能目标税或 P5/P6 触发队列。

## P4.32 Haste Ready Ninth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·075/298 美味仙灵` 作为第九个低风险急速代表，只接入可选费用，不进入绝念、召符文或抽牌触发队列：

- `OGN·075/298 美味仙灵` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和绿色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-tasty-faerie-haste-ready.fixture.json`：P1 以基础 7 mana + 额外 1 mana + 1 power 打出《美味仙灵》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-tasty-faerie-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 6 战力 `仙灵|急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForTastyFaerie` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现绿色资源精确匹配、绝念召出休眠符文、绝念抽牌或 P5/P6 触发队列。

## P4.33 Haste Ready Tenth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·110/298 艾克` 作为第十个低风险急速代表，只接入可选费用，不进入绝念、回收或符文活跃化：

- `OGN·110/298 艾克` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和蓝色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-ekko-haste-ready.fixture.json`：P1 以基础 5 mana + 额外 1 mana + 1 power 打出《艾克》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-ekko-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 5 战力 `急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForEkko` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现蓝色资源精确匹配、绝念回收、符文活跃化或 P5/P6 触发队列。

## P4.34 Haste Ready Eleventh Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `SFD·002/221 武装强袭者` 作为第十一个低风险急速代表，只接入可选费用，不进入百炼装配、武装贴附或 P5 装备系统：

- `SFD·002/221 武装强袭者` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和红色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-armed-assaulter-haste-ready.fixture.json`：P1 以基础 6 mana + 额外 1 mana + 1 power 打出《武装强袭者》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-armed-assaulter-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用且不选择百炼装配时只作为 6 战力 `急速|百炼` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForArmedAssaulter` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现红色资源精确匹配、百炼装配、武装贴附、装备持续修正或 P5/P6 触发队列。

## P4.35 Haste Ready Twelfth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `SFD·131/221 远古战狂` 作为第十二个低风险急速代表，只接入可选费用，不进入完整战斗、战场位置或动态强攻数值：

- `SFD·131/221 远古战狂` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和紫色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-ancient-berserker-haste-ready.fixture.json`：P1 以基础 5 mana + 额外 1 mana + 1 power 打出《远古战狂》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-ancient-berserker-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `灵体|急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForAncientBerserker` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现紫色资源精确匹配、战场目的地、同处敌方单位计数、动态强攻数值或完整战斗修正。

## P4.36 Haste Ready Thirteenth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·150/298 海妖猎手` 作为第十三个低风险急速代表，只接入可选费用，不进入完整战斗、强攻修正或消耗增益减费：

- `OGN·150/298 海妖猎手` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和橙色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-kraken-hunter-haste-ready.fixture.json`：P1 以基础 3 mana + 额外 1 mana + 1 power 打出《海妖猎手》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-kraken-hunter-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用且不消耗增益时只作为 5 战力 `海盗|急速|强攻` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForKrakenHunter` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现橙色资源精确匹配、强攻战斗修正、消耗增益额外费用、增益消耗事件或动态减费。

## P4.37 Haste Ready Fourteenth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·151/298 李青` 作为第十四个低风险急速代表，只接入可选费用，不进入战场位置、静态光环或友方增益单位持续修正：

- `OGN·151/298 李青` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和橙色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-lee-sin-haste-ready.fixture.json`：P1 以基础 6 mana + 额外 1 mana + 1 power 打出《李青》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-lee-sin-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 6 战力 `急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForLeeSin` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现橙色资源精确匹配、战场目的地、所在战场上其他拥有增益的友方单位 `+2` 静态战力修正或 P5/P6 静态/触发系统。

## P4.38 Haste Ready Fifteenth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·151a/298 李青` A 版本作为第十五个低风险急速代表，只接入可选费用，不进入战场位置、静态光环或友方增益单位持续修正：

- `OGN·151a/298 李青` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和橙色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-lee-sin-alt-a-haste-ready.fixture.json`：P1 以基础 6 mana + 额外 1 mana + 1 power 打出《李青》A 版本，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-lee-sin-alt-a-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 6 战力 `急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForLeeSinAltA` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现橙色资源精确匹配、战场目的地、所在战场上其他拥有增益的友方单位 `+2` 静态战力修正或 P5/P6 静态/触发系统。

## P4.39 Haste Ready Sixteenth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·116/298 千尾监视者` 作为第十六个低风险急速代表，在已有 P2 全体敌方单位临时战力修正基础上只补 `HASTE_READY` 可选费用：

- `OGN·116/298 千尾监视者` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和蓝色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-thousand-tailed-watcher-haste-ready.fixture.json`：P1 以基础 7 mana + 额外 1 mana + 1 power 打出《千尾监视者》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`，并继续让所有敌方单位本回合内战力 -3、不得低于 1。
- `p2-preflight-play-thousand-tailed-watcher-all-enemy-units-minus-3.fixture.json` 继续覆盖未支付急速额外费用时的源单位入场和全体敌方单位临时战力修正；`P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现蓝色资源精确匹配、完整战斗或全体效果的泛化模板迁移；全体敌方单位临时战力修正仍沿用 P2 已验证手写行为。

## P4.40 Haste Ready Seventeenth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·039/298 卡莎` 作为第十七个低风险急速代表，只接入可选费用，不进入征服抽牌触发或完整战斗/得分系统：

- `OGN·039/298 卡莎` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和红色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-kaisa-haste-ready.fixture.json`：P1 以基础 4 mana + 额外 1 mana + 1 power 打出《卡莎》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-kaisa-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `急速` 英雄单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForKaisa` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现红色资源精确匹配、征服抽牌触发、战场目的地或 P5/P6 触发队列系统。

## P4.41 Haste Ready Eighteenth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `SFD·029/221 雷克塞` 作为第十八个低风险急速代表，只接入可选费用，不进入强攻战斗修正、非手牌打出授予急速或完整触发系统：

- `SFD·029/221 雷克塞` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方“额外支付 1 和红色，让我以活跃状态进场”文本在现有 mana + power 资源模型下的代表路径。
- 新增 fixture `p4-play-reksai-haste-ready.fixture.json`：P1 以基础 3 mana + 额外 1 mana + 1 power 打出《雷克塞》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-reksai-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 3 战力 `强攻|急速` 英雄单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForReksai` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现红色资源精确匹配、强攻战斗修正、从手牌以外打出的友方单位获得急速、战场目的地或 P5/P6 触发队列系统。

## P4.42 Haste Ready Nineteenth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `SFD·029a/221 雷克塞` A 版本作为第十九个低风险急速代表，保持与普通版本相同的执行边界：

- `SFD·029a/221 雷克塞` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方异画卡面同样的“额外支付 1 和红色，让我以活跃状态进场”文本。
- 新增 fixture `p4-play-reksai-alt-a-haste-ready.fixture.json`：P1 以基础 3 mana + 额外 1 mana + 1 power 打出《雷克塞》A 版本，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-reksai-alt-a-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 3 战力 `强攻|急速` 英雄单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForReksaiAltA` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现红色资源精确匹配、强攻战斗修正、从手牌以外打出的友方单位获得急速、战场目的地或 P5/P6 触发队列系统。

## P4.43 Haste Ready Twentieth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·039a/298 卡莎` A 版本作为第二十个低风险急速代表，复用 P4.40 普通版本边界：

- `OGN·039a/298 卡莎` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官方异画卡面同样的“额外支付 1 和红色，让我以活跃状态进场”文本。
- 新增 fixture `p4-play-kaisa-alt-a-haste-ready.fixture.json`：P1 以基础 4 mana + 额外 1 mana + 1 power 打出《卡莎》A 版本，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-kaisa-alt-a-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `急速` 英雄单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForKaisaAltA` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现红色资源精确匹配、征服抽牌触发、战场目的地或 P5/P6 触发队列系统。

## P4.44 Haste Ready Twenty-First Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `UNL-029a/219 绯红印记树怪` A 版本作为第二十一个低风险急速代表，复用 P4.31 普通版本边界：

- `UNL-029a/219 绯红印记树怪` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；异画官网文本只写 `急速` 关键词，不展开 reminder，本批次以核心关键词规则承载同一 `HASTE_READY` 代表费用。
- 新增 fixture `p4-play-crimson-signet-treant-alt-a-haste-ready.fixture.json`：P1 以基础 4 mana + 额外 1 mana + 1 power 打出《绯红印记树怪》A 版本，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-crimson-signet-treant-alt-a-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForCrimsonSignetTreantAltA` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现红色资源精确匹配、征服额外触发、征服后增益、战场目的地或 P5/P6 触发队列系统。

## P4.45 Haste Ready Twenty-Second Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `UNL-115/219 尼菈` 作为第二十二个低风险急速代表，沿用现有 `HASTE_READY` optional cost 桥接，不进入游走移动或移动触发经验系统：

- `UNL-115/219 尼菈` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官网卡面“额外支付 1 和橙色，让我以活跃状态进场”的急速 reminder。
- 新增 fixture `p4-play-nilah-haste-ready.fixture.json`：P1 以基础 3 mana + 额外 1 mana + 1 power 打出《尼菈》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-nilah-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `急速`/`游走` 恶魔单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForNilah` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现橙色资源精确匹配、游走多战场移动、移动触发获得经验、战场目的地或 P5/P6 触发队列系统。

## P4.46 Haste Ready Twenty-Third Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `UNL-024/219 雷恩加尔` 作为第二十三个低风险急速代表，只接入 `HASTE_READY` optional cost，不进入完整战斗、技能目标税或游走移动系统：

- `UNL-024/219 雷恩加尔` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`，对应官网卡面“额外支付 1 和红色，让我以活跃状态进场”的急速 reminder。
- 新增 fixture `p4-play-rengar-haste-ready.fixture.json`：P1 以基础 4 mana + 额外 1 mana + 1 power 打出《雷恩加尔》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-rengar-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `强攻2`/`急速`/`法盾`/`游走` 猫科单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForRengar` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现红色资源精确匹配、强攻战斗修正、法盾技能目标税、游走多战场移动、战场目的地或 P5/P6 触发队列系统。

## P4.47 Haste Ready Twenty-Fourth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `UNL-024a/219 雷恩加尔` A 版本作为第二十四个低风险急速代表，复用 P4.46 普通版本边界：

- `UNL-024a/219 雷恩加尔` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；异画官网文本只写 `急速` 关键词，不展开 reminder，本批次以核心关键词规则承载同一 `HASTE_READY` 代表费用。
- 新增 fixture `p4-play-rengar-alt-a-haste-ready.fixture.json`：P1 以基础 4 mana + 额外 1 mana + 1 power 打出《雷恩加尔》A 版本，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-rengar-alt-a-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `强攻2`/`急速`/`法盾`/`游走` 猫科单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForRengarAltA` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现红色资源精确匹配、强攻战斗修正、法盾技能目标税、游走多战场移动、战场目的地或 P5/P6 触发队列系统。

## P4.48 Haste Ready Twenty-Fifth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·162/298 厄运小姐` 作为第二十五个低风险急速代表，复用 P4.45 尼菈的游走/移动触发边界：

- `OGN·162/298 厄运小姐` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；官网文本明确 `急速` 可额外支付 `1` 和橙色以活跃状态进场，P4 当前仍用既有 mana/power 代表模型，不做橙色资源精确匹配。
- 新增 fixture `p4-play-miss-fortune-haste-ready.fixture.json`：P1 以基础 5 mana + 额外 1 mana + 1 power 打出《厄运小姐》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-miss-fortune-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 5 战力 `急速`/`海盗`/`游走` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForMissFortune` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现橙色资源精确匹配、游走多战场移动、每回合首次移动使其他休眠物体变为活跃、战场目的地或 P5/P6 触发队列系统。

## P4.49 Haste Ready Twenty-Sixth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·162a/298 厄运小姐` A 版本作为第二十六个低风险急速代表，复用 P4.48 普通版本边界：

- `OGN·162a/298 厄运小姐` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；官网文本明确 `急速` 可额外支付 `1` 和橙色以活跃状态进场，P4 当前仍用既有 mana/power 代表模型，不做橙色资源精确匹配。
- 新增 fixture `p4-play-miss-fortune-alt-a-haste-ready.fixture.json`：P1 以基础 5 mana + 额外 1 mana + 1 power 打出《厄运小姐》A 版本，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-miss-fortune-alt-a-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 5 战力 `急速`/`海盗`/`游走` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForMissFortuneAltA` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现橙色资源精确匹配、游走多战场移动、每回合首次移动使其他休眠物体变为活跃、战场目的地或 P5/P6 触发队列系统。

## P4.50 Haste Ready Twenty-Seventh Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `SFD·143/221 希维尔` 作为第二十七个低风险急速代表：

- `SFD·143/221 希维尔` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；官网文本明确 `急速` 可额外支付 `1` 和紫色以活跃状态进场，P4 当前仍用既有 mana/power 代表模型，不做紫色资源精确匹配。
- 新增 fixture `p4-play-sivir-haste-ready.fixture.json`：P1 以基础 4 mana + 额外 1 mana + 1 power 打出《希维尔》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-sivir-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForSivir` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现紫色资源精确匹配、万能符能支付计数、本回合 +2 战力、获得游走、游走多战场移动、战场目的地或 P5/P6 触发队列系统。

## P4.51 Haste Ready Twenty-Eighth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `SFD·143a/221 希维尔` A 版本作为第二十八个低风险急速代表，复用 P4.50 普通版本边界：

- `SFD·143a/221 希维尔` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；官网异画文本同样明确 `急速` 可额外支付 `1` 和紫色以活跃状态进场，P4 当前仍用既有 mana/power 代表模型，不做紫色资源精确匹配。
- 新增 fixture `p4-play-sivir-alt-a-haste-ready.fixture.json`：P1 以基础 4 mana + 额外 1 mana + 1 power 打出《希维尔》A 版本，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-sivir-alt-a-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForSivirAltA` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现紫色资源精确匹配、万能符能支付计数、本回合 +2 战力、获得游走、游走多战场移动、战场目的地或 P5/P6 触发队列系统。

## P4.52 Haste Ready Twenty-Ninth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `UNL-082/219 莉莉娅` 作为第二十九个低风险急速代表：

- `UNL-082/219 莉莉娅` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；官网文本明确 `急速` 可额外支付 `1` 和蓝色以活跃状态进场，P4 当前仍用既有 mana/power 代表模型，不做蓝色资源精确匹配。
- 新增 fixture `p4-play-lillia-haste-ready.fixture.json`：P1 以基础 3 mana + 额外 1 mana + 1 power 打出《莉莉娅》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-lillia-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 3 战力 `仙灵`/`急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForLillia` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现蓝色资源精确匹配、游走/移动、移动触发 3 战力瞬息精灵、精灵指示物创建、瞬息到期或 P5/P6 触发队列系统。

## P4.53 Haste Ready Thirtieth Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `UNL-082a/219 莉莉娅` A 版本作为第三十个低风险急速代表，复用 P4.52 普通版本边界：

- `UNL-082a/219 莉莉娅` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；官网异画文本只写 `急速` 关键词，不展开 reminder，本批次以核心关键词规则承载同一 `HASTE_READY` 代表费用。
- 新增 fixture `p4-play-lillia-alt-a-haste-ready.fixture.json`：P1 以基础 3 mana + 额外 1 mana + 1 power 打出《莉莉娅》A 版本，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-lillia-alt-a-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 3 战力 `仙灵`/`急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForLilliaAltA` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现蓝色资源精确匹配、游走/移动、移动触发 3 战力瞬息精灵、精灵指示物创建、瞬息到期或 P5/P6 触发队列系统。

## P4.54 Haste Ready Thirty-First Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `SFD·177/221 阿兹尔` 作为第三十一个低风险急速代表，延续现有 `HASTE_READY` optional cost 桥接：

- `SFD·177/221 阿兹尔` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；官网文本明确 `急速` 可额外支付 `1` 和黄色以活跃状态进场，P4 当前仍用既有 mana/power 代表模型，不做黄色资源精确匹配。
- 新增 fixture `p4-play-azir-haste-ready.fixture.json`：P1 以基础 4 mana + 额外 1 mana + 1 power 打出《阿兹尔》，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-azir-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `鸟类`/`急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForAzir` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现黄色资源精确匹配、进攻触发、指示物单位移动、战场目的地或 P5/P6 触发队列系统。

## P4.55 Haste Ready Thirty-Second Representative Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `SFD·177a/221 阿兹尔` A 版本作为第三十二个低风险急速代表，复用 P4.54 普通版本边界：

- `SFD·177a/221 阿兹尔` registry 接入 `HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；官网异画文本同样明确 `急速` 可额外支付 `1` 和黄色以活跃状态进场，P4 当前仍用既有 mana/power 代表模型，不做黄色资源精确匹配。
- 新增 fixture `p4-play-azir-alt-a-haste-ready.fixture.json`：P1 以基础 4 mana + 额外 1 mana + 1 power 打出《阿兹尔》A 版本，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`。
- `p2-preflight-play-azir-alt-a-no-optional-haste.fixture.json` 继续覆盖未支付急速额外费用时只作为 4 战力 `鸟类`/`急速` 单位入场；`P4HasteOptionalReadyBranchPaysManaAndPowerForAzirAltA` 和 profile baseline 锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现黄色资源精确匹配、进攻触发、指示物单位移动、战场目的地或 P5/P6 触发队列系统。

## P4.56 Haste Ready Jinx Discard Branch Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·030/298 金克丝` 作为第三十三个低风险急速代表，原因是它已有 P2 手写“打出时弃两张手牌”路径，但尚未把官方 `急速`/`强攻2` 标签和 `HASTE_READY` 代表费用接到同一条可玩路径。

- `OGN·030/298 金克丝` registry 接入 `SourceUnitTags: "急速|强攻2"`、`HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；官网文本明确 `急速` 可额外支付 `1` 和红色以活跃状态进场，P4 当前仍用既有 mana/power 代表模型，不做红色资源精确匹配。
- 更新 `p2-preflight-play-jinx-discard-two-hand.fixture.json`：未支付急速额外费用时，源单位仍以 4 战力进入基地并弃置两张手牌，但现在记录 `急速` / `强攻2` 对象标签。
- 新增 fixture `p4-play-jinx-haste-ready.fixture.json`：P1 以基础 3 mana + 额外 1 mana + 1 power 打出《金克丝》，选择另外两张己方手牌，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`，随后按目标顺序弃置两张牌。
- 新增 `P4PermissionKeywordProfileIncludesJinxHasteReadyDiscardBranch` 和 `P4HasteOptionalReadyBranchPaysManaAndPowerForJinxDiscardBranch`，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现红色资源精确匹配、强攻战斗修正、金克丝 A 版本 `HASTE_READY`、弃牌触发英雄金克丝文本、战场目的地或 P5/P6 触发队列系统。

## P4.57 Haste Ready Jinx Alt Discard Branch Slice

本阶段继续按 completion audit 推进：P4 仍不能标记 goal complete。选择 `OGN·030a/298 金克丝` A 版本作为第三十四个低风险急速代表，复用 P4.56 普通版本边界：同一官方卡面文本、同一打出弃两张手牌路径、同一 `HASTE_READY` 代表费用模型。

- `OGN·030a/298 金克丝` registry 接入 `SourceUnitTags: "急速|强攻2"`、`HasteReadyManaCost: 1` / `HasteReadyPowerCost: 1`；官网异画文本明确 `急速` 可额外支付 `1` 和红色以活跃状态进场，P4 当前仍用既有 mana/power 代表模型，不做红色资源精确匹配。
- 更新 `p2-preflight-play-jinx-alt-a-discard-two-hand.fixture.json`：未支付急速额外费用时，源单位仍以 4 战力进入基地并弃置两张手牌，但现在记录 `急速` / `强攻2` 对象标签。
- 新增 fixture `p4-play-jinx-alt-a-haste-ready.fixture.json`：P1 以基础 3 mana + 额外 1 mana + 1 power 打出《金克丝》A 版本，选择另外两张己方手牌，双方让过后单位进入基地，事件 payload 标记 `hasteReadyOptionalCostPaid: true` 且 `isExhausted: false`，随后按目标顺序弃置两张牌。
- 新增 `P4PermissionKeywordProfileIncludesJinxAltAHasteReadyDiscardBranch` 和 `P4HasteOptionalReadyBranchPaysManaAndPowerForJinxAltADiscardBranch`，锁定官方文本 -> registry -> engine fixture 的证据链。
- 本批次没有实现红色资源精确匹配、强攻战斗修正、弃牌触发英雄金克丝文本、战场目的地或 P5/P6 触发队列系统。

## P4.58 Equipment Attachment Representative Audit

本阶段先做 completion audit，而不是把测试通过直接等同于 goal complete。审计结论：P4 仍不能标记完成；不过 P2 已有一条可验证的低风险装备贴附/卸除路径，可以作为 P4 装备执行代表补进证据链：

- 新增 `CardEquipmentAttachmentProfile` / `EquipmentAttachmentProfileStatuses`，只读识别 `CardBehaviorDefinition.AttachesOrDetachesSecondTargetEquipmentToFirstTarget`，并把《取放自如》归类为 `implemented-representative`。
- 新增 `P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach`，锁定官网卡面文本里的 `反应`、`武装`、`贴附`、`卸除` 到 `SFD·011/221` registry 和 P4.58 profile。
- 新增 `P4EquipmentAttachmentRepresentativeKeepsTakeUpAttachDetachFixturesGreen`，复用 `p2-preflight-play-take-up-attach-weapon-draw` / `p2-preflight-play-take-up-detach-weapon-draw`，断言 `attachedToObjectId` 写入/清除与 `EQUIPMENT_ATTACHED` / `EQUIPMENT_DETACHED` 事件。
- 本批次没有实现装配费用、灵便装备自身反应打出后的自动贴附、百炼 optional attach、装备静态修正、贴附层级、owner/controller 分离、控制权变更或 P5 装备大系统。

Prompt-to-artifact completion audit：

| Requirement | P4.58 decision |
|---|---|
| 权限关键词 | 迅捷/反应 profile 和代表时机已覆盖；急速 34/34 代表 `HASTE_READY` 费用已覆盖，但彩色资源精确匹配和若干活跃联动仍 deferred。 |
| 战斗关键词 | P4.6 只有 profile；P4.65/P4.67 已建立移动与战斗声明 command envelope 并显式拒绝执行；P4.98/P4.100 已分别把 `MOVE_UNIT` 与 `DECLARE_BATTLE` 前置拒绝边界纳入 fixture；完整强攻/坚守/壁垒/后排战斗承伤与游走移动仍是明确缺口。 |
| 生命周期关键词 | 瞬息到期、预知代表路径与预知非顶部目标拒绝 fixture 有覆盖；绝念触发队列仍 deferred。 |
| 资源关键词 | 法术目标法盾税、多个等级/鼓舞/经验代表路径已覆盖；P4.59 进一步锁定多目标法术对 `法盾` + `法盾2` 的税值聚合；P4.61 锁定友方法盾目标 no-tax 边界；P4.73 执行《蔚》无目标付费技能入栈/结算代表路径；P4.74 补场上对象 `cardNo` 身份校验，防止非《蔚》来源伪造 ability id；P4.77 执行《泽拉斯》带目标技能的敌方法盾税、横置和 3 点伤害代表路径；P4.78 补同一技能己方法盾目标 no-tax 边界；P4.79 补同一技能已横置来源拒绝边界；P4.80 补同一技能缺少目标拒绝边界；P4.81 补同一技能多目标拒绝边界；P4.82 补同一技能额外费用拒绝边界；P4.83 补同一技能非《泽拉斯》来源拒绝边界；P4.84 补同一技能非单位目标拒绝边界；P4.85 补同一技能来源不在战场拒绝边界；P4.86 补对手控制来源拒绝边界；P4.93 补敌方法盾目标税费用不足拒绝边界，并把 P4.79-P4.86/P4.93 拒绝 fixtures 纳入资源关键词聚合回放；更多技能目标税、狩猎征服/据守经验、更多动态经验仍 deferred。 |
| 互动关键词 | 回响 mana-only 已执行；P4.70 已执行待命 `HIDE_CARD` + `STANDBY_A` 最小正面朝下放置；P4.94 已把同一路径费用不足拒绝边界纳入 fixture；P4.72 已执行《游击战》`FREE_STANDBY_HIDE` / `STANDBY_FREE` 免费待命暗置；P4.96 已把 `STANDBY_FREE` 无权限拒绝边界纳入 fixture；P4.71 已执行待命 `REVEAL_CARD` + `STANDBY_REVEAL_0` 基地显露；P4.76 已执行待命 `STANDBY_REACTION` / `STACK` 无目标反应入栈；P4.95 已把无优先权窗口拒绝边界纳入 fixture；P4.69 已补对手视角正面朝下对象 redaction；P4.97 已把伏击 `PLAY_CARD mode=AMBUSH` 前置拒绝边界纳入 fixture；待命触发/完整隐藏区/目标伤害与伏击反应战场打出仍 deferred。 |
| 装备关键词 | P4.58 覆盖《取放自如》武装贴附/卸除代表路径；P4.66 建立 `ASSEMBLE_EQUIPMENT` command envelope 且显式拒绝执行；P4.99 已把 `ASSEMBLE_EQUIPMENT` 前置拒绝边界纳入 fixture；装配/灵便/百炼的完整贴附、费用和未激活文本仍 deferred。 |
| 基础动作模板 | draw/damage/destroy/stun/temp might primitive plan 已覆盖；P4.98 已把 `MOVE_UNIT` 前置拒绝边界纳入 fixture；move/recall/recycle/banish/boon/experience 的部分代表路径仍 delegated to P2 或小批次执行。 |

## P4.59 Spellshield Multi Target Tax Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。当前公开 command model 还没有安全的 `ACTIVATE_ABILITY` / 技能入栈路径，因此本批次不伪造技能目标税；转而选择现有 `PLAY_CARD` 多目标法术路径，把已实现的法盾费用计算锁成可回放证据。

- 代表牌选择 `OGN·256/298 妖异狐火`：官方文本允许选择一处战场中任意数量且总战力不高于 4 的单位，已有 P2 手写路径会在双方让过后摧毁目标，适合验证多目标费用聚合而不扩张规则系统。
- 新增 fixture `p4-play-spirit-fire-multiple-spellshield-tax.fixture.json`：P1 以 6 mana 打出《妖异狐火》，同时选择敌方 `法盾` 2 战力单位和 `法盾2` 2 战力单位，费用事件记录基础 3 mana + `spellshieldTaxMana: 3`，结算后两个目标都进入 P2 废牌堆。
- 新增 `P4SpellshieldTaxAggregatesMultipleEnemySpellTargets` 和 `CoreRuleEngineRejectsMultipleSpellshieldTaxWhenManaIsInsufficient`：前者锁定 fixture 结果和 `COST_PAID` payload，后者验证只有 5 mana 时不能支付基础 3 + 法盾税 3，状态保持不变；P4.105 已把同一边界提升为 `p4-play-spirit-fire-multiple-spellshield-tax-insufficient-rejected.fixture.json`。
- `CoreRuleEngine.ResolveSpellshieldTargetTaxMana` 已按目标遍历并累计 `法盾` / `法盾N` 标签税值，本批次只补代表 fixture、负向测试和文档证据，不改主规则引擎路径。
- 本批次没有实现技能目标税、授予/静态法盾、face-down/待命、伏击、战斗承伤、游走移动、完整装备装配/灵便/百炼或 P5/P6 批量迁移。

## P4.60 Recycle Banish Boon Template Skeleton Slice

本阶段先按 objective 做 completion audit：P4 仍不能完成，因为强攻/坚守/壁垒/后排战斗、游走移动、待命/伏击 face-down、技能目标税、完整装备装配/灵便/百炼、触发/替代大系统仍未覆盖。可继续安全推进的缺口是基础动作模板：`回收`、`放逐`、`增益` 此前只有 `CardBasicActionRules` profile/delegated 状态，尚未进入 P3 `BehaviorTemplateIds` / template registry。

Prompt-to-artifact checklist：

| Requirement | P4.60 evidence | Status |
|---|---|---|
| 复用 P3 BehaviorSpec/template skeleton | `BehaviorTemplateIds.Recycle`、`BehaviorTemplateIds.Banish`、`BehaviorTemplateIds.Boon`，`RuleTextParsers` 解析 `回收`/`放逐`/`增益`，`BehaviorTemplateRegistry` 注册三个 skeleton route | Covered for skeleton only。 |
| 不替换 CoreRuleEngine 主路径 | `BehaviorTemplatePrimitiveExecutor` 对三者仍返回 `delegated-to-p2`，状态写入继续由 P2 手写 `CoreRuleEngine` 行为完成 | Covered。 |
| 规则证据与官网文本 | `OGN·156/298 暗中破坏`、`OGN·102/298 传送门大营救`、`OGN·053/298 秘奥义！慈悲度魂落` 已在 `docs/rules-evidence-index.md` 和 `docs/p2-rules-preflight.md` 有 RULE_AUDITED 行 | Covered。 |
| Engine/catalog 测试 | `RuleTextParserExtractsMinimumP3Fields`、`BehaviorTemplateExecutorRoutesRegisteredTemplatesWithoutReplacingP2Rules`、`P4BridgeDelegatesLowRiskTemplatesToExistingP2Behaviors`、`P4PrimitiveExecutorBuildsBasicActionPlansAndLeavesComplexRoutesDelegated` 覆盖 parser、registry、safe delegation 和 delegated primitive plan | Covered。 |
| Conformance fixture | `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 继续回放 `p2-preflight-play-disposal-order-recycle-opponent-graveyard`、`p2-preflight-play-portalpal-rescue-banish-play-base`、`p2-preflight-play-secret-art-mercy-grant-boon` | Covered by existing audited fixtures。 |

- 新增 template stats：`boon 66`（implemented 51 / manual 15 / unimplemented 0）、`recycle 63`（55 / 8 / 0）、`banish 11`（8 / 3 / 0）；`/catalog/p3-status` 仍为 schema valid `1009/1009`、status counts `implemented 785`、`manual-rule-required 211`、`unimplemented 13`。
- `BehaviorSpecCatalogBuilder.SafeExistingTemplateMappings` 把这三个模板纳入“有 P2 手写 behavior 时可委托”集合，避免把已审计的 P2 路径误降级成 P3 unimplemented。
- `CardBasicActionRules` 改为优先从 template ids 识别 `recycle` / `banish` / `boon`，再回退到 P2 behavior flag 和官方文本。
- 本批次没有实现回收随机顺序之外的隐藏信息 UI、多玩家选择 prompt、放逐替代/来源放逐通用模型、全局增益额外加成、消耗增益费用或 P5/P6 触发替代系统。

## P4.61 Spellshield Friendly Target No-Tax Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。待命/伏击 face-down 与技能目标税都仍缺安全的公开 command/stack 模型，因此本批次选择法盾费用规则中的低风险边界：只有“对手”以法术或技能选择目标时才产生法盾税。

- 代表牌选择 `OGN·053/298 秘奥义！慈悲度魂落` 与 `OGN·013/298 呸呸魄罗`：前者是已由 P2 手写规则覆盖的友方单目标增益法术，后者提供官方 `法盾` 目标标签。
- 新增 fixture `p4-play-secret-art-mercy-friendly-spellshield-no-tax.fixture.json`：P1 以 3 mana 打出《秘奥义！慈悲度魂落》，选择己方带 `法盾` 的场上单位，费用事件记录 `baseMana: 3`、`spellshieldTaxMana: 0`、空 `spellshieldTaxTargetObjectIds`，结算后目标获得 `增益` 与 +1 战力。
- 新增 `P4SpellshieldTaxDoesNotApplyToFriendlySpellTarget`：锁定 conformance 结果、目标标签/战力和 `COST_PAID` payload；`P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 也纳入该 fixture。
- `CoreRuleEngine.ResolveSpellshieldTargetTaxMana` 既有实现已限定 enemy field object，本批次只补代表 fixture、payload 断言和文档证据，不改主规则引擎路径。
- 本批次没有实现技能目标税、授予/静态法盾、face-down/待命、伏击、战斗承伤、游走移动、完整装备装配/灵便/百炼或 P5/P6 批量迁移。

## P4.62 Activate Ability Command Premodel

本阶段继续 completion audit：P4 仍不能标记 goal complete。`法盾` 的官方规则覆盖“对手以法术或技能选择目标”都需支付目标税，但 P4.62 当时真正可玩路径只有 `PLAY_CARD`；若直接实现技能目标税会缺少安全的激活技能命令、技能入栈和结算边界。因此本批次只补技能 command 前置模型。P4.73 后已在该 envelope 上接入《蔚》无目标付费技能代表路径，技能目标税和通用技能仍 deferred。

- 新增 `ActivateAbilityCommand`，payload 字段为 `sourceObjectId`、`abilityId`、`targetObjectIds` 和 `optionalCosts`，让 Dev UI/fixture/后续技能系统有稳定协议 envelope。
- `GameCommandJsonMapper` 现在能把 `cmdType = "ACTIVATE_ABILITY"` 解析为 typed command；新增 `GameCommandMapperParsesActivateAbilityPayload` 锁定协议字段。
- P4.62 时 `CoreRuleEngine` 对 `ActivateAbilityCommand` 显式返回 `UNSUPPORTED_COMMAND`，并保留当前 prompts/状态不变，避免该 typed command 落入 `PlaceholderRuleEngine` 后被误接受；P4.73/P4.74 后仅接受来源对象 `cardNo = UNL-030/219` 的 `PAY_2_RED_DOUBLE_POWER` 无目标技能，其余技能继续拒绝。
- P4.62 新增的全拒绝测试已在 P4.73 替换为《蔚》入栈/拒绝边界/结算测试；`GameCommandMapperParsesActivateAbilityPayload` 继续锁定协议字段。
- P4.62 本批次没有实现技能费用支付、横置/资源颜色、技能目标税执行、技能结算链、技能伤害/增益、face-down/待命、伏击、完整装备装配/灵便/百炼或 P5/P6 批量迁移；P4.73 只补《蔚》无目标付费技能最小路径，P4.74 只补来源对象身份校验，其余仍 deferred。

## P4.63 Hide Card Command Premodel

本阶段继续 completion audit：P4 仍不能标记 goal complete。`待命` 的官方文本要求“支付 A 正面朝下放置此牌，之后可支付 0 将其当作反应牌打出”；P4.63 当时缺少安全隐藏信息视图，因此只补 `HIDE_CARD` command 前置模型。P4.69/P4.70 后已分别补 snapshot redaction 与最小放置执行，但翻开/触发/完整隐藏区仍不归 P4.63 覆盖。

- 新增 `HideCardCommand`，payload 字段为 `sourceObjectId`、`cardNo`、`destination` 和 `optionalCosts`，为待命放置路径保留稳定协议 envelope。
- `GameCommandJsonMapper` 现在能把 `cmdType = "HIDE_CARD"` 解析为 typed command；新增 `GameCommandMapperParsesHideCardPayload` 锁定协议字段。
- P4.63 时 `CoreRuleEngine` 对 `HideCardCommand` 显式返回 `UNSUPPORTED_COMMAND`，避免该 typed command 落入 `PlaceholderRuleEngine` 后被误接受；P4.70 后仅接受 `STANDBY_A` 最小放置，其余 hide 变体继续拒绝。
- P4.63 新增的拒绝测试已在 P4.70 替换为 `P4HideCardCommandPlacesStandbyCardFaceDown` / `P4HideCardCommandRejectsInsufficientStandbyCost`，继续用 `OGN·121/298 提莫` 的待命文本作为代表。
- P4.63 本批次没有实现正面朝下放置、待命区/战场位置选择、隐藏信息 snapshot、从正面朝下状态翻开打出、待命触发、伏击或 P5/P6 批量迁移；P4.70 只补最小基地放置，其余仍 deferred。

## P4.64 Ambush Play Premodel

本阶段继续 completion audit：P4 仍不能标记 goal complete。`伏击` 的官方文本要求“可以选择将我作为反应牌，打出到有己方单位的战场”，这同时依赖反应窗口、战场目的地、单位从手牌进入战场而不是基地、以及若干卡牌的特殊战场许可。因此本批次只补 `PLAY_CARD` 的伏击目的地前置模型。

- `PlayCardCommand` 新增 `destination` 字段，保留既有 `sourceObjectId`、`cardNo`、`targetObjectIds`、`mode` 和 `optionalCosts` 字段不变，方便后续表达战场目的地。
- `GameCommandJsonMapper` 现在能解析 `PLAY_CARD` 的 `destination`；新增 `GameCommandMapperParsesAmbushPlayCardDestination` 锁定 `mode = "AMBUSH"` 与 `destination = "BATTLEFIELD:P1-MAIN"` 的协议字段。
- `CoreRuleEngine` 对 `PLAY_CARD mode=AMBUSH` 显式返回 `UNSUPPORTED_COMMAND`，避免伏击请求被当作普通从手牌打出到基地的路径误接受。
- 新增 `P4AmbushPlayCardModeIsExplicitlyRejectedUntilBattlefieldReactionPlayExists`：用 `UNL-021/219 阴森药剂师` 的伏击文本作为代表，验证当前不会扣费用、不会把手牌移出、不会改动战场单位、不会生成 stack item。
- P4.97 新增 `P4AmbushPlayCardModeRejectionFixture` 和 `p4-ambush-play-card-premodel-rejected.fixture.json`，把同一显式拒绝边界纳入 conformance fixture 与互动关键词聚合回放。
- 本批次没有实现伏击反应窗口、战场目的地合法性、打出至战场、特殊敌方战场许可、进攻/防守触发、伏击与待命共用隐藏信息、或 P5/P6 批量迁移。

## P4.65 Move Unit Command Premodel

本阶段继续 completion audit：P4 仍不能标记 goal complete。`游走` 与多张基础移动牌都需要精确战场位置、移动权限、移动次数统计和移动触发模型；当前 `PlayerZones.Battlefields` 仍是公开对象列表，不能表达完整的“此处/其他战场”结算。因此本批次只补 `MOVE_UNIT` command 前置模型。

- 新增 `MoveUnitCommand`，payload 字段为 `sourceObjectId`、`origin`、`destination` 和 `optionalCosts`，为游走/基础移动路径保留稳定协议 envelope。
- `GameCommandJsonMapper` 现在能把 `cmdType = "MOVE_UNIT"` 解析为 typed command；新增 `GameCommandMapperParsesMoveUnitPayload` 锁定协议字段。
- `CoreRuleEngine` 对 `MoveUnitCommand` 显式返回 `UNSUPPORTED_COMMAND`，避免该 typed command 落入 `PlaceholderRuleEngine` 后被误接受。
- 新增 `P4MoveUnitCommandIsExplicitlyRejectedUntilRoamMovementExists`：用 `SFD·235/221 亚索` 的游走文本作为代表，验证当前不会扣费用、不会移动对象、不会改变战力、不会生成 stack item。
- P4.98 新增 `P4MoveUnitCommandRejectionFixture` 和 `p4-move-unit-command-premodel-rejected.fixture.json`，把同一显式拒绝边界纳入 conformance fixture、战斗关键词聚合和基础动作聚合回放。
- 本批次没有实现多战场位置、移动权限、游走合法性、移动触发、单回合移动次数得分、战斗中移动或 P5/P6 批量迁移。

## P4.66 Assemble Equipment Command Premodel

本阶段继续 completion audit：P4 仍不能标记 goal complete。`装配`、`灵便` 与 `百炼` 的真实执行同时依赖装备费用、未激活文本、贴附层级、owner/controller、灵便反应打出后的自动贴附和百炼 optional attach；当前只能安全推进协议前置模型，避免后续装备路径继续停留在字符串命令层。

- 新增 `AssembleEquipmentCommand`，payload 字段为 `sourceObjectId`、`targetObjectId` 和 `optionalCosts`，为装备装配/贴附后续小批次保留稳定协议 envelope。
- `GameCommandJsonMapper` 现在能把 `cmdType = "ASSEMBLE_EQUIPMENT"` 解析为 typed command；新增 `GameCommandMapperParsesAssembleEquipmentPayload` 锁定协议字段。
- `CoreRuleEngine` 对 `AssembleEquipmentCommand` 显式返回 `UNSUPPORTED_COMMAND`，避免该 typed command 落入 `PlaceholderRuleEngine` 后被误接受。
- 新增 `P4AssembleEquipmentCommandIsExplicitlyRejectedUntilEquipmentSystemExists`：用 `SFD·022/221 长剑` 的 `灵便` / `装配红色` 文本作为代表，验证当前不会扣费用、不会移动装备、不会设置 `AttachedToObjectId`、不会生成 stack item。
- P4.99 新增 `P4AssembleEquipmentCommandRejectionFixture` 和 `p4-assemble-equipment-command-premodel-rejected.fixture.json`，把同一显式拒绝边界纳入 conformance fixture 与装备关键词聚合回放。
- 本批次没有实现装配费用、灵便自动贴附、百炼 optional attach、装备静态修正、贴附层级、owner/controller 分离、控制权变更、装备未激活文本或 P5/P6 批量迁移。

## P4.67 Declare Battle Command Premodel

本阶段继续 completion audit：P4 仍不能标记 goal complete。`强攻`、`坚守`、`壁垒`、`后排` 的真实执行都依赖战斗声明、进攻/防守身份、承伤顺序和多战场位置；当前只能安全推进战斗声明协议前置模型，不进入完整战斗结算。

- 新增 `DeclareBattleCommand`，payload 字段为 `battlefieldId`、`attackerObjectIds`、`defenderObjectIds` 和 `optionalCosts`，为后续开战/承伤小批次保留稳定协议 envelope。
- `GameCommandJsonMapper` 现在能把 `cmdType = "DECLARE_BATTLE"` 解析为 typed command；新增 `GameCommandMapperParsesDeclareBattlePayload` 锁定协议字段。
- `CoreRuleEngine` 对 `DeclareBattleCommand` 显式返回 `UNSUPPORTED_COMMAND`，避免该 typed command 落入 `PlaceholderRuleEngine` 后被误接受。
- 新增 `P4DeclareBattleCommandIsExplicitlyRejectedUntilCombatSystemExists`：用 `OGS·007/024 盖伦` 的 `强攻2`/`坚守2` 与 `UNL-036/219 变异猫咪` 的 `坚守2`/`壁垒` 作为代表，验证当前不会扣费用、不会设置 `IsAttacking`/`IsDefending`、不会改变战力、不会生成 stack item。
- 本批次没有实现多战场位置、进攻/防守声明、强攻/坚守战力修正、壁垒/后排承伤顺序、战斗伤害、得分、移动触发或 P5/P6 批量迁移。

## P4.68 Reveal Card Command Premodel

本阶段继续 completion audit：P4 仍不能标记 goal complete。`待命` 已有 P4.63 `HIDE_CARD` 放置前置模型，但官方文本还要求之后可支付 0 将该正面朝下牌作为反应牌打出；P4.68 当时仍缺隐藏信息 snapshot、face-down 对象所有权视图和翻开结算模型，因此本批次只补 `REVEAL_CARD` command 前置模型。P4.71 后已在该 envelope 上接入基地显露，P4.76 后已接入无目标反应入栈；目标结算、触发和完整隐藏区仍 deferred。

- 新增 `RevealCardCommand`，payload 字段为 `sourceObjectId`、`cardNo`、`targetObjectIds`、`mode`、`optionalCosts` 和 `destination`，为待命翻开/显露路径保留稳定协议 envelope。
- `GameCommandJsonMapper` 现在能把 `cmdType = "REVEAL_CARD"` 解析为 typed command；新增 `GameCommandMapperParsesRevealCardPayload` 锁定协议字段。
- P4.68 时 `CoreRuleEngine` 对 `RevealCardCommand` 显式返回 `UNSUPPORTED_COMMAND`，避免该 typed command 落入 `PlaceholderRuleEngine` 后被误接受；P4.71 后仅接受 `STANDBY_REVEAL` / `BASE` 最小显露，其余 reveal 变体继续拒绝。
- P4.68 新增的拒绝测试已在 P4.71/P4.76 替换为 `P4RevealCardCommandRevealsStandbyCardInBase` / `P4RevealCardCommandPlaysStandbyReactionToStack` / `P4RevealCardCommandRejectsReactionPlayWithoutPriorityWindow`，继续用 `OGN·121/298 提莫` 的待命文本作为代表。
- P4.68 本批次没有实现正面朝下放置、隐藏信息 snapshot、从正面朝下状态翻开打出、待命触发、目标结算、伏击共用隐藏信息或 P5/P6 批量迁移；P4.71 只补最小基地显露，其余仍 deferred。

## P4.69 Face-Down Snapshot Redaction

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.63/P4.68 已有待命 hide/reveal command envelope，但 snapshot 若泄漏正面朝下对象的 tags/power/manaCost，就不能安全进入真实待命隐藏信息。因此本批次只补对手视角 redaction，不实现待命区域或翻开打出。

- `ResolutionResult.BuildSnapshots` 现在在非拥有者视角遇到 `IsFaceDown = true` 的可见对象时，只返回 `objectId` 与 `isFaceDown = true`。
- 拥有者视角仍保留完整对象信息，便于后续待命 UI/调试和 command authoring。
- 新增 `SnapshotsRedactOpponentFaceDownObjects`：验证对手视角不泄漏 `power`、`tags`、`manaCost`，拥有者视角仍可见完整属性。
- 本批次没有实现正面朝下放置、隐藏区、翻开打出、`REVEAL_CARD` 执行、待命触发、伏击共用隐藏信息或 P5/P6 批量迁移。

## P4.70 Standby Hide Execution

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.63/P4.69 已分别提供 `HIDE_CARD` envelope 和对手视角 redaction，本批次只把最低风险的待命放置接入执行，不进入翻开、触发或完整 hidden zone。

- `CoreRuleEngine` 现在接受 `HIDE_CARD destination=STANDBY optionalCosts=["STANDBY_A"]`，前提是主动玩家在 main/neutral open、来源牌位于手牌、registry 中能识别为带 `待命` 的单位。
- 执行时支付 1 mana，把来源牌移入控制者基地，设置 `IsFaceDown = true`，并在 owner-visible 状态保留 `power`、`tags` 与 `manaCost` 供后续翻开/调试使用。
- `CARD_HIDDEN` 公开事件只暴露 `playerId`、`sourceObjectId`、`destination`、`destinationZone` 和 `isFaceDown`，故意不携带 `cardNo`、`power`、`tags` 或 `manaCost`；P4.69 的 snapshot redaction 覆盖对手视角对象。
- 新增 `P4HideCardCommandPlacesStandbyCardFaceDown`、`P4HideCardCommandRejectsInsufficientStandbyCost` 和 `p4-hide-card-standby-face-down.fixture.json`。
- 本批次没有实现 `REVEAL_CARD` 执行、待命触发、`游击战` 免费布置权限、战场/隐藏区位置、装备/法术待命、伏击共用隐藏信息或 P5/P6 批量迁移。

## P4.71 Standby Reveal Execution

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.70 已能创建待命正面朝下对象，因此本批次只补最低风险的拥有者基地显露，不把待命牌作为反应牌打到结算链，也不执行目标文本。

- `CoreRuleEngine` 现在接受 `REVEAL_CARD mode=STANDBY_REVEAL destination=BASE optionalCosts=["STANDBY_REVEAL_0"]`，前提是主动玩家在 main/neutral open、来源牌位于控制者基地、对象 `IsFaceDown = true` 且带 `待命`。
- 执行时不扣费用、不移动区域、不创建 stack item，只把 `IsFaceDown` 改为 `false`，并用 registry 补齐 owner/opponent 可见的战力、标签和费用。
- `CARD_REVEALED` 公开事件携带 `cardNo`，因为对象已从隐藏状态转为公开；P4.69 的 snapshot redaction 不再适用于该对象，双方 snapshot 都能看到完整对象信息。
- 新增 `P4RevealCardCommandRevealsStandbyCardInBase` 和 `p4-reveal-card-standby-base.fixture.json`；P4.76 后补充 `P4RevealCardCommandPlaysStandbyReactionToStack` 与反应入栈 fixture。
- 本批次没有实现目标伤害/展示主牌堆、待命触发、`游击战` 免费布置权限、伏击共用隐藏信息或 P5/P6 批量迁移；P4.76 后已补无目标反应入栈。

## P4.72 Guerrilla Warfare Free Standby Hide Permission

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.70 已能安全暗置待命牌，P4.71 已能把基地正面朝下待命单位公开显露；本批次只把《游击战》的“本回合内可无视费用正面朝下布置待命卡牌”接到这条最小 `HIDE_CARD` 路径，不实现待命触发。

- `OGN·264/298 游击战` 结算时，在原有“最多两张己方废牌堆待命牌返回手牌”后授予控制者 `FREE_STANDBY_HIDE:{playerId}` until-end-of-turn 全局效果，并发出 `STANDBY_HIDE_PERMISSION_GRANTED` 事件。
- `CoreRuleEngine` 现在接受 `HIDE_CARD destination=STANDBY optionalCosts=["STANDBY_FREE"]`，前提是主动玩家本回合拥有对应 `FREE_STANDBY_HIDE:{playerId}`；该路径支付 0 mana，并复用 P4.70 的正面朝下基地放置与隐藏信息事件边界。
- 免费暗置权限不在暗置时消耗，随现有回合结束全局 until-end effects 清理；没有权限时 `STANDBY_FREE` 仍被显式拒绝，避免越权绕过 `STANDBY_A` 费用。
- 新增 `P4HideCardCommandUsesGuerrillaWarfareFreeStandbyPermission`、`P4HideCardCommandRejectsFreeStandbyWithoutGuerrillaWarfarePermission`、更新 `p2-preflight-play-guerrilla-warfare-return-standby-graveyard.fixture.json`，并新增 `p4-guerrilla-warfare-free-standby-hide.fixture.json`。
- 本批次没有实现待命触发、战场目的地、装备/法术待命、伏击共用隐藏信息或 P5/P6 批量迁移；P4.76 后已补无目标反应入栈。

## P4.73 Vi Activated Skill Stack Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.62 已有 `ACTIVATE_ABILITY` command envelope，但全拒绝无法验证技能入栈/结算生命周期；本批次只选择 `UNL-030/219 蔚` 的无目标自我战力翻倍技能，避免一次性实现技能目标税、横置技能、装备技能或通用技能 registry。

- `CoreRuleEngine` 现在接受 `ACTIVATE_ABILITY abilityId = PAY_2_RED_DOUBLE_POWER`，前提是主动玩家在 main/neutral open、stack 为空、来源是自己场上的 `UNL-030/219 蔚`，且命令不带目标和可选费用。
- 激活时支付 2 mana + 1 power，创建技能 stack item，写入 `ABILITY_ACTIVATED`、`COST_PAID` 和 `STACK_ITEM_ADDED` 事件，并关闭 neutral/open 让优先权回到主动玩家。
- 双方让过后，技能结算读取来源当前战力，给予本回合内等量战力修正，使来源战力翻倍，并累加 `UntilEndOfTurnPowerModifier`；该路径复用现有 until-end-of-turn 清理模型。
- 新增 `P4ActivateAbilityCommandAddsViDoublePowerSkillToStack`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillWithTargets`、`P4ActivateAbilityCommandRejectsViDoublePowerSkillWhenPowerIsMissing`、`P4ActivateAbilityCommandResolvesViDoublePowerSkillOnStack` 和 `p4-activate-vi-double-power-skill.fixture.json`。
- 本批次没有实现技能目标税、带目标技能、横置技能、装备技能、其他 ability id、通用 skill registry、技能伤害/增益模板或 P5/P6 批量迁移。

## P4.74 Card Object Identity Guard Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.73 已能执行《蔚》无目标技能，但激活校验只看 `abilityId`，缺少场上对象的官方卡号身份，存在非《蔚》单位伪造同一 ability id 的风险。本批次只补身份边界，不实现通用技能 registry。

- `CardObjectState` 新增可选 `CardNo` 字段；`ConformanceFixture` 支持在 `initialState.cardObjects` 与 `expected.finalState.cardObjects` 中读取/断言 `cardNo`。
- 源牌打出到基地、待命暗置和待命显露会保存官方 `behavior.CardNo`，对手视角正面朝下 snapshot 仍只暴露 `objectId` 与 `isFaceDown`，不泄漏卡号。
- `CoreRuleEngine` 激活《蔚》技能时要求来源是控制者场上单位，且 `sourceState.CardNo == UNL-030/219`；非《蔚》单位即使提交 `PAY_2_RED_DOUBLE_POWER` 也返回 `UNSUPPORTED_CARD_BEHAVIOR` 且不扣费、不入栈。
- 新增 `P4ActivateAbilityCommandRejectsViDoublePowerSkillFromNonViSource`、`P4ActivateAbilityCommandUsesCardIdentityAfterViIsPlayed` 和 `p4-play-then-activate-vi-double-power-skill.fixture.json`，证明先打出《蔚》后同一对象能凭 `cardNo` 身份激活技能。
- 本批次没有实现技能目标税、目标技能、横置技能、装备技能、其他 ability id、完整对象可见性重构或 P5/P6 批量迁移。

## P4.75 Objective Coverage Audit Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.74 后最相邻的技能目标税需要带目标技能 registry/结算模型，继续推进会进入通用激活技能系统；因此本批次选择低风险的 prompt-to-artifact 审计测试，把 P4 目标中显式点名的关键词与基础动作逐项锁到现有代表证据。

- 新增 `P4ObjectiveNamedSurfacesHaveRepresentativeCoverage`：读取官方卡表、FunctionalUnit、P3 `BehaviorSpec` 和 P2 `CardBehaviorDefinition`，逐项断言 33 个命名 surface 都有代表 profile、primitive、delegated P2 路径或小批次执行证据。
- 覆盖清单包括：权限关键词 `迅捷`/`反应`/`急速`，战斗关键词 `强攻`/`坚守`/`壁垒`/`后排`/`游走`，生命周期关键词 `瞬息`/`绝念`/`预知`，资源关键词 `狩猎`/`等级`/`鼓舞`/`法盾`，互动关键词 `待命`/`回响`/`伏击`，装备关键词 `装配`/`灵便`/`百炼`，基础动作 `抽牌`/`伤害`/`摧毁`/`眩晕`/`移动`/`召回`/`回收`/`放逐`/`临时战力`/`增益`/`经验获得`/`经验消耗`。
- 该测试不把 `recognized-deferred` 当作完成：它只证明 surface 已被 P3/P4 识别并有明确代表或 deferred 边界；完整战斗、游走移动、待命触发/完整隐藏区/目标伤害、伏击战场打出、技能目标税、完整装备系统和复杂触发仍是明确缺口。
- 本批次不改 `CoreRuleEngine`、不新增可玩路径、不进入 P5/P6/P7；只补 baseline 审计测试与状态文档，防止后续 completion audit 漏掉目标中的显式条目。

## P4.76 Standby Reaction Stack Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。`待命` 官方文本的“之后可支付 0 将其当作反应牌打出”已经具备 P4.70 正面朝下对象、P4.69 对手视角 redaction、P4.71 基地显露和 P4.74 `cardNo` 身份边界，因此本批次只接入最低风险的无目标反应入栈，不执行提莫的展示/伤害/回收触发。

- `CoreRuleEngine` 现在接受 `REVEAL_CARD mode=STANDBY_REACTION destination=STACK optionalCosts=["STANDBY_REVEAL_0"]`，前提是玩家在 main/neutral closed 的结算链优先权窗口中持有 priority，结算链已有待结算项目，来源对象在该玩家基地、`IsFaceDown = true` 且 registry/source object 身份匹配待命单位。
- 执行后来源对象翻为公开、写回官方 `cardNo`/战力/费用/标签、从基地移出，并以官方 behavior effect kind 加入结算链；事件顺序为 `CARD_REVEALED`、`CARD_PLAYED`、`COST_PAID`、`STACK_ITEM_ADDED`，优先权仍归该控制者。
- 新增 `P4RevealCardCommandPlaysStandbyReactionToStack`、`P4RevealCardCommandRejectsReactionPlayWithoutPriorityWindow` 和 `p4-reveal-card-standby-reaction-stack.fixture.json`，并把该 fixture 加入 interaction keyword representative 聚合。
- 本批次没有实现提莫展示主牌堆顶部五张、按待命牌数量伤害敌方单位、回收展示牌、防守/待命打出触发、战场/完整隐藏区、装备/法术待命、伏击共用隐藏信息或 P5/P6 批量迁移。

## P4.77 Xerath Skill Target Tax Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.62-P4.74 已具备激活技能命令、入栈生命周期和场上对象 `cardNo` 身份边界；P4.12/P4.59/P4.61 已具备法术目标税 helper 和边界测试。因此本批次只接入一张官方带目标技能代表，不泛化通用技能 registry。

- `CoreRuleEngine` 现在接受 `ACTIVATE_ABILITY abilityId = PAY_RED_EXHAUST_DAMAGE_3`，前提是主动玩家在 main/neutral open、stack 为空、来源是自己战场中的 `UNL-026/219 泽拉斯`、来源未横置、目标正好是一名场上单位且没有 optional cost。
- 若目标是敌方带 `法盾` / `法盾N` 的场上对象，技能激活会复用目标税 helper 额外支付 mana；代表 fixture 覆盖敌方 `法盾` 单位时支付 1 mana + 1 power、横置泽拉斯、技能入栈，双方让过后对目标造成 3 点伤害。
- 新增 `P4ActivateAbilityCommandAddsXerathDamageSkillWithSpellshieldTaxToStack`、`P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSpellshieldTaxManaIsMissing`、`P4ActivateAbilityCommandResolvesXerathDamageSkillWithSpellshieldTax` 和 `p4-activate-xerath-damage-skill-spellshield-tax.fixture.json`，并把该 fixture 加入 resource keyword representative 聚合。
- 本批次没有实现通用 skill registry、其他 ability id、装备技能、可选费用技能、技能伤害预防/重定向完整边界、授予/静态法盾、P5 触发替代系统或 P6 全卡牌迁移。

## P4.78 Xerath Friendly Spellshield No-Tax Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.77 已把《泽拉斯》带目标技能接入敌方法盾目标税；本批次只补相邻的同控制者边界，验证目标税 helper 不会错误收取友方 `法盾` 目标费用。

- `ACTIVATE_ABILITY abilityId = PAY_RED_EXHAUST_DAMAGE_3` 选择己方带 `法盾` 的场上单位时仍按《泽拉斯》技能正常支付 1 power、横置来源并入栈，但 `COST_PAID.spellshieldTaxMana = 0`，且 `spellshieldTaxTargetObjectIds` 为空。
- 代表 fixture 覆盖双方让过后对该己方单位造成 3 点伤害，来源保持横置，结算链清空；这只锁定友方目标 no-tax 边界，不改变技能目标范围或伤害结算模型。
- 新增 `P4ActivateAbilityCommandDoesNotTaxXerathDamageSkillForFriendlySpellshieldTarget`、`P4ActivateAbilityCommandResolvesXerathDamageSkillWithoutTaxForFriendlySpellshield` 和 `p4-activate-xerath-damage-skill-friendly-spellshield-no-tax.fixture.json`，并把该 fixture 加入 resource keyword representative 聚合。
- 本批次没有实现通用 skill registry、其他 ability id、装备技能、可选费用技能、技能伤害预防/重定向完整边界、授予/静态法盾、P5 触发替代系统或 P6 全卡牌迁移。

## P4.79 Xerath Exhausted Source Rejection Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.77/P4.78 已验证《泽拉斯》带目标技能的敌方法盾税、己方法盾 no-tax、横置和伤害代表路径；本批次只锁定相邻的来源状态拒绝边界，避免已横置来源重复支付并入栈。

- `ACTIVATE_ABILITY abilityId = PAY_RED_EXHAUST_DAMAGE_3` 若来源《泽拉斯》已经横置，会返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付资源、不改变来源状态，也不创建 stack item。
- 新增直接 engine 测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSourceIsExhausted`，验证资源池、来源横置状态、目标战力和结算链保持不变。
- 新增 fixture `p4-activate-xerath-damage-skill-exhausted-source-rejected.fixture.json` 和聚合测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillFromExhaustedSourceFixture`，把该拒绝边界纳入 conformance 回放。
- 本批次没有改变 `CoreRuleEngine` 主实现，只为现有已验证行为补拒绝证据；通用 skill registry、更多 ability id、装备技能、可选费用技能和复杂伤害/替代边界仍 deferred。

## P4.80 Xerath Missing Target Rejection Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.77-P4.79 已验证《泽拉斯》带目标技能的正向、友方 no-tax 和已横置来源拒绝；本批次只锁定官方“一名单位”目标数量边界，避免缺少目标时仍支付资源或横置来源。

- `ACTIVATE_ABILITY abilityId = PAY_RED_EXHAUST_DAMAGE_3` 缺少目标时会返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付资源、不横置来源，也不创建 stack item。
- 新增直接 engine 测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsMissing`，验证资源池、来源活跃状态、目标战力和结算链保持不变。
- 新增 fixture `p4-activate-xerath-damage-skill-missing-target-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillMissingTargetFixture`，把该拒绝边界纳入 conformance 证据。
- 本批次没有改变 `CoreRuleEngine` 主实现，只为现有 exactly-one target 校验补规则证据；通用 skill registry、更多 ability id、装备技能、可选费用技能和复杂伤害/替代边界仍 deferred。

## P4.81 Xerath Too Many Targets Rejection Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.80 已锁定缺目标拒绝；本批次只补 exactly-one target 校验的另一侧，避免提供多个目标时绕过官方“一名单位”限制。

- `ACTIVATE_ABILITY abilityId = PAY_RED_EXHAUST_DAMAGE_3` 提供两个目标时会返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付资源、不横置来源、不伤害目标，也不创建 stack item。
- 新增直接 engine 测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTooManyTargetsAreProvided`，验证资源池、来源活跃状态、两个目标伤害和结算链保持不变。
- 新增 fixture `p4-activate-xerath-damage-skill-too-many-targets-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillTooManyTargetsFixture`，把该拒绝边界纳入 conformance 证据。
- 本批次没有改变 `CoreRuleEngine` 主实现，只为现有 exactly-one target 校验补规则证据；通用 skill registry、更多 ability id、装备技能、可选费用技能和复杂伤害/替代边界仍 deferred。

## P4.82 Xerath Optional Cost Rejection Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.77-P4.81 已验证《泽拉斯》技能的正向、目标税和多个拒绝边界；本批次只锁定官方文本费用边界，避免额外 optional cost 被误接受。

- `ACTIVATE_ABILITY abilityId = PAY_RED_EXHAUST_DAMAGE_3` 携带未支持 optional cost 时会返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付资源、不横置来源、不伤害目标，也不创建 stack item。
- 新增直接 engine 测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillWithOptionalCosts`，验证来源、目标和资源均有效时，额外 optional cost 仍会被拒绝且状态保持不变。
- 新增 fixture `p4-activate-xerath-damage-skill-optional-cost-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillOptionalCostFixture`，把该拒绝边界纳入 conformance 证据。
- 本批次没有改变 `CoreRuleEngine` 主实现，只为现有 optional cost 校验补规则证据；通用 skill registry、更多 ability id、装备技能和复杂伤害/替代边界仍 deferred。

## P4.83 Xerath Source Identity Rejection Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.74 已建立场上对象 `cardNo` 身份边界，P4.77-P4.82 已锁定《泽拉斯》技能正向与多个相邻拒绝分支；本批次只补同一 ability id 的来源身份拒绝证据，避免其他官方英雄单位伪造《泽拉斯》技能。

- `ACTIVATE_ABILITY abilityId = PAY_RED_EXHAUST_DAMAGE_3` 若由 `cardNo != UNL-026/219` 的场上对象发起，会返回 `UNSUPPORTED_CARD_BEHAVIOR`，不推进 tick、不写事件、不支付资源、不横置来源、不伤害目标，也不创建 stack item。
- 新增直接 engine 测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillFromNonXerathSource`，用《蔚》场上对象伪造《泽拉斯》技能，验证资源、来源活跃状态、目标伤害和结算链保持不变。
- 新增 fixture `p4-activate-xerath-damage-skill-non-xerath-source-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillNonXerathSourceFixture`，把该拒绝边界纳入 conformance 证据。
- 本批次没有改变 `CoreRuleEngine` 主实现，只为现有来源身份校验补规则证据；通用 skill registry、更多 ability id、装备技能和复杂伤害/替代边界仍 deferred。

## P4.84 Xerath Target Type Rejection Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.80/P4.81 已锁定《泽拉斯》技能的“一名”目标数量边界，但还缺“一名单位”的目标类型证据；同时 P4 状态文档此前声称 P4.79-P4.83 拒绝 fixtures 进入资源关键词聚合，实际 theory 未覆盖。本批次只补这两个低风险证据缺口。

- `ACTIVATE_ABILITY abilityId = PAY_RED_EXHAUST_DAMAGE_3` 若选择场上装备等非单位对象，会返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付资源、不横置来源，也不创建 stack item。
- 新增直接 engine 测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenTargetIsNotUnit`，用 `SFD·022/221 长剑` 装备对象验证目标类型拒绝边界。
- 新增 fixture `p4-activate-xerath-damage-skill-non-unit-target-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillNonUnitTargetFixture`，把该拒绝边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放 P4.79-P4.84 的《泽拉斯》拒绝 fixtures，避免只依赖单独 fact 和文档备注；本批次没有改变 `CoreRuleEngine` 主实现，也不扩展通用 skill registry。

## P4.85 Xerath Source Zone Rejection Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.77 已执行《泽拉斯》技能正向路径，P4.79-P4.84 已锁定多个相邻拒绝分支；本批次只补官方卡面“自身必须位于战场中才能使用此技能”的来源区域证据。

- `ACTIVATE_ABILITY abilityId = PAY_RED_EXHAUST_DAMAGE_3` 若来源位于基地而非战场，会返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付资源、不横置来源、不伤害目标，也不创建 stack item。
- 新增直接 engine 测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillWhenSourceIsNotBattlefield`，用基地中的 `UNL-026/219 泽拉斯` 验证来源战场位置拒绝边界。
- 新增 fixture `p4-activate-xerath-damage-skill-source-not-battlefield-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillSourceNotBattlefieldFixture`，把该拒绝边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 当时实际回放 P4.79-P4.85 的《泽拉斯》拒绝 fixtures；本批次没有改变 `CoreRuleEngine` 主实现，也不扩展通用 skill registry。

## P4.86 Xerath Opponent Source Rejection Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.77 已执行《泽拉斯》技能正向路径，P4.79-P4.85 已锁定多个相邻拒绝分支；本批次只补“来源必须由命令玩家控制”的来源控制者证据。

- `ACTIVATE_ABILITY abilityId = PAY_RED_EXHAUST_DAMAGE_3` 若来源位于对手战场，会返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付资源、不横置来源、不伤害目标，也不创建 stack item。
- 新增直接 engine 测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillFromOpponentSource`，用对手战场中的 `UNL-026/219 泽拉斯` 验证来源控制者拒绝边界。
- 新增 fixture `p4-activate-xerath-damage-skill-opponent-source-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillOpponentSourceFixture`，把该拒绝边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放 P4.79-P4.86 的《泽拉斯》拒绝 fixtures；本批次没有改变 `CoreRuleEngine` 主实现，也不扩展通用 skill registry。

## P4.87 Vi Opponent Source Rejection Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.73/P4.74 已执行《蔚》技能正向路径和来源身份边界；本批次只补同一 ability id 的来源控制者证据。

- `ACTIVATE_ABILITY abilityId = PAY_2_RED_DOUBLE_POWER` 若来源位于对手基地，会返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付资源、不修改来源战力，也不创建 stack item。
- 新增直接 engine 测试 `P4ActivateAbilityCommandRejectsViDoublePowerSkillFromOpponentSource`，用对手基地中的 `UNL-030/219 蔚` 验证来源控制者拒绝边界。
- 新增 fixture `p4-activate-vi-double-power-skill-opponent-source-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsViDoublePowerSkillOpponentSourceFixture`，把该拒绝边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放《蔚》对手控制来源拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不扩展通用 skill registry。

## P4.88 Vi Source Zone Rejection Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.73/P4.74 已执行《蔚》技能正向路径和来源身份边界，P4.87 已补来源控制者边界；本批次只补同一 ability id 的来源区域证据。

- `ACTIVATE_ABILITY abilityId = PAY_2_RED_DOUBLE_POWER` 若来源仍在手牌、未进入控制者基地或战场，会返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付资源、不修改来源战力，也不创建 stack item。
- 新增直接 engine 测试 `P4ActivateAbilityCommandRejectsViDoublePowerSkillWhenSourceIsNotField`，用 P1 手牌中的 `UNL-030/219 蔚` 验证来源区域拒绝边界。
- 新增 fixture `p4-activate-vi-double-power-skill-source-not-field-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsViDoublePowerSkillSourceNotFieldFixture`，把该拒绝边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放《蔚》来源不在场上拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不扩展通用 skill registry。

## P4.89 Vi Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.73 已有直接 engine 测试覆盖《蔚》无目标技能携带目标时拒绝，本批次只把该边界提升为可回放 fixture。

- `ACTIVATE_ABILITY abilityId = PAY_2_RED_DOUBLE_POWER` 若携带任何目标，会返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付资源、不修改来源或目标，也不创建 stack item。
- 新增 fixture `p4-activate-vi-double-power-skill-target-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsViDoublePowerSkillTargetFixture`，把无目标技能的目标拒绝边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放《蔚》带目标拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不扩展通用 skill registry。

## P4.90 Vi Optional Cost Rejection Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.73/P4.74 已执行《蔚》技能正向路径和来源身份边界，P4.87-P4.89 已补来源控制者、来源区域和带目标拒绝；本批次只补同一 ability id 的 unsupported optional cost 证据。

- `ACTIVATE_ABILITY abilityId = PAY_2_RED_DOUBLE_POWER` 若携带未支持 optional cost，会返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付资源、不修改来源战力，也不创建 stack item。
- 新增直接 engine 测试 `P4ActivateAbilityCommandRejectsViDoublePowerSkillWithOptionalCosts`，用场上的 `UNL-030/219 蔚` 验证额外费用拒绝边界。
- 新增 fixture `p4-activate-vi-double-power-skill-optional-cost-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsViDoublePowerSkillOptionalCostFixture`，把该拒绝边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放《蔚》额外费用拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不扩展通用 skill registry。

## P4.91 Vi Resource Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.73 已有直接 engine 测试覆盖《蔚》无目标技能缺少 power 费用时拒绝，本批次只把该边界提升为可回放 fixture。

- `ACTIVATE_ABILITY abilityId = PAY_2_RED_DOUBLE_POWER` 需要支付 2 mana + 1 power；若来源控制者缺少 power，会返回 `INSUFFICIENT_COST`，不推进 tick、不写事件、不支付 mana、不修改来源战力，也不创建 stack item。
- 新增 fixture `p4-activate-vi-double-power-skill-power-missing-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsViDoublePowerSkillPowerMissingFixture`，把费用不足拒绝边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放《蔚》费用不足拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不扩展通用 skill registry。

## P4.92 Vi Source Identity Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.74 已有直接 engine 测试覆盖非《蔚》来源伪造《蔚》技能 id 时拒绝，本批次只把该边界提升为可回放 fixture。

- `ACTIVATE_ABILITY abilityId = PAY_2_RED_DOUBLE_POWER` 的来源必须拥有《蔚》的对应技能文本；若《呸呸魄罗》等非《蔚》对象伪造同一 ability id，会返回 `UNSUPPORTED_CARD_BEHAVIOR`，不推进 tick、不写事件、不支付资源、不修改来源战力，也不创建 stack item。
- 新增 fixture `p4-activate-vi-double-power-skill-non-vi-source-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsViDoublePowerSkillNonViSourceFixture`，把来源身份拒绝边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放《蔚》来源身份拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不扩展通用 skill registry。

## P4.93 Xerath Spellshield Tax Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.77 已有直接 engine 测试覆盖《泽拉斯》技能选择敌方法盾单位但缺少目标税 mana 时拒绝；本批次只把该费用边界提升为可回放 fixture。

- `ACTIVATE_ABILITY abilityId = PAY_RED_EXHAUST_DAMAGE_3` 选择敌方 `法盾` 单位时需要目标税 mana；若控制者 mana 为 0，即使命中单位目标且拥有 1 power，也返回 `INSUFFICIENT_COST`，不推进 tick、不写事件、不支付 power、不横置来源、不伤害目标，也不创建 stack item。
- 新增 fixture `p4-activate-xerath-damage-skill-spellshield-tax-insufficient-rejected.fixture.json` 和回放测试 `P4ActivateAbilityCommandRejectsXerathDamageSkillSpellshieldTaxInsufficientFixture`，把敌方法盾目标税费用不足边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放该《泽拉斯》法盾税拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不扩展通用 skill registry。

## P4.94 Standby Hide Cost Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.70 已有直接 engine 测试覆盖 `HIDE_CARD destination=STANDBY optionalCosts=["STANDBY_A"]` 在费用不足时拒绝；本批次只把该费用边界提升为可回放 fixture。

- `HIDE_CARD destination=STANDBY optionalCosts=["STANDBY_A"]` 需要支付 1 mana；若控制者 mana 为 0，即使来源手牌是带 `待命` 的 `OGN·121/298 提莫`，也返回 `INSUFFICIENT_COST`，不推进 tick、不写事件、不移动手牌、不创建正面朝下对象，也不创建 stack item。
- 新增 fixture `p4-hide-card-standby-insufficient-cost-rejected.fixture.json` 和回放测试 `P4HideCardCommandRejectsInsufficientStandbyCostFixture`，把待命暗置费用不足边界纳入 conformance 证据。
- `P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该待命费用不足拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入待命触发、完整隐藏区、目标伤害或伏击共用隐藏信息。

## P4.95 Standby Reaction Priority Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.76 已有直接 engine 测试覆盖 `REVEAL_CARD mode=STANDBY_REACTION destination=STACK optionalCosts=["STANDBY_REVEAL_0"]` 在没有优先权窗口时拒绝；本批次只把该时机边界提升为可回放 fixture。

- `REVEAL_CARD mode=STANDBY_REACTION destination=STACK optionalCosts=["STANDBY_REVEAL_0"]` 需要控制者处于优先权窗口且结算链已有待结算项目；若当前是 main/neutral open，则返回 `PHASE_NOT_ALLOWED`，不推进 tick、不写事件、不翻开正面朝下对象、不移动基地对象，也不创建 stack item。
- 新增 fixture `p4-reveal-card-standby-reaction-without-priority-rejected.fixture.json` 和回放测试 `P4RevealCardCommandRejectsReactionPlayWithoutPriorityWindowFixture`，把待命反应时机拒绝边界纳入 conformance 证据。
- `P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该待命反应拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入待命目标结算、完整隐藏区、触发队列或伏击共用隐藏信息。

## P4.96 Free Standby Permission Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.72 已有直接 engine 测试覆盖 `HIDE_CARD destination=STANDBY optionalCosts=["STANDBY_FREE"]` 在没有《游击战》免费暗置权限时拒绝；本批次只把该权限边界提升为可回放 fixture。

- `HIDE_CARD destination=STANDBY optionalCosts=["STANDBY_FREE"]` 需要控制者拥有 `FREE_STANDBY_HIDE:{playerId}` until-end-of-turn 效果；若没有该效果，则返回 `UNSUPPORTED_CARD_BEHAVIOR`，不推进 tick、不写事件、不移动手牌、不创建正面朝下对象，也不创建 stack item。
- 新增 fixture `p4-hide-card-standby-free-without-permission-rejected.fixture.json` 和回放测试 `P4HideCardCommandRejectsFreeStandbyWithoutGuerrillaWarfarePermissionFixture`，把《游击战》免费待命暗置权限拒绝边界纳入 conformance 证据。
- `P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该免费待命暗置拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入待命触发、完整隐藏区或伏击共用隐藏信息。

## P4.97 Ambush Premodel Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.64 已有直接 engine 测试覆盖 `PLAY_CARD mode=AMBUSH destination=BATTLEFIELD:*` 在真实伏击反应战场打出模型完成前被显式拒绝；本批次只把该前置拒绝边界提升为可回放 fixture。

- `PLAY_CARD mode=AMBUSH destination=BATTLEFIELD:P1-MAIN` 当前需要后续反应窗口、战场目的地合法性和单位打出至战场模型；在这些系统未完成前返回 `UNSUPPORTED_COMMAND`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变战场，也不创建 stack item。
- 新增 fixture `p4-ambush-play-card-premodel-rejected.fixture.json` 和回放测试 `P4AmbushPlayCardModeRejectionFixture`，把《阴森药剂师》伏击前置模型拒绝边界纳入 conformance 证据。
- `P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伏击拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入伏击真实反应战场打出、face-down 共用隐藏信息或战场目的地结算。

## P4.98 Move Unit Premodel Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.65 已有直接 engine 测试覆盖 `MOVE_UNIT` 在真实游走/基础移动模型完成前被显式拒绝；本批次只把该前置拒绝边界提升为可回放 fixture。

- `MOVE_UNIT origin=BATTLEFIELD:P1-LEFT destination=BATTLEFIELD:P1-RIGHT optionalCosts=["ROAM"]` 当前需要后续多战场位置、移动权限、游走合法性和移动触发模型；在这些系统未完成前返回 `UNSUPPORTED_COMMAND`，不推进 tick、不写事件、不支付费用、不移动对象、不改变战力，也不创建 stack item。
- 新增 fixture `p4-move-unit-command-premodel-rejected.fixture.json` 和回放测试 `P4MoveUnitCommandRejectionFixture`，把《亚索》游走前置模型拒绝边界纳入 conformance 证据。
- `P4CombatKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 与 `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该移动拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入真实多战场移动、游走权限或移动触发得分。

## P4.99 Assemble Equipment Premodel Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.66 已有直接 engine 测试覆盖 `ASSEMBLE_EQUIPMENT` 在真实装备装配/贴附模型完成前被显式拒绝；本批次只把该前置拒绝边界提升为可回放 fixture。

- `ASSEMBLE_EQUIPMENT sourceObjectId=P1-EQUIPMENT-LONG-SWORD targetObjectId=P1-UNIT-ASSEMBLE-TARGET optionalCosts=["ASSEMBLE_RED"]` 当前需要后续装配费用、灵便自动贴附、百炼 optional attach、owner/controller 和装备未激活文本模型；在这些系统未完成前返回 `UNSUPPORTED_COMMAND`，不推进 tick、不写事件、不支付费用、不移动装备、不设置 `attachedToObjectId`，也不创建 stack item。
- 新增 fixture `p4-assemble-equipment-command-premodel-rejected.fixture.json` 和回放测试 `P4AssembleEquipmentCommandRejectionFixture`，把《长剑》装配/灵便前置模型拒绝边界纳入 conformance 证据。
- `P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen` 现在实际回放该装备拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入真实装备贴附、未激活文本、owner/controller 或灵便/百炼可选贴附。

## P4.100 Declare Battle Premodel Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。P4.67 已有直接 engine 测试覆盖 `DECLARE_BATTLE` 在真实开战/承伤模型完成前被显式拒绝；本批次只把该前置拒绝边界提升为可回放 fixture。

- `DECLARE_BATTLE battlefieldId=BATTLEFIELD:P1-MAIN attackerObjectIds=["P1-BATTLEFIELD-GAREN"] defenderObjectIds=["P2-BATTLEFIELD-MUTANT-KITTEN"] optionalCosts=["COMBAT_ASSIGNMENT"]` 当前需要后续多战场位置、进攻/防守声明、强攻/坚守修正、壁垒/后排承伤顺序和战斗结算模型；在这些系统未完成前返回 `UNSUPPORTED_COMMAND`，不推进 tick、不写事件、不设置攻防状态、不改变战力、不移动对象，也不创建 stack item。
- 新增 fixture `p4-declare-battle-command-premodel-rejected.fixture.json` 和回放测试 `P4DeclareBattleCommandRejectionFixture`，把《盖伦》/《变异猫咪》战斗声明前置模型拒绝边界纳入 conformance 证据。
- `P4CombatKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放该战斗拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入真实战斗、强攻/坚守修正、壁垒/后排承伤或多战场开战。

## P4.101 Predict Outside Top Card Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《占卜贝壳》`预知` 选择非顶部主牌堆牌时拒绝；本批次只把该目标合法性边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SCRYING-SHELL cardNo=UNL-161/219 targetObjectIds=["P1-SCRYING-SHELL-SECOND-001"]` 当前违反 `预知` 只查看并可选择顶部一张主牌堆牌的边界；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌或主牌堆，也不创建 stack item。
- 新增 fixture `p4-scrying-shell-predict-outside-top-card-rejected.fixture.json` 和回放测试 `P4ScryingShellPredictOutsideTopCardRejectionFixture`，把《占卜贝壳》预知非顶部目标拒绝边界纳入 conformance 证据。
- `P4LifecycleKeywordProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该预知拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入广义预知授予、隐藏信息 UI 或绝念触发队列。

## P4.102 Guerrilla Warfare Non-Standby Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《游击战》选择废牌堆非待命目标时拒绝；本批次只把该目标标签边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-GUERRILLA-WARFARE cardNo=OGN·264/298 targetObjectIds=["P1-GRAVE-NON-STANDBY-001"]` 当前违反《游击战》只能返回己方废牌堆最多两张 `待命` 卡牌的边界；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌或废牌堆，也不创建 stack item。
- 新增 fixture `p4-guerrilla-warfare-non-standby-target-rejected.fixture.json` 和回放测试 `P4GuerrillaWarfareNonStandbyTargetRejectionFixture`，把《游击战》非待命废牌堆目标拒绝边界纳入 conformance 证据。
- `P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该《游击战》拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入待命触发、完整隐藏区、目标伤害或伏击共用隐藏信息。

## P4.103 Haste Ready Insufficient Power Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《灼焰飞龙》`HASTE_READY` 缺少 power 时拒绝；本批次只把该费用边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-UNIT-BLAZING-DRAKE cardNo=OGN·001/298 optionalCosts=["HASTE_READY"]` 当前需要基础 5 mana 加急速代表额外 1 mana + 1 power；P1 只有 6 mana、0 power 时返回 `INSUFFICIENT_COST`，不推进 tick、不写事件、不移动手牌、不改变符文池，也不创建 stack item。
- 新增 fixture `p4-play-blazing-drake-haste-ready-insufficient-power-rejected.fixture.json` 和回放测试 `P4HasteOptionalReadyBranchRejectsInsufficientPowerFixture`，把《灼焰飞龙》急速活跃分支 power 不足拒绝边界纳入 conformance 证据。
- `P4PermissionKeywordsKeepExistingP2FixturesGreen` 现在实际回放该急速拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入彩色资源精确匹配、授予急速、战场目的地或战斗/游走联动。

## P4.104 Incinerate Spellshield Tax Insufficient Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《焚烧》选择敌方 `法盾` 单位但无法支付目标税时拒绝；本批次只把该费用边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-INCINERATE cardNo=OGS·003/024 targetObjectIds=["P2-SPELLSHIELD-UNIT-001"]` 当前需要基础 2 mana 加 1 mana 法盾目标税；P1 只有 2 mana 时返回 `INSUFFICIENT_COST`，不推进 tick、不写事件、不移动手牌、不改变符文池/目标伤害，也不创建 stack item。
- 新增 fixture `p4-play-incinerate-spellshield-tax-insufficient-rejected.fixture.json` 和回放测试 `P4IncinerateSpellshieldTaxInsufficientFixture`，把《焚烧》敌方法盾目标税费用不足拒绝边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放该法盾拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入更多技能目标税、授予/静态法盾或完整 FAQ 细节。

## P4.105 Spirit Fire Multiple Spellshield Tax Insufficient Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《妖异狐火》同时选择敌方 `法盾` 与 `法盾2` 单位但无法支付聚合目标税时拒绝；本批次只把该费用边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-SPIRIT-FIRE cardNo=OGN·256/298 targetObjectIds=["P2-SPIRIT-FIRE-SPELLSHIELD-001","P2-SPIRIT-FIRE-SPELLSHIELD2-001"]` 当前需要基础 3 mana 加 3 mana 法盾目标税；P1 只有 5 mana 时返回 `INSUFFICIENT_COST`，不推进 tick、不写事件、不移动手牌、不改变符文池/目标区域/目标伤害，也不创建 stack item。
- 新增 fixture `p4-play-spirit-fire-multiple-spellshield-tax-insufficient-rejected.fixture.json` 和回放测试 `P4MultipleSpellshieldTaxInsufficientFixture`，把《妖异狐火》多目标法盾税费用不足拒绝边界纳入 conformance 证据。
- `P4ResourceKeywordProfilesKeepExistingKeywordUnitFixturesGreen` 现在实际回放该多目标法盾拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入更多技能目标税、授予/静态法盾或完整 FAQ 细节。

## P4.106 Spirit Fire Total Power Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《妖异狐火》选择目标总战力超过 4 时拒绝；本批次只把该目标合法性边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-SPIRIT-FIRE cardNo=OGN·256/298 targetObjectIds=["P2-SPIRIT-FIRE-UNIT-001","P2-SPIRIT-FIRE-UNIT-002"]` 当前选择 2 战力与 3 战力战场单位，总战力为 5，超过卡面“不得高于 4”的上限；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变目标区域或对象状态，也不创建 stack item。
- 新增 fixture `p4-play-spirit-fire-total-power-too-high-rejected.fixture.json` 和回放测试 `P4SpiritFireTotalPowerTooHighFixture`，把《妖异狐火》多目标摧毁的总战力上限拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该摧毁目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入多战场位置、目标重选或更复杂摧毁替代边界。

## P4.107 Playful Tentacles Total Power Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《顽皮触手》选择敌方战场单位总战力超过 8 时拒绝；本批次只把该移动目标合法性边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-PLAYFUL-TENTACLES cardNo=UNL-054/219 targetObjectIds=["P2-PLAYFUL-TENTACLES-UNIT-001","P2-PLAYFUL-TENTACLES-UNIT-002"]` 当前选择 4 战力与 5 战力敌方战场单位，总战力为 9，超过卡面“不得高于 8”的上限；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变目标区域或对象状态，也不创建 stack item。
- 新增 fixture `p4-play-playful-tentacles-total-power-too-high-rejected.fixture.json` 和回放测试 `P4PlayfulTentaclesTotalPowerTooHighFixture`，把《顽皮触手》多目标移动的总战力上限拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该移动目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入多战场位置、同一位置目的地、多控制者选择或真实 `MOVE_UNIT` 执行。

## P4.108 Hunt the Weak Target Power Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《狩魂》选择 4 战力战场单位时拒绝；本批次只把该摧毁目标合法性边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-HUNT-THE-WEAK cardNo=UNL-159/219 targetObjectIds=["P2-UNIT-001"]` 当前选择 4 战力战场单位，超过卡面“不高于 3 战力”的目标上限；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变目标区域或对象状态，也不创建 stack item。
- 新增 fixture `p4-play-hunt-the-weak-target-power-too-high-rejected.fixture.json` 和回放测试 `P4HuntTheWeakTargetPowerTooHighFixture`，把《狩魂》单目标摧毁的战力上限拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该摧毁目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入复杂摧毁替代、目标重选或 P6 全卡迁移。

## P4.109 Gust Target Power Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《罡风》选择 4 战力战场单位时拒绝；本批次只把该召回目标合法性边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-GUST cardNo=OGN·169/298 targetObjectIds=["P2-UNIT-001"]` 当前选择 4 战力战场单位，超过卡面“不高于 3 战力”的目标上限；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变目标区域或对象状态，也不创建 stack item。
- 新增 fixture `p4-play-gust-target-power-too-high-rejected.fixture.json` 和回放测试 `P4GustTargetPowerTooHighFixture`，把《罡风》单目标回手的战力上限拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该召回目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入完整目的地选择、隐藏信息或复杂控制权/所属者模型。

## P4.110 Highway Robbery Off-Field Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《巧取豪夺》选择友方单位或离场敌方单位牌时拒绝；本批次只把离场敌方目标边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-HIGHWAY-ROBBERY cardNo=OGN·033/298 targetObjectIds=["P2-GRAVEYARD-UNIT-001"]` 当前选择敌方废牌堆中的单位牌，不满足卡面“一名敌方单位”的场上目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变废牌堆或目标对象状态，也不创建 stack item。
- 新增 fixture `p4-play-highway-robbery-off-field-target-rejected.fixture.json` 和回放测试 `P4HighwayRobberyOffFieldTargetRejectedFixture`，把《巧取豪夺》伤害/抽牌分支共享的离场目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入目标控制者交互 UI、复杂目标重选或 P6 全卡迁移。

## P4.111 Deadly Flourish Friendly Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《致命华彩》选择友方单位时拒绝；本批次只把该伤害目标合法性边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-DEADLY-FLOURISH cardNo=UNL-073/219 targetObjectIds=["P1-FRIENDLY-DEADLY-FLOURISH-001"]` 当前选择友方基地单位，不满足卡面“一名敌方单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变基地或目标对象状态，也不创建 stack item。
- 新增 fixture `p4-play-deadly-flourish-friendly-target-rejected.fixture.json` 和回放测试 `P4DeadlyFlourishFriendlyTargetRejectedFixture`，把《致命华彩》单目标伤害的友方目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入金币装备指示物、延迟触发或 P6 全卡迁移。

## P4.112 Blood Money Target Power Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《血钱》选择 3 战力战场单位时拒绝；本批次只把该摧毁目标战力上限边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-BLOOD-MONEY cardNo=SFD·162/221 targetObjectIds=["P2-BATTLEFIELD-BLOOD-MONEY-LARGE-001"]` 当前选择 3 战力战场单位，超过卡面“不高于 2 战力”的目标上限；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变战场或目标对象状态，也不创建 stack item。
- 新增 fixture `p4-play-blood-money-target-power-too-high-rejected.fixture.json` 和回放测试 `P4BloodMoneyTargetPowerTooHighRejectedFixture`，把《血钱》单目标摧毁与金币创建前的目标上限拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该摧毁目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入金币资源技能、装备指示物扩展或 P6 全卡迁移。

## P4.113 Punishment Base Unit Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《惩戒》选择基地单位时拒绝；本批次只把该战场限定目标边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-PUNISHMENT cardNo=UNL-007/219 targetObjectIds=["P2-BASE-UNIT-001"]` 当前选择基地单位，不满足卡面“战场上的一名单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变基地或目标对象状态，也不创建 stack item。
- 新增 fixture `p4-play-punishment-base-unit-target-rejected.fixture.json` 和回放测试 `P4PunishmentBaseUnitTargetRejectedFixture`，把《惩戒》单目标伤害与本回合放逐替代效果前的目标区域拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入目标重选、替代效果扩展或 P6 全卡迁移。

## P4.114 Kerplunk Non-Attacking Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《扑咚！》选择非进攻方单位时拒绝；本批次只把该眩晕目标合法性边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-KERPLUNK cardNo=SFD·040/221 targetObjectIds=["P2-UNIT-001"]` 当前选择未在进攻的战场单位，不满足卡面“一名进攻方单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变战场或目标对象状态，也不创建 stack item。
- 新增 fixture `p4-play-kerplunk-non-attacking-target-rejected.fixture.json` 和回放测试 `P4KerplunkNonAttackingTargetRejectedFixture`，把《扑咚！》单目标眩晕和回响重复前的目标状态拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该眩晕目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入完整战斗声明、进攻状态生成或 P6 全卡迁移。

## P4.115 Zenith Blade Base Unit Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《天顶之刃》选择敌方基地单位时拒绝；本批次只把该战场限定眩晕目标边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-ZENITH-BLADE cardNo=OGN·262/298 targetObjectIds=["P2-BASE-UNIT-001"]` 当前选择敌方基地单位，不满足卡面“战场上的一名敌方单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变基地/战场或目标对象状态，也不创建 stack item。
- 新增 fixture `p4-play-zenith-blade-base-unit-target-rejected.fixture.json` 和回放测试 `P4ZenithBladeBaseUnitTargetRejectedFixture`，把《天顶之刃》单目标眩晕与可选移动分支前的目标区域拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该眩晕目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入可选移动、完整战场位置或 P6 全卡迁移。

## P4.116 Zenith Blade Friendly Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《天顶之刃》选择友方单位时拒绝；本批次只把该敌我归属目标边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-ZENITH-BLADE cardNo=OGN·262/298 targetObjectIds=["P1-FRIENDLY-UNIT-001"]` 当前选择友方战场单位，不满足卡面“一名敌方单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不改变战场或目标对象状态，也不创建 stack item。
- 新增 fixture `p4-play-zenith-blade-friendly-target-rejected.fixture.json` 和回放测试 `P4ZenithBladeFriendlyTargetRejectedFixture`，把《天顶之刃》单目标眩晕与可选移动分支前的目标控制者拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该眩晕目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入可选移动、完整战场位置或 P6 全卡迁移。

## P4.117 Last Breath Enemy Base Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《狂风绝息斩》第二目标选择敌方基地单位时拒绝；本批次只把该战场限定伤害目标边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-LAST-BREATH cardNo=OGN·260/298 targetObjectIds=["P1-FRIENDLY-LAST-BREATH-001","P2-BASE-LAST-BREATH-001"]` 当前第二目标选择敌方基地单位，不满足卡面“战场上的一名敌方单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不让友方目标变为活跃、不伤害敌方目标，也不创建 stack item。
- 新增 fixture `p4-play-last-breath-enemy-base-target-rejected.fixture.json` 和回放测试 `P4LastBreathEnemyBaseTargetRejectedFixture`，把《狂风绝息斩》重置友方单位与按友方战力造成伤害前的敌方目标区域拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入完整战斗、目标顺序泛化或 P6 全卡迁移。

## P4.118 Last Breath Target Order Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《狂风绝息斩》目标顺序错误时拒绝；本批次只把该复合目标顺序边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-LAST-BREATH cardNo=OGN·260/298 targetObjectIds=["P2-BATTLEFIELD-LAST-BREATH-001","P1-FRIENDLY-LAST-BREATH-001"]` 当前先选择敌方战场单位、再选择友方单位，不满足卡面“让一名友方单位变为活跃状态，并对……敌方单位造成伤害”的目标顺序；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不让友方目标变为活跃、不伤害敌方目标，也不创建 stack item。
- 新增 fixture `p4-play-last-breath-target-order-rejected.fixture.json` 和回放测试 `P4LastBreathTargetOrderRejectedFixture`，把《狂风绝息斩》友方重置目标必须先于敌方伤害目标的顺序边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入完整战斗、自动目标重排或 P6 全卡迁移。

## P4.119 Convergent Mutation Enemy Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《聚合变异》第二目标选择敌方单位或重复目标时拒绝；本批次只把敌方第二目标边界提升为可回放 fixture，重复目标仍留在直接测试中。

- `PLAY_CARD sourceObjectId=P1-SPELL-CONVERGENT-MUTATION cardNo=OGN·108/298 targetObjectIds=["P1-CONVERGENT-FRIENDLY-001","P2-CONVERGENT-ENEMY-001"]` 当前第二目标选择敌方单位，不满足卡面“另一名友方单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不修改任一目标战力，也不创建 stack item。
- 新增 fixture `p4-play-convergent-mutation-enemy-target-rejected.fixture.json` 和回放测试 `P4ConvergentMutationEnemyTargetRejectedFixture`，把《聚合变异》比较目标必须为友方单位的边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该临时战力目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入重复目标 fixture、复杂目标 UI 或 P6 全卡迁移。

## P4.120 Convergent Mutation Duplicate Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《聚合变异》重复选择同一友方单位时拒绝；本批次只把该“另一名友方单位”边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-CONVERGENT-MUTATION cardNo=OGN·108/298 targetObjectIds=["P1-CONVERGENT-FRIENDLY-001","P1-CONVERGENT-FRIENDLY-001"]` 当前两次选择同一名友方单位，不满足卡面“另一名友方单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不修改任一目标战力，也不创建 stack item。
- 新增 fixture `p4-play-convergent-mutation-duplicate-target-rejected.fixture.json` 和回放测试 `P4ConvergentMutationDuplicateTargetRejectedFixture`，把《聚合变异》两个目标必须为不同友方单位的边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该临时战力目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入复杂目标 UI 或 P6 全卡迁移。

## P4.121 Existential Dread Friendly Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《存在焦虑》选择正在进攻的友方单位时拒绝；本批次只把该敌我归属目标边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-EXISTENTIAL-DREAD cardNo=UNL-134/219 targetObjectIds=["P1-BATTLEFIELD-UNIT-001"]` 当前目标是正在进攻的友方单位，不满足卡面“正在进攻的敌方单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不眩晕目标，也不创建 stack item。
- 新增 fixture `p4-play-existential-dread-friendly-attacking-target-rejected.fixture.json` 和回放测试 `P4ExistentialDreadFriendlyAttackingTargetRejectedFixture`，把《存在焦虑》目标必须是敌方进攻单位的边界纳入 conformance 证据。
- `P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该互动关键词目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入完整战斗或 P6 全卡迁移。

## P4.122 Thundering Sky Insufficient Cost Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《霹天雳地》费用不足时拒绝；本批次只把该费用合法性边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-THUNDERING-SKY cardNo=OGN·014/298 targetObjectIds=["P2-UNIT-001"]` 当前 P1 只有 5 点法力且没有控制单位降低费用，无法支付卡面计算后的所需费用；命令返回 `INSUFFICIENT_COST`，不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标，也不创建 stack item。
- 新增 fixture `p4-play-thundering-sky-insufficient-reduced-cost-rejected.fixture.json` 和回放测试 `P4ThunderingSkyInsufficientReducedCostRejectedFixture`，把《霹天雳地》费用降低模型下的费用不足边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害法术费用拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入资源颜色精确匹配或 P6 全卡迁移。

## P4.123 Mind And Balance Insufficient Cost Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《御衡守念》在对手距离胜利得分超过 3 分、无法获得费用减少且费用不足时拒绝；本批次只把该费用条件边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-MIND-AND-BALANCE cardNo=OGN·047/298 targetObjectIds=[]` 当前 P2 距离胜利得分超过 3 分，P1 只有 1 点法力，无法支付未降低的 3 点费用；命令返回 `INSUFFICIENT_COST`，不推进 tick、不写事件、不支付费用、不移动手牌、不抽牌、不召出符文，也不创建 stack item。
- 新增 fixture `p4-play-mind-and-balance-insufficient-unreduced-cost-rejected.fixture.json` 和回放测试 `P4MindAndBalanceInsufficientUnreducedCostRejectedFixture`，把《御衡守念》对手得分条件减费模型下的费用不足边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该抽牌/召出符文法术费用拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入更多动态费用分支或 P6 全卡迁移。

## P4.124 Piercing Light Repeated Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《透体圣光》两次选择同一目标时拒绝；本批次只把该“另一名单位”目标约束提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-PIERCING-LIGHT cardNo=SFD·023/221 targetObjectIds=["P2-UNIT-001","P2-UNIT-001"]` 当前第二目标重复第一目标，不满足卡面“最多另一名单位”的约束；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标，也不创建 stack item。
- 新增 fixture `p4-play-piercing-light-repeated-target-rejected.fixture.json` 和回放测试 `P4PiercingLightRepeatedTargetRejectedFixture`，把《透体圣光》1-2 个不同战场单位目标约束纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害法术目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入有色回响费用或 P6 全卡迁移。

## P4.125 Bellows Breath Fourth Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《风箱炎息》重复目标和第四目标时拒绝；本批次只把“最多三名单位”的目标数量上限提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-BELLOWS-BREATH cardNo=SFD·080/221 targetObjectIds=["P1-UNIT-001","P1-UNIT-002","P2-UNIT-001","P2-UNIT-002"]` 当前选择四名单位，超过卡面“最多三名单位”的上限；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标，也不创建 stack item。
- 新增 fixture `p4-play-bellows-breath-fourth-target-rejected.fixture.json` 和回放测试 `P4BellowsBreathFourthTargetRejectedFixture`，把《风箱炎息》1-3 个单位目标数量上限纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害法术目标数量拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入同一位置精确筛选、有色回响费用或 P6 全卡迁移。

## P4.126 Firestorm Explicit Unit Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《烈火风暴》携带显式单位目标时拒绝；本批次只把当前已审计的 0 目标范围伤害契约提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-FIRESTORM cardNo=OGS·002/024 targetObjectIds=["P2-UNIT-001"]` 当前提供显式单位目标，不满足卡面“对一处战场的所有敌方单位各造成3点伤害”在单战场 preflight 中的 0 目标范围伤害契约；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标，也不创建 stack item。
- 新增 fixture `p4-play-firestorm-explicit-unit-target-rejected.fixture.json` 和回放测试 `P4FirestormExplicitUnitTargetRejectedFixture`，把《烈火风暴》显式单位目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该范围伤害法术目标契约拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，也不进入多战场位置选择或 P6 全卡迁移。

## P4.127 Crescent Strike Friendly Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《新月打击》选择友方单位或基地单位时拒绝；本批次只把友方战场单位目标边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-CRESCENT-STRIKE cardNo=UNL-072/219 targetObjectIds=["P1-FRIENDLY-UNIT-001"]` 当前选择友方战场单位，不满足卡面“该处的一名敌方单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标，也不创建 stack item。
- 新增 fixture `p4-play-crescent-strike-friendly-target-rejected.fixture.json` 和回放测试 `P4CrescentStrikeFriendlyTargetRejectedFixture`，把《新月打击》友方目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害法术目标阵营拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，基地目标拒绝已在 P4.130 提升为 fixture，多战场位置选择和 P6 全卡迁移仍 deferred。

## P4.128 Switcheroo Duplicate Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《换换乐》重复目标和基地单位目标时拒绝；本批次只把重复选择同一战场单位的目标边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-SWITCHEROO cardNo=SFD·145/221 targetObjectIds=["P1-BATTLEFIELD-UNIT-001","P1-BATTLEFIELD-UNIT-001"]` 当前重复选择同一名战场单位，不满足卡面“两名单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不修改战力，也不创建 stack item。
- 新增 fixture `p4-play-switcheroo-duplicate-target-rejected.fixture.json` 和回放测试 `P4SwitcherooDuplicateTargetRejectedFixture`，把《换换乐》重复目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该临时战力互换法术目标唯一性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，基地目标拒绝已在 P4.129 提升为 fixture，多战场同一位置选择和 P6 全卡迁移仍 deferred。

## P4.129 Switcheroo Base Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《换换乐》基地单位目标时拒绝；本批次只把该目标区域边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-SWITCHEROO cardNo=SFD·145/221 targetObjectIds=["P1-BASE-UNIT-001","P2-BATTLEFIELD-UNIT-001"]` 当前选择一名基地单位和一名战场单位，不满足卡面“同一处战场上的两名单位”的目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不修改战力，也不创建 stack item。
- 新增 fixture `p4-play-switcheroo-base-target-rejected.fixture.json` 和回放测试 `P4SwitcherooBaseTargetRejectedFixture`，把《换换乐》基地目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该临时战力互换法术目标区域拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，多战场同一位置选择和 P6 全卡迁移仍 deferred。

## P4.130 Crescent Strike Base Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《新月打击》选择敌方基地单位时拒绝；本批次只把该目标区域边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-CRESCENT-STRIKE cardNo=UNL-072/219 targetObjectIds=["P2-BASE-UNIT-001"]` 当前选择敌方基地单位，不满足卡面“该处的一名敌方单位”中战场位置目标要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标，也不创建 stack item。
- 新增 fixture `p4-play-crescent-strike-base-target-rejected.fixture.json` 和回放测试 `P4CrescentStrikeBaseTargetRejectedFixture`，把《新月打击》敌方基地目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害法术目标区域拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，多战场位置选择和 P6 全卡迁移仍 deferred。

## P4.131 Bellows Breath Repeated Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《风箱炎息》重复选择同一单位时拒绝；本批次只把该目标唯一性边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-BELLOWS-BREATH cardNo=SFD·080/221 targetObjectIds=["P1-UNIT-001","P1-UNIT-001"]` 当前重复选择同一名单位，不满足卡面“最多三名单位”的不同目标契约；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标，也不创建 stack item。
- 新增 fixture `p4-play-bellows-breath-repeated-target-rejected.fixture.json` 和回放测试 `P4BellowsBreathRepeatedTargetRejectedFixture`，把《风箱炎息》重复目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害法术目标唯一性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，同一位置精确约束、有色回响和 P6 全卡迁移仍 deferred。

## P4.132 Center Stage Echo Cost Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《台前作秀》选择支付 `ECHO` 但 mana 不足时拒绝；本批次只把该回响费用边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-CENTER-STAGE cardNo=UNL-061/219 optionalCosts=["ECHO"]` 在 P1 只有 3 mana 时无法支付基础 2 + 回响 2 的总费用；命令返回 `INSUFFICIENT_COST`，不推进 tick、不写事件、不支付费用、不移动手牌、不抽牌，也不创建 stack item。
- 新增 fixture `p4-play-center-stage-echo-insufficient-mana-rejected.fixture.json` 和回放测试 `P4CenterStageEchoInsufficientManaRejectedFixture`，把《台前作秀》回响费用不足拒绝边界纳入 conformance 证据。
- `P4EchoKeywordKeepsExistingP2FixturesGreen` 和 `P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该回响费用拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，有色/弃牌/授予回响和模式重复分支仍 deferred。

## P4.133 Punishment Illegal Echo Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖非回响法术《惩戒》携带 `ECHO` optional cost 时拒绝；本批次只把该回响可用性边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-PUNISHMENT cardNo=UNL-007/219 targetObjectIds=["P2-UNIT-001"] optionalCosts=["ECHO"]` 试图给没有回响的《惩戒》支付 `ECHO`；命令返回 `UNSUPPORTED_CARD_BEHAVIOR`，不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标，也不创建 stack item。
- 新增 fixture `p4-play-punishment-illegal-echo-rejected.fixture.json` 和回放测试 `P4PunishmentIllegalEchoRejectedFixture`，把非回响法术非法 `ECHO` optional cost 拒绝边界纳入 conformance 证据。
- `P4EchoKeywordKeepsExistingP2FixturesGreen` 和 `P4InteractionKeywordProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该非法回响拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，有色/弃牌/授予回响和模式重复分支仍 deferred。

## P4.134 Rocket Barrage Missing Mode Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《火箭轰击》缺失模式时拒绝；本批次只把该模式选择边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-ROCKET-BARRAGE cardNo=SFD·077/221 targetObjectIds=["P2-BASE-UNIT-001"]` 未提供 `mode`，不满足卡面“选择一个效果”的模式选择要求；命令返回 `UNSUPPORTED_CARD_BEHAVIOR`，不推进 tick、不写事件、不支付费用、不移动手牌、不伤害目标，也不创建 stack item。
- 新增 fixture `p4-play-rocket-barrage-missing-mode-rejected.fixture.json` 和回放测试 `P4RocketBarrageMissingModeRejectedFixture`，把《火箭轰击》缺失模式拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该模式选择拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，回响分支仍 deferred。

## P4.135 Rocket Barrage Destroy Equipment Unit Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《火箭轰击》选择摧毁装备模式但指定单位目标时拒绝；本批次只把该目标类型边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-ROCKET-BARRAGE cardNo=SFD·077/221 mode=DESTROY_EQUIPMENT targetObjectIds=["P2-BASE-ROCKET-BARRAGE-UNIT-001"]` 当前目标是 `CARD_TYPE:UNIT` 单位，不满足卡面“摧毁一件装备”的目标类型要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不摧毁目标，也不创建 stack item。
- 新增 fixture `p4-play-rocket-barrage-destroy-equipment-unit-target-rejected.fixture.json` 和回放测试 `P4RocketBarrageDestroyEquipmentUnitTargetRejectedFixture`，把《火箭轰击》摧毁装备模式指定单位目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该摧毁目标类型拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，回响分支仍 deferred。

## P4.136 Emergency Recall Unit Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《紧急召回》指定单位目标时拒绝；本批次只把该召回目标类型边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-EMERGENCY-RECALL cardNo=SFD·135/221 targetObjectIds=["P2-BASE-EMERGENCY-RECALL-UNIT-001"]` 当前目标是 `CARD_TYPE:UNIT` 单位，不满足卡面“让一件装备返回其所属的手牌”的目标类型要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不返回目标，也不创建 stack item。
- 新增 fixture `p4-play-emergency-recall-unit-target-rejected.fixture.json` 和回放测试 `P4EmergencyRecallUnitTargetRejectedFixture`，把《紧急召回》指定单位目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该召回目标类型拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装备贴附/卸除细节仍 deferred。

## P4.137 Poro Snax Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《魄罗佳肴》带目标打出时拒绝；本批次只把该 0 目标装备打出边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-PORO-SNAX cardNo=SFD·046/221 targetObjectIds=["P2-PORO-SNAX-TARGET-001"]` 为《魄罗佳肴》的 0 目标抽牌装备打出路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不抽牌，也不创建 stack item。
- 新增 fixture `p4-play-poro-snax-target-rejected.fixture.json` 和回放测试 `P4PoroSnaxTargetRejectedFixture`，把《魄罗佳肴》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，自毁激活抽牌技能仍 deferred。

## P4.138 Shurelya's Requiem Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《舒瑞娅的安魂曲》带目标打出时拒绝；本批次只把该 0 目标专属装备打出边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SHURELYAS-REQUIEM cardNo=SFD·192/221 targetObjectIds=["P1-SHURELYA-BASE-UNIT-001"]` 为《舒瑞娅的安魂曲》的 0 目标装备入场并活跃己方单位路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不活跃单位，也不创建 stack item。
- 新增 fixture `p4-play-shurelyas-requiem-target-rejected.fixture.json` 和回放测试 `P4ShurelyasRequiemTargetRejectedFixture`，把《舒瑞娅的安魂曲》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标专属装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，唯我和装配技能仍 deferred。

## P4.139 Future Forge Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《未来熔炉》带目标打出时拒绝；本批次只把该 0 目标装备打出边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-FUTURE-FORGE cardNo=OGN·212/298 targetObjectIds=["P1-FUTURE-FORGE-BASE-UNIT-001"]` 为《未来熔炉》的 0 目标装备入场并创建随从路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建随从，也不创建 stack item。
- 新增 fixture `p4-play-future-forge-target-rejected.fixture.json` 和回放测试 `P4FutureForgeTargetRejectedFixture`，把《未来熔炉》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，摧毁装备回收废牌堆分支仍 deferred。

## P4.140 Scrap Heap Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《废料堆》带目标打出时拒绝；本批次只把该 0 目标装备抽牌打出边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SCRAP-HEAP cardNo=OGN·182/298 targetObjectIds=["P1-SCRAP-HEAP-BASE-UNIT-001"]` 为《废料堆》的 0 目标装备入场并抽牌路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不抽牌，也不创建 stack item。
- 新增 fixture `p4-play-scrap-heap-target-rejected.fixture.json` 和回放测试 `P4ScrapHeapTargetRejectedFixture`，把《废料堆》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，弃置和摧毁触发抽牌分支仍 deferred。

## P4.141 Sprite Lantern Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《精灵提灯》带目标打出时拒绝；本批次只把该 0 目标瞬息装备创建精灵边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SPRITE-LANTERN cardNo=UNL-078/219 targetObjectIds=["P1-SPRITE-LANTERN-BASE-UNIT-001"]` 为《精灵提灯》的 0 目标装备入场并创建瞬息精灵路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不创建精灵，也不创建 stack item。
- 新增 fixture `p4-play-sprite-lantern-target-rejected.fixture.json` 和回放测试 `P4SpriteLanternTargetRejectedFixture`，把《精灵提灯》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标瞬息装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，绝念和开始阶段瞬息摧毁仍 deferred。

## P4.142 Sumpworks Map Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《地沟区地图》带目标打出时拒绝；本批次只把该 0 目标瞬息装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SUMPWORKS-MAP cardNo=UNL-085/219 targetObjectIds=["P1-SUMPWORKS-MAP-BASE-UNIT-001"]` 为《地沟区地图》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-sumpworks-map-target-rejected.fixture.json` 和回放测试 `P4SumpworksMapTargetRejectedFixture`，把《地沟区地图》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标瞬息装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，对手得分触发抽牌和开始阶段瞬息摧毁仍 deferred。

## P4.143 Scrying Blossom Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《占卜花朵》带目标打出时拒绝；本批次只把该 0 目标休眠装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SCRYING-BLOSSOM cardNo=UNL-136/219 targetObjectIds=["P1-SCRYING-BLOSSOM-BASE-UNIT-001"]` 为《占卜花朵》的 0 目标休眠装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-scrying-blossom-target-rejected.fixture.json` 和回放测试 `P4ScryingBlossomTargetRejectedFixture`，把《占卜花朵》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标休眠装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，洞察/抽牌/经验激活技能仍 deferred。

## P4.144 Magic Beans Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《魔法鲜豆》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-MAGIC-BEANS cardNo=UNL-011/219 targetObjectIds=["P1-MAGIC-BEANS-BASE-UNIT-001"]` 为《魔法鲜豆》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-magic-beans-target-rejected.fixture.json` 和回放测试 `P4MagicBeansTargetRejectedFixture`，把《魔法鲜豆》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，法术对决期间单位打出触发抽牌仍 deferred。

## P4.145 Steel Ballista Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《钢铁弩炮》带目标打出时拒绝；本批次只把该 0 目标休眠装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-STEEL-BALLISTA cardNo=OGN·017/298 targetObjectIds=["P1-STEEL-BALLISTA-BASE-UNIT-001"]` 为《钢铁弩炮》的 0 目标休眠装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-steel-ballista-target-rejected.fixture.json` 和回放测试 `P4SteelBallistaTargetRejectedFixture`，把《钢铁弩炮》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标休眠装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置伤害技能仍 deferred。

## P4.146 Heart of Ice Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《玄冰之心》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-HEART-OF-ICE cardNo=SFD·052/221 targetObjectIds=["P1-HEART-OF-ICE-BASE-UNIT-001"]` 为《玄冰之心》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-heart-of-ice-target-rejected.fixture.json` 和回放测试 `P4HeartOfIceTargetRejectedFixture`，把《玄冰之心》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置战力修正技能仍 deferred。

## P4.147 Remorse Orb Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《懊悔法球》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-REMORSE-ORB cardNo=OGN·090/298 targetObjectIds=["P1-REMORSE-ORB-BASE-UNIT-001"]` 为《懊悔法球》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-remorse-orb-target-rejected.fixture.json` 和回放测试 `P4RemorseOrbTargetRejectedFixture`，把《懊悔法球》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置负战力修正技能仍 deferred。

## P4.148 Soul Sword Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《灵魂之剑》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SOUL-SWORD cardNo=UNL-039/219 targetObjectIds=["P1-SOUL-SWORD-BASE-UNIT-001"]` 为《灵魂之剑》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-soul-sword-target-rejected.fixture.json` 和回放测试 `P4SoulSwordTargetRejectedFixture`，把《灵魂之剑》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配绿色贴附分支仍 deferred。

## P4.149 Jagged Dirk Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《锯齿短匕》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-JAGGED-DIRK cardNo=SFD·009/221 targetObjectIds=["P1-JAGGED-DIRK-BASE-UNIT-001"]` 为《锯齿短匕》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-jagged-dirk-target-rejected.fixture.json` 和回放测试 `P4JaggedDirkTargetRejectedFixture`，把《锯齿短匕》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配红色贴附分支仍 deferred。

## P4.150 Doran's Shield Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《多兰之盾》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-DORANS-SHIELD cardNo=SFD·033/221 targetObjectIds=["P1-DORANS-SHIELD-BASE-UNIT-001"]` 为《多兰之盾》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-dorans-shield-target-rejected.fixture.json` 和回放测试 `P4DoransShieldTargetRejectedFixture`，把《多兰之盾》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配绿色贴附分支仍 deferred。

## P4.151 Hextech-Infused Bulwark Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《海克斯注力刚壁》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-HEXTECH-INFUSED-BULWARK cardNo=SFD·073/221 targetObjectIds=["P1-HEXTECH-INFUSED-BULWARK-BASE-UNIT-001"]` 为《海克斯注力刚壁》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-hextech-infused-bulwark-target-rejected.fixture.json` 和回放测试 `P4HextechInfusedBulwarkTargetRejectedFixture`，把《海克斯注力刚壁》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配蓝色贴附分支仍 deferred。

## P4.152 Doran's Blade Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《多兰之刃》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-DORANS-BLADE cardNo=SFD·095/221 targetObjectIds=["P1-DORANS-BLADE-BASE-UNIT-001"]` 为《多兰之刃》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-dorans-blade-target-rejected.fixture.json` 和回放测试 `P4DoransBladeTargetRejectedFixture`，把《多兰之刃》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配橙色贴附分支仍 deferred。

## P4.153 Doran's Ring Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《多兰之戒》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-DORANS-RING cardNo=SFD·124/221 targetObjectIds=["P1-DORANS-RING-BASE-UNIT-001"]` 为《多兰之戒》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-dorans-ring-target-rejected.fixture.json` 和回放测试 `P4DoransRingTargetRejectedFixture`，把《多兰之戒》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配紫色贴附分支仍 deferred。

## P4.154 Marching Orders Enemy Base Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《行军号令》选择敌方基地单位作为第二目标时拒绝；本批次只把该目标范围边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-MARCHING-ORDERS cardNo=SFD·114/221 targetObjectIds=["P1-UNIT-MARCHING-001","P2-BASE-MARCHING-001"]` 选择友方单位后把敌方基地单位作为第二目标，不满足卡面“战场上的一名敌方单位”的目标范围；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不造成互伤，也不创建 stack item。
- 新增 fixture `p4-play-marching-orders-enemy-base-target-rejected.fixture.json` 和回放测试 `P4MarchingOrdersEnemyBaseTargetRejectedFixture`，把《行军号令》敌方基地第二目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害目标合法性拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，完整战斗承伤和更多目标交互仍 deferred。

## P4.155 Duel Target Order Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《决斗》目标顺序反转时拒绝；本批次只把该目标顺序边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-DUEL cardNo=OGN·128/298 targetObjectIds=["P2-UNIT-DUEL-001","P1-UNIT-DUEL-001"]` 先选择敌方单位、再选择友方单位，不满足卡面“选择任意一名友方和一名敌方单位”的目标顺序；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不造成互伤，也不创建 stack item。
- 新增 fixture `p4-play-duel-target-order-rejected.fixture.json` 和回放测试 `P4DuelTargetOrderRejectedFixture`，把《决斗》目标顺序反转拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该伤害目标顺序拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，完整战斗承伤和更多目标交互仍 deferred。

## P4.156 Battle Command Target Order Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《战斗号令》目标顺序反转时拒绝；本批次只把该目标顺序边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-BATTLE-COMMAND cardNo=UNL-101/219 targetObjectIds=["P2-BATTLE-COMMAND-BASE-001","P1-BATTLE-COMMAND-BASE-001"]` 先记录对手单位、再记录友方单位，不满足当前 2P preflight “控制者友方单位，然后对手单位”的目标列表模型；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不移动任何单位，也不创建 stack item。
- 新增 fixture `p4-play-battle-command-target-order-rejected.fixture.json` 和回放测试 `P4BattleCommandTargetOrderRejectedFixture`，把《战斗号令》目标顺序反转拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该移动目标顺序拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，完整对手选择 prompt 和多战场精确位置仍 deferred。

## P4.157 Void Assault Target Order Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《虚空来袭》目标顺序反转时拒绝；本批次只把该目标顺序边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-SPELL-VOID-ASSAULT cardNo=UNL-202/219 targetObjectIds=["P2-VOID-ASSAULT-ENEMY-001","P1-VOID-ASSAULT-FRIENDLY-001"]` 先记录敌方单位、再记录友方单位，不满足当前 preflight “友方单位，然后敌方单位”的目标列表模型；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不移动任何单位，也不创建 stack item。
- 新增 fixture `p4-play-void-assault-target-order-rejected.fixture.json` 和回放测试 `P4VoidAssaultTargetOrderRejectedFixture`，把《虚空来袭》目标顺序反转拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该移动目标顺序拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，战场控制/进攻方细节仍 deferred。

## P4.158 Vanguard's Eye Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《先锋之眼》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-VANGUARDS-EYE cardNo=SFD·153/221 targetObjectIds=["P1-VANGUARDS-EYE-BASE-UNIT-001"]` 为《先锋之眼》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-vanguards-eye-target-rejected.fixture.json` 和回放测试 `P4VanguardsEyeTargetRejectedFixture`，把《先锋之眼》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配黄色贴附分支仍 deferred。

## P4.159 Recurve Bow Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《反曲之弓》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-RECURVE-BOW cardNo=SFD·016/221 targetObjectIds=["P1-RECURVE-BOW-BASE-UNIT-001"]` 为《反曲之弓》的 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-recurve-bow-target-rejected.fixture.json` 和回放测试 `P4RecurveBowTargetRejectedFixture`，把《反曲之弓》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配红色贴附分支仍 deferred。

## P4.160 Long Sword Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《长剑》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-LONG-SWORD cardNo=SFD·022/221 targetObjectIds=["P1-LONG-SWORD-BASE-UNIT-001"]` 为《长剑》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-long-sword-target-rejected.fixture.json` 和回放测试 `P4LongSwordTargetRejectedFixture`，把《长剑》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，灵便反应贴附和装配红色贴附分支仍 deferred。

## P4.161 Cloth Armor Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《布甲》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-CLOTH-ARMOR cardNo=SFD·064/221 targetObjectIds=["P1-CLOTH-ARMOR-BASE-UNIT-001"]` 为《布甲》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-cloth-armor-target-rejected.fixture.json` 和回放测试 `P4ClothArmorTargetRejectedFixture`，把《布甲》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，灵便反应贴附和装配蓝色贴附分支仍 deferred。

## P4.162 Sterak's Gage Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《斯特拉克的挑战护手》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-STERAKS-GAGE cardNo=SFD·056/221 targetObjectIds=["P1-STERAKS-GAGE-BASE-UNIT-001"]` 为《斯特拉克的挑战护手》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-steraks-gage-target-rejected.fixture.json` 和回放测试 `P4SteraksGageTargetRejectedFixture`，把《斯特拉克的挑战护手》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，灵便反应贴附和装配绿色贴附分支仍 deferred。

## P4.163 Spinning Axe Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《旋转飞斧》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SPINNING-AXE cardNo=SFD·186/221 targetObjectIds=["P1-SPINNING-AXE-BASE-UNIT-001"]` 为《旋转飞斧》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-spinning-axe-target-rejected.fixture.json` 和回放测试 `P4SpinningAxeTargetRejectedFixture`，把《旋转飞斧》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，灵便反应贴附、装配 A 贴附和未贴附瞬息开始阶段摧毁分支仍 deferred。

## P4.164 Shepherd's Heirloom Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《牧人的传家宝》带目标打出时拒绝；本批次只把该 0 目标装备入场和打出后获得 1 经验边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SHEPHERDS-HEIRLOOM cardNo=UNL-158/219 targetObjectIds=["P1-SHEPHERDS-HEIRLOOM-BASE-UNIT-001"]` 为《牧人的传家宝》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备、不获得经验，也不创建 stack item。
- 新增 fixture `p4-play-shepherds-heirloom-target-rejected.fixture.json` 和回放测试 `P4ShepherdsHeirloomTargetRejectedFixture`，把《牧人的传家宝》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装备战力修正和装配消耗经验贴附分支仍 deferred。

## P4.165 Brutalizer Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《残暴之力》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-BRUTALIZER cardNo=SFD·042/221 targetObjectIds=["P1-BRUTALIZER-BASE-UNIT-001"]` 为《残暴之力》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-brutalizer-target-rejected.fixture.json` 和回放测试 `P4BrutalizerTargetRejectedFixture`，把《残暴之力》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配绿色贴附分支仍 deferred。

## P4.166 Guardian Angel Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《守护天使》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-GUARDIAN-ANGEL cardNo=SFD·051/221 targetObjectIds=["P1-GUARDIAN-ANGEL-BASE-UNIT-001"]` 为《守护天使》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-guardian-angel-target-rejected.fixture.json` 和回放测试 `P4GuardianAngelTargetRejectedFixture`，把《守护天使》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配绿色贴附分支仍 deferred。

## P4.167 Hexdrinker Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《海克斯饮魔刀》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-HEXDRINKER cardNo=SFD·102/221 targetObjectIds=["P1-HEXDRINKER-BASE-UNIT-001"]` 为《海克斯饮魔刀》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-hexdrinker-target-rejected.fixture.json` 和回放测试 `P4HexdrinkerTargetRejectedFixture`，把《海克斯饮魔刀》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配橙色贴附分支仍 deferred。

## P4.168 Warmog's Armor Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《狂徒铠甲》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-WARMOGS-ARMOR cardNo=SFD·108/221 targetObjectIds=["P1-WARMOGS-ARMOR-BASE-UNIT-001"]` 为《狂徒铠甲》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-warmogs-armor-target-rejected.fixture.json` 和回放测试 `P4WarmogsArmorTargetRejectedFixture`，把《狂徒铠甲》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配橙色贴附分支仍 deferred。

## P4.169 Trinity Force Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《三相之力》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-TRINITY-FORCE cardNo=SFD·115/221 targetObjectIds=["P1-TRINITY-FORCE-BASE-UNIT-001"]` 为《三相之力》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-trinity-force-target-rejected.fixture.json` 和回放测试 `P4TrinityForceTargetRejectedFixture`，把《三相之力》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配橙色贴附分支仍 deferred。

## P4.170 Boots of Swiftness Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《轻灵之靴》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-BOOTS-OF-SWIFTNESS cardNo=SFD·133/221 targetObjectIds=["P1-BOOTS-OF-SWIFTNESS-BASE-UNIT-001"]` 为《轻灵之靴》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-boots-of-swiftness-target-rejected.fixture.json` 和回放测试 `P4BootsOfSwiftnessTargetRejectedFixture`，把《轻灵之靴》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配紫色贴附分支仍 deferred。

## P4.171 Cull Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《萃取》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-CULL cardNo=SFD·134/221 targetObjectIds=["P1-CULL-BASE-UNIT-001"]` 为《萃取》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-cull-target-rejected.fixture.json` 和回放测试 `P4CullTargetRejectedFixture`，把《萃取》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配紫色贴附分支仍 deferred。

## P4.172 Sacred Shears Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《神圣剪刀》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SACRED-SHEARS cardNo=SFD·172/221 targetObjectIds=["P1-SACRED-SHEARS-BASE-UNIT-001"]` 为《神圣剪刀》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-sacred-shears-target-rejected.fixture.json` 和回放测试 `P4SacredShearsTargetRejectedFixture`，把《神圣剪刀》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配黄色贴附分支仍 deferred。

## P4.173 B. F. Sword Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《暴风大剑》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-BF-SWORD cardNo=SFD·161/221 targetObjectIds=["P1-BF-SWORD-BASE-UNIT-001"]` 为《暴风大剑》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-bf-sword-target-rejected.fixture.json` 和回放测试 `P4BfSwordTargetRejectedFixture`，把《暴风大剑》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配黄色贴附分支仍 deferred。

## P4.174 Wanderer's Guidebook Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《云游图鉴》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-WANDERERS-GUIDEBOOK cardNo=SFD·086/221 targetObjectIds=["P1-WANDERERS-GUIDEBOOK-BASE-UNIT-001"]` 为《云游图鉴》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-wanderers-guidebook-target-rejected.fixture.json` 和回放测试 `P4WanderersGuidebookTargetRejectedFixture`，把《云游图鉴》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配蓝色贴附分支仍 deferred。

## P4.175 Arion's Fall Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《阿瑞昂的陨落》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-ARIONS-FALL cardNo=SFD·030/221 targetObjectIds=["P1-ARIONS-FALL-BASE-UNIT-001"]` 为《阿瑞昂的陨落》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-arions-fall-target-rejected.fixture.json` 和回放测试 `P4ArionsFallTargetRejectedFixture`，把《阿瑞昂的陨落》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配 1 红色贴附分支仍 deferred。

## P4.176 Hunter's Machete Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《猎人的宽刃刀》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-HUNTERS-MACHETE cardNo=UNL-096/219 targetObjectIds=["P1-HUNTERS-MACHETE-BASE-UNIT-001"]` 为《猎人的宽刃刀》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-hunters-machete-target-rejected.fixture.json` 和回放测试 `P4HuntersMacheteTargetRejectedFixture`，把《猎人的宽刃刀》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配橙色贴附分支仍 deferred。

## P4.177 Withered Battleaxe Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《枯萎战斧》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-WITHERED-BATTLEAXE cardNo=UNL-019/219 targetObjectIds=["P1-WITHERED-BATTLEAXE-BASE-UNIT-001"]` 为《枯萎战斧》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-withered-battleaxe-target-rejected.fixture.json` 和回放测试 `P4WitheredBattleaxeTargetRejectedFixture`，把《枯萎战斧》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配 1 红色贴附分支仍 deferred。

## P4.178 Bone Club Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖普通版《碎骨棒》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，promo 版本仍沿用既有 P2 preflight。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-BONE-CLUB cardNo=SFD·118/221 targetObjectIds=["P1-BONE-CLUB-BASE-UNIT-001"]` 为普通版《碎骨棒》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-bone-club-target-rejected.fixture.json` 和回放测试 `P4BoneClubTargetRejectedFixture`，把普通版《碎骨棒》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配 1 橙色贴附分支仍 deferred。

## P4.179 Ancient Stele Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《远古簇碑》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置资源技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-ANCIENT-STELE cardNo=SFD·117/221 targetObjectIds=["P1-ANCIENT-STELE-BASE-UNIT-001"]` 为《远古簇碑》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-ancient-stele-target-rejected.fixture.json` 和回放测试 `P4AncientSteleTargetRejectedFixture`，把《远古簇碑》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置资源技能仍 deferred。

## P4.180 Hextech Anomaly Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《海克斯异常体》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置资源技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-HEXTECH-ANOMALY cardNo=SFD·083/221 targetObjectIds=["P1-HEXTECH-ANOMALY-BASE-UNIT-001"]` 为《海克斯异常体》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-hextech-anomaly-target-rejected.fixture.json` 和回放测试 `P4HextechAnomalyTargetRejectedFixture`，把《海克斯异常体》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置资源技能仍 deferred。

## P4.181 Energy Channel Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《能量通道》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置资源技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-ENERGY-CHANNEL cardNo=OGN·098/298 targetObjectIds=["P1-ENERGY-CHANNEL-BASE-UNIT-001"]` 为《能量通道》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-energy-channel-target-rejected.fixture.json` 和回放测试 `P4EnergyChannelTargetRejectedFixture`，把《能量通道》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置资源技能仍 deferred。

## P4.182 Time Gate Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《预时之门》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置回响授予技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-TIME-GATE cardNo=SFD·078/221 targetObjectIds=["P1-TIME-GATE-BASE-UNIT-001"]` 为《预时之门》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-time-gate-target-rejected.fixture.json` 和回放测试 `P4TimeGateTargetRejectedFixture`，把《预时之门》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置回响授予技能仍 deferred。

## P4.183 Raven Tome Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《邪鸦魔典》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置法术伤害修正技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-RAVEN-TOME cardNo=OGN·032/298 targetObjectIds=["P1-RAVEN-TOME-BASE-UNIT-001"]` 为《邪鸦魔典》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-raven-tome-target-rejected.fixture.json` 和回放测试 `P4RavenTomeTargetRejectedFixture`，把《邪鸦魔典》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置法术伤害修正技能仍 deferred。

## P4.184 Sun Disc Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《太阳圆盘》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置鼓舞技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SUN-DISC cardNo=OGN·021/298 targetObjectIds=["P1-SUN-DISC-BASE-UNIT-001"]` 为《太阳圆盘》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-sun-disc-target-rejected.fixture.json` 和回放测试 `P4SunDiscTargetRejectedFixture`，把《太阳圆盘》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置鼓舞技能仍 deferred。

## P4.185 Foresight Mask Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《远见面具》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，战斗触发继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-FORESIGHT-MASK cardNo=OGN·060/298 targetObjectIds=["P1-FORESIGHT-MASK-BASE-UNIT-001"]` 为《远见面具》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-foresight-mask-target-rejected.fixture.json` 和回放测试 `P4ForesightMaskTargetRejectedFixture`，把《远见面具》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，战斗触发仍 deferred。

## P4.186 Solari Altar Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《烈阳圣坛》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，摧毁触发抽牌分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SOLARI-ALTAR cardNo=OGN·072/298 targetObjectIds=["P1-SOLARI-ALTAR-BASE-UNIT-001"]` 为《烈阳圣坛》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-solari-altar-target-rejected.fixture.json` 和回放测试 `P4SolariAltarTargetRejectedFixture`，把《烈阳圣坛》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，摧毁触发抽牌分支仍 deferred。

## P4.187 Chemtech Barrel Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《炼金科技桶》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，对手回合法术触发打出金币分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-CHEMTECH-BARREL cardNo=SFD·063/221 targetObjectIds=["P1-CHEMTECH-BARREL-BASE-UNIT-001"]` 为《炼金科技桶》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-chemtech-barrel-target-rejected.fixture.json` 和回放测试 `P4ChemtechBarrelTargetRejectedFixture`，把《炼金科技桶》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，对手回合法术触发打出金币分支仍 deferred。

## P4.189 Mushroom Bag Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《蘑菇袋》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，开始阶段正面朝下待命牌抽牌触发继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-MUSHROOM-BAG cardNo=OGN·101/298 targetObjectIds=["P1-MUSHROOM-BAG-BASE-UNIT-001"]` 为《蘑菇袋》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-mushroom-bag-target-rejected.fixture.json` 和回放测试 `P4MushroomBagTargetRejectedFixture`，把《蘑菇袋》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，开始阶段正面朝下待命牌抽牌触发仍 deferred。

## P4.190 Arena Bar Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《竞技场酒吧》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置给予休眠友方单位增益技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-ARENA-BAR cardNo=OGN·124/298 targetObjectIds=["P1-ARENA-BAR-BASE-UNIT-001"]` 为《竞技场酒吧》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-arena-bar-target-rejected.fixture.json` 和回放测试 `P4ArenaBarTargetRejectedFixture`，把《竞技场酒吧》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置给予休眠友方单位增益技能仍 deferred。

## P4.191 Pirate Hideout Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《海盗避风港》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，友方单位变为活跃时战力修正触发继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-PIRATE-HIDEOUT cardNo=OGN·143/298 targetObjectIds=["P1-PIRATE-HIDEOUT-BASE-UNIT-001"]` 为《海盗避风港》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-pirate-hideout-target-rejected.fixture.json` 和回放测试 `P4PirateHideoutTargetRejectedFixture`，把《海盗避风港》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，友方单位变为活跃时战力修正触发仍 deferred。

## P4.192 Forgotten Signpost Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《被遗忘的路标》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，迅捷横置移动技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-FORGOTTEN-SIGNPOST cardNo=UNL-045/219 targetObjectIds=["P1-FORGOTTEN-SIGNPOST-BASE-UNIT-001"]` 为《被遗忘的路标》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-forgotten-signpost-target-rejected.fixture.json` 和回放测试 `P4ForgottenSignpostTargetRejectedFixture`，把《被遗忘的路标》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，迅捷横置移动技能仍 deferred。

## P4.193 Frozen Gem Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《冰封宝石》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，抽第二张牌触发战力修正分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-FROZEN-GEM cardNo=UNL-074/219 targetObjectIds=["P1-FROZEN-GEM-BASE-UNIT-001"]` 为《冰封宝石》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-frozen-gem-target-rejected.fixture.json` 和回放测试 `P4FrozenGemTargetRejectedFixture`，把《冰封宝石》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，抽第二张牌触发战力修正分支仍 deferred。

## P4.194 Crumbling Palace Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《倾颓宫殿》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，开始阶段胜利条件和横置创建战鹰分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-CRUMBLING-PALACE cardNo=UNL-088/219 targetObjectIds=["P1-CRUMBLING-PALACE-BASE-UNIT-001"]` 为《倾颓宫殿》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-crumbling-palace-target-rejected.fixture.json` 和回放测试 `P4CrumblingPalaceTargetRejectedFixture`，把《倾颓宫殿》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，开始阶段胜利条件和横置创建战鹰分支仍 deferred。

## P4.195 Scarlet Rose Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《猩红玫瑰》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，单位打出触发经验和横置活跃单位技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SCARLET-ROSE cardNo=UNL-109/219 targetObjectIds=["P1-SCARLET-ROSE-BASE-UNIT-001"]` 为《猩红玫瑰》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-scarlet-rose-target-rejected.fixture.json` 和回放测试 `P4ScarletRoseTargetRejectedFixture`，把《猩红玫瑰》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，单位打出触发经验和横置活跃单位技能仍 deferred。

## P4.196 Reversal Shard Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《逆转碎片》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，开始阶段摧毁触发分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-REVERSAL-SHARD cardNo=UNL-174/219 targetObjectIds=["P1-REVERSAL-SHARD-BASE-UNIT-001"]` 为《逆转碎片》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-reversal-shard-target-rejected.fixture.json` 和回放测试 `P4ReversalShardTargetRejectedFixture`，把《逆转碎片》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，开始阶段摧毁触发分支仍 deferred。

## P4.197 Assembly Rack Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《装配架》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置创建机器人技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-ASSEMBLY-RACK cardNo=SFD·019/221 targetObjectIds=["P1-ASSEMBLY-RACK-BASE-UNIT-001"]` 为《装配架》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-assembly-rack-target-rejected.fixture.json` 和回放测试 `P4AssemblyRackTargetRejectedFixture`，把《装配架》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置创建机器人技能仍 deferred。

## P4.198 Sfur Song Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《斯弗尔尚歌》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，装配贴附和复制技能文字分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SFUR-SONG cardNo=SFD·059/221 targetObjectIds=["P1-SFUR-SONG-BASE-UNIT-001"]` 为《斯弗尔尚歌》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-sfur-song-target-rejected.fixture.json` 和回放测试 `P4SfurSongTargetRejectedFixture`，把《斯弗尔尚歌》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配贴附和复制技能文字分支仍 deferred。

## P4.199 Z-Drive Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《Z型驱动》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，装配和放逐打出分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-Z-DRIVE cardNo=SFD·090/221 targetObjectIds=["P1-Z-DRIVE-BASE-UNIT-001"]` 为《Z型驱动》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-z-drive-target-rejected.fixture.json` 和回放测试 `P4ZDriveTargetRejectedFixture`，把《Z型驱动》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配和放逐打出分支仍 deferred。

## P4.200 Vanguard Armory Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《先锋军备》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置创建多个随从分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-VANGUARD-ARMORY cardNo=SFD·168/221 targetObjectIds=["P1-VANGUARD-ARMORY-BASE-UNIT-001"]` 为《先锋军备》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-vanguard-armory-target-rejected.fixture.json` 和回放测试 `P4VanguardArmoryTargetRejectedFixture`，把《先锋军备》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置创建多个随从分支仍 deferred。

## P4.201 Remembrance Altar Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《追忆祭坛》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，友方单位摧毁触发和牌堆放置选择继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-REMEMBRANCE-ALTAR cardNo=SFD·169/221 targetObjectIds=["P1-REMEMBRANCE-ALTAR-BASE-UNIT-001"]` 为《追忆祭坛》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-remembrance-altar-target-rejected.fixture.json` 和回放测试 `P4RemembranceAltarTargetRejectedFixture`，把《追忆祭坛》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，友方单位摧毁触发和牌堆放置选择仍 deferred。

## P4.202 Rage Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《暴怒之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得红色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-RAGE-SIGIL cardNo=SFD·222/221 targetObjectIds=["P1-RAGE-SIGIL-BASE-UNIT-001"]` 为《暴怒之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-rage-sigil-target-rejected.fixture.json` 和回放测试 `P4RageSigilTargetRejectedFixture`，把《暴怒之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得红色符能技能仍 deferred。

## P4.203 Focus Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《专注之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得绿色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-FOCUS-SIGIL cardNo=SFD·226/221 targetObjectIds=["P1-FOCUS-SIGIL-BASE-UNIT-001"]` 为《专注之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-focus-sigil-target-rejected.fixture.json` 和回放测试 `P4FocusSigilTargetRejectedFixture`，把《专注之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得绿色符能技能仍 deferred。

## P4.204 Insight Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《洞察之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得蓝色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-INSIGHT-SIGIL cardNo=SFD·229/221 targetObjectIds=["P1-INSIGHT-SIGIL-BASE-UNIT-001"]` 为《洞察之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-insight-sigil-target-rejected.fixture.json` 和回放测试 `P4InsightSigilTargetRejectedFixture`，把《洞察之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得蓝色符能技能仍 deferred。

## P4.205 Power Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《力量之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得橙色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-POWER-SIGIL cardNo=SFD·231/221 targetObjectIds=["P1-POWER-SIGIL-BASE-UNIT-001"]` 为《力量之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-power-sigil-target-rejected.fixture.json` 和回放测试 `P4PowerSigilTargetRejectedFixture`，把《力量之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得橙色符能技能仍 deferred。

## P4.206 Discord Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《不和之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得紫色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-DISCORD-SIGIL cardNo=SFD·234/221 targetObjectIds=["P1-DISCORD-SIGIL-BASE-UNIT-001"]` 为《不和之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-discord-sigil-target-rejected.fixture.json` 和回放测试 `P4DiscordSigilTargetRejectedFixture`，把《不和之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得紫色符能技能仍 deferred。

## P4.207 Unity Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《团结之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得黄色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-UNITY-SIGIL cardNo=SFD·238/221 targetObjectIds=["P1-UNITY-SIGIL-BASE-UNIT-001"]` 为《团结之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-unity-sigil-target-rejected.fixture.json` 和回放测试 `P4UnitySigilTargetRejectedFixture`，把《团结之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得黄色符能技能仍 deferred。

## P4.208 OGN Rage Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN 版《暴怒之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得红色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-OGN-RAGE-SIGIL cardNo=OGN·040/298 targetObjectIds=["P1-OGN-RAGE-SIGIL-BASE-UNIT-001"]` 为 OGN 版《暴怒之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-ogn-rage-sigil-target-rejected.fixture.json` 和回放测试 `P4OgnRageSigilTargetRejectedFixture`，把 OGN 版《暴怒之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得红色符能技能仍 deferred。

## P4.209 OGN Focus Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN 版《专注之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得绿色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-OGN-FOCUS-SIGIL cardNo=OGN·081/298 targetObjectIds=["P1-OGN-FOCUS-SIGIL-BASE-UNIT-001"]` 为 OGN 版《专注之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-ogn-focus-sigil-target-rejected.fixture.json` 和回放测试 `P4OgnFocusSigilTargetRejectedFixture`，把 OGN 版《专注之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得绿色符能技能仍 deferred。

## P4.210 OGN Insight Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN 版《洞察之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得蓝色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-OGN-INSIGHT-SIGIL cardNo=OGN·120/298 targetObjectIds=["P1-OGN-INSIGHT-SIGIL-BASE-UNIT-001"]` 为 OGN 版《洞察之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-ogn-insight-sigil-target-rejected.fixture.json` 和回放测试 `P4OgnInsightSigilTargetRejectedFixture`，把 OGN 版《洞察之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得蓝色符能技能仍 deferred。

## P4.211 OGN Power Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN 版《力量之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得橙色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-OGN-POWER-SIGIL cardNo=OGN·163/298 targetObjectIds=["P1-OGN-POWER-SIGIL-BASE-UNIT-001"]` 为 OGN 版《力量之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-ogn-power-sigil-target-rejected.fixture.json` 和回放测试 `P4OgnPowerSigilTargetRejectedFixture`，把 OGN 版《力量之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得橙色符能技能仍 deferred。

## P4.212 OGN Discord Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN 版《不和之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得紫色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-OGN-DISCORD-SIGIL cardNo=OGN·204/298 targetObjectIds=["P1-OGN-DISCORD-SIGIL-BASE-UNIT-001"]` 为 OGN 版《不和之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-ogn-discord-sigil-target-rejected.fixture.json` 和回放测试 `P4OgnDiscordSigilTargetRejectedFixture`，把 OGN 版《不和之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得紫色符能技能仍 deferred。

## P4.213 OGN Unity Sigil Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN 版《团结之印》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置获得黄色符能技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-OGN-UNITY-SIGIL cardNo=OGN·245/298 targetObjectIds=["P1-OGN-UNITY-SIGIL-BASE-UNIT-001"]` 为 OGN 版《团结之印》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-ogn-unity-sigil-target-rejected.fixture.json` 和回放测试 `P4OgnUnitySigilTargetRejectedFixture`，把 OGN 版《团结之印》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置获得黄色符能技能仍 deferred。

## P4.214 Wondrous Pack Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《奇妙行囊》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，横置回手技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-WONDROUS-PACK cardNo=OGN·181/298 targetObjectIds=["P1-WONDROUS-PACK-BASE-UNIT-001"]` 为《奇妙行囊》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-wondrous-pack-target-rejected.fixture.json` 和回放测试 `P4WondrousPackTargetRejectedFixture`，把《奇妙行囊》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置回手技能仍 deferred。

## P4.215 Siren Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《塞壬号》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，支付并横置移动技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SIREN cardNo=OGN·184/298 targetObjectIds=["P1-SIREN-BASE-UNIT-001"]` 为《塞壬号》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-siren-target-rejected.fixture.json` 和回放测试 `P4SirenTargetRejectedFixture`，把《塞壬号》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，支付并横置移动技能仍 deferred。

## P4.216 Ownerless Treasure Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《无主宝藏》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，离场触发和自毁激活技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-OWNERLESS-TREASURE cardNo=OGN·186/298 targetObjectIds=["P1-OWNERLESS-TREASURE-BASE-UNIT-001"]` 为《无主宝藏》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-ownerless-treasure-target-rejected.fixture.json` 和回放测试 `P4OwnerlessTreasureTargetRejectedFixture`，把《无主宝藏》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，离场触发和自毁激活技能仍 deferred。

## P4.217 Scavenging Whiz Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《拾荒小能手》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，回收/支付/横置抽牌技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SCAVENGING-WHIZ cardNo=OGN·099/298 targetObjectIds=["P1-SCAVENGING-WHIZ-BASE-UNIT-001"]` 为《拾荒小能手》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-scavenging-whiz-target-rejected.fixture.json` 和回放测试 `P4ScavengingWhizTargetRejectedFixture`，把《拾荒小能手》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，回收/支付/横置抽牌技能仍 deferred。

## P4.218 Mistfall Bladeyard Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《雾临剑冢》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，增益触发和支付休眠分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-MISTFALL-BLADEYARD cardNo=OGN·152/298 targetObjectIds=["P1-MISTFALL-BLADEYARD-BASE-UNIT-001"]` 为《雾临剑冢》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-mistfall-bladeyard-target-rejected.fixture.json` 和回放测试 `P4MistfallBladeyardTargetRejectedFixture`，把《雾临剑冢》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，增益触发和支付休眠分支仍 deferred。

## P4.219 Shimmering Aurora Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《闪耀极光》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，回合结束展示与免费打出分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SHIMMERING-AURORA cardNo=OGN·160/298 targetObjectIds=["P1-SHIMMERING-AURORA-BASE-UNIT-001"]` 为《闪耀极光》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 9 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-shimmering-aurora-target-rejected.fixture.json` 和回放测试 `P4ShimmeringAuroraTargetRejectedFixture`，把《闪耀极光》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，回合结束展示与免费打出分支仍 deferred。

## P4.220 Solari Emblem Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《烈阳徽记》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，战斗平局触发和全体召回分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SOLARI-EMBLEM cardNo=OGN·227/298 targetObjectIds=["P1-SOLARI-EMBLEM-BASE-UNIT-001"]` 为《烈阳徽记》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 1 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-solari-emblem-target-rejected.fixture.json` 和回放测试 `P4SolariEmblemTargetRejectedFixture`，把《烈阳徽记》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，战斗平局触发和全体召回分支仍 deferred。

## P4.221 Vanguard Helm Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《先锋之盔》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，增益单位摧毁触发和增益分配继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-VANGUARD-HELM cardNo=OGN·228/298 targetObjectIds=["P1-VANGUARD-HELM-BASE-UNIT-001"]` 为《先锋之盔》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-vanguard-helm-target-rejected.fixture.json` 和回放测试 `P4VanguardHelmTargetRejectedFixture`，把《先锋之盔》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，增益单位摧毁触发和增益分配仍 deferred。

## P4.222 Honeyfruit Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《蜜糖果实》带目标打出时拒绝；本批次只把该 0 目标休眠装备入场边界提升为可回放 fixture，横置资源技能和等级 6 分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-HONEYFRUIT cardNo=UNL-049/219 targetObjectIds=["P1-HONEYFRUIT-BASE-UNIT-001"]` 为《蜜糖果实》的当前 0 目标休眠装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-honeyfruit-target-rejected.fixture.json` 和回放测试 `P4HoneyfruitTargetRejectedFixture`，把《蜜糖果实》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置资源技能和等级 6 分支仍 deferred。

## P4.223 Last Rites Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《临终仪式》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，装配和废牌堆回收分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-LAST-RITES cardNo=SFD·150/221 targetObjectIds=["P1-LAST-RITES-BASE-UNIT-001"]` 为《临终仪式》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-last-rites-target-rejected.fixture.json` 和回放测试 `P4LastRitesTargetRejectedFixture`，把《临终仪式》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配和废牌堆回收分支仍 deferred。

## P4.224 Blade Of The Ruined King Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《破败王者之刃》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，装配、额外费用和贴附分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-BLADE-OF-RUINED-KING cardNo=SFD·178/221 targetObjectIds=["P1-BLADE-OF-RUINED-KING-BASE-UNIT-001"]` 为《破败王者之刃》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-blade-of-ruined-king-target-rejected.fixture.json` 和回放测试 `P4BladeOfRuinedKingTargetRejectedFixture`，把《破败王者之刃》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配、额外费用和贴附分支仍 deferred。

## P4.225 Mysterious Weapon Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《来路不明的武器》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，弃牌横置和摧毁替代效果继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-MYSTERIOUS-WEAPON cardNo=OGN·023/298 targetObjectIds=["P1-MYSTERIOUS-WEAPON-BASE-UNIT-001"]` 为《来路不明的武器》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-mysterious-weapon-target-rejected.fixture.json` 和回放测试 `P4MysteriousWeaponTargetRejectedFixture`，把《来路不明的武器》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，弃牌横置和摧毁替代效果仍 deferred。

## P4.226 Sea Monster Hook Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《海兽钓钩》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，激活、摧毁、查看和免费打出分支继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-SEA-MONSTER-HOOK cardNo=OGN·242/298 targetObjectIds=["P1-SEA-MONSTER-HOOK-BASE-UNIT-001"]` 为《海兽钓钩》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-sea-monster-hook-target-rejected.fixture.json` 和回放测试 `P4SeaMonsterHookTargetRejectedFixture`，把《海兽钓钩》带目标打出拒绝边界纳入 conformance 证据。

## P4.227 Petricite Monument Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《禁魔石丰碑》带目标打出时拒绝；本批次只把该 0 目标瞬息装备入场边界提升为可回放 fixture，友方单位法盾静态效果和开始阶段瞬息摧毁继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-PETRICITE-MONUMENT cardNo=SFD·104/221 targetObjectIds=["P1-PETRICITE-MONUMENT-BASE-UNIT-001"]` 为《禁魔石丰碑》的当前 0 目标瞬息装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-petricite-monument-target-rejected.fixture.json` 和回放测试 `P4PetriciteMonumentTargetRejectedFixture`，把《禁魔石丰碑》带目标打出拒绝边界纳入 conformance 证据。

## P4.228 Zhonyas Hourglass Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《中娅沙漏》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，待命/反应时机和摧毁替代召回效果继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-ZHONYAS-HOURGLASS cardNo=OGN·077/298 targetObjectIds=["P1-ZHONYAS-HOURGLASS-BASE-UNIT-001"]` 为《中娅沙漏》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-zhonyas-hourglass-target-rejected.fixture.json` 和回放测试 `P4ZhonyasHourglassTargetRejectedFixture`，把《中娅沙漏》带目标打出拒绝边界纳入 conformance 证据。

## P4.229 Edge of Night Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《夜之锋刃》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，待命正面朝下打出、即时贴附和装配继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-EDGE-OF-NIGHT cardNo=SFD·139/221 targetObjectIds=["P1-EDGE-OF-NIGHT-BASE-UNIT-001"]` 为《夜之锋刃》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-edge-of-night-target-rejected.fixture.json` 和回放测试 `P4EdgeOfNightTargetRejectedFixture`，把《夜之锋刃》带目标打出拒绝边界纳入 conformance 证据。

## P4.230 Hearthfire Cloak Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《炉火斗篷》带目标打出时拒绝；本批次只把该 0 目标专属装备入场边界提升为可回放 fixture，唯我构筑限制和装配贴附继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-HEARTHFIRE-CLOAK cardNo=SFD·190/221 targetObjectIds=["P1-HEARTHFIRE-CLOAK-BASE-UNIT-001"]` 为《炉火斗篷》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-hearthfire-cloak-target-rejected.fixture.json` 和回放测试 `P4HearthfireCloakTargetRejectedFixture`，把《炉火斗篷》带目标打出拒绝边界纳入 conformance 证据。

## P4.231 Rabadons Deathcap Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《灭世者的死亡之冠》带目标打出时拒绝；本批次只把该 0 目标专属装备入场边界提升为可回放 fixture，唯我构筑限制和装配贴附继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-RABADONS-DEATHCAP cardNo=SFD·191/221 targetObjectIds=["P1-RABADONS-DEATHCAP-BASE-UNIT-001"]` 为《灭世者的死亡之冠》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-rabadons-deathcap-target-rejected.fixture.json` 和回放测试 `P4RabadonsDeathcapTargetRejectedFixture`，把《灭世者的死亡之冠》带目标打出拒绝边界纳入 conformance 证据。

## P4.232 Blast Cone Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《喷射球果》当前 no-move 路径带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，可选移动、移动触发休眠和眩晕继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-BLAST-CONE cardNo=UNL-133/219 targetObjectIds=["P1-BLAST-CONE-BASE-UNIT-001"]` 为《喷射球果》的当前 no-move 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-blast-cone-target-rejected.fixture.json` 和回放测试 `P4BlastConeTargetRejectedFixture`，把《喷射球果》当前 no-move 路径带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，激活、摧毁、查看和免费打出分支仍 deferred。

## P4.233 Death List Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《夺命名单》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，宣告属性标签和横置战力修正技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-DEATH-LIST cardNo=UNL-138/219 targetObjectIds=["P1-DEATH-LIST-BASE-UNIT-001"]` 为《夺命名单》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 1 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-death-list-target-rejected.fixture.json` 和回放测试 `P4DeathListTargetRejectedFixture`，把《夺命名单》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，宣告属性标签和横置战力修正技能仍 deferred。

## P4.234 Cursed Sarcophagus Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《受诅咒的石棺》带目标打出时拒绝；本批次只把该 0 目标装备入场并放逐废牌堆单位牌边界提升为可回放 fixture，横置摧毁自身并打出放逐单位牌的激活技能继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-CURSED-SARCOPHAGUS cardNo=UNL-148/219 targetObjectIds=["P1-CURSED-SARCOPHAGUS-BASE-UNIT-001"]` 为《受诅咒的石棺》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场装备、不放逐废牌堆单位牌，也不创建 stack item。
- 新增 fixture `p4-play-cursed-sarcophagus-target-rejected.fixture.json` 和回放测试 `P4CursedSarcophagusTargetRejectedFixture`，把《受诅咒的石棺》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置摧毁自身并打出放逐单位牌的激活技能仍 deferred。

## P4.235 Boneclub Promo Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 promo 编号《碎骨棒》带目标打出时拒绝；本批次只把该 0 目标装备入场边界提升为可回放 fixture，装配贴附继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-BONECLUB-PROMO cardNo=SFD·118a/221·P targetObjectIds=["P1-BONECLUB-PROMO-BASE-UNIT-001"]` 为 promo 编号《碎骨棒》的当前 0 目标装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-boneclub-promo-target-rejected.fixture.json` 和回放测试 `P4BoneclubPromoTargetRejectedFixture`，把 promo 编号《碎骨棒》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装配贴附仍 deferred。

## P4.236 Hextech Gauntlet Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《海克斯科技护手》带目标打出时拒绝；本批次只把该 0 目标专属装备入场边界提升为可回放 fixture，动态装配费用和贴附继续暂缓。

- `PLAY_CARD sourceObjectId=P1-EQUIPMENT-HEXTECH-GAUNTLET cardNo=UNL-188/219 targetObjectIds=["P1-HEXTECH-GAUNTLET-BASE-UNIT-001"]` 为《海克斯科技护手》的当前 0 目标专属装备入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场装备，也不创建 stack item。
- 新增 fixture `p4-play-hextech-gauntlet-target-rejected.fixture.json` 和回放测试 `P4HextechGauntletTargetRejectedFixture`，把《海克斯科技护手》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标装备打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，动态装配费用和贴附仍 deferred。

## P4.237 Treasure Golem Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《宝藏魔像》带目标打出时拒绝；本批次只把该 0 目标单位入场并创建金币边界提升为可回放 fixture，完整目的地选择继续暂缓。

- `PLAY_CARD sourceObjectId=P1-UNIT-TREASURE-GOLEM cardNo=SFD·174/221 targetObjectIds=["P1-TREASURE-GOLEM-BASE-UNIT-001"]` 为《宝藏魔像》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 8 点费用、不移动手牌、不入场单位、不创建金币，也不创建 stack item。
- 新增 fixture `p4-play-treasure-golem-target-rejected.fixture.json` 和回放测试 `P4TreasureGolemTargetRejectedFixture`，把《宝藏魔像》带目标打出拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，完整目的地选择仍 deferred。

## P4.238 Xerath Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《泽拉斯》普通手牌打出带目标时拒绝；本批次只把该 0 目标英雄单位入场边界提升为可回放 fixture，通用 skill registry 不扩展。

- `PLAY_CARD sourceObjectId=P1-UNIT-XERATH cardNo=UNL-026/219 targetObjectIds=["P1-BASE-XERATH-TARGET-001"]` 为《泽拉斯》的当前 0 目标英雄单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 5 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-xerath-target-rejected.fixture.json` 和回放测试 `P4XerathTargetRejectedFixture`，把《泽拉斯》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，战场激活技能仍由 P4.77-P4.86/P4.93 覆盖，通用 skill registry 仍 deferred。

## P4.239 Dragon Soul Sage Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《龙魂贤者》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，反应横置获得资源技能不扩展。

- `PLAY_CARD sourceObjectId=P1-UNIT-DRAGON-SOUL-SAGE cardNo=UNL-093/219 targetObjectIds=["P1-BASE-DRAGON-SOUL-SAGE-TARGET-001"]` 为《龙魂贤者》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-dragon-soul-sage-target-rejected.fixture.json` 和回放测试 `P4DragonSoulSageTargetRejectedFixture`，把《龙魂贤者》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，反应时机、横置和获得资源技能仍 deferred。

## P4.240 Fluft Poro Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖《绵绵魄罗》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，横置召出战鹰技能不扩展。

- `PLAY_CARD sourceObjectId=P1-UNIT-FLUFT-PORO cardNo=UNL-160/219 targetObjectIds=["P1-BASE-FLUFT-PORO-TARGET-001"]` 为《绵绵魄罗》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 5 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-fluft-poro-target-rejected.fixture.json` 和回放测试 `P4FluftPoroTargetRejectedFixture`，把《绵绵魄罗》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，横置、战鹰指示物、法盾目标税和战场限制技能仍 deferred。

## P4.241 Renata Glasc Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 SFD·088《烈娜塔·戈拉斯克》普通手牌打出带目标时拒绝；本批次只把该 0 目标英雄单位入场边界提升为可回放 fixture，主动技能不扩展。

- `PLAY_CARD sourceObjectId=P1-UNIT-SFD-088-RENATA cardNo=SFD·088/221 targetObjectIds=["P1-BASE-SFD-088-RENATA-TARGET-001"]` 为 SFD·088《烈娜塔·戈拉斯克》的当前 0 目标英雄单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 5 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-sfd-088-renata-glasc-target-rejected.fixture.json` 和回放测试 `P4Sfd088RenataGlascTargetRejectedFixture`，把 SFD·088《烈娜塔·戈拉斯克》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标英雄单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，蓝色费用、横置、抽牌、得分和战场限制技能仍 deferred。

## P4.242 Renata Glasc Alt Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 SFD·088a《烈娜塔·戈拉斯克》普通手牌打出带目标时拒绝；本批次只把该 0 目标英雄单位异画 A 入场边界提升为可回放 fixture，主动技能不扩展。

- `PLAY_CARD sourceObjectId=P1-UNIT-SFD-088A-RENATA cardNo=SFD·088a/221 targetObjectIds=["P1-BASE-SFD-088A-RENATA-TARGET-001"]` 为 SFD·088a《烈娜塔·戈拉斯克》的当前 0 目标英雄单位异画 A 入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 5 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-sfd-088a-renata-glasc-target-rejected.fixture.json` 和回放测试 `P4Sfd088aRenataGlascTargetRejectedFixture`，把 SFD·088a《烈娜塔·戈拉斯克》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标英雄单位异画 A 打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，蓝色费用、横置、抽牌、得分和战场限制技能仍 deferred。

## P4.243 OGN Draven Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·028《德莱文》普通手牌打出带目标时拒绝；本批次只把该 0 目标英雄单位入场边界提升为可回放 fixture，非 0 分静态战力修正不扩展。

- `PLAY_CARD sourceObjectId=P1-UNIT-OGN-028-DRAVEN cardNo=OGN·028/298 targetObjectIds=["P1-BASE-OGN-028-DRAVEN-TARGET-001"]` 为 OGN·028《德莱文》的当前 0 目标英雄单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 5 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-ogn-028-draven-target-rejected.fixture.json` 和回放测试 `P4Ogn028DravenTargetRejectedFixture`，把 OGN·028《德莱文》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标英雄单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，非 0 分静态战力修正仍 deferred。

## P4.244 SFD Fiora Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 SFD·110《菲奥娜》普通手牌打出带目标时拒绝；本批次只把该 0 目标英雄单位入场边界提升为可回放 fixture，一对一战斗检测和战斗内战力翻倍不扩展。

- `PLAY_CARD sourceObjectId=P1-UNIT-SFD-110-FIORA cardNo=SFD·110/221 targetObjectIds=["P1-BASE-SFD-110-FIORA-TARGET-001"]` 为 SFD·110《菲奥娜》的当前 0 目标英雄单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-sfd-110-fiora-target-rejected.fixture.json` 和回放测试 `P4Sfd110FioraTargetRejectedFixture`，把 SFD·110《菲奥娜》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标英雄单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，一对一战斗检测和战斗内战力翻倍仍 deferred。

## P4.245 SFD Fiora Alt Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 SFD·110a《菲奥娜》异画 A 普通手牌打出带目标时拒绝；本批次只把该 0 目标英雄单位入场边界提升为可回放 fixture，一对一战斗检测和战斗内战力翻倍不扩展。

- `PLAY_CARD sourceObjectId=P1-UNIT-SFD-110A-FIORA cardNo=SFD·110a/221 targetObjectIds=["P1-BASE-SFD-110A-FIORA-TARGET-001"]` 为 SFD·110a《菲奥娜》的当前 0 目标英雄单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-sfd-110a-fiora-target-rejected.fixture.json` 和回放测试 `P4Sfd110aFioraTargetRejectedFixture`，把 SFD·110a《菲奥娜》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标英雄单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，一对一战斗检测和战斗内战力翻倍仍 deferred。

## P4.246 SFD Irelia Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 SFD·141《艾瑞莉娅》普通手牌打出带目标时拒绝；本批次只把该 0 目标英雄单位入场边界提升为可回放 fixture，不扩展“以艾瑞莉娅为目标的法术费用减少”静态能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-SFD-141-IRELIA cardNo=SFD·141/221 targetObjectIds=["P1-BASE-SFD-141-IRELIA-TARGET-001"]` 为 SFD·141《艾瑞莉娅》的当前 0 目标英雄单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-sfd-141-irelia-target-rejected.fixture.json` 和回放测试 `P4Sfd141IreliaTargetRejectedFixture`，把 SFD·141《艾瑞莉娅》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标英雄单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，以艾瑞莉娅为目标的法术费用减少路径仍 deferred。

## P4.247 SFD Irelia Alt Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 SFD·141a《艾瑞莉娅》异画 A 普通手牌打出带目标时拒绝；本批次只把该 0 目标英雄单位入场边界提升为可回放 fixture，不扩展“以艾瑞莉娅为目标的法术费用减少”静态能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-SFD-141A-IRELIA cardNo=SFD·141a/221 targetObjectIds=["P1-BASE-SFD-141A-IRELIA-TARGET-001"]` 为 SFD·141a《艾瑞莉娅》的当前 0 目标英雄单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-sfd-141a-irelia-target-rejected.fixture.json` 和回放测试 `P4Sfd141aIreliaTargetRejectedFixture`，把 SFD·141a《艾瑞莉娅》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标英雄单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，以艾瑞莉娅为目标的法术费用减少路径仍 deferred。

## P4.248 OGN Dragon Caller Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·140《唤龙使者》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展龙属性单位费用减少静态能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-DRAGON-CALLER cardNo=OGN·140/298 targetObjectIds=["P1-BASE-DRAGON-CALLER-TARGET-001"]` 为 OGN·140《唤龙使者》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-dragon-caller-target-rejected.fixture.json` 和回放测试 `P4DragonCallerTargetRejectedFixture`，把 OGN·140《唤龙使者》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，龙属性单位费用减少静态路径仍 deferred。

## P4.249 OGN Waterbender Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·055《驭水者》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展独自进攻/防守时的战力修正。

- `PLAY_CARD sourceObjectId=P1-UNIT-WATERBENDER cardNo=OGN·055/298 targetObjectIds=["P1-BASE-WATERBENDER-TARGET-001"]` 为 OGN·055《驭水者》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-waterbender-target-rejected.fixture.json` 和回放测试 `P4WaterbenderTargetRejectedFixture`，把 OGN·055《驭水者》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，独自进攻/防守战力修正路径仍 deferred。

## P4.250 OGN Wise Elder Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·065《睿智长者》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展拥有增益时的静态战力修正。

- `PLAY_CARD sourceObjectId=P1-UNIT-WISE-ELDER cardNo=OGN·065/298 targetObjectIds=["P1-BASE-WISE-ELDER-TARGET-001"]` 为 OGN·065《睿智长者》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-wise-elder-target-rejected.fixture.json` 和回放测试 `P4WiseElderTargetRejectedFixture`，把 OGN·065《睿智长者》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，拥有增益时额外战力 +1 路径仍 deferred。

## P4.251 OGN Eager Apprentice Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·084《踊跃的学徒》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展位于战场时的法术费用减少静态能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-EAGER-APPRENTICE cardNo=OGN·084/298 targetObjectIds=["P1-BASE-EAGER-APPRENTICE-TARGET-001"]` 为 OGN·084《踊跃的学徒》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-eager-apprentice-target-rejected.fixture.json` 和回放测试 `P4EagerApprenticeTargetRejectedFixture`，把 OGN·084《踊跃的学徒》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，战场法术费用减少静态路径仍 deferred。

## P4.252 OGN Arena Service Crew Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·091《竞技场勤务小队》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展打出装备时变为活跃状态的触发能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-ARENA-SERVICE-CREW cardNo=OGN·091/298 targetObjectIds=["P1-BASE-ARENA-SERVICE-CREW-TARGET-001"]` 为 OGN·091《竞技场勤务小队》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-arena-service-crew-target-rejected.fixture.json` 和回放测试 `P4ArenaServiceCrewTargetRejectedFixture`，把 OGN·091《竞技场勤务小队》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，装备打出触发活跃路径仍 deferred。

## P4.253 OGN Poro Herder Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·061《魄罗牧者》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展魄罗条件满足后的增益和抽牌能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-PORO-HERDER cardNo=OGN·061/298 targetObjectIds=["P1-BASE-PORO-HERDER-TARGET-001"]` 为 OGN·061《魄罗牧者》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-poro-herder-target-rejected.fixture.json` 和回放测试 `P4PoroHerderTargetRejectedFixture`，把 OGN·061《魄罗牧者》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，魄罗条件增益和抽牌路径仍 deferred。

## P4.254 OGN Ravenbloom Student Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·103《拉文布鲁姆学生》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展打出法术时的本回合战力修正触发能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-RAVENBLOOM-STUDENT cardNo=OGN·103/298 targetObjectIds=["P1-BASE-RAVENBLOOM-STUDENT-TARGET-001"]` 为 OGN·103《拉文布鲁姆学生》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-ravenbloom-student-target-rejected.fixture.json` 和回放测试 `P4RavenbloomStudentTargetRejectedFixture`，把 OGN·103《拉文布鲁姆学生》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，法术打出触发本回合战力 +1 路径仍 deferred。

## P4.255 OGN Resonant Soul Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·118《残响之魂》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展每回合首次友方单位被摧毁抽牌触发能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-RESONANT-SOUL cardNo=OGN·118/298 targetObjectIds=["P1-BASE-RESONANT-SOUL-TARGET-001"]` 为 OGN·118《残响之魂》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 6 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-resonant-soul-target-rejected.fixture.json` 和回放测试 `P4ResonantSoulTargetRejectedFixture`，把 OGN·118《残响之魂》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，友方单位被摧毁触发抽牌路径仍 deferred。

## P4.256 OGN Bilgewater Bully Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·125《比尔吉沃特恶霸》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展拥有增益时获得游走的静态能力或真实移动系统。

- `PLAY_CARD sourceObjectId=P1-UNIT-BILGEWATER-BULLY cardNo=OGN·125/298 targetObjectIds=["P1-BASE-BILGEWATER-BULLY-TARGET-001"]` 为 OGN·125《比尔吉沃特恶霸》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 6 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-bilgewater-bully-target-rejected.fixture.json` 和回放测试 `P4BilgewaterBullyTargetRejectedFixture`，把 OGN·125《比尔吉沃特恶霸》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，拥有增益时获得游走和游走真实移动路径仍 deferred。

## P4.257 OGN Sharpshooter Pirate Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·130《神射海盗》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展进攻触发和同处敌方单位伤害能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-SHARPSHOOTER-PIRATE cardNo=OGN·130/298 targetObjectIds=["P1-BASE-SHARPSHOOTER-PIRATE-TARGET-001"]` 为 OGN·130《神射海盗》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-sharpshooter-pirate-target-rejected.fixture.json` 和回放测试 `P4SharpshooterPirateTargetRejectedFixture`，把 OGN·130《神射海盗》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，进攻触发和同处敌方单位伤害路径仍 deferred。

## P4.258 OGN Dune Drake Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·131《沙丘亚龙》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展进攻触发或本回合战力修正能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-DUNE-DRAKE cardNo=OGN·131/298 targetObjectIds=["P1-BASE-DUNE-DRAKE-TARGET-001"]` 为 OGN·131《沙丘亚龙》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 5 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-dune-drake-target-rejected.fixture.json` 和回放测试 `P4DuneDrakeTargetRejectedFixture`，把 OGN·131《沙丘亚龙》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，进攻有准备状态敌方单位的战场时本回合 +2 路径仍 deferred。

## P4.259 OGN Ember Monk Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·167《余火修士》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展待命牌打出触发或本回合战力修正能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-EMBER-MONK cardNo=OGN·167/298 targetObjectIds=["P1-BASE-EMBER-MONK-TARGET-001"]` 为 OGN·167《余火修士》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-ember-monk-target-rejected.fixture.json` 和回放测试 `P4EmberMonkTargetRejectedFixture`，把 OGN·167《余火修士》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，待命牌打出触发本回合 +2 路径仍 deferred。

## P4.260 OGN Hidden Tracker Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·177《隐秘追踪者》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展跟随移动触发或真实移动能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-HIDDEN-TRACKER cardNo=OGN·177/298 targetObjectIds=["P1-BASE-HIDDEN-TRACKER-TARGET-001"]` 为 OGN·177《隐秘追踪者》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-hidden-tracker-target-rejected.fixture.json` 和回放测试 `P4HiddenTrackerTargetRejectedFixture`，把 OGN·177《隐秘追踪者》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，跟随移动和真实移动路径仍 deferred。

## P4.261 OGN Undercover Agent Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·178《卧底特工》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展绝念弃牌抽牌能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-UNDERCOVER-AGENT cardNo=OGN·178/298 targetObjectIds=["P1-BASE-UNDERCOVER-AGENT-TARGET-001"]` 为 OGN·178《卧底特工》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 5 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-undercover-agent-target-rejected.fixture.json` 和回放测试 `P4UndercoverAgentTargetRejectedFixture`，把 OGN·178《卧底特工》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，绝念弃牌抽牌路径仍 deferred。

## P4.262 OGN Traveling Merchant Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·185《旅行商人》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展移动触发弃牌抽牌能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-TRAVELING-MERCHANT cardNo=OGN·185/298 targetObjectIds=["P1-BASE-TRAVELING-MERCHANT-TARGET-001"]` 为 OGN·185《旅行商人》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-traveling-merchant-target-rejected.fixture.json` 和回放测试 `P4TravelingMerchantTargetRejectedFixture`，把 OGN·185《旅行商人》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，移动触发弃牌抽牌路径仍 deferred。

## P4.263 OGN Kog'Maw Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·190《克格莫》普通手牌打出带目标时拒绝；本批次只把该 0 目标英雄单位入场边界提升为可回放 fixture，不扩展绝念范围伤害能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-OGN-KOGMAW cardNo=OGN·190/298 targetObjectIds=["P1-BASE-OGN-KOGMAW-TARGET-001"]` 为 OGN·190《克格莫》的当前 0 目标英雄单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-ogn-kogmaw-target-rejected.fixture.json` 和回放测试 `P4OgnKogmawTargetRejectedFixture`，把 OGN·190《克格莫》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标英雄单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，绝念范围伤害路径仍 deferred。

## P4.264 OGN Noxian Drummer Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·222《诺克萨斯鼓手》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展移动到战场打出随从能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-NOXIAN-DRUMMER cardNo=OGN·222/298 targetObjectIds=["P1-BASE-NOXIAN-DRUMMER-TARGET-001"]` 为 OGN·222《诺克萨斯鼓手》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-noxian-drummer-target-rejected.fixture.json` 和回放测试 `P4NoxianDrummerTargetRejectedFixture`，把 OGN·222《诺克萨斯鼓手》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，移动到战场打出随从路径仍 deferred。

## P4.265 OGN Tide Caller Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·199《控潮者》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展待命/反应打出或可选位置交换能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-TIDE-CALLER cardNo=OGN·199/298 targetObjectIds=["P1-BASE-TIDE-CALLER-TARGET-001"]` 为 OGN·199《控潮者》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-tide-caller-target-rejected.fixture.json` 和回放测试 `P4TideCallerTargetRejectedFixture`，把 OGN·199《控潮者》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，待命/反应打出和可选位置交换路径仍 deferred。

## P4.266 OGN 202 Jinx Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·202《金克丝》普通手牌打出带目标时拒绝；本批次只把该 0 目标英雄单位入场边界提升为可回放 fixture，不扩展弃牌触发活跃或本回合战力修正能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-OGN-JINX cardNo=OGN·202/298 targetObjectIds=["P1-BASE-OGN-JINX-TARGET-001"]` 为 OGN·202《金克丝》的当前 0 目标英雄单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 5 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-ogn-202-jinx-target-rejected.fixture.json` 和回放测试 `P4Ogn202JinxTargetRejectedFixture`，把 OGN·202《金克丝》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标英雄单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，弃牌触发活跃和本回合战力 +1 路径仍 deferred。

## P4.267 Ghost Matron Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·226《幽灵主母》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展可选从废牌堆免费打出低费用单位能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-GHOST-MATRON cardNo=OGN·226/298 targetObjectIds=["P1-BASE-GHOST-MATRON-TARGET-001"]` 为 OGN·226《幽灵主母》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-ghost-matron-target-rejected.fixture.json` 和回放测试 `P4GhostMatronTargetRejectedFixture`，把 OGN·226《幽灵主母》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，可选从废牌堆打出低费用单位路径仍 deferred。

## P4.268 Albus Ferros Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·230《阿不思·菲罗斯》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展消耗增益并召出休眠符文能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-ALBUS-FERROS cardNo=OGN·230/298 targetObjectIds=["P1-BASE-ALBUS-FERROS-TARGET-001"]` 为 OGN·230《阿不思·菲罗斯》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 4 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-albus-ferros-target-rejected.fixture.json` 和回放测试 `P4AlbusFerrosTargetRejectedFixture`，把 OGN·230《阿不思·菲罗斯》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，消耗增益召出休眠符文路径仍 deferred。

## P4.269 OGN Karthus Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 OGN·236《卡尔萨斯》普通手牌打出带目标时拒绝；本批次只把该 0 目标英雄单位入场边界提升为可回放 fixture，不扩展绝念额外触发静态能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-OGN-KARTHUS cardNo=OGN·236/298 targetObjectIds=["P1-BASE-OGN-KARTHUS-TARGET-001"]` 为 OGN·236《卡尔萨斯》的当前 0 目标英雄单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 3 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-ogn-karthus-target-rejected.fixture.json` 和回放测试 `P4OgnKarthusTargetRejectedFixture`，把 OGN·236《卡尔萨斯》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标英雄单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，绝念额外触发静态路径仍 deferred。

## P4.270 Dunehorn Beast Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 SFD·027《穿沙角兽》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展低手牌活跃进场或据守抽两张牌能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-DUNEHORN-BEAST cardNo=SFD·027/221 targetObjectIds=["P1-BASE-DUNEHORN-BEAST-TARGET-001"]` 为 SFD·027《穿沙角兽》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 7 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-dunehorn-beast-target-rejected.fixture.json` 和回放测试 `P4DunehornBeastTargetRejectedFixture`，把 SFD·027《穿沙角兽》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，低手牌活跃进场和据守抽两张牌路径仍 deferred。

## P4.271 Gloompath Guard Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 SFD·035《幽径守卫》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展据守时废牌堆单位或装备返回手牌能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-GLOOMPATH-GUARD cardNo=SFD·035/221 targetObjectIds=["P1-BASE-GLOOMPATH-GUARD-TARGET-001"]` 为 SFD·035《幽径守卫》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 6 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-gloompath-guard-target-rejected.fixture.json` 和回放测试 `P4GloompathGuardTargetRejectedFixture`，把 SFD·035《幽径守卫》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，据守时废牌堆单位或装备返回手牌路径仍 deferred。

## P4.272 Apprentice Blacksmith Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 SFD·041《学徒铁匠》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展移动触发展示主牌堆顶部、装备抽取或非装备回收能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-APPRENTICE-BLACKSMITH cardNo=SFD·041/221 targetObjectIds=["P1-BASE-APPRENTICE-BLACKSMITH-TARGET-001"]` 为 SFD·041《学徒铁匠》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 2 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-apprentice-blacksmith-target-rejected.fixture.json` 和回放测试 `P4ApprenticeBlacksmithTargetRejectedFixture`，把 SFD·041《学徒铁匠》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，移动触发展示主牌堆顶部、装备抽取和非装备回收路径仍 deferred。

## P4.273 Mountain Ape Elder Target Rejection Fixture Slice

本阶段继续 completion audit：P4 仍不能标记 goal complete。已有直接 engine 测试覆盖 SFD·047《山猿老祖》普通手牌打出带目标时拒绝；本批次只把该 0 目标单位入场边界提升为可回放 fixture，不扩展获得增益后变为活跃状态能力。

- `PLAY_CARD sourceObjectId=P1-UNIT-MOUNTAIN-APE-ELDER cardNo=SFD·047/221 targetObjectIds=["P1-BASE-MOUNTAIN-APE-ELDER-TARGET-001"]` 为 SFD·047《山猿老祖》的当前 0 目标单位入场路径提供显式单位目标，不满足目标数量要求；命令返回 `INVALID_TARGET`，不推进 tick、不写事件、不支付 5 点费用、不移动手牌、不入场单位，也不创建 stack item。
- 新增 fixture `p4-play-mountain-ape-elder-target-rejected.fixture.json` 和回放测试 `P4MountainApeElderTargetRejectedFixture`，把 SFD·047《山猿老祖》普通手牌打出带目标拒绝边界纳入 conformance 证据。
- `P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen` 现在实际回放该 0 目标单位打出拒绝 fixture；本批次没有改变 `CoreRuleEngine` 主实现，获得增益后活跃路径仍 deferred。

## Risk Layers

低风险，可先做桥接和只读验证：

- `draw`、`damage`、`destroy`、`stun`、`temp_might` 已有 P4.5 primitive plan。
- 已由 P2 手写行为覆盖的 `move` / `recall` 代表路径仍保持 `delegated-to-p2`。
- 已由 P2 手写行为覆盖的 `recycle` / `banish` / `boon` 在 P4.60 进入 P3/P4 template skeleton 和 safe delegation；primitive executor 仍不直接改状态。
- 已由 P2 手写行为覆盖的《取放自如》武装贴附/卸除代表路径在 P4.58 进入 P4 evidence/profile，但不泛化成完整装备系统。
- `ACTIVATE_ABILITY` 已有 P4.62 command envelope；P4.73 已执行《蔚》`PAY_2_RED_DOUBLE_POWER` 无目标付费技能入栈/结算代表路径，P4.74 已补来源对象 `cardNo` 身份校验，P4.87-P4.92 已补同一技能的对手控制来源、来源不在场上、携带目标、携带未支持 optional cost、费用不足和非《蔚》来源拒绝边界并纳入资源关键词聚合回放；P4.77 已执行《泽拉斯》`PAY_RED_EXHAUST_DAMAGE_3` 单目标技能敌方法盾税代表路径，P4.93 已补同一技能敌方法盾目标税 mana 不足拒绝 fixture，P4.78 已补同一技能己方法盾目标 no-tax 边界，P4.79 已补同一技能来源已横置时拒绝且不改状态的边界，P4.80 已补同一技能缺少目标时拒绝且不改状态的边界，P4.81 已补同一技能提供两个目标时拒绝且不改状态的边界，P4.82 已补同一技能携带未支持 optional cost 时拒绝且不改状态的边界，P4.83 已补非《泽拉斯》来源伪造同一 ability id 时拒绝且不改状态的边界，P4.84 已补同一技能选择非单位目标时拒绝且不改状态的边界，P4.85 已补同一技能来源不在战场时拒绝且不改状态，P4.86 已补同一技能来源由对手控制时拒绝且不改状态的边界，并把 P4.79-P4.86/P4.93 拒绝 fixtures 纳入资源关键词聚合回放；通用技能 registry、更多目标/可选费用技能和装备技能仍 deferred。
- `HIDE_CARD` 已有 P4.63 command envelope；P4.70 已执行 `STANDBY_A` 最小正面朝下放置；P4.94 已补同一路径费用不足拒绝 fixture；P4.72 已执行《游击战》`STANDBY_FREE` 免费暗置代表路径，P4.96 已补 `STANDBY_FREE` 无权限拒绝 fixture，其他目的地和完整隐藏区仍 deferred。
- 《游击战》P2 返回废牌堆待命牌路径已由 `p2-preflight-play-guerrilla-warfare-return-standby-graveyard` 覆盖；P4.102 已补非待命废牌堆目标拒绝 fixture，目标伤害、待命触发和完整隐藏区仍 deferred。
- `REVEAL_CARD` 已有 P4.68 command envelope；P4.71 已执行 `STANDBY_REVEAL` / `BASE` 最小显露，P4.76 已执行 `STANDBY_REACTION` / `STACK` 无目标反应入栈，P4.95 已补同一路径无优先权窗口拒绝 fixture；目标结算、触发和完整隐藏区仍 deferred。
- Face-down snapshot redaction 已有 P4.69 对手视角防泄漏测试，并被 P4.70 最小待命放置复用；当前仍不创建完整隐藏区或执行待命触发/目标伤害。
- `PLAY_CARD mode=AMBUSH` 已有 P4.64 `destination` envelope 与 Core 显式拒绝前置模型，P4.97 已补同一路径 conformance fixture，可供后续伏击反应战场打出小批次复用；当前不把单位打出至战场。
- `MOVE_UNIT` 已有 P4.65 command envelope 与 Core 显式拒绝前置模型，P4.98 已补同一路径 conformance fixture，可供后续游走/基础移动小批次复用；当前不移动对象。
- `ASSEMBLE_EQUIPMENT` 已有 P4.66 command envelope 与 Core 显式拒绝前置模型，P4.99 已补同一路径 conformance fixture，可供后续装配/灵便/百炼小批次复用；当前不贴附装备。
- `DECLARE_BATTLE` 已有 P4.67 command envelope 与 Core 显式拒绝前置模型，P4.100 已补同一路径 conformance fixture，可供后续强攻/坚守/壁垒/后排战斗小批次复用；当前不开战。
- 目标：证明 P3 `BehaviorSpec` / template skeleton 可以安全定位到现有 `CardBehaviorDefinition`，并在 P4.5 继续保持 `CoreRuleEngine` 主路径不变。

中风险，需要小模型后再接入可玩路径：

- 迅捷、反应；急速的《灼焰飞龙》`HASTE_READY` 代表路径已由 P4.13 接入，《小鲨鱼》`HASTE_READY` 第二代表路径已由 P4.18 接入，《军团后卫》`HASTE_READY` 第三代表路径已由 P4.20 接入，《金克丝》`HASTE_READY` 第三十三代表路径已由 P4.56 接入，《金克丝》A 版本 `HASTE_READY` 第三十四代表路径已由 P4.57 接入，《树根先生》`HASTE_READY` 第四代表路径已由 P4.25 接入，《机械迷》`HASTE_READY` 第五代表路径已由 P4.26 接入，《琢珥鱼》`HASTE_READY` 第六代表路径已由 P4.27 接入，《卡银娜·薇蕊泽》`HASTE_READY` 第七代表路径已由 P4.30 接入，《绯红印记树怪》`HASTE_READY` 第八代表路径已由 P4.31 接入，《绯红印记树怪》A 版本 `HASTE_READY` 第二十一代表路径已由 P4.44 接入，《尼菈》`HASTE_READY` 第二十二代表路径已由 P4.45 接入，《雷恩加尔》`HASTE_READY` 第二十三代表路径已由 P4.46 接入，《雷恩加尔》A 版本 `HASTE_READY` 第二十四代表路径已由 P4.47 接入，《厄运小姐》`HASTE_READY` 第二十五代表路径已由 P4.48 接入，《厄运小姐》A 版本 `HASTE_READY` 第二十六代表路径已由 P4.49 接入，《希维尔》`HASTE_READY` 第二十七代表路径已由 P4.50 接入，《希维尔》A 版本 `HASTE_READY` 第二十八代表路径已由 P4.51 接入，《莉莉娅》`HASTE_READY` 第二十九代表路径已由 P4.52 接入，《莉莉娅》A 版本 `HASTE_READY` 第三十代表路径已由 P4.53 接入，《阿兹尔》`HASTE_READY` 第三十一代表路径已由 P4.54 接入，《阿兹尔》A 版本 `HASTE_READY` 第三十二代表路径已由 P4.55 接入，《美味仙灵》`HASTE_READY` 第九代表路径已由 P4.32 接入，《艾克》`HASTE_READY` 第十代表路径已由 P4.33 接入，《武装强袭者》`HASTE_READY` 第十一代表路径已由 P4.34 接入，《远古战狂》`HASTE_READY` 第十二代表路径已由 P4.35 接入，《海妖猎手》`HASTE_READY` 第十三代表路径已由 P4.36 接入，《李青》`HASTE_READY` 第十四代表路径已由 P4.37 接入，《李青》A 版本 `HASTE_READY` 第十五代表路径已由 P4.38 接入，《千尾监视者》`HASTE_READY` 第十六代表路径已由 P4.39 接入，《卡莎》`HASTE_READY` 第十七代表路径已由 P4.40 接入，《雷克塞》`HASTE_READY` 第十八代表路径已由 P4.41 接入，《雷克塞》A 版本 `HASTE_READY` 第十九代表路径已由 P4.42 接入，《卡莎》A 版本 `HASTE_READY` 第二十代表路径已由 P4.43 接入，其他急速牌的彩色资源/活跃进场仍需后续小批次
- 瞬息到期、预知最小回收/no-recycle 分支与 P4.101 非顶部目标拒绝 fixture
- 回响复杂额外费用、授予回响和模式重复分支
- 法盾目标税的最小支付校验已由 P4.12 覆盖法术选择敌方场上对象；P4.59 已覆盖同一法术同时选择 `法盾` 与 `法盾2` 敌方目标时的费用聚合；P4.61 已覆盖友方法术选择己方 `法盾` 目标时不产生目标税；P4.62 已补激活技能 command 前置模型，P4.73 已执行《蔚》无目标付费技能 stack/结算代表路径，P4.74 已补来源对象 `cardNo` 身份校验，P4.77 已执行《泽拉斯》单目标技能对敌方法盾单位的目标税代表路径，P4.93 已补同一技能敌方法盾目标税 mana 不足拒绝 fixture，P4.78 已补同一技能选择己方法盾单位时不产生目标税，P4.79 已补同一技能来源已横置时拒绝且不改状态，P4.80 已补同一技能缺少目标时拒绝且不改状态，P4.81 已补同一技能提供两个目标时拒绝且不改状态，P4.82 已补同一技能携带未支持 optional cost 时拒绝且不改状态，P4.83 已补非《泽拉斯》来源伪造同一 ability id 时拒绝且不改状态，P4.84 已补同一技能选择非单位目标时拒绝且不改状态，P4.85 已补同一技能来源不在战场时拒绝且不改状态，P4.86 已补同一技能来源由对手控制时拒绝且不改状态；更多技能目标税、授予/静态法盾和完整 FAQ 细节仍需后续小批次
- 固定数值“打出时获得经验”已由 P4.10 接入；固定经验额外费用减费已由 P4.11 接入；《诺克萨斯新兵》鼓舞费用减免已由 P4.14 接入；《踏苔蜥》`等级3` 入场 +1/法盾已由 P4.15 接入；《风行狐》`等级3` 入场 +1/游走已由 P4.16 接入；《无极学徒》`等级6` 打出抽 1 已由 P4.17 接入；《易》`等级6` 法盾/游走已由 P4.28 接入，《易》A 版本 `等级6` 法盾/游走已由 P4.29 接入；《严厉军士》按友方场上单位数量获得经验已由 P4.19 接入；《崔法利求战者》鼓舞自增益已由 P4.21 接入；《危险二人组》鼓舞目标临时战力已由 P4.22 接入；《垃圾场小霸王》鼓舞弃 2 抽 2 已由 P4.23 接入；《先锋队长》鼓舞创建随从已由 P4.24 接入；经验激活技能、经验改变效果/目标范围、其他动态经验、其他等级分支、其他鼓舞效果仍需后续小批次

高风险，暂不进入 P4.1：

- 强攻、坚守、壁垒、后排的完整战斗承伤/战力修正；P4.6 只完成 profile 识别
- 游走的多战场合法移动和移动触发得分；P4.6 只完成 profile 识别
- 待命触发/完整隐藏区/目标伤害与伏击的 face-down/隐藏信息/反应翻开路径
- 装配、灵便、百炼的费用、未激活文本、自动贴附和 owner/controller 边界；P4.58 只把《取放自如》武装贴附/卸除作为代表路径纳入 P4，不泛化完整装备系统
- 绝念和其他离场触发队列

## P4 Part Plan

| Part | Status | Percentage | Notes |
|---|---|---:|---|
| P4.0 审计与状态文档 | Done | 100% | 本文件记录候选、统计、风险分层和下一批边界。 |
| P4.1 基础模板安全桥接 | Done | 100% | 新增 template delegation bridge，覆盖 draw/damage/destroy/stun/temp might，并拒绝未启用 `echo` route；不替换 `CoreRuleEngine`。 |
| P4.2 权限关键词最小模型 | Done | 100% | 迅捷/反应/急速 profile/timing model；`顺劈` spell-duel focus 可玩路径；反应/急速复用并锁定 P2 边界。 |
| P4.3 生命周期/资源低风险小批 | Done | 100% | `瞬息` 当前控制者开始阶段到期摧毁；`预知`/`绝念`/法盾目标税后续另拆。 |
| P4.4 互动关键词一小批 | Done | 100% | `回响` mana-only optional cost/repeat 显式模型；复杂回响、待命、伏击继续 deferred。 |
| P4.5 基础动作 executor 小批测试 | Done | 100% | `draw`/`damage`/`destroy`/`stun`/`temp_might` primitive plan；`move`/`recall` 继续 delegated to P2 handwritten；P4.60 后 `recycle`/`banish`/`boon` 也进入 template skeleton 并 delegated to P2。 |
| P4.6 完成审计与战斗关键词 profile | Done | 100% | 审计确认 P4 尚未完成；新增 `强攻`/`坚守`/`壁垒`/`后排`/`游走` profile，完整战斗执行继续 deferred。 |
| P4.7 资源关键词 profile | Done | 100% | 新增 `狩猎`/`等级`/`鼓舞`/`法盾` profile；P4.12/P4.14/P4.15/P4.16/P4.17/P4.21/P4.22/P4.23/P4.24/P4.28/P4.29/P4.59/P4.61/P4.62/P4.77/P4.78/P4.79/P4.80/P4.81/P4.82/P4.83/P4.84/P4.85/P4.86/P4.93 后法盾法术目标税、多目标法盾税聚合、友方目标 no-tax、`ACTIVATE_ABILITY` 前置模型、《泽拉斯》技能敌方法盾税、己方法盾 no-tax、已横置来源拒绝、缺目标拒绝、多目标拒绝、额外费用拒绝、来源身份拒绝、非单位目标拒绝、来源不在战场拒绝、对手控制来源拒绝、法盾税费用不足拒绝与 P4.79-P4.86/P4.93 聚合回放边界、《诺克萨斯新兵》鼓舞费用、《崔法利求战者》鼓舞自增益、《危险二人组》鼓舞目标临时战力、《垃圾场小霸王》鼓舞弃牌抽牌、《先锋队长》鼓舞创建随从、《踏苔蜥》《风行狐》《无极学徒》和《易》等级代表路径已接入，其余资源关键词分支继续 deferred。 |
| P4.8 装备关键词 profile | Done | 100% | 新增 `装配`/`灵便`/`百炼` profile；贴附、费用、自动贴附和 owner/controller 执行继续 deferred。 |
| P4.9 完成审计与剩余 profile 收口 | Done | 100% | 新增 lifecycle/interaction/basic-action profile，明确 P4 goal 尚未完全达成的 deferred 能力。 |
| P4.10 固定获得经验执行切片 | Done | 100% | 新增玩家经验状态、固定 `GainExperienceOnPlay` 执行和 3 条代表 fixture；P4.19 已补《严厉军士》动态计数经验，其他动态经验与经验消耗继续 deferred。 |
| P4.11 经验额外费用减费执行切片 | Done | 100% | 新增 `SPEND_EXPERIENCE:n` optional cost、波比代表 fixture 和经验不足拒绝测试；改变效果/目标的经验费用继续 deferred。 |
| P4.12 法盾目标税执行切片 | Done | 100% | 新增 `spellshieldTaxMana` 费用计划、`法盾`/`法盾N` 标签税值复用、代表 fixture 和费用不足拒绝测试；P4.104 已把《焚烧》敌方法盾目标税费用不足边界提升为 conformance fixture；P4.59 已补多目标聚合，P4.105 已把《妖异狐火》多目标法盾税费用不足边界提升为 conformance fixture；P4.61 已补友方法术目标 no-tax 边界，P4.62 已补技能 command 前置模型，P4.77 已补《泽拉斯》带目标技能敌方法盾税代表路径，P4.93 已补同一技能敌方法盾目标税 mana 不足拒绝 fixture，P4.78 已补同一技能己方法盾 no-tax 边界，P4.79 已补同一技能已横置来源拒绝边界，P4.80 已补同一技能缺目标拒绝边界，P4.81 已补同一技能多目标拒绝边界，P4.82 已补同一技能额外费用拒绝边界，P4.83 已补非《泽拉斯》来源伪造同一 ability id 的拒绝边界，P4.84 已补同一技能非单位目标拒绝边界，P4.85 已补同一技能来源不在战场拒绝边界，P4.86 已补同一技能对手控制来源拒绝边界，P4.93 已补同一技能敌方法盾目标税费用不足拒绝 fixture，并聚合回放拒绝 fixtures；更多技能目标税、授予/FAQ 全细节继续 deferred。 |
| P4.13 急速活跃可选费用切片 | Done | 100% | 新增 `HASTE_READY` 代表 optional cost、《灼焰飞龙》fixture 和 power 不足拒绝测试；P4.103 已把该拒绝边界提升为 conformance fixture；其他急速牌彩色资源/授予/战场联动继续 deferred。 |
| P4.14 鼓舞费用减免执行切片 | Done | 100% | 新增同回合已打出卡牌记忆、《诺克萨斯新兵》费用 -2 代表 fixture、无先前打牌记忆费用不足拒绝测试和回合结束清空测试；其他鼓舞效果继续 deferred。 |
| P4.15 等级入场修正执行切片 | Done | 100% | 新增 `LevelExperienceThreshold` 源单位入场修正、《踏苔蜥》`等级3` +1/法盾 fixture；其他等级费用/效果/技能分支继续 deferred。 |
| P4.16 等级游走入场修正执行切片 | Done | 100% | 复用 `LevelExperienceThreshold` 源单位入场修正，新增《风行狐》`等级3` +1/游走 fixture；游走移动和其他等级分支继续 deferred。 |
| P4.17 等级条件抽牌执行切片 | Done | 100% | 新增 `LevelDrawOnPlayCount` 源牌结算抽牌、《无极学徒》`等级6` 抽 1 fixture；狩猎经验和其他等级分支继续 deferred。 |
| P4.18 急速活跃第二代表切片 | Done | 100% | 复用 `HASTE_READY` optional cost，新增《小鲨鱼》fixture 和 profile/fixture 测试；彩色资源精确匹配和强攻战斗修正继续 deferred。 |
| P4.19 动态经验获得执行切片 | Done | 100% | 新增 `GainExperienceOnPlayPerFriendlyFieldUnit` 和《严厉军士》动态经验 fixture；战斗/移动触发经验和技能消耗继续 deferred。 |
| P4.20 急速活跃第三代表切片 | Done | 100% | 复用 `HASTE_READY` optional cost，新增《军团后卫》fixture 和 profile/fixture 测试；彩色资源精确匹配和其他急速分支继续 deferred。 |
| P4.21 鼓舞自增益执行切片 | Done | 100% | 新增打出时 `PlayedAfterAnotherCardThisTurn` 快照位、《崔法利求战者》鼓舞自增益 fixture 和 profile/fixture 测试；其他鼓舞分支继续 deferred。 |
| P4.22 鼓舞目标临时战力执行切片 | Done | 100% | 新增条件化目标数量、《危险二人组》鼓舞目标 +2 本回合战力 fixture、缺少目标拒绝测试和 profile/fixture 测试；其他鼓舞分支继续 deferred。 |
| P4.23 completion audit + 鼓舞弃牌抽牌执行切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增条件抽牌、《垃圾场小霸王》鼓舞弃 2 抽 2 fixture、缺少两个弃牌目标拒绝测试和 profile/fixture 测试。 |
| P4.24 completion audit + 鼓舞随从创建执行切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增条件 token 创建、《先锋队长》鼓舞创建两名 1 战力随从 fixture 和 profile/fixture 测试。 |
| P4.25 completion audit + 急速活跃第四代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《树根先生》`HASTE_READY` fixture 和 profile/fixture 测试，移动触发经验继续 deferred。 |
| P4.26 completion audit + 急速活跃第五代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《机械迷》`HASTE_READY` fixture 和 profile/fixture 测试，武装贴附/双倍基础战力继续 deferred。 |
| P4.27 completion audit + 急速活跃第六代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《琢珥鱼》`HASTE_READY` fixture 和 profile/fixture 测试，强力单位减费继续 deferred。 |
| P4.28 completion audit + 等级6 法盾/游走执行切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《易》`等级6` 法盾/游走 fixture 和 profile/fixture 测试，狩猎经验、法盾技能目标税和游走移动继续 deferred。 |
| P4.29 completion audit + 等级6 法盾/游走 A 版本切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《易》A 版本 `等级6` 法盾/游走 fixture 和 profile/fixture 测试，狩猎经验、法盾技能目标税和游走移动继续 deferred。 |
| P4.30 completion audit + 急速活跃第七代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《卡银娜·薇蕊泽》`HASTE_READY` fixture 和 profile/fixture 测试，移动触发和随从指示物创建继续 deferred。 |
| P4.31 completion audit + 急速活跃第八代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《绯红印记树怪》`HASTE_READY` fixture 和 profile/fixture 测试，征服额外触发、增益和技能目标税继续 deferred。 |
| P4.32 completion audit + 急速活跃第九代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《美味仙灵》`HASTE_READY` fixture 和 profile/fixture 测试，绝念召符文和抽牌触发继续 deferred。 |
| P4.33 completion audit + 急速活跃第十代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《艾克》`HASTE_READY` fixture 和 profile/fixture 测试，绝念回收和符文活跃化继续 deferred。 |
| P4.34 completion audit + 急速活跃第十一代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《武装强袭者》`HASTE_READY` fixture 和 profile/fixture 测试，百炼装配与武装贴附继续 deferred。 |
| P4.35 completion audit + 急速活跃第十二代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《远古战狂》`HASTE_READY` fixture 和 profile/fixture 测试，战场位置与动态强攻继续 deferred。 |
| P4.36 completion audit + 急速活跃第十三代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《海妖猎手》`HASTE_READY` fixture 和 profile/fixture 测试，强攻战斗修正与消耗增益减费继续 deferred。 |
| P4.37 completion audit + 急速活跃第十四代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《李青》`HASTE_READY` fixture 和 profile/fixture 测试，战场位置与友方增益单位静态修正继续 deferred。 |
| P4.38 completion audit + 急速活跃第十五代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《李青》A 版本 `HASTE_READY` fixture 和 profile/fixture 测试，战场位置与友方增益单位静态修正继续 deferred。 |
| P4.39 completion audit + 急速活跃第十六代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《千尾监视者》`HASTE_READY` fixture 和 profile/fixture 测试，全体临时战力修正继续复用 P2 手写行为，蓝色资源精确匹配继续 deferred。 |
| P4.40 completion audit + 急速活跃第十七代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《卡莎》`HASTE_READY` fixture 和 profile/fixture 测试，征服抽牌触发继续 deferred。 |
| P4.41 completion audit + 急速活跃第十八代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《雷克塞》`HASTE_READY` fixture 和 profile/fixture 测试，强攻战斗修正与非手牌打出授予急速继续 deferred。 |
| P4.42 completion audit + 急速活跃第十九代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《雷克塞》A 版本 `HASTE_READY` fixture 和 profile/fixture 测试，强攻战斗修正与非手牌打出授予急速继续 deferred。 |
| P4.43 completion audit + 急速活跃第二十代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《卡莎》A 版本 `HASTE_READY` fixture 和 profile/fixture 测试，征服抽牌触发继续 deferred。 |
| P4.44 completion audit + 急速活跃第二十一代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《绯红印记树怪》A 版本 `HASTE_READY` fixture 和 profile/fixture 测试，征服额外触发与征服后增益继续 deferred。 |
| P4.45 completion audit + 急速活跃第二十二代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《尼菈》`HASTE_READY` fixture 和 profile/fixture 测试，游走移动与移动触发经验继续 deferred。 |
| P4.46 completion audit + 急速活跃第二十三代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《雷恩加尔》`HASTE_READY` fixture 和 profile/fixture 测试，强攻战斗、法盾技能目标税与游走移动继续 deferred。 |
| P4.47 completion audit + 急速活跃第二十四代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《雷恩加尔》A 版本 `HASTE_READY` fixture 和 profile/fixture 测试，强攻战斗、法盾技能目标税与游走移动继续 deferred。 |
| P4.48 completion audit + 急速活跃第二十五代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《厄运小姐》`HASTE_READY` fixture 和 profile/fixture 测试，游走移动与移动触发活跃化继续 deferred。 |
| P4.49 completion audit + 急速活跃第二十六代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《厄运小姐》A 版本 `HASTE_READY` fixture 和 profile/fixture 测试，游走移动与移动触发活跃化继续 deferred。 |
| P4.50 completion audit + 急速活跃第二十七代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《希维尔》`HASTE_READY` fixture 和 profile/fixture 测试，万能符能计数与 +2/游走继续 deferred。 |
| P4.51 completion audit + 急速活跃第二十八代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《希维尔》A 版本 `HASTE_READY` fixture 和 profile/fixture 测试，万能符能计数与 +2/游走继续 deferred。 |
| P4.52 completion audit + 急速活跃第二十九代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《莉莉娅》`HASTE_READY` fixture 和 profile/fixture 测试，移动触发瞬息精灵继续 deferred。 |
| P4.53 completion audit + 急速活跃第三十代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《莉莉娅》A 版本 `HASTE_READY` fixture 和 profile/fixture 测试，移动触发瞬息精灵继续 deferred。 |
| P4.54 completion audit + 急速活跃第三十一代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《阿兹尔》`HASTE_READY` fixture 和 profile/fixture 测试，进攻触发指示物移动继续 deferred。 |
| P4.55 completion audit + 急速活跃第三十二代表切片 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《阿兹尔》A 版本 `HASTE_READY` fixture 和 profile/fixture 测试，进攻触发指示物移动继续 deferred。 |
| P4.56 completion audit + 金克丝急速弃牌分支 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《金克丝》`HASTE_READY` fixture、profile/fixture 测试，并让 no-optional 打出弃牌 fixture 记录 `急速` / `强攻2` 标签；金克丝 A 版本、红色资源精确匹配和强攻战斗修正继续 deferred。 |
| P4.57 completion audit + 金克丝 A 急速弃牌分支 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《金克丝》A 版本 `HASTE_READY` fixture、profile/fixture 测试，并让 no-optional 打出弃牌 fixture 记录 `急速` / `强攻2` 标签；红色资源精确匹配和强攻战斗修正继续 deferred。 |
| P4.58 completion audit + 装备贴附/卸除代表路径 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《取放自如》武装贴附/卸除 representative profile 和 P4 fixture 聚合测试，完整装配/灵便/百炼系统继续 deferred。 |
| P4.59 completion audit + 法盾多目标税聚合 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《妖异狐火》同时选择 `法盾` 与 `法盾2` 敌方目标的费用聚合 fixture、正向 payload 测试和费用不足拒绝测试，技能目标税继续 deferred。 |
| P4.60 completion audit + 回收/放逐/增益模板骨架 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `recycle`/`banish`/`boon` BehaviorTemplateIds、parser/registry/safe delegation tests，状态写入继续委托 P2。 |
| P4.61 completion audit + 法盾友方目标 no-tax | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《秘奥义！慈悲度魂落》指定己方 `法盾` 单位时 `spellshieldTaxMana = 0` 的 fixture 和 payload 测试，技能目标税继续 deferred。 |
| P4.62 completion audit + 激活技能 command 前置模型 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `ActivateAbilityCommand`、JSON mapper 支持和 Core 显式 `UNSUPPORTED_COMMAND`，为技能目标税/激活技能后续小批次提供安全协议边界。 |
| P4.63 completion audit + 待命 HIDE_CARD command 前置模型 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `HideCardCommand`、JSON mapper 支持和安全协议边界，P4.70 后已在该 envelope 上接入 `STANDBY_A` 最小放置。 |
| P4.64 completion audit + 伏击 PLAY_CARD 目的地前置模型 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `PlayCardCommand.Destination`、JSON mapper 支持和 `mode=AMBUSH` Core 显式 `UNSUPPORTED_COMMAND`，为伏击反应战场打出后续小批次提供安全协议边界。 |
| P4.65 completion audit + 游走 MOVE_UNIT command 前置模型 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `MoveUnitCommand`、JSON mapper 支持和 Core 显式 `UNSUPPORTED_COMMAND`，为游走/基础移动后续小批次提供安全协议边界。 |
| P4.66 completion audit + 装备 ASSEMBLE_EQUIPMENT command 前置模型 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `AssembleEquipmentCommand`、JSON mapper 支持和 Core 显式 `UNSUPPORTED_COMMAND`，为装配/灵便/百炼后续小批次提供安全协议边界。 |
| P4.67 completion audit + 战斗 DECLARE_BATTLE command 前置模型 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `DeclareBattleCommand`、JSON mapper 支持和 Core 显式 `UNSUPPORTED_COMMAND`，为强攻/坚守/壁垒/后排战斗后续小批次提供安全协议边界。 |
| P4.68 completion audit + 待命 REVEAL_CARD command 前置模型 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `RevealCardCommand`、JSON mapper 支持和安全协议边界，P4.71 后已在该 envelope 上接入 `STANDBY_REVEAL` 最小显露。 |
| P4.69 completion audit + 正面朝下 snapshot redaction | Done | 100% | 审计确认 P4 仍不能标记 goal complete；对手视角的正面朝下对象只暴露 `objectId` / `isFaceDown`，不泄漏战力、标签或费用；拥有者视角仍可见完整状态。 |
| P4.70 completion audit + 待命 HIDE_CARD 最小放置执行 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；`HIDE_CARD destination=STANDBY optionalCosts=["STANDBY_A"]` 支付 1 mana、把待命单位手牌放到基地并设为正面朝下，事件和对手 snapshot 不泄漏卡面信息。 |
| P4.71 completion audit + 待命 REVEAL_CARD 最小显露执行 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；`REVEAL_CARD mode=STANDBY_REVEAL destination=BASE optionalCosts=["STANDBY_REVEAL_0"]` 把基地正面朝下待命单位翻为公开，目标结算仍 deferred。 |
| P4.72 completion audit + 游击战免费待命暗置权限 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；《游击战》结算后授予 `FREE_STANDBY_HIDE:{playerId}` 本回合效果，`HIDE_CARD optionalCosts=["STANDBY_FREE"]` 在该效果存在时支付 0 点费用并复用最小待命暗置路径；待命触发/完整隐藏区仍 deferred。 |
| P4.73 completion audit + 蔚激活技能最小入栈执行 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《蔚》`PAY_2_RED_DOUBLE_POWER` 无目标付费技能入栈/结算 fixture、正向入栈测试、目标拒绝测试和资源不足拒绝测试，技能目标税/通用技能仍 deferred。 |
| P4.74 completion audit + 场上对象 cardNo 身份边界 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `CardObjectState.CardNo`、fixture 读写/断言、打出/暗置/显露源牌身份保存，以及《蔚》技能非《蔚》来源拒绝和先打出后激活 fixture。 |
| P4.75 completion audit + 目标覆盖审计测试 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `P4ObjectiveNamedSurfacesHaveRepresentativeCoverage`，把目标中 33 个显式关键词/基础动作 surface 映射到现有 profile、primitive、delegated P2 或代表执行证据。 |
| P4.76 completion audit + 待命反应入栈执行 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `STANDBY_REACTION` / `STACK` 无目标 0 费用反应入栈路径、正向/拒绝 engine tests 和 `p4-reveal-card-standby-reaction-stack` fixture，待命触发/完整隐藏区/目标伤害仍 deferred。 |
| P4.77 completion audit + 泽拉斯技能目标税 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》`PAY_RED_EXHAUST_DAMAGE_3` 带目标技能入栈/横置/法盾目标税/3 点伤害 fixture、正向入栈测试和费用不足拒绝测试，P4.93 已补敌方法盾目标税费用不足拒绝 fixture，P4.78 已补己方法盾 no-tax 边界，P4.79 已补已横置来源拒绝边界，P4.80 已补缺目标拒绝边界，P4.81 已补多目标拒绝边界，P4.82 已补额外费用拒绝边界，P4.83 已补来源身份拒绝边界，P4.84 已补非单位目标拒绝边界，P4.85 已补来源不在战场拒绝边界，P4.86 已补对手控制来源拒绝边界，通用技能 registry 和更多技能目标税仍 deferred。 |
| P4.78 completion audit + 泽拉斯友方法盾 no-tax | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》同一带目标技能选择己方 `法盾` 单位时 `spellshieldTaxMana = 0` 的入栈/结算 fixture 和直接测试，P4.79 已补已横置来源拒绝边界，P4.80 已补缺目标拒绝边界，P4.81 已补多目标拒绝边界，P4.82 已补额外费用拒绝边界，P4.83 已补来源身份拒绝边界，P4.84 已补非单位目标拒绝边界，P4.85 已补来源不在战场拒绝边界，P4.86 已补对手控制来源拒绝边界，仍只覆盖已验证边界。 |
| P4.79 completion audit + 泽拉斯已横置来源拒绝 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》同一带目标技能来源已横置时拒绝且不推进 tick/事件/资源/stack 的直接测试和 fixture，P4.80 已补缺目标拒绝边界，P4.81 已补多目标拒绝边界，P4.82 已补额外费用拒绝边界，P4.83 已补来源身份拒绝边界，P4.84 已补非单位目标拒绝边界，P4.85 已补来源不在战场拒绝边界，P4.86 已补对手控制来源拒绝边界，仍不进入通用技能 registry。 |
| P4.80 completion audit + 泽拉斯缺目标拒绝 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》同一带目标技能缺少“一名单位”目标时拒绝且不推进 tick/事件/资源/横置/stack 的直接测试和 fixture，P4.81 已补多目标拒绝边界，P4.82 已补额外费用拒绝边界，P4.83 已补来源身份拒绝边界，P4.84 已补非单位目标拒绝边界，P4.85 已补来源不在战场拒绝边界，P4.86 已补对手控制来源拒绝边界，仍不进入通用技能 registry。 |
| P4.81 completion audit + 泽拉斯多目标拒绝 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》同一带目标技能提供两个目标时拒绝且不推进 tick/事件/资源/横置/目标伤害/stack 的直接测试和 fixture，P4.82 已补额外费用拒绝边界，P4.83 已补来源身份拒绝边界，P4.84 已补非单位目标拒绝边界，P4.85 已补来源不在战场拒绝边界，P4.86 已补对手控制来源拒绝边界，仍不进入通用技能 registry。 |
| P4.82 completion audit + 泽拉斯额外费用拒绝 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》同一带目标技能携带未支持 optional cost 时拒绝且不推进 tick/事件/资源/横置/目标伤害/stack 的直接测试和 fixture，P4.83 已补来源身份拒绝边界，P4.84 已补非单位目标拒绝边界，P4.85 已补来源不在战场拒绝边界，P4.86 已补对手控制来源拒绝边界，仍不进入通用技能 registry。 |
| P4.83 completion audit + 泽拉斯来源身份拒绝 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》同一带目标技能由非《泽拉斯》来源伪造 ability id 时拒绝且不推进 tick/事件/资源/横置/目标伤害/stack 的直接测试和 fixture，P4.84 已补非单位目标拒绝边界，P4.85 已补来源不在战场拒绝边界，P4.86 已补对手控制来源拒绝边界，仍不进入通用技能 registry。 |
| P4.84 completion audit + 泽拉斯非单位目标拒绝 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》同一带目标技能选择场上装备等非单位目标时拒绝且不推进 tick/事件/资源/横置/stack 的直接测试和 fixture，P4.85 已补来源不在战场拒绝边界，P4.86 已补对手控制来源拒绝边界；仍不进入通用技能 registry。 |
| P4.85 completion audit + 泽拉斯来源战场位置拒绝 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》同一带目标技能来源位于基地而非战场时拒绝且不推进 tick/事件/资源/横置/目标伤害/stack 的直接测试和 fixture，并把 P4.79-P4.85 拒绝 fixtures 纳入资源关键词聚合回放；仍不进入通用技能 registry。 |
| P4.86 completion audit + 泽拉斯对手控制来源拒绝 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》同一带目标技能来源由对手控制时拒绝且不推进 tick/事件/资源/横置/目标伤害/stack 的直接测试和 fixture，并把 P4.79-P4.86 拒绝 fixtures 纳入资源关键词聚合回放；仍不进入通用技能 registry。 |
| P4.87 completion audit + 蔚对手控制来源拒绝 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《蔚》无目标付费技能来源由对手控制时拒绝且不推进 tick/事件/资源/战力/stack 的直接测试和 fixture，并把该 fixture 纳入资源关键词聚合回放；仍不进入通用技能 registry。 |
| P4.88 completion audit + 蔚来源区域拒绝 | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《蔚》无目标付费技能来源仍在手牌、未进入场上时拒绝且不推进 tick/事件/资源/战力/stack 的直接测试和 fixture，并把该 fixture 纳入资源关键词聚合回放；仍不进入通用技能 registry。 |
| P4.89 completion audit + 蔚带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《蔚》无目标付费技能携带目标时拒绝且不推进 tick/事件/资源/战力/目标/stack 的 fixture，并把该 fixture 纳入资源关键词聚合回放；仍不进入通用技能 registry。 |
| P4.90 completion audit + 蔚额外费用拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《蔚》无目标付费技能携带未支持 optional cost 时拒绝且不推进 tick/事件/资源/战力/stack 的直接测试和 fixture，并把该 fixture 纳入资源关键词聚合回放；仍不进入通用技能 registry。 |
| P4.91 completion audit + 蔚费用不足拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《蔚》无目标付费技能缺少 power 费用时拒绝且不推进 tick/事件/资源/战力/stack 的 fixture，并把该 fixture 纳入资源关键词聚合回放；仍不进入通用技能 registry。 |
| P4.92 completion audit + 蔚来源身份拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增非《蔚》来源伪造 `PAY_2_RED_DOUBLE_POWER` 时拒绝且不推进 tick/事件/资源/战力/stack 的 fixture，并把该 fixture 纳入资源关键词聚合回放；仍不进入通用技能 registry。 |
| P4.93 completion audit + 泽拉斯法盾税费用不足 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》同一带目标技能选择敌方法盾单位但目标税 mana 不足时拒绝且不推进 tick/事件/power/横置/目标伤害/stack 的 fixture，并把该 fixture 纳入资源关键词聚合回放；仍不进入通用技能 registry。 |
| P4.94 completion audit + 待命暗置费用不足 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `HIDE_CARD destination=STANDBY optionalCosts=["STANDBY_A"]` mana 不足时拒绝且不推进 tick/事件/手牌/基地/stack 的 fixture，并把该 fixture 纳入互动关键词聚合回放；仍不进入完整隐藏区或待命触发。 |
| P4.95 completion audit + 待命反应无优先权拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `REVEAL_CARD mode=STANDBY_REACTION destination=STACK optionalCosts=["STANDBY_REVEAL_0"]` 在无优先权窗口时拒绝且不推进 tick/事件/正面朝下状态/基地/stack 的 fixture，并把该 fixture 纳入互动关键词聚合回放；仍不进入待命目标结算或完整隐藏区。 |
| P4.96 completion audit + 免费待命暗置无权限拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `HIDE_CARD destination=STANDBY optionalCosts=["STANDBY_FREE"]` 在没有 `FREE_STANDBY_HIDE:{playerId}` 效果时拒绝且不推进 tick/事件/手牌/基地/stack 的 fixture，并把该 fixture 纳入互动关键词聚合回放；仍不进入完整隐藏区或待命触发。 |
| P4.97 completion audit + 伏击前置拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `PLAY_CARD mode=AMBUSH destination=BATTLEFIELD:P1-MAIN` 在伏击真实反应战场打出模型完成前显式拒绝且不推进 tick/事件/费用/手牌/战场/stack 的 fixture，并把该 fixture 纳入互动关键词聚合回放；仍不进入伏击战场打出。 |
| P4.98 completion audit + 游走移动前置拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `MOVE_UNIT origin=BATTLEFIELD:P1-LEFT destination=BATTLEFIELD:P1-RIGHT optionalCosts=["ROAM"]` 在真实移动模型完成前显式拒绝且不推进 tick/事件/费用/对象位置/战力/stack 的 fixture，并把该 fixture 纳入战斗关键词和基础动作聚合回放；仍不进入游走真实移动。 |
| P4.99 completion audit + 装备装配前置拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `ASSEMBLE_EQUIPMENT optionalCosts=["ASSEMBLE_RED"]` 在真实装备装配模型完成前显式拒绝且不推进 tick/事件/费用/基地对象/attachedToObjectId/stack 的 fixture，并把该 fixture 纳入装备关键词聚合回放；仍不进入完整装备系统。 |
| P4.100 completion audit + 战斗声明前置拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 `DECLARE_BATTLE optionalCosts=["COMBAT_ASSIGNMENT"]` 在真实战斗模型完成前显式拒绝且不推进 tick/事件/攻防状态/战力/对象位置/stack 的 fixture，并把该 fixture 纳入战斗关键词聚合回放；仍不进入完整战斗系统。 |
| P4.101 completion audit + 预知非顶部目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《占卜贝壳》`预知` 选择非顶部主牌堆牌时拒绝且不推进 tick/事件/费用/手牌/主牌堆/stack 的 fixture，并把该 fixture 纳入生命周期关键词聚合回放；仍不进入广义预知授予或隐藏信息 UI。 |
| P4.102 completion audit + 游击战非待命目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《游击战》选择己方废牌堆非 `待命` 目标时拒绝且不推进 tick/事件/费用/手牌/废牌堆/stack 的 fixture，并把该 fixture 纳入互动关键词聚合回放；仍不进入完整隐藏区或待命触发。 |
| P4.103 completion audit + 急速 power 不足拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《灼焰飞龙》`HASTE_READY` 缺少 power 费用时拒绝且不推进 tick/事件/符文池/手牌/stack 的 fixture，并把该 fixture 纳入权限关键词聚合回放；仍不进入彩色资源精确匹配或战场联动。 |
| P4.104 completion audit + 焚烧法盾税费用不足 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《焚烧》选择敌方 `法盾` 单位但无法支付目标税时拒绝且不推进 tick/事件/符文池/手牌/目标伤害/stack 的 fixture，并把该 fixture 纳入资源关键词聚合回放；仍不进入更多技能目标税或授予/静态法盾。 |
| P4.105 completion audit + 妖异狐火多目标法盾税费用不足 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《妖异狐火》同时选择敌方 `法盾` 与 `法盾2` 单位但无法支付聚合目标税时拒绝且不推进 tick/事件/符文池/手牌/目标区域/stack 的 fixture，并把该 fixture 纳入资源关键词聚合回放；仍不进入更多技能目标税或授予/静态法盾。 |
| P4.106 completion audit + 妖异狐火总战力拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《妖异狐火》选择 2+3 战力目标、超过总战力 4 上限时拒绝且不推进 tick/事件/符文池/手牌/目标区域/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入多战场位置或复杂摧毁替代边界。 |
| P4.107 completion audit + 顽皮触手总战力拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《顽皮触手》选择 4+5 战力敌方战场目标、超过总战力 8 上限时拒绝且不推进 tick/事件/符文池/手牌/目标区域/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入多战场位置、同一位置目的地或真实移动系统。 |
| P4.108 completion audit + 狩魂目标战力拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《狩魂》选择 4 战力战场目标、超过“不高于 3 战力”上限时拒绝且不推进 tick/事件/符文池/手牌/目标区域/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入复杂摧毁替代或全卡批量迁移。 |
| P4.109 completion audit + 罡风目标战力拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《罡风》选择 4 战力战场目标、超过“不高于 3 战力”上限时拒绝且不推进 tick/事件/符文池/手牌/目标区域/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入完整目的地选择或复杂控制权/所属者模型。 |
| P4.110 completion audit + 巧取豪夺离场目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《巧取豪夺》选择敌方废牌堆单位牌时拒绝且不推进 tick/事件/符文池/手牌/废牌堆/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入目标控制者交互 UI 或全卡批量迁移。 |
| P4.111 completion audit + 致命华彩友方目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《致命华彩》选择友方单位时拒绝且不推进 tick/事件/符文池/手牌/基地/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入金币装备指示物或延迟触发。 |
| P4.112 completion audit + 血钱目标战力拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《血钱》选择 3 战力战场目标、超过“不高于 2 战力”上限时拒绝且不推进 tick/事件/符文池/手牌/战场/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入金币资源技能或装备系统扩展。 |
| P4.113 completion audit + 惩戒基地目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《惩戒》选择基地单位、违反“战场上的一名单位”目标要求时拒绝且不推进 tick/事件/符文池/手牌/基地/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入替代效果扩展或全卡批量迁移。 |
| P4.114 completion audit + 扑咚非进攻方目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《扑咚！》选择非进攻方单位、违反“一名进攻方单位”目标要求时拒绝且不推进 tick/事件/符文池/手牌/战场/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入完整战斗声明或进攻状态生成。 |
| P4.115 completion audit + 天顶之刃基地目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《天顶之刃》选择敌方基地单位、违反“战场上的一名敌方单位”目标要求时拒绝且不推进 tick/事件/符文池/手牌/基地/战场/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入可选移动或完整战场位置。 |
| P4.116 completion audit + 天顶之刃友方目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《天顶之刃》选择友方战场单位、违反“一名敌方单位”目标要求时拒绝且不推进 tick/事件/符文池/手牌/战场/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入可选移动或完整战场位置。 |
| P4.117 completion audit + 狂风绝息斩基地目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《狂风绝息斩》第二目标选择敌方基地单位、违反“战场上的一名敌方单位”目标要求时拒绝且不推进 tick/事件/符文池/手牌/基地/战场/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入完整战斗或目标顺序泛化。 |
| P4.118 completion audit + 狂风绝息斩目标顺序拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《狂风绝息斩》先选敌方战场单位、再选友方单位时违反目标顺序而拒绝且不推进 tick/事件/符文池/手牌/基地/战场/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；仍不进入完整战斗或自动目标重排。 |
| P4.119 completion audit + 聚合变异敌方目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《聚合变异》第二目标选择敌方单位、违反“另一名友方单位”目标要求时拒绝且不推进 tick/事件/符文池/手牌/基地/战场/战力/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放；重复目标拒绝仍由直接测试覆盖。 |
| P4.120 completion audit + 聚合变异重复目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《聚合变异》重复选择同一友方单位、违反“另一名友方单位”目标要求时拒绝且不推进 tick/事件/符文池/手牌/基地/战场/战力/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.121 completion audit + 存在焦虑友方进攻目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《存在焦虑》选择正在进攻的友方单位、违反“正在进攻的敌方单位”目标要求时拒绝且不推进 tick/事件/符文池/手牌/战场/眩晕/stack 的 fixture，并把该 fixture 纳入互动关键词聚合回放。 |
| P4.122 completion audit + 霹天雳地费用不足拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《霹天雳地》没有足够法力支付所需费用时拒绝且不推进 tick/事件/符文池/手牌/战场/伤害/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.123 completion audit + 御衡守念费用不足拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《御衡守念》未满足对手得分减费条件且无法支付未降低费用时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/符文牌堆/基地/废牌堆/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.124 completion audit + 透体圣光重复目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《透体圣光》重复选择同一战场单位时拒绝且不推进 tick/事件/符文池/手牌/战场/目标伤害/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.125 completion audit + 风箱炎息第四目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《风箱炎息》选择第四个单位目标时拒绝且不推进 tick/事件/符文池/手牌/双方战场/目标伤害/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.126 completion audit + 烈火风暴显式单位目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《烈火风暴》携带显式单位目标时拒绝且不推进 tick/事件/符文池/手牌/战场/目标伤害/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.127 completion audit + 新月打击友方目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《新月打击》选择友方战场单位时拒绝且不推进 tick/事件/符文池/手牌/双方战场/目标伤害/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.128 completion audit + 换换乐重复目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《换换乐》重复选择同一战场单位时拒绝且不推进 tick/事件/符文池/手牌/双方战场/目标战力/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.129 completion audit + 换换乐基地目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《换换乐》选择基地单位和战场单位时拒绝且不推进 tick/事件/符文池/手牌/基地/双方战场/目标战力/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.130 completion audit + 新月打击基地目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《新月打击》选择敌方基地单位时拒绝且不推进 tick/事件/符文池/手牌/基地/双方战场/目标伤害/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.131 completion audit + 风箱炎息重复目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《风箱炎息》重复选择同一单位目标时拒绝且不推进 tick/事件/符文池/手牌/双方战场/目标伤害/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.132 completion audit + 台前作秀回响费用不足拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《台前作秀》支付 `ECHO` 但总费用不足时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/stack 的 fixture，并把该 fixture 纳入回响关键词和互动关键词聚合回放。 |
| P4.133 completion audit + 惩戒非法回响拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增非回响法术《惩戒》携带 `ECHO` optional cost 时拒绝且不推进 tick/事件/符文池/手牌/战场/目标伤害/stack 的 fixture，并把该 fixture 纳入回响关键词和互动关键词聚合回放。 |
| P4.134 completion audit + 火箭轰击缺失模式拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《火箭轰击》未提供 `PLAY_CARD.mode`、违反“选择一个效果”要求时拒绝且不推进 tick/事件/符文池/手牌/基地/目标伤害/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.135 completion audit + 火箭轰击摧毁装备单位目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《火箭轰击》`DESTROY_EQUIPMENT` 模式指定单位目标时拒绝且不推进 tick/事件/符文池/手牌/基地/目标状态/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.136 completion audit + 紧急召回单位目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《紧急召回》指定单位目标时拒绝且不推进 tick/事件/符文池/手牌/基地/目标状态/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.137 completion audit + 魄罗佳肴带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《魄罗佳肴》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/战场目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.138 completion audit + 舒瑞娅的安魂曲带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《舒瑞娅的安魂曲》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标休眠状态/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.139 completion audit + 未来熔炉带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《未来熔炉》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.140 completion audit + 废料堆带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《废料堆》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.141 completion audit + 精灵提灯带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《精灵提灯》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.142 completion audit + 地沟区地图带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《地沟区地图》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.143 completion audit + 占卜花朵带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《占卜花朵》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.144 completion audit + 魔法鲜豆带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《魔法鲜豆》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.145 completion audit + 钢铁弩炮带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《钢铁弩炮》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.146 completion audit + 玄冰之心带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《玄冰之心》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.147 completion audit + 懊悔法球带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《懊悔法球》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.148 completion audit + 灵魂之剑带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《灵魂之剑》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.149 completion audit + 锯齿短匕带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《锯齿短匕》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.150 completion audit + 多兰之盾带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《多兰之盾》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.151 completion audit + 海克斯注力刚壁带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《海克斯注力刚壁》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.152 completion audit + 多兰之刃带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《多兰之刃》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.153 completion audit + 多兰之戒带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《多兰之戒》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.154 completion audit + 行军号令敌方基地第二目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《行军号令》选择敌方基地单位作为第二目标时拒绝且不推进 tick/事件/符文池/手牌/友方战场单位/敌方基地单位/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.155 completion audit + 决斗目标顺序反转拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《决斗》先敌方后友方目标顺序时拒绝且不推进 tick/事件/符文池/手牌/双方战场单位/双方单位伤害/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.156 completion audit + 战斗号令目标顺序反转拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《战斗号令》先对手单位后友方单位目标顺序时拒绝且不推进 tick/事件/符文池/手牌/双方基地与战场单位/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.157 completion audit + 虚空来袭目标顺序反转拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《虚空来袭》先敌方单位后友方单位目标顺序时拒绝且不推进 tick/事件/符文池/手牌/双方基地与战场单位/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.158 completion audit + 先锋之眼带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《先锋之眼》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.159 completion audit + 反曲之弓带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《反曲之弓》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.160 completion audit + 长剑带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《长剑》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.161 completion audit + 布甲带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《布甲》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.162 completion audit + 斯特拉克的挑战护手带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《斯特拉克的挑战护手》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.163 completion audit + 旋转飞斧带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《旋转飞斧》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.164 completion audit + 牧人的传家宝带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《牧人的传家宝》带目标打出时拒绝且不推进 tick/事件/经验/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.165 completion audit + 残暴之力带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《残暴之力》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.166 completion audit + 守护天使带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《守护天使》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.167 completion audit + 海克斯饮魔刀带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《海克斯饮魔刀》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.168 completion audit + 狂徒铠甲带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《狂徒铠甲》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.169 completion audit + 三相之力带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《三相之力》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.170 completion audit + 轻灵之靴带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《轻灵之靴》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.171 completion audit + 萃取带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《萃取》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.172 completion audit + 神圣剪刀带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《神圣剪刀》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.173 completion audit + 暴风大剑带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《暴风大剑》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.174 completion audit + 云游图鉴带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《云游图鉴》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.175 completion audit + 阿瑞昂的陨落带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《阿瑞昂的陨落》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.176 completion audit + 猎人的宽刃刀带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《猎人的宽刃刀》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.177 completion audit + 枯萎战斧带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《枯萎战斧》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.178 completion audit + 碎骨棒带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《碎骨棒》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.179 completion audit + 远古簇碑带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《远古簇碑》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.180 completion audit + 海克斯异常体带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《海克斯异常体》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.181 completion audit + 能量通道带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《能量通道》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.182 completion audit + 预时之门带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《预时之门》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.183 completion audit + 邪鸦魔典带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《邪鸦魔典》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.184 completion audit + 太阳圆盘带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《太阳圆盘》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.185 completion audit + 远见面具带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《远见面具》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.186 completion audit + 烈阳圣坛带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《烈阳圣坛》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.187 completion audit + 炼金科技桶带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《炼金科技桶》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.188 completion audit + 灵魂之轮带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《灵魂之轮》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.189 completion audit + 蘑菇袋带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《蘑菇袋》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.190 completion audit + 竞技场酒吧带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《竞技场酒吧》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.191 completion audit + 海盗避风港带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《海盗避风港》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.192 completion audit + 被遗忘的路标带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《被遗忘的路标》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.193 completion audit + 冰封宝石带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《冰封宝石》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.194 completion audit + 倾颓宫殿带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《倾颓宫殿》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.195 completion audit + 猩红玫瑰带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《猩红玫瑰》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.196 completion audit + 逆转碎片带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《逆转碎片》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.197 completion audit + 装配架带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《装配架》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.198 completion audit + 斯弗尔尚歌带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《斯弗尔尚歌》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.199 completion audit + Z型驱动带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《Z型驱动》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.200 completion audit + 先锋军备带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《先锋军备》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.201 completion audit + 追忆祭坛带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《追忆祭坛》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.202 completion audit + 暴怒之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《暴怒之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.203 completion audit + 专注之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《专注之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.204 completion audit + 洞察之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《洞察之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.205 completion audit + 力量之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《力量之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.206 completion audit + 不和之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《不和之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.207 completion audit + 团结之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《团结之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.208 completion audit + OGN 暴怒之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN 版《暴怒之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.209 completion audit + OGN 专注之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN 版《专注之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.210 completion audit + OGN 洞察之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN 版《洞察之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.211 completion audit + OGN 力量之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN 版《力量之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.212 completion audit + OGN 不和之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN 版《不和之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.213 completion audit + OGN 团结之印带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN 版《团结之印》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.214 completion audit + 奇妙行囊带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《奇妙行囊》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.215 completion audit + 塞壬号带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《塞壬号》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.216 completion audit + 无主宝藏带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《无主宝藏》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.217 completion audit + 拾荒小能手带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《拾荒小能手》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.218 completion audit + 雾临剑冢带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《雾临剑冢》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.219 completion audit + 闪耀极光带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《闪耀极光》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.220 completion audit + 烈阳徽记带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《烈阳徽记》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.221 completion audit + 先锋之盔带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《先锋之盔》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.222 completion audit + 蜜糖果实带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《蜜糖果实》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.223 completion audit + 临终仪式带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《临终仪式》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.224 completion audit + 破败王者之刃带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《破败王者之刃》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.225 completion audit + 来路不明的武器带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《来路不明的武器》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.226 completion audit + 海兽钓钩带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《海兽钓钩》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.227 completion audit + 禁魔石丰碑带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《禁魔石丰碑》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.228 completion audit + 中娅沙漏带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《中娅沙漏》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.229 completion audit + 夜之锋刃带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《夜之锋刃》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.230 completion audit + 炉火斗篷带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《炉火斗篷》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.231 completion audit + 灭世者的死亡之冠带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《灭世者的死亡之冠》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.232 completion audit + 喷射球果带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《喷射球果》当前 no-move 路径带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.233 completion audit + 夺命名单带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《夺命名单》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.234 completion audit + 受诅咒的石棺带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《受诅咒的石棺》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/废牌堆/放逐区/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.235 completion audit + promo 编号碎骨棒带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 promo 编号《碎骨棒》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.236 completion audit + 海克斯科技护手带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《海克斯科技护手》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack 的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.237 completion audit + 宝藏魔像带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《宝藏魔像》带目标打出时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不创建金币的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.238 completion audit + 泽拉斯普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《泽拉斯》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.239 completion audit + 龙魂贤者普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《龙魂贤者》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.240 completion audit + 绵绵魄罗普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增《绵绵魄罗》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.241 completion audit + SFD·088 烈娜塔普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 SFD·088《烈娜塔·戈拉斯克》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.242 completion audit + SFD·088a 烈娜塔普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 SFD·088a《烈娜塔·戈拉斯克》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.243 completion audit + OGN·028 德莱文普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·028《德莱文》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.244 completion audit + SFD·110 菲奥娜普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 SFD·110《菲奥娜》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.245 completion audit + SFD·110a 菲奥娜普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 SFD·110a《菲奥娜》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.246 completion audit + SFD·141 艾瑞莉娅普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 SFD·141《艾瑞莉娅》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.247 completion audit + SFD·141a 艾瑞莉娅普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 SFD·141a《艾瑞莉娅》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.248 completion audit + OGN·140 唤龙使者普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·140《唤龙使者》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.249 completion audit + OGN·055 驭水者普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·055《驭水者》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.250 completion audit + OGN·065 睿智长者普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·065《睿智长者》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.251 completion audit + OGN·084 踊跃的学徒普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·084《踊跃的学徒》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.252 completion audit + OGN·091 竞技场勤务小队普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·091《竞技场勤务小队》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.253 completion audit + OGN·061 魄罗牧者普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·061《魄罗牧者》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.254 completion audit + OGN·103 拉文布鲁姆学生普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·103《拉文布鲁姆学生》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.255 completion audit + OGN·118 残响之魂普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·118《残响之魂》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.256 completion audit + OGN·125 比尔吉沃特恶霸普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·125《比尔吉沃特恶霸》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.257 completion audit + OGN·130 神射海盗普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·130《神射海盗》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.258 completion audit + OGN·131 沙丘亚龙普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·131《沙丘亚龙》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.259 completion audit + OGN·167 余火修士普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·167《余火修士》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.260 completion audit + OGN·177 隐秘追踪者普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·177《隐秘追踪者》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.261 completion audit + OGN·178 卧底特工普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·178《卧底特工》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.262 completion audit + OGN·185 旅行商人普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·185《旅行商人》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.263 completion audit + OGN·190 克格莫普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·190《克格莫》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.264 completion audit + OGN·222 诺克萨斯鼓手普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·222《诺克萨斯鼓手》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.265 completion audit + OGN·199 控潮者普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·199《控潮者》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.266 completion audit + OGN·202 金克丝普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·202《金克丝》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.267 completion audit + OGN·226 幽灵主母普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·226《幽灵主母》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.268 completion audit + OGN·230 阿不思·菲罗斯普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·230《阿不思·菲罗斯》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.269 completion audit + OGN·236 卡尔萨斯普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 OGN·236《卡尔萨斯》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.270 completion audit + SFD·027 穿沙角兽普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 SFD·027《穿沙角兽》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.271 completion audit + SFD·035 幽径守卫普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 SFD·035《幽径守卫》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.272 completion audit + SFD·041 学徒铁匠普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 SFD·041《学徒铁匠》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.273 completion audit + SFD·047 山猿老祖普通打出带目标拒绝 fixture | Done | 100% | 审计确认 P4 仍不能标记 goal complete；新增 SFD·047《山猿老祖》普通手牌打出带目标时拒绝且不推进 tick/事件/符文池/手牌/主牌堆/基地目标/stack、不入场单位的 fixture，并把该 fixture 纳入基础动作聚合回放。 |
| P4.274 next low-risk gap | Pending | 0% | 基于 P4.273 audit 继续选择低风险可验证小批次；优先从待命/伏击边界、技能边界、更多法盾边界或基础动作目标合法性中选一项，仍不进入 P5/P6/P7。 |

P4 当前整体进度：按当前 part 计 `274/275 = 99.6%`。已完成 P4.1-P4.273：template delegation/primitive plan、权限关键词代表时机、瞬息到期、回响 mana-only 与费用不足/非法回响拒绝、战斗/资源/装备/生命周期/互动/basic-action profile、固定/动态经验、经验费用、法盾法术目标税、法盾法术目标税费用不足拒绝、多目标法盾税聚合费用不足拒绝、《妖异狐火》总战力上限拒绝、《顽皮触手》总战力上限拒绝、《狩魂》目标战力上限拒绝、《罡风》目标战力上限拒绝、《巧取豪夺》离场目标拒绝、《致命华彩》友方目标拒绝、《血钱》目标战力上限拒绝、《惩戒》基地单位目标拒绝和非法回响拒绝、《扑咚！》非进攻方目标拒绝、《天顶之刃》敌方基地和友方目标拒绝、《狂风绝息斩》敌方基地和目标顺序拒绝、《聚合变异》敌方和重复目标拒绝、《存在焦虑》友方进攻目标拒绝、《霹天雳地》和《御衡守念》费用不足拒绝、《透体圣光》重复目标拒绝、《风箱炎息》第四目标和重复目标拒绝、《火箭轰击》缺失模式和摧毁装备模式单位目标拒绝、《紧急召回》单位目标拒绝、《魄罗佳肴》《舒瑞娅的安魂曲》《未来熔炉》《废料堆》《精灵提灯》《地沟区地图》《占卜花朵》《魔法鲜豆》《钢铁弩炮》《玄冰之心》《懊悔法球》《灵魂之剑》《锯齿短匕》《多兰之盾》《海克斯注力刚壁》《多兰之刃》《多兰之戒》《先锋之眼》《反曲之弓》《长剑》《布甲》《斯特拉克的挑战护手》《旋转飞斧》《牧人的传家宝》《残暴之力》《守护天使》《海克斯饮魔刀》《狂徒铠甲》《三相之力》《轻灵之靴》《萃取》《神圣剪刀》《暴风大剑》《云游图鉴》《阿瑞昂的陨落》《猎人的宽刃刀》《枯萎战斧》《碎骨棒》《远古簇碑》《海克斯异常体》《能量通道》《预时之门》《邪鸦魔典》《太阳圆盘》《远见面具》《烈阳圣坛》《炼金科技桶》《灵魂之轮》《蘑菇袋》《竞技场酒吧》《海盗避风港》《被遗忘的路标》《冰封宝石》《倾颓宫殿》《猩红玫瑰》《逆转碎片》《装配架》《斯弗尔尚歌》《Z型驱动》《先锋军备》《追忆祭坛》《暴怒之印》《专注之印》《洞察之印》《力量之印》《不和之印》《团结之印》和 OGN 版《暴怒之印》《专注之印》《洞察之印》《力量之印》《不和之印》《团结之印》带目标拒绝、《奇妙行囊》《塞壬号》《无主宝藏》《拾荒小能手》《雾临剑冢》《闪耀极光》《烈阳徽记》《先锋之盔》《蜜糖果实》《临终仪式》《破败王者之刃》《来路不明的武器》《海兽钓钩》《禁魔石丰碑》《中娅沙漏》《夜之锋刃》《炉火斗篷》《灭世者的死亡之冠》《喷射球果》带目标拒绝、《夺命名单》带目标拒绝、《受诅咒的石棺》带目标拒绝、promo 编号《碎骨棒》带目标拒绝、《海克斯科技护手》带目标拒绝、《宝藏魔像》带目标拒绝、《泽拉斯》普通手牌打出带目标拒绝、《龙魂贤者》普通手牌打出带目标拒绝、《绵绵魄罗》普通手牌打出带目标拒绝、SFD·088《烈娜塔·戈拉斯克》普通手牌打出带目标拒绝、SFD·088a《烈娜塔·戈拉斯克》普通手牌打出带目标拒绝、OGN·028《德莱文》普通手牌打出带目标拒绝、SFD·110《菲奥娜》普通手牌打出带目标拒绝、SFD·110a《菲奥娜》普通手牌打出带目标拒绝、SFD·141《艾瑞莉娅》普通手牌打出带目标拒绝、SFD·141a《艾瑞莉娅》普通手牌打出带目标拒绝、《唤龙使者》普通手牌打出带目标拒绝、《驭水者》普通手牌打出带目标拒绝、《睿智长者》普通手牌打出带目标拒绝、《踊跃的学徒》普通手牌打出带目标拒绝、《竞技场勤务小队》普通手牌打出带目标拒绝、《魄罗牧者》普通手牌打出带目标拒绝、《拉文布鲁姆学生》普通手牌打出带目标拒绝、《残响之魂》普通手牌打出带目标拒绝、《比尔吉沃特恶霸》普通手牌打出带目标拒绝、《神射海盗》普通手牌打出带目标拒绝、《沙丘亚龙》普通手牌打出带目标拒绝、《余火修士》普通手牌打出带目标拒绝、《隐秘追踪者》普通手牌打出带目标拒绝、《卧底特工》普通手牌打出带目标拒绝、《旅行商人》普通手牌打出带目标拒绝、《克格莫》普通手牌打出带目标拒绝、《诺克萨斯鼓手》普通手牌打出带目标拒绝、《控潮者》普通手牌打出带目标拒绝、OGN·202《金克丝》普通手牌打出带目标拒绝、OGN·226《幽灵主母》普通手牌打出带目标拒绝、OGN·230《阿不思·菲罗斯》普通手牌打出带目标拒绝、OGN·236《卡尔萨斯》普通手牌打出带目标拒绝、SFD·027《穿沙角兽》普通手牌打出带目标拒绝、SFD·035《幽径守卫》普通手牌打出带目标拒绝、SFD·041《学徒铁匠》普通手牌打出带目标拒绝、SFD·047《山猿老祖》普通手牌打出带目标拒绝、《行军号令》敌方基地目标拒绝、《决斗》《战斗号令》和《虚空来袭》目标顺序反转拒绝、《烈火风暴》显式单位目标拒绝、《新月打击》友方和基地目标拒绝、《换换乐》重复和基地目标拒绝、多目标税聚合与友方目标 no-tax 边界、`ACTIVATE_ABILITY` / `HIDE_CARD` / `REVEAL_CARD` / `MOVE_UNIT` / `ASSEMBLE_EQUIPMENT` / `DECLARE_BATTLE` command 前置模型、《蔚》无目标付费技能入栈/结算代表路径、带目标/额外费用/费用不足/非《蔚》来源拒绝、对手控制来源拒绝与来源不在场上拒绝、《泽拉斯》带目标技能敌方法盾税、法盾税费用不足拒绝、己方法盾 no-tax、已横置来源拒绝、缺目标/多目标/额外费用/非《泽拉斯》来源/非单位目标/来源不在战场拒绝、对手控制来源拒绝和 P4.79-P4.86/P4.93 拒绝聚合回放、横置和伤害代表路径、场上对象 `cardNo` 身份边界、对手正面朝下对象 snapshot redaction、待命 `HIDE_CARD` 最小正面朝下放置、费用不足拒绝及《游击战》`STANDBY_FREE` 免费暗置/无权限/非待命废牌堆目标拒绝、`REVEAL_CARD` 基地显露、`STANDBY_REACTION` 无目标反应入栈及无优先权窗口拒绝、`PLAY_CARD mode=AMBUSH` 目的地前置模型及显式拒绝 fixture、`MOVE_UNIT` 游走/基础移动前置拒绝 fixture、`ASSEMBLE_EQUIPMENT` 装备装配前置拒绝 fixture、`DECLARE_BATTLE` 战斗声明前置拒绝 fixture、`预知` 非顶部牌目标拒绝 fixture、回收/放逐/增益 template skeleton、多条等级/鼓舞代表路径、34 条急速 `HASTE_READY` 代表路径及 power 不足拒绝 fixture、P4.58《取放自如》武装贴附/卸除代表路径，以及 P4.75 对 33 个显式 P4 目标 surface 的 baseline 覆盖审计测试。当前仍不能标记 P4 goal complete：战斗承伤/强攻修正、游走真实移动、待命触发/完整隐藏区/目标伤害、伏击真实反应战场打出、更多技能目标税/通用技能 registry、完整装备装配/灵便/百炼、战斗/移动触发经验和若干复杂卡牌分支仍 deferred。

## Validation Gate

每个进入 P4 可玩路径的能力都必须补齐：

- 规则证据：至少关联 `docs/rules-evidence-index.md` 中的 PDF/FAQ 条目；关键词默认从 `CORE-260330` p92-p105 rules 800+ 起步，法盾/回响/百炼必须核对 `SOUL-OFAQ-260114` / `SOUL-JFAQ-260114`。
- 官网卡面文本：从 `data/official/card-catalog.zh-CN.json` 或 `BehaviorSpec.OfficialText` 固定代表卡文本。
- Engine test：覆盖状态变化或明确验证 delegated behavior plan。
- Conformance fixture：至少一条 `ConformanceFixtureRunnerTests` 可回放路径，或记录 Java legacy oracle 差异。
- SignalR/Room 或等价 E2E：高风险能力进入可玩路径时补 GameHub/Browser Use smoke；P4.1 的纯桥接可先用 engine/conformance 等价测试。
- 文档状态：同步本文件、必要时同步 `docs/rules-evidence-index.md` / `docs/p2-rules-preflight.md` / README。

## Latest Validation

P4.273 已完成验证：

- `jq empty tests/Riftbound.ConformanceTests/Fixtures/p4-play-mountain-ape-elder-target-rejected.fixture.json`：pass
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`：pass，0 warnings，0 errors
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：pass，2243/2243
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`：pass，2162/2162
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`：pass，23/23
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4MountainApeElderTargetRejectedFixture"`：pass，1/1
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen"`：pass，173/173
- `git diff --check`：pass

## Next Step

进入 P4.274：继续基于 completion audit 选择一个低风险、可验证的小批次。当前不能标记 P4 goal complete：更多技能目标税/通用 skill registry、待命触发/完整隐藏区/目标伤害、伏击真实反应战场打出、游走真实移动、完整战斗、完整装备装配/灵便/百炼、战斗/移动触发经验、《不死军团》废牌堆打出、德莱厄斯活跃/光环和其他急速牌彩色资源/活跃分支等仍有明确 deferred 项。
