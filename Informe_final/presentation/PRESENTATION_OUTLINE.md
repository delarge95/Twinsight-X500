# PRESENTATION_OUTLINE.md
# Estructura maestra de sustentación — TwinSight X500

**Estado:** canónico. Sincronizado con `index_final.html` (pantalla de espera + 21 slides + B1–B15), `PRESENTATION_SCRIPT.md` y `SPEAKER_CARDS.md` v2026-09-30.
**Fecha de actualización:** 2026-09-30.
**Fuente académica autoritativa:** `awoodcocks.pdf` (informe final).

---

## 0. Archivos maestros del paquete de defensa

| Archivo | Función |
|---|---|
| `index_final.html` | **El deck.** Pantalla de espera (slide 0) + 21 slides principales + 15 backups. |
| `PRESENTATION_OUTLINE.md` | **Este documento.** Estructura, tiempos y assets por slide. |
| `PRESENTATION_SCRIPT.md` | Guion oral maestro con tiempos, clics, transiciones y frases defensibles. |
| `SPEAKER_CARDS.md` | Tarjetas orales comprimidas para ensayo cronometrado. |
| `DEFENSE_STUDY_GUIDE.md` | Estudio por bloques con explicación simple, técnica, evidencia y preguntas difíciles. |
| `BIBLIOGRAPHY_EVIDENCE_ATLAS.md` | Atlas de fuentes con citas breves verificadas y límites de uso. |
| `DEFENSE_EVIDENCE_MAP.md` | Trazabilidad entre slides, informe, anexos, app, README, manuales y bibliografía. |
| `JURY_QA_BANK.md` | Banco de preguntas con respuestas cortas, ampliadas, evidencia y alertas. |
| `DEMO_SCRIPT.md` | Recorrido de demo coherente con la UI pública real. |
| `ASSETS_REQUIREMENTS.md` | Lista de capturas, videos, GIFs, diagramas y prioridades. |
| `audits/` | Auditorías de layout, color, tipografía, storytelling, interacción, rigor, lenguaje e imágenes. |

**Numeración:** en todo el paquete, "slide N" es la posición en el deck, que coincide con el número visible de cada slide y con el contador "NN / 21". Los backups se numeran B1–B11.

---

## 1. Criterio rector

La defensa demuestra comprensión, criterio técnico, validez metodológica y honestidad de alcance. La estructura no es cronológica ni promocional — es **evaluativa**:

```
problema → brecha → propuesta → decisiones técnicas → implementación → evidencia → límites → contribución
```

Cada diapositiva tiene **una sola afirmación defendible**. El visual actúa como evidencia de esa afirmación, no como decoración. Los detalles que no caben en 30 minutos se reservan para los backups y la ronda de preguntas.

---

## 2. Tesis oral central

> TwinSight X500 lleva la documentación técnica y el CAD del dron Holybro X500 V2 a una app WebGL que se puede inspeccionar en el navegador. Es un *visual product twin*: representa las piezas, cómo se relacionan y distintas formas de mirarlas. No se conecta al dron real. Viene con evidencia técnica y una evaluación formativa con doce personas.

---

## 3. Tres mensajes que el jurado debe llevarse

1. La información existía. Lo difícil era reconstruir en la cabeza cómo se relacionan las piezas a partir de documentos planos o CAD pesado.
2. La solución combina pipeline 3D, optimización WebGL, taxonomía semántica, UI de inspección y modos visuales para convertir un ensamblaje complejo en una experiencia técnica explorable.
3. La evidencia es favorable pero acotada: SUS alto en el prototipo 3D, NASA-TLX Raw y tiempos T1–T3 menores frente al soporte 2D, 96/96 registros de tareas completados en ambas condiciones (efecto techo), rendimiento documentado por dispositivo, y límites de alcance, muestra y compatibilidad declarados con precisión.

---

## 4. Distribución de tiempo

| Bloque | Slides | Tiempo | Función |
|---|---|---|---|
| Pantalla de espera | 0 | — | Se proyecta antes de empezar; → la cierra, P la reabre |
| Apertura y problema | 1–2 | 1:40 | Presentación, términos base y necesidad |
| Teoría y alcance | 3–4 | 2:15 | Carga cognitiva (con la idea de Hutchins) y visual product twin |
| Objetivos y evaluación | 5–6 | 1:30 | Objetivos, DSR en una frase, cinco fuentes |
| Construcción | 7–12 | 5:45 | Pipeline, arquitectura, taxonomía, flujo, capturas móviles, Thermal |
| Demo | 13 | 3:00 | En vivo: landing WebGL y app Unity; video de respaldo de 88 s |
| Resultados y discusión | 14–18 | 5:40 | Rendimiento, SUS, NASA-TLX, Think-Aloud, discusión |
| Conclusiones y cierre | 19–21 | 2:10 | Objetivos y aporte, trabajo futuro, cierre |
| **Total** | **21** | **22:00** | Con el ritmo medido en ensayo, ~29–30 min |

> **Recorte del 2026-09-30.** Salieron de la ruta principal: visor 3D y geometría (B6), principios de interfaz (B12), metodología (B13), profiler (B5), limitaciones (B14) y aporte (B15). Inspect/Analyze y Studio se unieron en una slide con las tres capturas móviles. Arquitectura, taxonomía y evaluación se acortaron. La demo en vivo mantiene sus 3:00.

---

## 5. Ruta principal de diapositivas

| # | Título (deck) | Tiempo | Clics | Qué defiende | En pantalla |
|---:|---|---|:---:|---|---|
| **0** | Pantalla de espera | — | → | Presentación visual antes de empezar | Dron texturizado: encendido, despegue, vuelo, aterrizaje |
| **1** | TwinSight X500 | 0:40 | 0 | Qué es el proyecto; CAD y WebGL | Ficha del proyecto + captura |
| **2** | El reto es entender cómo se relacionan las piezas del dron | 1:00 | 4 | La documentación plana exige reconstrucción espacial | Documentación → Fricción → Respuesta + Idea clave |
| **3** | La carga cognitiva explica por qué la forma de presentar importa | 1:15 | 4 | Marco teórico de la interfaz | Intrínseca / Extrínseca / Germana + límite |
| **4** | La tesis responde con un visual product twin, no con un digital twin operacional | 1:00 | 2 | Alcance preciso | Exclusiones (clic 1) · alcance (clic 2) |
| **5** | Los objetivos definen el contrato: construir y evaluar | 0:55 | 4 | Pregunta, objetivos y método | Pregunta + OE1–OE4 |
| **6** | Ninguna métrica mide todo: la evaluación triangula cinco capas | 0:35 | 6 | Cinco fuentes, ninguna sola | Triangulación + lista |
| **7** | Al teselar el CAD aparecieron 6,5 millones de triángulos; el activo para WebGL usa 95 617 | 1:15 | 8 | El CAD no tiene triángulos hasta teselarse | Flujo + cifras por ruta (Tabla 28) |
| **8** | Los módulos se hablan por un bus de eventos | 0:50 | 1 | Cómo funciona la arquitectura | Cuatro grupos visibles + ejemplo PartSelectedEvent |
| **9** | La taxonomía le dice a la app qué tocó el usuario; la tornillería exigió un sistema propio | 1:15 | 7 | Taxonomía, hotspots y tornillo modular | 28/30/257 + 425 208 → 14 408 + piezas base |
| **10** | El detalle aparece cuando el usuario lo pide; los modos están siempre a mano | 0:45 | 7 | Barra de modos siempre visible | Flujo con barra fija + captura |
| **11** | Tres modos, tres preguntas sobre el mismo dron | 0:30 | 3 | Inspect, Analyze y Studio en el móvil | Tres capturas en video |
| **12** | Thermal estima el calor con un modelo físico simplificado, sin llegar a FEA | 1:10 | 2 | Base física y límites de Thermal | Cómo calcula + diagrama + límite |
| **13** | Demo: de dron completo a pieza, relación y modo visual | 3:00 | 2 | La app hace lo que se dijo | **Demo en vivo:** landing + app; respaldo `vid_01_demo_compilado.mp4` (88 s) |
| **14** | El rendimiento es viable, pero no universal en todo móvil | 1:00 | 5 | Lectura honesta del rendimiento | 59,8 · 58,7 · 26,5 · 17,6 FPS |
| **15** | SUS de 91,88: recepción favorable del visor interactivo 3D | 1:15 | 4 | SUS solo 3D, y cómo se calcula | 91,88 · cálculo · 60–100 · 68 |
| **16** | En la muestra, el visor 3D se asoció con menor carga de trabajo percibida | 1:25 | 5 | NASA-TLX Raw y tiempos | 8,69 vs 19,89 · tiempos · cálculo |
| **17** | Think-Aloud explica comprensión espacial y fricciones residuales | 0:55 | 4 | Por qué ocurrió el patrón | 11/12, 8/12 · 10/12, 6/12, 2/12 |
| **18** | La discusión acota el resultado | 1:05 | 2 | Qué se afirma y qué no | Dos columnas (96/96 = efecto techo) |
| **19** | Las conclusiones cierran cada objetivo con evidencia trazable | 1:20 | 4 | Cada objetivo con resultado + aporte | Tabla por objetivo |
| **20** | El siguiente paso es conectar el modelo a datos reales, por fases | 0:45 | 6 | Ruta desde lo construido + cierre narrativo | Escalera + fases 0–5 |
| **21** | Gracias. | 0:05 | 0 | Preguntas | "Preguntas del jurado" + URL |

---

## 6. Slides de respaldo (Backups)

| # | Tema | Cuándo usar |
|---|---|---|
| **B1** | Fórmula SUS y lectura de la referencia 68 | Interpretación del puntaje o comparación 3D/2D |
| **B2** | Fórmula NASA-TLX Raw y rendimiento invertido | Validez del instrumento o escala invertida |
| **B3** | Diseño comparativo: muestra, orden AB/BA, variables de control, condición 2D, T4 | Validez interna o confusores |
| **B4** | Rendimiento por dispositivo (FPS y frame time) | Dato de FPS no mostrado en la ruta principal |
| **B5** | Profiler interno con el extracto JSON real (antes slide principal) | Trazabilidad de las métricas |
| **B6** | 95 617 vs. 229 054 triángulos | Si vuelve la confusión activo base / escena runtime |
| **B7** | Arquitectura ampliada con clases reales (UI, Core, Scene, Data, Measurement) | Detalles de implementación en Unity |
| **B8** | Thermal: ecuaciones del modelo, escalas por material y límites | Si preguntan de dónde salen los valores, qué significan los °C o cómo se llevaría a simulación real |
| **B9** | Límites metodológicos ampliados | Presión sobre la validez estadística |
| **B10** | Trabajo futuro por fases 0–1 / 2–3 / 4–5 | Qué requeriría el siguiente paso técnico |
| **B12** | Principios de interfaz: Norman, Gestalt, Nielsen, Hutchins | Si preguntan por el fundamento del diseño |
| **B13** | Metodología DSR en seis fases | Si preguntan por el método |
| **B14** | Limitaciones una por una | Si piden detallar límites |
| **B15** | Aporte técnico, metodológico y comunicativo | Si preguntan por la contribución |
| **B11** | Mobile UX: gestos, bottom sheet, rendimiento | Preguntas sobre la experiencia táctil (⚠ verificar "60 % de altura" y "filtrado anisotrópico": no están en el informe) |

---

## 7. Animaciones y microinteracciones

Las animaciones controlan la carga cognitiva — no adornan. Cada paso `data-step` del HTML es un clic y está marcado en el guion como `[click n]`.

| Uso | Dónde aplica | Forma |
|---|---|---|
| Construcción progresiva | Slides 2, 3, 6, 7, 9, 10 | Una capa por clic |
| Comparación | Slides 4, 18 | Dos columnas reveladas por turno |
| Foco en cifras | Slides 14–17 | Una cifra o categoría por clic; contadores animados |
| Microdemo embebida | Slide 11 | Tres clips MP4 silenciosos, uno por clic |
| Demo en vivo + respaldo | Slide 13 | Demo en la build; el video de 88 s espera en pausa en 0:00 y se reproduce con un clic |

**Regla:** si una animación requiere sincronización exacta con el guion memorizado, eliminarla.

---

## 8. Assets visuales por slide

### 8.1 Videos (carpeta `assets/video/`, 720×1280 a 30 fps)

| Archivo | Duración | Slide | Contenido real |
|---|---|---|---|
| `vid_01_demo_compilado.mp4` | 88 s | 13 (respaldo de la demo en vivo; `data-manual`, no arranca solo) | Selección → Isolate → ficha (0:00) · Power (0:17) · Explode (0:34) · Cut (0:56) · X-Ray (1:08) · Thermal (1:16) · Solid (1:22). Compilado de metraje real de la build. |
| `anim_01_inspect.mp4` | 15 s | 11 | Selección, aislamiento y ficha del soporte de riel y batería. |
| `anim_02_explode.mp4` | 14 s | 11 | Vista explosionada. |
| `anim_03_studio_shaders.mp4` | 12 s | 11 | X-Ray → Thermal (leyenda en °C) → Solid. |
| `anim_04_microinteracciones.mp4` | 11 s | B11 | Microinteracciones móviles. |

> Los videos no usan `autoplay`: el deck reproduce cada uno al entrar a su slide (o al revelar su paso), lo que evita decodificar los cinco a la vez al abrir el HTML. Los originales a 1080p/60 fps y los assets sin uso están archivados fuera del repositorio.

### 8.2 Imágenes (carpeta `assets/img/`) y modelo

- `ui04_hero_f.png` → Slide 1
- `preshow_glb_datauri.js` → Slide 0 (escena final con el atlas horneado: color, normales, rugosidad)
- `x500v2_runtime_low.glb` / `drone_glb_datauri.js` → sin uso tras retirar el visor de la antigua slide 10
- `fasteners_modular.jpg` → Slide 9 (composición de las Figs. 28 y 29 del informe)
- `ui01.jpg` → Slide 10
- `ui02.jpg`, `app_02.jpg` → sin uso en la ruta principal (se conservan para preguntas)
- `ui_analyze.png` → B11
- B5: extracto de `Telemetria/Mediciones_WebGL/x500v2_perf_all_sessions_20260604_185230_webglplayer_chrome-141.0.0.0.json` (sesión `thermal_studio`), incrustado como texto.

> No usar `fig_profiler_internal_evidence.png` como evidencia del profiler interno: es el panel *Statistics* del editor de Unity (75,5 FPS, 232,1k triángulos) y sus cifras no corresponden a las del informe.

### 8.3 Diagramas construidos en el deck

| Diagrama | Slide | Fuente en el informe |
|---|---|---|
| Ciclo DSRM aplicado | B13 | Figura 14 |
| Triangulación de cinco capas | 6 | Figuras 19–20 |
| Pipeline CAD → WebGL | 7 | Figura 22, Tabla 5 |
| Arquitectura y ejemplo de evento | 8 | Figuras 47–48 |
| Taxonomía 28/30/257 y tornillo modular | 9 | Figura 21, Tabla 3, Tabla 8, Figs. 28–29 |
| Flujo público | 10 | Sección "Flujo Visible de la Build Final" |
| Modelo térmico (fuente → vecina → aire) | 12 | Modelo matemático del subsistema térmico, Fig. 66 |
| FPS por dispositivo | 14 | Tabla 16 |
| Categorías Think-Aloud | 17 | Tabla 27, Figura 77 |
| Escalera de madurez y fases | 20 | Figuras 4 y 80 |

---

## 9. Control de coherencia — verificar antes de modificar cualquier slide

**Nunca decir o mostrar:**
- "Gemelo digital completo" o "digital twin operacional" para referirse a TwinSight.
- Thermal como simulación calibrada, diagnóstico o temperatura medida (los °C son la salida del modelo por componentes). Tampoco como "valores inventados": el modelo tiene base física (fuentes por carga, conducción por área/longitud/material, convección).
- "Telemetría" para referirse al profiler interno.
- T4 como tarea cronometrada.
- SUS como comparación 3D vs. 2D.
- NASA-TLX como medición directa de carga intrínseca, extrínseca o germana, o con lenguaje causal ("redujo").
- "Se redujo de 229 054 a 95 617 triángulos" .
- Que la barra Inspect · Analyze · Studio aparece solo al seleccionar una pieza (está siempre visible).
- Que el CAD "tiene" 6,5 millones de triángulos (los triángulos aparecen al teselar).
- "Navegación y control" como fortaleza (en el informe es fricción, 10/12).
- Efecto techo definido con tiempos (es la completitud 96/96 en ambas condiciones).
- Nombres de clases que no existen en `desarrollo/unity_project/Assets/Scripts`.
- FPS, tiempos, pesos o reducciones que no estén en el informe o en `DEFENSE_EVIDENCE_MAP.md` (las cifras de tornillería 425 208 / 14 408 están documentadas allí con su archivo de origen).
- Módulos no integrados en la UI pública como parte del flujo evaluado.

---

## 10. Protocolo de ensayo

1. Ensayar la ruta oral corta con `SPEAKER_CARDS.md` — cronometrar.
2. Estudiar cada bloque con `DEFENSE_STUDY_GUIDE.md`.
3. Verificar fuentes y citas con `BIBLIOGRAPHY_EVIDENCE_ATLAS.md`.
4. Comprobar cada afirmación con `DEFENSE_EVIDENCE_MAP.md`.
5. Practicar objeciones con `JURY_QA_BANK.md`.
6. Ensayar la demo narrando sobre el video de 88 s con las marcas de tiempo de la slide 13.
7. Ensayo completo cronometrado 48 h antes — objetivo: 22:00 según guion; con tu ritmo real, máximo 30:00.
8. Ensayo final 24 h antes — solo con `SPEAKER_CARDS.md` y sin leer el guion.
