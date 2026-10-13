// Synthetic native OIDC browser qualification; no consumer/provider-account journey or paid call.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawn } = require('node:child_process');
const playwright = require('playwright');
const root = path.resolve(__dirname, '../..');
const engines = process.argv.find(arg => arg.startsWith('--engines='))?.split('=')[1].split(',') || ['chromium', 'firefox', 'webkit'];
const configuration = process.argv.find(arg => arg.startsWith('--configuration='))?.split('=')[1] || 'Release';
assert(engines.length && engines.every(name => ['chromium', 'firefox', 'webkit'].includes(name)));
assert(['Debug', 'Release'].includes(configuration));
const evidence = path.join(root, 'artifacts/ds01-repairs/browser');
fs.mkdirSync(evidence, { recursive: true });
const server = spawn('dotnet', [`tests/dotnet/Orbyss.Foundation.Authentication.BffCookie.Probe/bin/${configuration}/net10.0/Orbyss.Foundation.Authentication.BffCookie.Probe.dll`, '--serve-recovery'], { cwd: root, windowsHide: true });
let output = '';
server.stdout.on('data', data => { output += data; });
server.stderr.on('data', data => { output += data; });
const observations = [];

async function run() {
  const base = await new Promise((resolve, reject) => {
    const deadline = setTimeout(() => { clearInterval(poll); reject(new Error('Recovery fixture startup timeout: ' + output)); }, 90000);
    const poll = setInterval(() => {
      const match = output.match(/RECOVERY_BROWSER_PROBE_URL=(http:\/\/127\.0\.0\.1:\d+)/);
      if (match) { clearTimeout(deadline); clearInterval(poll); resolve(match[1]); }
      else if (server.exitCode !== null || server.signalCode !== null) { clearTimeout(deadline); clearInterval(poll); reject(new Error('Fixture exited: ' + output)); }
    }, 100);
  });
  for (const name of engines) {
    const browser = await playwright[name].launch({ headless: true });
    try {
      const fresh = await browser.newContext();
      const page = await fresh.newPage();
      page.on('requestfailed', request => console.error('Failed fixture navigation: ' + new URL(request.url()).origin + new URL(request.url()).pathname + ' ' + request.failure()?.errorText));
      await page.goto(base + '/a/bff/login?returnUrl=/a/api/private');
      await page.getByRole('button', { name: 'Sign in', exact: true }).waitFor();
      // Real elapsed provider dwell verifies the formerly ten-second default cannot expire this new flow.
      await new Promise(resolve => setTimeout(resolve, 15000));
      await page.getByRole('button', { name: 'Sign in', exact: true }).click();
      await page.waitForURL(base + '/a/api/private');
      assert.equal((await (await fresh.request.get(base + '/a/bff/user')).json()).authenticated, true);

      const expired = await browser.newContext();
      const expiryPage = await expired.newPage();
      await expiryPage.goto(base + '/expired/bff/login');
      await expiryPage.getByRole('button', { name: 'Sign in', exact: true }).waitFor();
      await new Promise(resolve => setTimeout(resolve, 2500));
      await expiryPage.getByRole('button', { name: 'Sign in', exact: true }).click();
      await expiryPage.getByRole('link', { name: 'Try signing in again' }).waitFor();
      assert.equal((await (await expired.request.get(base + '/expired/bff/user')).json()).authenticated, false);
      await expiryPage.getByRole('link', { name: 'Try signing in again' }).click();
      await expiryPage.getByRole('button', { name: 'Sign in', exact: true }).click();
      await expiryPage.waitForURL(base + '/expired/api/private');
      assert.equal((await (await expired.request.get(base + '/expired/bff/user')).json()).authenticated, true);

      const existing = await browser.newContext();
      await existing.request.get(base + '/expired/fixture/session');
      const existingPage = await existing.newPage();
      await existingPage.goto(base + '/expired/bff/login');
      await existingPage.getByRole('button', { name: 'Sign in', exact: true }).waitFor();
      await new Promise(resolve => setTimeout(resolve, 2500));
      await existingPage.getByRole('button', { name: 'Sign in', exact: true }).click();
      await existingPage.getByRole('link', { name: 'Try signing in again' }).waitFor();
      assert((await existingPage.locator('main').textContent()).includes('Continue to the application'));
      assert.equal((await (await existing.request.get(base + '/expired/bff/user')).json()).authenticated, true);
      assert.equal((await existing.request.get(base + '/expired/api/private')).status(), 200);

      const failure = await browser.newContext();
      const recovery = await failure.newPage();
      const anonymousFailure = await recovery.goto(base + '/transport/bff/login');
      assert.equal(anonymousFailure.status(), 503);
      assert(!(await recovery.locator('main').textContent()).includes('Continue'));
      await failure.request.get(base + '/transport/fixture/session');
      await recovery.goto(base + '/transport/bff/login');
      assert((await recovery.locator('main').textContent()).includes('Continue to the application'));
      assert.equal((await (await failure.request.get(base + '/transport/bff/user')).json()).authenticated, true);
      const csrf = await (await failure.request.get(base + '/transport/bff/antiforgery')).json();
      const logout = await failure.request.post(base + '/transport/bff/logout', { headers: { 'X-CSRF-TOKEN': csrf.requestToken }, maxRedirects: 0 });
      assert.equal(logout.status(), 302);
      assert.equal(logout.headers().location, '/transport/landing?remote=unavailable');
      await recovery.goto(base + logout.headers().location);
      assert((await recovery.locator('body').textContent()).includes("You're signed out of this application"));
      assert((await recovery.locator('body').textContent()).includes('could not be confirmed'));
      assert.equal((await (await failure.request.get(base + '/transport/bff/user')).json()).authenticated, false);
      assert.equal((await failure.request.get(base + '/transport/api/private')).status(), 401);
      observations.push({ engine: name, version: browser.version(), humanDwellSeconds: 15, freshNativeLogin: true, expiryRestart: true, expiredAttemptPreservedSession: true, preservedSessionRecovery: true, localLogoutObserved: true, providerLogoutUnconfirmed: true, status: 'passed' });
      console.log(name + ': native human-paced login, expiry/restart, preserved-session recovery and observed local logout passed');
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
});
