# PRESENTATION_OUTLINE.md
# Estructura maestra de sustentación — TwinSight X500

**Estado:** canónico. Sincronizado con `index_final.html` (29 slides + B1–B11), `PRESENTATION_SCRIPT.md` y `SPEAKER_CARDS.md` v2026-09-29.
**Fecha de actualización:** 2026-09-29.
**Fuente académica autoritativa:** `awoodcocks.pdf` (informe final).

---

## 0. Archivos maestros del paquete de defensa

| Archivo | Función |
|---|---|
| `index_final.html` | **El deck.** 29 slides principales + 11 backups. |
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

**Numeración:** en todo el paquete, "slide N" es la posición en el deck, que coincide con el número visible de cada slide y con el contador "NN / 29". Los backups se numeran B1–B11.

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
| Apertura y problema | 1–2 | 1:40 | Presentación, términos base y necesidad |
| Marco teórico y alcance | 3–5 | 3:05 | Fundar el argumento y delimitar el sistema |
| Objetivos, método e instrumentos | 6–8 | 2:40 | Cómo se evalúa, antes de los resultados |
| Construcción | 9–17 | 8:35 | Pipeline, arquitectura, taxonomía, modos y Thermal |
| Demo | 18 | 3:00 | En vivo: landing WebGL y app Unity; video de respaldo de 88 s |
| Resultados y discusión | 19–24 | 6:20 | Rendimiento, SUS, NASA-TLX, Think-Aloud, discusión |
| Conclusiones y cierre | 25–29 | 3:10 | Objetivos, límites, trabajo futuro, aporte |
| **Total** | **29** | **28:30** | + 1:30 de margen = 30:00 |

> La antigua slide 18 ("La demo debe probar tres capacidades") se eliminó: repetía la ruta que ya muestra la slide de demo. Sus 20 s pasaron a explicar tornillería, Thermal, SUS y NASA-TLX.

---

## 5. Ruta principal de diapositivas

| # | Título (deck) | Tiempo | Clics | Qué defiende | En pantalla |
|---:|---|---|:---:|---|---|
| **1** | TwinSight X500 | 0:40 | 0 | Qué es el proyecto; CAD y WebGL | Ficha del proyecto + captura |
| **2** | El reto es entender cómo se relacionan las piezas del dron | 1:00 | 4 | La documentación plana exige reconstrucción espacial | Documentación → Fricción → Respuesta + Idea clave |
| **3** | La carga cognitiva explica por qué la forma de presentar importa | 1:00 | 4 | Marco teórico de la interfaz | Intrínseca / Extrínseca / Germana + límite |
| **4** | La interfaz traduce la teoría en jerarquía, agrupación, estado y reconocimiento | 1:05 | 4 | Principios citados en el informe | Norman · Gestalt · Nielsen (×2), Hutchins como marco |
| **5** | La tesis responde con un visual product twin, no con un digital twin operacional | 1:00 | 2 | Alcance preciso | Exclusiones (clic 1) · alcance (clic 2) |
| **6** | Los objetivos definen el contrato: construir y evaluar | 0:50 | 4 | Pregunta y objetivos | Pregunta + OE1–OE4 |
| **7** | La metodología usa DSR con validación formativa descriptiva | 0:45 | 7 | Método adecuado a un prototipo | Ciclo DSRM + n = 12 |
| **8** | Ninguna métrica mide todo: la evaluación triangula cinco capas | 1:05 | 6 | Cinco fuentes, ninguna sola | Triangulación + lista |
| **9** | Al teselar el CAD aparecieron 6,5 millones de triángulos; el activo para WebGL usa 95 617 | 1:15 | 8 | El CAD no tiene triángulos hasta teselarse; el pipeline conserva las piezas | Flujo + por qué optimizar + cifras por ruta (Tabla 28) |
| **10** | La escena runtime exportada es explorable | 0:25 | 2 | El resultado es real | Visor three.js (252 mallas, ~229 000 tri) |
| **11** | 95 617 y 229 054 triángulos miden dos cosas distintas | 0:45 | 3 | Modelo vs. escena en ejecución | Dos cifras + cómo leerlas |
| **12** | Los módulos se hablan por un bus de eventos: quien avisa no sabe quién escucha | 1:10 | 5 | Cómo funciona la arquitectura | Cuatro grupos + ejemplo PartSelectedEvent |
| **13** | La taxonomía le dice a la app qué tocó el usuario; la tornillería exigió un sistema propio | 1:30 | 7 | Taxonomía, hotspots, BOM y tornillo modular | 28/30/257 + hotspots + 425 208 → 14 408 + piezas base |
| **14** | El detalle aparece cuando el usuario lo pide; los modos están siempre a mano | 0:45 | 7 | Barra de modos siempre visible; ficha bajo demanda | Flujo con barra fija + captura |
| **15** | Inspect y Analyze eliminan el ruido visual para hacer legible el ensamblaje | 0:50 | 4 | Herramientas de lectura | Inspect, Analyze + dos clips |
| **16** | Cada modo de Studio responde una pregunta distinta sobre el mismo dron | 0:45 | 6 | Los modos son lecturas técnicas | Cuatro modos + clip |
| **17** | Thermal estima el calor con un modelo físico simplificado, sin llegar a FEA | 1:10 | 2 | Base física y límites de Thermal | Cómo calcula + diagrama fuente → vecina → aire + límite |
| **18** | Demo: de dron completo a pieza, relación y modo visual | 3:00 | 2 | La app hace lo que se dijo | **Demo en vivo:** landing + app; respaldo `vid_01_demo_compilado.mp4` (88 s) |
| **19** | El profiler interno vuelve trazable el rendimiento por escenario y dispositivo | 0:45 | 2 | Métricas con fuente | Extracto JSON del WebGLProfiler |
| **20** | El rendimiento es viable, pero no universal en todo móvil | 0:55 | 5 | Lectura honesta del rendimiento | 59,8 · 58,7 · 26,5 · 17,6 FPS |
| **21** | SUS de 91,88: recepción favorable del visor interactivo 3D | 1:15 | 4 | SUS solo 3D, y cómo se calcula | 91,88 · cálculo · 60–100 · 68 |
| **22** | En la muestra, el visor 3D se asoció con menor carga de trabajo percibida | 1:25 | 5 | Comparación descriptiva y cómo se calcula NASA-TLX Raw | 8,69 vs 19,89 · tiempos · cálculo |
| **23** | Think-Aloud explica comprensión espacial y fricciones residuales | 0:55 | 4 | Por qué ocurrió el patrón | 11/12, 8/12 · 10/12, 6/12, 2/12 |
| **24** | La discusión acota el resultado: efecto techo, muestra pequeña y compatibilidad limitada | 1:05 | 2 | Qué se afirma y qué no | Dos columnas (96/96 = efecto techo) |
| **25** | Las conclusiones cierran cada objetivo con evidencia trazable | 1:05 | 4 | Cada objetivo con resultado | Tabla por objetivo |
| **26** | Cuatro límites marcan dónde valen estos resultados | 0:40 | 5 | Alcance de validez | Modelo único / n=12 / PC / cables |
| **27** | El siguiente paso es conectar el modelo a datos reales, por fases | 0:40 | 6 | Ruta desde lo construido | Escalera + fases 0–5 (Fig. 80) |
| **28** | La contribución es técnica, metodológica y comunicativa | 0:40 | 3 | Aporte | Tres columnas |
| **29** | Gracias. | 0:05 | 0 | Preguntas | "Preguntas del jurado" + URL |

---

## 6. Slides de respaldo (Backups)

| # | Tema | Cuándo usar |
|---|---|---|
| **B1** | Fórmula SUS y lectura de la referencia 68 | Interpretación del puntaje o comparación 3D/2D |
| **B2** | Fórmula NASA-TLX Raw y rendimiento invertido | Validez del instrumento o escala invertida |
| **B3** | Diseño comparativo: muestra, orden AB/BA, variables de control, condición 2D, T4 | Validez interna o confusores |
| **B4** | Rendimiento por dispositivo (FPS y frame time) | Dato de FPS no mostrado en la ruta principal |
| **B5** | Profiler interno: exportación JSON/CSV y reproducibilidad | Trazabilidad de las métricas |
| **B6** | 95 617 vs. 229 054 triángulos | Si vuelve la confusión activo base / escena runtime |
| **B7** | Arquitectura ampliada con clases reales (UI, Core, Scene, Data, Measurement) | Detalles de implementación en Unity |
| **B8** | Thermal: ecuaciones del modelo, escalas por material y límites | Si preguntan de dónde salen los valores, qué significan los °C o cómo se llevaría a simulación real |
| **B9** | Límites metodológicos ampliados | Presión sobre la validez estadística |
| **B10** | Trabajo futuro por fases 0–1 / 2–3 / 4–5 | Qué requeriría el siguiente paso técnico |
| **B11** | Mobile UX: gestos, bottom sheet, rendimiento | Preguntas sobre la experiencia táctil (⚠ verificar "60 % de altura" y "filtrado anisotrópico": no están en el informe) |

---

## 7. Animaciones y microinteracciones

Las animaciones controlan la carga cognitiva — no adornan. Cada paso `data-step` del HTML es un clic y está marcado en el guion como `[click n]`.

| Uso | Dónde aplica | Forma |
|---|---|---|
| Construcción progresiva | Slides 3, 7, 8, 9, 12, 13, 14 | Una capa por clic |
| Comparación | Slides 5, 11, 24 | Dos columnas reveladas por turno |
| Foco en cifras | Slides 20–23 | Una cifra o categoría por clic; contadores animados |
| Microdemo embebida | Slides 15, 16 | Clips MP4 silenciosos de 12–15 s |
| Demo en vivo + respaldo | Slide 18 | Demo en la build; el video de 88 s espera en pausa en 0:00 y se reproduce con un clic |

**Regla:** si una animación requiere sincronización exacta con el guion memorizado, eliminarla.

---

## 8. Assets visuales por slide

### 8.1 Videos (carpeta `assets/video/`, 720×1280 a 30 fps)

| Archivo | Duración | Slide | Contenido real |
|---|---|---|---|
| `vid_01_demo_compilado.mp4` | 88 s | 18 (respaldo de la demo en vivo; `data-manual`, no arranca solo) | Selección → Isolate → ficha (0:00) · Power (0:17) · Explode (0:34) · Cut (0:56) · X-Ray (1:08) · Thermal (1:16) · Solid (1:22). Compilado de metraje real de la build. |
| `anim_01_inspect.mp4` | 15 s | 15 | Selección, aislamiento y ficha del soporte de riel y batería. |
| `anim_02_explode.mp4` | 14 s | 15 | Vista explosionada. |
| `anim_03_studio_shaders.mp4` | 12 s | 16 | X-Ray → Thermal (leyenda en °C) → Solid. |
| `anim_04_microinteracciones.mp4` | 11 s | B11 | Microinteracciones móviles. |

> Los videos no usan `autoplay`: el deck reproduce cada uno al entrar a su slide (o al revelar su paso), lo que evita decodificar los cinco a la vez al abrir el HTML. Los originales a 1080p/60 fps y los assets sin uso están archivados fuera del repositorio.

### 8.2 Imágenes (carpeta `assets/img/`) y modelo

- `ui04_hero_f.png` → Slide 1
- `x500v2_runtime_low.glb` (vía `drone_glb_datauri.js`) → Slide 10 (252 mallas, 229 070 triángulos)
- `fasteners_modular.jpg` → Slide 13 (composición de las Figs. 28 y 29 del informe)
- `ui01.jpg` → Slide 14
- `ui02.jpg`, `app_02.jpg` → sin uso en la ruta principal (se conservan para preguntas)
- `ui_analyze.png` → B11
- Slide 19: extracto de `Telemetria/Mediciones_WebGL/x500v2_perf_all_sessions_20260604_185230_webglplayer_chrome-141.0.0.0.json` (sesión `thermal_studio`), incrustado como texto.

> No usar `fig_profiler_internal_evidence.png` como evidencia del profiler interno: es el panel *Statistics* del editor de Unity (75,5 FPS, 232,1k triángulos) y sus cifras no corresponden a las del informe.

### 8.3 Diagramas construidos en el deck

| Diagrama | Slide | Fuente en el informe |
|---|---|---|
| Ciclo DSRM aplicado | 7 | Figura 14 |
| Triangulación de cinco capas | 8 | Figuras 19–20 |
| Pipeline CAD → WebGL | 9 | Figura 22, Tabla 5 |
| Arquitectura y ejemplo de evento | 12 | Figuras 47–48 |
| Taxonomía 28/30/257 y tornillo modular | 13 | Figura 21, Tabla 3, Tabla 8, Figs. 28–29 |
| Flujo público | 14 | Sección "Flujo Visible de la Build Final" |
| Modelo térmico (fuente → vecina → aire) | 17 | Modelo matemático del subsistema térmico, Fig. 66 |
| FPS por dispositivo | 20 | Tabla 16 |
| Categorías Think-Aloud | 23 | Tabla 27, Figura 77 |
| Escalera de madurez y fases | 27 | Figuras 4 y 80 |

---

## 9. Control de coherencia — verificar antes de modificar cualquier slide

**Nunca decir o mostrar:**
- "Gemelo digital completo" o "digital twin operacional" para referirse a TwinSight.
- Thermal como simulación calibrada, diagnóstico o temperatura medida (los °C son la salida del modelo por componentes). Tampoco como "valores inventados": el modelo tiene base física (fuentes por carga, conducción por área/longitud/material, convección).
- "Telemetría" para referirse al profiler interno.
- T4 como tarea cronometrada.
- SUS como comparación 3D vs. 2D.
- NASA-TLX como medición directa de carga intrínseca, extrínseca o germana, o con lenguaje causal ("redujo").
- "Se redujo de 229 054 a 95 617 triángulos" · el visor de la slide 10 como "activo de 95 617".
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
6. Ensayar la demo narrando sobre el video de 88 s con las marcas de tiempo de la slide 18.
7. Ensayo completo cronometrado 48 h antes — objetivo: 28:30, máximo 30:00.
8. Ensayo final 24 h antes — solo con `SPEAKER_CARDS.md` y sin leer el guion.
