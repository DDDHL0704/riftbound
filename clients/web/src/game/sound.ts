import { useSyncExternalStore } from "react";

// Table sound, synthesized with WebAudio so the client ships no audio files. Cues are layered from
// a few voices (noise transients, pitch-dropping thumps, inharmonic bells) and share a generated
// reverb, so they sit in one space. An optional ambience bed plays quietly under the table.
// Sound, volume and ambience are each remembered in this browser.

export type Cue =
  | "play"
  | "play-opp"
  | "cast"
  | "cast-opp"
  | "hit"
  | "destroy"
  | "clash"
  | "conquer"
  | "conquer-opp"
  | "hold"
  | "score-me"
  | "score-opp"
  | "turn"
  | "win"
  | "loss";

const KEY = "riftbound.web.sound";
const read = (key: string) => {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
};
const write = (key: string, value: string) => {
  try {
    localStorage.setItem(key, value);
  } catch {
    /* the choice just isn't remembered */
  }
};

let enabled = read(KEY) !== "off";
let volume = (() => {
  const v = Number(read(KEY + ".volume"));
  return Number.isFinite(v) && v > 0 && v <= 1 ? v : 0.8;
})();
let ambienceOn = read(KEY + ".ambience") !== "off";
const listeners = new Set<() => void>();
const notify = () => listeners.forEach((fn) => fn());
const subscribe = (fn: () => void) => {
  listeners.add(fn);
  return () => listeners.delete(fn);
};

export function soundEnabled() {
  return enabled;
}

export function setSoundEnabled(on: boolean) {
  enabled = on;
  write(KEY, on ? "on" : "off");
  if (!on) stopAmbience();
  else if (ambienceWanted) startAmbience();
  notify();
}

export function soundVolume() {
  return volume;
}

export function setSoundVolume(v: number) {
  volume = Math.min(1, Math.max(0.05, v));
  if (master && ctx) master.gain.setTargetAtTime(volume, ctx.currentTime, 0.05);
  write(KEY + ".volume", String(volume));
  notify();
}

export function setAmbienceEnabled(on: boolean) {
  ambienceOn = on;
  write(KEY + ".ambience", on ? "on" : "off");
  if (on && ambienceWanted) startAmbience();
  else if (!on) stopAmbience();
  notify();
}

export const useSoundVolume = () => useSyncExternalStore(subscribe, () => volume);
export const useSoundEnabled = () => useSyncExternalStore(subscribe, () => enabled);
export const useAmbienceEnabled = () => useSyncExternalStore(subscribe, () => ambienceOn);

let ctx: AudioContext | null = null;
// Every voice plays through master (the volume setting); `wet` feeds the shared reverb.
let master: GainNode | null = null;
let dry: AudioNode | null = null;
let wet: GainNode | null = null;

/** A stereo impulse response of decaying noise: a medium hall, made at start instead of shipped. */
function impulse(a: AudioContext, seconds: number, decay: number) {
  const len = Math.floor(a.sampleRate * seconds);
  const buf = a.createBuffer(2, len, a.sampleRate);
  for (let ch = 0; ch < 2; ch++) {
    const d = buf.getChannelData(ch);
    for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * (1 - i / len) ** decay;
  }
  return buf;
}

function audio(): AudioContext | null {
  if (!enabled || typeof AudioContext === "undefined") return null;
  if (!ctx) {
    ctx = new AudioContext();
    master = ctx.createGain();
    master.gain.value = volume;
    // A gentle compressor keeps stacked cues (a battle's worth of hits) from clipping.
    const comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -16;
    comp.ratio.value = 4;
    comp.attack.value = 0.004;
    comp.release.value = 0.2;
    master.connect(comp).connect(ctx.destination);
    dry = master;
    const verb = ctx.createConvolver();
    verb.buffer = impulse(ctx, 2.2, 3);
    wet = ctx.createGain();
    wet.gain.value = 0.32;
    wet.connect(verb).connect(master);
  }
  // Browsers start the context suspended until the page has seen a user gesture.
  if (ctx.state === "suspended") void ctx.resume();
  return ctx.state === "running" ? ctx : null;
}

/** Routes a voice to the dry mix and, by `send`, to the reverb. */
function out(a: AudioContext, node: AudioNode, send = 0.4) {
  node.connect(dry ?? a.destination);
  if (wet && send > 0) {
    const s = a.createGain();
    s.gain.value = send;
    node.connect(s).connect(wet);
  }
}

/** One enveloped oscillator note, optionally sliding in pitch. */
function tone(a: AudioContext, at: number, freq: number, dur: number, gain: number, type: OscillatorType = "sine", slideTo?: number, send = 0.3) {
  const osc = a.createOscillator();
  const env = a.createGain();
  osc.type = type;
  osc.frequency.setValueAtTime(freq, at);
  if (slideTo) osc.frequency.exponentialRampToValueAtTime(slideTo, at + dur);
  env.gain.setValueAtTime(0.0001, at);
  env.gain.exponentialRampToValueAtTime(gain, at + 0.008);
  env.gain.exponentialRampToValueAtTime(0.0001, at + dur);
  osc.connect(env);
  out(a, env, send);
  osc.start(at);
  osc.stop(at + dur + 0.05);
}

/** A bell: inharmonic partials decaying at different rates, the basis of chimes and clangs. */
function bell(a: AudioContext, at: number, freq: number, dur: number, gain: number, send = 0.6, metal = false) {
  const partials = metal ? [1, 2.4, 3.9, 5.6, 8.2] : [1, 2.76, 5.4, 8.93];
  partials.forEach((p, i) => tone(a, at, freq * p, dur / (1 + i * 0.7), gain / (1 + i * 1.4), "sine", undefined, send));
}

/** A filtered noise burst: card slaps, impacts, sweeps. */
function noise(a: AudioContext, at: number, dur: number, gain: number, freq: number, opts: { type?: BiquadFilterType; q?: number; sweepTo?: number; send?: number; shape?: number } = {}) {
  const buf = a.createBuffer(1, Math.ceil(a.sampleRate * dur), a.sampleRate);
  const data = buf.getChannelData(0);
  const shape = opts.shape ?? 2;
  for (let i = 0; i < data.length; i++) data[i] = (Math.random() * 2 - 1) * (1 - i / data.length) ** shape;
  const src = a.createBufferSource();
  src.buffer = buf;
  const filter = a.createBiquadFilter();
  filter.type = opts.type ?? "bandpass";
  filter.Q.value = opts.q ?? 1;
  filter.frequency.setValueAtTime(freq, at);
  if (opts.sweepTo) filter.frequency.exponentialRampToValueAtTime(opts.sweepTo, at + dur);
  const env = a.createGain();
  env.gain.value = gain;
  src.connect(filter).connect(env);
  out(a, env, opts.send ?? 0.2);
  src.start(at);
}

/** A rising or falling air sweep (noise whose envelope swells, then cuts). */
function whoosh(a: AudioContext, at: number, dur: number, gain: number, from: number, to: number) {
  const buf = a.createBuffer(1, Math.ceil(a.sampleRate * dur), a.sampleRate);
  const data = buf.getChannelData(0);
  for (let i = 0; i < data.length; i++) {
    const t = i / data.length;
    data[i] = (Math.random() * 2 - 1) * Math.sin(Math.PI * t) ** 1.5;
  }
  const src = a.createBufferSource();
  src.buffer = buf;
  const filter = a.createBiquadFilter();
  filter.type = "bandpass";
  filter.Q.value = 2.5;
  filter.frequency.setValueAtTime(from, at);
  filter.frequency.exponentialRampToValueAtTime(to, at + dur);
  const env = a.createGain();
  env.gain.value = gain;
  src.connect(filter).connect(env);
  out(a, env, 0.5);
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
    case "play-opp": {
      // Card slap on the felt, a short air sweep, and a soft body thump.
      const mine = kind === "play";
      noise(a, now, 0.07, 0.55, mine ? 2400 : 1700, { q: 0.8, shape: 3 });
      whoosh(a, now, 0.22, 0.18, mine ? 600 : 500, mine ? 2600 : 1600);
      tone(a, now + 0.02, mine ? 180 : 130, 0.18, 0.16, "sine", mine ? 110 : 80, 0.15);
      break;
    }
    case "cast":
    case "cast-opp": {
      // A rising arcane zap with a sparkle on top.
      const mine = kind === "cast";
      whoosh(a, now, 0.35, 0.22, mine ? 500 : 400, mine ? 5200 : 3600);
      tone(a, now, mine ? 440 : 330, 0.35, 0.05, "triangle", mine ? 1320 : 880, 0.6);
      bell(a, now + 0.28, mine ? 1568 : 1175, 0.6, 0.04, 0.8);
      break;
    }
    case "hit":
      // Impact: a pitch-dropping thump, a crunchy transient and a little grit.
      tone(a, now, 160, 0.22, 0.32, "sine", 48, 0.1);
      noise(a, now, 0.11, 0.7, 700, { q: 0.7, shape: 3 });
      noise(a, now, 0.05, 0.35, 3200, { type: "highpass", shape: 4 });
      break;
    case "destroy":
      // Shatter: a scatter of glassy shards over a low boom and a falling tail.
      tone(a, now, 95, 0.5, 0.3, "sine", 38, 0.2);
      noise(a, now, 0.4, 0.4, 900, { q: 0.6, sweepTo: 200, send: 0.4 });
      for (let i = 0; i < 6; i++) noise(a, now + 0.02 + i * 0.035, 0.06, 0.18, 3000 + Math.random() * 4000, { q: 6, send: 0.5 });
      tone(a, now + 0.05, 420, 0.45, 0.05, "sawtooth", 70, 0.4);
      break;
    case "clash":
      // Steel on steel: two metallic bells a beat apart over an impact.
      noise(a, now, 0.09, 0.6, 2500, { q: 1.5, shape: 3 });
      bell(a, now, 523, 0.9, 0.09, 0.5, true);
      bell(a, now + 0.07, 659, 0.8, 0.07, 0.5, true);
      tone(a, now, 110, 0.3, 0.22, "sine", 55, 0.2);
      break;
    case "conquer":
    case "conquer-opp": {
      // A banner raised: low swell into a bright (or, for the opponent, darker) brass chord.
      const mine = kind === "conquer";
      whoosh(a, now, 0.5, 0.16, 200, 1800);
      const chord = mine ? [392, 494, 587, 784] : [349, 415, 523, 698];
      chord.forEach((f, i) => tone(a, now + 0.18 + i * 0.03, f, 1.1, 0.055, "sawtooth", undefined, 0.6));
      chord.forEach((f) => tone(a, now + 0.18, f * 2, 0.9, 0.02, "triangle", undefined, 0.7));
      tone(a, now + 0.15, 98, 0.8, 0.2, "sine", 70, 0.3);
      break;
    }
    case "hold":
      // Holding ground: a steady low bell with a shield-like shimmer.
      bell(a, now, 262, 1.4, 0.08, 0.7);
      bell(a, now + 0.12, 392, 1.2, 0.05, 0.7);
      break;
    case "score-me":
      bell(a, now, 1047, 0.9, 0.07);
      bell(a, now + 0.08, 1319, 0.9, 0.06);
      bell(a, now + 0.16, 1568, 1.2, 0.06, 0.8);
      break;
    case "score-opp":
      bell(a, now, 659, 0.9, 0.06);
      bell(a, now + 0.1, 523, 1.1, 0.05, 0.7);
      break;
    case "turn":
      // A soft gong to mark the player's turn.
      bell(a, now, 220, 1.6, 0.07, 0.8);
      bell(a, now + 0.02, 659, 1.2, 0.03, 0.8);
      break;
    case "win":
      [523, 659, 784, 1047, 1319].forEach((f, i) => bell(a, now + i * 0.11, f, 1.6, 0.07, 0.7));
      [262, 392, 523].forEach((f) => tone(a, now + 0.45, f, 2.4, 0.05, "sawtooth", undefined, 0.8));
      break;
    case "loss":
      [392, 330, 262, 196].forEach((f, i) => bell(a, now + i * 0.18, f, 1.5, 0.06, 0.8));
      tone(a, now + 0.5, 98, 2.2, 0.08, "triangle", 82, 0.7);
      break;
  }
}

// ---------- ambience: a slow, quiet pad with a rare distant chime ----------

let ambienceWanted = false;
let bed: { gain: GainNode; stop: () => void } | null = null;

/** The table asks for ambience while it is open; it plays only if sound and ambience are on. */
export function wantAmbience(on: boolean) {
  ambienceWanted = on;
  if (on) startAmbience();
  else stopAmbience();
}

function startAmbience() {
  if (bed || !ambienceOn || !ambienceWanted) return;
  const a = audio();
  if (!a) {
    // No gesture yet: try again on the first one.
    const retry = () => {
      window.removeEventListener("pointerdown", retry);
      window.removeEventListener("keydown", retry);
      startAmbience();
    };
    window.addEventListener("pointerdown", retry, { once: true });
    window.addEventListener("keydown", retry, { once: true });
    return;
  }
  const gain = a.createGain();
  gain.gain.setValueAtTime(0.0001, a.currentTime);
  gain.gain.exponentialRampToValueAtTime(0.05, a.currentTime + 4);
  const filter = a.createBiquadFilter();
  filter.type = "lowpass";
  filter.frequency.value = 700;
  filter.Q.value = 0.5;
  // A slow sweep of the filter keeps the pad breathing.
  const lfo = a.createOscillator();
  const lfoGain = a.createGain();
  lfo.frequency.value = 0.05;
  lfoGain.gain.value = 300;
  lfo.connect(lfoGain).connect(filter.frequency);
  filter.connect(gain);
  out(a, gain, 0.8);
  const oscs = [110, 110.6, 164.8, 220.4, 329.6].map((f, i) => {
    const o = a.createOscillator();
    o.type = i < 2 ? "sawtooth" : "triangle";
    o.frequency.value = f;
    const g = a.createGain();
    g.gain.value = i < 2 ? 0.18 : 0.12;
    o.connect(g).connect(filter);
    o.start();
    return o;
  });
  lfo.start();
  const notes = [784, 880, 988, 1175, 1319];
  const timer = setInterval(() => {
    if (!ctx || ctx.state !== "running" || Math.random() < 0.4) return;
    bell(ctx, ctx.currentTime, notes[Math.floor(Math.random() * notes.length)], 2.5, 0.012, 0.95);
  }, 7000);
  bed = {
    gain,
    stop: () => {
      clearInterval(timer);
      const t = a.currentTime;
      gain.gain.cancelScheduledValues(t);
      gain.gain.setTargetAtTime(0.0001, t, 0.6);
      setTimeout(() => {
        oscs.forEach((o) => o.stop());
        lfo.stop();
        gain.disconnect();
      }, 3000);
    },
  };
}

function stopAmbience() {
  bed?.stop();
  bed = null;
}
