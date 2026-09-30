/* =====================================================================
   TwinSight · Fase B — 3D sincronizado con los clics (solo en index_final_3d.html)
   Slide 9 (Taxonomía): subsistemas, grupo de tornillería, elementos, hotspots,
   tornillería resaltada y tornillo modular que se arma pieza a pieza.
   Slide 12 (Thermal): simulación por componentes con las ecuaciones y parámetros
   de ThermalSimulationManager (fuentes por carga, conducción s·A/L × material,
   enfriamiento por exposición), sobre el modelo real.
   No añade clics ni cambia el guion: lee los pasos ya revelados en cada slide.
   ===================================================================== */
(function(){
  'use strict';
  if (!window.THREE || !THREE.GLTFLoader || !window.__PRESHOW_GLB) return;

  const slides = Array.from(document.querySelectorAll('.slide'));
  const sTax = slides.find(s => s.dataset.label === 'Taxonomía');
  const sTh = slides.find(s => s.dataset.label === 'Thermal');
  if (!sTax || !sTh) return;

  /* ------------------------------------------------------------------ */
  /* Estilos                                                             */
  /* ------------------------------------------------------------------ */
  const css = document.createElement('style');
  css.textContent = `
  .pb-grid{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1.08fr);gap:clamp(18px,2.2vw,36px);align-items:stretch;margin-top:clamp(8px,1.4vh,16px)}
  .pb-left .band{margin-top:0;grid-template-columns:repeat(3,minmax(0,1fr))}
  .pb-left .band .metric{min-width:0}
  .pb-left .band .tag{white-space:normal;line-height:1.35;letter-spacing:.12em;max-width:100%}
  .pb-left .band .metric{padding:clamp(8px,1.2vh,14px) clamp(10px,1.2vw,18px) 0}
  .pb-left .band .m-value{font-size:clamp(34px,3.4vw,52px)}
  .pb-left .band p{font-size:clamp(12px,.9vw,13.5px)}
  .pb-left .trio{grid-template-columns:1fr;gap:clamp(8px,1.2vh,14px);margin-top:clamp(8px,1.4vh,16px)}
  .pb-left .trio>div{padding-top:clamp(6px,1vh,10px)}
  .pb-left .trio h3{margin:4px 0 2px}
  .pb-left .trio p{font-size:clamp(12.5px,.95vw,14px);line-height:1.45}
  .pb-hide{display:none !important}
  .pb-host{position:relative;min-height:clamp(340px,54vh,600px);border:1px solid var(--line);border-radius:12px;overflow:hidden;
    background:radial-gradient(90% 80% at 55% 45%,#15181e 0%,#0b0c10 60%,#07080a 100%)}
  .pb-host canvas{position:absolute;inset:0;width:100%;height:100%;display:block}
  .pb-cap{position:absolute;left:14px;bottom:12px;right:14px;font:500 clamp(10px,.78vw,12px)/1.5 'JetBrains Mono',monospace;letter-spacing:.12em;text-transform:uppercase;color:#9BA0A6;pointer-events:none;transition:opacity .4s}
  .pb-cap b{color:#C8F53F;font-weight:600}
  .pb-legend{position:absolute;right:12px;top:12px;display:grid;gap:5px;font:500 clamp(9.5px,.72vw,11px)/1 'JetBrains Mono',monospace;letter-spacing:.08em;color:#B4B8BE;pointer-events:none;transition:opacity .5s}
  .pb-legend span{display:flex;align-items:center;gap:7px;justify-content:flex-end}
  .pb-legend i{width:10px;height:10px;border-radius:3px;display:inline-block}
  .pb-labels{position:absolute;inset:0;pointer-events:none}
  .pb-labels span{position:absolute;transform:translate(-50%,-140%);font:500 clamp(9.5px,.72vw,11px)/1.2 'JetBrains Mono',monospace;letter-spacing:.06em;color:#EDEEE8;
    background:rgba(7,8,10,.78);border:1px solid rgba(200,245,63,.35);border-radius:5px;padding:3px 6px;white-space:nowrap;transition:opacity .4s}
  .pb-therm{position:absolute;right:14px;top:14px;bottom:44px;width:12px;border-radius:6px;
    background:linear-gradient(to top,rgb(13,0,89),rgb(255,128,0) 33%,rgb(255,235,20) 66%,#fff);pointer-events:none}
  .pb-therm em{position:absolute;right:18px;font:500 10px 'JetBrains Mono',monospace;color:#9BA0A6;font-style:normal;white-space:nowrap}
  .pb-hud{position:absolute;left:14px;top:12px;font:500 clamp(10px,.78vw,12px)/1.7 'JetBrains Mono',monospace;letter-spacing:.08em;color:#B4B8BE;pointer-events:none}
  .pb-hud b{color:#EDEEE8;font-weight:600}
  .pb-hud .st{color:#C8F53F;text-transform:uppercase;letter-spacing:.16em}
  `;
  document.head.appendChild(css);

  /* ------------------------------------------------------------------ */
  /* DOM: slide 9 en dos columnas, slide 12 con el visor en la derecha    */
  /* ------------------------------------------------------------------ */
  function mkHost(){
    const h = document.createElement('div'); h.className = 'pb-host';
    h.innerHTML = '<div class="pb-labels"></div><div class="pb-cap"></div>';
    return h;
  }
  const taxInner = sTax.querySelector('.slide-inner');
  const band = taxInner.querySelector('.band'), trio = taxInner.querySelector('.trio'), fig = taxInner.querySelector('figure.melt');
  const grid = document.createElement('div'); grid.className = 'pb-grid';
  const left = document.createElement('div'); left.className = 'pb-left';
  band.parentNode.insertBefore(grid, band);
  left.appendChild(band); left.appendChild(trio);
  const hostTax = mkHost();
  const legend = document.createElement('div'); legend.className = 'pb-legend'; hostTax.appendChild(legend);
  grid.appendChild(left); grid.appendChild(hostTax);
  if (fig) fig.classList.add('pb-hide');            // su paso (7) sigue existiendo: lo usa el 3D

  const thPanel = sTh.querySelector('.svg-panel');
  const hostTh = mkHost();
  hostTh.insertAdjacentHTML('beforeend', '<div class="pb-therm"><em style="top:-2px">95 °C</em><em style="top:33%">70</em><em style="top:66%">45</em><em style="bottom:-2px">20 °C</em></div><div class="pb-hud"></div>');
  thPanel.parentNode.insertBefore(hostTh, thPanel);
  thPanel.classList.add('pb-hide');

  const CATS = [
    { name: 'Estructura', color: 0x5b8cff, re: null },
    { name: 'Motores', color: 0xff6b3d, re: /^DJ-2216/i },
    { name: 'Hélices', color: 0xC8F53F, re: /propeller/i },
    { name: 'Batería', color: 0xffb224, re: /battery_PROXY|x500v2_battery|BATTERY-PAD/i },
    { name: 'Potencia', color: 0xff5ca8, re: /PM06|XT60|BM06B/i },
    { name: 'Aviónica', color: 0x3fe0c5, re: /PIXHAWK|gps_m10|GPS|telemetry|GAI-GUANGLIU/i },
    { name: 'Tornillería', color: 0xEDEEE8, re: /^(GB70|LM-|M25-|M3-|NILONG|ZSLM)|_PRIM/i }
  ];
  function catOf(name){
    if (CATS[6].re.test(name)) return 6;
    for (let i = 1; i < 6; i++) if (CATS[i].re.test(name)) return i;
    return 0;
  }
  legend.innerHTML = CATS.map((c, i) => '<span data-c="' + i + '">' + c.name + '<i style="background:#' + c.color.toString(16).padStart(6, '0') + '"></i></span>').join('');

  /* ------------------------------------------------------------------ */
  /* Render compartido (un solo contexto WebGL para las dos slides)       */
  /* ------------------------------------------------------------------ */
  const canvas = document.createElement('canvas');
  const renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true, alpha: true, powerPreference: 'high-performance' });
  renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 1.75));
  renderer.outputEncoding = THREE.sRGBEncoding;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.1;
  renderer.physicallyCorrectLights = true;
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(32, 1, 0.01, 60);

  (function studio(){
    const env = new THREE.Scene();
    const room = new THREE.Mesh(new THREE.BoxGeometry(20, 12, 20), new THREE.MeshBasicMaterial({ color: 0x0d0e11, side: THREE.BackSide }));
    room.position.y = 4; env.add(room);
    const box = (w, h, x, y, z, ry, rx, c) => { const m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshBasicMaterial({ color: c, side: THREE.DoubleSide })); m.position.set(x, y, z); m.rotation.set(rx || 0, ry || 0, 0); env.add(m); };
    box(9, 5, 0, 9.5, 0, 0, Math.PI / 2, new THREE.Color(5, 5, 5));
    box(3.2, 6, -8.5, 4, 3, Math.PI / 2, 0, new THREE.Color(7, 7, 7));
    box(1.2, 8, 8.5, 4, -2, -Math.PI / 2, 0, new THREE.Color(2.6, 2.9, 3.4));
    box(1.2, 8, 3, 4, -9.5, 0, 0, new THREE.Color(3.2, 3.3, 3.4));
    const pm = new THREE.PMREMGenerator(renderer);
    scene.environment = pm.fromScene(env, 0.035).texture; pm.dispose();
  })();
  const key = new THREE.DirectionalLight(0xfff4e8, 2.4); key.position.set(-3.2, 6.5, 3.4); scene.add(key);
  const rim = new THREE.DirectionalLight(0xcfe0ff, 1.6); rim.position.set(2.5, 3, -4.5); scene.add(rim);
  scene.add(new THREE.HemisphereLight(0xdfe6ee, 0x0a0b0e, 0.35));

  const drone = new THREE.Group(); scene.add(drone);
  const meshes = [];                       // { mesh, cat, tex, th, wire, box, center, node }
  let ready = false, S = 1, H = 1;         // S: unidades de escena por metro real
  const target = new THREE.Vector3();

  function parse(dataUri){ return fetch(dataUri).then(r => r.arrayBuffer()).then(buf => new Promise((res, rej) => new THREE.GLTFLoader().parse(buf, '', res, rej))); }

  Promise.all([parse(window.__PRESHOW_GLB), window.__SCREW_GLB ? parse(window.__SCREW_GLB) : Promise.resolve(null)]).then(([g, sg]) => {
    const obj = g.scene;
    const raw = new THREE.Box3().setFromObject(obj).getSize(new THREE.Vector3());
    const orient = new THREE.Group(); orient.add(obj);
    if (raw.z < raw.y && raw.z <= raw.x) orient.rotation.x = -Math.PI / 2;
    else if (raw.x < raw.y && raw.x < raw.z) orient.rotation.z = Math.PI / 2;
    orient.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(orient);
    const size = box.getSize(new THREE.Vector3()), c = box.getCenter(new THREE.Vector3());
    const sc = 2.7 / Math.max(size.x, size.z);
    orient.position.sub(c);
    drone.add(orient); drone.scale.setScalar(sc); drone.position.y = size.y * sc / 2;
    drone.updateMatrixWorld(true);
    S = sc; H = size.y * sc; target.set(0, H * 0.45, 0);
    const wireMat = () => new THREE.MeshBasicMaterial({ color: 0xC8F53F, wireframe: true, transparent: true, opacity: 0, depthWrite: false });
    const list = []; obj.traverse(o => { if (o.isMesh) list.push(o); });
    list.forEach(o => {
      const cat = catOf(o.name);
      const tex = o.material.clone();
      if (tex.name === 'X500_Atlas'){ tex.metalness = 0.32; tex.roughness = 1.0; tex.envMapIntensity = 1.1; }
      tex.emissive = new THREE.Color(0x000000); tex.transparent = true; tex.opacity = 1;
      const th = new THREE.MeshStandardMaterial({ color: 0x0d0059, roughness: 0.55, metalness: 0.08, emissive: 0x000000, envMapIntensity: 0.6 });
      o.material = tex;
      const wire = new THREE.Mesh(o.geometry, wireMat()); wire.visible = false; o.add(wire);
      const b = new THREE.Box3().setFromObject(o);
      meshes.push({ mesh: o, cat: cat, tex: tex, th: th, wire: wire, box: b, center: b.getCenter(new THREE.Vector3()), name: o.name });
    });
    buildHotspots();
    buildThermal();
    if (sg) buildScrew(sg.scene);
    ready = true;
  }).catch(e => console.warn('[phaseB]', e));

  /* ------------------------------------------------------------------ */
  /* Hotspots (paso 4 de la slide 9)                                     */
  /* ------------------------------------------------------------------ */
  const hotspots = [];
  const ringTex = (() => {
    const cv = document.createElement('canvas'); cv.width = cv.height = 128; const x = cv.getContext('2d');
    x.strokeStyle = '#C8F53F'; x.lineWidth = 7; x.beginPath(); x.arc(64, 64, 44, 0, Math.PI * 2); x.stroke();
    x.fillStyle = '#C8F53F'; x.beginPath(); x.arc(64, 64, 13, 0, Math.PI * 2); x.fill();
    const t = new THREE.CanvasTexture(cv); t.encoding = THREE.sRGBEncoding; return t;
  })();
  function buildHotspots(){
    const pick = re => meshes.filter(m => re.test(m.name)).map(m => m.center.clone());
    const pts = [].concat(pick(/^DJ-2216/), pick(/gps_m10/).slice(0, 1), pick(/MIANKE-PIXHAWK/).slice(0, 1), pick(/battery_PROXY|x500v2_battery/).slice(0, 1));
    pts.forEach((p, i) => {
      const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: ringTex, transparent: true, opacity: 0, depthTest: false }));
      sp.position.copy(p).add(new THREE.Vector3(0, 0.12, 0)); sp.scale.setScalar(0.16); sp.renderOrder = 10;
      scene.add(sp); hotspots.push({ sprite: sp, phase: i * 0.7 });
    });
  }

  /* ------------------------------------------------------------------ */
  /* Tornillo modular (pasos 6 y 7 de la slide 9)                        */
  /* ------------------------------------------------------------------ */
  const screw = { ok: false };
  function buildScrew(sgScene){
    const pieces = {};
    sgScene.updateMatrixWorld(true);
    sgScene.traverse(o => { if (o.isMesh) pieces[o.name.replace(/\.\d+$/, '')] = o; });
    const need = ['mod_head_ding', 'mod_head_pan', 'mod_head_chen', 'mod_thread_turn', 'mod_thread_end'];
    if (!need.every(n => pieces[n])) return;
    /* geometría de cada pieza centrada, con el eje del tornillo en +Y */
    const geo = {};
    need.forEach(n => {
      const m = pieces[n];
      const g = m.geometry.clone(); g.applyMatrix4(m.matrixWorld);
      g.computeBoundingBox(); const bb = g.boundingBox, cc = bb.getCenter(new THREE.Vector3());
      g.translate(-cc.x, -cc.y, -cc.z); g.computeBoundingBox();
      const sz = g.boundingBox.getSize(new THREE.Vector3());
      geo[n] = { g: g, h: sz.y, w: Math.max(sz.x, sz.z) };
    });
    /* el tornillo del brazo (M3 × 38) más visible */
    const cand = meshes.filter(m => /GB70-M3-38/i.test(m.name));
    if (!cand.length) return;
    cand.sort((a, b) => (b.center.x + b.center.z) - (a.center.x + a.center.z));
    const prox = cand[0];
    const bs = prox.box.getSize(new THREE.Vector3());
    const axisIdx = bs.x >= bs.y && bs.x >= bs.z ? 0 : (bs.y >= bs.z ? 1 : 2);
    const axis = new THREE.Vector3(axisIdx === 0 ? 1 : 0, axisIdx === 1 ? 1 : 0, axisIdx === 2 ? 1 : 0);
    const L = bs.getComponent(axisIdx);
    const dHead = Math.max(bs.getComponent((axisIdx + 1) % 3), bs.getComponent((axisIdx + 2) % 3));
    /* ¿en qué extremo está la cabeza? el extremo con más radio en la malla proxy */
    const pos = prox.mesh.geometry.attributes.position, v = new THREE.Vector3();
    const c0 = prox.center, ends = [0, 0];
    for (let i = 0; i < pos.count; i += 2){
      v.fromBufferAttribute(pos, i).applyMatrix4(prox.mesh.matrixWorld);
      const t = v.clone().sub(c0).dot(axis) / (L / 2);
      const rad = v.clone().sub(c0).sub(axis.clone().multiplyScalar(v.clone().sub(c0).dot(axis))).length();
      if (t > 0.8) ends[1] = Math.max(ends[1], rad); else if (t < -0.8) ends[0] = Math.max(ends[0], rad);
    }
    const dir = axis.clone().multiplyScalar(ends[1] >= ends[0] ? 1 : -1);   // de la punta hacia la cabeza
    const tipPt = c0.clone().addScaledVector(dir, -L / 2);
    const q = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
    const steel = new THREE.MeshStandardMaterial({ color: 0x8c9199, metalness: 0.95, roughness: 0.28, envMapIntensity: 1.5, transparent: true, opacity: 0 });
    const shaftD = dHead * 0.56, headD = dHead;
    const headH = L * 0.08, tipH = L * 0.035;
    const shaftLen = L - headH - tipH;
    const pitch = 0.0005 * S;                       // M3: 0,5 mm de paso
    const N = Math.max(2, Math.min(56, Math.round(shaftLen / pitch)));
    const seg = shaftLen / N;
    const grp = new THREE.Group(); scene.add(grp);
    function part(n, len, diam, at){
      const G = geo[n];
      const m = new THREE.Mesh(G.g, steel.clone());
      m.scale.set(diam / G.w, len / G.h, diam / G.w);
      m.quaternion.copy(q);
      m.userData.home = tipPt.clone().addScaledVector(dir, at);
      m.position.copy(m.userData.home);
      m.renderOrder = 5;
      grp.add(m); return m;
    }
    const tip = part('mod_thread_end', tipH, shaftD, tipH / 2);
    const turns = [];
    for (let i = 0; i < N; i++) turns.push(part('mod_thread_turn', seg * 0.98, shaftD, tipH + seg * (i + 0.5)));
    const head = part('mod_head_ding', headH, headD, tipH + shaftLen + headH / 2);
    /* piezas base para el paso 7: en fila, junto al tornillo */
    const lib = ['mod_head_ding', 'mod_head_chen', 'mod_head_pan', 'mod_thread_turn', 'mod_thread_end'];
    const labels = ['Cabeza cilíndrica', 'Cabeza avellanada', 'Cabeza redondeada', 'Vuelta de rosca', 'Punta'];
    const libMeshes = lib.map((n, i) => {
      const G = geo[n], k = (headD * 1.6) / Math.max(G.w, G.h * 1.2);
      const m = new THREE.Mesh(G.g, steel.clone()); m.scale.setScalar(k); m.quaternion.copy(q);
      m.visible = false; grp.add(m); return m;
    });
    Object.assign(screw, { ok: true, prox: prox, grp: grp, tip: tip, turns: turns, head: head, dir: dir, center: c0.clone(), L: L,
                           headD: headD, lib: libMeshes, libLabels: labels, N: N });
  }

  /* ------------------------------------------------------------------ */
  /* Simulación térmica (slide 12)                                       */
  /* Nodos = piezas canónicas, con los valores de sus DronePartData      */
  /* (thermalHover, thermalPeak, thermalWarmupSeconds, thermalExposure,  */
  /* thermalSourceWeight) y las reglas de ThermalSimulationManager.      */
  /* ------------------------------------------------------------------ */
  const AMB = 20, IDLE_LOAD = 0.2, HOVER_LOAD = 0.45, ACCEL = 3.5, DEF_COOL = 0.08;
  /* id, patrón de malla, fuente de calor, hover, pico, τ, exposición, peso, escala de conductividad del material */
  const PARTS = [
    ['motor',      /^DJ-2216/i,                                              true,  55, 95, 10, 0.80, 1.0,  1.8 ],  // aluminio + cobre
    ['pixhawk6c',  /PIXHAWK/i,                                               true,  42, 60,  6, 0.45, 0.5,  0.4 ],
    ['power',      /PM06|BM06B|XT60/i,                                       true,  35, 55,  3, 0.55, 0.5,  0.4 ],
    ['battery',    /x500v2_battery/i,                                        true,  35, 55, 15, 0.35, 0.8,  0.18],
    ['gps',        /gps_m10|GPS/i,                                           true,  28, 40,  2, 0.55, 0.2,  0.65],
    ['telemetry',  /telemetry/i,                                             true,  32, 45,  2, 0.55, 0.2,  0.4 ],
    ['prop',       /propeller/i,                                             false, 22, 25,  0, 0.95, 0.08, 0.2 ],
    ['arm',        /HMX5V|BAN-DJ-DIAN/i,                                     false, 30, 45,  8, 0.80, 0.2,  0.65],
    ['top',        /TOP-PLATE/i,                                             false, 28, 38,  0, 0.55, 0.2,  0.65],
    ['bottom',     /BOTTOM-PLATE/i,                                          false, 25, 35,  0, 0.55, 0.2,  0.65],
    ['rails',      /BATTERY-MOUNTING|BATTERY-PAD|PYLONS|TUBE300|JIA-GUAN|HUAN-GUIJIAO|PLATFORM|ZHIJIA-CAMERA/i, false, 22, 25, 0, 0.55, 0.2, 0.65],
    ['landing',    /CARBON-FIBER-TUBE|GUAN-CHENG|JIAO-EVA|JIAO-LIANJIE|JIA-LIANJIE|MAO-JIAO/i, false, 22, 25, 0, 0.95, 0.2, 0.65],
    ['fastener',   /_PRIM|^(GB70|LM-|M25-|M3-|ZSLM|NILONG)/i,                false, 22, 26, 60, 0.55, 0.2,  1.0 ]
  ];
  const nodes = [], links = [], nodeById = new Map();
  function quadrant(c){ return (c.z >= 0 ? 'F' : 'B') + (c.x >= 0 ? 'R' : 'L'); }
  function buildThermal(){
    meshes.forEach(m => {
      const def = PARTS.find(p => p[1].test(m.name));
      let id, P;
      if (def){
        P = { src: def[2], hover: def[3], peak: def[4], tau: def[5] || (def[2] ? 12 : 20), exposure: def[6], w: def[7], k: def[8] };
        /* motores, brazos y hélices: un nodo por brazo, como en la app; tornillería: un nodo por pieza */
        id = def[0] === 'fastener' ? 'f:' + m.name : (/motor|arm|prop/.test(def[0]) ? def[0] + '_' + quadrant(m.center) : def[0]);
        if (def[0] === 'prop'){ P.hover = Math.min(Math.max(P.hover, 21.5), 24); P.peak = Math.min(Math.max(P.peak, 25), 32); }
      } else {
        id = 'p:' + m.name; P = { src: false, hover: 22, peak: 60, tau: 12, exposure: 0.55, w: 0.2, k: 0.65 };
      }
      if (P.src && def && def[0] === 'battery') P.hover = Math.max(P.hover, 35);
      P.cool = def && def[0] === 'prop' ? 0.34 : DEF_COOL * (0.75 + (1.5 - 0.75) * P.exposure);
      let n = nodeById.get(id);
      if (!n){ n = { id: id, T: AMB, P: P, meshes: [] }; nodeById.set(id, n); nodes.push(n); }
      n.meshes.push(m); m.node = n;
    });
    /* enlaces: contactos por cajas envolventes entre mallas de nodos distintos (criterio de la auditoría de contactos) */
    const e = 0.0015 * S, tmp = new THREE.Box3(), inter = new THREE.Box3(), sz = new THREE.Vector3(), acc = new Map();
    for (let i = 0; i < meshes.length; i++){
      tmp.copy(meshes[i].box).expandByScalar(e);
      for (let j = i + 1; j < meshes.length; j++){
        const a = meshes[i].node, b = meshes[j].node;
        if (a === b || !tmp.intersectsBox(meshes[j].box)) continue;
        inter.copy(tmp).intersect(meshes[j].box.clone().expandByScalar(e)); inter.getSize(sz);
        const d = [sz.x, sz.y, sz.z].sort((x, y) => y - x);
        const Acm2 = (d[0] / S * 100) * (d[1] / S * 100);
        const Lmm = Math.max(1, meshes[i].center.distanceTo(meshes[j].center) / S * 1000);
        const key = a.id < b.id ? a.id + '|' + b.id : b.id + '|' + a.id;
        const cur = acc.get(key) || { a: a, b: b, g: 0 };
        cur.g += Acm2 / Lmm;                                   // Ĝ = s·A/L acumulado sobre los contactos del par
        acc.set(key, cur);
      }
    }
    acc.forEach(({ a, b, g }) => {
      const matScale = 0.5 * (a.P.k + b.P.k);                 // promedio de escalas de material, como TryAddLink
      links.push([a, b, Math.min(0.09, Math.max(0.006, 0.02 * g * matScale))]);   // rango de la app: 0,006–0,09
    });
  }
  function loadAt(ts){                 // estados de DroneStateController; al final, el control de carga al 80 %
    if (ts < 1.5) return { load: Math.max(0.1, IDLE_LOAD * 0.5), label: 'Arranque' };
    if (ts < 4) return { load: IDLE_LOAD, label: 'Reposo · motores armados' };
    if (ts < 12) return { load: HOVER_LOAD, label: 'Vuelo estacionario' };
    return { load: 0.8, label: 'Vuelo · carga 80 %' };
  }
  const sm01 = x => { x = Math.max(0, Math.min(1, x)); return x * x * (3 - 2 * x); };
  function equilibrium(P, load){
    if (!P.src || load <= 0) return AMB;
    if (load <= HOVER_LOAD) return AMB + (P.hover - AMB) * sm01(load / HOVER_LOAD);
    return P.hover + (P.peak - P.hover) * sm01((load - HOVER_LOAD) / (1 - HOVER_LOAD));
  }
  function stepThermal(dt, load){
    const d = new Map();
    for (const n of nodes){
      const P = n.P;
      let dT = (equilibrium(P, load) - n.T) * (1 - Math.exp(-dt / Math.max(P.tau, 0.2))) * P.w;
      dT += (AMB - n.T) * P.cool * P.exposure * dt;
      d.set(n, dT);
    }
    for (const [a, b, G] of links){ const q = (b.T - a.T) * G * dt; d.set(a, d.get(a) + q); d.set(b, d.get(b) - q); }
    for (const n of nodes) n.T = Math.min(Math.max(n.T + d.get(n), AMB), Math.max(n.P.peak + 15, AMB + 5));
  }
  /* rampa de la leyenda de la app: índigo → naranja → amarillo → blanco */
  const C0 = new THREE.Color(0.05, 0.0, 0.35), C1 = new THREE.Color(1, 0.5, 0), C2 = new THREE.Color(1, 0.92, 0.08), C3 = new THREE.Color(1, 1, 1);
  const _c = new THREE.Color();
  function ramp(t, out){
    t = Math.max(0, Math.min(1, t));
    if (t < 0.33) return out.copy(C0).lerp(C1, t / 0.33);
    if (t < 0.66) return out.copy(C1).lerp(C2, (t - 0.33) / 0.33);
    return out.copy(C2).lerp(C3, (t - 0.66) / 0.34);
  }
  function resetThermal(){ nodes.forEach(n => { n.T = AMB; }); }

  /* ------------------------------------------------------------------ */
  /* Utilidades                                                          */
  /* ------------------------------------------------------------------ */
  function shownSteps(slide){
    const set = new Set();
    slide.querySelectorAll('[data-step].shown').forEach(el => set.add(+el.dataset.step));
    let k = 0; while (set.has(k + 1)) k++;
    return k;
  }
  const lerp = (a, b, t) => a + (b - a) * t;
  const camPos = new THREE.Vector3(), camLook = new THREE.Vector3(), wantPos = new THREE.Vector3(), wantLook = new THREE.Vector3();
  function orbitPose(theta, phi, r, tg){
    wantLook.copy(tg);
    wantPos.set(tg.x + r * Math.sin(phi) * Math.sin(theta), tg.y + r * Math.cos(phi), tg.z + r * Math.sin(phi) * Math.cos(theta));
  }
  function setCap(host, html){ const c = host.querySelector('.pb-cap'); if (c.innerHTML !== html) c.innerHTML = html; }
  function fit(host){
    const r = host.getBoundingClientRect();
    const w = Math.max(2, Math.round(r.width)), h = Math.max(2, Math.round(r.height));
    if (canvas.parentNode !== host) host.insertBefore(canvas, host.firstChild);
    if (canvas._w !== w || canvas._h !== h){ renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); canvas._w = w; canvas._h = h; }
  }

  /* ------------------------------------------------------------------ */
  /* Bucle                                                               */
  /* ------------------------------------------------------------------ */
  let mode = null, last = performance.now(), t = 0, stepPrev = -1, stepT = 0, thT = 0, thRunning = false, snap = true;
  function setMode(next){
    mode = next; snap = true; stepPrev = -1;
    meshes.forEach(m => {
      m.mesh.material = next === 'th' ? m.th : m.tex;
      m.wire.visible = false;
    });
    hotspots.forEach(h => { h.sprite.visible = next === 'tax'; });
    if (screw.ok) screw.grp.visible = next === 'tax';
    if (next === 'th'){ resetThermal(); thRunning = false; thT = 0; }
  }

  function updateTax(dt, step){
    if (step !== stepPrev){ stepPrev = step; stepT = 0; }
    stepT += dt;
    /* tinte por subsistema, grupo de tornillería, alambre, fantasma */
    const tint = step >= 1 && step <= 3 ? (step === 3 ? 0.14 : 0.32) : 0;
    const fastGlow = step === 2 ? 0.9 + 0.3 * Math.sin(t * 4) : 0;
    const ghost = step >= 5;
    meshes.forEach(m => {
      const mat = m.tex;
      const col = CATS[m.cat].color;
      let em = m.cat === 0 ? tint * 0.35 : tint;
      if (m.cat === 6 && step === 2) em = fastGlow;
      if (step === 5) em = m.cat === 6 ? 0.8 : 0;
      if (step >= 6) em = m.cat === 6 ? 0.25 : 0;
      const tgtCol = em > 0 ? _c.setHex(m.cat === 6 && step >= 5 ? 0xffb224 : col).multiplyScalar(em) : _c.setRGB(0, 0, 0);
      mat.emissive.lerp(tgtCol, Math.min(1, dt * 5));
      const op = ghost ? (m.cat !== 6 ? (step >= 6 ? 0.05 : 0.2) : (step >= 6 ? 0.05 : 1)) : 1;
      mat.opacity = lerp(mat.opacity, op, Math.min(1, dt * 4));
      mat.depthWrite = mat.opacity > 0.9;
      if (m.cat === 6 && step >= 6 && screw.ok && m === screw.prox) mat.opacity = lerp(mat.opacity, 0, Math.min(1, dt * 6));
      const wOn = step === 3;
      m.wire.visible = wOn || m.wire.material.opacity > 0.01;
      m.wire.material.opacity = lerp(m.wire.material.opacity, wOn ? 0.22 : 0, Math.min(1, dt * 5));
    });
    legend.style.opacity = step >= 1 && step <= 3 ? 1 : 0;
    legend.querySelectorAll('span').forEach(s => { s.style.opacity = (step === 2 && s.dataset.c !== '6') ? 0.35 : 1; });
    hotspots.forEach((h, i) => {
      const on = step === 4;
      const o = h.sprite.material;
      o.opacity = lerp(o.opacity, on ? 1 : 0, Math.min(1, dt * 5));
      h.sprite.scale.setScalar(0.22 + 0.05 * Math.sin(t * 3 + h.phase));
    });
    /* tornillo modular */
    const lab = hostTax.querySelector('.pb-labels');
    if (screw.ok){
      const a = step >= 6 ? stepT + (step >= 7 ? 99 : 0) : -1;     // en el paso 7 el tornillo ya está armado
      const show = (m, t0, from) => {
        const k = a < 0 ? 0 : sm01((a - t0) / 0.35);
        m.material.opacity = k;
        m.visible = k > 0.001;
        m.position.copy(m.userData.home).addScaledVector(screw.dir, (1 - k) * from);
      };
      show(screw.tip, 0.4, -screw.L * 0.25);
      screw.turns.forEach((m, i) => show(m, 0.6 + i * 0.03, -screw.L * 0.3));
      show(screw.head, 0.8 + screw.N * 0.03, screw.L * 0.5);
      /* piezas base (paso 7) */
      const camR = new THREE.Vector3().setFromMatrixColumn(camera.matrixWorld, 0);
      const camU = new THREE.Vector3().setFromMatrixColumn(camera.matrixWorld, 1);
      let html = '';
      screw.lib.forEach((m, i) => {
        const on = step >= 7;
        m.visible = on;
        if (!on) return;
        const k = sm01((stepT - i * 0.12) / 0.5);
        m.material.opacity = k;
        const p = screw.center.clone().addScaledVector(camR, screw.headD * (3.0 + i * 2.6)).addScaledVector(camU, screw.L * 0.12);
        m.position.copy(p);
        m.rotation.y += dt * 0.6;
        const up = i % 2 === 0;
        const sp = p.clone().addScaledVector(camU, screw.headD * (up ? 1.5 : -1.7)).project(camera);
        const x = (sp.x * 0.5 + 0.5) * 100, y = (-sp.y * 0.5 + 0.5) * 100;
        html += '<span style="left:' + x.toFixed(2) + '%;top:' + y.toFixed(2) + '%;opacity:' + k.toFixed(2) + ';transform:translate(-50%,' + (up ? '-120%' : '20%') + ')">' + screw.libLabels[i] + '</span>';
      });
      if (lab.innerHTML !== html) lab.innerHTML = html;
    } else lab.innerHTML = '';
    /* cámara */
    if (step >= 6 && screw.ok){
      const tg = screw.center.clone().addScaledVector(screw.dir, screw.L * 0.05);
      const off = new THREE.Vector3(1, 0.35, 0.9).normalize();
      const r = screw.L * (step >= 7 ? 4.8 : 2.6);
      wantLook.copy(tg).add(step >= 7 ? new THREE.Vector3().setFromMatrixColumn(camera.matrixWorld, 0).multiplyScalar(screw.headD * 6.7) : new THREE.Vector3());
      const ang = step >= 7 ? 0 : t * 0.08;
      wantPos.copy(tg).add(off.applyAxisAngle(new THREE.Vector3(0, 1, 0), ang).multiplyScalar(r));
    } else {
      orbitPose(0.6 + t * 0.06, step === 4 ? 1.02 : 1.12, 5.4, target);
    }
    const caps = [
      '<b>28</b> piezas · <b>7</b> subsistemas en el modelo real',
      'Subsistemas por color · <b>28</b> piezas con ficha',
      'Grupo de tornillería · <b>30</b> anclas en escena',
      '<b>257</b> elementos que se dibujan o se tocan',
      'Hotspots · un toque selecciona el grupo',
      'Tornillería · <b>425 208 → 14 408</b> triángulos',
      screw.ok ? 'Tornillo armado en la app · cabeza + <b>' + screw.N + '</b> vueltas + punta' : 'Tornillo modular',
      '<b>5</b> piezas base arman cualquier tornillo'
    ];
    setCap(hostTax, caps[Math.min(step, caps.length - 1)]);
  }

  function updateTh(dt, step){
    if (step >= 1 && !thRunning){ thRunning = true; thT = 0; }
    if (step < 1 && thRunning){ thRunning = false; resetThermal(); }
    const hud = hostTh.querySelector('.pb-hud');
    if (thRunning){
      thT += dt;
      const st = loadAt(thT);
      const sub = 4, h = (dt * ACCEL) / sub;
      for (let i = 0; i < sub; i++) stepThermal(h, st.load);
      const avg = re => { const g = meshes.filter(m => re.test(m.name)); return g.length ? g.reduce((a, m) => a + m.node.T, 0) / g.length : AMB; };
      const html = '<span class="st">' + st.label + '</span><br>Motores <b>' + avg(/^DJ-2216/).toFixed(0) + ' °C</b><br>Controladora <b>' + avg(/PIXHAWK/).toFixed(0) +
        ' °C</b><br>Batería <b>' + avg(/x500v2_battery/).toFixed(0) + ' °C</b><br>Brazos <b>' + avg(/HMX5V|BAN-DJ-DIAN/).toFixed(0) + ' °C</b><br><span style="color:#767C85">+' + Math.round(thT * ACCEL) + ' s simulados</span>';
      if (hud.innerHTML !== html) hud.innerHTML = html;
    } else {
      const html = '<span class="st">Apagado</span><br>Todo a ' + AMB + ' °C';
      if (hud.innerHTML !== html) hud.innerHTML = html;
    }
    meshes.forEach(m => {
      ramp((m.node.T - AMB) / (95 - AMB), _c);
      m.th.color.copy(_c);
      m.th.emissive.copy(_c).multiplyScalar(0.28);
    });
    setCap(hostTh, step >= 2 ? 'Tiempo acelerado · sin calibrar · <b>°C del modelo</b>' : 'Ecuaciones y parámetros de ThermalSimulationManager');
    orbitPose(-0.9 + t * 0.05, 1.08, 5.2, target);
  }

  function frame(now){
    requestAnimationFrame(frame);
    const dt = Math.min(0.05, (now - last) / 1000); last = now;
    if (!ready || window.__preshowOpen) return;
    const cur = document.querySelector('.slide.active');
    const next = cur === sTax ? 'tax' : cur === sTh ? 'th' : null;
    if (next !== mode) setMode(next);
    if (!mode) return;
    t += dt;
    const host = mode === 'tax' ? hostTax : hostTh;
    fit(host);
    const step = shownSteps(mode === 'tax' ? sTax : sTh);
    if (mode === 'tax') updateTax(dt, step); else updateTh(dt, step);
    const near = mode === 'tax' && step >= 6 ? 0.004 : 0.02;
    if (camera.near !== near){ camera.near = near; camera.updateProjectionMatrix(); }
    if (snap){ camPos.copy(wantPos); camLook.copy(wantLook); snap = false; }
    const k = Math.min(1, dt * 2.2);
    camPos.lerp(wantPos, k); camLook.lerp(wantLook, k);
    camera.position.copy(camPos); camera.lookAt(camLook);
    renderer.render(scene, camera);
  }
  requestAnimationFrame(frame);
})();
