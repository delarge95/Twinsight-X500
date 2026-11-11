// Genera los iconos y la imagen para compartir de la landing.
//   node desarrollo/landing/make-images.mjs            → iconos (favicon.ico, apple-touch-icon, PWA)
//   node desarrollo/landing/make-images.mjs --og       → además og.jpg y el póster del héroe
// --og necesita el servidor local en marcha (node desarrollo/landing/serve.mjs), Microsoft Edge y las capas
// del dron guardadas antes desde un navegador con GPU (ver el bloque --og más abajo).
// sharp se toma de E:/twinsight-web-twin/node_modules (o de la ruta que se pase con --modules=).
import { createRequire } from 'node:module';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
const docs = path.resolve(here, '../../docs');
const arg = (name, fallback) => process.argv.find((a) => a.startsWith(`--${name}=`))?.split('=')[1] ?? fallback;
const modules = path.resolve(arg('modules', 'E:/twinsight-web-twin/node_modules'));
const sharp = createRequire(path.join(modules, 'x.js'))('sharp');
const img = path.join(docs, 'landing/img');
fs.mkdirSync(img, { recursive: true });

const MARK = `<g fill="none" stroke="#e6edf3" stroke-width="6" stroke-linecap="round"><path d="M28 28 L72 72 M72 28 L28 72"/><circle cx="24" cy="24" r="15"/><circle cx="76" cy="24" r="15"/><circle cx="24" cy="76" r="15"/><circle cx="76" cy="76" r="15"/></g><rect x="40" y="38" width="20" height="26" rx="6" fill="#e6edf3"/><path d="M50 24 L43 38 L57 38 Z" fill="#46afff"/>`;
const rounded = fs.readFileSync(path.join(docs, 'favicon.svg'));
// fondo a sangre (iOS y Android recortan el icono con su propia máscara)
const bleed = (scale) => Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><rect width="100" height="100" fill="#0b0f14"/><g transform="translate(50 50) scale(${scale}) translate(-50 -50)">${MARK}</g></svg>`);
const png = (svg, size) => sharp(svg, { density: 72 * (size / 100) * 4 }).resize(size, size).png({ compressionLevel: 9 }).toBuffer();

const out = async (file, buf) => { fs.writeFileSync(file, buf); console.log(path.relative(docs, file), buf.length, 'bytes'); };
await out(path.join(docs, 'apple-touch-icon.png'), await png(bleed(0.78), 180));
await out(path.join(img, 'icon-192.png'), await png(rounded, 192));
await out(path.join(img, 'icon-512.png'), await png(rounded, 512));
await out(path.join(img, 'icon-maskable-512.png'), await png(bleed(0.6), 512));

// favicon.ico con PNG incrustados (16, 32, 48)
const sizes = [16, 32, 48];
const pngs = await Promise.all(sizes.map((s) => png(rounded, s)));
const header = Buffer.alloc(6 + 16 * sizes.length);
header.writeUInt16LE(0, 0); header.writeUInt16LE(1, 2); header.writeUInt16LE(sizes.length, 4);
let offset = header.length;
sizes.forEach((s, i) => {
  const e = 6 + 16 * i;
  header.writeUInt8(s, e); header.writeUInt8(s, e + 1); header.writeUInt8(0, e + 2); header.writeUInt8(0, e + 3);
  header.writeUInt16LE(1, e + 4); header.writeUInt16LE(32, e + 6);
  header.writeUInt32LE(pngs[i].length, e + 8); header.writeUInt32LE(offset, e + 12);
  offset += pngs[i].length;
});
await out(path.join(docs, 'favicon.ico'), Buffer.concat([header, ...pngs]));

if (process.argv.includes('--og')) {
  // 1) Abrir en un navegador con GPU (con el servidor local en marcha) para guardar las capas del dron:
  //      http://127.0.0.1:4340/__tools/og.html?layer=drone&save=og-drone
  //      http://127.0.0.1:4340/__tools/og.html?poster=1&save=poster
  // 2) Este paso captura fondo y texto con Edge sin interfaz y compone el resultado.
  const edge = arg('edge', 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe');
  const origin = arg('origin', 'http://127.0.0.1:4340');
  const tmp = path.join(here, '.tmp');
  fs.mkdirSync(tmp, { recursive: true });
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'twinsight-og-'));
  const textShot = path.join(tmp, 'og-text.png');
  fs.rmSync(textShot, { force: true });
  spawn(edge, [
    '--headless=new', `--user-data-dir=${profile}`, '--no-first-run', '--hide-scrollbars', '--force-device-scale-factor=1',
    '--window-size=1200,630', '--virtual-time-budget=8000', `--screenshot=${textShot}`, `${origin}/__tools/og.html?layer=text`,
  ], { stdio: 'ignore', detached: true }).unref();
  for (let i = 0; i < 60 && !fs.existsSync(textShot); i++) await new Promise((r) => setTimeout(r, 500));
  await new Promise((r) => setTimeout(r, 800));
  if (!fs.existsSync(textShot)) throw new Error('Edge no generó la captura de texto');
  const drone = path.join(tmp, 'og-drone.png');
  if (!fs.existsSync(drone)) throw new Error('Falta .tmp/og-drone.png (ver paso 1)');
  const layer = await sharp(drone).resize(1200, 630).png().toBuffer();
  // JPEG: una imagen para compartir debe pesar poco (algunas apps descartan las que pasan de ~300 KB)
  await out(path.join(docs, 'og.jpg'), await sharp(textShot).resize(1200, 630).composite([{ input: layer }]).jpeg({ quality: 88, mozjpeg: true, chromaSubsampling: '4:4:4' }).toBuffer());
  const poster = path.join(tmp, 'poster.png');
  if (fs.existsSync(poster)) await out(path.join(img, 'poster.webp'), await sharp(poster).resize(1600, 1000).webp({ quality: 80, alphaQuality: 90 }).toBuffer());
}
