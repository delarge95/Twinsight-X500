// Compila la web app (twinsight-web-twin, Three.js / R3F) dentro de la landing, en docs/app/.
//   node desarrollo/landing/build-app.mjs [ruta al repo de la web app]
// La app pide /parts/*.json y /models/*.glb con ruta absoluta, así que esas dos carpetas se publican
// en la raíz del sitio (docs/parts, docs/models). El GLB es el mismo que usa la landing: se descarga una vez.
// No modifica el repo de la web app: solo lee su código y escribe en docs/.
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import fs from 'node:fs';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
const docs = path.resolve(here, '../../docs');
const repo = path.resolve(process.argv[2] ?? 'E:/twinsight-web-twin');
const out = path.join(docs, 'app');

const vite = path.join(repo, 'node_modules/vite/bin/vite.js');
const res = spawnSync(process.execPath, [vite, 'build', '--base=./', '--outDir', out, '--emptyOutDir'], { cwd: repo, stdio: 'inherit' });
if (res.status !== 0) process.exit(res.status ?? 1);

for (const dir of ['parts', 'models']) {
  const from = path.join(out, dir), to = path.join(docs, dir);
  fs.rmSync(to, { recursive: true, force: true });
  fs.renameSync(from, to);
}
fs.rmSync(path.join(out, 'og.png'), { force: true }); // la imagen para compartir es la de la landing

const rev = spawnSync('git', ['-C', repo, 'rev-parse', '--short', 'HEAD'], { encoding: 'utf8' }).stdout.trim();
const dirty = spawnSync('git', ['-C', repo, 'status', '--porcelain', '--untracked-files=no'], { encoding: 'utf8' }).stdout.trim() !== '';
fs.writeFileSync(path.join(out, 'BUILD.txt'), `twinsight-web-twin ${rev}${dirty ? ' + cambios sin confirmar' : ''}\ncompilado ${new Date().toISOString()}\n`);
console.log(`ok: docs/app (${rev}${dirty ? ', con cambios locales' : ''}), docs/parts, docs/models`);
