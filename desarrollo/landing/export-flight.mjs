// Extrae un tramo del vuelo real (data/x500v2_flight.twf de twinsight-web-twin) a un JSON compacto
// para la demostración de telemetría de la landing. No inventa datos: solo remuestrea y redondea.
// Uso: node desarrollo/landing/export-flight.mjs [ruta al .twf] [segundos]
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const src = process.argv[2] ?? 'E:/twinsight-web-twin/data/x500v2_flight.twf';
const seconds = Number(process.argv[3] ?? 60);
const buf = fs.readFileSync(src);
if (buf.toString('latin1', 0, 4) !== 'TWF1') throw new Error('no es TWF1');
const hlen = buf.readUInt32LE(4);
const header = JSON.parse(buf.toString('utf8', 8, 8 + hlen));
const stride = header.fields.length;
const rows = new Float32Array(buf.buffer, buf.byteOffset + 8 + hlen, header.frame_count * stride);
const c = Object.fromEntries(header.fields.map((f, i) => [f, i]));
const at = (k, name) => rows[k * stride + c[name]];
const rate = header.rate_hz;
const n = header.frame_count;

// Ventana en vuelo con más movimiento horizontal (suma de |v| horizontal).
const win = Math.round(seconds * rate);
let best = 0, bestScore = -1;
const speed = new Float32Array(n);
for (let k = 0; k < n; k++) speed[k] = at(k, 'landed') > 0.5 ? -5 : Math.hypot(at(k, 'vx'), at(k, 'vy'));
let acc = 0;
for (let k = 0; k < n; k++) {
  acc += speed[k];
  if (k >= win) acc -= speed[k - win];
  if (k >= win - 1 && acc > bestScore) { bestScore = acc; best = k - win + 1; }
}

const step = Math.round(rate / 10); // 10 Hz
const deg = 180 / Math.PI;
const r = (x, d) => Math.round(x * d) / d;
const frames = [];
for (let k = best; k < best + win; k += step) {
  const qw = at(k, 'qw'), qx = at(k, 'qx'), qy = at(k, 'qy'), qz = at(k, 'qz');
  const roll = Math.atan2(2 * (qw * qx + qy * qz), 1 - 2 * (qx * qx + qy * qy)) * deg;
  const pitch = Math.asin(Math.max(-1, Math.min(1, 2 * (qw * qy - qz * qx)))) * deg;
  const yaw = Math.atan2(2 * (qw * qz + qx * qy), 1 - 2 * (qy * qy + qz * qz)) * deg;
  frames.push([
    r(roll, 100), r(pitch, 100), r(yaw, 10),
    r(at(k, 'x'), 100), r(at(k, 'y'), 100), r(-at(k, 'z'), 100),
    r(Math.hypot(at(k, 'vx'), at(k, 'vy'), at(k, 'vz')), 100),
    r(at(k, 'vbat'), 100), r(at(k, 'rem'), 1000),
    r(at(k, 'u1'), 1000), r(at(k, 'u2'), 1000), r(at(k, 'u3'), 1000), r(at(k, 'u4'), 1000),
  ]);
}
const out = {
  note: 'Tramo de un vuelo real de un Holybro X500 V2 (registro público de PX4 Flight Review), remuestreado a 10 Hz. Posición local NED en metros; altura = -z.',
  source: header.source,
  vehicle: header.vehicle,
  rate_hz: 10,
  start_s: r(at(best, 't'), 100),
  fields: ['roll_deg', 'pitch_deg', 'yaw_deg', 'north_m', 'east_m', 'alt_m', 'speed_ms', 'vbat', 'rem', 'u1', 'u2', 'u3', 'u4'],
  frames,
};
const dst = path.resolve(here, '../../docs/landing/data/flight.json');
fs.writeFileSync(dst, JSON.stringify(out));
const col = (i) => frames.map((f) => f[i]);
const mm = (a) => [Math.min(...a), Math.max(...a)];
console.log('ok', dst, fs.statSync(dst).size, 'bytes', frames.length, 'frames, inicio', out.start_s, 's');
console.log('roll', mm(col(0)), 'pitch', mm(col(1)), 'alt', mm(col(5)), 'vel', mm(col(6)), 'vbat', mm(col(7)), 'u1', mm(col(9)));
