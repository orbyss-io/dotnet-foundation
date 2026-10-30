// Actual native assets and browser lifecycle; recording writes establish no consumer/database acceptance.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawn } = require('node:child_process');
const playwright = require('playwright');
const root = path.resolve(__dirname, '../..');
const engines = process.argv.find(value => value.startsWith('--engines='))?.slice(10).split(',') || ['chromium', 'firefox', 'webkit'];
assert(engines.length && engines.every(name => ['chromium', 'firefox', 'webkit'].includes(name)));
const evidence = path.join(root, 'artifacts/browser-autosave', Date.now() + '-' + crypto.randomUUID());
fs.mkdirSync(evidence, { recursive: true });
const server = spawn('dotnet', ['tests/dotnet/Orbyss.Foundation.Authentication.BffCookie.Probe/bin/Release/net10.0/Orbyss.Foundation.Authentication.BffCookie.Probe.dll', '--serve-presentation'],
  { cwd: root, windowsHide: true });
const serverClosed = new Promise(resolve => server.once('close', resolve));
const stdout = []; const stderr = [];
server.stdout.on('data', data => stdout.push(data)); server.stderr.on('data', data => stderr.push(data));
const observations = [];
const assetHashes = {};
let status = 'failed';
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');

async function run() {
  try {
    const base = await new Promise((resolve, reject) => {
      const deadline = setTimeout(() => { clearInterval(poll); reject(new Error('Native autosave fixture startup timed out.')); }, 60000);
      const poll = setInterval(() => {
        const match = Buffer.concat(stdout).toString().match(/PRESENTATION_BROWSER_PROBE_URL=(http:\/\/127\.0\.0\.1:\d+)/);
        if (match) { clearTimeout(deadline); clearInterval(poll); resolve(match[1]); }
        else if (server.exitCode !== null) { clearTimeout(deadline); clearInterval(poll); reject(new Error('Native autosave fixture exited.')); }
      }, 50);
    });
    for (const prefix of ['', '/pages-a/app']) {
      const response = await fetch(base + prefix + '/auth/configuration');
      const configuration = await response.json();
      assert.equal(Object.keys(configuration).length, 7);
      assert.equal(configuration.contract, 'foundation-browser-presentation/v1');
      for (const name of ['autosave.mjs', 'autosave.d.mts']) {
        const route = base + prefix + '/auth/assets/v1/' + name;
        const get = await fetch(route); assert.equal(get.status, 200);
        assert.equal(get.headers.get('content-type'), 'text/javascript; charset=utf-8');
        assert.equal(get.headers.get('x-robots-tag'), 'noindex, nofollow');
        const bytes = Buffer.from(await get.arrayBuffer());
        const source = fs.readFileSync(path.join(root, 'src/Orbyss.Foundation.Web.Presentation/browser', name));
        assert.equal(sha(bytes), sha(source)); assetHashes[name] = sha(bytes);
        const head = await fetch(route, { method: 'HEAD' });
        assert.equal(head.status, 200); assert.equal((await head.arrayBuffer()).byteLength, 0);
      }
    }
    for (const engine of engines) {
      const browser = await playwright[engine].launch({ headless: true });
      try {
        for (const viewport of [{ width: 1280, height: 720 }, { width: 390, height: 844 }]) {
          const context = await browser.newContext({ viewport });
          try {
            const page = await context.newPage(); const errors = [];
            page.on('pageerror', error => errors.push(error.message));
            await page.goto(base + '/pages-a/app/auth/sign-in');
            const result = await page.evaluate(async () => {
              const configuration = await import('/pages-a/app/auth/assets/v1/configuration.mjs');
              const selected = await configuration.loadBrowserPresentation('/pages-a/app/auth/configuration');
              const folder = selected.modulePath.slice(0, -'presentation.mjs'.length);
              const { createAutosaveController, AUTOSAVE_CONTRACT } = await import(folder + 'autosave.mjs');
              const { unsavedWork } = await import(selected.modulePath);
              const until = async predicate => {
                const end = performance.now() + 10000;
                while (!predicate()) {
                  if (performance.now() >= end) throw new Error('Autosave browser observation timed out.');
                  await new Promise(resolve => setTimeout(resolve, 5));
                }
              };
              const initial = { editRevision: 1, confirmedEditRevision: 0, generation: 'browser-fixture', pending: null };
              const controllers = [];
              let stopStalled = () => {};
              let release;
              const gate = new Promise(resolve => { release = resolve; });
              let calls = 0; let effects = 0; let acknowledgedPhase;
              let target;
              const automatic = createAutosaveController({ enabled: true, intervalMilliseconds: 100, operationTimeoutMilliseconds: 5000 });
              controllers.push(automatic);
              const unregister = unsavedWork(selected.surface).register({ getState: () => target.unsaved() });
              const button = document.createElement('button'); button.textContent = 'Other save'; document.body.append(button);
              try {
                target = automatic.register({ target: 'automatic', maximumPayloadBytes: 2048, initial,
                  adapter: {
                    async admit(context) { context.signal.throwIfAborted(); return 'ready'; },
                    capture(identity) { return { kind: 'prepared', payload: JSON.stringify(identity) }; },
                    async write(attempt) { calls++; await gate; effects++; return { kind: 'confirmed', receipt: { editRevision: attempt.editRevision } }; },
                    confirmed() { if (!acknowledgedPhase) acknowledgedPhase = target.state.phase; },
                  } });
                await until(() => calls === 1);
                if (unsavedWork(selected.surface).getState() !== 'unknown') throw new Error('Pending save was reported clean.');
                target.edited(); release();
                await until(() => acknowledgedPhase !== undefined);
                if (acknowledgedPhase !== 'dirty') throw new Error('Earlier confirmation cleared newer browser input.');
                await until(() => !automatic.activity.busy);
                await target.save();
                if (effects !== 2 || target.state.phase !== 'clean') throw new Error('Manual and automatic saves did not share acknowledgement.');
                await automatic.dispose();

                let stop;
                const stalled = new Promise(resolve => { stop = resolve; });
                stopStalled = stop;
                const controller = createAutosaveController({ enabled: false, intervalMilliseconds: 100, operationTimeoutMilliseconds: 50 });
                controllers.push(controller);
                controller.subscribeActivity(activity => { button.disabled = activity.busy; });
                const slow = controller.register({ target: 'slow', maximumPayloadBytes: 1024, initial,
                  adapter: { async admit() { return 'ready'; }, capture() { return { kind: 'prepared', payload: '{}' }; },
                    write() { return stalled; }, confirmed() {} } });
                const saving = slow.save();
                await until(() => controller.activity.draining);
                if (!button.disabled || slow.state.phase !== 'unknown') throw new Error('Drain activity was not visible.');
                stop({ kind: 'unknown' }); await saving;
                await until(() => !controller.activity.busy);
                if (button.disabled || slow.state.phase !== 'unknown') throw new Error('Drain release changed business certainty or left controls disabled.');
                if (localStorage.length || sessionStorage.length) throw new Error('Autosave persisted private data.');
                return { contract: AUTOSAVE_CONTRACT, effects, newerEditPreserved: acknowledgedPhase === 'dirty',
                  drainReleaseObserved: !button.disabled, originalOutcomeStillUnknown: slow.state.phase === 'unknown' };
              } finally {
                release(); stopStalled({ kind: 'unknown' }); unregister(); button.remove();
                for (const controller of controllers) await controller.dispose();
              }
            });
            assert.equal(result.contract, 'foundation-browser-autosave/v1');
            assert.deepEqual(errors, []);
            observations.push({ engine, browserVersion: browser.version(), viewport, ...result });
          } finally { await context.close(); }
        }
      } finally { await browser.close(); }
    }
    status = 'passed';
    console.log('Native autosave browser checks passed: ' + engines.join(','));
  } finally {
    if (server.exitCode === null) server.kill();
    await serverClosed;
    fs.writeFileSync(path.join(evidence, 'server.stdout.log'), Buffer.concat(stdout));
    fs.writeFileSync(path.join(evidence, 'server.stderr.log'), Buffer.concat(stderr));
    fs.writeFileSync(path.join(evidence, 'result.json'), JSON.stringify({ status, assetHashes, observations,
      requestedEngines: engines, consumerAcceptance: false, databaseAcceptance: false }, null, 2));
  }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
