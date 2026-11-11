// Empaqueta three.js (solo lo que usa la landing) en un único módulo ES minificado.
// Uso: node desarrollo/landing/build-vendor.mjs [ruta a un node_modules con three y esbuild]
// Por defecto toma las dependencias de E:/twinsight-web-twin/node_modules (misma versión que la app web).
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
const modules = path.resolve(process.argv[2] ?? 'E:/twinsight-web-twin/node_modules');
const require = createRequire(path.join(modules, 'x.js'));
const esbuild = require('esbuild');
const three = JSON.parse((await import('node:fs')).readFileSync(path.join(modules, 'three/package.json'), 'utf8'));

const out = path.resolve(here, '../../docs/landing/vendor/three.bundle.js');
await esbuild.build({
  entryPoints: [path.join(here, 'vendor-entry.mjs')],
  bundle: true,
  format: 'esm',
  minify: true,
  target: 'es2020',
  nodePaths: [modules],
  legalComments: 'none',
  banner: { js: `/* three.js r${three.version.split('.')[1]} (MIT) + GLTFLoader + MeshoptDecoder + RoomEnvironment. Generado por desarrollo/landing/build-vendor.mjs */` },
  outfile: out,
});
console.log('ok', out);
