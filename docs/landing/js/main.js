// Landing de TwinSight X500: intro, idioma, scroll → estado 3D, rótulos, telemetría reproducida
// y el «portal» que abre la app dentro de la propia página.
import { ES, EN } from './i18n.js';

// ─────────────────────────────────────────────────────────────────────────────
// ENLACES DE PRODUCTO
//   app:     la web app nueva (Three.js / R3F), compilada en docs/app con desarrollo/landing/build-app.mjs
//   legacy:  el visor Unity WebGL evaluado con usuarios (docs/Build)
//   desktop: URL de descarga del escritorio. Vacío → el enlace dice «próximamente» y no lleva a nada.
// ─────────────────────────────────────────────────────────────────────────────
const LINKS = {
  app: './app/?embed=1', // embed=1: la app salta su pantalla de inicio y avisa cuando ha dibujado
  legacy: './Build/',
  desktop: '',
};

const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const clamp = (x, a = 0, b = 1) => Math.min(b, Math.max(a, x));
const lerp = (a, b, t) => a + (b - a) * t;
const ease = (t) => t * t * (3 - 2 * t);
const wait = (ms) => new Promise((r) => setTimeout(r, ms));
const root = document.documentElement;
const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
const store = {
  get(k) { try { return localStorage.getItem(k); } catch { return null; } },
  set(k, v) { try { localStorage.setItem(k, v); } catch { /* sin almacenamiento */ } },
};
const bootTime = performance.now();

// ───────────── idioma ─────────────
const esDom = {};
for (const el of $$('[data-i18n]')) if (!(el.dataset.i18n in esDom)) esDom[el.dataset.i18n] = el.textContent;
let lang = 'es';
const t = (key) => (lang === 'en' ? EN[key] : ES[key] ?? esDom[key]) ?? ES[key] ?? esDom[key] ?? key;

function fmt(value, dec) {
  const [int, frac] = Math.abs(value).toFixed(dec).split('.');
  const grouped = int.replace(/\B(?=(\d{3})+(?!\d))/g, lang === 'en' ? ',' : ' ');
  return (value < 0 ? '−' : '') + grouped + (frac ? (lang === 'en' ? '.' : ',') + frac : '');
}

function applyLinks() {
  const hasDesk = !!LINKS.desktop;
  for (const a of $$('[data-link="desktop"]')) {
    const label = $('[data-i18n]', a);
    if (hasDesk) { a.href = LINKS.desktop; a.removeAttribute('aria-disabled'); label.dataset.i18n = 'ctaDesktop'; }
    else { a.removeAttribute('href'); a.setAttribute('aria-disabled', 'true'); a.setAttribute('role', 'link'); label.dataset.i18n = 'ctaDesktopSoon'; }
  }
  const sd = $('[data-status="desktop"]');
  if (sd) { sd.dataset.i18n = hasDesk ? 'stLive' : 'stSoon'; sd.classList.toggle('status--ok', hasDesk); }
}

function setLang(next, persist = true) {
  lang = next === 'en' ? 'en' : 'es';
  root.lang = lang;
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
  for (const [id, tag] of tags) { $('b', tag.el).textContent = t('a_' + id); $('i', tag.el).textContent = t('a_' + id + '_c'); tag.w = undefined; }
  if (persist) store.set('twinsight.lang', lang); // la web app lee esta misma clave
}

// ───────────── capítulos ─────────────
const sections = $$('[data-chapter]');
const BASE = { az: 2.27, el: 0.3, dist: 1.12, sx: 0, sy: 0.26, explode: 0, sweep: 0, thermal: 0, flight: 0, grid: 0.55, sway: 0.22, spin: 1, labels: 0, o: 1, fov: 28, aim: 0 };
const KEYS = {
  hero: {},
  pipeline: { az: 2.05, el: 0.52, dist: 0.98, sx: 0.2, sy: 0, sweep: 1, kind: 'wire', sway: 0.12, grid: 0.7 },
  explode: { az: -0.55, el: 0.42, dist: 1.62, sx: -0.2, sy: 0.04, explode: 1, labels: 1, sway: 0.08, spin: 0, grid: 0.2 },
  xray: { az: 0.35, el: 0.22, dist: 0.92, sx: 0.2, sy: 0, sweep: 1, kind: 'xray', sway: 0.16, grid: 0.3 },
  thermal: { az: -0.95, el: 0.78, dist: 1.02, sx: -0.2, sy: 0, thermal: 1, sway: 0.1 },
  flight: { az: 2.6, el: 0.2, dist: 1.34, sx: 0.1, sy: -0.1, flight: 1, grid: 1, sway: 0.05 },
  apps: { az: 0.9, el: 0.3, dist: 1.7, sx: 0, sy: -0.08, o: 0.14, sway: 0.1 },
  evidence: { az: 0.4, el: 0.3, dist: 1.7, sx: 0, sy: 0, o: 0 },
  engineering: { az: 0.4, el: 0.3, dist: 1.7, sx: 0, sy: 0, o: 0 },
  docs: { az: 0.2, el: 0.3, dist: 1.5, sx: 0.2, sy: 0, o: 0 },
  outro: { az: 0.45, el: 0.3, dist: 1.02, sx: 0.2, sy: -0.04, o: 1, sway: 0.25 },
};
const MOBILE = { // el dron sube y el texto queda abajo
  hero: { sy: 0.21, dist: 1.16 },
  pipeline: { sy: -0.2 }, explode: { sy: -0.2, dist: 1.7 }, xray: { sy: -0.2 },
  thermal: { sy: -0.2 }, flight: { sy: -0.26, dist: 1.3 }, apps: { sy: -0.2 }, outro: { sy: -0.22, dist: 1.05 },
};
const DOCK_CHAPTERS = new Set(['pipeline', 'explode', 'xray', 'thermal']);
const MODES = {
  solid: { sweep: 0, thermal: 0, explode: 0, labels: 0 },
  wire: { sweep: 1, kind: 'wire', thermal: 0, explode: 0, labels: 0 },
  xray: { sweep: 1, kind: 'xray', thermal: 0, explode: 0, labels: 0 },
  thermal: { sweep: 0, thermal: 1, explode: 0, labels: 0 },
  explode: { sweep: 0, thermal: 0, explode: 1, labels: 1, spin: 0 },
};
// Intro: el dron llega desarmado y se ensambla. Lanzamiento: la cámara entra en el dron antes de abrir la app.
const INTRO_FROM = { az: 0.8, el: 0.95, dist: 2.3, sx: 0, sy: 0.06, explode: 1, sway: 0, spin: 0, grid: 0 };
// Vista con la que arranca la web app, copiada de su código para que el fundido sea exacto. Si cambia allí, cambiar aquí:
//   src/web/scene/director.ts  DEFAULT_DIR (1.1, 0.6, 1.3), la cámara mira al origen del dron
//   src/web/App.tsx            fov 45
//   src/shared/camera.ts       defaultView(): la distancia que encuadra el modelo (CAM.DEFAULT_VIEW_*)
const APP_CAMERA = { dir: [1.1, 0.6, 1.3], fov: 45, padding: 1.85, dominant: 2.8, minRatio: 0.18 };
const LAUNCH_BASE = { sx: 0, sy: 0, explode: 0, sweep: 0, thermal: 0, flight: 0, labels: 0, sway: 0, spin: 0, grid: 0.35, o: 1, aim: 1 };
/** El estado de la escena que reproduce esa vista: mismo ángulo, mismo campo de visión, misma distancia y mismo punto de mira. */
function launchPose() {
  const c = APP_CAMERA;
  const aspect = innerWidth / innerHeight;
  const halfV = (c.fov / 2) * (Math.PI / 180), halfH = Math.atan(Math.tan(halfV) * aspect);
  const [sx, sy, sz] = twin?.sizeM ?? [0.6, 0.25, 0.6];
  const ex = sx / 2, ey = sy / 2, ez = sz / 2;
  const framing = Math.max((Math.max(ey / Math.tan(halfV), ex / Math.tan(halfH)) + ez) * c.padding, Math.max(ex, ey, ez) * c.dominant);
  const metres = Math.max(framing, clamp(Math.max(sx, sy, sz) * c.minRatio, 0.05, 2) + 0.5);
  const fit = 1 / Math.sin(Math.min(halfV, halfH));
  const [x, y, z] = c.dir;
  // La app gira el modelo −90° sobre el eje vertical (src/shared/frames.ts: NED_TO_THREE · MODEL_TO_FRD); aquí el modelo no gira,
  // así que es la cámara la que suma esos 90°.
  return { ...LAUNCH_BASE, az: Math.atan2(x, z) + Math.PI / 2, el: Math.asin(y / Math.hypot(x, y, z)), fov: c.fov, dist: metres / (twin?.radiusM ?? 0.4) / fit };
}

let twin = null;
let active = 0;
let override = null, overrideAt = -1;
let flightOn = false;
let introT = 1;     // 0 → dron desarmado; 1 → estado normal
let launching = false;
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
    for (const a of $$('.nav__links a')) a.classList.toggle('is-active', a.getAttribute('href') === '#' + navTarget(active));
  }
  if (override && overrideAt !== active) override = null;
  const chapter = sections[active].dataset.chapter;
  if (override) { Object.assign(s, MODES[override]); if (MODES[override].kind) kind = MODES[override].kind; }
  if (introT < 1) { const k = 1 - Math.pow(1 - introT, 3); for (const name in INTRO_FROM) s[name] = lerp(INTRO_FROM[name], s[name], k); }
  if (launching) Object.assign(s, launchPose());
  s.kind = kind;

  dock.classList.toggle('is-on', DOCK_CHAPTERS.has(chapter) && !!twin && !launching);
  const mode = s.thermal > 0.5 ? 'thermal' : s.sweep > 0.5 ? kind : s.explode > 0.5 ? 'explode' : 'solid';
  for (const b of $$('button', dock)) b.setAttribute('aria-pressed', String(b.dataset.mode === mode));

  // la luz de fondo sigue al dron: es lo que lo separa del negro
  root.style.setProperty('--gx', ((0.5 + s.sx) * 100).toFixed(1) + '%');
  root.style.setProperty('--gy', ((0.5 + s.sy) * 100).toFixed(1) + '%');
  root.style.setProperty('--go', s.o.toFixed(3));
  stage.style.setProperty('--stage-o', s.o.toFixed(3));
  flightOn = s.flight > 0.02;
  if (twin) { twin.setState(s); twin.setVisible(s.o > 0.01 && !portal.covered); }

  const max = root.scrollHeight - vh;
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
const hud = {
  alt: $('#hAlt'), spd: $('#hSpd'), roll: $('#hRoll'), pitch: $('#hPitch'), bat: $('#hBat'), bar: $('#hBatBar'), t: $('#hudT'),
  motors: $$('.motors__x i'), path: $('#hudPath'), done: $('#hudDone'), dot: $('#hudDot'),
};
let flight = null, flightIdx = 0, flightReq = false, hudClock = 0, track = null;
async function loadFlight() {
  if (flightReq) return;
  flightReq = true;
  try {
    flight = await (await fetch('landing/data/flight.json')).json();
    // trayectoria vista desde arriba (norte arriba, este a la derecha), encajada en el recuadro del HUD
    const f = flight.frames;
    const n = f.map((r) => r[3]), e = f.map((r) => r[4]);
    const minN = Math.min(...n), maxN = Math.max(...n), minE = Math.min(...e), maxE = Math.max(...e);
    const scale = 80 / Math.max(maxN - minN, maxE - minE, 1);
    const ox = 50 - ((minE + maxE) / 2) * scale, oy = 50 + ((minN + maxN) / 2) * scale;
    track = f.map((r) => [(ox + r[4] * scale).toFixed(1), (oy - r[3] * scale).toFixed(1)]);
    hud.path.setAttribute('d', 'M' + track.map((p) => p.join(' ')).join('L'));
    twin?.setFlight(flight.frames);
  } catch { flightReq = false; }
}
function paintHud() {
  if (!flight) return;
  const i = Math.floor(flightIdx) % flight.frames.length;
  const f = flight.frames[i];
  hud.alt.textContent = fmt(f[5], 1);
  hud.spd.textContent = fmt(f[6], 1);
  hud.roll.textContent = fmt(f[0], 1);
  hud.pitch.textContent = fmt(f[1], 1);
  hud.bat.textContent = fmt(f[7], 2);
  hud.bar.style.width = (clamp(f[8]) * 100).toFixed(1) + '%';
  const secs = Math.floor(flight.start_s + flightIdx / flight.rate_hz);
  hud.t.textContent = 'T+' + String(Math.floor(secs / 60)).padStart(2, '0') + ':' + String(secs % 60).padStart(2, '0');
  for (const m of hud.motors) m.style.setProperty('--u', clamp(f[9 + +m.dataset.m]).toFixed(3));
  if (track) {
    hud.done.setAttribute('d', 'M' + track.slice(0, i + 1).map((p) => p.join(' ')).join('L'));
    hud.dot.setAttribute('cx', track[i][0]); hud.dot.setAttribute('cy', track[i][1]);
  }
}
let lastTick = performance.now();
function tick(now) {
  requestAnimationFrame(tick);
  const dt = Math.min(0.1, (now - lastTick) / 1000);
  lastTick = now;
  if (!flight || !flightOn || document.hidden || portal.open) return;
  if (!reduced) flightIdx = (flightIdx + dt * flight.rate_hz) % flight.frames.length;
  twin?.setFlightIndex(flightIdx);
  hudClock += dt;
  if (hudClock > 0.1) { hudClock = 0; paintHud(); }
}

// ───────────── entradas y contadores ─────────────
const heroReveals = [];
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
    if (el.closest('.hero')) heroReveals.push(el); else io.observe(el); // el héroe entra cuando termina la intro
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

// ───────────── intro ─────────────
const introBar = $('#introBar'), introPct = $('#introPct');
let loadShown = 0, loadReal = 0, loading = true;
function introProgress() {
  if (!loading) return;
  // avanza con la descarga real y, si el servidor no informa del tamaño, con un avance suave que no llega al final
  loadShown = Math.max(loadShown + (0.9 - loadShown) * 0.012, loadReal * 0.96);
  introBar.style.transform = `scaleX(${loadShown.toFixed(3)})`;
  introPct.textContent = String(Math.round(loadShown * 100)).padStart(3, '0');
  requestAnimationFrame(introProgress);
}
async function endIntro(has3d) {
  loading = false;
  introBar.style.transform = 'scaleX(1)';
  introPct.textContent = '100';
  await wait(Math.max(350, 1500 - (performance.now() - bootTime))); // deja terminar el trazo del isotipo
  const assemble = has3d && !reduced && scrollY < 40;
  root.classList.remove('is-intro');
  if (assemble) {
    const start = performance.now(), dur = 2600;
    const step = (now) => {
      introT = clamp((now - start) / dur);
      compute();
      if (introT < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  } else { introT = 1; compute(); }
  await wait(assemble ? 1100 : 150);
  for (const el of heroReveals) el.classList.add('is-in');
  if (location.hash === '#app' || location.hash === '#legacy') openPortal(location.hash.slice(1), false);
}

// ───────────── portal: la app se abre dentro de la página ─────────────
// Sin corte: la landing apaga su interfaz y lleva la cámara a la vista inicial de la app; la app carga debajo, invisible, y cuando
// avisa de que ya dibujó su primer cuadro (postMessage 'twinsight:ready') se funde encima. El dron está en pantalla todo el tiempo.
const portal = { el: $('#portal'), frame: $('#portalFrame'), label: $('#portalLabel'), close: $('#portalClose'), open: false, covered: false, kind: null, opener: null, token: 0 };
let appReady = null; // resuelve la promesa de «la app ya dibujó»
addEventListener('message', (e) => {
  if (e.origin === location.origin && e.source === portal.frame.contentWindow && e.data?.type === 'twinsight:ready') appReady?.();
});
async function openPortal(kind, push = true) {
  if (portal.open || !LINKS[kind]) return;
  const token = ++portal.token;
  portal.open = true; portal.kind = kind; portal.opener = document.activeElement;
  portal.label.textContent = t(kind === 'app' ? 'portalApp' : 'portalLegacy');
  if (kind === 'app') store.set('twinsight.lang', lang); // la app lee esta misma clave: abre en el mismo idioma
  // Los avisos se escuchan antes de poner la dirección: la app puede terminar de cargar durante la transición.
  const drawn = new Promise((r) => { appReady = r; });
  const loaded = new Promise((r) => portal.frame.addEventListener('load', r, { once: true }));
  portal.el.hidden = false;
  portal.el.classList.remove('is-ready', 'is-closing', 'is-slow');
  portal.frame.src = LINKS[kind];
  if (push) history.pushState({ portal: kind }, '', '#' + kind);
  launching = true;
  root.classList.add('is-launching', 'is-portal');
  compute();
  const slow = setTimeout(() => { if (token === portal.token) portal.el.classList.add('is-slow'); }, 1600);
  // La web app avisa cuando ha dibujado; la versión legacy (Unity) no, así que basta con que cargue su página.
  const ready = kind === 'app' ? Promise.race([drawn, loaded.then(() => wait(6000))]) : loaded.then(() => wait(300));
  await Promise.all([Promise.race([ready, wait(15000)]), wait(reduced ? 0 : 1150)]); // 1150 ms: la cámara ya llegó a la vista de la app
  clearTimeout(slow);
  if (token !== portal.token) return;
  portal.el.classList.add('is-ready');
  await wait(900); // fundido terminado: la app tapa la landing
  if (token !== portal.token) return;
  portal.covered = true;
  twin?.setVisible(false);
  portal.frame.focus();
}
async function closePortal(pop = false) {
  if (!portal.open) return;
  const token = ++portal.token;
  portal.open = false;
  portal.covered = false;
  appReady = null;
  twin?.setVisible(true);
  portal.el.classList.add('is-closing'); // la app se desvanece y deja ver el mismo dron de la landing
  if (!pop && history.state?.portal) history.back();
  else if (!pop) history.replaceState(null, '', location.pathname + location.search);
  await wait(reduced ? 0 : 450);
  if (token !== portal.token) return;
  launching = false;
  root.classList.remove('is-launching', 'is-portal');
  compute();
  portal.opener?.focus?.({ preventScroll: true });
  await wait(200);
  if (token !== portal.token) return;
  portal.frame.src = 'about:blank'; // libera la GPU de la app
  portal.el.hidden = true;
  portal.el.classList.remove('is-ready', 'is-closing', 'is-slow');
}
addEventListener('popstate', () => { if (portal.open) closePortal(true); else if (location.hash === '#app' || location.hash === '#legacy') openPortal(location.hash.slice(1), false); });
portal.close.addEventListener('click', () => closePortal());
document.addEventListener('click', (e) => {
  const opener = e.target.closest('[data-open]');
  if (opener) { e.preventDefault(); openPortal(opener.dataset.open); return; }
  if (e.target.closest('[aria-disabled="true"]')) e.preventDefault();
});
addEventListener('keydown', (e) => { if (e.key === 'Escape' && portal.open) closePortal(); });

// ───────────── interacción ─────────────
for (const b of $$('[data-lang]')) b.addEventListener('click', () => setLang(b.dataset.lang));
for (const b of $$('button', dock)) b.addEventListener('click', () => { override = b.dataset.mode; overrideAt = active; compute(); });
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
  if (e.pointerType !== 'mouse' || e.button !== 0 || !twin || portal.open) return;
  if (e.target.closest('a, button, p, h1, h2, h3, li, dl, aside, article, header, footer, .arch')) return;
  dragX = e.clientX; root.style.cursor = 'grabbing'; e.preventDefault();
});
addEventListener('pointermove', (e) => { if (dragX !== null) { twin.drag((dragX - e.clientX) * 0.006, true); dragX = e.clientX; } });
addEventListener('pointerup', () => { if (dragX !== null) { dragX = null; twin.drag(0, false); root.style.cursor = ''; } });

// ───────────── arranque ─────────────
const query = new URLSearchParams(location.search).get('lang');
applyLinks();
setLang(query || store.get('twinsight.lang') || (/^es/i.test(navigator.language || 'es') ? 'es' : 'en'), !!query);
setupReveals();
introT = reduced || scrollY >= 40 ? 1 : 0;
compute();
requestAnimationFrame(tick);
requestAnimationFrame(introProgress);
new IntersectionObserver((entries, io) => { if (entries.some((e) => e.isIntersecting)) { loadFlight(); io.disconnect(); } }, { rootMargin: '150% 0px' }).observe($('#telemetria'));

async function boot3d() {
  try {
    const { createTwin } = await import('./scene.js');
    twin = await createTwin(stage, {
      modelUrl: 'models/holybro-x500.glb', // el mismo archivo que usa la web app: se descarga una sola vez
      reducedMotion: reduced,
      onProgress: (p) => { loadReal = clamp(p); },
      onAnchors: placeTags,
    });
    if (flight) twin.setFlight(flight.frames);
    compute();
    twin.snap();
    stage.classList.add('is-ready');
    root.classList.add('has-3d');
    endIntro(true);
  } catch (err) {
    console.warn('TwinSight: sin 3D', err);
    root.classList.add('no-3d');
    introT = 1;
    endIntro(false);
  }
}
boot3d();
