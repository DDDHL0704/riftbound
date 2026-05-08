import { Library, Play, Settings, Swords } from "lucide-react";
import { AppRoute } from "../app/router";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { useCatalog } from "../stores/catalogStore";
import { readinessLabel } from "../utils/formatters";

export function HomePage({ onNavigate }: { onNavigate: (route: AppRoute) => void }) {
  const catalog = useCatalog();
  return (
    <div className="page-grid home-page">
      <section className="command-center">
        <div>
          <span className="eyebrow">1v1 决斗模式</span>
          <h1>符文战场联机对战桌面</h1>
          <p>服务端裁决规则，前端只展示快照、候选行动和玩家选择窗口。</p>
        </div>
        <div className="hero-actions">
          <Button icon={<Play size={18} />} onClick={() => onNavigate({ name: "lobby" })}>进入大厅</Button>
          <Button icon={<Swords size={18} />} onClick={() => onNavigate({ name: "match", matchId: "local" })} variant="secondary">打开对战桌面</Button>
          <Button icon={<Library size={18} />} onClick={() => onNavigate({ name: "cards" })} variant="secondary">卡牌图鉴</Button>
          <Button icon={<Settings size={18} />} onClick={() => onNavigate({ name: "settings" })} variant="ghost">设置</Button>
        </div>
      </section>
      <section className="status-grid">
        <article>
          <span className="eyebrow">卡牌数据</span>
          <h2>{catalog.loading ? "加载中" : `${catalog.specs.length} 张`}</h2>
          <p>{catalog.error ? `加载失败：${catalog.error}` : "图鉴状态来自服务端 /catalog/behavior-specs。"}</p>
        </article>
        <article>
          <span className="eyebrow">一致性口径</span>
          <h2>{readinessLabel("NOT_READY")}</h2>
          <p>当前仍需补齐服务端 P0/P1 规则状态机和全官方证据。</p>
        </article>
        <article>
          <span className="eyebrow">前端原则</span>
          <h2>不本地裁决</h2>
          <p>按钮、目标、费用和响应窗口必须来自服务端行动提示。</p>
        </article>
        <article>
          <span className="eyebrow">本批状态</span>
          <h2>新架构</h2>
          <p>旧 Dev UI 已清理，正在重建产品级桌面。</p>
        </article>
      </section>
      <section className="audit-banner">
        <StatusPill tone="warn">服务端缺口需补实现</StatusPill>
        <p>前端遇到服务端未给出的 P0/P1 必需能力时，会记录缺口并推进服务端补齐；补齐前不显示假可玩入口。</p>
      </section>
    </div>
  );
}
