# Landing de TwinSight X500 (v2)

La página pública vive en `docs/` y GitHub Pages la publica tal cual desde `master` (sin paso de compilación).
Esta carpeta guarda solo las herramientas para regenerar sus assets; **no se publica**.

## Qué hay en `docs/`

| Ruta | Qué es |
|---|---|
| `index.html` | La landing. Español en el HTML; inglés en `landing/js/i18n.js`. |
| `app/` | La web app nueva (twinsight-web-twin, Three.js / R3F), compilada con `build-app.mjs`. |
| `parts/`, `models/` | Datos y modelo que la web app pide con ruta absoluta. La landing usa el mismo `models/holybro-x500.glb`. |
| `Build/` | Versión legacy: el visor Unity WebGL evaluado con usuarios. |
| `index-v1.html` | La landing anterior, conservada tal cual (enlazada desde el pie). |
| `docs.html` | Lector de los manuales de `manuals/*.md`. |
| `landing/css/` | `landing.css` (tokens alineados con `design/tokens.json` de la app web) y `docs.css`. |
| `landing/js/main.js` | Intro, idioma, scroll → estado 3D, rótulos, telemetría reproducida y portal de la app. |
| `landing/js/scene.js` | Escena three.js: cámara, explosión, barrido malla / rayos X, térmico ilustrativo, vuelo. |
| `landing/data/flight.json` | 60 s de un vuelo real (PX4 Flight Review `0f3c4a74…`) a 10 Hz. |
| `landing/vendor/` | three.js empaquetado y `marked` (MIT). Sin CDN, salvo Mermaid en el lector. |
| `og.jpg`, `favicon.*`, `apple-touch-icon.png`, `site.webmanifest`, `landing/img/` | Miniatura para compartir e iconos. |

## Cómo se abre la app («portal»)

Los botones con `data-open="app"` o `data-open="legacy"` no navegan: la landing se apaga, la cámara entra en el dron
y la app se carga en un `<iframe>` a pantalla completa dentro de la misma página (`#portal`). La pestaña del borde
izquierdo o el botón Atrás del navegador devuelven a la landing. `/#app` y `/#legacy` abren la app directamente.

Antes de abrir la web app, la landing escribe `tw.heroSeen=1` y `twinsight.lang` en `localStorage` para que la app
entre directa al visor y en el mismo idioma.

Las direcciones están al principio de `docs/landing/js/main.js`:

```js
const LINKS = {
  app: './app/',       // web app nueva
  legacy: './Build/',  // visor Unity WebGL
  desktop: '',         // URL de descarga del escritorio. Vacío → «próximamente», sin enlace
};
```

## Actualizar la web app publicada

```
node desarrollo/landing/build-app.mjs      # compila E:/twinsight-web-twin tal como está en disco → docs/app, docs/parts, docs/models
```

`docs/app/BUILD.txt` anota el commit y si había cambios sin confirmar. **Telemetría:** en GitHub Pages no hay
servidor WebSocket, así que la app muestra «Esperando datos…». Para que la sombra digital funcione hay que desplegar
el Worker de Cloudflare y compilar con `VITE_REPLAY_WS=wss://…/ws` en el entorno.

## Previsualizar en local

```
node desarrollo/landing/serve.mjs        # http://127.0.0.1:4340/
```

## Regenerar otros assets

```
node desarrollo/landing/build-vendor.mjs     # three.bundle.js (three + GLTFLoader + meshopt + RoomEnvironment)
node desarrollo/landing/export-flight.mjs    # flight.json desde data/x500v2_flight.twf de twinsight-web-twin
node desarrollo/landing/make-images.mjs      # favicon.ico, apple-touch-icon e iconos PWA
node desarrollo/landing/make-images.mjs --og # además og.jpg y el póster (ver los dos pasos dentro del script)
```

Las dependencias (`three`, `esbuild`, `sharp`, `vite`) se toman de `E:/twinsight-web-twin/node_modules`.

## Reglas de contenido y diseño

- Mínimo de texto: una idea por frase. Los detalles viven en la documentación, no en la landing.
- Color 60-30-10: fondo casi negro, superficies y texto neutros, y celeste solo como acento
  (botón primario, segunda línea de cada titular, luz del dron, estados activos). El naranja es solo marca.
- Ninguna cifra inventada: las métricas salen del informe final y de `docs/CONTEXTO.md` de la app web.
- Rotulado honesto: «sombra digital» para la repetición del vuelo real, «gemelo SITL» para PX4 + Gazebo,
  y «vista ilustrativa» para el térmico de esta página (el modelo real corre en las apps).
- La explosión de la landing es procedural (radial + capas); no usa los vectores del bake de Unity.
