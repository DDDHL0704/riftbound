// Keeps card art from the image CDN on this device, so a second visit draws cards at once.
// It only ever answers requests to that CDN; every other request goes to the network untouched.
const CACHE = "card-art-v1";
const HOST = "cdn.playloltcg.com";
// The CDN sends no CORS headers, so entries are opaque and the browser counts each one generously
// against storage; a few hundred cards covers several decks without crowding anything else out.
const LIMIT = 320;

self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", (event) => event.waitUntil(self.clients.claim()));

async function trim(cache) {
  const keys = await cache.keys();
  for (const key of keys.slice(0, Math.max(0, keys.length - LIMIT))) await cache.delete(key);
}

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") return;
  let url;
  try {
    url = new URL(request.url);
  } catch {
    return;
  }
  if (url.hostname !== HOST) return;
  event.respondWith(
    (async () => {
      const cache = await caches.open(CACHE);
      const hit = await cache.match(request, { ignoreVary: true });
      if (hit) return hit;
      const response = await fetch(request);
      // An opaque response hides its status; only keep it when it carries a body.
      if (response.ok || (response.type === "opaque" && request.destination === "image")) {
        await cache.put(request, response.clone());
        void trim(cache);
      }
      return response;
    })().catch(() => fetch(request)),
  );
});
