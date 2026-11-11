// Escena 3D de la landing de TwinSight X500.
// Un solo modelo (el GLB optimizado de la app web) y un estado numérico que el scroll interpola:
// cámara, explosión, barrido de escáner (malla / rayos X), térmico ilustrativo y vuelo reproducido.
import {
  WebGLRenderer, Scene, PerspectiveCamera, Group, Mesh, Color, Vector3, Vector4, Box3, MathUtils,
  PMREMGenerator, Quaternion, DirectionalLight, HemisphereLight, ShaderMaterial, PlaneGeometry, BufferGeometry,
  BufferAttribute, MeshBasicMaterial, AdditiveBlending, DoubleSide, SRGBColorSpace,
  ACESFilmicToneMapping, GLTFLoader, MeshoptDecoder, RoomEnvironment,
} from '../vendor/three.bundle.js';

const ACCENT = new Color('#46afff');
const HIGHLIGHT = new Color('#bfeaff');
const FOV = 28;
const HEAT_COUNT = 6;
const TRAIL_POINTS = 70;
const UNITS_PER_METRE = 0.32; // escala visual de la estela (no es la escala del modelo)
const TRAIL_LENGTH = 2.1;      // longitud máxima de la estela, en unidades de escena (≈ 6,5 m de vuelo)

export const DEFAULT_STATE = {
  az: 0.7, el: 0.38, dist: 1, sx: 0, sy: 0,
  explode: 0, sweep: 0, thermal: 0, flight: 0,
  grid: 0.5, sway: 0.2, spin: 1, labels: 0,
  fov: 28, // campo de visión vertical, en grados; se anima para igualar la cámara de la web app al abrirla
  aim: 0,  // 0 = la cámara mira al centro del modelo; 1 = a su origen, como hace la web app
};

// Piezas que la landing rotula en la vista explosionada. `match` es el prefijo del nodo en el GLB;
// los nombres visibles salen de las fichas técnicas del proyecto (ver main.js).
export const ANCHORS = [
  { id: 'fc', match: 'DIKE-PIXHAWK6C' },
  { id: 'gps', match: 'x500v2_gps_m10_PROXY' },
  { id: 'motor', match: 'DJ-2216-KV880_001' },
  { id: 'prop', match: 'x500v2_propeller_instance_2' },
  { id: 'battery', match: 'x500v2_battery_PROXY' },
  { id: 'pm', match: 'PCB-PM06' },
  { id: 'radio', match: 'x500v2_telemetry_radio_PROXY' },
  { id: 'plate', match: 'TOP-PLATE-X500' },
];

const smooth = (x) => { const t = MathUtils.clamp(x, 0, 1); return t * t * (3 - 2 * t); };

const OVERLAY_VERT = /* glsl */ `
  varying vec3 vW; varying vec3 vN; varying vec3 vV;
  void main() {
    vec4 w = modelMatrix * vec4(position, 1.0);
    vW = w.xyz;
    vN = normalize(mat3(modelMatrix) * normal);
    vV = normalize(cameraPosition - w.xyz);
    gl_Position = projectionMatrix * viewMatrix * w;
  }`;

const OVERLAY_FRAG = /* glsl */ `
  uniform vec4 uClip; uniform vec3 uColor; uniform vec3 uHot; uniform float uMode;
  varying vec3 vW; varying vec3 vN; varying vec3 vV;
  void main() {
    float s = dot(vW, uClip.xyz);
    if (s > uClip.w) discard;
    float edge = smoothstep(0.07, 0.0, uClip.w - s);
    float f = pow(1.0 - abs(dot(normalize(vN), normalize(vV))), 2.2);
    vec3 col; float a = 1.0;
    if (uMode < 0.5) { col = uColor * (0.07 + 1.15 * f); }            // rayos X (aditivo)
    else if (uMode < 1.5) { col = mix(uColor, uHot, 0.45); a = 0.3; }  // malla
    else { col = vec3(0.027, 0.039, 0.058) * (0.7 + 0.6 * (1.0 - f)); } // relleno bajo la malla
    col += uHot * edge * (uMode > 1.5 ? 0.25 : 0.9);
    gl_FragColor = vec4(col, a);
  }`;

const THERMAL_HEAD = /* glsl */ `
  varying vec3 vTwinW;
  uniform vec4 uClip; uniform float uThermal; uniform float uRim;
  uniform vec4 uHeat[${HEAT_COUNT}]; uniform float uHeatSigma[${HEAT_COUNT}];
  vec3 twinRamp(float t) {
    vec3 c = mix(vec3(0.03, 0.07, 0.30), vec3(0.36, 0.10, 0.55), smoothstep(0.0, 0.28, t));
    c = mix(c, vec3(0.86, 0.20, 0.36), smoothstep(0.24, 0.50, t));
    c = mix(c, vec3(1.00, 0.54, 0.16), smoothstep(0.46, 0.72, t));
    c = mix(c, vec3(1.00, 0.90, 0.48), smoothstep(0.70, 0.92, t));
    return mix(c, vec3(1.0), smoothstep(0.95, 1.0, t) * 0.7);
  }
  float twinHeat(vec3 p) {
    float h = 0.05;
    for (int i = 0; i < ${HEAT_COUNT}; i++) {
      vec3 d = p - uHeat[i].xyz;
      h += uHeat[i].w * exp(-dot(d, d) / (2.0 * uHeatSigma[i] * uHeatSigma[i]));
    }
    return clamp(h, 0.0, 1.0);
  }`;

const THERMAL_MIX = /* glsl */ `
  {
    // luz de contorno celeste: separa las piezas negras de carbono del fondo oscuro
    float rim = pow(1.0 - clamp(dot(normalize(normal), normalize(vViewPosition)), 0.0, 1.0), 2.4);
    gl_FragColor.rgb += vec3(0.30, 0.70, 1.0) * rim * uRim * (1.0 - uThermal);
  }
  if (uThermal > 0.001) {
    float lum = dot(gl_FragColor.rgb, vec3(0.299, 0.587, 0.114));
    vec3 th = twinRamp(twinHeat(vTwinW)) * (0.5 + 0.85 * lum);
    gl_FragColor.rgb = mix(gl_FragColor.rgb, th, uThermal);
  }`;

const GRID_FRAG = /* glsl */ `
  uniform vec2 uOff; uniform float uAlpha; uniform vec3 uColor;
  varying vec3 vW;
  float gridLine(vec2 p) {
    vec2 g = abs(fract(p - 0.5) - 0.5) / fwidth(p);
    return 1.0 - min(min(g.x, g.y), 1.0);
  }
  void main() {
    vec2 p = vW.xz + uOff;
    float fade = smoothstep(7.5, 0.6, length(vW.xz));
    float g = gridLine(p * 2.0) * 0.35 + gridLine(p * 0.5) * 0.65;
    float pool = smoothstep(2.6, 0.0, length(vW.xz)); // charco de luz bajo el dron
    gl_FragColor = vec4(mix(uColor, vec3(0.30, 0.70, 1.0), pool * 0.6), (g * fade + pool * pool * 0.16) * uAlpha);
  }`;

const SHEET_FRAG = /* glsl */ `
  uniform float uAlpha; uniform vec3 uColor;
  varying vec2 vUv;
  void main() {
    vec2 c = vUv - 0.5;
    float r = length(c) * 2.0;
    float fade = smoothstep(1.0, 0.35, r);
    vec2 b = min(vUv, 1.0 - vUv);
    float lines = step(fract(vUv.y * 46.0), 0.06) * 0.5;
    float a = (0.035 + lines * 0.05) * fade + smoothstep(0.012, 0.0, min(b.x, b.y)) * 0.25 * fade;
    gl_FragColor = vec4(uColor, a * uAlpha);
  }`;

export async function createTwin(canvas, { modelUrl, onProgress, onAnchors, reducedMotion = false, capture = false } = {}) {
  // capture: conserva el búfer para poder exportar el lienzo a PNG (solo lo usan las plantillas de imagen)
  const renderer = new WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: 'high-performance', preserveDrawingBuffer: capture });
  renderer.setClearColor(0x000000, 0);
  renderer.outputColorSpace = SRGBColorSpace;
  renderer.toneMapping = ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.22;

  const scene = new Scene();
  const pmrem = new PMREMGenerator(renderer);
  scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
  scene.environmentIntensity = 0.8;
  pmrem.dispose();

  const camera = new PerspectiveCamera(FOV, 1, 0.1, 120);
  const key = new DirectionalLight(0xffffff, 3.0); key.position.set(3, 5, 4);
  const rim = new DirectionalLight(ACCENT, 3.2); rim.position.set(-4, 1.5, -4);
  scene.add(key, rim, new HemisphereLight(0xcfe3ff, 0x1a2230, 0.85));

  // ---- uniforms compartidos ----
  const clipNormal = new Vector3(0.93, 0.31, 0.2).normalize();
  const clip = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -9);
  const shared = {
    uClip: { value: clip },
    uThermal: { value: 0 },
    uRim: { value: 0.55 },
    uHeat: { value: Array.from({ length: HEAT_COUNT }, () => new Vector4(0, 0, 0, 0)) },
    uHeatSigma: { value: new Array(HEAT_COUNT).fill(0.3) },
  };

  const overlayMat = (mode, extra) => new ShaderMaterial({
    vertexShader: OVERLAY_VERT, fragmentShader: OVERLAY_FRAG,
    uniforms: { uClip: shared.uClip, uColor: { value: ACCENT }, uHot: { value: HIGHLIGHT }, uMode: { value: mode } },
    ...extra,
  });
  const xrayMat = overlayMat(0, { transparent: true, blending: AdditiveBlending, depthWrite: false, side: DoubleSide });
  const wireMat = overlayMat(1, { transparent: true, wireframe: true, depthWrite: false });
  const fillMat = overlayMat(2, { polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1 });

  // ---- modelo ----
  const loader = new GLTFLoader();
  loader.setMeshoptDecoder(MeshoptDecoder);
  const gltf = await new Promise((resolve, reject) => {
    loader.load(modelUrl, resolve, (e) => onProgress?.(e.total ? e.loaded / e.total : 0), reject);
  });
  const root = gltf.scene;
  root.updateMatrixWorld(true);

  const box = new Box3().setFromObject(root);
  const center = box.getCenter(new Vector3());
  const size = box.getSize(new Vector3());
  const radius = size.length() / 2;
  const halfHeight = size.y / 2;

  // Hélices: pivote en su centro para poder girarlas.
  const props = [];
  for (const node of root.children.slice()) {
    if (!node.name.startsWith('x500v2_propeller')) continue;
    const c = new Box3().setFromObject(node).getCenter(new Vector3());
    const pivot = new Group();
    pivot.name = node.name; node.name += '_mesh';
    pivot.position.copy(c);
    root.add(pivot);
    pivot.attach(node);
    props.push({ pivot, dir: Math.sign((c.x - center.x) * (c.z - center.z)) || 1 });
  }
  root.updateMatrixWorld(true);

  // Explosión procedural: separación radial + capas verticales (no usa el bake de Unity).
  const parts = [];
  const tmpBox = new Box3();
  for (const node of root.children) {
    const c = tmpBox.setFromObject(node).getCenter(new Vector3());
    const rel = c.clone().sub(center);
    const fastener = /^GB70|_PRIM$/.test(node.name);
    const off = new Vector3(rel.x * 0.72, 0, rel.z * 0.72);
    off.y = Math.sign(rel.y) * radius * 0.85 * Math.pow(Math.abs(rel.y) / halfHeight, 0.62);
    if (fastener) { off.multiplyScalar(1.14); off.y += Math.sign(rel.y || 1) * radius * 0.09; }
    parts.push({
      node, base: node.position.clone(), off, localCenter: node.worldToLocal(c.clone()),
      delay: 0.34 * (1 - Math.min(1, rel.length() / radius)),
    });
  }

  // Materiales sólidos: recorte de escáner + mezcla térmica inyectados en el PBR original.
  const meshes = [];
  root.traverse((o) => { if (o.isMesh) meshes.push(o); });
  const patched = new Set();
  for (const m of meshes) {
    const mat = m.material;
    if (patched.has(mat)) continue;
    patched.add(mat);
    mat.envMapIntensity = 1;
    mat.onBeforeCompile = (shader) => {
      Object.assign(shader.uniforms, shared);
      shader.vertexShader = 'varying vec3 vTwinW;\n' + shader.vertexShader.replace(
        '#include <project_vertex>',
        '#include <project_vertex>\n  vTwinW = (modelMatrix * vec4(transformed, 1.0)).xyz;',
      );
      shader.fragmentShader = THERMAL_HEAD + '\n' + shader.fragmentShader
        .replace('void main() {', 'void main() {\n  if (dot(vTwinW, uClip.xyz) < uClip.w) discard;')
        .replace('#include <dithering_fragment>', '#include <dithering_fragment>\n' + THERMAL_MIX);
    };
    mat.customProgramCacheKey = () => 'twinsight-landing';
  }
  const overlays = meshes.map((m) => {
    const fill = new Mesh(m.geometry, fillMat); fill.visible = false;
    const ov = new Mesh(m.geometry, wireMat); ov.visible = false;
    ov.renderOrder = 2;
    m.add(fill, ov);
    return { fill, ov };
  });

  const model = new Group();
  model.add(root);
  root.position.copy(center).multiplyScalar(-1);
  model.scale.setScalar(1 / radius);
  const rig = new Group();
  rig.add(model);
  scene.add(rig);
  const groundY = (box.min.y - center.y) / radius - 0.03;

  // Fuentes de calor de la vista térmica ilustrativa: motores, batería y controlador.
  const find = (prefix) => parts.find((p) => p.node.name.startsWith(prefix));
  const heat = [
    ...parts.filter((p) => p.node.name.startsWith('DJ-2216-KV880')).slice(0, 4).map((p, i) => ({ p, w: 0.9, sigma: 0.2, phase: i * 1.7 })),
    { p: find('x500v2_battery_PROXY'), w: 0.42, sigma: 0.26, phase: 0 },
    { p: find('PCB-PM06') ?? find('DIKE-PIXHAWK6C'), w: 0.34, sigma: 0.14, phase: 2 },
  ].filter((h) => h.p).slice(0, HEAT_COUNT);
  heat.forEach((h, i) => { shared.uHeatSigma.value[i] = h.sigma; });

  const nose = new Vector3(0, 0, -1);
  const front = find('ZHIJIA-CAMERA');
  if (front) {
    // el modelo ya está centrado y escalado: basta el signo del eje dominante
    scene.updateMatrixWorld(true);
    const rel = front.node.localToWorld(front.localCenter.clone());
    if (Math.abs(rel.x) > Math.abs(rel.z)) nose.set(Math.sign(rel.x), 0, 0); else nose.set(0, 0, Math.sign(rel.z) || -1);
  }
  const up = new Vector3(0, 1, 0);
  const right = new Vector3().crossVectors(nose, up);
  const qPitch = new Quaternion(), qRoll = new Quaternion(), qId = new Quaternion();

  const anchors = ANCHORS.map((a) => ({ id: a.id, part: find(a.match) })).filter((a) => a.part);

  // ---- suelo, hoja de escáner y estela ----
  const grid = new Mesh(
    new PlaneGeometry(18, 18),
    new ShaderMaterial({
      vertexShader: 'varying vec3 vW; void main(){ vec4 w = modelMatrix * vec4(position,1.0); vW = w.xyz; gl_Position = projectionMatrix * viewMatrix * w; }',
      fragmentShader: GRID_FRAG,
      uniforms: { uOff: { value: { x: 0, y: 0 } }, uAlpha: { value: 0 }, uColor: { value: new Color('#7fa6c8') } },
      transparent: true, depthWrite: false,
    }),
  );
  grid.rotation.x = -Math.PI / 2;
  grid.position.y = groundY;
  grid.renderOrder = -1;
  scene.add(grid);

  const sheet = new Mesh(
    new PlaneGeometry(3.2, 3.2),
    new ShaderMaterial({
      vertexShader: 'varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
      fragmentShader: SHEET_FRAG,
      uniforms: { uAlpha: { value: 0 }, uColor: { value: HIGHLIGHT } },
      transparent: true, depthWrite: false, side: DoubleSide, blending: AdditiveBlending,
    }),
  );
  sheet.lookAt(clipNormal);
  sheet.renderOrder = 3;
  scene.add(sheet);

  const trailPts = Array.from({ length: TRAIL_POINTS }, () => new Vector3());
  const trailPos = new Float32Array(TRAIL_POINTS * 2 * 3);
  const trailCol = new Float32Array(TRAIL_POINTS * 2 * 3);
  const trailLen = new Float32Array(TRAIL_POINTS);
  const trailIdx = [];
  for (let k = 0; k < TRAIL_POINTS - 1; k++) trailIdx.push(k * 2, k * 2 + 1, k * 2 + 2, k * 2 + 1, k * 2 + 3, k * 2 + 2);
  const trailGeo = new BufferGeometry();
  trailGeo.setAttribute('position', new BufferAttribute(trailPos, 3));
  trailGeo.setAttribute('color', new BufferAttribute(trailCol, 3));
  trailGeo.setIndex(trailIdx);
  const trail = new Mesh(trailGeo, new MeshBasicMaterial({ vertexColors: true, transparent: true, blending: AdditiveBlending, depthWrite: false, side: DoubleSide }));
  trail.frustumCulled = false;
  trail.visible = false;
  scene.add(trail);

  // ---- estado ----
  const target = { ...DEFAULT_STATE };
  const cur = { ...DEFAULT_STATE };
  let kind = 'wire';
  let kindTarget = 'wire';
  let sweep = 0;
  let lastExplode = -1;
  let pointer = { x: 0, y: 0 };
  const pcur = { x: 0, y: 0 };
  let userAz = 0, userVel = 0, dragging = false;
  let flight = null; // { frames, index }
  let propAngle = 0;
  let width = 1, height = 1, aspect = 1;
  let running = true, visible = true;
  let pixelCap = Math.min(window.devicePixelRatio || 1, matchMedia('(pointer: coarse)').matches ? 1.5 : 2);
  let slowFrames = 0, measured = 0;

  const v = new Vector3();
  const v2 = new Vector3();
  const originN = center.clone().multiplyScalar(-1 / radius); // origen del GLB en la escena normalizada
  const camTarget = new Vector3();

  function resize() {
    const w = canvas.clientWidth, h = canvas.clientHeight;
    if (!w || !h) return;
    width = w; height = h; aspect = w / h;
    renderer.setPixelRatio(pixelCap);
    renderer.setSize(w, h, false);
    camera.aspect = aspect;
    camera.updateProjectionMatrix();
  }

  function fitDistance() {
    const half = MathUtils.degToRad(cur.fov / 2);
    const horizontal = Math.atan(Math.tan(half) * aspect);
    return 1 / Math.sin(Math.min(half, horizontal));
  }

  function applyExplode(e) {
    if (Math.abs(e - lastExplode) < 1e-4) return;
    lastExplode = e;
    for (const p of parts) {
      const k = smooth((e - p.delay) / 0.66);
      p.node.position.copy(p.base).addScaledVector(p.off, k);
    }
  }

  function applyOverlayKind() {
    const mat = kind === 'xray' ? xrayMat : wireMat;
    for (const o of overlays) { o.ov.material = mat; }
  }

  function updateFlight(w, dt, time) {
    if (!flight || w < 0.002) {
      rig.quaternion.identity();
      rig.position.y = reducedMotion ? 0 : Math.sin(time * 1.1) * 0.012;
      trail.visible = false;
      grid.material.uniforms.uOff.value.x = 0; grid.material.uniforms.uOff.value.y = 0;
      grid.position.y = groundY;
      return;
    }
    const { frames } = flight;
    const n = frames.length;
    const fi = ((flight.index % n) + n) % n;
    const i0 = Math.floor(fi), i1 = (i0 + 1) % n, t = fi - i0;
    const a = frames[i0], b = i1 === 0 ? a : frames[i1];
    const L = (k) => a[k] + (b[k] - a[k]) * t;
    const roll = MathUtils.degToRad(L(0)), pitch = MathUtils.degToRad(L(1));
    const yaw = MathUtils.degToRad(a[2]);
    // cabeceo positivo levanta el morro; alabeo positivo baja el lado derecho
    qPitch.setFromAxisAngle(right, pitch); qRoll.setFromAxisAngle(nose, roll);
    rig.quaternion.copy(qId).slerp(qPitch.multiply(qRoll), w);
    rig.position.y = 0;
    const north = L(3), east = L(4), alt = L(5);
    const cy = Math.cos(yaw), sy = Math.sin(yaw);
    // Estela tipo cometa: solo el último tramo recorrido (longitud acotada), fina y que se desvanece.
    // Posiciones anteriores relativas a la actual, en el marco del morro (adelante / derecha / arriba).
    let count = 0, length = 0;
    for (let k = 0; k < TRAIL_POINTS; k++) {
      const idx = i0 - k;
      if (idx < 0) break;
      const f = frames[idx];
      const dn = k === 0 ? 0 : f[3] - north, de = k === 0 ? 0 : f[4] - east, da = k === 0 ? 0 : f[5] - alt;
      const fwd = dn * cy + de * sy, rgt = -dn * sy + de * cy;
      trailPts[k].set(0, 0, 0).addScaledVector(nose, fwd * UNITS_PER_METRE).addScaledVector(right, rgt * UNITS_PER_METRE);
      trailPts[k].y = da * UNITS_PER_METRE;
      if (k > 0) length += trailPts[k].distanceTo(trailPts[k - 1]);
      trailLen[k] = length;
      count++;
      if (length > TRAIL_LENGTH) break;
    }
    for (let k = 0; k < count; k++) {
      const p = trailPts[k];
      const frac = Math.min(1, trailLen[k] / TRAIL_LENGTH);
      const tan = v.copy(trailPts[Math.min(count - 1, k + 1)]).sub(trailPts[Math.max(0, k - 1)]);
      const side = tan.cross(v2.copy(camera.position).sub(p)).normalize().multiplyScalar(0.013 * (1 - frac) + 0.0015);
      const o = k * 6;
      trailPos[o] = p.x + side.x; trailPos[o + 1] = p.y + side.y; trailPos[o + 2] = p.z + side.z;
      trailPos[o + 3] = p.x - side.x; trailPos[o + 4] = p.y - side.y; trailPos[o + 5] = p.z - side.z;
      // nace ya fuera del cuerpo del dron y se apaga hacia la cola
      const fade = Math.pow(1 - frac, 1.7) * Math.min(1, trailLen[k] / 0.5) * w;
      for (let j = 0; j < 2; j++) { trailCol[o + j * 3] = 0.55 * fade; trailCol[o + j * 3 + 1] = 0.86 * fade; trailCol[o + j * 3 + 2] = fade; }
    }
    trailGeo.setDrawRange(0, Math.max(0, count - 1) * 6);
    trailGeo.attributes.position.needsUpdate = true;
    trailGeo.attributes.color.needsUpdate = true;
    trail.visible = count > 2;
    const gf = north * cy + east * sy, gr = -north * sy + east * cy;
    grid.material.uniforms.uOff.value.x = -(nose.x * gf + right.x * gr) * UNITS_PER_METRE * w;
    grid.material.uniforms.uOff.value.y = -(nose.z * gf + right.z * gr) * UNITS_PER_METRE * w;
    grid.position.y = groundY - w * 1.9;
  }

  let last = performance.now();
  let time = 0;
  function frame(now) {
    if (!running) return;
    requestAnimationFrame(frame);
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    if (!visible || document.hidden) return;
    update(dt);
  }

  function update(dt) {
    time += dt;

    // calidad adaptativa: si los primeros cuadros van lentos, baja la resolución una vez
    if (measured < 120) {
      measured++;
      if (dt > 0.03) slowFrames++;
      if (measured === 120 && slowFrames > 50 && pixelCap > 1) { pixelCap = 1; resize(); }
    }

    const k = reducedMotion ? 1 : 1 - Math.exp(-dt * 5.5);
    for (const name in cur) cur[name] += (target[name] - cur[name]) * k;
    pcur.x += (pointer.x - pcur.x) * (reducedMotion ? 0 : 1 - Math.exp(-dt * 3));
    pcur.y += (pointer.y - pcur.y) * (reducedMotion ? 0 : 1 - Math.exp(-dt * 3));
    if (!dragging) { userAz += userVel; userVel *= 0.92; userAz *= 0.988; }

    // barrido: para cambiar de tipo (malla ↔ rayos X) primero se retira el actual
    const sweepGoal = kind !== kindTarget ? 0 : cur.sweep;
    sweep += (sweepGoal - sweep) * (reducedMotion ? 1 : 1 - Math.exp(-dt * 7));
    if (kind !== kindTarget && sweep < 0.02) { kind = kindTarget; applyOverlayKind(); }
    const on = sweep > 0.004;
    const cut = MathUtils.lerp(-1.25, 1.25, sweep);
    clip.w = on ? cut : -9;
    for (const o of overlays) { o.ov.visible = on; o.fill.visible = on && kind === 'wire'; }
    sheet.material.uniforms.uAlpha.value = on ? Math.min(1, 4 * sweep * (1 - sweep) * 1.6) : 0;
    sheet.visible = on && sweep < 0.996;
    sheet.position.copy(clipNormal).multiplyScalar(cut);

    applyExplode(cur.explode);
    updateFlight(cur.flight, dt, time);

    const spin = reducedMotion ? 0 : (cur.spin * 1.6 + cur.flight * 38 + cur.thermal * 9) * (1 - smooth(cur.explode * 1.6));
    propAngle += spin * dt;
    for (const p of props) p.pivot.rotation.y = propAngle * p.dir;

    shared.uThermal.value = cur.thermal;
    if (cur.thermal > 0.001) {
      heat.forEach((h, i) => {
        v.copy(h.p.localCenter); h.p.node.localToWorld(v);
        const pulse = 0.86 + 0.14 * Math.sin(time * 0.9 + h.phase);
        shared.uHeat.value[i].set(v.x, v.y, v.z, h.w * pulse);
      });
    }

    grid.material.uniforms.uAlpha.value = cur.grid * 0.42;

    // cámara
    const sway = reducedMotion ? 0 : Math.sin(time * 0.22) * cur.sway;
    const az = cur.az + sway + userAz + pcur.x * 0.16;
    const el = MathUtils.clamp(cur.el - pcur.y * 0.06, -0.2, 1.45);
    const d = cur.dist * fitDistance();
    camera.position.set(Math.sin(az) * Math.cos(el) * d, Math.sin(el) * d, Math.cos(az) * Math.cos(el) * d);
    camTarget.copy(originN).multiplyScalar(cur.aim);
    camera.position.add(camTarget);
    camera.lookAt(camTarget);
    camera.fov = cur.fov; // setViewOffset recalcula la proyección a continuación
    camera.setViewOffset(width, height, -cur.sx * width, -cur.sy * height, width, height);

    renderer.render(scene, camera);

    if (onAnchors && cur.labels > 0.02) {
      const out = anchors.map((a) => {
        v.copy(a.part.localCenter); a.part.node.localToWorld(v); v.project(camera);
        return { id: a.id, x: (v.x * 0.5 + 0.5) * width, y: (-v.y * 0.5 + 0.5) * height, hidden: v.z > 1 };
      });
      onAnchors(out, cur.labels);
    } else if (onAnchors) onAnchors(null, 0);
  }

  resize();
  applyOverlayKind();
  renderer.compile(scene, camera);
  requestAnimationFrame((t) => { last = t; frame(t); });

  return {
    resize,
    setState(next) {
      for (const name in next) {
        if (name === 'kind') kindTarget = next.kind;
        else if (name in target) target[name] = next[name];
      }
    },
    snap() { Object.assign(cur, target); sweep = target.sweep; kind = kindTarget; applyOverlayKind(); },
    setPointer(x, y) { pointer = { x, y }; },
    drag(dx, active) { dragging = active; if (active) { userAz += dx; userVel = dx; } },
    setFlight(frames) { flight = { frames, index: 0 }; },
    setFlightIndex(i) { if (flight) flight.index = i; },
    setVisible(vis) { visible = vis; },
    renderNow() { update(0.016); }, // un cuadro inmediato, sin esperar a requestAnimationFrame
    get state() { return cur; },
    /** Radio del modelo en metros (el GLB está en metros): la escena lo normaliza a 1. */
    radiusM: radius,
    /** Caja del modelo en metros, en el marco del GLB. */
    sizeM: size.toArray(),
    stats: { triangles: meshes.reduce((n, m) => n + (m.geometry.index ? m.geometry.index.count : m.geometry.attributes.position.count) / 3, 0), parts: parts.length },
    dispose() { running = false; renderer.dispose(); },
  };
}
