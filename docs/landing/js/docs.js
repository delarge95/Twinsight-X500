// Lector de los manuales de TwinSight X500. Solo abre los documentos de esta lista (manuals/<id>.md).
import { marked } from '../vendor/marked.esm.js';

const GROUPS = [
  { es: 'Manuales', en: 'Manuals', docs: [
    ['manual_usuario', 'Manual de usuario', 'User manual'],
    ['manual_tecnico', 'Manual técnico', 'Technical manual'],
    ['07_Guia_Demo', 'Guía de demo', 'Demo guide'],
  ] },
  { es: 'Arquitectura', en: 'Architecture', docs: [
    ['01_Arquitectura_del_Sistema', 'Arquitectura del sistema', 'System architecture'],
    ['02_Referencia_Tecnica_Modulos', 'Referencia técnica de módulos', 'Technical module reference'],
    ['03_Pipeline_Renderizado_Shaders', 'Pipeline de renderizado y shaders', 'Rendering pipeline and shaders'],
    ['08_Sistema_Termico_Hibrido', 'Sistema térmico híbrido', 'Hybrid thermal system'],
    ['06_Assets_Audio', 'Alcance de assets y audio', 'Assets and audio scope'],
  ] },
  { es: 'Despliegue', en: 'Deployment', docs: [
    ['04_Guia_Despliegue', 'Guía de despliegue', 'Deployment guide'],
    ['05_Configuracion_WebGL', 'Configuración WebGL', 'WebGL configuration'],
  ] },
  { es: 'Validación con usuarios', en: 'User validation', docs: [
    ['CUESTIONARIO_SUS', 'Cuestionario SUS', 'SUS questionnaire'],
    ['CUESTIONARIO_NASA_TLX', 'Cuestionario NASA-TLX', 'NASA-TLX questionnaire'],
    ['PROTOCOLO_THINK_ALOUD', 'Protocolo Think Aloud', 'Think Aloud protocol'],
    ['GUIA_TAREAS_VALIDACION', 'Guía de tareas de validación', 'Validation task guide'],
  ] },
];
const TEXT = {
  es: { back: '← Inicio', docs: 'Documentos', loading: 'Cargando…', notice: '', error: 'No se pudo cargar este documento.', title: 'Documentación · TwinSight X500', pdf: 'Informe final (PDF)' },
  en: { back: '← Home', docs: 'Documents', loading: 'Loading…', notice: 'The documentation is written in Spanish.', error: 'This document could not be loaded.', title: 'Documentation · TwinSight X500', pdf: 'Final report (PDF, Spanish)' },
};
const ALL = new Map(GROUPS.flatMap((g) => g.docs.map((d) => [d[0], d])));

const params = new URLSearchParams(location.search);
let lang = params.get('lang') === 'en' ? 'en' : params.get('lang') === 'es' ? 'es' : null;
try { lang ??= localStorage.getItem('twinsight.lang'); } catch { /* sin almacenamiento */ }
lang = lang === 'en' ? 'en' : 'es';
let current = ALL.has(params.get('doc')) ? params.get('doc') : 'manual_usuario';

const $ = (s) => document.querySelector(s);
const toc = $('#toc'), article = $('#doc'), notice = $('#notice'), toggle = $('#toggle');

function renderToc() {
  const t = TEXT[lang];
  toc.innerHTML = '';
  for (const g of GROUPS) {
    const h = document.createElement('h2');
    h.textContent = g[lang];
    toc.append(h);
    for (const [id, es, en] of g.docs) {
      const a = document.createElement('a');
      a.href = `docs.html?doc=${id}&lang=${lang}`;
      a.textContent = lang === 'en' ? en : es;
      if (id === current) a.setAttribute('aria-current', 'page');
      a.addEventListener('click', (e) => {
        if (e.metaKey || e.ctrlKey || e.shiftKey) return;
        e.preventDefault();
        open(id, true);
      });
      toc.append(a);
    }
  }
  const pdf = document.createElement('a');
  pdf.href = 'manuals/informe_final.pdf'; pdf.target = '_blank'; pdf.rel = 'noopener';
  pdf.textContent = t.pdf + ' ↗';
  const h = document.createElement('h2'); h.textContent = 'PDF';
  toc.append(h, pdf);
  $('#back').textContent = t.back;
  $('#back').href = `./?lang=${lang}#docs`;
  $('.brand').href = `./?lang=${lang}`;
  $('#toggleLabel').textContent = t.docs;
  notice.hidden = !t.notice; notice.textContent = t.notice;
  document.documentElement.lang = lang;
  for (const b of document.querySelectorAll('[data-lang]')) b.setAttribute('aria-pressed', String(b.dataset.lang === lang));
}

async function open(id, push) {
  current = id;
  const [, es, en] = ALL.get(id);
  if (push) history.pushState({ id }, '', `docs.html?doc=${id}&lang=${lang}`);
  renderToc();
  toc.classList.remove('is-open'); toggle.setAttribute('aria-expanded', 'false');
  document.title = `${lang === 'en' ? en : es} · TwinSight X500`;
  article.innerHTML = `<p class="reader__loading">${TEXT[lang].loading}</p>`;
  try {
    const res = await fetch(`manuals/${id}.md`);
    if (!res.ok) throw new Error(res.status);
    const md = await res.text();
    if (current !== id) return;
    article.innerHTML = marked.parse(md, { gfm: true });
    polish();
    if (push) scrollTo({ top: 0, behavior: 'instant' });
  } catch (err) {
    article.innerHTML = `<p class="reader__loading">${TEXT[lang].error}</p>`;
    console.warn(err);
  }
}

function polish() {
  // enlaces: los relativos a otros manuales abren en el lector; los externos, en pestaña nueva
  for (const a of article.querySelectorAll('a[href]')) {
    const href = a.getAttribute('href');
    if (/^https?:/i.test(href)) { a.target = '_blank'; a.rel = 'noopener'; continue; }
    if (href.startsWith('#')) continue;
    const m = href.match(/([^/]+)\.md(#.*)?$/i);
    if (m && ALL.has(m[1])) {
      a.href = `docs.html?doc=${m[1]}&lang=${lang}`;
      a.addEventListener('click', (e) => { e.preventDefault(); open(m[1], true); });
    } else a.setAttribute('href', 'manuals/' + href.replace(/^\.\//, ''));
  }
  for (const img of article.querySelectorAll('img[src]')) {
    const src = img.getAttribute('src');
    if (!/^(https?:|data:|\/)/i.test(src)) img.src = 'manuals/' + src.replace(/^\.\//, '');
    img.loading = 'lazy';
  }
  for (const table of article.querySelectorAll('table')) {
    const wrap = document.createElement('div');
    wrap.className = 'table';
    table.replaceWith(wrap);
    wrap.append(table);
  }
  const diagrams = [...article.querySelectorAll('pre > code.language-mermaid')];
  if (diagrams.length) renderMermaid(diagrams);
}

async function renderMermaid(blocks) {
  try {
    const { default: mermaid } = await import('https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs');
    mermaid.initialize({ startOnLoad: false, theme: 'dark', securityLevel: 'strict', themeVariables: { background: '#0b0d11', primaryColor: '#12151b', primaryBorderColor: '#46afff', lineColor: '#8b95a3', fontFamily: 'Space Grotesk, system-ui, sans-serif' } });
    for (const code of blocks) {
      const div = document.createElement('div');
      div.className = 'mermaid';
      div.textContent = code.textContent;
      code.parentElement.replaceWith(div);
    }
    await mermaid.run({ nodes: article.querySelectorAll('.mermaid') });
  } catch (err) { console.warn('Mermaid no disponible; se deja el diagrama como texto.', err); }
}

for (const b of document.querySelectorAll('[data-lang]')) b.addEventListener('click', () => {
  lang = b.dataset.lang;
  try { localStorage.setItem('twinsight.lang', lang); } catch { /* sin almacenamiento */ }
  history.replaceState({ id: current }, '', `docs.html?doc=${current}&lang=${lang}`);
  open(current, false);
});
toggle.addEventListener('click', () => {
  const on = toc.classList.toggle('is-open');
  toggle.setAttribute('aria-expanded', String(on));
});
addEventListener('popstate', () => {
  const id = new URLSearchParams(location.search).get('doc');
  if (ALL.has(id)) open(id, false);
});
open(current, false);
