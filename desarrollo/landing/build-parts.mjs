// Genera docs/landing/data/parts.json: los datos de la pieza interactiva de la landing (capítulo «Taxonomía»).
// Fuente única: docs/parts/sheets.json (fichas técnicas) y docs/parts/nodes.json (nodo del GLB → pieza madre).
// No se inventa ninguna cifra: lo que la ficha no trae se deja en null y la landing no lo muestra.
// Uso: node desarrollo/landing/build-parts.mjs
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../docs');
const read = (p) => JSON.parse(fs.readFileSync(path.join(root, p), 'utf8'));
const sheets = read('parts/sheets.json').entries;
const nodes = read('parts/nodes.json').nodes;

// Grupos seleccionables. `sheet` = ficha técnica de la que salen los datos; `short` = etiqueta corta del chip.
// `cat` reutiliza las categorías que la landing ya rotula (Aviónica, Propulsión, Estructura…).
// `qty` solo donde la propia ficha dice «cuatro posiciones»; para la tornillería se cuenta en nodes.json.
const GROUPS = [
  { id: 'fc', sheet: 'x500v2_pixhawk6c', short: { es: 'Controlador de vuelo', en: 'Flight controller' }, cat: { es: 'Aviónica', en: 'Avionics' } },
  { id: 'gps', sheet: 'x500v2_gps_m10', short: { es: 'GPS', en: 'GPS' }, cat: { es: 'Sensores y comunicaciones', en: 'Sensors & comms' } },
  { id: 'radio', sheet: 'x500v2_telemetry_radio', short: { es: 'Radio de telemetría', en: 'Telemetry radio' }, cat: { es: 'Sensores y comunicaciones', en: 'Sensors & comms' } },
  { id: 'motor', sheet: 'x500v2_motor', short: { es: 'Motores', en: 'Motors' }, cat: { es: 'Propulsión', en: 'Propulsion' }, qty: 4, name: { es: 'Motores 2216 KV920', en: 'Motors 2216 KV920' } },
  { id: 'prop', sheet: 'x500v2_prop', short: { es: 'Hélices', en: 'Propellers' }, cat: { es: 'Propulsión', en: 'Propulsion' }, qty: 4, name: { es: 'Hélices 1045', en: 'Propellers 1045' } },
  { id: 'battery', sheet: 'x500v2_battery', short: { es: 'Batería', en: 'Battery' }, cat: { es: 'Potencia', en: 'Power' } },
  { id: 'pm', sheet: 'x500v2_power_module', short: { es: 'Módulo de potencia', en: 'Power module' }, cat: { es: 'Distribución de potencia', en: 'Power distribution' } },
  { id: 'plate', sheet: 'x500v2_top_plate', short: { es: 'Placa superior', en: 'Top plate' }, cat: { es: 'Estructura', en: 'Airframe' } },
  { id: 'bottom', sheet: 'x500v2_bottom_plate', short: { es: 'Placa inferior', en: 'Bottom plate' }, cat: { es: 'Estructura', en: 'Airframe' } },
  { id: 'arm', sheet: 'x500v2_arm', short: { es: 'Brazos', en: 'Arms' }, cat: { es: 'Estructura', en: 'Airframe' }, qty: 4, name: { es: 'Brazos del chasis', en: 'Frame arms' } },
  { id: 'gear', sheet: 'x500v2_landing_gear', short: { es: 'Tren de aterrizaje', en: 'Landing gear' }, cat: { es: 'Estructura', en: 'Airframe' } },
  { id: 'rails', sheet: 'x500v2_rails_battery', short: { es: 'Rieles de batería', en: 'Battery rails' }, cat: { es: 'Estructura', en: 'Airframe' }, name: { es: 'Rieles y soporte de batería', en: 'Rails and battery mount' } },
  { id: 'fasteners', sheet: null, short: { es: 'Tornillería', en: 'Fasteners' }, cat: { es: 'Fijaciones', en: 'Fasteners' }, name: { es: 'Tornillería', en: 'Fasteners' },
    role: { es: 'Tornillos, tuercas y separadores que unen el ensamblaje.', en: 'Screws, nuts and standoffs that hold the assembly together.' } },
];

// Corrección puntual de traducciones que la ficha deja sin traducir (mismo dato, solo el idioma).
const DIM_EN = { '144 × 144 mm, 2 mm espesor': '144 × 144 mm, 2 mm thick' };
const MAT_EN = { 'PCB FR4, carcasa de aluminio anodizado': 'FR4 PCB, anodised aluminium housing', 'Polímero de litio (LiPo)': 'Lithium polymer (LiPo)' };

// Nodo del GLB → grupo. Los motores y las hélices viven dentro de los brazos en nodes.json; aquí se separan.
function groupOf(name, n) {
  if (n.k === 'fastener') return 'fasteners';
  if (n.e?.startsWith('x500v2_blend_dj_2216')) return 'motor';
  if (n.gi === 'x500v2_prop' || /propeller/.test(name)) return 'prop';
  const map = {
    x500v2_pixhawk6c: 'fc', x500v2_gps_m10: 'gps', x500v2_telemetry_radio: 'radio', x500v2_battery: 'battery',
    x500v2_power_module: 'pm', x500v2_top_plate: 'plate', x500v2_bottom_plate: 'bottom',
    x500v2_landing_gear: 'gear', x500v2_rails_battery: 'rails',
  };
  if (map[n.m]) return map[n.m];
  if (/^x500v2_arm_/.test(n.m)) return 'arm';
  return null;
}

// GLTFLoader quita puntos, corchetes y dos puntos de los nombres de nodo (PropertyBinding.sanitizeNodeName):
// las claves de nodeMap son los nombres tal como los ve three.js en la landing.
const sanitize = (name) => name.replace(/\s/g, '_').replace(/[\[\].:/]/g, '');
const nodeMap = {};
const count = {};
for (const [name, n] of Object.entries(nodes)) {
  const g = groupOf(name, n);
  if (!g) throw new Error('nodo sin grupo: ' + name);
  if (sanitize(name) in nodeMap) throw new Error('nombre repetido tras sanear: ' + name);
  nodeMap[sanitize(name)] = g;
  count[g] = (count[g] ?? 0) + 1;
}

const spec = (sheet, labelEn) => sheet.specifications.find((s) => s.label.en === labelEn)?.value ?? null;
const out = { version: 1, source: 'docs/parts/sheets.json + docs/parts/nodes.json', groups: [], nodes: nodeMap };
for (const g of GROUPS) {
  const sh = g.sheet ? sheets[g.sheet] : null;
  const dims = sh?.dimensions ? { es: sh.dimensions.es, en: DIM_EN[sh.dimensions.es] ?? sh.dimensions.en } : null;
  const mat0 = sh ? spec(sh, 'Material') : null;
  const mat = mat0 && { es: mat0.es, en: MAT_EN[mat0.en] ?? mat0.en };
  const massG = sh?.weight?.basis === 'project estimate' ? Math.round(sh.weight.value * 1000) : null;
  const sizes = g.id === 'fasteners'
    ? [...new Set(Object.values(sheets).filter((s) => s.kind === 'fastener_family').map((s) => spec(s, 'Metric size')?.es).filter(Boolean))].sort()
    : null;
  out.groups.push({
    id: g.id,
    sheet: g.sheet,
    short: g.short,
    name: g.name ?? sh.name,
    cat: g.cat,
    role: g.role ?? sh.identification[0].value,
    material: mat,
    dims,
    massG,
    qty: g.id === 'fasteners' ? count.fasteners : g.qty ?? null,
    sizes,
    meshes: count[g.id] ?? 0,
  });
}
const dest = path.join(root, 'landing/data/parts.json');
fs.writeFileSync(dest, JSON.stringify(out) + '\n');
console.log('escrito', path.relative(process.cwd(), dest), `· ${out.groups.length} grupos · ${Object.keys(nodeMap).length} nodos`, count);
