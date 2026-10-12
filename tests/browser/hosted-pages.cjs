// Browser checks use only the synthetic selected-shell fixture, never a consumer journey.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawn } = require('node:child_process');
const playwright = require('playwright');
const root = path.resolve(__dirname, '../..');
const evidence = path.join(root, 'artifacts/authenticated-hosted-pages/browser');
fs.mkdirSync(evidence, { recursive: true });
const engines = process.argv.find(arg => arg.startsWith('--engines='))?.split('=')[1].split(',') || ['chromium', 'firefox', 'webkit'];
assert(engines.length && engines.every(name => ['chromium', 'firefox', 'webkit'].includes(name)), 'Explicit supported browser engines required');
const server = spawn('dotnet', ['tests/dotnet/Orbyss.Foundation.Web.HostedPages.Probe/bin/Release/net10.0/Orbyss.Foundation.Web.HostedPages.Probe.dll', '--serve-private'], { cwd: root, windowsHide: true });
let output = '';
server.stdout.on('data', data => { output += data; });
server.stderr.on('data', data => { output += data; });
const results = [];

async function run() {
  const base = await new Promise((resolve, reject) => {
    const deadline = setTimeout(() => { clearInterval(poll); reject(new Error('Fixture startup timeout: ' + output)); }, 90000);
    const poll = setInterval(() => {
      const match = output.match(/PRIVATE_BROWSER_PROBE_URL=(http:\/\/127\.0\.0\.1:\d+)/);
      if (match) { clearTimeout(deadline); clearInterval(poll); resolve(match[1]); }
      else if (server.exitCode !== null) { clearTimeout(deadline); clearInterval(poll); reject(new Error('Fixture exited: ' + output)); }
    }, 100);
  });
  for (const name of engines) {
    const browser = await playwright[name].launch({ headless: true });
    try {
      const alpha = await browser.newContext();
      const beta = await browser.newContext();
      const full = await browser.newContext();
      const anonymous = await browser.newContext();
      for (const [context, identity] of [[alpha, 'alpha'], [beta, 'beta'], [full, 'full']])
        assert.equal((await context.request.get(base + '/p/fixture/session/' + identity)).status(), 204);
      const page = await alpha.newPage();
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
      const bootstrapResponses = [];
      page.on('response', response => { if (response.url().includes('/_foundation/bootstrap/')) bootstrapResponses.push(response.status()); });
      const response = await page.goto(base + '/p/alpha');
      assert.equal(response.status(), 200);
      assert.equal(response.headers()['cache-control'], 'no-store');
      assert.equal(response.headers()['x-robots-tag'], 'noindex, nofollow');
      await page.locator('#foundation-status').waitFor({ state: 'hidden' });
      assert.match(await page.locator('#foundation-form').textContent(), /FIXTURE_PRIVATE_PAGE_p loaded dynamically/);
      const alphaBootstrap = await page.locator('#foundation-form').getAttribute('data-bootstrap');
      assert.equal(await page.locator('#foundation-form').getAttribute('data-credentials'), 'same-origin');
      assert.deepEqual(bootstrapResponses, [200], 'Browser cookie did not authenticate bootstrap');
      assert.deepEqual(errors, [], 'Runtime import or CSP browser errors');
      const betaPage = await beta.newPage();
      await betaPage.goto(base + '/p/beta');
      await betaPage.locator('#foundation-status').waitFor({ state: 'hidden' });
      const betaBootstrap = await betaPage.locator('#foundation-form').getAttribute('data-bootstrap');
      assert.notEqual(alphaBootstrap, betaBootstrap);
      for (const [context, bootstrap] of [[alpha, betaBootstrap], [beta, alphaBootstrap], [anonymous, alphaBootstrap]]) {
        const denied = await context.request.get(base + bootstrap, { maxRedirects: 0 });
        assert.equal(denied.status(), 302);
        assert.equal(denied.headers()['cache-control'], 'no-store');
        assert.equal(denied.headers()['x-robots-tag'], 'noindex, nofollow');
        assert(!((await denied.text()).includes('FIXTURE_PRIVATE_PAGE')));
      }
      await page.goto(base + '/p/staged');
      await page.locator('#foundation-status[role=alert]').waitFor();
      const admittedStaged = await full.newPage();
      await admittedStaged.goto(base + '/p/staged');
      await admittedStaged.locator('#foundation-status').waitFor({ state: 'hidden' });
      const publicPage = await anonymous.newPage();
      await publicPage.goto(base + '/p/public');
      await publicPage.locator('#foundation-status').waitFor({ state: 'hidden' });
      assert.match(await publicPage.locator('#foundation-form').textContent(), /Explicit public entry/);
      const deniedEntry = await anonymous.request.get(base + '/p/alpha', { maxRedirects: 0 });
      assert.equal(deniedEntry.status(), 302);
      const token = (await (await alpha.request.get(base + '/p/bff/antiforgery')).json()).requestToken;
      const logout = await alpha.request.post(base + '/p/bff/logout', { headers: { 'X-CSRF-TOKEN': token }, maxRedirects: 0 });
      assert.equal(logout.status(), 302);
      for (const url of ['/p/alpha', alphaBootstrap])
        assert.equal((await alpha.request.get(base + url, { maxRedirects: 0 })).status(), 302, 'Logout retained private admission');
      results.push({ engine: name, version: browser.version(), status: 'passed', authenticatedBootstrap: true, crossRouteDenied: true, publicRuntimeMounted: true, logoutReentryDenied: true });
      console.log(name + ' ' + browser.version() + ': authenticated loader, route permissions, public mount and logout/re-entry passed');
    } finally { await browser.close(); }
  }
}
run().catch(error => { console.error(error); process.exitCode = 1; }).finally(async () => {
  server.kill();
  await new Promise(resolve => { if (server.exitCode !== null) resolve(); else server.once('exit', resolve); });
  fs.writeFileSync(path.join(evidence, 'browser-host.log'), output);
  fs.writeFileSync(path.join(evidence, 'results.json'), JSON.stringify({ engines, results, status: process.exitCode ? 'failed' : 'passed' }, null, 2) + '\n');
});
