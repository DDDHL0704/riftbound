// UI soak test: two isolated browser contexts play whole matches by clicking the web client
// only, choosing randomly among the actions it offers. It reports places where the server
// waits on a player but the page offers no control ("stuck"), server rejections and page errors.
//
//   npm run soak -- [pairs] [stepCap]      e.g. npm run soak -- 0-1,2-4 900
//   npm run soak -- bot <roomCode> [deck]  one visible-free seat joins a room and plays it out,
//                                          so a person can play the other seat by hand
//
// Needs the API (default http://127.0.0.1:5088) and this dev server (npm run dev) running:
// stuck detection reads the client state the dev build exposes as window.__rb (it never acts through it).
// Uses the system Chrome; set CHROME_PATH to use another Chromium build.
import fs from "node:fs";
import path from "node:path";
import { chromium } from "playwright-core";

const WEB = process.env.WEB_URL || "http://127.0.0.1:5176";
const OUT_DIR = path.resolve(process.env.SOAK_OUT || "soak-results");
const PAIRS = (process.argv[2] === "bot" ? "" : process.argv[2] || "0-1,2-4,3-9,5-10,6-7,8-0").split(",").map((p) => p.split("-").map(Number));
const STEP_CAP = Number(process.argv[3] || 1200);
const SHOT_EVERY = Number(process.env.SOAK_SHOTS || 0);
const RELOAD_EVERY = Number(process.env.SOAK_RELOAD || 0);
const SHOT_TITLES = process.env.SOAK_SHOT_TITLES ? new RegExp(process.env.SOAK_SHOT_TITLES) : null;
const [VW, VH] = (process.env.SOAK_VIEW || "1440x900").split("x").map(Number);
// SOAK_MOBILE=1 emulates a touch device, which honours the page's viewport meta tag.
const CONTEXT = { viewport: { width: VW, height: VH }, ...(process.env.SOAK_MOBILE ? { isMobile: true, hasTouch: true } : {}) };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// Runs inside the page: performs one UI action, returns a short label or null when nothing to do.
const STEP = async () => {
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const pick = (a) => a[Math.floor(Math.random() * a.length)];
  const vis = (el) => el && el.offsetParent !== null;
  const btns = (sel) => [...document.querySelectorAll(sel)].filter((b) => !b.disabled && vis(b));
  const title = document.querySelector(".rail__title")?.textContent ?? "";
  const turn = document.querySelector(".rail__turn")?.textContent ?? "";
  if (document.querySelector(".result")) return "RESULT";
  // The first table in a fresh browser opens a tour; a player would read it and skip it.
  const skipGuide = [...document.querySelectorAll(".guide button")].find((b) => b.textContent === "跳过");
  if (skipGuide) {
    skipGuide.click();
    return "guide:skip";
  }

  // 1. A half-built selection on the board.
  const bar = document.querySelector(".pickbar:not(.pickbar--leaving)");
  if (bar) {
    const toggles = [...bar.querySelectorAll(".chip--toggle input")];
    const confirm = [...bar.querySelectorAll("button")].find((b) => b.textContent.startsWith("确认"));
    if (confirm) {
      for (const t of toggles) if (!t.checked && Math.random() < 0.15) t.click();
      if (confirm.disabled) {
        bar.querySelector(".pickbar__cancel").click();
        return "pick:cancel-unaffordable";
      }
      confirm.click();
      return "pick:confirm";
    }
    const chips = [...bar.querySelectorAll("button.chip")];
    const real = chips.filter((c) => c.textContent !== "不选");
    // Sometimes add another mover to a group move before choosing the destination.
    const peers = [...document.querySelectorAll(".side--me .card--legal, .field__row--me .card--legal")];
    if (bar.textContent.includes("一起移动") && peers.length && Math.random() < 0.4) {
      pick(peers).click();
      return "pick:group-add";
    }
    if (real.length) {
      pick(real).click();
      return "pick:choice";
    }
    const skip = chips.find((c) => c.textContent === "不选");
    if (skip) {
      skip.click();
      return "pick:skip";
    }
    const what = bar.querySelector(".pickbar__title")?.textContent + " / " + [...bar.querySelectorAll(".pickbar__hint")].map((h) => h.textContent).join(" ");
    bar.querySelector(".pickbar__cancel").click();
    return "pick:cancel-empty:" + what;
  }

  if (title === "等待对手" || title === "对局结束") return null;

  // 2. Sidebar panels for prompts that are not plain main actions.
  if (title === "起手调度") {
    const hand = [...document.querySelectorAll(".mulligan__card")];
    for (const c of hand) if (Math.random() < 0.3) c.click();
    await sleep(100);
    const b = btns(".mulligan .btn--primary")[0];
    b?.click();
    return b ? "mulligan:" + b.textContent : null;
  }
  if (title === "声明战斗" || (document.querySelector(".panel--battle") && Math.random() < 0.6)) {
    const chips = btns(".panel--battle button.chip");
    for (const c of chips) if (Math.random() < 0.5) c.click();
    for (const t of document.querySelectorAll(".panel--battle .chip--toggle input")) if (Math.random() < 0.2) t.click();
    await sleep(100);
    const b = btns(".panel--battle button.btn--primary")[0];
    if (b) {
      b.click();
      return "battle:declare" + (chips.length ? "+targets" : "");
    }
    return null;
  }
  if (title === "分配战斗伤害" || document.querySelector(".panel--damage")) {
    const b = btns(".panel--damage button.btn--primary")[0];
    b?.click();
    return b ? "damage:" + title : null;
  }
  if (title === "触发排序") {
    const ups = btns(".order__moves button");
    if (ups.length && Math.random() < 0.5) pick(ups).click();
    const b = btns(".rail .panel button.btn--primary")[0];
    b?.click();
    return b ? "order" : null;
  }
  if (title === "支付费用") {
    for (const t of document.querySelectorAll(".rail .panel .chip--toggle input")) if (Math.random() < 0.3) t.click();
    const options = btns(".rail .panel .btn");
    if (options.length) {
      const o = pick(options);
      o.click();
      return "pay:" + o.textContent;
    }
    return null;
  }
  if (title === "选择手牌" || title === "选择卡牌") {
    const cards = [...document.querySelectorAll(".rail .choices .card, .rail .choices .chip")];
    for (const c of cards) if (Math.random() < 0.5) c.click();
    await sleep(80);
    let b = btns(".rail .panel button.btn--primary")[0];
    if (!b) {
      // Not enough picked yet: pick in order until the button enables.
      for (const c of cards) {
        if (btns(".rail .panel button.btn--primary")[0]) break;
        c.click();
        await sleep(40);
      }
      b = btns(".rail .panel button.btn--primary")[0];
    }
    b?.click();
    return b ? "choice:" + title : null;
  }
  const generic = btns(".rail .panel .btn").filter((b) => !b.closest(".panel--battle,.panel--damage"));
  if (generic.length && title !== "主行动" && title !== "优先行动" && title !== "法术对决") {
    pick(generic).click();
    return "generic:" + title;
  }

  // Rune multi-select bar: run one of its actions.
  const runeButtons = btns(".runebar .btn");
  if (runeButtons.length) {
    const b = pick(runeButtons);
    b.click();
    return "runes:" + b.textContent.replace(/[+\d\s]+.*$/, "");
  }

  // 3. Board actions: tap runes, play cards, use abilities, move units.
  const runes = [...document.querySelectorAll(".runes .card--playable")].filter(vis);
  if (runes.length && title === "主行动" && Math.random() < 0.12) {
    runes.slice(0, 1 + Math.floor(Math.random() * runes.length)).forEach((r) => r.click());
    return "runes:select";
  }
  const actionable = [...document.querySelectorAll(".card--playable, .card--autopay")].filter((c) => vis(c) && !c.closest(".runes"));
  const mainTurn = title === "主行动" && turn.includes("你的回合");
  const reacting = title === "优先行动" || title === "法术对决";
  const tapAll = btns(".dock button").find((b) => b.textContent.startsWith("全部横置"));
  if (mainTurn && tapAll && Math.random() < 0.8) {
    tapAll.click();
    return "tap-all";
  }
  const chance = mainTurn ? 0.85 : reacting ? 0.3 : 0;
  if (actionable.length && Math.random() < chance) {
    const c = pick(actionable);
    c.click();
    await sleep(150);
    const items = [...document.querySelectorAll(".menu__item")].filter((b) => !b.classList.contains("menu__item--quiet"));
    if (items.length) {
      const item = pick(items);
      const label = item.textContent;
      item.click();
      return "act:" + label.replace(/\d+ ?(法力|符能|经验).*/, "").slice(0, 16);
    }
    // Units skip the menu and go straight into choosing where to move.
    if (document.querySelector(".pickbar:not(.pickbar--leaving)")) return "act:move";
    document.body.click();
  }
  // Dock labels may carry a hotkey hint after the text ("结束回合空格"), so match the start.
  const label = (b) => b.textContent.replace(/空格$/, "");
  const pass = btns(".dock button").find((b) => ["让过", "让过焦点", "跳过"].includes(label(b)));
  if (pass) {
    pass.click();
    return "pass:" + pass.textContent;
  }
  const end = btns(".dock button").find((b) => label(b) === "结束回合");
  if (end && mainTurn) {
    end.click();
    return "end-turn";
  }
  return null;
};

// Diagnostic read only: is the server waiting on this player for something?
const PENDING = () => {
  const st = window.__rb?.state;
  const p = st?.prompt;
  if (!p?.actionable) return null;
  const enabled = (p.candidates ?? []).filter((c) => c.enabled && !["SURRENDER", "WAIT"].includes(c.action)).map((c) => c.action);
  return enabled.length ? { type: p.view?.type, enabled, promptId: p.promptId } : null;
};

async function login(page, handle) {
  await page.goto(WEB);
  await page.fill(".gate__form input", handle);
  await page.click(".gate__form button[type=submit]");
  await page.waitForSelector(".lobby__grid", { timeout: 15000 });
}

async function playMatch(browser, a, b, idx) {
  const report = { pair: [a, b], stuck: [], rejections: [], pageErrors: [], actions: {}, titles: {}, steps: 0, result: null };
  const ctxs = [await browser.newContext(CONTEXT), await browser.newContext(CONTEXT)];
  const pages = await Promise.all(ctxs.map((c) => c.newPage()));
  // Tally the floating feedback marks (damage / destroy / score) the page shows during the match.
  for (const p of pages)
    await p.addInitScript(() => {
      window.__fx = {};
      new MutationObserver((records) => {
        for (const r of records)
          for (const n of r.addedNodes) {
            if (!(n instanceof HTMLElement)) continue;
            // The first mark arrives inside a newly added container, so look inside added nodes too.
            const marks = n.classList.contains("fx__item") ? [n] : [...n.querySelectorAll(".fx__item")];
            for (const m of marks) {
              const kind = [...m.classList].find((c) => c.startsWith("fx__item--"))?.slice(10) ?? "other";
              window.__fx[kind] = (window.__fx[kind] ?? 0) + 1;
            }
          }
        // Opponent objects that light up after arriving somewhere new.
        for (const r of records)
          if (r.type === "attributes" && r.target instanceof HTMLElement && r.target.classList.contains("card--recent") && !String(r.oldValue).includes("card--recent"))
            window.__fx.recent = (window.__fx.recent ?? 0) + 1;
      }).observe(document, { childList: true, subtree: true, attributes: true, attributeFilter: ["class"], attributeOldValue: true });
    });
  pages.forEach((p, i) => {
    p.on("pageerror", (e) => report.pageErrors.push(`${i}: ${e.message}`));
    p.on("console", (m) => m.type() === "error" && !/Failed to load resource/.test(m.text()) && report.pageErrors.push(`${i} console: ${m.text().slice(0, 200)}`));
  });
  const names = [`s${idx}a${Date.now() % 1e5}`, `s${idx}b${Date.now() % 1e5}`];
  await login(pages[0], names[0]);
  await login(pages[1], names[1]);
  await pages[0].click("button:text-is(\"创建房间\")");
  await pages[0].waitForSelector(".setup__code");
  const code = await pages[0].textContent(".setup__code");
  await pages[1].fill(".lobby__row .input", code);
  await pages[1].click(".lobby__row button[type=submit]");
  for (const [i, deck] of [[0, a], [1, b]]) {
    const p = pages[i];
    await p.waitForSelector(".deck");
    await p.locator(".deck").nth(deck).click();
    await p.click("button:text-is(\"使用此卡组\")");
    await p.waitForSelector("button:text-is(\"准备开战\")");
    await p.click("button:text-is(\"准备开战\")");
  }

  const idle = [0, 0];
  const t0 = Date.now();
  while (report.steps < STEP_CAP && Date.now() - t0 < 12 * 60 * 1000) {
    let acted = false;
    for (let i = 0; i < 2; i++) {
      const p = pages[i];
      const label = await p.evaluate(STEP).catch((e) => "EVAL_ERR " + e.message.slice(0, 120));
      if (process.env.SOAK_LOG)
        for (const t of await p.$$eval(".log__item", (els) => els.map((e) => e.textContent ?? "")).catch(() => []))
          (report.logLines ??= new Set()).add(t.replace(/×\d+$/, ""));
      if (label === "RESULT") {
        report.result = await p.evaluate(() => document.querySelector(".result")?.textContent ?? null);
        if (SHOT_EVERY) await p.screenshot({ path: path.join(OUT_DIR, `result-${idx}.png`) });
        break;
      }
      const title = await p.evaluate(() => document.querySelector(".rail__title")?.textContent ?? "");
      if (title) report.titles[title] = (report.titles[title] ?? 0) + 1;
      // SOAK_SHOT_TITLES="分配战斗伤害|触发排序" saves the first time each matching prompt shows up.
      if (SHOT_TITLES && SHOT_TITLES.test(title) && report.titles[title] === 1)
        await p.screenshot({ path: path.join(OUT_DIR, `prompt-${idx}-${title}.png`) });
      if (label) {
        acted = true;
        idle[i] = 0;
        report.steps++;
        const key = label.split(":")[0] + ":" + (label.split(":")[1] ?? "");
        if (label.startsWith("pick:cancel-empty")) (report.emptyPicks ??= []).push(label.slice(18));
        report.actions[key] = (report.actions[key] ?? 0) + 1;
        await sleep(250);
        // SOAK_RELOAD=n reloads this seat's page every n actions: the tab must rejoin its seat and play on.
        if (RELOAD_EVERY && report.steps % RELOAD_EVERY === 0) {
          await p.reload();
          await p.waitForSelector(".table", { timeout: 15000 }).catch(() => report.rejections.push({ player: i, after: "reload", toast: "未能回到对局" }));
          report.reloads = (report.reloads ?? 0) + 1;
        }
        // SOAK_SHOTS=n saves both seats every n actions, for reviewing the table by eye.
        if (SHOT_EVERY && report.steps % SHOT_EVERY === 0)
          for (const [j, q] of pages.entries()) await q.screenshot({ path: path.join(OUT_DIR, `view-${idx}-${String(report.steps).padStart(4, "0")}-${j}.png`) });
        const toast = await p.evaluate(() => {
          const t = document.querySelector(".toast");
          const text = t?.textContent ?? null;
          t?.click();
          return text;
        });
        // An opponent action landing while this click was in flight expires the prompt; that is a
        // legitimate race, reported apart from real rejections.
        if (toast && (toast.includes("行动窗口已更新") || toast.includes("行动窗口已过期"))) report.races = (report.races ?? 0) + 1;
        else if (toast && toast.startsWith("费用不足")) {
          report.unaffordable = (report.unaffordable ?? 0) + 1;
          (report.unaffordableAfter ??= []).push(`${label} → ${toast}`.slice(0, 120));
        }
        else if (toast) report.rejections.push({ player: i, after: label, title, toast });
      } else {
        const pending = await p.evaluate(PENDING);
        if (pending) {
          idle[i]++;
          if (idle[i] === 8) {
            const panel = await p.evaluate(() => document.querySelector(".rail__panel")?.textContent ?? "");
            const shot = path.join(OUT_DIR, `stuck-${idx}-${i}-${report.stuck.length}.png`);
            await p.screenshot({ path: shot });
            report.stuck.push({ player: i, title, pending, panel: panel?.slice(0, 300), shot });
          }
        } else idle[i] = 0;
      }
    }
    if (report.result) break;
    if (!acted) await sleep(300);
    if (idle[0] >= 8 && idle[1] >= 8) break;
    if (report.stuck.length >= 3) break;
  }
  report.seconds = Math.round((Date.now() - t0) / 1000);
  // SOAK_EVAL=file.js runs that script in both seats when the match stops, e.g. to measure layout.
  if (process.env.SOAK_EVAL) {
    const code = fs.readFileSync(process.env.SOAK_EVAL, "utf8");
    report.eval = await Promise.all(pages.map((q) => q.evaluate(code).catch((e) => String(e))));
    if (SHOT_EVERY) for (const [j, q] of pages.entries()) await q.screenshot({ path: path.join(OUT_DIR, `eval-${idx}-${j}.png`) });
  }
  if (report.logLines) report.logLines = [...report.logLines].sort();
  report.fx = await pages[0].evaluate(() => window.__fx).catch(() => null);
  report.score = await pages[0].$$eval(".spine__score", (els) => els.map((e) => e.textContent)).catch(() => null);
  await Promise.all(ctxs.map((c) => c.close()));
  return report;
}

async function playBot(browser, code, deck) {
  const ctx = await browser.newContext(CONTEXT);
  const page = await ctx.newPage();
  page.on("pageerror", (e) => console.log("page error:", e.message));
  await login(page, `bot${Date.now() % 1e5}`);
  await page.fill(".lobby__row .input", code);
  await page.click(".lobby__row button[type=submit]");
  await page.waitForSelector(".deck");
  await page.locator(".deck").nth(deck).click();
  await page.click("button:text-is(\"使用此卡组\")");
  await page.waitForSelector("button:text-is(\"准备开战\")");
  await page.click("button:text-is(\"准备开战\")");
  console.log(`bot joined ${code} with deck ${deck}`);
  for (;;) {
    const label = await page.evaluate(STEP).catch((e) => "EVAL_ERR " + e.message.slice(0, 120));
    if (label === "RESULT") break;
    if (label) {
      console.log(label);
      // Slow enough for the person at the other seat to follow along.
      await sleep(900);
      await page.evaluate(() => document.querySelector(".toast")?.click());
    } else await sleep(400);
  }
  console.log("match over:", await page.textContent(".result").catch(() => ""));
  await ctx.close();
}

fs.mkdirSync(OUT_DIR, { recursive: true });
const browser = await chromium.launch(
  process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH, headless: true } : { channel: "chrome", headless: true },
);
if (process.argv[2] === "bot") {
  await playBot(browser, process.argv[3], Number(process.argv[4] ?? 1));
  await browser.close();
  process.exit(0);
}
const reports = [];
for (const [i, [a, b]] of PAIRS.entries()) {
  const r = await playMatch(browser, a, b, i).catch((e) => ({ pair: [a, b], crash: e.message }));
  reports.push(r);
  console.log(
    JSON.stringify({
      pair: r.pair,
      result: r.result,
      steps: r.steps,
      stuck: r.stuck?.length,
      rejections: r.rejections?.length,
      races: r.races,
      reloads: r.reloads,
      unaffordable: r.unaffordable,
      pageErrors: r.pageErrors?.length,
      fx: r.fx,
      crash: r.crash,
    }),
  );
  fs.writeFileSync(path.join(OUT_DIR, "report.json"), JSON.stringify(reports, null, 1));
}
await browser.close();
const failed = reports.some((r) => r.crash || r.stuck?.length || r.rejections?.length || r.pageErrors?.length || !r.result);
console.log(failed ? `Problems found; details in ${OUT_DIR}/report.json` : "All matches finished without problems.");
process.exit(failed ? 1 : 0);
