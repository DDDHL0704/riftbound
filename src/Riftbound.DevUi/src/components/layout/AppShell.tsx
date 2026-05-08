import { BookOpen, Home, Library, Settings, Shield, Swords, UsersRound } from "lucide-react";
import { AppRoute } from "../../app/router";
import { Button } from "../ui/Button";

type AppShellProps = {
  activeRoute: AppRoute["name"];
  onNavigate: (route: AppRoute) => void;
  children: React.ReactNode;
};

export function AppShell({ activeRoute, onNavigate, children }: AppShellProps) {
  return (
    <div className="app-frame">
      <aside className="main-nav" aria-label="主导航">
        <div className="brand-mark" aria-label="符文战场">
          <Shield size={28} />
          <div>
            <strong>符文战场</strong>
            <span>服务端权威对战</span>
          </div>
        </div>
        <nav>
          <NavButton active={activeRoute === "home"} icon={<Home size={18} />} label="首页" onClick={() => onNavigate({ name: "home" })} />
          <NavButton active={activeRoute === "lobby" || activeRoute === "room"} icon={<UsersRound size={18} />} label="大厅" onClick={() => onNavigate({ name: "lobby" })} />
          <NavButton active={activeRoute === "match"} icon={<Swords size={18} />} label="对战" onClick={() => onNavigate({ name: "match", matchId: "local" })} />
          <NavButton active={activeRoute === "cards"} icon={<Library size={18} />} label="图鉴" onClick={() => onNavigate({ name: "cards" })} />
          <NavButton active={activeRoute === "decks"} icon={<BookOpen size={18} />} label="卡组" onClick={() => onNavigate({ name: "decks" })} />
          <NavButton active={activeRoute === "settings"} icon={<Settings size={18} />} label="设置" onClick={() => onNavigate({ name: "settings" })} />
        </nav>
        <p className="nav-footnote">所有可玩操作只来自服务端行动提示。</p>
      </aside>
      <main className="app-content">{children}</main>
    </div>
  );
}

function NavButton({ active, icon, label, onClick }: { active: boolean; icon: React.ReactNode; label: string; onClick: () => void }) {
  return (
    <Button aria-current={active ? "page" : undefined} className={active ? "nav-active" : ""} icon={icon} onClick={onClick} variant="ghost">
      {label}
    </Button>
  );
}
