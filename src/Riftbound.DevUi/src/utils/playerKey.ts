// A persistent secret that proves ownership of the player's handle to the server.
// Generated once per browser and never shown in the UI; the server only stores its hash.
const PLAYER_KEY_STORAGE = "riftbound.playerKey";
const MINIMUM_KEY_LENGTH = 16;

export function getOrCreatePlayerKey(): string {
  if (typeof window === "undefined") {
    return generatePlayerKey();
  }

  const existing = window.localStorage.getItem(PLAYER_KEY_STORAGE);
  if (existing && existing.length >= MINIMUM_KEY_LENGTH) {
    return existing;
  }

  const next = generatePlayerKey();
  window.localStorage.setItem(PLAYER_KEY_STORAGE, next);
  return next;
}

function generatePlayerKey(): string {
  const cryptoApi = typeof globalThis !== "undefined" ? globalThis.crypto : undefined;
  if (cryptoApi?.randomUUID) {
    return `pk_${cryptoApi.randomUUID()}${cryptoApi.randomUUID()}`;
  }

  if (cryptoApi?.getRandomValues) {
    const bytes = cryptoApi.getRandomValues(new Uint8Array(24));
    return `pk_${Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("")}`;
  }

  return `pk_${Date.now().toString(16)}${Math.random().toString(16).slice(2)}${Math.random().toString(16).slice(2)}`;
}
