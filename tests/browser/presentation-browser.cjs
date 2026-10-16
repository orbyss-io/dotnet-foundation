// Real shared-module dialog/gesture checks. This is not native Host, auth
// protocol, configured-composition or consumer-journey acceptance.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const playwright = require('playwright');
const root = path.resolve(__dirname, '../..');
const moduleBytes = fs.readFileSync(path.join(root, 'src/Orbyss.Foundation.Web.Presentation/browser/presentation.mjs'));
const engines = process.argv.find(value => value.startsWith('--engines='))?.slice(10).split(',') || ['chromium', 'firefox', 'webkit'];
assert(engines.length && engines.every(name => ['chromium', 'firefox', 'webkit'].includes(name)));
const evidence = path.join(root, 'artifacts/browser-presentation/module');
fs.mkdirSync(evidence, { recursive: true });
const html = `<!doctype html><html lang="en"><meta charset="utf-8"><title>Module-only probe</title>
<main><label>Draft <input id="draft" value="retained local draft"></label>
<button id="first">Sign out</button><button id="second">Other control</button></main>
<script type="module">
import { createSignOutControl, unsavedWork } from '/presentation.mjs';
import { createSignOutControl as independentlyBundled } from '/presentation.mjs?bundle=second';
let controls = [];
let disposeSource;
window.configure = (mode, state) => {
  controls.forEach(control => control.dispose()); disposeSource?.();
  window.effects = []; window.reads = 0; window.localState = state;
  const surface = 'synthetic-module-probe';
  disposeSource = unsavedWork(surface).register({ getState() {
    window.reads++; if (window.localState === 'throw') throw new Error('synthetic uncertain save');
    return window.localState;
  } });
  const signOut = () => {
    window.effects.push({ action: 'dispatch', activation: navigator.userActivation?.isActive ?? null });
    window.open('/popup', '_blank');
    return Promise.resolve({ protocol: 'fixture-only' });
  };
  controls = [createSignOutControl({ surface, mode, signOut }), independentlyBundled({ surface, mode, signOut })];
  document.querySelector('#first').onclick = event => controls[0].request({ returnFocus: event.currentTarget }).then(result => window.result = result);
  document.querySelector('#second').onclick = event => controls[1].request({ returnFocus: event.currentTarget }).then(result => window.result = result);
  window.otherRequest = () => controls[1].request();
  window.result = null;
};
window.configure('when-unsaved', 'dirty'); window.ready = true;
</script></html>`;
const server = http.createServer((request, response) => {
  const pathname = new URL(request.url, 'http://localhost').pathname;
  response.setHeader('Cache-Control', 'no-store');
  if (pathname === '/presentation.mjs') {
    response.setHeader('Content-Type', 'text/javascript; charset=utf-8'); response.end(moduleBytes);
  } else if (pathname === '/popup') response.end('Synthetic gesture observation; no provider call.');
  else if (pathname === '/') { response.setHeader('Content-Type', 'text/html; charset=utf-8'); response.end(html); }
  else { response.statusCode = 404; response.end(); }
});

async function run() {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const origin = 'http://127.0.0.1:' + server.address().port;
  const observations = [];
  try {
    for (const name of engines) {
      const browser = await playwright[name].launch({ headless: true });
      try {
        const context = await browser.newContext({ viewport: { width: 1024, height: 768 } });
        const page = await context.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.goto(origin);
        await page.waitForFunction(() => window.ready === true);
        await page.locator('#first').click();
        const dialog = page.getByRole('dialog', { name: 'Sign out?', exact: true });
        await dialog.waitFor();
        assert.equal(await page.evaluate(() => document.activeElement.textContent), 'Keep working');
        assert.deepEqual(await page.evaluate(() => window.effects), []);
        await page.keyboard.press('Escape');
        await dialog.waitFor({ state: 'detached' });
        assert.equal(await page.locator('#draft').inputValue(), 'retained local draft');
        assert.equal(await page.evaluate(() => document.activeElement.id), 'first');
        assert.deepEqual(await page.evaluate(() => window.effects), []);
        assert.deepEqual(await page.evaluate(() => window.result), { status: 'cancelled', dispatched: false });

        await page.locator('#first').click();
        await dialog.waitFor();
        // The second independently loaded control reuses the actual open view.
        await page.evaluate(() => { window.secondPending = window.otherRequest(); window.localState = 'unknown'; });
        assert.equal(await page.getByRole('dialog').count(), 1);
        const popupEvent = context.waitForEvent('page');
        await dialog.getByRole('button', { name: 'Sign out', exact: true }).click();
        const popup = await popupEvent;
        await popup.waitForLoadState();
        assert(popup.url().startsWith(origin + '/popup'));
        await popup.close();
        await page.waitForFunction(() => window.result?.status === 'dispatched');
        const confirmed = await page.evaluate(() => window.effects);
        assert.equal(confirmed.length, 1);
        if (confirmed[0].activation !== null) assert.equal(confirmed[0].activation, true);

        for (const [mode, state, expectedReads] of [['when-unsaved', 'clean', 1], ['never', 'throw', 0]]) {
          await page.evaluate(([mode, state]) => window.configure(mode, state), [mode, state]);
          const popupEvent = context.waitForEvent('page');
          await page.locator('#first').click();
          const direct = await popupEvent;
          await direct.close();
          await page.waitForFunction(() => window.result?.status === 'dispatched');
          assert.equal(await page.getByRole('dialog').count(), 0);
          assert.equal(await page.evaluate(() => window.reads), expectedReads);
        }
        await page.evaluate(() => window.configure('always', 'clean'));
        await page.locator('#first').click();
        await dialog.waitFor();
        assert.deepEqual(await page.evaluate(() => window.effects), []);
        await dialog.getByRole('button', { name: 'Keep working' }).click();
        await dialog.waitFor({ state: 'detached' });
        assert.equal(await page.locator('#draft').inputValue(), 'retained local draft');
        assert.equal(await page.evaluate(() => localStorage.length + sessionStorage.length), 0);
        assert.deepEqual(errors, []);
        const viewport = await page.evaluate(() => ({ width: innerWidth, height: innerHeight }));
        assert.deepEqual(viewport, { width: 1024, height: 768 });
        observations.push({ engine: name, browserVersion: browser.version(), passed: true, viewport,
          scope: 'Actual shared module dialog/keyboard/focus/gesture behavior; fixture-only protocol action.' });
      } finally { await browser.close(); }
    }
    fs.writeFileSync(path.join(evidence, 'result.json'), JSON.stringify({ status: 'passed',
      moduleSha256: crypto.createHash('sha256').update(moduleBytes).digest('hex'), observations,
      nativeHostAcceptance: false, consumerAcceptance: false }, null, 2));
    console.log('Shared presentation module browser checks passed: ' + engines.join(','));
  } finally { await new Promise(resolve => server.close(resolve)); }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
