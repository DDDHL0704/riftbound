import { BookOpen, ClipboardCheck, Grid3X3, Home, Library, MoreHorizontal, Settings, Shield, UserRound, UsersRound } from "lucide-react";
import { AppRoute } from "../../app/router";
import { useSettings } from "../../stores/settingsStore";
import { Button } from "../ui/Button";
import { Tooltip, TooltipProvider } from "../ui/Tooltip";

type AppShellProps = {
  activeRoute: AppRoute["name"];
  onNavigate: (route: AppRoute) => void;
  children: React.ReactNode;
};

export function AppShell({ activeRoute, onNavigate, children }: AppShellProps) {
  const matchFrame = activeRoute === "match";
  const { settings } = useSettings();
  const profileHandle = settings.playerId.trim() || "player";

  return (
    <TooltipProvider delayDuration={160}>
      <div className={`app-frame game-app-shell ${matchFrame ? "app-frame-match app-frame-stitch-match" : ""}`} data-game-shell>
        {!matchFrame && (
          <aside className="main-nav" aria-label="主导航">
            <button className="brand-mark" onClick={() => onNavigate({ name: "lobby" })} type="button">
              <Shield size={28} />
              <div>
                <strong>符文战场</strong>
                <span>Riftbound</span>
              </div>
            </button>
            <nav className="game-primary-nav">
              <NavButton active={activeRoute === "lobby" || activeRoute === "room"} icon={<UsersRound size={18} />} label="对战大厅" onClick={() => onNavigate({ name: "lobby" })} />
              <NavButton active={activeRoute === "cards"} icon={<Library size={18} />} label="卡牌图鉴" onClick={() => onNavigate({ name: "cards" })} />
              <NavButton active={activeRoute === "decks"} icon={<BookOpen size={18} />} label="我的卡组" onClick={() => onNavigate({ name: "decks" })} />
              <NavButton active={activeRoute === "settings"} icon={<Settings size={18} />} label="设置" onClick={() => onNavigate({ name: "settings" })} />
            </nav>
            <details className="game-secondary-nav">
              <summary><MoreHorizontal size={18} />更多</summary>
              <div>
                <NavButton active={activeRoute === "home"} icon={<Home size={17} />} label="首页" onClick={() => onNavigate({ name: "home" })} />
                <NavButton active={activeRoute === "profile"} icon={<UserRound size={17} />} label="资料" onClick={() => onNavigate({ name: "profile", handle: profileHandle })} />
                <NavButton active={activeRoute === "layoutLab"} icon={<Grid3X3 size={17} />} label="布局工具" onClick={() => onNavigate({ name: "layoutLab" })} />
                <NavButton active={activeRoute === "audit"} icon={<ClipboardCheck size={17} />} label="规则复核" onClick={() => onNavigate({ name: "audit" })} />
              </div>
            </details>
            <p className="nav-footnote">规则与合法行动由服务器提供</p>
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
