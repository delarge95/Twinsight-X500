# Landing de TwinSight X500 (v2)

La página pública vive en `docs/` y GitHub Pages la publica tal cual desde `master` (sin paso de compilación).
Esta carpeta guarda solo las herramientas para regenerar sus assets; **no se publica**.

## Qué hay en `docs/`

| Ruta | Qué es |
|---|---|
| `index.html` | La landing nueva. Español en el HTML; inglés en `landing/js/i18n.js`. |
| `index-v1.html` | La landing anterior, conservada tal cual (enlazada desde el pie). |
| `docs.html` | Lector de los manuales de `manuals/*.md` (antes era un modal de la landing). |
| `landing/css/` | `landing.css` (tokens alineados con `design/tokens.json` de la app web) y `docs.css`. |
| `landing/js/main.js` | Idioma, enlaces de producto, scroll → estado 3D, rótulos, telemetría reproducida. |
| `landing/js/scene.js` | Escena three.js: cámara, explosión, barrido malla / rayos X, térmico ilustrativo, vuelo. |
| `landing/models/x500.glb` | El mismo GLB optimizado de la app web (78 974 triángulos, meshopt + WebP). |
| `landing/data/flight.json` | 60 s de un vuelo real (PX4 Flight Review `0f3c4a74…`) a 10 Hz. |
| `landing/vendor/` | three.js empaquetado y `marked` (MIT). Sin CDN, salvo Mermaid en el lector. |
| `og.jpg`, `favicon.*`, `apple-touch-icon.png`, `site.webmanifest`, `landing/img/` | Miniatura para compartir e iconos. |

## Enlaces de producto (lo único que hay que tocar al publicar las apps)

Al principio de `docs/landing/js/main.js`:

```js
const LINKS = {
  webApp: '',          // URL de la web app en Three.js. Vacío → los botones abren el visor Unity WebGL (./Build/)
  viewer: './Build/',
  desktop: '',         // URL de descarga del escritorio. Vacío → el botón dice «próximamente» y no enlaza
};
```

Los textos de los botones, la nota del héroe y los estados de las tarjetas cambian solos.

## Previsualizar en local

```
node desarrollo/landing/serve.mjs        # http://127.0.0.1:4340/
```

## Regenerar assets

```
node desarrollo/landing/build-vendor.mjs     # three.bundle.js (three + GLTFLoader + meshopt + RoomEnvironment)
node desarrollo/landing/export-flight.mjs    # flight.json desde data/x500v2_flight.twf de twinsight-web-twin
node desarrollo/landing/make-images.mjs      # favicon.ico, apple-touch-icon e iconos PWA
node desarrollo/landing/make-images.mjs --og # además og.jpg y el póster (ver los dos pasos dentro del script)
```

Las dependencias (`three`, `esbuild`, `sharp`) se toman de `E:/twinsight-web-twin/node_modules`.

## Reglas de contenido

- Ninguna cifra inventada: las métricas salen del informe final y de `docs/CONTEXTO.md` de la app web.
- Rotulado honesto: «sombra digital» para la repetición del vuelo real, «gemelo SITL» para PX4 + Gazebo,
  y «vista ilustrativa» para el térmico de esta página (el modelo real corre en las apps).
- La explosión de la landing es procedural (radial + capas); no usa los vectores del bake de Unity.
