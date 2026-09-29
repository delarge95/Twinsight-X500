# SPEAKER_CARDS.md
# Tarjetas orales de sustentación — TwinSight X500

**Estado:** operativo para ensayo cronometrado. Sincronizado con `index_final.html` (30 slides) y `PRESENTATION_SCRIPT.md`.
**Fecha:** 2026-09-29.
**Duración meta:** 28:30 min + 1:30 min de margen = 30:00 total.
**Fuente canónica:** `awoodcocks.pdf` (informe final).
**Numeración:** posición en el deck (contador inferior del HTML). El kicker visible de cada slide va una unidad por detrás (ver tabla en `PRESENTATION_SCRIPT.md` §2).

---

## Regla de uso

Estas tarjetas son la versión oral comprimida del guion maestro. Si el tiempo se aprieta, **estas frases tienen prioridad** sobre `PRESENTATION_SCRIPT.md`. Cada tarjeta debe poder decirse sin leer. La columna "Clics" indica cuántas veces pulsar → antes de avanzar de slide.

---

## Tarjetas

| Slide | Tiempo | Clics | **Tesis oral — lo que debe quedar claro** | Evidencia visible en pantalla | Riesgo crítico a evitar |
|---|---|---|---|---|---|
| **1** | 0:00–0:45 | 0 | "TwinSight X500 convierte un ensamblaje disperso en planos, manuales y CAD pesado en una experiencia web explorable. CAD y WebGL, definidos desde el inicio." | Título, ficha del proyecto con URL de la demo, captura del visor. | Abrir con "es un gemelo digital". |
| **2** | 0:45–1:45 | 4 | "El problema no es falta de información: es la distancia entre la información disponible y la comprensión espacial del sistema." | Filas Documentación → Fricción → Respuesta + Idea clave. | Caricaturizar el 2D como inútil. |
| **3** | 1:45–2:55 | 4 | "La carga cognitiva explica por qué la forma de presentar importa. La tesis la usa como marco, no la mide." | Intrínseca / Extrínseca / Germana + Límite declarado. | Decir que NASA-TLX mide carga intrínseca o germana. |
| **4** | 2:55–4:05 | 4 | "La interfaz es un artefacto cognitivo (Hutchins). Aplica jerarquía visual (Norman), agrupación (Gestalt), visibilidad del estado y reconocimiento antes que memoria (Nielsen)." | Tabla: principio / en la app / en este deck. | Presentar los principios como evidencia empírica · citar autores fuera de la bibliografía del informe. |
| **5** | 4:05–5:15 | 2 | "TwinSight es un visual product twin: sin telemetría, sin sincronización física, sin FEA. Promete hacer legible el dron, no operarlo." | Columna de exclusiones (clic 1) y columna de alcance (clic 2). | "Gemelo digital completo" o "Thermal calcula temperatura real". |
| **6** | 5:15–6:10 | 4 | "Cuatro objetivos verificables: pipeline, materiales y rendimiento, prototipo, evaluación. Se evaluó un artefacto construido." | Tarjetas OE1–OE4. | Decir que se "aseguró" 30 FPS en todo dispositivo (el objetivo dice "procurando"). |
| **7** | 6:10–7:15 | 7 | "Ciclo DSRM de Peffers en seis fases, rigor de Hevner. Evaluación formativa y descriptiva: eso es rigor." | Seis cajas del ciclo + marco DSR + formativa/descriptiva. | "Se probó causalidad". |
| **8** | 7:15–8:35 | 6 | "Cinco capas: KPIs del profiler, SUS solo 3D, tareas (completitud T1–T4, tiempos T1–T3), Think-Aloud, NASA-TLX por condición." | Diagrama de triangulación + lista de capas. | Mezclar SUS con la comparación 3D/2D · llamar "telemetría" al profiler. |
| **9** | 8:35–9:50 | 8 | "El pipeline traduce CAD de manufactura a runtime: de más de 6,5 millones a 95 617 triángulos, conservando la jerarquía seleccionable." | Flujo MoI3D/STEPper → Blender → Retopo → Bake (normal/AO) → FBX → WebGL. | "Optimizar fue solo bajar polígonos". |
| **10** | 9:50–10:20 | 2 | "Esto es la escena runtime exportada: 252 mallas, ~229 000 triángulos. No es la cifra del título anterior." | Visor 3D rotable con Wireframe; etiqueta "GLB · 229 070 tri". | Decir que el visor muestra el activo de 95 617. |
| **11** | 10:20–11:15 | 3 | "95 617 = activo base y sus masters. 229 054 = conteo estimado por el profiler de la escena instrumentada. No son equivalentes." | Dos cifras + criterio de interpretación. | "Se redujo de 229 054 a 95 617". |
| **12** | 11:15–12:25 | 4 | "Cuatro capas del informe: UI, orquestación (EventBus + AppStateMachine), servicios de escena, datos (DronePartData)." | Capas con clases reales del código. | Nombrar clases que no existen · "es solo un visor". |
| **13** | 12:25–13:25 | 7 | "28 piezas canónicas → 30 anchors → 257 renderers y colliders. Tornillos modulares, resto de sujetadores con proxies." | Cifras 28/30/257 + Hotspots, Fasteners, Bottom sheet + captura. | Decir "28 categorías" · confundir con BOM certificada. |
| **14** | 13:25–14:25 | 7 | "El flujo revela la complejidad por etapas: Explore → selección → ficha → herramientas. Dato y forma en el mismo plano." | Diagrama de flujo + revelado por etapas + ruido excluido + captura. | Presentar módulos no integrados como parte del flujo. |
| **15** | 14:25–15:25 | 4 | "Inspect responde qué pieza es y dónde está; Analyze, cómo se conecta. Son herramientas de lectura, no efectos." | Columna Inspect → su clip → columna Analyze → clip de Explode. | Explode como ensamblaje físicamente exacto. |
| **16** | 15:25–16:25 | 6 | "Cada modo responde una pregunta: Realistic reconoce, X-Ray muestra lo interno, Solid la forma, Thermal jerarquías relativas. Blueprint es preset." | Cuatro modos + clip X-Ray → Thermal → Solid. | Prometer que los shaders simulan física. |
| **17** | 16:25–17:15 | 2 | "Thermal es heurístico: modelo reducido por componentes; los °C son la escala del modelo, no medición. No es FEA." | Límite declarado + escala Estructura / ESC-electrónica / Motores-batería. | Leer los °C como temperatura real · "Thermal diagnostica". |
| **18** | 17:15–17:45 | 5 | "Observen tres cosas: selección, relación y modo visual." | Tres capacidades + ruta + captura. | Improvisar el recorrido. |
| **19** | 17:45–19:25 | 2 | "Demo EN VIVO: selección → Isolate → ficha · Power · Explode · Cut · X-Ray → Thermal → Solid (≤ 1:40)." | Ruta con marcas de tiempo; video de respaldo en pausa (clic para reproducir). | Pasar de 10 s sin respuesta de la build: volver al deck y reproducir el video sin comentarlo. |
| **20** | 19:25–20:25 | 2 | "El profiler exporta cada sesión con build, dispositivo, navegador y caché. Extracto real: 59,8 FPS, 16,7 ms, 229 054 triángulos." | Lista de trazabilidad + JSON literal del WebGLProfiler. | Decir que el profiler sustituye pruebas reales. |
| **21** | 20:25–21:25 | 5 | "Seis configuraciones medidas: escritorio e iPhone ≈60 FPS; Redmi 26,5 funcional bajo la meta; Adreno 610 17,6. No es universal." | Barras con línea de 30 FPS + tres lecturas. | "Los entornos documentados son dos" (solo aplica a las sesiones con usuarios). |
| **22** | 21:25–22:25 | 4 | "SUS 91,88 solo en 3D (mediana 95, DE 11,24, rango 60–100). El 68 es referencia histórica, no umbral." | SUS · Rango · Referencia 68 + lectura correcta. | Usar SUS para comparar 3D con 2D. |
| **23** | 22:25–23:30 | 5 | "NASA-TLX Raw 8,69 frente a 19,89; Δ 11,19; menor en los 12 casos. T1–T3: 20,58 s frente a 54 s. T4 no cronometrada. Descriptivo." | Cifras 3D/2D + tabla de tiempos + nota T4. | Lenguaje causal ("redujo") · mezclar T4. |
| **24** | 23:30–24:30 | 4 | "Apoyo: comprensión espacial 11/12, claridad 8/12. Fricción: navegación y control 10/12, iconos 6, piezas pequeñas 2." | Barras lima/ámbar + dos listas. | Presentar "navegación y control" como fortaleza. |
| **25** | 24:30–25:40 | 2 | "Sí soporta: tiempo, carga, SUS y orientación. No afirma: éxito superior (efecto techo 96/96), causalidad, móvil universal, Thermal físico." | Dos columnas: sí soporta / no debe afirmarse. | Definir efecto techo con los tiempos (es la completitud). |
| **26** | 25:40–26:45 | 4 | "OE1 6,5M → 95 617 · OE2 frame time en presupuesto en escritorio, móvil funcional no universal · OE3 build pública · OE4 SUS 91,88 / NASA 8,69 vs 19,89." | Tabla OE → resultado → evidencia. | Omitir el matiz móvil de OE2. |
| **27** | 26:45–27:30 | 5 | "Modelo único, n=12, PC como adaptación, cables fuera del MVP. Límites declarados, no fallas ocultas." | Cuatro tarjetas + rigor metodológico. | Disculparse por las limitaciones. |
| **28** | 27:30–28:00 | 6 | "Escalera de madurez en fases 0–5: validación y twin manifest → telemetría histórica y en vivo → modo servicio y twin operacional." | Escalera + columnas Fases 0–1 / 2–3 / 4–5. | Prometer el twin operacional como próxima versión. |
| **29** | 28:00–28:25 | 3 | "Técnica, metodológica y comunicativa: hardware complejo legible desde la web, delimitado como visual product twin." | Tres columnas de contribución. | "Categoría validada" (el informe la propone). |
| **30** | 28:25–28:30 | 0 | "Muchas gracias. Quedo atento a sus preguntas." | Cierre. | Alargar el cierre. |

---

## Cortes de emergencia

| Situación | Acción inmediata |
|---|---|
| Quedan **< 7 min** al llegar a la slide 20 | Fusionar 20–21: profiler por dispositivo; escritorio ≈60 FPS, Redmi 26,5 funcional, límite 17,6; no universal. Continuar en la 22. |
| Quedan **< 5 min** al llegar a la slide 22 | Fusionar 22–24: SUS 91,88 solo 3D / NASA 8,69 vs 19,89 / T1–T3 menores en 3D / fricciones en navegación móvil, iconos y piezas pequeñas. Continuar en la 25 (solo columna derecha). |
| **La build no responde en la demo en vivo** (> 10 s o congelada) | Volver al deck (slide 19), clic sobre el video (arranca en 0:00) y narrar el mismo recorrido. No comentar el fallo. |
| El jurado interrumpe durante la demo | Detener la interacción y responder con alcance: *"Puedo mostrar lo que está publicado y evaluado. Las capacidades no integradas en la UI final no las presento como alcance visible."* |
| El jurado pregunta por algo fuera del alcance | *"Eso corresponde a una fase posterior documentada en el roadmap. En esta defensa me limito al alcance del MVP evaluado."* |

---

## Frases de anclaje — memorizar

- **Sobre el alcance:** *"TwinSight no promete operar el dron; promete hacerlo legible desde la web."*
- **Sobre los resultados:** *"La evidencia es favorable, acotada y descriptiva — y esa honestidad es parte del rigor."*
- **Sobre las limitaciones:** *"Cada límite es una decisión documentada, no una falla oculta."*
- **Sobre la metodología:** *"No se evaluó una idea abstracta. Se evaluó un artefacto construido."*
- **Ante cualquier pregunta técnica difícil:** *"Eso está cubierto en el informe en [capítulo/sección]. El punto central que defiendo aquí es..."*
