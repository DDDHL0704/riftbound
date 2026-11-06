import { useState, type ReactNode } from "react";
import { artLoaded, markArtLoaded } from "../game/art";

type ArtProps = {
  src: string;
  className?: string;
  alt?: string;
  /** Shown when the image cannot be loaded, e.g. the card's name and cost. */
  fallback: ReactNode;
  lazy?: boolean;
};

/** A card image that fades in once loaded and falls back to text when the CDN fails. */
export function Art({ src, className = "", alt = "", fallback, lazy }: ArtProps) {
  const [state, setState] = useState<{ src: string; status: "wait" | "ok" | "bad" }>(() => ({ src, status: artLoaded(src) ? "ok" : "wait" }));
  // A new source starts over (the same element can be reused for another card).
  const status = state.src === src ? state.status : artLoaded(src) ? "ok" : "wait";
  if (status === "bad") return <>{fallback}</>;
  return (
    <img
      className={`${className} art art--${status}`}
      src={src}
      alt={alt}
      loading={lazy ? "lazy" : undefined}
      decoding="async"
      draggable={false}
      onLoad={() => {
        markArtLoaded(src);
        setState({ src, status: "ok" });
      }}
      onError={() => setState({ src, status: "bad" })}
    />
  );
}
