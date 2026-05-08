import { LogIn, Plus } from "lucide-react";
import { FormEvent, useState } from "react";
import { AppRoute } from "../app/router";
import { Button } from "../components/ui/Button";
import { useSettings } from "../stores/settingsStore";

export function LobbyPage({ onNavigate }: { onNavigate: (route: AppRoute) => void }) {
  const { settings, updateSettings } = useSettings();
  const [roomId, setRoomId] = useState("");

  const createRoom = () => {
    const nextRoom = `room-${Math.random().toString(36).slice(2, 8)}`;
    onNavigate({ name: "room", roomId: nextRoom });
  };

  const joinRoom = (event: FormEvent) => {
    event.preventDefault();
    if (roomId.trim()) {
      onNavigate({ name: "room", roomId: roomId.trim() });
    }
  };

  return (
    <div className="page-grid lobby-page">
      <section className="page-header">
        <div>
          <span className="eyebrow">大厅</span>
          <h1>创建或加入 1v1 房间</h1>
          <p>房间由服务端实时连接创建；正式准备前必须提交服务端可验证卡组。</p>
        </div>
        <Button icon={<Plus size={18} />} onClick={createRoom}>创建房间</Button>
      </section>
      <form className="lobby-form" onSubmit={joinRoom}>
        <label>
          <span>玩家名称</span>
          <input value={settings.playerId} onChange={(event) => updateSettings({ playerId: event.target.value })} />
        </label>
        <label>
          <span>服务端地址</span>
          <input value={settings.serverUrl} onChange={(event) => updateSettings({ serverUrl: event.target.value })} />
        </label>
        <label>
          <span>房间码</span>
          <input value={roomId} onChange={(event) => setRoomId(event.target.value)} placeholder="输入邀请房间码" />
        </label>
        <Button icon={<LogIn size={18} />} type="submit">加入房间</Button>
      </form>
    </div>
  );
}
