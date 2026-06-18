import { ClipboardCheck, ShieldAlert, ShieldCheck } from "lucide-react";
import { StatusPill } from "../components/ui/StatusPill";

type AuditRow = {
  screen: string;
  ruleLens: string;
  result: "PASS" | "FIXED" | "GATE" | "SUPERSEDED";
  evidence: string;
};

const auditRows: AuditRow[] = [
  { screen: "对战桌面", ruleLens: "2 战场、0-8 分、优先权/焦点、ActionPrompt、隐藏信息", result: "PASS", evidence: "实时快照和服务端候选已接入 MatchPage" },
  { screen: "PAY_COST", ruleLens: "支付来源、候选费用、连接状态、服务端校验", result: "PASS", evidence: "ActionPanel 从 PromptView/legalActions 渲染候选" },
  { screen: "ORDER_TRIGGERS", ruleLens: "本地/对手触发排序、隐藏来源脱敏", result: "PASS", evidence: "只提交 ORDER_TRIGGERS intent" },
  { screen: "MULLIGAN", ruleLens: "仅本方手牌可见，调度选择由服务端限制", result: "PASS", evidence: "MulliganCandidate 使用服务端 sources/maxSelection" },
  { screen: "法术对决与优先权", ruleLens: "focus、priority、连续让过、控制权锁定", result: "FIXED", evidence: "关闭对决不在前端裁决" },
  { screen: "声明战斗", ruleLens: "争夺中 controllerPlayerId 不自动变化", result: "PASS", evidence: "BattlefieldArea 显示 contested + controllerId" },
  { screen: "征服/据守得分", ruleLens: "每战场每回合 1 分，目标 8 分", result: "PASS", evidence: "得分状态读取 scoredThisTurn/scoredPlayerId" },
  { screen: "待命/隐藏信息", ruleLens: "对手待命只显示卡背/slot/ref", result: "PASS", evidence: "CardFace/hiddenInfo 不渲染未公开卡名" },
  { screen: "燃尽/清理", ruleLens: "清理和燃尽仅播放服务端事件", result: "PASS", evidence: "RuleEventRibbon/EventLog 展示事件，不推断结果" },
  { screen: "过期操作/重连", ruleLens: "旧 snapshot 阻断提交并请求 resync", result: "PASS", evidence: "命令会带 promptId/snapshotTick" },
  { screen: "通用 Prompt", ruleLens: "无 Nexus、脱敏 payload、legalActions only", result: "FIXED", evidence: "GenericPromptDetails 展示安全候选摘要" },
  { screen: "大厅", ruleLens: "规则集/服务端状态/无本地裁决", result: "PASS", evidence: "房间创建和加入只导航，连接由 RoomPage 执行" },
  { screen: "准备房间", ruleLens: "1v1、8 分、2 公共战场、40+/12/3 卡组规则", result: "PASS", evidence: "房间页显示服务端玩家/ready/deckSubmitted 快照" },
  { screen: "卡组构筑", ruleLens: "主牌 40+、符文 12、战场 3、同名限制服务端判定", result: "FIXED", evidence: "DecksPage 展示 12 符文并声明等待服务端验证" },
  { screen: "卡牌图鉴", ruleLens: "官方快照字段、无伪勘误/伪效果", result: "FIXED", evidence: "CardLibraryPage 从 catalog endpoints 加载" },
  { screen: "对战结算", ruleLens: "胜负由服务端宣告，目标 + 领先确认", result: "FIXED", evidence: "ResultPage 只读 winnerPlayerId/score" },
  { screen: "设置/调试日志", ruleLens: "健康检查、图鉴证据、脱敏日志导出", result: "GATE", evidence: "真实隐藏信息泄漏仍需 Playwright/fixture 检查" },
  { screen: "关键词图例/覆盖矩阵", ruleLens: "只做状态可视化，不裁决规则", result: "PASS", evidence: "复核页和设置页显示规则覆盖口径" },
  { screen: "桌面区域契约", ruleLens: "双战场、双方 7 区、固定 12 符文、坐标不越界", result: "PASS", evidence: "tabletopLayoutData + check:tabletop-layout" },
  { screen: "开源参考吸收", ruleLens: "社区项目只做架构/布局/工具参考，不作规则源", result: "PASS", evidence: "AGENTS.md + open-source-reference-audit" },
  { screen: "v2 交付索引", ruleLens: "旧 Stitch 索引", result: "SUPERSEDED", evidence: "以本页 v3 接入状态为准" }
];

export function RuleAuditPage() {
  const fixedCount = auditRows.filter((row) => row.result === "FIXED").length;
  const passCount = auditRows.filter((row) => row.result === "PASS").length;

  return (
    <div className="page-grid rule-audit-page">
      <section className="page-header">
        <div>
          <span className="eyebrow">规则复核</span>
          <h1>逐屏规则接入报告 v3</h1>
          <p>当前前端页面以官方 PDF、data/official、Riot/playloltcg 与服务端权威快照为基准；社区与开源资料只作为 UI、架构和工具链参考。</p>
        </div>
        <StatusPill tone="good">{passCount + fixedCount} 项已接入</StatusPill>
      </section>
      <section className="audit-summary-grid">
        <article>
          <ShieldCheck size={20} />
          <strong>服务端权威</strong>
          <span>费用、伤害、战场控制、法术对决、得分、胜负都不在前端裁决。</span>
        </article>
        <article>
          <ShieldAlert size={20} />
          <strong>隐藏信息边界</strong>
          <span>对手手牌、牌堆顺序、符文牌堆、面朝下待命不进入可见 UI。</span>
        </article>
        <article>
          <ClipboardCheck size={20} />
          <strong>构筑基准</strong>
          <span>主牌 40+，符文牌堆 12，战场牌 3 张唯一，正式合法性以服务端验证为准。</span>
        </article>
        <article>
          <ClipboardCheck size={20} />
          <strong>桌面契约</strong>
          <span>双方传奇、英雄、基地、符文、手牌和双战场坐标由自动检查守住。</span>
        </article>
      </section>
      <section className="audit-table" aria-label="逐屏规则复核表">
        <div className="audit-table-head">
          <span>页面</span>
          <span>规则检查点</span>
          <span>状态</span>
          <span>接入证据</span>
        </div>
        {auditRows.map((row) => (
          <article className="audit-row" key={row.screen}>
            <strong>{row.screen}</strong>
            <span>{row.ruleLens}</span>
            <StatusPill tone={toneForResult(row.result)}>{row.result}</StatusPill>
            <span>{row.evidence}</span>
          </article>
        ))}
      </section>
      <section className="audit-banner">
        <StatusPill tone="warn">实现准入</StatusPill>
        <p>设计已接入真实前端壳和现有服务协议；上线前仍需 TypeScript strict、Playwright no-blank/no-leak/no-stale-submit、axe-core 和服务端 fixture 回归。</p>
      </section>
    </div>
  );
}

function toneForResult(result: AuditRow["result"]) {
  switch (result) {
    case "PASS":
    case "FIXED":
      return "good";
    case "GATE":
      return "warn";
    case "SUPERSEDED":
      return "neutral";
  }
}
