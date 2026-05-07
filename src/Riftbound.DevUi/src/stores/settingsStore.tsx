import { createContext, ReactNode, useContext, useMemo, useState } from "react";

export type AppSettings = {
  serverUrl: string;
  playerId: string;
  animationLevel: "full" | "reduced" | "off";
  logDensity: "compact" | "standard" | "detailed";
};

type SettingsContextValue = {
  settings: AppSettings;
  updateSettings: (patch: Partial<AppSettings>) => void;
};

const defaultSettings = readInitialSettings();

const SettingsContext = createContext<SettingsContextValue | undefined>(undefined);

export function SettingsProvider({ children }: { children: ReactNode }) {
  const [settings, setSettings] = useState<AppSettings>(defaultSettings);

  const value = useMemo<SettingsContextValue>(
    () => ({
      settings,
      updateSettings: (patch) => {
        setSettings((current) => {
          const next = { ...current, ...patch };
          localStorage.setItem("riftbound.serverUrl", next.serverUrl);
          localStorage.setItem("riftbound.playerId", next.playerId);
          localStorage.setItem("riftbound.animationLevel", next.animationLevel);
          localStorage.setItem("riftbound.logDensity", next.logDensity);
          return next;
        });
      }
    }),
    [settings]
  );

  return <SettingsContext.Provider value={value}>{children}</SettingsContext.Provider>;
}

export function useSettings(): SettingsContextValue {
  const value = useContext(SettingsContext);
  if (!value) {
    throw new Error("useSettings must be used within SettingsProvider");
  }

  return value;
}

function readInitialSettings(): AppSettings {
  const serverUrl = localStorage.getItem("riftbound.serverUrl") ?? "http://127.0.0.1:5088";
  const storedPlayerId = localStorage.getItem("riftbound.playerId");
  const playerId = storedPlayerId ?? `玩家${Math.floor(Math.random() * 900 + 100)}`;
  const animationLevel = readEnum("riftbound.animationLevel", ["full", "reduced", "off"], "full");
  const logDensity = readEnum("riftbound.logDensity", ["compact", "standard", "detailed"], "standard");

  if (!storedPlayerId) {
    localStorage.setItem("riftbound.playerId", playerId);
  }

  return {
    serverUrl,
    playerId,
    animationLevel,
    logDensity
  };
}

function readEnum<const T extends string>(key: string, values: readonly T[], fallback: T): T {
  const value = localStorage.getItem(key);
  return values.includes(value as T) ? (value as T) : fallback;
}
