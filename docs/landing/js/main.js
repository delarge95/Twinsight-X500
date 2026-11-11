// Landing de TwinSight X500: idioma, enlaces, scroll → estado 3D, rótulos, telemetría reproducida.
import { ES, EN } from './i18n.js';

// ─────────────────────────────────────────────────────────────────────────────
// ENLACES DE PRODUCTO. Editar aquí cuando se publiquen las apps.
//   webApp:  URL de la web app en Three.js. Vacío → los botones abren el visor Unity WebGL (viewer).
//   desktop: URL de descarga de la app de escritorio (instalador, .zip o página de release).
//            Vacío → el botón queda como «próximamente» y no enlaza a nada.
// ─────────────────────────────────────────────────────────────────────────────
const LINKS = {
  webApp: '',
  viewer: './Build/',
  desktop: '',
};

const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const clamp = (x, a = 0, b = 1) => Math.min(b, Math.max(a, x));
const lerp = (a, b, t) => a + (b - a) * t;
const ease = (t) => t * t * (3 - 2 * t);
const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
const store = {
  get(k) { try { return localStorage.getItem(k); } catch { return null; } },
  set(k, v) { try { localStorage.setItem(k, v); } catch { /* sin almacenamiento */ } },
};

// ───────────── idioma ─────────────
const i18nEls = $$('[data-i18n]');
const esDom = {};
for (const el of i18nEls) if (!(el.dataset.i18n in esDom)) esDom[el.dataset.i18n] = el.textContent;
let lang = 'es';
const t = (key) => (lang === 'en' ? EN[key] : ES[key] ?? esDom[key]) ?? ES[key] ?? esDom[key] ?? key;

function fmt(value, dec) {
  const [int, frac] = Math.abs(value).toFixed(dec).split('.');
  const grouped = int.replace(/\B(?=(\d{3})+(?!\d))/g, lang === 'en' ? ',' : ' ');
  return (value < 0 ? '−' : '') + grouped + (frac ? (lang === 'en' ? '.' : ',') + frac : '');
}

function applyLinks() {
  const hasApp = !!LINKS.webApp, hasDesk = !!LINKS.desktop;
  for (const a of $$('[data-link="webapp"]')) {
    a.href = hasApp ? LINKS.webApp : LINKS.viewer;
    const label = $('[data-i18n]', a);
    const short = label.dataset.i18n.endsWith('Short');
    label.dataset.i18n = hasApp ? (short ? 'ctaWebShort' : 'ctaWeb') : (short ? 'ctaViewerShort' : 'ctaViewer');
  }
  for (const a of $$('[data-link="desktop"]')) {
    const label = $('[data-i18n]', a);
    if (hasDesk) {
      a.href = LINKS.desktop; a.removeAttribute('aria-disabled'); label.dataset.i18n = 'ctaDesktop';
    } else {
      a.removeAttribute('href'); a.setAttribute('aria-disabled', 'true'); a.setAttribute('role', 'link');
      label.dataset.i18n = 'ctaDesktopSoon';
    }
  }
  const note = $('[data-link-note]');
  if (note) note.dataset.i18n = hasApp ? (hasDesk ? '' : 'noteDesktop') : 'noteViewer';
  const sw = $('[data-status="webapp"]'), sd = $('[data-status="desktop"]');
  if (sw) { sw.dataset.i18n = hasApp ? 'stLive' : 'stViewer'; sw.classList.add('status--ok'); }
  if (sd) { sd.dataset.i18n = hasDesk ? 'stLive' : 'stSoon'; sd.classList.toggle('status--ok', hasDesk); }
}

function setLang(next, persist = true) {
  lang = next === 'en' ? 'en' : 'es';
  document.documentElement.lang = lang;
  for (const el of $$('[data-i18n]')) el.textContent = el.dataset.i18n ? t(el.dataset.i18n) : '';
  for (const el of $$('[data-num]')) if (!el.dataset.counting) el.textContent = fmt(+el.dataset.num, +el.dataset.dec || 0);
  for (const b of $$('[data-lang]')) b.setAttribute('aria-pressed', String(b.dataset.lang === lang));
  document.title = t('docTitle');
  $('meta[name="description"]')?.setAttribute('content', t('docDesc'));
  for (const a of $$('.docs a[href^="docs.html"]')) {
    const u = new URL(a.getAttribute('href'), location.href);
    u.searchParams.set('lang', lang);
    a.setAttribute('href', 'docs.html' + u.search);
  }
  for (const a of $$('#rail a')) a.textContent = t('rail_' + a.dataset.ch);
  for (const tag of $$('.tag')) { $('b', tag).textContent = t('a_' + tag.dataset.id); $('i', tag).textContent = t('a_' + tag.dataset.id + '_c'); }
  for (const tag of tags.values()) tag.w = undefined; // el ancho de cada rótulo cambia con el idioma
  if (persist) store.set('twinsight.lang', lang);
}

// ───────────── capítulos ─────────────
const sections = $$('[data-chapter]');
const BASE = { az: 0.7, el: 0.36, dist: 1, sx: 0.19, sy: 0.02, explode: 0, sweep: 0, thermal: 0, flight: 0, grid: 0.5, sway: 0.22, spin: 1, labels: 0, o: 1 };
const KEYS = {
  hero: {},
  pipeline: { az: 2.05, el: 0.52, dist: 0.98, sx: 0.2, sy: 0, sweep: 1, kind: 'wire', sway: 0.12, grid: 0.7 },
  explode: { az: -0.55, el: 0.42, dist: 1.85, sx: -0.2, sy: 0.03, explode: 1, labels: 1, sway: 0.08, spin: 0, grid: 0.2 },
  xray: { az: 0.35, el: 0.22, dist: 0.92, sx: 0.2, sy: 0, sweep: 1, kind: 'xray', sway: 0.16, grid: 0.3 },
  thermal: { az: -0.95, el: 0.78, dist: 1.02, sx: -0.2, sy: 0, thermal: 1, sway: 0.1 },
  flight: { az: 2.6, el: 0.2, dist: 1.34, sx: 0.1, sy: -0.1, flight: 1, grid: 1, sway: 0.05 },
  apps: { az: 0.9, el: 0.3, dist: 1.7, sx: 0, sy: -0.08, o: 0.16, sway: 0.1 },
  evidence: { az: 0.4, el: 0.3, dist: 1.7, sx: 0, sy: 0, o: 0 },
  engineering: { az: 0.4, el: 0.3, dist: 1.7, sx: 0, sy: 0, o: 0 },
  docs: { az: 0.2, el: 0.3, dist: 1.5, sx: 0.2, sy: 0, o: 0 },
  outro: { az: 0.45, el: 0.3, dist: 1.08, sx: 0.2, sy: -0.06, o: 1, sway: 0.25 },
};
const MOBILE = { // el dron sube y el texto queda abajo
  hero: { sx: 0, sy: -0.31, dist: 1.16 },
  pipeline: { sx: 0, sy: -0.2 }, explode: { sx: 0, sy: -0.2, dist: 1.75 }, xray: { sx: 0, sy: -0.2 },
  thermal: { sx: 0, sy: -0.2 }, flight: { sx: 0, sy: -0.26, dist: 1.3 }, apps: { sy: -0.2 }, outro: { sx: 0, sy: -0.2, dist: 1.05 },
};
const DOCK_CHAPTERS = new Set(['pipeline', 'explode', 'xray', 'thermal']);
const MODES = {
  solid: { sweep: 0, thermal: 0, explode: 0, labels: 0 },
  wire: { sweep: 1, kind: 'wire', thermal: 0, explode: 0, labels: 0 },
  xray: { sweep: 1, kind: 'xray', thermal: 0, explode: 0, labels: 0 },
  thermal: { sweep: 0, thermal: 1, explode: 0, labels: 0 },
  explode: { sweep: 0, thermal: 0, explode: 1, labels: 1, spin: 0 },
};

let twin = null;
let active = 0;
let override = null, overrideAt = -1;
let flightOn = false;
const stage = $('#stage');
const dock = $('#dock');
const nav = $('#nav');
const progress = $('#progress');

function keyFor(name, mobile) {
  return { ...BASE, ...KEYS[name], ...(mobile ? { sx: 0, ...MOBILE[name] } : null) };
}

function compute() {
  const vh = innerHeight;
  const mobile = innerWidth <= 860;
  const s = keyFor('hero', mobile);
  let kind = 'wire';
  let current = 0;
  sections.forEach((sec, i) => {
    if (i === 0) return;
    const top = sec.getBoundingClientRect().top;
    const k = ease(clamp((vh * 0.92 - top) / (vh * 0.66)));
    if (k <= 0) return;
    const key = keyFor(sec.dataset.chapter, mobile);
    for (const name in s) s[name] = lerp(s[name], key[name], k);
    if (key.kind) kind = key.kind;
    if (top < vh * 0.55) current = i;
  });
  if (current !== active) {
    active = current;
    for (const a of $$('#rail a')) a.classList.toggle('is-active', +a.dataset.i === active);
    const id = sections[active].id || sections[active].closest('[id]')?.id;
    for (const a of $$('.nav__links a')) a.classList.toggle('is-active', a.getAttribute('href') === '#' + navTarget(active));
  }
  if (override && overrideAt !== active) override = null;
  const chapter = sections[active].dataset.chapter;
  if (override) { Object.assign(s, MODES[override]); if (MODES[override].kind) kind = MODES[override].kind; }
  s.kind = kind;

  const showDock = DOCK_CHAPTERS.has(chapter) && !!twin;
  dock.classList.toggle('is-on', showDock);
  const mode = s.thermal > 0.5 ? 'thermal' : s.sweep > 0.5 ? kind : s.explode > 0.5 ? 'explode' : 'solid';
  for (const b of $$('button', dock)) b.setAttribute('aria-pressed', String(b.dataset.mode === mode));

  stage.style.setProperty('--stage-o', s.o.toFixed(3));
  flightOn = s.flight > 0.02;
  if (twin) { twin.setState(s); twin.setVisible(s.o > 0.01); }

  const max = document.documentElement.scrollHeight - vh;
  progress.style.transform = `scaleX(${max > 0 ? clamp(scrollY / max) : 0})`;
  nav.classList.toggle('is-stuck', scrollY > 24);
}

const NAV_ORDER = ['producto', 'telemetria', 'apps', 'evidencia', 'docs'];
function navTarget(index) {
  let id = '';
  for (let i = 0; i <= index; i++) if (NAV_ORDER.includes(sections[i].id)) id = sections[i].id;
  return id;
}

let ticking = false;
function schedule() { if (!ticking) { ticking = true; requestAnimationFrame(() => { ticking = false; compute(); }); } }

// ───────────── índice lateral y rótulos ─────────────
const rail = $('#rail');
sections.forEach((sec, i) => {
  if (!sec.id) sec.id = 'ch-' + sec.dataset.chapter;
  const a = document.createElement('a');
  a.href = '#' + sec.id; a.dataset.i = i; a.dataset.ch = sec.dataset.chapter; a.tabIndex = -1;
  rail.append(a);
});

const labelLayer = $('#labels');
const SVG = 'http://www.w3.org/2000/svg';
const labelSvg = document.createElementNS(SVG, 'svg');
labelLayer.append(labelSvg);
const tags = new Map();
const MOBILE_TAGS = new Set(['fc', 'motor', 'battery', 'gps']);
for (const id of ['fc', 'gps', 'motor', 'prop', 'battery', 'pm', 'radio', 'plate']) {
  const el = document.createElement('div');
  el.className = 'tag'; el.dataset.id = id;
  el.innerHTML = '<b></b><i></i>';
  const line = document.createElementNS(SVG, 'line'), dot = document.createElementNS(SVG, 'circle');
  dot.setAttribute('r', 3);
  labelSvg.append(line, dot);
  labelLayer.append(el);
  tags.set(id, { el, line, dot });
}
labelLayer.style.opacity = 0;
// Rótulos tipo plano técnico: caja desplazada hacia fuera, línea guía hasta la pieza y sin solapes.
function placeTags(list, alpha) {
  const o = list ? clamp((alpha - 0.5) / 0.5) : 0;
  labelLayer.style.opacity = o.toFixed(2);
  if (!list || o === 0) return;
  const mobile = innerWidth <= 860;
  const gap = mobile ? 20 : 54, pitch = mobile ? 36 : 52;
  const shown = [];
  for (const a of list) {
    const tag = tags.get(a.id);
    if (!tag) continue;
    const off = a.hidden || a.x < 12 || a.x > innerWidth - 12 || a.y < 76 || a.y > innerHeight - 60 || (mobile && !MOBILE_TAGS.has(a.id));
    tag.el.style.visibility = tag.line.style.visibility = tag.dot.style.visibility = off ? 'hidden' : 'visible';
    if (!off) shown.push({ a, tag });
  }
  if (!shown.length) return;
  const cx = shown.reduce((n, s) => n + s.a.x, 0) / shown.length;
  for (const side of [-1, 1]) {
    const group = shown.filter((s) => (s.a.x < cx ? -1 : 1) === side).sort((p, q) => p.a.y - q.a.y);
    let prev = -Infinity;
    for (const { a, tag } of group) {
      const y = Math.min(innerHeight - 48, Math.max(a.y - 14, prev + pitch, 90));
      prev = y;
      const w = (tag.w ??= tag.el.offsetWidth);
      let x = a.x + side * gap;
      x = side < 0 ? Math.max(x, w + 8) : Math.min(x, innerWidth - w - 8); // la caja no se sale de la pantalla
      tag.el.classList.toggle('tag--flip', side < 0);
      tag.el.style.transform = `translate(${x.toFixed(1)}px, ${y.toFixed(1)}px) translate(${side < 0 ? '-100%' : '0'}, -50%)`;
      tag.line.setAttribute('x1', a.x.toFixed(1)); tag.line.setAttribute('y1', a.y.toFixed(1));
      tag.line.setAttribute('x2', x.toFixed(1)); tag.line.setAttribute('y2', y.toFixed(1));
      tag.dot.setAttribute('cx', a.x.toFixed(1)); tag.dot.setAttribute('cy', a.y.toFixed(1));
    }
  }
}

// ───────────── telemetría reproducida ─────────────
const hud = { alt: $('#hAlt'), spd: $('#hSpd'), roll: $('#hRoll'), pitch: $('#hPitch'), bat: $('#hBat'), bar: $('#hBatBar'), t: $('#hudT'), motors: $$('.motors__x i') };
let flight = null, flightIdx = 0, flightReq = false, hudClock = 0;
async function loadFlight() {
  if (flightReq) return;
  flightReq = true;
  try {
    flight = await (await fetch('landing/data/flight.json')).json();
    twin?.setFlight(flight.frames);
  } catch { flightReq = false; }
}
function paintHud() {
  if (!flight) return;
  const f = flight.frames[Math.floor(flightIdx) % flight.frames.length];
  hud.alt.textContent = fmt(f[5], 1);
  hud.spd.textContent = fmt(f[6], 1);
  hud.roll.textContent = fmt(f[0], 1);
  hud.pitch.textContent = fmt(f[1], 1);
  hud.bat.textContent = fmt(f[7], 2);
  hud.bar.style.width = (clamp(f[8]) * 100).toFixed(1) + '%';
  const secs = Math.floor(flight.start_s + flightIdx / flight.rate_hz);
  hud.t.textContent = 'T+' + String(Math.floor(secs / 60)).padStart(2, '0') + ':' + String(secs % 60).padStart(2, '0');
  for (const m of hud.motors) m.style.setProperty('--u', clamp(f[9 + +m.dataset.m]).toFixed(3));
}
let lastTick = performance.now();
function tick(now) {
  requestAnimationFrame(tick);
  const dt = Math.min(0.1, (now - lastTick) / 1000);
  lastTick = now;
  if (!flight || !flightOn || document.hidden) return;
  if (!reduced) flightIdx = (flightIdx + dt * flight.rate_hz) % flight.frames.length;
  twin?.setFlightIndex(flightIdx);
  hudClock += dt;
  if (hudClock > 0.1) { hudClock = 0; paintHud(); }
}

// ───────────── entradas y contadores ─────────────
function setupReveals() {
  for (const title of $$('.title, .hero__title, .outro__title')) $$('.ln > span', title).forEach((s, i) => s.style.setProperty('--l', i));
  for (const parent of new Set($$('[data-reveal]').map((e) => e.parentElement))) {
    [...parent.children].filter((c) => c.hasAttribute('data-reveal')).forEach((c, i) => c.style.setProperty('--i', i));
  }
  const io = new IntersectionObserver((entries) => {
    for (const e of entries) {
      if (!e.isIntersecting) continue;
      e.target.classList.add('is-in');
      for (const n of $$('[data-count]', e.target).concat(e.target.matches('[data-count]') ? [e.target] : [])) countUp(n);
      io.unobserve(e.target);
    }
  }, { rootMargin: '0px 0px -12% 0px', threshold: 0.08 });
  for (const el of $$('[data-reveal], .title, .hero__title, .outro__title')) {
    if (el.closest('.hero')) requestAnimationFrame(() => el.classList.add('is-in')); else io.observe(el);
  }
}
function countUp(el) {
  if (reduced || el.dataset.counting) return;
  const end = +el.dataset.num, dec = +el.dataset.dec || 0, start = performance.now(), dur = 1500;
  el.dataset.counting = '1';
  const step = (now) => {
    const k = clamp((now - start) / dur);
    el.textContent = fmt(end * (1 - Math.pow(1 - k, 4)), dec);
    if (k < 1) requestAnimationFrame(step); else delete el.dataset.counting;
  };
  requestAnimationFrame(step);
}

// ───────────── interacción ─────────────
for (const b of $$('[data-lang]')) b.addEventListener('click', () => setLang(b.dataset.lang));
for (const b of $$('button', dock)) b.addEventListener('click', () => {
  override = b.dataset.mode; overrideAt = active; compute();
});
document.addEventListener('click', (e) => { if (e.target.closest('[aria-disabled="true"]')) e.preventDefault(); });
addEventListener('scroll', schedule, { passive: true });
addEventListener('resize', () => { twin?.resize(); for (const tag of tags.values()) tag.w = undefined; schedule(); });
addEventListener('pointermove', (e) => {
  if (e.pointerType !== 'mouse') return;
  twin?.setPointer((e.clientX / innerWidth) * 2 - 1, (e.clientY / innerHeight) * 2 - 1);
  const card = e.target.closest?.('.app');
  if (card) { const r = card.getBoundingClientRect(); card.style.setProperty('--mx', e.clientX - r.left + 'px'); card.style.setProperty('--my', e.clientY - r.top + 'px'); }
}, { passive: true });

// arrastrar con el ratón sobre zonas vacías gira el modelo
let dragX = null;
addEventListener('pointerdown', (e) => {
  if (e.pointerType !== 'mouse' || e.button !== 0 || !twin) return;
  if (e.target.closest('a, button, p, h1, h2, h3, li, dl, aside, article, header, footer, .arch')) return;
  dragX = e.clientX; document.documentElement.style.cursor = 'grabbing'; e.preventDefault();
});
addEventListener('pointermove', (e) => { if (dragX !== null) { twin.drag((dragX - e.clientX) * 0.006, true); dragX = e.clientX; } });
addEventListener('pointerup', () => { if (dragX !== null) { dragX = null; twin.drag(0, false); document.documentElement.style.cursor = ''; } });

// ───────────── arranque ─────────────
const query = new URLSearchParams(location.search).get('lang');
applyLinks();
setLang(query || store.get('twinsight.lang') || (/^es/i.test(navigator.language || 'es') ? 'es' : 'en'), !!query);
setupReveals();
compute();
requestAnimationFrame(tick);
new IntersectionObserver((entries, io) => { if (entries.some((e) => e.isIntersecting)) { loadFlight(); io.disconnect(); } }, { rootMargin: '150% 0px' }).observe($('#telemetria'));

const loader = $('#loader'), loaderPct = $('#loaderPct');
async function boot3d() {
  try {
    const { createTwin } = await import('./scene.js');
    twin = await createTwin(stage, {
      modelUrl: 'landing/models/x500.glb',
      reducedMotion: reduced,
      onProgress: (p) => { loaderPct.textContent = Math.round(clamp(p) * 100) + '%'; },
      onAnchors: placeTags,
    });
    if (flight) twin.setFlight(flight.frames);
    compute();
    twin.snap();
    stage.classList.add('is-ready');
    loader.classList.add('is-done');
    document.documentElement.classList.add('has-3d');
  } catch (err) {
    console.warn('TwinSight: sin 3D', err);
    loader.classList.add('is-error');
    document.documentElement.classList.add('no-3d');
    loader.firstElementChild.dataset.i18n = 'loadError';
    loader.firstElementChild.textContent = t('loadError');
    loaderPct.textContent = '';
  }
}
if ('requestIdleCallback' in window) requestIdleCallback(boot3d, { timeout: 600 }); else setTimeout(boot3d, 60);
