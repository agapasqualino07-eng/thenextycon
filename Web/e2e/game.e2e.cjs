// End-to-end tests: start a static server for the PUBLISHED app under a sub-path (like GitHub Pages),
// open it in Chromium with an iPhone-sized touch viewport and drive the real UI.
// Usage: RELEASE_DIR=<publish>/wwwroot DEV_DIR=<dev publish>/wwwroot node --test --test-concurrency=1 game.e2e.cjs
'use strict';
const { test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');

const RELEASE_DIR = process.env.RELEASE_DIR;
const DEV_DIR = process.env.DEV_DIR;
const SHOTS = process.env.SCREENSHOT_DIR || '';
const BASE = '/thenextycon/';
const IPHONE = {
  viewport: { width: 390, height: 844 }, deviceScaleFactor: 3, isMobile: true, hasTouch: true,
  userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1',
  locale: 'it-IT'
};
const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json',
  '.wasm': 'application/wasm', '.png': 'image/png', '.webmanifest': 'application/manifest+json', '.dat': 'application/octet-stream',
  '.dll': 'application/octet-stream', '.ico': 'image/x-icon', '.blat': 'application/octet-stream' };

function serve(root) {
  const rootAbs = path.resolve(root);
  const server = http.createServer((req, res) => {
    const url = new URL(req.url, 'http://localhost');
    if (!url.pathname.startsWith(BASE)) { res.writeHead(404); return res.end(); }
    let rel = decodeURIComponent(url.pathname.slice(BASE.length)) || 'index.html';
    const file = path.resolve(rootAbs, rel);
    if (!file.startsWith(rootAbs + path.sep)) { res.writeHead(403); return res.end(); }   // no path traversal
    fs.readFile(file, (err, data) => {
      if (err) { res.writeHead(404); return res.end(); }
      res.writeHead(200, { 'Content-Type': MIME[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-cache' });
      res.end(data);
    });
  });
  return new Promise(resolve => server.listen(0, '127.0.0.1', () => resolve(server)));
}

let browser, releaseServer, devServer, releaseUrl, devUrl;
before(async () => {
  assert.ok(RELEASE_DIR && fs.existsSync(path.join(RELEASE_DIR, 'index.html')), 'RELEASE_DIR must point to a published wwwroot');
  releaseServer = await serve(RELEASE_DIR);
  releaseUrl = `http://127.0.0.1:${releaseServer.address().port}${BASE}`;
  if (DEV_DIR) { devServer = await serve(DEV_DIR); devUrl = `http://127.0.0.1:${devServer.address().port}${BASE}`; }
  browser = await chromium.launch(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {});
});
after(async () => { await browser?.close(); releaseServer?.close(); devServer?.close(); });

// Opens the app in a fresh iPhone-like context and records console errors, CSP violations and foreign requests.
async function open(url, extra = {}) {
  const context = await browser.newContext({ ...IPHONE, ...extra });
  const page = await context.newPage();
  const problems = [];
  const origin = new URL(url).origin;
  page.on('console', m => { if (m.type() === 'error') problems.push('console: ' + m.text()); });
  page.on('pageerror', e => problems.push('pageerror: ' + e.message));
  page.on('request', r => { if (!r.url().startsWith(origin) && !r.url().startsWith('data:')) problems.push('foreign request: ' + r.url()); });
  await page.goto(url);
  await page.locator('[data-test=balance]').waitFor({ timeout: 30000 });
  // First visit in iPhone Safari (not installed): the "add to Home Screen" instructions are shown once.
  const hint = page.locator('[data-test="install-hint"]');
  const hadHint = await hint.count() > 0;
  if (hadHint) await hint.locator('button').click();
  return { context, page, problems, hadHint };
}
const t = (name) => `[data-test="${name}"]`;
async function shot(page, name) { if (SHOTS) await page.screenshot({ path: path.join(SHOTS, name + '.png') }); }
async function balance(page) { return (await page.locator(t('balance')).textContent()).trim(); }
async function tapN(page, n) { for (let i = 0; i < n; i++) await page.locator(t('tap')).click(); }
async function tab(page, name) { await closeCelebrations(page); await page.locator(t('nav-' + name)).click(); }
// Celebration popups (new phase, crate reveal, daily gift) are part of the design: tests acknowledge them like a player.
async function closeCelebrations(page) {
  for (const m of ['phase-modal', 'crate-modal', 'daily-modal']) {
    const loc = page.locator(t(m));
    if (await loc.count()) await loc.locator('button').last().click();
  }
}
async function dev(page, name) { await closeCelebrations(page); await page.locator(t(name)).click(); await page.waitForTimeout(150); await closeCelebrations(page); }

// ---------------------------------------------------------------- release build
test('release: boots on an iPhone viewport with no errors, CSP violations or third-party requests', async () => {
  const { context, page, problems, hadHint } = await open(releaseUrl);
  assert.ok(hadHint, 'iPhone Safari users must see how to add the app to the Home Screen');
  assert.equal(await page.title(), 'Officina Tycoon');
  assert.equal(await balance(page), '0 Cr');
  const csp = await page.locator('meta[http-equiv="Content-Security-Policy"]').getAttribute('content');
  assert.match(csp, /script-src 'self' 'wasm-unsafe-eval'/);
  assert.doesNotMatch(csp, /(^|\s)'unsafe-eval'/);   // only the narrower 'wasm-unsafe-eval' is allowed
  await shot(page, '01-avvio');
  assert.deepEqual(problems, []);
  await context.close();
});

test('release: first minute — tap, claim tutorial, buy the first machine, production starts', async () => {
  const { context, page, problems } = await open(releaseUrl);
  await tapN(page, 10);
  await tab(page, 'orders');
  await page.locator(t('claim-t1')).click();
  await tab(page, 'factory');
  await page.locator(t('buy-drill')).click();
  await page.waitForFunction(() => /Liv\. 1/.test(document.querySelector('[data-test="machine-drill"]').textContent));
  const ops = await page.locator(t('ops')).textContent();
  assert.match(ops, /\+0\.5\/s/);
  await shot(page, '02-primo-macchinario');
  assert.deepEqual(problems, []);
  await context.close();
});

test('release: progress survives a reload (saved in the browser)', async () => {
  const { context, page } = await open(releaseUrl);
  await tapN(page, 20);
  await tab(page, 'factory');
  await page.locator(t('buy-drill')).click();
  await page.waitForFunction(() => /Liv\. 1/.test(document.querySelector('[data-test="machine-drill"]').textContent));
  await page.reload();
  await page.locator(t('balance')).waitFor();
  await page.waitForFunction(() => /Liv\. 1/.test(document.querySelector('[data-test="machine-drill"]').textContent), null, { timeout: 15000 });
  await context.close();
});

test('release: every tab renders, nothing overflows horizontally, no button text is cut', async () => {
  const { context, page, problems } = await open(releaseUrl);
  for (const name of ['factory', 'upgrades', 'orders', 'events', 'settings']) {
    await tab(page, name);
    await page.waitForTimeout(250);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    assert.ok(overflow <= 0, `${name}: page scrolls horizontally by ${overflow}px`);
    const cut = await page.evaluate(() => [...document.querySelectorAll('button')]
      .filter(b => b.offsetParent !== null && b.scrollWidth > b.clientWidth + 1).map(b => b.textContent.trim()));
    assert.deepEqual(cut, [], `${name}: buttons with clipped text`);
    await shot(page, 'tab-' + name);
  }
  assert.deepEqual(problems, []);
  await context.close();
});

test('release: no simulated ads and no dev tools in the public build, even with ?dev=1', async () => {
  const { context, page } = await open(releaseUrl + '?dev=1');
  await tab(page, 'settings');
  assert.equal(await page.locator(t('dev-tools')).count(), 0);
  assert.match(await page.locator('.panel').textContent(), /senza pubblicit/);
  await tab(page, 'factory');
  assert.equal(await page.getByText('Acceleratore').count(), 0);
  await context.close();
});

test('release: backup export -> reset -> import restores progress; invalid code is rejected', async () => {
  const { context, page } = await open(releaseUrl, { permissions: ['clipboard-read', 'clipboard-write'] });
  await tapN(page, 30);
  await tab(page, 'factory');
  await page.locator(t('buy-drill')).click();
  await tab(page, 'settings');
  await page.locator(t('export')).click();
  await page.waitForTimeout(300);
  const code = await page.evaluate(() => navigator.clipboard.readText());
  assert.match(code, /"payload"/);
  // invalid import: no change
  await page.locator(t('import-text')).fill('{"v":1,"sum":"nope","payload":"{}"}');
  await page.locator(t('import')).click();
  await page.locator(t('import')).click();
  await page.waitForFunction(() => /non valido/.test(document.querySelector('[data-test="import-message"]')?.textContent || ''));
  // reset (two taps) then import the real code (two taps)
  await page.locator(t('reset')).click();
  await page.locator(t('reset')).click();
  await page.waitForFunction(() => document.querySelector('[data-test="balance"]').textContent.trim() === '0 Cr');
  await page.locator(t('import-text')).fill(code);
  await page.locator(t('import')).click();
  await page.locator(t('import')).click();
  await page.waitForFunction(() => /ripristinati/.test(document.querySelector('[data-test="import-message"]')?.textContent || ''));
  await tab(page, 'factory');
  await page.waitForFunction(() => /Liv\. 1/.test(document.querySelector('[data-test="machine-drill"]').textContent));
  await context.close();
});

test('release: installable PWA — manifest, icons and offline start via service worker', async () => {
  const { context, page } = await open(releaseUrl);
  const manifest = await page.evaluate(async () => (await fetch('manifest.webmanifest')).json());
  assert.equal(manifest.display, 'standalone');
  for (const icon of manifest.icons) {
    const ok = await page.evaluate(async src => (await fetch(src)).ok, icon.src);
    assert.ok(ok, 'missing icon ' + icon.src);
  }
  assert.ok(await page.evaluate(async () => (await fetch('icons/apple-touch-icon.png')).ok));
  await page.waitForFunction(async () => !!(await navigator.serviceWorker.ready), null, { timeout: 30000 });
  await page.waitForFunction(async () => (await caches.keys()).some(k => k.startsWith('officina-cache-')), null, { timeout: 30000 });
  await page.waitForTimeout(2000);
  await context.setOffline(true);
  await page.reload();
  await page.locator(t('balance')).waitFor({ timeout: 30000 });
  await context.close();
});

// ---------------------------------------------------------------- dev build (simulations)
test('dev: offline earnings popup, simulated rewarded ad grants the declared bonus once', { skip: !DEV_DIR }, async () => {
  const { context, page, problems } = await open(devUrl + '?dev=1');
  await tab(page, 'settings');
  await dev(page, 'dev-1b');
  await tab(page, 'factory');
  await page.locator(t('buy-drill')).click();
  await tab(page, 'settings');
  await page.locator(t('dev-away')).click();
  await page.locator(t('offline-modal')).waitFor();
  await shot(page, '03-offline');
  const before = await page.locator(t('offline-claim')).textContent();
  await page.locator(t('offline-ad')).click();
  await page.waitForFunction(() => /con bonus/.test(document.querySelector('[data-test="offline-claim"]')?.textContent || ''), null, { timeout: 15000 });
  const after = await page.locator(t('offline-claim')).textContent();
  assert.notEqual(before, after);
  await page.locator(t('offline-claim')).click();
  assert.equal(await page.locator(t('offline-modal')).count(), 0);
  assert.deepEqual(problems, []);
  await context.close();
});

test('dev: experimental machine shows odds before paying, needs confirmation, pays once', { skip: !DEV_DIR }, async () => {
  const { context, page, problems } = await open(devUrl + '?dev=1');
  await tab(page, 'settings');
  await dev(page, 'dev-1b');
  await tab(page, 'factory');
  await page.locator(t('buy-drill')).click();
  await tab(page, 'settings');
  await dev(page, 'dev-phase');
  await dev(page, 'dev-events');
  await tab(page, 'events');
  const rows = page.locator(`${t('exp-odds')} tr`);
  assert.equal(await rows.count(), 4);
  const percents = await rows.locator('td:first-child').allTextContents();
  assert.equal(percents.map(p => parseFloat(p)).reduce((a, b) => a + b, 0), 100);
  await shot(page, '04-evento-sperimentale');
  await page.locator(t('exp-test')).click();                // first tap only arms the confirmation: nothing is paid yet
  assert.equal(await page.locator(t('exp-odds')).count(), 1, 'offer still open after the first tap');
  assert.doesNotMatch(await page.locator(t('experimental')).textContent(), /Collaudo in corso/);
  assert.match(await page.locator(t('exp-test')).textContent(), /Conferma/);
  await page.locator(t('exp-test')).click();                // second tap pays
  await page.waitForFunction(() => /Collaudo in corso/.test(document.querySelector('[data-test="experimental"]').textContent));
  await tab(page, 'settings');
  await page.locator(t('dev-away')).click();                // let the test finish while "away"
  await page.locator(t('offline-claim')).click();
  await tab(page, 'events');
  await page.locator(t('exp-collect')).click();
  await page.waitForFunction(() => /Prossimo prototipo/.test(document.querySelector('[data-test="experimental"]').textContent));
  assert.deepEqual(problems, []);
  await context.close();
});

test('dev: surge and free crate work and show their odds', { skip: !DEV_DIR }, async () => {
  const { context, page, problems } = await open(devUrl + '?dev=1');
  await tab(page, 'settings');
  await dev(page, 'dev-1b');
  await dev(page, 'dev-phase');
  await dev(page, 'dev-events');
  await dev(page, 'dev-crate');
  await tab(page, 'events');
  await page.locator(t('surge-guaranteed')).click();
  await page.waitForFunction(() => /Bonus attivo/.test(document.querySelector('[data-test="surge"]').textContent));
  assert.equal(await page.locator(`${t('crate-odds')} tr`).count(), 5);
  await page.locator(t('crate-open')).click();
  await page.locator(t('crate-modal')).waitFor();
  await page.waitForTimeout(1200);                          // short, fixed opening animation
  assert.ok((await page.locator(`${t('crate-modal')} ${t('crate-result')}`).textContent()).length > 3);
  await shot(page, '05-cassa');
  await closeCelebrations(page);
  await shot(page, '06-eventi');
  assert.deepEqual(problems, []);
  await context.close();
});

// ---------------------------------------------------------------- engagement & visual effects
test('release: animated factory scene shows a drawing for every machine and reacts to purchases', async () => {
  const { context, page, problems } = await open(releaseUrl);
  assert.equal(await page.locator('.scene .bay').count(), 8);
  assert.equal(await page.locator('.scene .bay svg.art').count(), 8);
  assert.equal(await page.locator('.scene .bay.locked').count(), 7);
  await tapN(page, 20);
  await tab(page, 'factory');
  await page.locator(t('buy-drill')).click();
  await page.waitForSelector('.bay[data-bay="drill"].owned');
  const anim = await page.evaluate(() => getComputedStyle(document.querySelector('.bay[data-bay="drill"] .a-drill')).animationName);
  assert.equal(anim, 'drill', 'owned machines are animated');
  assert.ok(await page.locator('.belt .item').count() >= 1, 'goods ride the conveyor');
  await shot(page, '07-fabbrica');
  assert.deepEqual(problems, []);
  await context.close();
});

test('release: fast tapping fills the frenzy meter and triggers the boost', async () => {
  const { context, page, problems } = await open(releaseUrl);
  const tap = page.locator(t('tap'));
  for (let i = 0; i < 60 && !(await tap.getAttribute('class')).includes('frenzy'); i++) await tap.click({ delay: 0 });
  await page.waitForFunction(() => document.querySelector('[data-test="tap"]').classList.contains('frenzy'));
  assert.match(await tap.textContent(), /FRENESIA/);
  await shot(page, '08-frenesia');
  assert.deepEqual(problems, []);
  await context.close();
});

test('release: daily gift opens from the top bar, pays once and disappears', async () => {
  const { context, page } = await open(releaseUrl);
  await page.locator(t('daily-open')).click();
  await page.locator(t('daily-modal')).waitFor();
  assert.equal(await page.locator('.day').count(), 7);
  await shot(page, '09-regalo');
  await page.locator(t('daily-claim')).click();
  await page.waitForFunction(() => !document.querySelector('[data-test="daily-modal"]'));
  await page.waitForFunction(() => document.querySelector('[data-test="balance"]').textContent.trim() !== '0 Cr');
  assert.equal(await page.locator(t('daily-open')).count(), 0, 'one gift per day');
  await context.close();
});

test('dev: milestone at level 25 and new-phase celebration are shown', { skip: !DEV_DIR }, async () => {
  const { context, page, problems } = await open(devUrl + '?dev=1');
  await tab(page, 'settings');
  await page.locator(t('dev-1b')).click();
  await page.locator(t('phase-modal')).waitFor();             // 1B credits => new phase
  assert.ok(await page.locator(`${t('phase-modal')} svg.art`).count() >= 1, 'unlocked machines are pictured');
  await shot(page, '10-nuova-fase');
  await closeCelebrations(page);
  await tab(page, 'factory');
  await page.locator('.mode', { hasText: 'Max' }).click();
  await page.locator(t('buy-drill')).click();
  await page.locator(t('banner')).waitFor();
  assert.match(await page.locator(t('banner')).textContent(), /livello \d+: produzione x2/);
  assert.deepEqual(problems, []);
  await context.close();
});

test('release: reduced effects setting is honoured and remembered', async () => {
  const { context, page } = await open(releaseUrl);
  await tab(page, 'settings');
  await page.locator(t('toggle-effects')).click();
  assert.match(await page.locator(t('toggle-effects')).textContent(), /Ridotti/);
  await page.reload();
  await page.locator(t('balance')).waitFor();
  await tab(page, 'settings');
  assert.match(await page.locator(t('toggle-effects')).textContent(), /Ridotti/);
  await context.close();
});

test('release: animations stay smooth (no long main-thread stalls while tapping and producing)', async () => {
  const { context, page } = await open(releaseUrl);
  await page.evaluate(() => { window.__long = []; new PerformanceObserver(l => window.__long.push(...l.getEntries().map(e => e.duration))).observe({ type: 'longtask', buffered: false }); });
  await tapN(page, 25);
  await page.waitForTimeout(2000);
  const long = await page.evaluate(() => window.__long);
  const worst = long.length ? Math.max(...long) : 0;
  assert.ok(worst < 250, 'longest main-thread task ' + worst + 'ms');
  await context.close();
});
