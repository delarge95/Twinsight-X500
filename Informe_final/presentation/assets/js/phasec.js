/* =====================================================================
   TwinSight · Fase C — integraciones ilustrativas (solo en index_final_3d.html)
   Slide 2  Problema     : tres vistas de plano vs el dron en 3D (motor marcado).
   Slide 4  Alcance      : flujo de datos físico ↔ digital (qué existe y qué no).
   Slide 8  Arquitectura : el aviso de selección viajando por el EventBus.
   Slide 10 Flujo        : la interfaz real de la app en un teléfono (estilos de Theme.uss,
                           íconos de ProceduralIcons, textos de DronePartData).
   Slide 14 Rendimiento  : el mismo dron girando a los FPS medidos en cada equipo.
   Usa los clics existentes; no cambia el guion.
   ===================================================================== */
(function(){
  'use strict';
  const PB = window.__PB;
  if (!PB) return;
  const THREE = PB.THREE, ICON = window.__APP_ICONS || {};
  const slides = Array.from(document.querySelectorAll('.slide'));
  const bySlide = l => slides.find(s => s.dataset.label === l);
  const sm = x => { x = Math.max(0, Math.min(1, x)); return x * x * (3 - 2 * x); };
  const lerp = (a, b, t) => a + (b - a) * t;

  const css = document.createElement('style');
  css.textContent = `
  /* ---------- común ---------- */
  .pc-grid{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:clamp(18px,2.4vw,40px);align-items:center;margin-top:clamp(8px,1.4vh,16px)}
  .pc-host{position:relative;border:1px solid var(--line);border-radius:12px;overflow:hidden;background:radial-gradient(90% 80% at 55% 45%,#15181e 0%,#0b0c10 60%,#07080a 100%)}
  .pc-host>canvas{position:absolute;inset:0;width:100%;height:100%;display:block}
  .pc-hide{display:none !important}
  .pc-cap{font:500 clamp(10px,.78vw,12px)/1.5 'JetBrains Mono',monospace;letter-spacing:.12em;text-transform:uppercase;color:#9BA0A6}
  .pc-cap b{color:#C8F53F;font-weight:600}

  /* ---------- slide 10: teléfono con la interfaz de la app ---------- */
  .pc-flow-left .flow-wrap{margin-top:0}
  .pc-flow-left .duo-cols{grid-template-columns:1fr;gap:clamp(8px,1.4vh,14px)}
  .pc-phone-wrap{display:flex;justify-content:center;align-items:center}
  .pc-phone{position:relative;height:clamp(420px,66vh,660px);aspect-ratio:9/16;border-radius:clamp(24px,2.6vw,36px);border:1px solid rgba(237,238,232,.18);
    background:#000;box-shadow:0 30px 70px -30px rgba(0,0,0,.9),inset 0 0 0 6px #0b0c10;overflow:hidden}
  .pc-scr{position:absolute;left:6px;top:6px;width:360px;height:640px;transform-origin:0 0;overflow:hidden;border-radius:20px;
    background:radial-gradient(75% 55% at 50% 45%,#1c1e25 0%,#0b0c10 55%,#000 100%);font-family:Inter,'Space Grotesk',system-ui,sans-serif;color:#fff;--u:.3335px}
  .pc-scr>canvas{position:absolute;inset:0;width:100%;height:100%}
  .pc-scr svg{width:100%;height:100%;display:block}
  .ap-top{position:absolute;left:0;right:0;top:calc(var(--u)*42);height:calc(var(--u)*56);display:flex;align-items:center;justify-content:center}
  .ap-title{font-size:calc(var(--u)*20);font-weight:700;letter-spacing:calc(var(--u)*4);color:rgba(255,255,255,.65);transition:color .35s}
  .ap-title.sel{color:rgb(15,186,128)}
  .ap-round{position:absolute;top:50%;width:calc(var(--u)*64);height:calc(var(--u)*64);margin-top:calc(var(--u)*-32);border-radius:50%;border:1px solid rgba(255,255,255,.1);
    display:grid;place-items:center;color:rgba(255,255,255,.8)}
  .ap-round i{width:calc(var(--u)*30);height:calc(var(--u)*30);display:block}
  .ap-round.l{left:calc(var(--u)*24)} .ap-round.r{right:calc(var(--u)*24)}
  .ap-bottom{position:absolute;left:0;right:0;bottom:0;display:flex;flex-direction:column;align-items:center;transition:transform .45s cubic-bezier(.2,.8,.2,1)}
  .ap-sub{display:flex;flex-direction:column;align-items:center;margin-bottom:calc(var(--u)*8);transition:opacity .3s,transform .3s;opacity:0;transform:translateY(calc(var(--u)*12));height:0}
  .ap-sub.on{opacity:1;transform:none;height:auto}
  .ap-subt{font-size:calc(var(--u)*16);font-weight:700;letter-spacing:calc(var(--u)*2);color:rgba(255,255,255,.85);margin-bottom:calc(var(--u)*10)}
  .ap-cards{display:flex}
  .ap-card{width:calc(var(--u)*148);height:calc(var(--u)*148);margin:calc(var(--u)*4);border-radius:calc(var(--u)*16);border:1px solid rgba(255,255,255,.1);
    display:flex;flex-direction:column;align-items:center;justify-content:center;color:rgba(255,255,255,.8);transition:border-color .2s,background-color .2s}
  .ap-card i{width:calc(var(--u)*48);height:calc(var(--u)*48);display:block;margin-bottom:calc(var(--u)*14)}
  .ap-card b{font-size:calc(var(--u)*15);font-weight:500;letter-spacing:calc(var(--u)*1);color:rgba(255,255,255,.7)}
  .ap-card.act{border-color:rgb(70,175,255);color:rgb(70,175,255)} .ap-card.act b{color:rgb(70,175,255);font-weight:700}
  .ap-pill{display:flex;align-items:center;justify-content:center;border:1px solid rgba(255,255,255,.3);border-radius:calc(var(--u)*56);padding:0 calc(var(--u)*48);
    height:calc(var(--u)*112);margin-bottom:calc(var(--u)*8);background:rgba(0,0,0,.35);transition:box-shadow .5s}
  .ap-pill.pulse{box-shadow:0 0 0 calc(var(--u)*6) rgba(70,175,255,.25),0 0 calc(var(--u)*40) rgba(70,175,255,.35)}
  .ap-mode{width:calc(var(--u)*120);height:calc(var(--u)*96);margin:0 calc(var(--u)*12);display:flex;flex-direction:column;align-items:center;justify-content:center;color:rgba(255,255,255,.8)}
  .ap-mode i{width:calc(var(--u)*56);height:calc(var(--u)*56);display:block;margin-bottom:calc(var(--u)*8)}
  .ap-mode b{font-size:calc(var(--u)*18);font-weight:700;letter-spacing:calc(var(--u)*1);color:rgba(255,255,255,.65)}
  .ap-mode.act{color:rgb(70,175,255)} .ap-mode.act b{color:rgba(70,175,255,.9)}
  .ap-peek{width:calc(var(--u)*320);height:calc(var(--u)*68);background:rgba(0,0,0,.96);border:1px solid rgba(255,255,255,.08);border-bottom:0;
    border-radius:calc(var(--u)*16) calc(var(--u)*16) 0 0;display:flex;flex-direction:column;align-items:center;justify-content:center;transition:opacity .3s,transform .3s;opacity:0;transform:translateY(100%)}
  .ap-peek.on{opacity:1;transform:none}
  .ap-peek i{width:calc(var(--u)*72);height:calc(var(--u)*5);background:rgba(255,255,255,.2);border-radius:3px;margin-bottom:calc(var(--u)*6)}
  .ap-peek b{font-size:calc(var(--u)*18);font-weight:700;letter-spacing:calc(var(--u)*2);color:rgba(255,255,255,.4)}
  .ap-sheet{position:absolute;left:0;right:0;bottom:0;height:55%;background:rgba(0,0,0,.82);border-top:1px solid rgba(255,255,255,.1);
    border-radius:calc(var(--u)*24) calc(var(--u)*24) 0 0;padding:calc(var(--u)*80) calc(var(--u)*24) calc(var(--u)*40);box-sizing:border-box;
    transform:translateY(105%);opacity:0;transition:transform .45s cubic-bezier(.2,.8,.2,1),opacity .3s}
  .ap-sheet.on{transform:none;opacity:1}
  .ap-handle{position:absolute;left:50%;top:calc(var(--u)*24);width:calc(var(--u)*112);height:calc(var(--u)*28);margin-left:calc(var(--u)*-56);border-radius:calc(var(--u)*14);
    background:rgba(255,255,255,.12);border:1px solid rgba(255,255,255,.1)}
  .ap-close{position:absolute;top:calc(var(--u)*24);right:calc(var(--u)*24);width:calc(var(--u)*64);height:calc(var(--u)*64);border-radius:50%;border:1px solid rgba(255,255,255,.1);
    display:grid;place-items:center;color:rgba(255,255,255,.6)} .ap-close i{width:calc(var(--u)*26);height:calc(var(--u)*26);display:block}
  .ap-stitle{font-family:'Space Grotesk',sans-serif;font-size:calc(var(--u)*28);letter-spacing:calc(var(--u)*-.5);color:#fff}
  .ap-desc{font-size:calc(var(--u)*17);line-height:1.4;color:rgba(255,255,255,.45);margin:calc(var(--u)*8) calc(var(--u)*48) calc(var(--u)*20) 0}
  .ap-fold{display:flex;justify-content:space-between;align-items:center;min-height:calc(var(--u)*44);border-top:1px solid rgba(255,255,255,.06);
    font-size:calc(var(--u)*13);font-weight:700;letter-spacing:calc(var(--u)*4);color:rgba(255,255,255,.35)}
  .ap-fold:first-of-type{border-top:0}
  .ap-fold::after{content:"";width:0;height:0;border-left:calc(var(--u)*7) solid rgba(255,255,255,.4);border-top:calc(var(--u)*5) solid transparent;border-bottom:calc(var(--u)*5) solid transparent}
  .ap-hero{position:absolute;inset:0;background:rgb(5,5,8);display:flex;flex-direction:column;align-items:center;justify-content:center;transition:opacity .6s}
  .ap-hero.off{opacity:0;pointer-events:none}
  .ap-hero .ht{position:relative;font-family:'Space Grotesk',sans-serif;font-weight:600;font-size:calc(var(--u)*84);line-height:calc(var(--u)*92);letter-spacing:calc(var(--u)*-2);text-align:center;margin-bottom:calc(var(--u)*24)}
  .ap-hero .ht span{position:absolute;inset:0;opacity:.85}
  .ap-hero .ht .c{color:rgba(0,240,255,.65);transform:translateX(calc(var(--u)*-2))} .ap-hero .ht .r{color:rgba(255,0,85,.65);transform:translateX(calc(var(--u)*2))}
  .ap-hero .ht .m{position:relative;color:#fff}
  .ap-hero .hs{font-size:calc(var(--u)*22);letter-spacing:calc(var(--u)*1);color:rgba(255,255,255,.65);margin-bottom:calc(var(--u)*48)}
  .ap-hero .hb{border-radius:100px;border:1px solid rgba(255,255,255,.35);background:rgba(255,255,255,.04);padding:calc(var(--u)*14) calc(var(--u)*44);
    font-size:calc(var(--u)*13);letter-spacing:calc(var(--u)*3);margin-bottom:calc(var(--u)*12)}
  .ap-tap{position:absolute;width:calc(var(--u)*90);height:calc(var(--u)*90);margin:calc(var(--u)*-45) 0 0 calc(var(--u)*-45);border-radius:50%;
    border:calc(var(--u)*5) solid rgba(255,255,255,.9);opacity:0;pointer-events:none}
  .ap-tap.go{animation:apTap .9s ease-out}
  @keyframes apTap{0%{opacity:.95;transform:scale(.35)}100%{opacity:0;transform:scale(1.4)}}

  /* ---------- slide 8: pulso por el bus de eventos ---------- */
  .pc-arch{position:absolute;inset:0;pointer-events:none;overflow:visible}
  .chip.pc-lit{border-color:#C8F53F !important;color:#C8F53F !important;box-shadow:0 0 18px rgba(200,245,63,.35);transition:all .25s}
  .pc-badge{position:absolute;transform:translate(-50%,-135%);font:600 clamp(9.5px,.72vw,11px)/1.3 'JetBrains Mono',monospace;letter-spacing:.06em;color:#07080a;
    background:#C8F53F;border-radius:5px;padding:3px 7px;white-space:nowrap;opacity:0;transition:opacity .3s}
  .pc-badge.on{opacity:1}

  /* ---------- slide 2: planos vs 3D ---------- */
  .pc-prob-host{height:clamp(300px,50vh,540px)}
  .pc-prob-lbl{position:absolute;font:500 clamp(9.5px,.72vw,11px)/1 'JetBrains Mono',monospace;letter-spacing:.18em;text-transform:uppercase;color:#767C85;pointer-events:none}
  .pc-prob-frame{position:absolute;border:1px dashed rgba(237,238,232,.12);border-radius:8px;pointer-events:none}
  .pc-ring{position:absolute;width:26px;height:26px;margin:-13px 0 0 -13px;border:2px solid #C8F53F;border-radius:50%;box-shadow:0 0 14px rgba(200,245,63,.5);opacity:0;transition:opacity .35s;pointer-events:none}
  .pc-ring.on{opacity:1;animation:pcRing 1.4s ease-in-out infinite}
  @keyframes pcRing{50%{transform:scale(1.25)}}
  .pc-prob-q{position:absolute;left:0;right:0;bottom:12px;text-align:center;pointer-events:none;transition:opacity .4s}

  /* ---------- slide 14: fluidez por equipo ---------- */
  .pc-fps{display:grid;grid-template-columns:repeat(4,1fr);gap:clamp(6px,.8vw,12px);margin-top:clamp(8px,1.2vh,14px)}
  .pc-fps figure{margin:0;border:1px solid var(--line);border-radius:8px;overflow:hidden;background:#0b0c10;position:relative}
  .pc-fps canvas{display:block;width:100%;aspect-ratio:4/3}
  .pc-fps figcaption{position:absolute;left:6px;bottom:5px;font:600 clamp(9.5px,.72vw,11px)/1 'JetBrains Mono',monospace;letter-spacing:.06em}

  /* ---------- slide 4: flujo de datos ---------- */
  .pc-scope{margin:clamp(6px,1vh,12px) 0 clamp(2px,.6vh,8px)}
  .pc-scope svg{width:100%;height:auto;max-height:clamp(96px,15vh,150px);display:block}
  .pc-scope .dim{opacity:.28;transition:opacity .5s} .pc-scope .x{opacity:0;transition:opacity .5s} .pc-scope.s1 .x{opacity:1}
  .pc-scope .flow{stroke-dasharray:6 8;animation:pcDash 1.1s linear infinite} @keyframes pcDash{to{stroke-dashoffset:-28}}
  .pc-scope .ok{transition:opacity .5s;opacity:.35} .pc-scope.s2 .ok{opacity:1}
  `;
  document.head.appendChild(css);

  /* =================================================================== */
  /* Slide 10 · Flujo con la interfaz de la app                          */
  /* =================================================================== */
  (function flow(){
    const s = bySlide('Flujo público'); if (!s) return;
    const inner = s.querySelector('.slide-inner');
    const fw = inner.querySelector('.flow-wrap'), dc = inner.querySelector('.duo-cols'), fig = inner.querySelector('figure.melt');
    const grid = document.createElement('div'); grid.className = 'pc-grid';
    const left = document.createElement('div'); left.className = 'pc-flow-left';
    fw.parentNode.insertBefore(grid, fw); left.appendChild(fw); left.appendChild(dc);
    const wrap = document.createElement('div'); wrap.className = 'pc-phone-wrap';
    const ic = n => '<i>' + (ICON[n] || '') + '</i>';
    wrap.innerHTML = '<div class="pc-phone"><div class="pc-scr">' +
      '<div class="ap-top"><div class="ap-round l">' + ic('home') + '</div><div class="ap-title">SELECT A PART</div><div class="ap-round r">' + ic('reset') + '</div></div>' +
      '<div class="ap-sheet"><div class="ap-handle"></div><div class="ap-close">' + ic('close') + '</div>' +
        '<div class="ap-stitle">Motor 2216 KV920 FL</div>' +
        '<div class="ap-desc">Brushless outrunner 2216 motor, KV920. Supports LiPo 3-4S and 1045 propellers. Approximate 4S current range: 3.5-16.2 A, peak power about 240 W.</div>' +
        ['IDENTIFICATION', 'SPECIFICATIONS', 'PARENT ASSEMBLY', 'ASSEMBLY', 'KEY & REFERENCES'].map(x => '<div class="ap-fold">' + x + '</div>').join('') + '</div>' +
      '<div class="ap-bottom">' +
        '<div class="ap-sub" data-sub="inspect"><div class="ap-subt">INSPECT</div><div class="ap-cards"><div class="ap-card">' + ic('pins') + '<b>PINS</b></div><div class="ap-card">' + ic('isolate') + '<b>ISOLATE</b></div><div class="ap-card">' + ic('power') + '<b>POWER</b></div></div></div>' +
        '<div class="ap-sub" data-sub="analyze"><div class="ap-subt">ANALYZE</div><div class="ap-cards"><div class="ap-card">' + ic('cut') + '<b>CUT</b></div><div class="ap-card">' + ic('explode') + '<b>EXPLODE</b></div><div class="ap-card">' + ic('filter') + '<b>FILTER</b></div></div></div>' +
        '<div class="ap-pill"><div class="ap-mode" data-m="inspect">' + ic('inspect') + '<b>INSPECT</b></div><div class="ap-mode" data-m="analyze">' + ic('analyze') + '<b>ANALYZE</b></div><div class="ap-mode" data-m="studio">' + ic('studio') + '<b>STUDIO</b></div></div>' +
        '<div class="ap-peek"><i></i><b>PART INFO</b></div>' +
      '</div>' +
      '<div class="ap-hero"><div class="ht"><span class="c">TwinSight<br>X500</span><span class="r">TwinSight<br>X500</span><span class="m">TwinSight<br>X500</span></div>' +
        '<div class="hs">Visor WebGL visual-semántico</div><div class="hb">ABRIR VISOR</div><div class="hb">DEVICE</div><div class="hb">CONFIG</div></div>' +
      '<div class="ap-tap"></div>' +
      '</div></div>';
    grid.appendChild(left); grid.appendChild(wrap);
    if (fig) fig.classList.add('pc-hide');
    const phone = wrap.querySelector('.pc-phone'), scr = wrap.querySelector('.pc-scr');
    const $ = q => scr.querySelector(q);
    const title = $('.ap-title'), sheet = $('.ap-sheet'), bottom = $('.ap-bottom'), peek = $('.ap-peek'), hero = $('.ap-hero'), tap = $('.ap-tap'), pill = $('.ap-pill');
    function fitScreen(){
      const r = phone.getBoundingClientRect(), z = r.width / phone.offsetWidth || 1;   // la slide puede tener zoom
      const k = (phone.offsetHeight - 12) / 640;
      scr.style.transform = 'scale(' + k + ')'; scr.style.width = ((phone.offsetWidth - 12) / k) + 'px';
    }
    function tapAt(x, y){ tap.style.left = x + 'px'; tap.style.top = y + 'px'; tap.classList.remove('go'); void tap.offsetWidth; tap.classList.add('go'); }
    function tapEl(el){ const a = el.getBoundingClientRect(), b = scr.getBoundingClientRect(), k = b.width / scr.offsetWidth; tapAt((a.left + a.width / 2 - b.left) / k, (a.top + a.height / 2 - b.top) / k); }
    let motor = null, prevStep = -1, stT = 0, selected = false;
    const selCol = new THREE.Color(0.42, 0.82, 1.0);
    function motorScreen(){
      if (!motor) return null;
      const p = motor.center.clone().project(PB.camera);
      return { x: (p.x * 0.5 + 0.5) * scr.offsetWidth, y: (-p.y * 0.5 + 0.5) * 640 };
    }
    function setMode(m){ scr.querySelectorAll('.ap-mode').forEach(e => e.classList.toggle('act', e.dataset.m === m)); scr.querySelectorAll('.ap-sub').forEach(e => e.classList.toggle('on', e.dataset.sub === m)); }
    PB.register({
      slide: s, host: scr,
      enter(){ fitScreen(); motor = PB.meshes.find(m => /^DJ-2216/.test(m.name) && m.center.x < 0 && m.center.z > 0) || PB.meshes.find(m => /^DJ-2216/.test(m.name)); prevStep = -1; PB.snap(); },
      exit(){ if (motor) motor.tex.emissive.setRGB(0, 0, 0); },
      update(dt, step, t){
        fitScreen();
        if (step !== prevStep){ prevStep = step; stT = 0; }
        stT += dt;
        hero.classList.toggle('off', step >= 1);
        /* estado por paso */
        const wantSel = step === 2 || step === 3 || step === 4 || step >= 6;
        const sheetOn = step === 3;
        if (step === 2 && stT < 0.05){ const p = motorScreen(); if (p) tapAt(p.x, p.y); }
        if (step === 6 && stT < 0.05){ const p = motorScreen(); if (p) tapAt(p.x, p.y); }
        if (step === 5 && stT < 0.05) tapAt(scr.offsetWidth * 0.78, 640 * 0.3);
        if (step === 4 && stT > 0.9 && stT < 0.95) tapEl(scr.querySelector('.ap-mode[data-m="inspect"]'));
        if (step === 7 && stT < 0.05) tapEl(scr.querySelector('.ap-mode[data-m="analyze"]'));
        selected = wantSel && (step !== 2 || stT > 0.25);
        title.textContent = selected ? 'MOTOR 2216 KV920 FL' : 'SELECT A PART';
        title.classList.toggle('sel', selected);
        sheet.classList.toggle('on', sheetOn);
        peek.classList.toggle('on', selected && !sheetOn);
        bottom.style.transform = sheetOn ? 'translateY(' + (-(640 * 0.55) + 44) + 'px)' : 'none';
        pill.classList.toggle('pulse', step === 4 && stT < 0.9);
        setMode(step >= 7 ? 'analyze' : (step >= 4 && (step > 4 || stT > 0.95)) ? 'inspect' : null);
        if (motor) motor.tex.emissive.lerp(selected ? selCol.clone().multiplyScalar(0.85) : new THREE.Color(0, 0, 0), Math.min(1, dt * 6));
        /* cámara: dron pequeño como en la app; se acerca un poco con la ficha abierta */
        PB.orbitPose(0.7 + t * 0.12, 1.05, sheetOn ? 11.5 : 9.2, sheetOn ? PB.target.clone().add(new THREE.Vector3(0, -2.1, 0)) : PB.target);
      }
    });
  })();

  /* =================================================================== */
  /* Slide 8 · El aviso viajando por el EventBus                         */
  /* =================================================================== */
  (function arch(){
    const s = bySlide('Arquitectura'); if (!s) return;
    const stack = s.querySelector('.stack'); if (!stack) return;
    stack.style.position = 'relative';
    const chip = txt => Array.from(stack.querySelectorAll('.chip')).find(c => c.textContent.trim().startsWith(txt));
    /* la tornillería detallada la arma FastenerInspectionManager: se muestra en la capa de escena */
    const sceneChips = stack.querySelectorAll('.layer')[2].querySelector('.chips');
    const fim = document.createElement('span'); fim.className = 'chip'; fim.textContent = 'FastenerInspectionManager'; sceneChips.appendChild(fim);
    const svgNS = 'http://www.w3.org/2000/svg';
    const svg = document.createElementNS(svgNS, 'svg'); svg.setAttribute('class', 'pc-arch'); stack.appendChild(svg);
    const badges = {};
    const route = [
      { from: null, to: 'SelectionManager', at: 0.0, badge: 'toque en una pieza' },
      { from: 'SelectionManager', to: 'EventBus', at: 0.55, badge: 'PartSelectedEvent' },
      { from: 'EventBus', to: 'UIDetailsSheet', at: 1.25, badge: 'abre la ficha' },
      { from: 'EventBus', to: 'HotspotManager', at: 1.25, badge: 'limpia el resaltado' },
      { from: 'EventBus', to: 'FastenerInspectionManager', at: 1.25, badge: 'arma el tornillo' }
    ];
    route.forEach(r => { const b = document.createElement('div'); b.className = 'pc-badge'; b.textContent = r.badge; stack.appendChild(b); badges[r.to] = b; });
    function center(el){
      const a = el.getBoundingClientRect(), b = stack.getBoundingClientRect(), z = b.width / stack.offsetWidth || 1;
      return { x: (a.left + a.width / 2 - b.left) / z, y: (a.top + a.height / 2 - b.top) / z, top: (a.top - b.top) / z };
    }
    let dots = [], tLoop = 0, active = false;
    function clear(){ svg.innerHTML = ''; dots = []; stack.querySelectorAll('.chip.pc-lit').forEach(c => c.classList.remove('pc-lit')); Object.values(badges).forEach(b => b.classList.remove('on')); }
    PB.register({
      slide: s, dom: true,
      enter(){ clear(); tLoop = 0; },
      exit(){ clear(); },
      update(dt, step){
        if (step < 1){ if (active){ clear(); active = false; } return; }
        active = true;
        tLoop += dt;
        const T = tLoop % 5.2;                                  // el recorrido se repite mientras se explica
        if (T < dt * 1.5) clear();
        route.forEach(r => {
          const to = chip(r.to); if (!to) return;
          const c2 = center(to);
          const k = sm((T - r.at) / 0.6);
          if (r.from){
            const c1 = center(chip(r.from));
            let path = svg.querySelector('[data-r="' + r.to + '"]');
            if (!path){
              path = document.createElementNS(svgNS, 'path'); path.setAttribute('data-r', r.to);
              path.setAttribute('fill', 'none'); path.setAttribute('stroke', '#C8F53F'); path.setAttribute('stroke-width', '1.6'); path.setAttribute('stroke-dasharray', '4 5');
              svg.appendChild(path);
              const dot = document.createElementNS(svgNS, 'circle'); dot.setAttribute('r', '5'); dot.setAttribute('fill', '#C8F53F'); dot.setAttribute('data-d', r.to); svg.appendChild(dot);
            }
            const mx = (c1.x + c2.x) / 2, my = Math.min(c1.y, c2.y) - 26;
            path.setAttribute('d', 'M' + c1.x + ' ' + c1.y + ' Q' + mx + ' ' + my + ' ' + c2.x + ' ' + c2.y);
            path.style.opacity = T > r.at ? 0.55 : 0;
            const dot = svg.querySelector('[data-d="' + r.to + '"]');
            const q = (1 - k) * (1 - k), w = 2 * (1 - k) * k, e = k * k;
            dot.setAttribute('cx', q * c1.x + w * mx + e * c2.x); dot.setAttribute('cy', q * c1.y + w * my + e * c2.y);
            dot.style.opacity = T > r.at && k < 1 ? 1 : 0;
          }
          const lit = T > r.at + 0.6 && T < 4.9;
          to.classList.toggle('pc-lit', lit);
          const b = badges[r.to]; b.classList.toggle('on', lit);
          b.style.left = c2.x + 'px'; b.style.top = c2.top + 'px';
        });
      }
    });
  })();

  /* =================================================================== */
  /* Slide 2 · Tres vistas de plano vs el dron en 3D                     */
  /* =================================================================== */
  (function prob(){
    const s = bySlide('Problema'); if (!s) return;
    const inner = s.querySelector('.slide-inner');
    const rows = inner.querySelector('.rows'), key = inner.querySelector('.callout');
    const grid = document.createElement('div'); grid.className = 'pc-grid';
    rows.parentNode.insertBefore(grid, rows);
    const left = document.createElement('div'); left.appendChild(rows);
    const host = document.createElement('div'); host.className = 'pc-host pc-prob-host';
    host.innerHTML = ['PLANTA', 'ALZADO', 'PERFIL', '3D'].map(l => '<div class="pc-prob-frame" data-f="' + l + '"></div><div class="pc-prob-lbl" data-l="' + l + '">' + l + '</div>').join('') +
      '<div class="pc-prob-q pc-cap"></div>' + ['PLANTA', 'ALZADO', 'PERFIL', '3D'].map(k => '<div class="pc-ring" data-ring="' + k + '"></div>').join('');
    grid.appendChild(left); grid.appendChild(host);
    /* vistas: izquierda 2 × 2 (planta arriba, alzado y perfil abajo), derecha 3D */
    const V = { PLANTA: [0.02, 0.52, 0.27, 0.44], ALZADO: [0.02, 0.05, 0.27, 0.44], PERFIL: [0.31, 0.05, 0.27, 0.44], '3D': [0.61, 0.05, 0.37, 0.91] };
    Object.keys(V).forEach(k => {
      const v = V[k], f = host.querySelector('[data-f="' + k + '"]'), l = host.querySelector('[data-l="' + k + '"]');
      f.style.left = v[0] * 100 + '%'; f.style.width = v[2] * 100 + '%'; f.style.bottom = v[1] * 100 + '%'; f.style.height = v[3] * 100 + '%';
      l.style.left = (v[0] * 100 + 1.2) + '%'; l.style.bottom = (v[1] + v[3]) * 100 - 5 + '%';
    });
    const lines = new THREE.Group(), hi = new THREE.Group();
    let built = false, motorMeshes = [];
    function buildLines(){
      if (built) return; built = true;
      const lm = new THREE.LineBasicMaterial({ color: 0x9fb3c8, transparent: true, opacity: 0.55 });
      const hm = new THREE.LineBasicMaterial({ color: 0xC8F53F });
      motorMeshes = PB.meshes.filter(m => /^DJ-2216/.test(m.name) && m.center.x < 0 && m.center.z > 0);
      PB.meshes.forEach(m => {
        if (/_PRIM/.test(m.name)) return;                                     // la tornillería satura el plano
        const eg = new THREE.EdgesGeometry(m.mesh.geometry, 28);
        const isMotor = motorMeshes.includes(m);
        const ls = new THREE.LineSegments(eg, isMotor ? hm : lm);
        m.mesh.updateMatrixWorld(true);
        ls.matrixAutoUpdate = false; ls.matrix.copy(m.mesh.matrixWorld);
        (isMotor ? hi : lines).add(ls);
      });
      PB.scene.add(lines); PB.scene.add(hi); lines.visible = hi.visible = false;
    }
    (function warm(){ if (PB.ready()) setTimeout(buildLines, 2500); else setTimeout(warm, 500); })();
    const ortho = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 50);
    const persp = new THREE.PerspectiveCamera(30, 1, 0.05, 60);
    let prev = -1, stT = 0, t3 = 0;
    PB.register({
      slide: s, host: host,
      enter(){ buildLines(); prev = -1; },
      exit(){ lines.visible = hi.visible = false; PB.meshes.forEach(m => m.tex.emissive.setRGB(0, 0, 0)); },
      update(dt, step){
        if (step !== prev){ prev = step; stT = 0; }
        stT += dt; t3 += dt;
        const q = host.querySelector('.pc-prob-q');
        const txt = ['', 'Planos, manuales y listas: la información está repartida',
          '¿Dónde está el motor? <b>Tres vistas</b>, un solo objeto que hay que armar en la cabeza',
          'En 3D, <b>el mismo motor</b> aparece una sola vez, en su lugar', 'Un puente entre los datos y la comprensión del conjunto'][Math.min(step, 4)];
        if (q.innerHTML !== txt) q.innerHTML = txt;
        host.querySelectorAll('[data-f="3D"],[data-l="3D"]').forEach(e => e.style.opacity = step >= 3 ? 1 : 0.15);
        host.querySelectorAll('.pc-prob-frame:not([data-f="3D"]),.pc-prob-lbl:not([data-l="3D"])').forEach(e => e.style.opacity = step >= 1 ? 1 : 0.4);
      },
      render(){
        const R = PB.renderer, W = PB.canvas._w, Hh = PB.canvas._h;
        const step = PB.shownSteps(s);
        R.setScissorTest(true);
        R.setClearColor(0x000000, 0); R.clear();
        const box = new THREE.Box3().setFromObject(PB.drone), c = box.getCenter(new THREE.Vector3()), sz = box.getSize(new THREE.Vector3());
        const mc = motorMeshes.length ? motorMeshes[0].center : null;
        const view = (k, cam) => {
          const v = V[k]; R.setViewport(v[0] * W, v[1] * Hh, v[2] * W, v[3] * Hh); R.setScissor(v[0] * W, v[1] * Hh, v[2] * W, v[3] * Hh); R.render(PB.scene, cam);
          const ring = host.querySelector('[data-ring="' + k + '"]');
          const on = mc && (k === '3D' ? step >= 3 : step >= 2);
          ring.classList.toggle('on', !!on);
          if (on){ cam.updateMatrixWorld(); const p = mc.clone().project(cam); ring.style.left = ((v[0] + (p.x * 0.5 + 0.5) * v[2]) * 100) + '%'; ring.style.top = ((1 - v[1] - (p.y * 0.5 + 0.5) * v[3]) * 100) + '%'; }
        };
        /* planos: solo líneas */
        PB.drone.visible = false; lines.visible = true; hi.visible = step >= 2;
        const pulse = step >= 2 ? 0.6 + 0.4 * Math.sin(t3 * 5) : 1;
        hi.children.forEach(l => { l.material.opacity = pulse; l.material.transparent = true; });
        const setOrtho = (asp, halfW) => { const hw = halfW, hh = hw / asp; ortho.left = -hw; ortho.right = hw; ortho.top = hh; ortho.bottom = -hh; ortho.updateProjectionMatrix(); };
        const aspV = (V.PLANTA[2] * W) / (V.PLANTA[3] * Hh);
        setOrtho(aspV, Math.max(sz.x, sz.z) * 0.62); ortho.position.set(c.x, c.y + 10, c.z); ortho.up.set(0, 0, -1); ortho.lookAt(c); view('PLANTA', ortho);
        setOrtho(aspV, Math.max(sz.x, sz.y) * 0.62); ortho.up.set(0, 1, 0); ortho.position.set(c.x, c.y, c.z + 10); ortho.lookAt(c); view('ALZADO', ortho);
        ortho.position.set(c.x + 10, c.y, c.z); ortho.lookAt(c); view('PERFIL', ortho);
        /* 3D: el dron real con el motor resaltado como en la app */
        lines.visible = hi.visible = false; PB.drone.visible = true;
        if (step >= 3){
          PB.meshes.forEach(m => m.tex.emissive.setRGB(0, 0, 0));
          motorMeshes.forEach(m => m.tex.emissive.setRGB(0.42 * 0.9, 0.82 * 0.9, 0.9));
          const v = V['3D']; persp.aspect = (v[2] * W) / (v[3] * Hh); persp.updateProjectionMatrix();
          const th = 0.8 + t3 * 0.18, r = 10.5;
          persp.position.set(PB.target.x + r * Math.sin(1.1) * Math.sin(th), PB.target.y + r * Math.cos(1.1), PB.target.z + r * Math.sin(1.1) * Math.cos(th));
          persp.lookAt(PB.target); view('3D', persp);
        }
        R.setScissorTest(false); R.setViewport(0, 0, W, Hh);
      }
    });
  })();

  /* =================================================================== */
  /* Slide 14 · El mismo dron a los FPS medidos                          */
  /* =================================================================== */
  (function perf(){
    const s = bySlide('Rendimiento'); if (!s) return;
    const panel = s.querySelector('.svg-panel'); if (!panel) return;
    const DEV = [['Escritorio', 59.8, '#C8F53F'], ['iPhone 17 Pro', 58.7, '#C8F53F'], ['Redmi Note 10S', 26.5, '#FFB224'], ['Adreno 610', 17.6, '#FF5C5C']];
    const row = document.createElement('div'); row.className = 'pc-fps';
    row.innerHTML = DEV.map(d => '<figure><canvas width="320" height="240"></canvas><figcaption style="color:' + d[2] + '">' + String(d[1]).replace('.', ',') + ' FPS</figcaption></figure>').join('');
    panel.appendChild(row);
    const cvs = Array.from(row.querySelectorAll('canvas')).map(c => c.getContext('2d'));
    const last = DEV.map(() => -1);
    const host = document.createElement('div'); host.style.cssText = 'position:absolute;width:320px;height:240px;left:-9999px;top:0';
    document.body.appendChild(host);
    const cam = new THREE.PerspectiveCamera(30, 4 / 3, 0.05, 60);
    let clock = 0;
    PB.register({
      slide: s, host: null,
      enter(){ last.fill(-1); },
      exit(){ PB.drone.rotation.y = 0; },
      update(dt){ clock += dt; },
      render(){
        if (PB.shownSteps(s) < 1) return;
        const R = PB.renderer;
        if (PB.canvas.parentNode !== host){ host.appendChild(PB.canvas); PB.canvas._w = 0; }
        if (PB.canvas._w !== 320){ R.setSize(320, 240, false); PB.canvas._w = 320; PB.canvas._h = 240; }
        R.setScissorTest(false); R.setClearColor(0x0b0c10, 1);
        const r = 4.1;
        cam.position.set(PB.target.x + r * Math.sin(1.12) * Math.sin(0.7), PB.target.y + r * Math.cos(1.12), PB.target.z + r * Math.sin(1.12) * Math.cos(0.7));
        cam.lookAt(PB.target);
        DEV.forEach((d, i) => {
          const frameIdx = Math.floor(clock * d[1]);             // un cuadro nuevo cada 1/FPS; entre cuadros, la imagen se queda quieta
          if (frameIdx === last[i]) return;
          last[i] = frameIdx;
          PB.drone.rotation.y = (frameIdx / d[1]) * 1.4;          // misma velocidad de giro para todos
          R.render(PB.scene, cam);
          cvs[i].drawImage(PB.canvas, 0, 0, 320, 240);
        });
        R.setClearColor(0x000000, 0);
      }
    });
  })();

  /* =================================================================== */
  /* Slide 4 · Qué datos fluyen en cada nivel                            */
  /* =================================================================== */
  (function scope(){
    const s = bySlide('Alcance'); if (!s) return;
    const inner = s.querySelector('.slide-inner'), sc = inner.querySelector('.scope');
    const box = document.createElement('div'); box.className = 'pc-scope';
    box.innerHTML = `<svg viewBox="0 0 900 150" xmlns="http://www.w3.org/2000/svg" font-family="JetBrains Mono,monospace">
      <defs><marker id="pcA" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto"><path d="M0,0L0,6L7,3z" fill="#C8F53F"/></marker>
        <marker id="pcG" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto"><path d="M0,0L0,6L7,3z" fill="#767C85"/></marker></defs>
      <rect x="10" y="40" width="170" height="70" rx="10" fill="rgba(255,255,255,.02)" stroke="rgba(237,238,232,.3)"/>
      <text x="95" y="70" text-anchor="middle" fill="#EDEEE8" font-size="14" font-weight="600">Dron físico</text><text x="95" y="90" text-anchor="middle" fill="#767C85" font-size="11">Holybro X500 V2</text>
      <rect x="720" y="40" width="170" height="70" rx="10" fill="rgba(200,245,63,.05)" stroke="#C8F53F"/>
      <text x="805" y="70" text-anchor="middle" fill="#C8F53F" font-size="14" font-weight="600">Modelo digital</text><text x="805" y="90" text-anchor="middle" fill="#767C85" font-size="11">TwinSight X500</text>
      <g class="ok"><path class="flow" d="M180 52 C 380 30, 520 30, 720 52" fill="none" stroke="#C8F53F" stroke-width="2.2" marker-end="url(#pcA)"/>
        <text x="450" y="16" text-anchor="middle" fill="#C8F53F" font-size="12" font-weight="600">DISEÑO CAD → forma, piezas y datos · VISUAL PRODUCT TWIN</text></g>
      <g class="dim"><path d="M180 78 L 720 78" fill="none" stroke="#767C85" stroke-width="1.6" stroke-dasharray="3 6" marker-end="url(#pcG)"/>
        <text x="450" y="72" text-anchor="middle" fill="#9BA0A6" font-size="11.5">telemetría en vivo → DIGITAL SHADOW</text></g>
      <g class="dim"><path d="M720 104 L 180 104" fill="none" stroke="#767C85" stroke-width="1.6" stroke-dasharray="3 6" marker-end="url(#pcG)"/>
        <text x="450" y="126" text-anchor="middle" fill="#9BA0A6" font-size="11.5">← decisiones y control · DIGITAL TWIN</text></g>
      <g class="x" stroke="#FF5C5C" stroke-width="2.4" stroke-linecap="round"><path d="M598 70 l12 12 M610 70 l-12 12"/><path d="M598 96 l12 12 M610 96 l-12 12"/></g>
    </svg>`;
    sc.parentNode.insertBefore(box, sc);
    PB.register({
      slide: s, dom: true,
      update(dt, step){ box.classList.toggle('s1', step >= 1); box.classList.toggle('s2', step >= 2); }
    });
  })();
  /* =================================================================== */
  /* Slide 7 · Antes y después de una pieza (motor DJ-2216)              */
  /* Antes: el motor en la importación teselada (optimizing6.fbx, 23 402 */
  /* triángulos). Después: el motor del activo final (2 720) con el bake. */
  /* =================================================================== */
  (function pipe(){
    const s = bySlide('Pipeline'); if (!s || !window.__MOTOR_CAD_GLB) return;
    const inner = s.querySelector('.slide-inner'), dc = inner.querySelector('.duo-cols');
    const grid = document.createElement('div'); grid.className = 'pc-grid'; grid.style.alignItems = 'stretch';
    dc.parentNode.insertBefore(grid, dc);
    const left = document.createElement('div'); left.appendChild(dc);
    dc.style.gridTemplateColumns = '1fr'; dc.style.gap = 'clamp(8px,1.4vh,14px)'; dc.style.marginTop = '0';
    const host = document.createElement('div'); host.className = 'pc-host'; host.style.minHeight = 'clamp(230px,34vh,380px)';
    host.innerHTML = '<div class="pc-ba" data-s="0"><span>CAD teselado</span><b data-n="23402">0</b><em>triángulos</em></div>' +
      '<div class="pc-ba" data-s="1"><span>Activo WebGL</span><b data-n="2720">0</b><em>triángulos</em></div>' +
      '<div class="pc-ba-div"></div><div class="pc-prob-q pc-cap"></div>';
    grid.appendChild(left); grid.appendChild(host);
    const st = document.createElement('style');
    st.textContent = ".pc-ba{position:absolute;top:12px;font:500 clamp(9.5px,.72vw,11px)/1.4 'JetBrains Mono',monospace;letter-spacing:.14em;text-transform:uppercase;color:#767C85;transition:opacity .4s}" +
      ".pc-ba[data-s='0']{left:14px}.pc-ba[data-s='1']{left:calc(50% + 14px)}" +
      ".pc-ba b{display:block;font:500 clamp(20px,2vw,30px)/1.2 'Clash Display','Space Grotesk',sans-serif;letter-spacing:-.01em;color:#EDEEE8;margin-top:4px}" +
      ".pc-ba[data-s='1'] b{color:#C8F53F}.pc-ba em{font-style:normal}" +
      ".pc-ba-div{position:absolute;left:50%;top:10%;bottom:14%;border-left:1px dashed rgba(237,238,232,.14)}";
    document.head.appendChild(st);
    let before = null, after = null, built = false, afterGray = null, afterTex = null, wireB = null, wireA = null;
    const grpB = new THREE.Group(), grpA = new THREE.Group();
    function normalize(geo){
      geo.computeBoundingBox();
      const bb = geo.boundingBox, c = bb.getCenter(new THREE.Vector3()), sz = bb.getSize(new THREE.Vector3());
      geo.translate(-c.x, -c.y, -c.z);
      const k = 1 / Math.max(sz.x, sz.y, sz.z); geo.scale(k, k, k);
      return geo;
    }
    function build(){
      if (built) return; built = true;
      const m = PB.meshes.find(x => /^DJ-2216/.test(x.name));
      if (m){
        m.mesh.updateMatrixWorld(true);
        const g = normalize(m.mesh.geometry.clone().applyMatrix4(m.mesh.matrixWorld));
        afterTex = m.tex.clone(); afterTex.opacity = 1; afterTex.transparent = false; afterTex.emissive = new THREE.Color(0, 0, 0);
        afterGray = new THREE.MeshStandardMaterial({ color: 0x8a9099, roughness: 0.55, metalness: 0.25, envMapIntensity: 1.1 });
        after = new THREE.Mesh(g, afterGray); grpA.add(after);
        wireA = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ color: 0xC8F53F, wireframe: true, transparent: true, opacity: 0, depthWrite: false }));
        grpA.add(wireA);
      }
      fetch(window.__MOTOR_CAD_GLB).then(r => r.arrayBuffer()).then(buf => new THREE.GLTFLoader().parse(buf, '', gl => {
        let mm = null; gl.scene.updateMatrixWorld(true); gl.scene.traverse(o => { if (o.isMesh && !mm) mm = o; });
        const g = normalize(mm.geometry.clone().applyMatrix4(mm.matrixWorld));
        before = new THREE.Mesh(g, new THREE.MeshStandardMaterial({ color: 0x8a9099, roughness: 0.5, metalness: 0.3, envMapIntensity: 1.1 }));
        grpB.add(before);
        wireB = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ color: 0xb8c6d6, wireframe: true, transparent: true, opacity: 0.32, depthWrite: false }));
        grpB.add(wireB);
      }));
      PB.scene.add(grpB); PB.scene.add(grpA); grpB.visible = grpA.visible = false;
    }
    (function warm(){ if (PB.ready()) setTimeout(build, 3500); else setTimeout(warm, 500); })();
    const cam = new THREE.PerspectiveCamera(30, 1, 0.01, 20);
    let prev = -1, stT = 0, tt = 0;
    const counters = Array.from(host.querySelectorAll('.pc-ba b'));
    const fmtN = n => String(Math.round(n)).replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
    const caps = ['El motor DJ-2216, tal como sale del CAD', 'La teselación convierte superficies en <b>23 402</b> triángulos',
      'Limpieza de la malla en Blender', 'Retopología: la misma forma con <b>2 720</b> triángulos',
      'Bake: el detalle vuelve como textura (normales y oclusión)', 'Exportación a Unity', 'Corre en el navegador',
      'Esta pieza: <b>23 402 → 2 720</b> triángulos (−88 %)', 'La pieza conserva su nombre y su lugar en el ensamblaje'];
    PB.register({
      slide: s, host: host,
      enter(){ build(); prev = -1; counters.forEach(c => { c.textContent = '0'; c._v = 0; }); },
      exit(){ grpB.visible = grpA.visible = false; PB.drone.visible = true; },
      update(dt, step){
        if (step !== prev){ prev = step; stT = 0; }
        stT += dt; tt += dt;
        host.querySelector('[data-s="1"]').style.opacity = step >= 3 ? 1 : 0.25;
        counters.forEach((c, i) => {
          const on = i === 0 ? step >= 1 : step >= 3, n = +c.dataset.n;
          c._v = on ? Math.min(n, (c._v || 0) + n * dt * 1.4) : 0;
          c.textContent = fmtN(c._v);
        });
        if (after){
          after.material = step >= 4 ? afterTex : afterGray;
          wireA.material.opacity = lerp(wireA.material.opacity, step === 3 ? 0.55 : 0, Math.min(1, dt * 4));
        }
        if (wireB) wireB.material.opacity = step === 7 ? 0.32 + 0.25 * Math.sin(tt * 5) : 0.32;
        const q = host.querySelector('.pc-prob-q'), html = caps[Math.min(step, caps.length - 1)];
        if (q.innerHTML !== html) q.innerHTML = html;
      },
      render(){
        const R = PB.renderer, W = PB.canvas._w, Hh = PB.canvas._h, step = PB.shownSteps(s);
        PB.drone.visible = false;
        R.setScissorTest(true); R.setClearColor(0x000000, 0); R.clear();
        const ang = 0.6 + tt * 0.35, r = 2.35;
        cam.position.set(r * Math.sin(ang) * 0.93, r * 0.36, r * Math.cos(ang) * 0.93); cam.lookAt(0, 0, 0);
        const half = (x, grp, on) => {
          grpB.visible = grp === grpB && on; grpA.visible = grp === grpA && on;
          R.setViewport(x, 0, W / 2, Hh * 0.9); R.setScissor(x, 0, W / 2, Hh * 0.9);
          cam.aspect = (W / 2) / (Hh * 0.9); cam.updateProjectionMatrix();
          if (on) R.render(PB.scene, cam);
        };
        half(0, grpB, !!before);
        half(W / 2, grpA, !!after && step >= 3);
        grpB.visible = grpA.visible = false;
        R.setScissorTest(false); R.setViewport(0, 0, W, Hh);
      }
    });
  })();
})();
