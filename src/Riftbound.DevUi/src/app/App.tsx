import { useEffect, useMemo, useState } from "react";
import { AppShell } from "../components/layout/AppShell";
import { CardLibraryPage } from "../pages/CardLibraryPage";
import { DecksPage } from "../pages/DecksPage";
import { HomePage } from "../pages/HomePage";
import { LobbyPage } from "../pages/LobbyPage";
import { MatchPage } from "../pages/MatchPage";
import { ResultPage } from "../pages/ResultPage";
import { RoomPage } from "../pages/RoomPage";
import { RuleAuditPage } from "../pages/RuleAuditPage";
import { SettingsPage } from "../pages/SettingsPage";
import { TabletopLayoutLabPage } from "../pages/TabletopLayoutLabPage";
import { isWireLayoutFixtureEnabled } from "../fixtures/wireLayoutFixture";
import { CatalogProvider } from "../stores/catalogStore";
import { SettingsProvider, useSettings } from "../stores/settingsStore";
import { AppRoute, parseRoute, routePath } from "./router";

function RoutedApp() {
  const [route, setRoute] = useState<AppRoute>(() => parseRoute());
  const { settings } = useSettings();
  const catalogDisabled = route.name === "match" && isWireLayoutFixtureEnabled();

  useEffect(() => {
    const onPop = () => setRoute(parseRoute());
    window.addEventListener("popstate", onPop);
    return () => window.removeEventListener("popstate", onPop);
  }, []);

  const navigate = useMemo(
    () => (next: AppRoute) => {
      window.history.pushState({}, "", routePath(next));
      setRoute(next);
    },
    []
  );

  return (
    <CatalogProvider disabled={catalogDisabled} serverUrl={settings.serverUrl}>
      <AppShell activeRoute={route.name} onNavigate={navigate}>
        {route.name === "home" && <HomePage onNavigate={navigate} />}
        {route.name === "cards" && <CardLibraryPage />}
        {route.name === "decks" && <DecksPage onNavigate={navigate} />}
        {route.name === "lobby" && <LobbyPage onNavigate={navigate} />}
        {route.name === "room" && <RoomPage roomId={route.roomId} onNavigate={navigate} />}
        {route.name === "match" && <MatchPage matchId={route.matchId} onNavigate={navigate} />}
        {route.name === "result" && <ResultPage matchId={route.matchId} onNavigate={navigate} />}
        {route.name === "settings" && <SettingsPage />}
        {route.name === "layoutLab" && <TabletopLayoutLabPage />}
        {route.name === "audit" && <RuleAuditPage />}
      </AppShell>
    </CatalogProvider>
  );
}

export function App() {
  return (
    <SettingsProvider>
      <RoutedApp />
    </SettingsProvider>
  );
}
