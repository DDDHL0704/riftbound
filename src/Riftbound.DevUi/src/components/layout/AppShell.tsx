import { BookOpen, ClipboardCheck, Grid3X3, Home, Library, Settings, Shield, Swords, UsersRound } from "lucide-react";
import { AppRoute } from "../../app/router";
import { Button } from "../ui/Button";
import { Tooltip, TooltipProvider } from "../ui/Tooltip";

type AppShellProps = {
  activeRoute: AppRoute["name"];
  onNavigate: (route: AppRoute) => void;
  children: React.ReactNode;
};

export function AppShell({ activeRoute, onNavigate, children }: AppShellProps) {
  const matchFrame = activeRoute === "match";

  return (
    <TooltipProvider delayDuration={160}>
      <div className={`app-frame ${matchFrame ? "app-frame-match app-frame-stitch-match" : ""}`}>
        {!matchFrame && (
          <aside className="main-nav" aria-label="主导航">
            <div className="brand-mark" aria-label="符文战场" role="img">
              <Shield size={28} />
              <div>
                <strong>符文战场</strong>
                <span>服务端权威对战</span>
              </div>
            </div>
            <nav>
              <NavButton active={activeRoute === "home"} icon={<Home size={18} />} label="首页" onClick={() => onNavigate({ name: "home" })} />
              <NavButton active={activeRoute === "lobby" || activeRoute === "room"} icon={<UsersRound size={18} />} label="大厅" onClick={() => onNavigate({ name: "lobby" })} />
              <NavButton active={false} icon={<Swords size={18} />} label="对战" onClick={() => onNavigate({ name: "match", matchId: "local" })} />
              <NavButton active={activeRoute === "cards"} icon={<Library size={18} />} label="图鉴" onClick={() => onNavigate({ name: "cards" })} />
              <NavButton active={activeRoute === "decks"} icon={<BookOpen size={18} />} label="卡组" onClick={() => onNavigate({ name: "decks" })} />
              <NavButton active={activeRoute === "layoutLab"} icon={<Grid3X3 size={18} />} label="布局" onClick={() => onNavigate({ name: "layoutLab" })} />
              <NavButton active={activeRoute === "audit"} icon={<ClipboardCheck size={18} />} label="复核" onClick={() => onNavigate({ name: "audit" })} />
              <NavButton active={activeRoute === "settings"} icon={<Settings size={18} />} label="设置" onClick={() => onNavigate({ name: "settings" })} />
            </nav>
            <p className="nav-footnote">所有可玩操作只来自服务端行动提示。</p>
          </aside>
        )}
        <main className="app-content">{children}</main>
      </div>
    </TooltipProvider>
  );
}

function NavButton({ active, icon, label, onClick }: { active: boolean; icon: React.ReactNode; label: string; onClick: () => void }) {
  return (
    <Tooltip label={label}>
      <Button aria-current={active ? "page" : undefined} className={active ? "nav-active" : ""} icon={icon} onClick={onClick} variant="ghost">
        {label}
      </Button>
    </Tooltip>
  );
}
