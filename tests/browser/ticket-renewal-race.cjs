// Native cookie delivery race with fictional identities and no external provider.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawn } = require('node:child_process');
const playwright = require('playwright');
const root = path.resolve(__dirname, '../..');
const engines = process.argv.find(arg => arg.startsWith('--engines='))?.split('=')[1].split(',') || ['chromium', 'firefox', 'webkit'];
const configuration = process.argv.find(arg => arg.startsWith('--configuration='))?.split('=')[1] || 'Release';
assert(engines.length && new Set(engines).size === engines.length && engines.every(name => ['chromium', 'firefox', 'webkit'].includes(name)));
assert(['Debug', 'Release'].includes(configuration));
const evidence = path.join(root, 'artifacts/ticket-renewal-browser', String(Date.now()));
fs.mkdirSync(evidence, { recursive: true });
const server = spawn('dotnet', [`tests/dotnet/Orbyss.Foundation.Authentication.BffCookie.Probe/bin/${configuration}/net10.0/Orbyss.Foundation.Authentication.BffCookie.Probe.dll`, '--serve-ticket-renewal'], { cwd: root, windowsHide: true });
let output = '';
server.stdout.on('data', data => { output += data; });
server.stderr.on('data', data => { output += data; });
const observations = [];

async function run() {
  const base = await new Promise((resolve, reject) => {
    const deadline = setTimeout(() => { clearInterval(poll); reject(new Error('Renewal fixture startup timeout')); }, 30000);
    const poll = setInterval(() => {
      const match = output.match(/TICKET_RENEWAL_PROBE_URL=(http:\/\/127\.0\.0\.1:\d+)/);
      if (match) { clearTimeout(deadline); clearInterval(poll); resolve(match[1]); }
      else if (server.exitCode !== null || server.signalCode !== null) { clearTimeout(deadline); clearInterval(poll); reject(new Error('Renewal fixture exited')); }
    }, 50);
    server.once('error', error => { clearTimeout(deadline); clearInterval(poll); reject(error); });
  });
  async function control(operation, method = 'POST') {
    const response = await fetch(base + '/probe/barrier/' + operation, { method });
    assert.equal(response.status, 204);
  }
  for (const engine of engines) {
    const browser = await playwright[engine].launch({ headless: true });
    try {
      for (const explicit of [false, true]) for (const persistent of [false, true]) {
        const context = await browser.newContext();
        try {
          const page = await context.newPage();
          await page.goto(base + '/probe/page');
          await control('arm');
          const login = await context.request.get(base + `/probe/login?old=${!explicit}&persistent=${persistent}&longLife=false`);
          assert.equal(login.status(), 204);
          const oldCookie = (await context.cookies()).find(cookie => cookie.name.startsWith('orbyss-foundation-session-'));
          assert(oldCookie);
          // This fetch goes through the real browser cookie jar. Its late Set-Cookie
          // arrives only after the browser has processed native logout's deletion.
          const late = page.evaluate(async route => (await fetch(route)).status, explicit ? '/probe/late-explicit' : '/probe/late-slide');
          await control('entered', 'GET');
          const csrf = await (await context.request.get(base + '/bff/antiforgery')).json();
          const logout = await context.request.post(base + '/bff/logout', { headers: { 'X-CSRF-TOKEN': csrf.requestToken }, maxRedirects: 0 });
          assert.equal(logout.status(), 302);
          assert(!(await context.cookies()).some(cookie => cookie.name.startsWith('orbyss-foundation-session-')));
          assert.equal((await context.request.get(base + '/probe/check')).status(), 401);
          await control('release');
          assert.equal(await late, 200);
          assert((await context.cookies()).some(cookie => cookie.name.startsWith('orbyss-foundation-session-')));
          assert.equal((await context.request.get(base + '/probe/check')).status(), 401);
          const replay = await fetch(base + '/probe/check', { headers: { Cookie: oldCookie.name + '=' + oldCookie.value } });
          assert.equal(replay.status, 401);
          observations.push({ engine, browserVersion: browser.version(), explicit, persistent, logoutDeletionObserved: true, lateCookieObserved: true, lateCookieDenied: true, oldCookieDenied: true });
        } finally { await control('release').catch(() => {}); await context.close(); }
      }
      console.log(engine + ': native sliding/explicit, session/persistent late cookies cannot restore logout');
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
  console.log('Renewal browser evidence: ' + evidence);
});
