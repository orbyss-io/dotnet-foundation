const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawn } = require('node:child_process');
const playwright = require('playwright');
const root = path.resolve(__dirname, '../..');
const engines = process.argv.find(value => value.startsWith('--engines='))?.slice(10).split(',') || ['chromium', 'firefox', 'webkit'];
assert(engines.length && engines.every(name => ['chromium', 'firefox', 'webkit'].includes(name)));
const evidence = path.join(root, 'artifacts/browser-presentation/localization', Date.now().toString());
fs.mkdirSync(evidence, { recursive: true });
const server = spawn('dotnet', ['tests/dotnet/Orbyss.Foundation.Authentication.BffCookie.Probe/bin/Release/net10.0/Orbyss.Foundation.Authentication.BffCookie.Probe.dll', '--serve-enhanced-recovery'], { cwd: root, windowsHide: true });
let output = '';
server.stdout.on('data', data => { output += data; });
server.stderr.on('data', data => { output += data; });
const observations = [];
async function run() {
  const base = await new Promise((resolve, reject) => {
    const timeout = setTimeout(() => { clearInterval(poll); reject(new Error('Recovery startup timed out: ' + output)); }, 30000);
    const poll = setInterval(() => {
      const match = output.match(/RECOVERY_ENHANCEMENT_PROBE_URL=(http:\/\/127\.0\.0\.1:\d+)/);
      if (match) { clearTimeout(timeout); clearInterval(poll); resolve(match[1]); }
      else if (server.exitCode !== null) { clearTimeout(timeout); clearInterval(poll); reject(new Error('Recovery fixture exited: ' + output)); }
    }, 50);
  });
  for (const name of engines) {
    const browser = await playwright[name].launch({ headless: true });
    try {
      const context = await browser.newContext({ locale: 'nl-BE' });
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      const response = await page.goto(base + '/pages-localized/app/bff/access-denied');
      assert.equal(response.status(), 403);
      assert.equal(response.headers()['content-language'], 'nl');
      assert.equal(await page.locator('h1').innerText(), 'Geen toegang');
      await page.bringToFront();
      await page.waitForFunction(() => document.activeElement === document.querySelector('main h1'));
      assert((await page.locator('main').innerText()).includes('Je hebt geen toegang'));
      assert(!((await page.content()).includes('Forbidden')));
      assert.deepEqual(errors, []);
      await page.goto(base + '/pages-custom/app/auth/sign-in');
      assert.equal(await page.locator('[data-recovery-layout="custom"]').count(), 1);
      await page.getByRole('button', { name: 'Aanmelden' }).click();
      await page.waitForURL(base + '/pages-custom/app/fixture/provider**');
      assert.equal(await page.getByRole('heading').innerText(), 'Synthetic identity provider');
      await context.close();
      const noScript = await browser.newContext({ locale: 'nl-BE', javaScriptEnabled: false });
      const plain = await noScript.newPage();
      await plain.goto(base + '/pages-localized/app/auth/sign-in');
      await plain.getByRole('button', { name: 'Aanmelden' }).click();
      await plain.waitForURL(base + '/pages-localized/app/fixture/provider**');
      await noScript.close();
      observations.push({ engine: name, version: browser.version(), status: 'passed', localizedFailure: true, recoveryFocus: true, nativeChallengeThroughCustomLayout: true, noScriptNativeForm: true });
    } finally { await browser.close(); }
  }
}
run().then(() => console.log(JSON.stringify(observations)), error => { console.error(error); process.exitCode = 1; })
  .finally(() => { fs.writeFileSync(path.join(evidence, 'observations.json'), JSON.stringify(observations, null, 2)); fs.writeFileSync(path.join(evidence, 'server.log'), output); server.kill(); });
