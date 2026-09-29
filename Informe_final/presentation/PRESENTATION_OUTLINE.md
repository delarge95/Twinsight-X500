# PRESENTATION_OUTLINE.md
# Estructura maestra de sustentación — TwinSight X500

**Estado:** canónico. Sincronizado con `index_final.html` (30 slides + B1–B11), `PRESENTATION_SCRIPT.md` y `SPEAKER_CARDS.md` v2026-09-29.
**Fecha de actualización:** 2026-09-29.
**Fuente académica autoritativa:** `awoodcocks.pdf` (informe final).

---

## 0. Archivos maestros del paquete de defensa

| Archivo | Función |
|---|---|
| `index_final.html` | **El deck.** 30 slides principales + 11 backups. |
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

**Numeración:** en todo el paquete, "slide N" es la posición en el deck, que coincide con el número visible de cada slide y con el contador "NN / 30". Los backups se numeran B1–B11.

---

## 1. Criterio rector

La defensa demuestra comprensión, criterio técnico, validez metodológica y honestidad de alcance. La estructura no es cronológica ni promocional — es **evaluativa**:

```
problema → brecha → propuesta → decisiones técnicas → implementación → evidencia → límites → contribución
```

Cada diapositiva tiene **una sola afirmación defendible**. El visual actúa como evidencia de esa afirmación, no como decoración. Los detalles que no caben en 30 minutos se reservan para los backups y la ronda de preguntas.

---

## 2. Tesis oral central

> TwinSight X500 transforma documentación técnica y activos 3D pesados del dron Holybro X500 V2 en un prototipo WebGL inspeccionable, desplegado como *visual product twin*. El aporte no es un gemelo digital operacional: es hacer legibles piezas, relaciones y modos de análisis visual desde el navegador, con evidencia técnica y evaluación formativa de usuarios reales.

---

## 3. Tres mensajes que el jurado debe llevarse

1. El problema no era falta de información, sino la dificultad de reconstruir mentalmente relaciones espaciales y funcionales desde documentación plana o CAD pesado.
2. La solución combina pipeline 3D, optimización WebGL, taxonomía semántica, UI de inspección y modos visuales para convertir un ensamblaje complejo en una experiencia técnica explorable.
3. La evidencia es favorable pero acotada: SUS alto en el prototipo 3D, NASA-TLX Raw y tiempos T1–T3 menores frente al soporte 2D, 96/96 registros de tareas completados en ambas condiciones (efecto techo), rendimiento documentado por dispositivo, y límites de alcance, muestra y compatibilidad declarados con precisión.

---

## 4. Distribución de tiempo

| Bloque | Slides | Tiempo | Función |
|---|---|---|---|
| Apertura y problema | 1–2 | 1:45 | Identidad, términos base y necesidad |
| Marco teórico y alcance | 3–5 | 3:25 | Fundar el argumento y delimitar el sistema |
| Objetivos, método e instrumentos | 6–8 | 2:55 | Establecer cómo se evalúa antes de mostrar resultados |
| Implementación técnica | 9–17 | 8:00 | Demostrar criterio de ingeniería multimedia |
| Demo | 18–19 | 3:20 | Demo en vivo en dos tramos: landing WebGL (scrollytelling) y app Unity; video de respaldo de 88 s |
| Resultados y discusión | 20–25 | 6:15 | Interpretar datos, no solo mostrarlos |
| Conclusiones y cierre | 26–30 | 2:50 | Objetivos, límites, trabajo futuro, contribución |
| **Total** | **30** | **28:30** | + 1:30 de margen = 30:00 |

---

## 5. Ruta principal de diapositivas

| # | Kicker | Título-sentencia (deck) | Tiempo | Clics | Claim que defiende | Evidencia / contenido en pantalla |
|---:|:---:|---|---|:---:|---|---|
| **1** | 01 | TwinSight X500 | 0:45 | 0 | Identidad y propuesta; definición de CAD y WebGL | Ficha del proyecto con URL de demo + captura del visor |
| **2** | 02 | El reto central es comprender relaciones, no solo ver piezas | 1:00 | 4 | La documentación plana exige reconstrucción espacial | Documentación → Fricción → Respuesta + Idea clave |
| **3** | 03 | La carga cognitiva explica por qué la forma de presentar importa | 1:05 | 4 | Marco teórico que justifica la interfaz guiada | Intrínseca / Extrínseca / Germana + Límite declarado |
| **4** | 04 | La interfaz traduce la teoría en jerarquía, agrupación, estado y reconocimiento | 1:10 | 4 | Principios de interfaz citados en el informe guían app y deck | Norman · Gestalt · Nielsen (×2), con Hutchins como marco |
| **5** | 05 | La tesis responde con un visual product twin, no con un digital twin operacional | 1:10 | 2 | Alcance preciso contra la sobrepromesa | Exclusiones DT (clic 1) · Alcance VPT (clic 2) |
| **6** | 06 | Los objetivos definen el contrato: construir y evaluar | 0:55 | 4 | La pregunta de investigación y los OE que la responden | Pregunta de investigación + tarjetas OE1–OE4 (OE2 "procurando" ≤ 33,33 ms) |
| **7** | 07 | La metodología usa DSR con validación formativa descriptiva | 0:50 | 7 | Método apropiado para un prototipo formativo | Ciclo DSRM (Peffers) + Hevner + n = 12 |
| **8** | 08 | Ninguna métrica mide todo: la evaluación triangula cinco capas | 1:10 | 6 | Cinco capas, ninguna cierra sola | KPIs (profiler) / SUS solo 3D / tareas T1–T4 (tiempos T1–T3) / Think-Aloud / NASA-TLX |
| **9** | 09 | De 6,5 millones a 95 617 triángulos: la traducción de activos CAD para WebGL | 1:10 | 8 | La preparación 3D es aporte técnico central | Flujo MoI3D/STEPper → Blender → Retopo → Bake normal/AO → FBX → WebGL |
| **10** | 10 | La escena runtime exportada es explorable | 0:25 | 2 | El resultado es real y manipulable | Visor three.js del GLB (252 mallas, ~229 000 tri) |
| **11** | 11 | La reducción geométrica se lee como presupuesto de activo, no como conteo runtime | 0:55 | 3 | 95 617 y 229 054 no son equivalentes | Activo base (masters) vs. conteo estimado por profiler |
| **12** | 12 | La arquitectura separa UI, orquestación, servicios de escena y datos | 1:05 | 4 | No es un visor aislado: capas coordinadas | Clases reales (Fig. 47): UIManager, EventBus, AppStateMachine, managers, DronePartData |
| **13** | 13 | La taxonomía permite seleccionar piezas madre, subpiezas, hotspots y fasteners | 0:55 | 7 | La interacción depende de estructura semántica | 28 piezas / 30 anchors / 257 renderers + hotspots, fasteners, bottom sheet |
| **14** | 14 | El flujo de usuario revela la complejidad del dron de forma progresiva | 0:50 | 7 | El alcance visible está cerrado | Hero → Explore → Selección → Bottom Sheet → herramientas |
| **15** | 15 | Inspect y Analyze eliminan el ruido visual para hacer legible el ensamblaje | 0:55 | 4 | Las herramientas reducen carga extrínseca | Columna Inspect → clip → columna Analyze → clip Explode |
| **16** | 16 | Los shaders son herramientas de inspección técnica, no filtros estéticos | 0:55 | 6 | Los modos son lecturas técnicas | Realistic / X-Ray / Solid / Thermal (+ Blueprint como preset) + clip |
| **17** | 17 | Thermal es una visualización heurística, no una simulación FEA calibrada | 0:50 | 2 | Límite técnico declarado | Límite + escala Estructura / ESC-electrónica / Motores-batería (Fig. 13) |
| **18** | 18 | La demo debe probar tres capacidades, no navegar improvisadamente | 0:20 | 5 | El jurado observa con criterios | Selección / Relación / Modo visual + ruta |
| **19** | 19 | Demo: de dron completo a pieza, relación y modo visual | 3:00 | 2 | La app realiza lo que el argumento promete | **Demo en vivo (3:00):** landing WebGL (ensamblaje, 6 capítulos, mini app) + app Unity desde «Abrir visor»; respaldo del tramo de la app: `vid_01_demo_compilado.mp4` (88 s) |
| **20** | 20 | El profiler interno vuelve trazable el rendimiento por escenario y dispositivo | 1:00 | 2 | Métricas ancladas a exportaciones reales | Extracto JSON literal del WebGLProfiler (4 jun 2026) |
| **21** | 21 | El rendimiento es viable, pero no universal en todo móvil | 1:00 | 5 | Lectura técnica honesta | Escritorio 59,8 · iPhone 17 Pro 58,7 · Redmi 26,5 · Adreno 610 17,6 FPS |
| **22** | 22 | SUS de 91,88: recepción favorable del visor interactivo 3D | 1:00 | 4 | SUS solo 3D, no comparación | 91,88 (mediana 95, DE 11,24) · rango 60–100 · referencia 68 |
| **23** | 23 | En la muestra, el visor 3D se asoció con menor carga de trabajo percibida | 1:05 | 5 | Comparación descriptiva, no causal | 8,69 vs 19,89 · Δ 11,19 · 12/12 · T1–T3 20,58 s vs 54 s · T4 no cronometrada |
| **24** | 24 | Think-Aloud explica comprensión espacial y fricciones residuales | 1:00 | 4 | La triangulación cualitativa da textura | Apoyo: 11/12, 8/12 · Fricción: navegación 10/12, iconos 6/12, piezas pequeñas 2/12 |
| **25** | 25 | La discusión acota el resultado: efecto techo, muestra pequeña y compatibilidad limitada | 1:10 | 2 | Se sabe qué demuestra el trabajo y qué no | Sí soporta / no debe afirmarse (efecto techo = completitud 96/96) |
| **26** | 26 | Las conclusiones cierran cada objetivo con evidencia trazable | 1:05 | 4 | Cada OE tiene resultado asociado | Tabla OE → resultado → evidencia |
| **27** | 27 | Las limitaciones son alcance declarado, no fallas ocultas | 0:40 | 5 | Cada límite es decisión documentada | Modelo único / n=12 / PC como adaptación / cables |
| **28** | 28 | El trabajo futuro es una ruta de madurez, no una lista de deseos | 0:30 | 6 | El roadmap parte de lo existente | Escalera + Fases 0–1 / 2–3 / 4–5 (Fig. 80) |
| **29** | 29 | La contribución es técnica, metodológica y comunicativa | 0:30 | 3 | Triple aporte | Tres columnas |
| **30** | 30 | Gracias. | 0:05 | 0 | Apertura a preguntas | "Preguntas del jurado" + URL de la demo |

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
| **B8** | Thermal: qué hace y qué no | Si Thermal puede evolucionar o qué significan los colores y los °C |
| **B9** | Límites metodológicos ampliados | Presión sobre la validez estadística |
| **B10** | Trabajo futuro por fases 0–1 / 2–3 / 4–5 | Qué requeriría el siguiente paso técnico |
| **B11** | Mobile UX: gestos, bottom sheet, rendimiento | Preguntas sobre la experiencia táctil (⚠ verificar "60 % de altura" y "filtrado anisotrópico": no están en el informe) |

---

## 7. Animaciones y microinteracciones

Las animaciones controlan la carga cognitiva — no adornan. Cada paso `data-step` del HTML es un clic y está marcado en el guion como `[click n]`.

| Uso | Dónde aplica | Forma |
|---|---|---|
| Construcción progresiva | Slides 3, 7, 8, 9, 12, 13, 14 | Una capa por clic |
| Comparación | Slides 5, 11, 25 | Dos columnas reveladas por turno |
| Foco en cifras | Slides 21–24 | Una cifra o categoría por clic; contadores animados |
| Microdemo embebida | Slides 15, 16 | Clips MP4 silenciosos de 12–15 s |
| Demo en vivo + respaldo | Slide 19 | Demo en la build; el video de 88 s espera en pausa en 0:00 y se reproduce con un clic |

**Regla:** si una animación requiere sincronización exacta con el guion memorizado, eliminarla.

---

## 8. Assets visuales por slide

### 8.1 Videos (carpeta `assets/video/`, 720×1280 a 30 fps)

| Archivo | Duración | Slide | Contenido real |
|---|---|---|---|
| `vid_01_demo_compilado.mp4` | 88 s | 19 (respaldo de la demo en vivo; `data-manual`, no arranca solo) | Selección → Isolate → ficha (0:00) · Power (0:17) · Explode (0:34) · Cut (0:56) · X-Ray (1:08) · Thermal (1:16) · Solid (1:22). Compilado de metraje real de la build. |
| `anim_01_inspect.mp4` | 15 s | 15 | Selección, aislamiento y ficha del soporte de riel y batería. |
| `anim_02_explode.mp4` | 14 s | 15 | Vista explosionada. |
| `anim_03_studio_shaders.mp4` | 12 s | 16 | X-Ray → Thermal (leyenda en °C) → Solid. |
| `anim_04_microinteracciones.mp4` | 11 s | B11 | Microinteracciones móviles. |

> Los videos no usan `autoplay`: el deck reproduce cada uno al entrar a su slide (o al revelar su paso), lo que evita decodificar los cinco a la vez al abrir el HTML. Los originales a 1080p/60 fps y los assets sin uso están archivados fuera del repositorio.

### 8.2 Imágenes (carpeta `assets/img/`) y modelo

- `ui04_hero_f.png` → Slide 1
- `x500v2_runtime_low.glb` (vía `drone_glb_datauri.js`) → Slide 10 (252 mallas, 229 070 triángulos)
- `ui02.jpg` → Slide 13
- `ui01.jpg` → Slide 14
- `app_02.jpg` → Slide 18
- `ui_analyze.png` → B11
- Slide 20: extracto de `Telemetria/Mediciones_WebGL/x500v2_perf_all_sessions_20260604_185230_webglplayer_chrome-141.0.0.0.json` (sesión `thermal_studio`), incrustado como texto.

> No usar `fig_profiler_internal_evidence.png` como evidencia del profiler interno: es el panel *Statistics* del editor de Unity (75,5 FPS, 232,1k triángulos) y sus cifras no corresponden a las del informe.

### 8.3 Diagramas construidos en el deck

| Diagrama | Slide | Fuente en el informe |
|---|---|---|
| Ciclo DSRM aplicado | 7 | Figura 14 |
| Triangulación de cinco capas | 8 | Figuras 19–20 |
| Pipeline CAD → WebGL | 9 | Figura 22, Tabla 5 |
| Arquitectura por capas | 12 | Figura 47 |
| Taxonomía 28/30/257 | 13 | Figura 21, Tabla 3 |
| Flujo público | 14 | Sección "Flujo Visible de la Build Final" |
| Escala térmica relativa | 17 | Figura 13 |
| FPS por dispositivo | 21 | Tabla 16 |
| Categorías Think-Aloud | 24 | Tabla 27, Figura 77 |
| Escalera de madurez y fases | 28 | Figuras 4 y 80 |

---

## 9. Control de coherencia — verificar antes de modificar cualquier slide

**Nunca decir o mostrar:**
- "Gemelo digital completo" o "digital twin operacional" para referirse a TwinSight.
- Thermal como simulación calibrada, diagnóstico o temperatura real (los °C de la leyenda son escala del modelo heurístico).
- "Telemetría" para referirse al profiler interno.
- T4 como tarea cronometrada.
- SUS como comparación 3D vs. 2D.
- NASA-TLX como medición directa de carga intrínseca, extrínseca o germana, o con lenguaje causal ("redujo").
- "Se redujo de 229 054 a 95 617 triángulos" · el visor de la slide 10 como "activo de 95 617".
- "Navegación y control" como fortaleza (en el informe es fricción, 10/12).
- Efecto techo definido con tiempos (es la completitud 96/96 en ambas condiciones).
- Nombres de clases que no existen en `desarrollo/unity_project/Assets/Scripts`.
- FPS, tiempos, pesos o reducciones que no estén en el informe o en `DEFENSE_EVIDENCE_MAP.md`.
- Módulos no integrados en la UI pública como parte del flujo evaluado.

---

## 10. Protocolo de ensayo

1. Ensayar la ruta oral corta con `SPEAKER_CARDS.md` — cronometrar.
2. Estudiar cada bloque con `DEFENSE_STUDY_GUIDE.md`.
3. Verificar fuentes y citas con `BIBLIOGRAPHY_EVIDENCE_ATLAS.md`.
4. Comprobar cada afirmación con `DEFENSE_EVIDENCE_MAP.md`.
5. Practicar objeciones con `JURY_QA_BANK.md`.
6. Ensayar la demo narrando sobre el video de 88 s con las marcas de tiempo de la slide 19.
7. Ensayo completo cronometrado 48 h antes — objetivo: 28:30, máximo 30:00.
8. Ensayo final 24 h antes — solo con `SPEAKER_CARDS.md` y sin leer el guion.
