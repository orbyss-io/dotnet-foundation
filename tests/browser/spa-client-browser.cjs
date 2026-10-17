// Actual selected OIDC client, native browser modules and a synthetic two-origin provider.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const crypto = require('node:crypto');
const playwright = require('playwright');
const root = path.resolve(__dirname, '../..');
const engines = process.argv.find(value => value.startsWith('--engines='))?.slice(10).split(',') || ['chromium', 'firefox', 'webkit'];
assert(engines.length && engines.every(name => ['chromium', 'firefox', 'webkit'].includes(name)));
const evidence = path.join(root, 'artifacts/browser-presentation/spa-client', Date.now() + '-' + crypto.randomUUID());
fs.mkdirSync(evidence, { recursive: true });
const { privateKey, publicKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
const key = { ...publicKey.export({ format: 'jwk' }), kid: 'synthetic-spa', alg: 'RS256', use: 'sig' };
let origin, authority, active = false, mode = 'normal';
let counts = {}; const codes = new Map(); const tokens = new Set(); const idTokens = new Set();
const observations = [];
const sourcePaths = ['tests/browser/spa-client-browser.cjs', 'tests/browser/spa-client-fixture.mjs',
  'tests/browser/spa-client-required-cases.json', 'tests/browser/package-lock.json',
  'src/Orbyss.Foundation.Web.Presentation/browser/presentation.mjs', 'src/Orbyss.Foundation.Web.Presentation/browser/spa.mjs',
  'src/Orbyss.Foundation.Web.Presentation/browser/session.mjs',
  'tests/browser/node_modules/oidc-client-ts/dist/esm/oidc-client-ts.js', 'tests/browser/node_modules/jwt-decode/build/esm/index.js'];
const sourceHashes = () => Object.fromEntries(sourcePaths.map(file => [file, crypto.createHash('sha256').update(fs.readFileSync(path.join(root, file))).digest('hex')]));
const originalSources = sourceHashes();
const requiredCases = JSON.parse(fs.readFileSync(path.join(__dirname, 'spa-client-required-cases.json'), 'utf8'));
function reset() { active = false; mode = 'normal'; counts = { authorize: 0, token: 0, logout: 0, userinfo: 0 }; codes.clear(); tokens.clear(); idTokens.clear(); }
function json(response, status, value, cors = false) {
  response.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store',
    ...(cors ? { 'Access-Control-Allow-Origin': origin } : {}) }); response.end(JSON.stringify(value));
}
function page(response, body, providerPage = false) { response.writeHead(200, { 'Content-Type': 'text/html', 'Cache-Control': 'public, max-age=120',
  ...(!providerPage ? { 'Cross-Origin-Opener-Policy': 'same-origin-allow-popups' } : {}), 'Referrer-Policy': 'no-referrer' });
  response.end('<!doctype html><html lang="en"><meta charset="utf-8"><title>Public SPA shell fixture</title>'
    + (!providerPage ? '<script type="importmap">{"imports":{"jwt-decode":"/vendor/jwt-decode.mjs"}}</script>' : '') + body + '</html>'); }
function signed(claims) {
  const head = Buffer.from(JSON.stringify({ alg: 'RS256', kid: key.kid, typ: 'JWT' })).toString('base64url');
  const body = Buffer.from(JSON.stringify(claims)).toString('base64url');
  return head + '.' + body + '.' + crypto.sign('RSA-SHA256', Buffer.from(head + '.' + body), privateKey).toString('base64url');
}
const provider = http.createServer(async (request, response) => {
  const url = new URL(request.url, authority);
  if (request.method === 'OPTIONS') { response.writeHead(204, { 'Access-Control-Allow-Origin': origin,
    'Access-Control-Allow-Headers': 'authorization,content-type', 'Access-Control-Allow-Methods': 'GET,POST,OPTIONS' }); response.end(); return; }
  if (url.pathname === '/.well-known/openid-configuration') return json(response, 200, { issuer: authority,
    authorization_endpoint: authority + '/authorize', token_endpoint: authority + '/token', userinfo_endpoint: authority + '/userinfo',
    jwks_uri: authority + '/keys', end_session_endpoint: authority + '/end-session', response_types_supported: ['code'],
    subject_types_supported: ['public'], id_token_signing_alg_values_supported: ['RS256'], code_challenge_methods_supported: ['S256'] }, true);
  if (url.pathname === '/keys') return json(response, 200, { keys: [key] }, true);
  if (url.pathname === '/authorize') {
    counts.authorize++;
    assert.equal(url.searchParams.get('client_id'), 'fixture-spa'); assert.equal(url.searchParams.get('redirect_uri'), origin + '/callback');
    assert.equal(url.searchParams.get('code_challenge_method'), 'S256');
    const code = crypto.randomUUID(); codes.set(code, Object.fromEntries(url.searchParams));
    return page(response, '<main><h1>Synthetic OIDC provider</h1><form action="/confirm"><input type="hidden" name="code" value="' + code + '"><button>Continue synthetic sign in</button></form></main>', true);
  }
  if (url.pathname === '/confirm') {
    const code = url.searchParams.get('code'); const selected = codes.get(code); assert(selected);
    active = true; response.writeHead(302, { Location: origin + '/callback?code=' + code + '&state=' + encodeURIComponent(selected.state) }); response.end(); return;
  }
  if (url.pathname === '/token') {
    counts.token++; let body = ''; for await (const chunk of request) { body += chunk; assert(body.length < 16384); }
    const input = new URLSearchParams(body); const code = input.get('code'); const selected = codes.get(code); codes.delete(code);
    assert(selected); assert.equal(input.get('grant_type'), 'authorization_code'); assert.equal(input.get('client_id'), 'fixture-spa');
    assert.equal(input.get('redirect_uri'), origin + '/callback');
    assert.equal(crypto.createHash('sha256').update(input.get('code_verifier')).digest('base64url'), selected.code_challenge);
    const now = Math.floor(Date.now() / 1000); const idToken = signed({ iss: authority, aud: 'fixture-spa', sub: 'synthetic-owner',
      iat: now, exp: now + 300, auth_time: now, nonce: mode === 'bad-nonce' ? 'different-nonce' : selected.nonce });
    const token = crypto.randomUUID(); tokens.add(token); idTokens.add(idToken);
    return json(response, 200, { id_token: idToken, access_token: token, token_type: 'Bearer', expires_in: 300, scope: 'openid profile' }, true);
  }
  if (url.pathname === '/userinfo') {
    counts.userinfo++;
    if (mode === 'userinfo-hold') return;
    if (mode === 'userinfo-unavailable') return json(response, 503, { error: 'synthetic_unavailable' }, true);
    if (!active || !tokens.has(request.headers.authorization?.slice(7))) return json(response, 401, { error: 'inactive_synthetic_session' }, true);
    return json(response, 200, { sub: 'synthetic-owner' }, true);
  }
  if (url.pathname === '/end-session') {
    counts.logout++;
    assert(idTokens.has(url.searchParams.get('id_token_hint')));
    assert.equal(url.searchParams.get('post_logout_redirect_uri'), origin + '/logout-callback');
    if (mode === 'unavailable') return json(response, 503, { error: 'synthetic_unavailable' });
    active = false; response.writeHead(302, { Location: origin + '/logout-callback?state=' + encodeURIComponent(url.searchParams.get('state')) }); response.end(); return;
  }
  response.writeHead(404); response.end();
});
const application = http.createServer((request, response) => {
  const url = new URL(request.url, origin);
  if (url.pathname === '/configuration') return json(response, 200, { authority });
  if (url.pathname === '/public') return page(response, '<main><h1>Public fixture</h1></main>');
  if (['/', '/callback', '/logout-callback'].includes(url.pathname)) return page(response,
    '<main><h1>SPA fixture</h1><button id="sign-in">Native client sign in</button><button id="sign-out">Native client sign out</button>'
    + '<p id="status">checking</p><section id="private"></section><a href="/public">Public page</a></main><script type="module" src="/fixture.mjs"></script>');
  const files = { '/fixture.mjs': path.join(__dirname, 'spa-client-fixture.mjs'),
    '/vendor/oidc-client-ts.mjs': path.join(__dirname, 'node_modules/oidc-client-ts/dist/esm/oidc-client-ts.js'),
    '/vendor/jwt-decode.mjs': path.join(__dirname, 'node_modules/jwt-decode/build/esm/index.js') };
  for (const name of ['spa.mjs', 'presentation.mjs', 'session.mjs']) files['/modules/' + name] = path.join(root, 'src/Orbyss.Foundation.Web.Presentation/browser', name);
  if (files[url.pathname]) { response.writeHead(200, { 'Content-Type': 'text/javascript', 'Cache-Control': 'public, max-age=120' }); response.end(fs.readFileSync(files[url.pathname])); return; }
  response.writeHead(404); response.end();
});
async function listen(server) { await new Promise(resolve => server.listen(0, '127.0.0.1', resolve)); return 'http://127.0.0.1:' + server.address().port; }
async function main() {
  authority = await listen(provider); origin = await listen(application);
  const selected = require('./node_modules/oidc-client-ts/package.json'); assert.equal(selected.version, '3.5.0');
  for (const name of engines) {
    const browser = await playwright[name].launch(name === 'chromium' ? { headless: true, channel: 'chromium', ignoreDefaultArgs: ['--disable-back-forward-cache'] } : { headless: true });
    try {
      for (const viewport of [{ width: 1280, height: 720 }, { width: 390, height: 844 }]) {
        reset(); const context = await browser.newContext({ viewport });
        try {
          const page = await context.newPage(); await page.goto(origin);
          await page.waitForFunction(() => window.fixture);
          const popupPromise = page.waitForEvent('popup'); await page.getByRole('button', { name: 'Native client sign in', exact: true }).click();
          const popup = await popupPromise; await popup.getByRole('button', { name: 'Continue synthetic sign in' }).click();
          await page.waitForFunction(() => document.querySelector('#status').textContent === 'authenticated');
          assert(active); assert.equal(counts.authorize, 1); assert.equal(counts.token, 1);
          await page.getByRole('textbox', { name: 'Raw private draft' }).fill(' 1e- ');
          await page.evaluate(() => window.fixture.setDirty(true));
          const prior = { ...counts }; const openings = (await page.evaluate(() => window.fixture.state())).openings.length;
          await page.getByRole('button', { name: 'Native client sign out', exact: true }).click(); await page.getByRole('dialog').waitFor();
          assert.deepEqual(counts, prior); assert.equal((await page.evaluate(() => window.fixture.state())).openings.length, openings);
          await page.keyboard.press('Escape'); assert.equal(await page.getByRole('textbox', { name: 'Raw private draft' }).inputValue(), ' 1e- ');
          assert.equal((await page.evaluate(() => window.fixture.state())).nativeUserAbsent, false); assert.equal(counts.logout, 0);
          mode = 'unavailable';
          await page.getByRole('button', { name: 'Native client sign out', exact: true }).click();
          await page.getByRole('dialog').getByRole('button', { name: 'Sign out', exact: true }).click();
          await page.waitForFunction(() => document.querySelector('#status').textContent === 'anonymous; provider=unavailable');
          const cleared = await page.evaluate(() => window.fixture.state());
          assert(cleared.nativeUserAbsent && cleared.memory === 0 && cleared.local === 0 && cleared.session === 0);
          assert.equal(counts.logout, 1); assert(active, '503 must not claim remote provider termination');
          assert(cleared.openings.at(-1).activation && cleared.openings.at(-1).localEmpty && cleared.openings.at(-1).projectionEmpty);
          // A new native login and independent provider revocation exercise a real
          // persisted restore. This synthetic provider qualifies live userinfo revocation;
          // it does not imply immediate revocation for every JWT/provider topology.
          mode = 'normal';
          const againPromise = page.waitForEvent('popup'); await page.getByRole('button', { name: 'Native client sign in', exact: true }).click();
          const again = await againPromise; await again.getByRole('button', { name: 'Continue synthetic sign in' }).click();
          await page.waitForFunction(() => document.querySelector('#status').textContent === 'authenticated');
          mode = 'userinfo-unavailable';
          assert.equal((await page.evaluate(() => window.fixture.observer.refresh())).state, 'unavailable');
          const unavailable = await page.evaluate(() => window.fixture.state());
          assert(!unavailable.privateVisible && !unavailable.nativeUserAbsent, 'Unavailable proof must hide private UI without inventing local termination');
          mode = 'userinfo-hold';
          assert.equal((await page.evaluate(() => window.fixture.observer.refresh())).state, 'unavailable');
          assert.equal((await page.evaluate(() => window.fixture.state())).privateVisible, false);
          mode = 'normal'; await page.evaluate(() => window.fixture.observer.refresh());
          await page.getByRole('link', { name: 'Public page' }).click(); active = false;
          await page.goBack({ waitUntil: 'commit' }); await page.waitForFunction(() => window.fixture && document.querySelector('#status').textContent === 'anonymous');
          const restored = await page.evaluate(() => ({ persisted: window.persistedSpaRestore,
            hidden: document.querySelector('#private').hidden, status: document.querySelector('#status').textContent }));
          const restoredState = await page.evaluate(() => window.fixture.state());
          assert(restoredState.nativeUserAbsent && !restoredState.privateVisible && restoredState.local === 0 && restoredState.session === 0);
          if (name === 'chromium') assert.equal(restored.persisted, true, 'Required actual persisted SPA restore did not occur');
          const finalLoginPromise = page.waitForEvent('popup'); await page.getByRole('button', { name: 'Native client sign in', exact: true }).click();
          const finalLogin = await finalLoginPromise; await finalLogin.getByRole('button', { name: 'Continue synthetic sign in' }).click();
          await page.waitForFunction(() => document.querySelector('#status').textContent === 'authenticated');
          await page.evaluate(() => window.fixture.setDirty(false));
          await page.getByRole('button', { name: 'Native client sign out', exact: true }).click();
          await page.waitForFunction(() => document.querySelector('#status').textContent === 'anonymous; provider=dispatched');
          assert.equal(counts.logout, 2); assert.equal(active, false, 'Native provider logout callback did not terminate its synthetic session');
          const finalState = await page.evaluate(() => window.fixture.state());
          assert(finalState.nativeUserAbsent && finalState.local === 0 && finalState.session === 0);
          assert(finalState.openings.at(-1).activation && finalState.openings.at(-1).localEmpty);
          mode = 'bad-nonce';
          const rejectedPromise = page.waitForEvent('popup'); await page.getByRole('button', { name: 'Native client sign in', exact: true }).click();
          const rejected = await rejectedPromise; await rejected.getByRole('button', { name: 'Continue synthetic sign in' }).click();
          assert.equal(await page.evaluate(() => window.signinResult), 'unavailable');
          assert.equal((await page.evaluate(() => window.fixture.state())).nativeUserAbsent, true);
          const cases = ['native-pkce-code-state-token-exchange', 'confirmation-cancel-zero-provider-effects-raw-draft-retained',
            'native-popup-fresh-activation-local-store-cleared-first', 'provider-unavailable-local-termination-no-retry',
            'fresh-userinfo-unavailable-private-hidden-no-invented-logout', 'real-observation-timeout-private-hidden',
            'actual-history-private-revalidation', 'native-provider-logout-confirmed-independently', 'native-invalid-nonce-rejected'];
          assert.equal(new Set(cases).size, cases.length); assert.deepEqual([...cases].sort(), [...requiredCases].sort());
          await page.screenshot({ path: path.join(evidence, name + '-' + viewport.width + '.png') });
          observations.push({ engine: name, measuredViewport: await page.evaluate(() => ({ width: innerWidth, height: innerHeight })),
            persisted: restored.persisted, counts: { ...counts }, localUserAbsent: restoredState.nativeUserAbsent,
            cases,
            scope: 'Real public OIDC client and synthetic live-userinfo provider; no native Host/API/public release/consumer acceptance' });
        } finally { await context.close(); }
      }
    } finally { await browser.close(); }
  }
  assert.deepEqual(sourceHashes(), originalSources, 'SPA protocol sources changed during qualification');
  fs.writeFileSync(path.join(evidence, 'results.json'), JSON.stringify({ status: 'passed', clientVersion: selected.version, sourceSha256: originalSources,
    clientSha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(__dirname, 'node_modules/oidc-client-ts/dist/esm/oidc-client-ts.js'))).digest('hex'),
    lockSha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(__dirname, 'package-lock.json'))).digest('hex'), observations }, null, 2));
}
main().then(() => console.log('Selected SPA client proof: ' + evidence), error => {
  fs.writeFileSync(path.join(evidence, 'failure.json'), JSON.stringify({ status: 'failed', errorType: error.name, diagnostic: error.message, observations }, null, 2));
  process.exitCode = 1; console.error(error);
}).finally(() => { provider.closeAllConnections(); application.closeAllConnections(); provider.close(); application.close(); });
