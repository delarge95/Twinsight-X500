// Ventana estilizada de un simulador alrededor del vídeo real de Gazebo: barra de título con botones decorativos, barra de
// herramientas con iconos simples, panel lateral de entidades y barra de estado. Se pinta una vez en un lienzo 2D (textura del plano).
// No es una captura de Gazebo ni lleva su logotipo: es una interfaz genérica en el lenguaje del diseño v2 (grafito, radios pequeños).
// Medidas: fracciones del ANCHO del vídeo recortado (gazebo-config.js → ventana).

const COL = {
  fondo: '#111315', barra: '#1F2327', herramientas: '#171A1D', panel: '#171A1D', linea: '#33393F', borde: '#4A5158',
  texto: '#A9AFB6', tenue: '#858C94', sel: '#2A2F34', video: '#0C0D0E',
};

/** Tamaño de la ventana y posición del vídeo dentro, en unidades de «ancho del vídeo» (el vídeo mide 1 × 1/aspecto). */
export function ventanaLayout(v, aspect) {
  const b = v.borde, top = v.titulo + v.herramientas, vh = 1 / aspect;
  const W = 1 + v.panel + 2 * b, H = vh + top + v.estado + 2 * b;
  // centro del vídeo respecto al centro de la ventana (x a la derecha, y hacia arriba)
  const vx = (b + v.panel + 0.5) - W / 2, vy = H / 2 - (b + top + vh / 2);
  return { W, H, vx, vy, vh };
}

export function drawChrome(v, aspect, canvas = document.createElement('canvas')) {
  const L = ventanaLayout(v, aspect);
  const S = 1280 / L.W; // px de lienzo por unidad
  canvas.width = Math.round(L.W * S); canvas.height = Math.round(L.H * S);
  const g = canvas.getContext('2d');
  const px = (u) => Math.round(u * S);
  const b = px(v.borde), tH = px(v.titulo), hH = px(v.herramientas), eH = px(v.estado), pW = px(v.panel);
  const W = canvas.width, H = canvas.height;
  const font = (size, weight = 400, mono = false) => `${weight} ${size}px ${mono ? '"IBM Plex Mono", Consolas, monospace' : '"IBM Plex Sans", "Segoe UI", sans-serif'}`;

  g.fillStyle = COL.fondo; g.fillRect(0, 0, W, H);
  // barra de título
  g.fillStyle = COL.barra; g.fillRect(b, b, W - 2 * b, tH);
  g.fillStyle = COL.texto; g.font = font(Math.round(tH * 0.42), 500); g.textBaseline = 'middle';
  g.fillText('Gazebo Sim', b + tH * 0.45, b + tH / 2);
  // botones de ventana (decorativos): minimizar, maximizar, cerrar
  g.strokeStyle = COL.tenue; g.lineWidth = Math.max(1.5, tH * 0.05);
  const bs = tH * 0.26, by = b + tH / 2;
  const bx = (i) => W - b - tH * (0.55 + i * 0.85);
  g.beginPath(); g.moveTo(bx(2) - bs, by + bs * 0.6); g.lineTo(bx(2) + bs, by + bs * 0.6); g.stroke();
  g.strokeRect(bx(1) - bs, by - bs, 2 * bs, 2 * bs);
  g.beginPath(); g.moveTo(bx(0) - bs, by - bs); g.lineTo(bx(0) + bs, by + bs); g.moveTo(bx(0) + bs, by - bs); g.lineTo(bx(0) - bs, by + bs); g.stroke();
  // barra de herramientas: iconos simples
  const hy = b + tH;
  g.fillStyle = COL.herramientas; g.fillRect(b, hy, W - 2 * b, hH);
  g.fillStyle = COL.linea; g.fillRect(b, hy, W - 2 * b, 1); g.fillRect(b, hy + hH - 1, W - 2 * b, 1);
  const s = hH * 0.24, cy = hy + hH / 2;
  let x = b + hH * 0.6;
  const step = hH * 0.95;
  g.strokeStyle = COL.tenue; g.fillStyle = COL.tenue; g.lineWidth = Math.max(1.5, hH * 0.045);
  const icons = [
    () => { g.beginPath(); g.moveTo(x - s * 0.7, cy - s); g.lineTo(x + s, cy); g.lineTo(x - s * 0.7, cy + s); g.closePath(); g.fill(); },            // reproducir
    () => { g.fillRect(x - s * 0.8, cy - s, s * 0.5, 2 * s); g.fillRect(x + s * 0.3, cy - s, s * 0.5, 2 * s); },                                  // pausa
    () => { g.beginPath(); g.moveTo(x - s, cy - s); g.lineTo(x + s * 0.3, cy); g.lineTo(x - s, cy + s); g.closePath(); g.fill(); g.fillRect(x + s * 0.45, cy - s, s * 0.4, 2 * s); }, // paso
    null,
    () => { g.beginPath(); g.moveTo(x - s * 1.1, cy + s); g.lineTo(x - s * 0.2, cy - s * 0.1); g.lineTo(x + s * 0.2, cy + s * 0.3); g.lineTo(x + s * 1.1, cy - s); g.stroke(); }, // seleccionar
    () => { g.beginPath(); g.moveTo(x - s, cy); g.lineTo(x + s, cy); g.moveTo(x, cy - s); g.lineTo(x, cy + s); g.stroke(); },                   // mover
    () => { g.beginPath(); g.arc(x, cy, s * 0.9, 0.3, Math.PI * 1.75); g.stroke(); },                                                             // rotar
    null,
    () => { g.strokeRect(x - s * 0.85, cy - s * 0.85, s * 1.7, s * 1.7); },                                                                       // caja
    () => { g.beginPath(); g.arc(x, cy, s * 0.9, 0, Math.PI * 2); g.stroke(); },                                                                 // esfera
    () => { g.beginPath(); g.ellipse(x, cy - s * 0.65, s * 0.8, s * 0.3, 0, 0, Math.PI * 2); g.moveTo(x - s * 0.8, cy - s * 0.65); g.lineTo(x - s * 0.8, cy + s * 0.7); g.moveTo(x + s * 0.8, cy - s * 0.65); g.lineTo(x + s * 0.8, cy + s * 0.7); g.stroke(); }, // cilindro
    () => { g.beginPath(); g.arc(x, cy, s * 0.45, 0, Math.PI * 2); g.stroke(); for (let k = 0; k < 8; k++) { const a = (k * Math.PI) / 4; g.moveTo(x + Math.cos(a) * s * 0.7, cy + Math.sin(a) * s * 0.7); g.lineTo(x + Math.cos(a) * s * 1.05, cy + Math.sin(a) * s * 1.05); } g.stroke(); }, // luz
  ];
  for (const ic of icons) {
    if (ic) { ic(); x += step; } else { g.fillStyle = COL.linea; g.fillRect(x - step * 0.3, cy - s * 1.1, 1, 2.2 * s); g.fillStyle = COL.tenue; x += step * 0.45; }
  }
  // panel lateral: árbol de entidades (identificadores del mundo, no texto de interfaz)
  const py = hy + hH, ph = H - py - eH - b;
  if (pW > 0) {
    g.fillStyle = COL.panel; g.fillRect(b, py, pW, ph);
    g.fillStyle = COL.linea; g.fillRect(b + pW - 1, py, 1, ph);
    // panel sencillo: pocas filas y letra legible (≥ 11 px con la ventana a ~500 px)
    const rh = Math.min(hH * 1.15, ph / 5), fs = Math.round(Math.min(rh * 0.5, pW / 9.5));
    g.font = font(fs, 500, true); g.textBaseline = 'middle';
    const rows = [['default', 0], ['x500_0', 1], ['demo_cam', 1], ['sun', 1]];
    rows.forEach(([name, lvl], i) => {
      const ry = py + rh * (i + 0.6);
      if (name === 'x500_0') { g.fillStyle = COL.sel; g.fillRect(b + 1, ry - rh / 2, pW - 2, rh); }
      g.fillStyle = lvl === 0 ? COL.texto : COL.tenue;
      g.fillText((lvl ? '▸ ' : '') + name, b + rh * (0.3 + 0.35 * lvl), ry);
    });
  }
  // zona del vídeo (el plano del vídeo va encima) y barra de estado
  g.fillStyle = COL.video; g.fillRect(b + pW, py, W - 2 * b - pW, ph);
  const ey = H - b - eH;
  g.fillStyle = COL.barra; g.fillRect(b, ey, W - 2 * b, eH);
  g.fillStyle = COL.tenue; g.fillRect(b + eH * 0.45, ey + eH / 2 - eH * 0.13, eH * 0.26, eH * 0.26);
  g.fillStyle = COL.texto; g.font = font(Math.round(eH * 0.46), 400, true); g.textBaseline = 'middle';
  g.fillText('PX4 SITL · x500_0', b + eH * 1.0, ey + eH / 2);
  // borde de la ventana
  g.strokeStyle = COL.borde; g.lineWidth = Math.max(2, b); g.strokeRect(b / 2, b / 2, W - b, H - b);
  return canvas;
}
