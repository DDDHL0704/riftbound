export type AppRoute =
  | { name: "home" }
  | { name: "cards" }
  | { name: "decks" }
  | { name: "lobby" }
  | { name: "room"; roomId: string }
  | { name: "match"; matchId: string }
  | { name: "result"; matchId: string }
  | { name: "settings" };

export function parseRoute(pathname = window.location.pathname): AppRoute {
  const segments = pathname.split("/").filter(Boolean).map(decodeURIComponent);
  if (segments.length === 0) {
    return { name: "home" };
  }

  if (segments[0] === "cards") {
    return { name: "cards" };
  }

  if (segments[0] === "decks") {
    return { name: "decks" };
  }

  if (segments[0] === "lobby") {
    return { name: "lobby" };
  }

  if (segments[0] === "rooms" && segments[1]) {
    return { name: "room", roomId: segments[1] };
  }

  if (segments[0] === "matches" && segments[1] && segments[2] === "result") {
    return { name: "result", matchId: segments[1] };
  }

  if (segments[0] === "matches" && segments[1]) {
    return { name: "match", matchId: segments[1] };
  }

  if (segments[0] === "settings") {
    return { name: "settings" };
  }

  return { name: "home" };
}

export function routePath(route: AppRoute): string {
  switch (route.name) {
    case "home":
      return "/";
    case "cards":
      return "/cards";
    case "decks":
      return "/decks";
    case "lobby":
      return "/lobby";
    case "room":
      return `/rooms/${encodeURIComponent(route.roomId)}`;
    case "match":
      return `/matches/${encodeURIComponent(route.matchId)}`;
    case "result":
      return `/matches/${encodeURIComponent(route.matchId)}/result`;
    case "settings":
      return "/settings";
  }
}
