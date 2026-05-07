import { RefreshCw, Server, ShieldCheck, UserRound } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Button } from "../components/ui/Button";
import { StatusPill } from "../components/ui/StatusPill";
import { ApiClient, HealthResponse } from "../services/apiClient";
import { useCatalog } from "../stores/catalogStore";
import { useSettings } from "../stores/settingsStore";

type HealthState = {
  loading: boolean;
  checkedAt?: string;
  result?: HealthResponse;
  error?: string;
};

export function SettingsPage() {
  const { settings, updateSettings } = useSettings();
  const catalog = useCatalog();
  const [healthTick, setHealthTick] = useState(0);
  const [health, setHealth] = useState<HealthState>({ loading: true });

  useEffect(() => {
    const controller = new AbortController();
    const client = new ApiClient(settings.serverUrl);
    setHealth({ loading: true });

    client
      .health(controller.signal)
      .then((result) => {
        setHealth({
          loading: false,
          checkedAt: new Date().toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", second: "2-digit" }),
          result
        });
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setHealth({
            loading: false,
            checkedAt: new Date().toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", second: "2-digit" }),
            error: error instanceof Error ? error.message : String(error)
          });
        }
      });

    return () => controller.abort();
  }, [healthTick, settings.serverUrl]);

  const deferredFamilyCount = useMemo(
    () => catalog.keywordCoverage?.families.filter((family) => family.deferredCards.length > 0).length ?? 0,
    [catalog.keywordCoverage]
  );
  const statusCounts = useMemo(() => Object.entries(catalog.keywordCoverage?.statusCounts ?? {}).slice(0, 4), [catalog.keywordCoverage]);
  const refreshDiagnostics = () => {
    setHealthTick((current) => current + 1);
    catalog.reload();
  };

  return (
    <div className="page-grid">
      <section className="page-header">
        <div>
          <span className="eyebrow">设置</span>
          <h1>本地客户端设置</h1>
          <p>仅保存前端偏好，不改变服务端规则。</p>
        </div>
        <Button disabled={health.loading || catalog.loading} icon={<RefreshCw size={16} />} onClick={refreshDiagnostics} variant="secondary">
          刷新诊断
        </Button>
      </section>
      <section className="settings-grid">
        <label>
          <span>服务端地址</span>
          <input value={settings.serverUrl} onChange={(event) => updateSettings({ serverUrl: event.target.value })} />
        </label>
        <label>
          <span>玩家名称</span>
          <input value={settings.playerId} onChange={(event) => updateSettings({ playerId: event.target.value })} />
        </label>
        <label>
          <span>动画强度</span>
          <select value={settings.animationLevel} onChange={(event) => updateSettings({ animationLevel: event.target.value as typeof settings.animationLevel })}>
            <option value="full">完整</option>
            <option value="reduced">简化</option>
            <option value="off">关闭</option>
          </select>
        </label>
      </section>
      <section className="diagnostics-header">
        <div>
          <span className="eyebrow">诊断</span>
          <h2>服务端连接与规则证据</h2>
        </div>
        <span>{health.checkedAt ? `最近检查：${health.checkedAt}` : "等待检查"}</span>
      </section>
      <section className="diagnostic-grid">
        <article className="diagnostic-card">
          <header>
            <Server size={18} />
            <strong>API 健康</strong>
            <StatusPill tone={health.error ? "bad" : health.loading ? "info" : "good"}>{health.error ? "失败" : health.loading ? "检查中" : "在线"}</StatusPill>
          </header>
          {health.result ? (
            <dl>
              <div>
                <dt>状态</dt>
                <dd>{health.result.status}</dd>
              </div>
              <div>
                <dt>服务</dt>
                <dd>{health.result.service}</dd>
              </div>
              <div>
                <dt>角色</dt>
                <dd>{health.result.role}</dd>
              </div>
              <div>
                <dt>.NET</dt>
                <dd>{health.result.dotnet}</dd>
              </div>
            </dl>
          ) : (
            <p>{health.error ?? "正在读取 /health。"}</p>
          )}
        </article>
        <article className="diagnostic-card">
          <header>
            <ShieldCheck size={18} />
            <strong>图鉴证据</strong>
            <StatusPill tone={catalog.error ? "bad" : catalog.loading ? "info" : deferredFamilyCount > 0 ? "warn" : "good"}>
              {catalog.error ? "失败" : catalog.loading ? "加载中" : `${catalog.specs.length} 张`}
            </StatusPill>
          </header>
          {catalog.error ? (
            <p>{catalog.error}</p>
          ) : (
            <dl>
              <div>
                <dt>行为规格</dt>
                <dd>{catalog.specs.length}</dd>
              </div>
              <div>
                <dt>关键词族</dt>
                <dd>{catalog.keywordCoverage?.families.length ?? 0}</dd>
              </div>
              <div>
                <dt>Deferred family</dt>
                <dd>{deferredFamilyCount}</dd>
              </div>
              {statusCounts.map(([status, count]) => (
                <div key={status}>
                  <dt>{status}</dt>
                  <dd>{count}</dd>
                </div>
              ))}
            </dl>
          )}
        </article>
        <article className="diagnostic-card">
          <header>
            <UserRound size={18} />
            <strong>本地身份</strong>
            <StatusPill tone="neutral">localStorage</StatusPill>
          </header>
          <dl>
            <div>
              <dt>玩家</dt>
              <dd>{settings.playerId}</dd>
            </div>
            <div>
              <dt>动画</dt>
              <dd>{animationLabel(settings.animationLevel)}</dd>
            </div>
            <div>
              <dt>API</dt>
              <dd>{settings.serverUrl}</dd>
            </div>
          </dl>
        </article>
      </section>
    </div>
  );
}

function animationLabel(level: "full" | "reduced" | "off"): string {
  switch (level) {
    case "full":
      return "完整";
    case "reduced":
      return "简化";
    case "off":
      return "关闭";
  }
}
