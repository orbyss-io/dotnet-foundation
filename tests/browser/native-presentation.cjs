// Shipped native feature and BFF transport; synthetic provider, no consumer acceptance claim.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawn } = require('node:child_process');
const playwright = require('playwright');
const root = path.resolve(__dirname, '../..');
const engines = process.argv.find(value => value.startsWith('--engines='))?.slice(10).split(',') || ['chromium', 'firefox', 'webkit'];
assert(engines.length && engines.every(name => ['chromium', 'firefox', 'webkit'].includes(name)));
const evidence = path.join(root, 'artifacts/browser-presentation/native', Date.now() + '-' + crypto.randomUUID());
fs.mkdirSync(evidence, { recursive: true });
const server = spawn('dotnet', ['tests/dotnet/Orbyss.Foundation.Authentication.BffCookie.Probe/bin/Release/net10.0/Orbyss.Foundation.Authentication.BffCookie.Probe.dll', '--serve-presentation'], { cwd: root, windowsHide: true });
let output = '';
server.stdout.on('data', data => { output += data; });
server.stderr.on('data', data => { output += data; });
const observations = [];
async function run() {
  const base = await new Promise((resolve, reject) => {
    const deadline = setTimeout(() => { clearInterval(poll); reject(new Error('Native presentation startup timed out: ' + output)); }, 60000);
    const poll = setInterval(() => {
      const match = output.match(/PRESENTATION_BROWSER_PROBE_URL=(http:\/\/127\.0\.0\.1:\d+)/);
      if (match) { clearTimeout(deadline); clearInterval(poll); resolve(match[1]); }
      else if (server.exitCode !== null) { clearTimeout(deadline); clearInterval(poll); reject(new Error('Native presentation exited: ' + output)); }
    }, 100);
  });
  for (const name of engines) {
    // The selected Playwright distribution disables BFCache by default. This
    // explicit Chromium fixture enables the real browser mechanism under test.
    const launchOptions = name === 'chromium' ? { headless: true, channel: 'chromium',
      ignoreDefaultArgs: ['--disable-back-forward-cache'] } : { headless: true };
    const browser = await playwright[name].launch(launchOptions);
    try {
      for (const viewport of [{ width: 1280, height: 720 }, { width: 390, height: 844 }]) {
        const context = await browser.newContext({ viewport, colorScheme: 'light' });
        try {
          const page = await context.newPage();
          await page.goto(base + '/pages-a/app/auth/sign-in');
          assert.deepEqual(await page.evaluate(() => ({ width: innerWidth, height: innerHeight })), viewport);
          assert.equal(await page.locator('body').getAttribute('data-theme'), 'green');
          assert.equal(await page.getByRole('heading', { name: 'Aanmelden', exact: true }).evaluate(node => getComputedStyle(node).color), 'rgb(25, 102, 67)');
          await page.getByRole('button', { name: 'Aanmelden', exact: true }).click();
          await page.getByRole('heading', { name: 'Synthetic identity provider' }).waitFor();
          await page.getByRole('button', { name: 'Sign in', exact: true }).click();
          await page.waitForURL(base + '/pages-a/app/');
          assert.equal((await (await context.request.get(base + '/pages-a/app/bff/user')).json()).authenticated, true);
          await page.goto(base + '/pages-a/app/auth/signed-out?remote=unavailable');
          assert((await page.locator('main').innerText()).includes('A local session is active.'));
          await page.goto(base + '/pages-failure/fixture/page-failure');
          await page.getByRole('heading', { name: 'The request could not be completed' }).waitFor();
          assert(!(await page.locator('body').innerText()).includes('private-exception'));
          await context.request.get(base + '/pages-failure/fixture/session');
          await page.goto(base + '/pages-failure/auth/signed-out');
          const transportRequests = [];
          page.on('request', request => {
            if (/\/bff\/(?:antiforgery|logout)$/u.test(new URL(request.url()).pathname)) transportRequests.push(request.method());
          });
          await page.evaluate(async () => {
            const selected = await (await fetch('/pages-failure/auth/configuration')).json();
            const { prepareBffSignOut } = await import(selected.bffModulePath);
            const { unsavedWork } = await import(selected.modulePath);
            window.disposeSource = unsavedWork(selected.surface).register({ getState: () => 'dirty' });
            window.control = await prepareBffSignOut({ surface: selected.surface, mode: selected.signOutConfirmation,
              antiforgeryPath: selected.antiforgeryPath, logoutPath: selected.logoutPath });
            const input = document.createElement('input'); input.setAttribute('aria-label', 'Private draft'); input.value = 'retained fixture draft';
            const button = document.createElement('button'); button.textContent = 'Native sign out';
            button.onclick = () => { window.requestResult = window.control.request({ returnFocus: button }); };
            document.querySelector('main').append(input, button);
          });
          await page.getByRole('button', { name: 'Native sign out' }).click();
          await page.getByRole('dialog').waitFor();
          assert.deepEqual(transportRequests, [], 'transport acquired or dispatched before confirmation');
          await page.keyboard.press('Escape');
          assert.equal(await page.getByRole('dialog').count(), 0);
          assert.equal(await page.getByRole('textbox', { name: 'Private draft' }).inputValue(), 'retained fixture draft');
          assert.equal((await (await context.request.get(base + '/pages-failure/bff/user')).json()).authenticated, true);
          assert.deepEqual(transportRequests, [], 'cancellation caused protocol or antiforgery acquisition');
          assert.equal(await page.evaluate(() => document.activeElement.textContent), 'Native sign out');
          await page.getByRole('button', { name: 'Native sign out' }).click();
          await page.getByRole('dialog').getByRole('button', { name: 'Sign out', exact: true }).click();
          await page.waitForURL(base + '/pages-failure/auth/signed-out?remote=unavailable');
          assert.deepEqual(transportRequests, ['GET', 'POST'], 'native transport duplicated or bypassed admitted acquisition');
          assert((await page.locator('main').innerText()).includes('No local session is active.'));
          assert((await page.locator('main').innerText()).includes('provider session may still be active'));
          assert.equal((await (await context.request.get(base + '/pages-failure/bff/user')).json()).authenticated, false);
          assert.deepEqual(await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length })), { local: 0, session: 0 });
          await page.screenshot({ path: path.join(evidence, name + '-' + viewport.width + '.png'), fullPage: true });
          observations.push({ engine: name, version: browser.version(), viewport, measured: true, nativeLogin: true,
            independentSession: true, encodedError: true, cancelPreservedDraftAndSession: true,
            nativeCsrfPost: true, zeroPreAdmissionAcquisition: true, transportRequests,
            observedLocalLogout: true, providerOutcome: 'unconfirmed', storageEmpty: true, status: 'passed' });
        } finally { await context.close(); }
      }
      const noScript = await browser.newContext({ javaScriptEnabled: false });
      try {
        await noScript.request.get(base + '/pages-failure/fixture/session');
        const page = await noScript.newPage();
        await page.goto(base + '/pages-failure/auth/sign-out');
        await page.getByRole('button', { name: 'Sign out', exact: true }).click();
        await page.waitForURL(base + '/pages-failure/auth/signed-out?remote=unavailable');
        assert((await page.locator('main').innerText()).includes('No local session is active.'));
        observations.push({ engine: name, javaScript: false, nativeAntiforgeryForm: true, observedLocalLogout: true, status: 'passed' });
      } finally { await noScript.close(); }
      const dark = await browser.newContext({ colorScheme: 'dark' });
      try {
        const page = await dark.newPage();
        await page.goto(base + '/pages-a/app/auth/sign-in');
        const colors = await page.evaluate(() => ({ theme: document.body.dataset.theme,
          heading: getComputedStyle(document.querySelector('h1')).color,
          background: getComputedStyle(document.documentElement).backgroundColor }));
        assert.deepEqual(colors, { theme: 'green', heading: 'rgb(134, 239, 172)', background: 'rgb(17, 24, 39)' });
        const luminance = color => color.match(/\d+/g).slice(0, 3).map(Number).map(channel => {
          const value = channel / 255; return value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4;
        }).reduce((sum, value, index) => sum + value * [.2126, .7152, .0722][index], 0);
        const contrast = (luminance(colors.heading) + .05) / (luminance(colors.background) + .05);
        assert(contrast >= 4.5);
        observations.push({ engine: name, colorScheme: 'dark', colors, headingContrast: contrast, status: 'passed' });
      } finally { await dark.close(); }
      const lifecycle = await browser.newContext();
      try {
        const page = await lifecycle.newPage();
        const cacheMisses = [];
        if (name === 'chromium') {
          const debug = await lifecycle.newCDPSession(page);
          await debug.send('Page.enable');
          debug.on('Page.backForwardCacheNotUsed', event => cacheMisses.push(event.notRestoredExplanations));
        }
        await page.addInitScript(() => {
          window.actualPageShows = [];
          window.addEventListener('pageshow', event => window.actualPageShows.push({ persisted: event.persisted }));
        });
        await lifecycle.request.get(base + '/pages-failure/fixture/session');
        await page.goto(base + '/pages-failure/fixture/private-page');
        await page.waitForFunction(() => document.querySelector('#session-status')?.textContent === 'authenticated');
        assert(await page.locator('#private').isVisible());
        await page.getByRole('link', { name: 'Public page' }).click();
        await page.getByRole('heading', { name: 'Public fixture' }).waitFor();
        const cookiesBefore = await lifecycle.cookies();
        assert.equal((await lifecycle.request.post(base + '/pages-failure/fixture/revoke')).status(), 204);
        assert.deepEqual(await lifecycle.cookies(), cookiesBefore, 'server revocation changed browser cookies and invalidated the cache test');
        await page.goBack({ waitUntil: 'commit' });
        await page.waitForFunction(() => document.querySelector('#session-status')?.textContent === 'anonymous'
          || document.querySelector('h1')?.textContent === 'The request could not be completed');
        assert(!(await page.locator('#private').isVisible()));
        const actualPageShows = await page.evaluate(() => window.actualPageShows);
        observations.push({ engine: name, navigation: 'real-history-back-after-server-revocation', actualPageShows,
          launchOptions, cacheMisses, persistedRestoreObserved: actualPageShows.some(event => event.persisted), privateBoundaryVisible: false, status: 'passed' });
        if (name === 'chromium') assert(actualPageShows.some(event => event.persisted), 'Chromium fixture did not exercise a real persisted page restore');
      } finally { await lifecycle.close(); }
      console.log(name + ': configured native presentation, measured viewports, actual login, secure shared signout and no-script recovery passed');
    } finally { await browser.close(); }
  }
}
run().catch(error => { console.error(error); process.exitCode = 1; }).finally(async () => {
  server.kill();
  const deadline = setTimeout(() => server.kill('SIGKILL'), 10000);
  await new Promise(resolve => { if (server.exitCode !== null || server.signalCode !== null) resolve(); else server.once('exit', resolve); });
  clearTimeout(deadline);
  fs.writeFileSync(path.join(evidence, 'host.log'), output);
  fs.writeFileSync(path.join(evidence, 'results.json'), JSON.stringify({ engines, observations, status: process.exitCode ? 'failed' : 'passed' }, null, 2) + '\n');
  console.log('Native presentation evidence: ' + evidence);
});

