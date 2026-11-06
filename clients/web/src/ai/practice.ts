import { useSyncExternalStore } from "react";
import { client, GameClient } from "../net/client";
import type { PreconstructedDeck } from "../game/types";
import { decide, type Level } from "./policy";

/*
 * Practice mode: the computer takes the other seat through its own hub connection in this tab.
 * It answers only the prompts the server sends it, so rules, costs and hidden information stay
 * with the server exactly as in a match between two people.
 */

const META_KEY = "riftbound.web.practice";
const HANDLE_KEY = "riftbound.web.practice.handle";

type Meta = { roomId: string; level: Level; deckId: string };

function readMeta(): Meta | null {
  try {
    return JSON.parse(sessionStorage.getItem(META_KEY) || "null");
  } catch {
    return null;
  }
}

function writeMeta(meta: Meta | null) {
  try {
    if (meta) sessionStorage.setItem(META_KEY, JSON.stringify(meta));
    else sessionStorage.removeItem(META_KEY);
  } catch {
    /* without storage the opponent just won't survive a reload */
  }
}

/** The computer's handle in this browser, if it has played here. */
export function practiceHandle() {
  try {
    return localStorage.getItem(HANDLE_KEY);
  } catch {
    return null;
  }
}

/** One computer handle per browser, so the player's record shows a single practice opponent. */
function botHandle() {
  try {
    const saved = localStorage.getItem(HANDLE_KEY);
    if (saved) return saved;
    const handle = `ai_${Math.random().toString(36).slice(2, 8)}`;
    localStorage.setItem(HANDLE_KEY, handle);
    return handle;
  } catch {
    return `ai_${Math.random().toString(36).slice(2, 8)}`;
  }
}

class PracticeBot {
  readonly client = new GameClient("practice");
  private unsubscribe: (() => void) | null = null;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private busy = false;
  private tried = new Map<string, Set<string>>();
  private submittedDeck = false;
  /** A prompt whose every option was refused; the opponent waits for the next prompt. */
  private exhausted = "";

  constructor(
    readonly meta: Meta,
    private readonly deck: PreconstructedDeck | null,
  ) {}

  async start(server: string) {
    const ok = await this.client.connect(server, botHandle());
    if (!ok) return false;
    if (this.client.state.roomId !== this.meta.roomId) await this.client.joinRoom(this.meta.roomId);
    this.unsubscribe = this.client.subscribe(() => this.schedule());
    this.schedule();
    return true;
  }

  async stop() {
    clearTimeout(this.timer);
    this.unsubscribe?.();
    await this.client.close();
  }

  private schedule() {
    if (this.busy) return;
    clearTimeout(this.timer);
    const { prompt, snapshot } = this.client.state;
    if (!prompt?.actionable || !snapshot || this.client.state.pending || prompt.promptId === this.exhausted) return;
    this.timer = setTimeout(() => void this.act(), 300);
  }

  private async act() {
    const { prompt, snapshot, handle } = this.client.state;
    if (!prompt?.actionable || !snapshot || this.busy) return;
    this.busy = true;
    try {
      const promptId = prompt.promptId ?? "";
      if (prompt.candidates?.some((c) => c.action === "SUBMIT_DECK" && c.enabled) && this.deck && !this.submittedDeck) {
        const receipt = await this.client.submit({
          cmdType: "SUBMIT_DECK",
          legendCardNo: this.deck.legendCardNo,
          championCardNo: this.deck.championCardNo,
          mainDeck: this.deck.mainDeck,
          runeDeck: this.deck.runeDeck,
          battlefields: this.deck.battlefields,
          ...(this.deck.additionalLegendCardNos?.length ? { additionalLegendCardNos: this.deck.additionalLegendCardNos } : {}),
        });
        this.submittedDeck = Boolean(receipt?.accepted);
        return;
      }
      const tried = this.tried.get(promptId) ?? new Set<string>();
      this.tried.set(promptId, tried);
      if (this.tried.size > 40) this.tried.delete(this.tried.keys().next().value!);
      for (const option of decide(prompt, snapshot, handle, this.meta.level)) {
        if (tried.has(option.key)) continue;
        tried.add(option.key);
        let cmd = option.cmd;
        if (option.play) {
          // Quote the play like the table does: add rune taps for missing mana, skip what can't be paid.
          let quote = await this.client.previewPlayCard(cmd);
          const missing = quote?.isValid ? quote.cost?.missingMana ?? 0 : 0;
          if (missing > 0 && option.play.taps.length >= missing) {
            cmd = { ...cmd, optionalCosts: [...((cmd.optionalCosts as string[]) ?? []), ...option.play.taps.slice(0, missing)] };
            quote = await this.client.previewPlayCard(cmd);
          }
          if (quote && !(quote.isValid && quote.canPay)) continue;
        }
        await new Promise((r) => setTimeout(r, option.pause));
        if (this.client.state.prompt?.promptId !== promptId) return;
        const receipt = await this.client.submit(cmd, promptId);
        if (receipt?.accepted) return;
      }
      this.exhausted = promptId;
    } finally {
      this.busy = false;
      // The next prompt may have arrived while this one was being answered; look again.
      this.schedule();
    }
  }
}

let bot: PracticeBot | null = null;
const listeners = new Set<() => void>();
const notify = () => listeners.forEach((fn) => fn());

async function fetchDecks(server: string): Promise<PreconstructedDeck[]> {
  try {
    const r = await fetch(`${server.replace(/\/$/, "")}/decks/preconstructed`);
    return r.ok ? ((await r.json()) as PreconstructedDeck[]) : [];
  } catch {
    return [];
  }
}

/** Creates a room for the player and seats the computer in it with a preconstructed deck. */
export async function startPractice(level: Level, deckId: string | "random") {
  const server = client.state.server;
  const decks = await fetchDecks(server);
  const deck = deckId === "random" ? decks[Math.floor(Math.random() * decks.length)] : decks.find((d) => d.id === deckId);
  if (!deck) {
    client.toast("无法读取电脑使用的卡组");
    return;
  }
  const roomId = await client.createRoom();
  if (!roomId) return;
  await stopPractice();
  const meta = { roomId, level, deckId: deck.id };
  writeMeta(meta);
  bot = new PracticeBot(meta, deck);
  notify();
  if (!(await bot.start(server))) {
    client.toast("电脑对手没能加入房间，请重试");
    await stopPractice();
  }
}

/** After a reload: put the computer back in its seat if the player is resuming that room. */
export async function resumePractice() {
  const meta = readMeta();
  if (!meta || bot) return;
  const decks = await fetchDecks(client.state.server);
  bot = new PracticeBot(meta, decks.find((d) => d.id === meta.deckId) ?? null);
  notify();
  // The deck was submitted before the reload unless the room is still in setup; the server says which.
  if (!(await bot.start(client.state.server))) await stopPractice();
}

export async function stopPractice() {
  const current = bot;
  bot = null;
  writeMeta(null);
  notify();
  await current?.stop();
}

/** The practice room this tab is hosting, if any (for labels such as "电脑 · 普通"). */
export function usePractice() {
  return useSyncExternalStore(
    (fn) => {
      listeners.add(fn);
      return () => listeners.delete(fn);
    },
    () => bot?.meta ?? null,
  );
}

// The computer leaves when the player leaves its room. The player's client only reports the room
// once the server confirms the seat, so the computer waits until the player has been seen there.
let playerSeated = false;
client.subscribe(() => {
  if (!bot) {
    playerSeated = false;
    return;
  }
  const here = client.state.roomId === bot.meta.roomId && client.state.phase === "room";
  if (here) playerSeated = true;
  else if (playerSeated && client.state.phase !== "connecting") {
    playerSeated = false;
    void stopPractice();
  }
});

// Read-only handle for the UI soak and devtools, like window.__rb for the player's client.
if (import.meta.env.DEV) (window as unknown as { __practice: () => PracticeBot | null }).__practice = () => bot;

export const LEVEL_LABEL: Record<Level, string> = { easy: "简单", normal: "普通" };
