import { cardInfo } from "./cards";

/*
 * Card art comes from an external CDN. Images the player is about to see are fetched ahead in the
 * background, a few at a time, and a shared set records which ones have arrived so a card that
 * mounts later can show its art at once instead of fading in again.
 */

const loaded = new Set<string>();
const queued = new Set<string>();
const waiting: string[] = [];
let active = 0;
const PARALLEL = 6;

export const artLoaded = (src: string) => loaded.has(src);
export const markArtLoaded = (src: string) => void loaded.add(src);

function pump() {
  while (active < PARALLEL && waiting.length) {
    const src = waiting.shift()!;
    active++;
    const img = new Image();
    img.decoding = "async";
    const done = (ok: boolean) => {
      if (ok) loaded.add(src);
      active--;
      pump();
    };
    img.onload = () => done(true);
    img.onerror = () => done(false);
    img.src = src;
  }
}

/** Starts fetching the art of these cards (duplicates and already loaded ones are skipped). */
export function preloadArt(cardNos: Iterable<string | null | undefined>) {
  for (const no of cardNos) {
    const src = cardInfo(no)?.image;
    if (!src || loaded.has(src) || queued.has(src)) continue;
    queued.add(src);
    waiting.push(src);
  }
  pump();
}

/** Lets the art service worker keep card images on this device between visits. */
export function registerArtCache() {
  // Any other card image that fails (deck tiles, previews, packs) hides instead of showing a broken icon;
  // those places all print the card's name beside the image.
  document.addEventListener(
    "error",
    (e) => {
      if (e.target instanceof HTMLImageElement) e.target.classList.add("img--broken");
    },
    true,
  );
  if (!("serviceWorker" in navigator)) return;
  navigator.serviceWorker.register(`${import.meta.env.BASE_URL}art-sw.js`).catch(() => {
    /* without it images simply come from the CDN each visit */
  });
}
