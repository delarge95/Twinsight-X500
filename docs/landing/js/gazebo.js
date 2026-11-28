// Sección «Gazebo»: una ventana pequeña de simulador (interfaz estilizada alrededor del vídeo real de Gazebo, recortado) es un plano
// dentro de la escena 3D de la landing (mismo renderer, mismo GLB), y el gemelo vuela alrededor y por delante de ella, grande, con la
// telemetría de PX4 grabada a la vez. Cómo se transforma la posición del dron del vídeo en la del gemelo: gazebo-config.js (gemelo).
// El vídeo manda: el tiempo sale de requestVideoFrameCallback (o currentTime) → mapa de tiempo (o desfase) → telemetría.
import { GAZEBO as C } from './gazebo-config.js';
import { Vector3, Quaternion, Matrix4, Euler, VideoTexture, Texture, SRGBColorSpace } from '../vendor/three.bundle.js';
import { drawChrome, ventanaLayout } from './gazebo-ventana.js';

const smoothstep = (a, b, x) => { const t = Math.min(1, Math.max(0, (x - a) / (b - a))); return t * t * (3 - 2 * t); };
const getJson = (url) => (url ? fetch(url).then((r) => (r.ok ? r.json() : null)).catch(() => null) : Promise.resolve(null));
const num = (...xs) => xs.find((x) => typeof x === 'number' && Number.isFinite(x));

export function createGazebo({ section, video, frame, fail, reduced, onState }) {
  const hasRvfc = 'requestVideoFrameCallback' in HTMLVideoElement.prototype;
  // el recuadro del DOM toma la proporción de la ventana completa (vídeo recortado + barras + panel): la escena la clava ahí
  { const r = C.recorte; const L0 = ventanaLayout(C.ventana, (r.x1 - r.x0) / (r.y1 - r.y0)); frame.style.aspectRatio = `${L0.W} / ${L0.H}`; }
  const st = {
    data: null, cam: { ...C.camara }, desfase: C.desfase, desfaseFuente: 'config', mapa: null,
    mediaTime: 0, clock: 0, failed: false, playing: false, inView: false, started: false,
    tex: null, posterTex: null, videoTex: null, hasFrame: false,
  };

  // ───── telemetría ─────
  function decode(d) {
    const F = d?.frames;
    if (!F?.length) return null;
    const n = F.length;
    const T = new Float32Array(n), P = new Float32Array(n * 3), Q = new Float32Array(n * 4), U = new Float32Array(n);
    for (let i = 0; i < n; i++) {
      const f = F[i];
      T[i] = f[0] / 1000;
      P[i * 3] = f[2] / 100; P[i * 3 + 1] = f[1] / 100; P[i * 3 + 2] = -f[3] / 100; // NED → ENU: (e, n, −d)
      Q[i * 4] = f[4] / 1e4; Q[i * 4 + 1] = f[5] / 1e4; Q[i * 4 + 2] = f[6] / 1e4; Q[i * 4 + 3] = f[7] / 1e4; // w x y z (FRD → NED)
      U[i] = (f[8] + f[9] + f[10] + f[11]) / 4000;
    }
    const rec = (d.events || []).filter((e) => e.kind === 'record' && typeof e.lt === 'number').map((e) => e.lt - d.t0_lt);
    return { n, T, P, Q, U, duration: T[n - 1], recStart: rec[0], recEnd: rec.length > 1 ? rec[rec.length - 1] : undefined };
  }

  // ───── cámara de Gazebo → cámara three ─────
  // A: mundo ENU → cámara three (x derecha, y arriba, −z adelante). La cámara de Gazebo mira por su +x con +z arriba.
  const A = new Matrix4(), qPre = new Quaternion(), qPost = new Quaternion(), camPos = new Vector3();
  let k = 1, radiusM = 0.4, halfW = 1, halfH = 1;
  function setupCamera(nose) {
    const [x, y, z, roll, pitch, yaw] = st.cam.pose;
    camPos.set(x, y, z);
    const Rwc = new Matrix4().makeRotationFromEuler(new Euler(roll, pitch, yaw, 'ZYX')); // SDF: Rz·Ry·Rx
    const Cm = new Matrix4().set(0, -1, 0, 0, 0, 0, 1, 0, -1, 0, 0, 0, 0, 0, 0, 1); // enlace de cámara (x adelante, y izq., z arriba) → three
    A.multiplyMatrices(Cm, Rwc.clone().transpose());
    // Actitud del gemelo = A · (NED→ENU) · R(q) · (FLU→FRD) · (modelo GLB → FLU)
    const T = new Matrix4().set(0, 1, 0, 0, 1, 0, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1);
    const S = new Matrix4().makeScale(1, -1, -1);
    const up = new Vector3(0, 1, 0), left = new Vector3().crossVectors(up, nose);
    const B = new Matrix4().set(nose.x, nose.y, nose.z, 0, left.x, left.y, left.z, 0, up.x, up.y, up.z, 0, 0, 0, 0, 1);
    qPre.setFromRotationMatrix(new Matrix4().multiplyMatrices(A, T));
    qPost.setFromRotationMatrix(new Matrix4().multiplyMatrices(S, B));
    k = C.D / C.Z_ref;
    halfW = C.D * Math.tan((st.cam.hfov ?? C.camara.hfov) / 2);
    halfH = halfW * (st.cam.alto ?? C.camara.alto) / (st.cam.ancho ?? C.camara.ancho);
  }

  // ───── muestra de la telemetría en el instante del vídeo ─────
  const out = { pos: new Vector3(), quat: new Quaternion(), scale: 1, alpha: 1, near: 0, u: 0, rho: 1, t: 0 };
  const qa = new Quaternion(), qb = new Quaternion(), v = new Vector3();
  function index(t) {
    const T = st.data.T;
    let lo = 0, hi = st.data.n - 1;
    if (t <= T[0]) return [0, 0];
    if (t >= T[hi]) return [hi, 0];
    while (hi - lo > 1) { const mid = (lo + hi) >> 1; if (T[mid] <= t) lo = mid; else hi = mid; }
    const span = T[hi] - T[lo];
    return [lo, span > 1e-6 ? (t - T[lo]) / span : 0];
  }
  // Tiempo de vídeo → tiempo de telemetría: con mapaTiempo (nudos [t_vídeo, t_telemetría], lineal a tramos y pendiente 1 fuera de
  // ellos) para el vídeo recortado y acelerado; si no hay mapa, t_vídeo + desfase.
  function telTime(tv) {
    const m = st.mapa;
    if (!m) return tv + st.desfase;
    if (tv <= m[0][0]) return m[0][1] + (tv - m[0][0]);
    const last = m[m.length - 1];
    if (tv >= last[0]) return last[1] + (tv - last[0]);
    let lo = 0, hi = m.length - 1;
    while (hi - lo > 1) { const mid = (lo + hi) >> 1; if (m[mid][0] <= tv) lo = mid; else hi = mid; }
    const a = m[lo], b = m[hi];
    return a[1] + ((tv - a[0]) / (b[0] - a[0])) * (b[1] - a[1]);
  }
  function sample(dt, tVideo) {
    const d = st.data;
    if (!d) return null;
    if (tVideo !== undefined) { /* instante dado (afinado): no toca el reloj */ }
    else if (reduced) st.mediaTime = C.tPoster;
    else if (st.failed || !st.started) { if (st.inView && st.failed) st.clock = (st.clock + dt) % Math.max(1, st.videoDur || d.duration - st.desfase); st.mediaTime = st.clock; }
    else if (!hasRvfc) st.mediaTime = video.currentTime;
    const t = telTime(tVideo ?? st.mediaTime);
    out.t = t;
    const [i, f] = index(t);
    const j = Math.min(d.n - 1, i + 1);
    const P = d.P, Q = d.Q;
    v.set(P[i * 3] + (P[j * 3] - P[i * 3]) * f, P[i * 3 + 1] + (P[j * 3 + 1] - P[i * 3 + 1]) * f, P[i * 3 + 2] + (P[j * 3 + 2] - P[i * 3 + 2]) * f);
    qa.set(Q[i * 4 + 1], Q[i * 4 + 2], Q[i * 4 + 3], Q[i * 4]);
    qb.set(Q[j * 4 + 1], Q[j * 4 + 2], Q[j * 4 + 3], Q[j * 4]);
    qa.slerp(qb, f);
    out.quat.copy(qPre).multiply(qa).multiply(qPost);
    out.u = d.U[i] + (d.U[j] - d.U[i]) * f;

    const prof = innerWidth <= 860 ? C.movil : C.escritorio;
    v.z += C.alturaCuerpo;                   // el EKF da 0 en el suelo; el centro del dron va más arriba
    v.sub(camPos).applyMatrix4(A);           // coordenadas de cámara three (m)
    const z = Math.max(0.5, -v.z);           // profundidad delante de la cámara de Gazebo
    const rho = z / C.Z_ref;
    const zp = C.Z_ref * (rho >= 1 ? C.planoMax : Math.min(C.planoMax, Math.max(prof.rhoMin, Math.pow(rho, prof.gamma))));
    out.pos.copy(v).multiplyScalar((zp / z) * k); // mismo rayo, más cerca: mismo píxel que el dron del vídeo
    const near = smoothstep(C.opacidad.rhoLejos, C.opacidad.rhoCerca, rho);
    out.scaleExact = (C.radioDron ?? radiusM) * k * (zp / z); // tamaño de la proyección exacta (el del dron del vídeo)
    out.scale = out.scaleExact;
    out.alpha = C.opacidad.lejos + (C.opacidad.cerca - C.opacidad.lejos) * near;
    out.near = near;
    out.rho = rho;
    out.zReal = z;
    // posición y tamaño del dron en el vídeo, normalizados al recorte (−1…1; y hacia abajo): lo que usa el gemelo para volar alrededor
    const b = cropBox(), fv = (b.w / 2) / Math.tan((st.cam.hfov ?? C.camara.hfov) / 2);
    const pxv = b.w / 2 + (v.x / z) * fv, pyv = b.h / 2 - (v.y / z) * fv;
    out.nx = (pxv - (b.x0 + b.x1) / 2) / ((b.x1 - b.x0) / 2);
    out.ny = (pyv - (b.y0 + b.y1) / 2) / ((b.y1 - b.y0) / 2);
    out.rVid = ((C.radioDron ?? radiusM) * fv) / z; // radio del dron del vídeo, en px del vídeo
    return out;
  }

  // ───── vídeo ─────
  function onVideoFrame(now, meta) {
    st.mediaTime = meta.mediaTime;
    st.hasFrame = true;
    video.requestVideoFrameCallback(onVideoFrame);
  }
  function setFailed() {
    if (st.failed) return;
    st.failed = true;
    st.clock = st.mediaTime;
    fail.hidden = false;
    section.classList.add('gz--novideo');
    onState?.('failed');
  }
  function loadVideo() {
    if (reduced) return;
    for (const s of C.video) {
      const el = document.createElement('source');
      el.src = s.src; el.type = s.type;
      video.append(el);
    }
    const sources = video.querySelectorAll('source');
    sources[sources.length - 1]?.addEventListener('error', setFailed); // la última fuente falló: no hay vídeo que reproducir
    video.addEventListener('error', setFailed);
    video.addEventListener('loadedmetadata', () => {
      // sin sincronia.json: la grabadora arranca un poco tarde respecto al evento «record»; el sobrante del guion va al principio
      if (st.desfaseFuente === 'eventos' && st.data.recEnd !== undefined && video.duration) {
        st.desfase = st.data.recStart + Math.max(0, st.data.recEnd - st.data.recStart - video.duration);
      }
    });
    video.addEventListener('playing', () => { st.started = true; st.playing = true; });
    video.addEventListener('pause', () => { st.playing = false; });
    video.addEventListener('seeked', () => { if (hasRvfc && video.paused) st.mediaTime = video.currentTime; });
    if (hasRvfc) video.requestVideoFrameCallback(onVideoFrame);
    video.preload = 'auto';
    video.load();
    if (st.inView) play();
  }
  function play() {
    if (reduced || st.failed || !video.querySelector('source')) return;
    const p = video.play();
    p?.catch?.(() => { /* autoplay bloqueado: se queda en el póster y el gemelo quieto */ });
  }

  // ───── carga perezosa y pausa fuera de vista ─────
  let loaded = false, twinRef = null;
  async function load() {
    if (loaded) return;
    loaded = true;
    if (!reduced) video.poster = C.poster;
    const [tel, sync] = await Promise.all([getJson(C.telemetria), getJson(C.sincronia)]);
    st.data = decode(tel);
    if (!st.data) { section.classList.add('gz--nodata'); return; }
    const off = num(sync?.desfase_s, sync?.offset_video_a_telemetria_s, sync?.desfase, sync?.offset_s, sync?.offset);
    if (off !== undefined) { st.desfase = off; st.desfaseFuente = 'sincronia'; }
    else if (st.data.recStart !== undefined) { st.desfase = st.data.recStart; st.desfaseFuente = 'eventos'; }
    // cámara: { camara: { pose, hfov, ancho, alto } } o el formato de la grabación { camara_gazebo: { pose_enu_xyz_rpy, hfov_rad }, video: { ancho, alto } }
    const cg = sync?.camara_gazebo;
    const cam = sync?.camara ?? sync?.camera ?? (cg && { pose: cg.pose_enu_xyz_rpy, hfov: cg.hfov_rad, ancho: sync.video?.ancho, alto: sync.video?.alto });
    if (Array.isArray(cam?.pose) && cam.pose.length === 6) for (const key of ['pose', 'hfov', 'ancho', 'alto']) if (cam[key] != null) st.cam[key] = cam[key];
    // vídeo recortado y acelerado: mapaTiempo = [[t_vídeo_nuevo, t_telemetría], …]
    const mapa = sync?.mapaTiempo ?? sync?.mapa_tiempo;
    if (Array.isArray(mapa) && mapa.length >= 2 && mapa.every((p) => Array.isArray(p) && p.length >= 2)) { st.mapa = mapa.map((p) => [+p[0], +p[1]]).sort((a, b) => a[0] - b[0]); st.desfaseFuente = 'mapaTiempo'; }
    st.videoDur = num(sync?.video?.duracion_s);
    loadVideo();
    if (twinRef) attach(twinRef);
  }
  new IntersectionObserver((es, io) => { if (es.some((e) => e.isIntersecting)) { load(); io.disconnect(); } }, { rootMargin: C.margenCarga }).observe(section);
  new IntersectionObserver((es) => {
    st.inView = es.some((e) => e.isIntersecting);
    if (!loaded) return;
    if (st.inView) play(); else video.pause();
  }, { threshold: 0.05 }).observe(frame);
  document.addEventListener('visibilitychange', () => { if (document.hidden) video.pause(); else if (st.inView) play(); });

  // ───── texturas ─────
  function posterTexture() {
    if (st.posterTex) return st.posterTex;
    const img = new Image();
    const tex = new Texture(img);
    tex.colorSpace = SRGBColorSpace;
    cropTexture(tex);
    img.onload = () => { tex.needsUpdate = true; st.posterReady = true; };
    img.onerror = () => { st.posterReady = false; };
    img.src = C.poster;
    st.posterTex = tex;
    return tex;
  }
  // Recorte por UV (sin recodificar): el plano muestra solo la caja C.recorte del vídeo; el póster se recorta igual.
  function cropBox() {
    const w = st.cam.ancho ?? C.camara.ancho, h = st.cam.alto ?? C.camara.alto;
    const r = C.recorte ?? { x0: 0, y0: 0, x1: w, y1: h };
    return { w, h, x0: Math.max(0, r.x0), y0: Math.max(0, r.y0), x1: Math.min(w, r.x1), y1: Math.min(h, r.y1) };
  }
  function cropTexture(tex) {
    const b = cropBox();
    tex.repeat.set((b.x1 - b.x0) / b.w, (b.y1 - b.y0) / b.h);
    tex.offset.set(b.x0 / b.w, 1 - b.y1 / b.h);
  }
  function texture() {
    if (st.failed) return null;               // sin vídeo: solo el gemelo (y el marco)
    if (!reduced && st.hasFrame || (!reduced && !hasRvfc && st.started)) {
      if (!st.videoTex) { st.videoTex = new VideoTexture(video); st.videoTex.colorSpace = SRGBColorSpace; cropTexture(st.videoTex); }
      return st.videoTex;
    }
    const p = posterTexture();
    return st.posterReady ? p : null;
  }

  // marco de la ventana del simulador (barra de título, herramientas, panel y estado) dibujado una vez en un lienzo 2D
  let chromeTex = null;
  function chromeTexture() {
    if (chromeTex) return chromeTex;
    const b = cropBox();
    const canvas = drawChrome(C.ventana, (b.x1 - b.x0) / (b.y1 - b.y0));
    chromeTex = new Texture(canvas);
    chromeTex.colorSpace = SRGBColorSpace;
    chromeTex.anisotropy = 4;
    chromeTex.needsUpdate = true;
    // las fuentes IBM Plex pueden llegar después: se repinta una vez cuando estén
    document.fonts?.ready?.then(() => { drawChrome(C.ventana, (b.x1 - b.x0) / (b.y1 - b.y0), canvas); chromeTex.needsUpdate = true; });
    return chromeTex;
  }

  function attach(twin) {
    twinRef = twin;
    if (!st.data) return;
    setupCamera(new Vector3().fromArray(twin.noseDir));
    radiusM = twin.radiusM;
    // recorte en unidades de escena a la distancia del plano (centro relativo al eje óptico y medio ancho/alto)
    const b = cropBox(), upp = (2 * halfW) / b.w;
    const crop = { cx: ((b.x0 + b.x1) / 2 - b.w / 2) * upp, cy: -((b.y0 + b.y1) / 2 - b.h / 2) * upp, hw: ((b.x1 - b.x0) / 2) * upp, hh: ((b.y1 - b.y0) / 2) * upp };
    twin.setGazebo(st.layer = {
      D: C.D, halfW, halfH, crop,
      sample, texture,
      rect: () => frame.getBoundingClientRect(),
      headRect: () => section.querySelector('.block__head')?.getBoundingClientRect() ?? null,
      params: () => (innerWidth <= 860 ? C.movil : C.escritorio),
      ventana: ventanaLayout(C.ventana, (b.x1 - b.x0) / (b.y1 - b.y0)), chrome: chromeTexture,
      cropPx: b,
      spin: C.giroHelices,
    });
  }

  // Píxel del vídeo donde el mapeo pone al gemelo (para comprobar el solape con el dron del vídeo).
  function pixel() {
    const w = st.cam.ancho ?? C.camara.ancho, h = st.cam.alto ?? C.camara.alto, f = (w / 2) / Math.tan((st.cam.hfov ?? C.camara.hfov) / 2);
    const z = -out.pos.z;
    return z > 0 ? [Math.round(w / 2 + (out.pos.x / z) * f), Math.round(h / 2 - (out.pos.y / z) * f)] : null;
  }

  /** Solo para afinar: píxel del vídeo que da el mapeo para un instante de vídeo y un desfase dados (no toca el estado). */
  function project(tVideo, desfase = st.desfase) {
    const keep = st.desfase;
    st.desfase = desfase;
    sample(0, tVideo);
    const px = pixel();
    st.desfase = keep;
    return px;
  }

  return {
    attach,
    project,
    /** Estado para pruebas y para afinar: tiempo, ρ, desfase y de dónde salió. */
    debug: () => ({ ultimo: st.layer?.ultimo, nx: out.nx, ny: out.ny, mapa: !!st.mapa, px: pixel(), poster: st.posterReady, tex: st.videoTex ? 'video' : st.posterTex ? 'póster' : 'ninguna', mediaTime: st.mediaTime, t: out.t, rho: out.rho, alpha: out.alpha, scale: out.scale, desfase: st.desfase, fuente: st.desfaseFuente, failed: st.failed, playing: st.playing, rvfc: hasRvfc, cam: st.cam.pose }),
  };
}
