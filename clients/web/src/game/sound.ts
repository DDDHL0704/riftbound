import { useSyncExternalStore } from "react";

// Table sound cues, synthesized with WebAudio so the client ships no audio files. Every cue is short
// and quiet; the player can switch them off, and the choice is remembered in this browser.

export type Cue = "play" | "play-opp" | "hit" | "destroy" | "score-me" | "score-opp" | "turn" | "win" | "loss";

const KEY = "riftbound.web.sound";
let enabled = (() => {
  try {
    return localStorage.getItem(KEY) !== "off";
  } catch {
    return true;
  }
})();
const listeners = new Set<() => void>();

export function soundEnabled() {
  return enabled;
}

export function setSoundEnabled(on: boolean) {
  enabled = on;
  try {
    localStorage.setItem(KEY, on ? "on" : "off");
  } catch {
    /* the choice just isn't remembered */
  }
  listeners.forEach((fn) => fn());
}

export function useSoundEnabled() {
  return useSyncExternalStore(
    (fn) => {
      listeners.add(fn);
      return () => listeners.delete(fn);
    },
    () => enabled,
  );
}

let ctx: AudioContext | null = null;
function audio(): AudioContext | null {
  if (!enabled || typeof AudioContext === "undefined") return null;
  ctx ??= new AudioContext();
  // Browsers start the context suspended until the page has seen a user gesture.
  if (ctx.state === "suspended") void ctx.resume();
  return ctx.state === "running" ? ctx : null;
}

/** One enveloped oscillator note. */
function tone(a: AudioContext, at: number, freq: number, dur: number, gain: number, type: OscillatorType = "sine", slideTo?: number) {
  const osc = a.createOscillator();
  const env = a.createGain();
  osc.type = type;
  osc.frequency.setValueAtTime(freq, at);
  if (slideTo) osc.frequency.exponentialRampToValueAtTime(slideTo, at + dur);
  env.gain.setValueAtTime(0.0001, at);
  env.gain.exponentialRampToValueAtTime(gain, at + 0.01);
  env.gain.exponentialRampToValueAtTime(0.0001, at + dur);
  osc.connect(env).connect(a.destination);
  osc.start(at);
  osc.stop(at + dur + 0.02);
}

/** A filtered noise burst: card slaps, impacts. */
function noise(a: AudioContext, at: number, dur: number, gain: number, freq: number) {
  const buf = a.createBuffer(1, Math.ceil(a.sampleRate * dur), a.sampleRate);
  const data = buf.getChannelData(0);
  for (let i = 0; i < data.length; i++) data[i] = (Math.random() * 2 - 1) * (1 - i / data.length) ** 2;
  const src = a.createBufferSource();
  src.buffer = buf;
  const filter = a.createBiquadFilter();
  filter.type = "bandpass";
  filter.frequency.value = freq;
  const env = a.createGain();
  env.gain.value = gain;
  src.connect(filter).connect(env).connect(a.destination);
  src.start(at);
}

// Several events of one kind often land together (a battle damages many units); one cue each is enough.
const lastAt = new Map<Cue, number>();

export function cue(kind: Cue) {
  const a = audio();
  if (!a) return;
  const now = a.currentTime;
  if (now - (lastAt.get(kind) ?? -1) < 0.12) return;
  lastAt.set(kind, now);
  switch (kind) {
    case "play":
    case "play-opp":
      noise(a, now, 0.09, 0.5, kind === "play" ? 1800 : 1200);
      tone(a, now, kind === "play" ? 220 : 165, 0.12, 0.12, "triangle");
      break;
    case "hit":
      noise(a, now, 0.12, 0.6, 500);
      tone(a, now, 140, 0.16, 0.18, "sine", 70);
      break;
    case "destroy":
      noise(a, now, 0.3, 0.45, 900);
      tone(a, now, 300, 0.32, 0.1, "sawtooth", 60);
      break;
    case "score-me":
      tone(a, now, 784, 0.18, 0.08);
      tone(a, now + 0.09, 1175, 0.3, 0.08);
      break;
    case "score-opp":
      tone(a, now, 523, 0.18, 0.07);
      tone(a, now + 0.09, 392, 0.3, 0.07);
      break;
    case "turn":
      tone(a, now, 659, 0.5, 0.06);
      tone(a, now, 988, 0.5, 0.03);
      break;
    case "win":
      [523, 659, 784, 1047].forEach((f, i) => tone(a, now + i * 0.12, f, 0.4, 0.08, "triangle"));
      break;
    case "loss":
      [392, 330, 262].forEach((f, i) => tone(a, now + i * 0.16, f, 0.45, 0.07, "triangle"));
      break;
  }
}
