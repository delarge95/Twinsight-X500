// Servidor estático mínimo para previsualizar docs/ en local (la landing usa módulos ES y fetch).
// Uso: node desarrollo/landing/serve.mjs [puerto]
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../../docs');
const port = Number(process.argv[2] ?? process.env.PORT ?? 4340);
const types = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8', '.webmanifest': 'application/manifest+json',
  '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg', '.webp': 'image/webp', '.ico': 'image/x-icon',
  '.glb': 'model/gltf-binary', '.woff2': 'font/woff2', '.md': 'text/markdown; charset=utf-8', '.pdf': 'application/pdf',
  '.wasm': 'application/wasm', '.br': 'application/octet-stream',
};
http.createServer((req, res) => {
  // POST /__tools/save?name=x.png guarda una captura del lienzo en desarrollo/landing/.tmp/ (solo en local)
  if (req.method === 'POST' && req.url.startsWith('/__tools/save')) {
    const name = new URL(req.url, 'http://x').searchParams.get('name') ?? '';
    if (!/^[a-z0-9-]+\.png$/.test(name)) { res.writeHead(400).end('nombre no válido'); return; }
    const chunks = [];
    req.on('data', (c) => chunks.push(c));
    req.on('end', () => {
      fs.mkdirSync(path.join(here, '.tmp'), { recursive: true });
      fs.writeFileSync(path.join(here, '.tmp', name), Buffer.concat(chunks));
      res.writeHead(200, { 'content-type': 'text/plain' }).end('ok');
    });
    return;
  }
  let rel = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  if (rel.endsWith('/')) rel += 'index.html';
  // /__tools/* sirve las plantillas de desarrollo (no se publican): og.html, etc.
  const tools = rel.startsWith('/__tools/');
  const base = tools ? here : root;
  const file = path.join(base, tools ? rel.slice('/__tools'.length) : rel);
  if (!file.startsWith(base)) { res.writeHead(403).end(); return; }
  fs.stat(file, (err, st) => {
    if (err || !st.isFile()) { res.writeHead(404, { 'content-type': 'text/plain' }).end('404'); return; }
    res.writeHead(200, { 'content-type': types[path.extname(file).toLowerCase()] ?? 'application/octet-stream', 'cache-control': 'no-store' });
    fs.createReadStream(file).pipe(res);
  });
}).listen(port, '127.0.0.1', () => console.log(`landing en http://127.0.0.1:${port}/`));
