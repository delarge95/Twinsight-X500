# ASSETS_REQUIREMENTS.md
# Inventario final de assets del deck — TwinSight X500

**Estado:** cerrado. Reemplaza el plan de producción de junio de 2026 (deck de 24 slides), que ya no aplica.
**Fecha:** 2026-09-29.
**Regla:** todo asset visible debe provenir de la build real, del informe o de las exportaciones del profiler. No se muestran módulos ocultos o no integrados (Measurement, BOM, anotaciones, conexiones, Wireframe, Ghosted).

---

## 1. Imágenes (`assets/img/`)

| Archivo | Resolución | Slide | Contenido |
|---|---|---|---|
| `ui04_hero_f.png` | 960×540 | 1 | Visor TwinSight con el dron completo. |
| `hero01.jpg` | 1920×1080 | 10 | Póster del visor 3D mientras carga el GLB. |
| `fasteners_modular.jpg` | 2695×520 | 13 | Piezas base del tornillo modular (tres cabezas, vuelta de rosca, punta) y un tornillo armado. Composición de las Figs. 28 y 29 del informe. |
| `ui02.jpg` | 1600×900 | — | Hotspots «select a part». Sin uso en la ruta principal; se conserva para preguntas. |
| `ui01.jpg` | 1600×900 | 14 | Selección de pieza, herramientas Analyze y navegación Inspect/Analyze/Studio. |
| `app_02.jpg` | 1600×900 | — | Dron ensamblado con herramientas Analyze. Sin uso tras eliminar la antigua slide 18. |
| `ui_analyze.png` | 960×540 | B11 | Herramientas Analyze (Cut · Explode · Filter). |

## 2. Videos (`assets/video/`, H.264 720×1280 a 30 fps, sin audio)

| Archivo | Duración | Slide | Contenido |
|---|---|---|---|
| `anim_01_inspect.mp4` | 15 s | 15 | Selección → aislamiento → ficha del soporte de riel y batería. |
| `anim_02_explode.mp4` | 14 s | 15 | Vista explosionada. |
| `anim_03_studio_shaders.mp4` | 12 s | 16 | X-Ray → Thermal (leyenda en °C del modelo por componentes) → Solid. |
| `anim_04_microinteracciones.mp4` | 11 s | B11 | Microinteracciones móviles. |
| `vid_01_demo_compilado.mp4` | 88 s | 18 | **Respaldo de la demo en vivo.** 0:00 selección → Isolate → ficha · 0:17 Power · 0:34 Explode · 0:56 Cut · 1:08 X-Ray · 1:16 Thermal · 1:22 Solid. No arranca solo: se reproduce con un clic. |

Los videos no usan `autoplay`: el deck reproduce cada uno al entrar a su slide (o al revelar su paso), lo que evita decodificarlos todos al abrir el HTML.

## 3. Modelo 3D y librerías

| Archivo | Uso |
|---|---|
| `assets/model/drone_glb_datauri.js` | GLB embebido como data URI; permite abrir el deck con `file://` sin servidor (slide 10). |
| `assets/model/x500v2_runtime_low.glb` | Mismo modelo (252 mallas, 229 070 triángulos), usado si no está la versión embebida. Es la escena runtime exportada, **no** el activo base de 95 617 triángulos. |
| `assets/vendor/three.min.js`, `GLTFLoader.js` | Visor three.js de la slide 10. |

## 4. Evidencia incrustada como texto

| Slide | Fuente |
|---|---|
| 19 | Extracto literal de `Telemetria/Mediciones_WebGL/x500v2_perf_all_sessions_20260604_185230_webglplayer_chrome-141.0.0.0.json` (sesión `thermal_studio`: 59,8 FPS, 16,7 ms, 413 MB, 252 renderers, 229 054 triángulos). |

## 5. Assets descartados

- `Informe_final/figures/screenshots_contextual/fig_profiler_internal_evidence.png`: es el panel *Statistics* del editor de Unity (75,5 FPS, 232,1k triángulos); sus cifras no corresponden a las del informe y no debe usarse como evidencia del profiler interno.
- Los originales a 1080p/60 fps, el video de demo original de 96 s (sin aislamiento ni ficha abierta), las texturas sueltas y las copias de respaldo de imágenes quedaron archivados fuera del repositorio (`scratch/presentacion_archivo_2026-09-29/`).
