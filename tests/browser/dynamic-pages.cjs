// Native request-time pages with fictional resources. No production/provider acceptance claim.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawn } = require('node:child_process');
const playwright = require('playwright');
const root = path.resolve(__dirname, '../..');
const engines = process.argv.find(value => value.startsWith('--engines='))?.slice(10).split(',') || ['chromium', 'firefox', 'webkit'];
assert(engines.length && engines.every(name => ['chromium', 'firefox', 'webkit'].includes(name)));
const evidence = path.join(root, 'artifacts/dynamic-pages/browser', Date.now().toString());
fs.mkdirSync(evidence, { recursive: true });
const server = spawn('dotnet', ['tests/dotnet/Orbyss.Foundation.Web.DynamicPages.Probe/bin/Release/net10.0/Orbyss.Foundation.Web.DynamicPages.Probe.dll', '--serve'], { cwd: root, windowsHide: true });
let output = '';
server.stdout.on('data', data => { output += data; });
server.stderr.on('data', data => { output += data; });
const observations = [];
async function run() {
  const base = await new Promise((resolve, reject) => {
    const timeout = setTimeout(() => { clearInterval(poll); reject(new Error('Dynamic fixture startup timed out: ' + output)); }, 30000);
    const poll = setInterval(() => {
      const match = output.match(/DYNAMIC_PAGES_PROBE_URL=(http:\/\/127\.0\.0\.1:\d+)/);
      if (match) { clearTimeout(timeout); clearInterval(poll); resolve(match[1]); }
      else if (server.exitCode !== null) { clearTimeout(timeout); clearInterval(poll); reject(new Error('Dynamic fixture exited: ' + output)); }
    }, 50);
  });
  for (const name of engines) {
    const browser = await playwright[name].launch({ headless: true });
    try {
      const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
      const page = await context.newPage();
      const failures = [];
      page.on('pageerror', error => failures.push(error.message));
      const response = await page.goto(base + '/a/app/stalls/market');
      assert.equal(response.status(), 200);
      assert.equal(response.headers()['cache-control'], 'no-store');
      await page.waitForFunction(() => window.dynamicApplicationLoaded === true);
      assert.equal(await page.evaluate(() => window.injected), undefined);
      assert.match(await page.title(), /Stall <script>/);
      assert.equal(await page.locator('meta[name="description"]').getAttribute('content'), 'Fresh "metadata" & publication');
      const bootstrap = await page.evaluate(() => JSON.parse(document.querySelector('#foundation-page-data').textContent));
      assert.equal(bootstrap.slug, 'market');
      assert.equal(bootstrap.scope, '/a/app');
      assert.match(bootstrap.text, /<\/script>/);
      const dimensions = await page.evaluate(() => ({ width: document.documentElement.clientWidth, scroll: document.documentElement.scrollWidth, mount: document.querySelector('#foundation-application').getBoundingClientRect().height }));
      assert(dimensions.scroll <= dimensions.width && dimensions.mount > 400);
      assert.deepEqual(failures, []);
      const hidden = await page.goto(base + '/a/app/stalls/hidden');
      assert.equal(hidden.status(), 404);
      assert(!(await page.content()).includes('Fresh stall'));
      await page.goto(base + '/custom/app/stalls/market');
      assert.equal(await page.locator('[data-custom-layout]').count(), 1);
      await page.screenshot({ path: path.join(evidence, name + '-custom.png') });
      observations.push({ engine: name, version: browser.version(), status: 'passed', initialMetadata: true, inertBootstrap: true, nativePrefixes: true, mobileLayout: true, hiddenResource: true, customLayout: true });
      await context.close();
    } finally { await browser.close(); }
  }
}
run().then(() => console.log(JSON.stringify(observations)), error => { console.error(error); process.exitCode = 1; })
  .finally(() => { fs.writeFileSync(path.join(evidence, 'observations.json'), JSON.stringify(observations, null, 2)); fs.writeFileSync(path.join(evidence, 'server.log'), output); server.kill(); });
