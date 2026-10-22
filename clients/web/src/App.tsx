import { useEffect, useSyncExternalStore } from "react";
import { client } from "./net/client";
import { useCards } from "./game/cards";
import { ConnectScreen, LobbyScreen, SetupScreen } from "./ui/Lobby";
import { Table } from "./ui/Table";

let resumed = false;

export function App() {
  const state = useSyncExternalStore(client.subscribe, client.getState);
  useCards();

  useEffect(() => {
    if (resumed) return;
    resumed = true;
    if (client.hasResumableRoom()) void client.resume();
  }, []);

  useEffect(() => {
    if (!state.toast) return;
    const t = setTimeout(() => client.dismissToast(), 4200);
    return () => clearTimeout(t);
  }, [state.toast]);

  let screen;
  if (state.phase === "offline" || state.phase === "connecting") screen = <ConnectScreen state={state} />;
  else if (state.phase === "lobby" || state.phase === "matchmaking") screen = <LobbyScreen state={state} />;
  else if (!state.snapshot || state.snapshot.timing.roomStatus !== "IN_PROGRESS" && !state.snapshot.timing.winnerPlayerId)
    screen = <SetupScreen state={state} snapshot={state.snapshot} prompt={state.prompt} />;
  else screen = <Table state={state} snapshot={state.snapshot} prompt={state.prompt} />;

  return (
    <>
      {screen}
      {state.toast && (
        <div className={`toast toast--${state.toast.tone}`} role="alert" onClick={() => client.dismissToast()}>
          {state.toast.text}
        </div>
      )}
    </>
  );
}
