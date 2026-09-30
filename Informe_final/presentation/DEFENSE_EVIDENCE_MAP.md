# Mapa de evidencias de sustentacion - TwinSight X500

Estado: canonico para enlazar informe, app, documentacion, presentacion y preguntas.
Fecha de actualizacion: 2026-09-29 (sincronizado con `index_final.html`, pantalla de espera + 21 slides + B1-B15).
Fuente autoritativa: `Informe_final/awoodcocks.pdf` (informe final); fuentes LaTeX en `Informe_final/chapters/`.

## 1. Jerarquia de autoridad

Cuando exista conflicto entre fuentes, usar este orden:

1. `Informe_final/informe_final.pdf` y fuentes LaTeX en `Informe_final/chapters/`.
2. Anexos, validacion, manuales y figuras bajo `Informe_final/`.
3. App publica y documentacion en `docs/`.
4. README publico.
5. Documentacion tecnica interna versionada, solo si no contradice el informe.
6. Notas de estudio locales o historicas, solo como apoyo personal, no como fuente publica.

Regla para defensa:

> Si una cifra, feature o afirmacion no aparece en informe, anexo, build, manual, profiler o README publico vigente, no se afirma como resultado cerrado.

## 2. Rutas maestras

| Tipo | Ruta | Uso en defensa |
|---|---|---|
| Informe final PDF | `Informe_final/informe_final.pdf` | Fuente academica principal. |
| Introduccion, problema, alcance | `Informe_final/chapters/01_introduccion.tex` | Problema, preguntas, objetivos, alcance, visual product twin. |
| Metodologia | `Informe_final/chapters/03_marco_metodologico.tex` | DSR, muestra, variables, instrumentos, tareas, etica. |
| Desarrollo | `Informe_final/chapters/04_desarrollo.tex` | Pipeline, taxonomia, UI, arquitectura, shaders, tooling. |
| Resultados | `Informe_final/chapters/05_resultados.tex` | Rendimiento, SUS, NASA-TLX Raw, tiempos, Think-Aloud, discusion. |
| Conclusiones | `Informe_final/chapters/06_conclusiones.tex` | Contribuciones, limitaciones, trabajo futuro. |
| Referencias | `Informe_final/chapters/07_referencias.tex` | Bibliografia formal del informe. |
| Apendices | `Informe_final/chapters/08_apendices.tex` | Indice de anexos y rutas. |
| Validacion tecnica | `Informe_final/validacion/06_GUIA_MEDICIONES_TECNICAS_WEBGL.md` | Procedimiento de profiler y mediciones WebGL. |
| Tablas rendimiento | `Informe_final/validacion/07_TABLAS_RENDIMIENTO_WEBGL_MEDICIONES.tex` | FPS, frame time, memoria, triangulos runtime. |
| Manual usuario | `Informe_final/Manual_de_usuario/manual_usuario.pdf` | Flujo visible y uso de app. |
| Manual tecnico | `Informe_final/Manual_tecnico/manual_tecnico.pdf` | Arquitectura, build y decisiones tecnicas. |
| Demo publica | `docs/index.html` | Landing/demo publicada por GitHub Pages. |
| Build publica | `docs/Build/` | Artefactos WebGL publicados. |
| Manuales publicos | `docs/manuals/` | Copias publicas del informe/manuales. |
| README | `README.md` | Limite publico: visible_ui, oculto, legacy, futuro. |
| Guion oral | `Informe_final/presentation/PRESENTATION_SCRIPT.md` | Tarjetas orales slide por slide. |
| Demo | `Informe_final/presentation/DEMO_SCRIPT.md` | Recorrido de demostracion. |
| Deck | `Informe_final/presentation/index_final.html` | Presentacion final (abrir directamente en Chrome/Edge). |
| Assets | `Informe_final/presentation/ASSETS_REQUIREMENTS.md` | Inventario final de imagenes, videos y modelo del deck. |
| Exportaciones del profiler | `Telemetria/Mediciones_WebGL/` | JSON/CSV citados en el backup B5. |
| Preguntas | `Informe_final/presentation/JURY_QA_BANK.md` | Banco de respuestas para jurado. |

## 3. Mapa slide -> evidencia

Numeracion = posicion en el deck = numero visible de cada slide.

| Slide | Claim defendido | Evidencia documental (informe) | Evidencia visual/app | Riesgo que controla |
|---:|---|---|---|---|
| 1 | TwinSight X500 es una app WebGL para inspeccionar el dron. | Resumen; `01_introduccion.tex`. | Captura del visor + URL de la demo. | Evita abrir como pitch comercial. |
| 2 | El reto es entender como se relacionan las piezas. | Introduccion; Fig. 1 y 2. | Documentacion → friccion → respuesta. | No desacreditar la documentacion 2D. |
| 3 | La carga cognitiva explica por que importa la forma de presentar. | Marco teorico (Sweller); Fig. 7. | Intrinseca / extrinseca / germana. | Decir que NASA mide las tres cargas. |
| 4 | Es visual product twin, no digital twin operacional. | Alcance y limitaciones; Fig. 3, 4 y 15. | Exclusiones / alcance. | Principal riesgo de sobrepromesa. |
| 5 | Pregunta de investigacion y objetivos verificables. | Preguntas de investigacion; objetivos. | Pregunta + matriz OE1-OE4. | Objetivos sin resultado asociado. |
| 6 | La evaluacion triangula cinco capas. | Instrumentos; Fig. 19 y 20. | Diagrama de triangulacion. | Mal uso de SUS/NASA. |
| 7 | El CAD se tesela (6,5-6,9 M tri segun la ruta) y el pipeline lo lleva a 95 617. | Pipeline 3D; Tabla 5; Tabla 28; Fig. 22. | Flujo CAD → WebGL + cifras por ruta. | Decir que el CAD "tiene" triangulos. |
| 8 | Los modulos se comunican por un bus de eventos. | Arquitectura operativa; Fig. 47-50. Codigo: `SelectionManager` publica `PartSelectedEvent`; `UIManager`, `HotspotManager`, `FastenerInspectionManager` suscritos. | Cuatro grupos + ejemplo real. | Lectura de "viewer simple" o explicacion espacial (arriba/abajo). |
| 9 | La taxonomia 28/30/257 estructura la seleccion; la tornilleria usa proxies y un tornillo modular. | Normalizacion; Tabla 3; Fig. 21; Tabla 8; Figs. 28-30. Cifras de triangulos: ver claims numericos. | Cifras + hotspots + tornilleria + piezas base. | Confusion de conteos; "todos los fasteners son modulares". |
| 10 | La ficha aparece bajo demanda; la barra de modos siempre esta visible. | Flujo visible de la build final; manual de usuario. | Flujo con barra fija + captura. | Decir que los modos aparecen al seleccionar. |
| 11 | Inspect, Analyze y Studio responden tres preguntas distintas. | Flujo visible; Fig. 59-63; Tabla 12. | Tres capturas moviles en video. | Feature-list sin proposito. |
| 12 | Thermal usa un modelo fisico simplificado por componentes, no FEA. | Modelo matematico del subsistema termico (pp. 136-138); Fig. 66-67. Codigo: `ThermalSimulationManager.cs`. | Como calcula + diagrama + limite. | Sobrevender (diagnostico) o infravalorar ("valores inventados"). |
| 13 | La build realiza el flujo prometido. | `docs/Build/`; manual de usuario; `DEMO_SCRIPT.md`. | Demo en vivo; video de respaldo. | Depender de red o azar. |
| 14 | Rendimiento viable, no universal. | Tabla 16; Tabla 19; Fig. 73. | Barras FPS por dispositivo. | Promesa universal movil. |
| 15 | SUS favorable en el prototipo 3D; como se calcula. | Resultados de usabilidad; formula SUS (cap. 3); Tabla 25; Fig. 75. | SUS, calculo, rango, referencia 68. | Comparar SUS 3D vs 2D; leerlo como porcentaje. |
| 16 | NASA-TLX Raw y tiempos favorecen al 3D descriptivamente; como se calcula. | Formula NASA-TLX Raw (cap. 3); Tablas 23, 25 y 26. | Cifras 3D/2D + tiempos + calculo. | Inferencia causal. |
| 17 | Think-Aloud explica comprension y fricciones. | Tabla 27; Fig. 77. | Barras lima/ambar. | Leer "navegacion y control" como fortaleza. |
| 18 | La discusion acota el resultado. | Discusion. | Soporta / no afirma. | Objeciones metodologicas. |
| 19 | Cada objetivo cerrado con evidencia; pregunta respondida; aporte. | Conclusiones por objetivo. | Tabla OE → resultado → evidencia. | Conclusiones genericas. |
| 20 | Trabajo futuro por fases hacia datos reales. | Trabajo futuro; Fig. 80. | Escalera + fases 0-5. | Prometer twin operacional inmediato. |
| 21 | Cierre y apertura a preguntas. | — | "Gracias." + URL. | Alargar el cierre. |

Fuera de la ruta principal desde el recorte del 2026-09-30: principios (B12), metodologia (B13), profiler (B5), geometria (B6), limitaciones (B14) y aporte (B15).

## 4. Claims numericos autorizados

| Claim | Valor autorizado | Fuente | Como decirlo |
|---|---:|---|---|
| Participantes | 12 anonimizados | `03_marco_metodologico.tex`; `05_resultados.tex` | "Muestra no probabilistica formativa de 12 participantes." |
| Registros tarea-condicion | 96 | `05_resultados.tex` | "Cuatro tareas por condicion, ambas condiciones, 12 participantes." |
| Tareas completadas | 12/12 en T1-T4 por condicion | `05_resultados.tex` | "Hubo efecto techo en completitud." |
| T4 | No cronometrada | `05_resultados.tex` | "T4 fue exploratoria guiada." |
| T1 3D vs 2D | 5,75 s vs 13,00 s | `05_resultados.tex` | "Tiempo medio descriptivo." |
| T2 3D vs 2D | 3,50 s vs 18,00 s | `05_resultados.tex` | "Tiempo medio descriptivo." |
| T3 3D vs 2D | 11,33 s vs 23,00 s | `05_resultados.tex` | "Tiempo medio descriptivo." |
| Total T1-T3 | 20,58 s vs 54,00 s | `05_resultados.tex` | "Diferencia media acumulada de 33,42 s." |
| SUS promedio | 91,88 | `05_resultados.tex` | "SUS solo del prototipo 3D." |
| SUS mediana | 95,00 | `05_resultados.tex` | "Lectura descriptiva." |
| SUS minimo/maximo | 60,00 / 100,00 | `05_resultados.tex` | "Muestra pequena y no probabilistica." |
| NASA 3D promedio | 8,69 | `05_resultados.tex` | "NASA-TLX Raw, workload percibido." |
| NASA 2D promedio | 19,89 | `05_resultados.tex` | "Referencia 2D por condicion." |
| NASA diferencia | 11,19 | `05_resultados.tex` | "Diferencia pareada descriptiva 2D-3D." |
| Casos NASA menor en 3D | 12/12 | `05_resultados.tex` | "Tendencia consistente en esta muestra." |
| Activo base optimizado | 95 617 triangulos | `05_resultados.tex` | "Activo base, no runtime total." |
| Escena runtime profiler | 229 054 triangulos estimados | `07_TABLAS_RENDIMIENTO_WEBGL_MEDICIONES.tex` | "Escena instrumentada, no equivalente al activo base." |
| Taxonomia | 28 piezas canonicas | `04_desarrollo.tex` | "Nivel semantico." |
| Anchors | 30 anchors | `04_desarrollo.tex` | "Nivel operativo de escena." |
| Renderers/colliders | 257 | `04_desarrollo.tex` | "Fragmentacion geometrica/runtime." |
| Teselacion del CAD por ruta | 6 717 499 / 6 864 586 / 6 525 748 triangulos | Tabla 28 | "Al teselar el STEP salieron entre 6,5 y 6,9 millones de triangulos." |
| Tornilleria importada (STEPper) | 425 208 triangulos (160 sujetadores; 79 % del archivo) | Medido en `blender_files/welded/stepper.fbx` (Blender 4.3, 2026-09-29). **No esta en el informe.** | "En la importacion con STEPper que se conserva en el proyecto." |
| Tornilleria en la escena final | 14 408 triangulos (161 proxies de 88 tri o menos) | Medido en `blender_files/welded/x500v2_runtime_low_final.fbx`. **No esta en el informe.** | "En la escena exportada final." |
| Piezas base del tornillo modular | 5 (3 cabezas, vuelta de rosca, punta) | Tabla 8; Figs. 28-29; `FastenerBuilder.cs` | "Las vueltas se repiten segun largo / paso de rosca." |
| Familias / instancias / reconciliaciones de fasteners | 20 / 168 / 9 | Tabla 8 | "Registradas en la escena documentada." |

## 5. Evidencia por frente

### 5.1 Problema y marco conceptual

Usar para:

- justificar necesidad de pasar de informacion plana a comprension espacial;
- explicar hardware complejo;
- delimitar visual product twin.

Fuentes:

- `Informe_final/chapters/01_introduccion.tex`
- `Informe_final/figures/chapter1/fig_1_fragmentacion_hardware_complejo.pdf`
- `Informe_final/figures/chapter1/fig_1_alcance_visual_product_twin.pdf`

Respuesta corta:

> La tesis no nace porque el manual 2D sea inutil, sino porque el ensamblaje exige reconstruccion espacial que una interfaz 3D puede externalizar parcialmente.

### 5.2 Metodologia

Usar para:

- defender DSR;
- explicar muestra;
- aclarar instrumentos;
- responder por inferencia estadistica.

Fuentes:

- `Informe_final/chapters/03_marco_metodologico.tex`
- `Informe_final/figures/chapter3/fig_3_dsrm_aplicado_proyecto.pdf`
- `Informe_final/figures/chapter3/fig_3_triangulacion_evidencia.pdf`

Respuesta corta:

> La investigacion es aplicada y formativa. Construye y evalua un artefacto; por eso los resultados son descriptivos y trazados, no inferenciales poblacionales.

### 5.3 Implementacion tecnica

Usar para:

- pipeline CAD/WebGL;
- arquitectura;
- taxonomia;
- UI visible;
- shaders y Thermal.

Fuentes:

- `Informe_final/chapters/04_desarrollo.tex`
- `Informe_final/Manual_tecnico/manual_tecnico.pdf`
- `README.md`
- `docs/Build/`

Respuesta corta:

> El aporte tecnico no es una escena aislada, sino una cadena CAD/Blender/Unity/WebGL con taxonomia, UI, shaders, datos y medicion runtime.

### 5.4 Resultados tecnicos

Usar para:

- FPS;
- frame time;
- memoria;
- compatibilidad;
- triangulos;
- profiler.

Fuentes:

- `Informe_final/chapters/05_resultados.tex`
- `Informe_final/validacion/06_GUIA_MEDICIONES_TECNICAS_WEBGL.md`
- `Informe_final/validacion/07_TABLAS_RENDIMIENTO_WEBGL_MEDICIONES.tex`
- `Telemetria/Mediciones_WebGL/`

Respuesta corta:

> El rendimiento debe leerse por dispositivo, navegador, resolucion y escenario. La tesis no promete compatibilidad universal.

### 5.5 Resultados con usuarios

Usar para:

- SUS;
- NASA-TLX Raw;
- tiempos;
- Think-Aloud;
- efecto techo;
- T4.

Fuentes:

- `Informe_final/chapters/05_resultados.tex`
- `Informe_final/validacion/`
- `docs/manuals/` para copias publicas de instrumentos cuando aplique.

Respuesta corta:

> La evidencia de usuario favorece al 3D en workload y tiempos descriptivos, pero no se interpreta como prueba causal fuerte por el tamano y tipo de muestra.

## 6. Bibliografia por funcion defensiva

No memorizar referencias completas; citar autores o familias cuando aporten criterio.

| Tema | Referencias del informe | Para que sirven en defensa |
|---|---|---|
| Design Science Research | Hevner et al.; Peffers et al. | Justificar construir y evaluar un artefacto. |
| Carga cognitiva | Sweller; Hegarty y Waller | Explicar reconstruccion mental y esfuerzo espacial. |
| HCI/UX 3D | Bowman et al.; Norman; Ware; Darken y Sibert | Justificar navegacion, seleccion, feedback y legibilidad. |
| SUS | Brooke; Bangor et al.; Sauro y Lewis | Explicar calculo, lectura descriptiva y referencia 68. |
| NASA-TLX | Hart y Staveland; Hart | Explicar Raw TLX, subescalas y workload percibido. |
| Think-Aloud | Ericsson y Simon | Justificar verbalizacion concurrente y codificacion cualitativa. |
| Digital twin | Kritzinger et al.; Digital Twin Consortium; Lin et al.; Jones et al. | Delimitar visual product twin vs digital shadow/twin operacional. |
| WebGL/WebAssembly/Unity | Unity Technologies; WebAssembly Community Group; fuentes WebGL | Justificar restricciones de build web y runtime. |
| Documentacion Holybro | Holybro | Soporte del caso de estudio y condicion 2D. |

Ruta formal:

- `Informe_final/chapters/07_referencias.tex`

## 7. Slides de respaldo y preguntas que cubren

| Backup | Evidencia | Preguntas que responde |
|---|---|---|
| B1 SUS | `03_marco_metodologico.tex`; `05_resultados.tex` | Por que SUS solo 3D; que significa 68; como se calcula. |
| B2 NASA | `03_marco_metodologico.tex`; `05_resultados.tex` | Raw TLX, rendimiento invertido, no carga cognitiva directa. |
| B3 Variables de control | `03_marco_metodologico.tex`; `05_resultados.tex` | Dispositivo, navegador, cache, orden AB/BA, build. |
| B4 Rendimiento completo | `07_TABLAS_RENDIMIENTO_WEBGL_MEDICIONES.tex` | FPS, frame time, memoria, escenarios. |
| B5 Profiler | `06_GUIA_MEDICIONES_TECNICAS_WEBGL.md` | Como se obtuvo JSON/CSV y que registra. |
| B6 Geometria | `05_resultados.tex`; `04_desarrollo.tex` | 95 617 vs 229 054, 28/30/257. |
| B7 Arquitectura | `Informe_final/Manual_tecnico/manual_tecnico.pdf`; `04_desarrollo.tex` | Managers, eventos, datos, shaders. |
| B8 Thermal | Modelo matematico del subsistema termico (pp. 136-138); `ThermalSimulationManager.cs` | Ecuaciones, escalas por material, limites (sin FEA, sin calibracion SI). |
| B9 Limitaciones | `05_resultados.tex`; `06_conclusiones.tex` | Muestra, efecto techo, no inferencia, movil. |
| B10 Futuro | `06_conclusiones.tex`; Fig. 80 | Fases 0-5: validacion ampliada, twin manifest, telemetria, live shadow, modo servicio, twin operacional. |
| B11 Mobile UX | Tabla 20; Fig. 74; Tabla 16 | Gestos y ajustes moviles de la prueba formativa; rendimiento del Redmi. |

## 8. Claims prohibidos o condicionados

| Claim | Estado | Forma segura |
|---|---|---|
| "TwinSight es un digital twin completo" | Prohibido | "Visual product twin; base visual-semantica previa." |
| "Thermal simula fisica real" / "Thermal usa valores inventados" | Prohibidos | "Modelo fisico simplificado por componentes: fuentes por carga, conduccion por area, longitud y material, conveccion; tiempo acelerado, sin calibrar." |
| "Funciona en cualquier movil" | Prohibido | "Funciona en dispositivos evaluados, con limites." |
| "SUS demuestra que 3D supera a 2D" | Prohibido | "SUS describe usabilidad del prototipo 3D." |
| "NASA mide carga cognitiva directamente" | Prohibido | "NASA-TLX Raw mide carga de trabajo percibida." |
| "T4 fue cronometrada" | Prohibido | "T4 fue exploratoria guiada y no cronometrada." |
| "95 617 es todo el runtime" | Prohibido | "95 617 es el activo base optimizado." |
| "README historico define el alcance final" | Prohibido | "El informe final es fuente academica autoritativa." |
| "Modulos ocultos son features publicas" | Prohibido | "Visible, oculto, legacy y futuro se diferencian." |
| "Los °C de Thermal son temperatura real" | Prohibido | "Temperatura que calcula el modelo por componentes, sin calibracion." |
| "Navegacion y control fue una fortaleza (10/12)" | Prohibido | "Fue la friccion mas frecuente: orbita, pan y sensibilidad tactil." |
| "El profiler usa telemetria" | Prohibido | "Profiler interno de la build (WebGLProfiler), exporta JSON/CSV." |
| "Efecto techo en los tiempos" | Prohibido | "Efecto techo en completitud: 96/96 registros en ambas condiciones." |

## 9. Checklist antes de cerrar deck

- Cada slide tiene un claim, no un titulo generico.
- Cada claim tiene al menos una evidencia en informe, anexo, app o README.
- Las cifras coinciden con capitulo 5 y anexos.
- El video de demo no muestra Measurement, BOM, annotations ni modulos no publicados.
- La slide de Thermal explica el modelo (fuentes, conduccion, conveccion) y su limite (sin FEA, sin calibracion).
- La slide de flujo muestra la barra Inspect · Analyze · Studio como siempre visible.
- La slide de rendimiento dice "por dispositivo" y "no universal".
- La slide de SUS dice "solo prototipo 3D".
- La slide de NASA dice "Raw TLX" y "descriptivo".
- La slide de tareas dice "T4 no cronometrada".
- El cierre repite "visual product twin".
