import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { loadCards } from "./game/cards";
import { registerArtCache } from "./game/art";
import { client } from "./net/client";
import "./styles.css";
import "./polish.css";
import "./stage.css";

loadCards();
registerArtCache();
// Handy for inspecting live protocol state from the devtools console.
if (import.meta.env.DEV) (window as unknown as { __rb: typeof client }).__rb = client;
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
