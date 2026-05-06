import { useSettings } from "../stores/settingsStore";

export function SettingsPage() {
  const { settings, updateSettings } = useSettings();
  return (
    <div className="page-grid">
      <section className="page-header">
        <div>
          <span className="eyebrow">设置</span>
          <h1>本地客户端设置</h1>
          <p>仅保存前端偏好，不改变服务端规则。</p>
        </div>
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
    </div>
  );
}
