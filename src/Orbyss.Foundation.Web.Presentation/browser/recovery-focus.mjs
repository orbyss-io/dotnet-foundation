// One recovery focus operation, deferred until this document is visible and active.
const heading = document.querySelector('main h1[tabindex="-1"]');
let pending = Boolean(heading);
function attempt() {
  if (!pending || document.visibilityState !== 'visible' || !document.hasFocus()) return;
  const active = document.activeElement;
  pending = false;
  if (active === document.body || active === document.documentElement || active === heading) heading.focus();
  window.removeEventListener('focus', attempt);
  document.removeEventListener('visibilitychange', attempt);
}
window.addEventListener('focus', attempt);
document.addEventListener('visibilitychange', attempt);
attempt();
