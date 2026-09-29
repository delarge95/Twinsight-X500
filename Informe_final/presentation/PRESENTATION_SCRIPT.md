# PRESENTATION_SCRIPT.md
# Guion maestro de sustentación — TwinSight X500

**Estado:** canónico para defensa académica. Sincronizado con `index_final.html` (30 slides principales + B1–B11).
**Fecha de actualización:** 2026-09-29.
**Duración objetivo:** 28:30 de exposición real + 1:30 de margen = 30:00 total. Guion calibrado a ~140 palabras/min.
**Fuente autoritativa:** `awoodcocks.pdf` (informe final) y sus anexos del repositorio.
**Uso complementario:** ensayar tiempo con `SPEAKER_CARDS.md`.

---

## 1. Regla de uso

Este documento es una partitura oral, no un texto para memorizar. Cada slide tiene una tesis central, una evidencia visual de apoyo, un guion base, una transición y una zona de riesgo. La defensa debe sonar conversacional, técnica y segura.

La columna vertebral narrativa es:

```
problema real → teoría que lo explica → alcance honesto → método → decisiones técnicas → implementación → evidencia → límites → contribución
```

**Frase de tesis oral — memorizar antes de entrar:**

> TwinSight X500 no se defiende como un gemelo digital operacional. Se defiende como un *visual product twin*: una capa web 3D, optimizada y semánticamente organizada, que hace legibles piezas, relaciones y modos de inspección de un hardware complejo directamente en el navegador.

---

## 2. Numeración única

La numeración de este guion, de `SPEAKER_CARDS.md` y de `PRESENTATION_OUTLINE.md` es la **posición en el deck**. Coincide con el número visible de cada slide (kicker) y con el contador superior derecho ("NN / 30"). Los backups se numeran B1–B11 fuera del total; para saltar a uno, escribir su código (por ejemplo `B3`) en el contador y pulsar Enter.

---

## 3. Tabla de prerequisito conceptual

Ningún término técnico puede usarse como argumento antes de haber sido definido.

| Término | Se define en | Uso posterior permitido |
|---|---|---|
| CAD, WebGL | Slide 1 | Pipeline, activo base, optimización |
| Hardware complejo, reconstrucción espacial | Slide 2 | Carga cognitiva, tareas, Think-Aloud |
| Carga cognitiva (intrínseca / extrínseca / germana) | Slide 3 | Discusión; nunca como medición directa |
| Digital twin / digital shadow / visual product twin | Slide 5 | Alcance, limitaciones, trabajo futuro |
| Telemetría, FEA | Slide 5 | Límites de Thermal y roadmap |
| DSR, validación formativa y descriptiva | Slide 7 | Resultados y conclusiones |
| KPIs, profiler interno, SUS, NASA-TLX Raw, Think-Aloud | Slide 8 | Rendimiento y resultados |
| Runtime | Slide 9 | Activo 3D, geometría, arquitectura |
| Taxonomía, hotspot, fastener | Slide 13 | Flujo, selección, demo |
| Bottom sheet | Slide 14 | Inspect, demo |
| Shader, preset | Slide 16 | Studio, Thermal, demo |
| Heurístico | Slide 17 | Thermal, discusión |
| Efecto techo | Slide 25 | Discusión metodológica |

> Nota: la slide 4 menciona hotspots y bottom sheet como ejemplos de interfaz antes de su definición formal; se nombran solo como ejemplo visual, no como argumento.

---

## 4. Convenciones orales

| Marca | Acción |
|---|---|
| `[click n — etiqueta]` | Pulsar → para revelar el paso *n* de la slide (coincide con `data-step="n"` del HTML). Decir lo que sigue **cuando ya está en pantalla**. |
| `[avanzar]` | Pasar a la siguiente slide (todos los pasos ya están revelados). |
| `[pausa]` | Detenerse ~1 segundo. Permite que el dato aterrice. |
| `[pausa larga]` | Detenerse ~2 segundos. Usar antes de una limitación o número clave. |
| `[mirar jurado]` | Levantar la vista, cerrar una idea con contacto visual directo. |
| `[señalar]` | Indicar zona específica del visual con la mano o el puntero. |
| `[respirar]` | Bajar velocidad antes de un dato numérico. |

**Regla de oro:** si la velocidad sube, es señal de nervios. Bajarla comunica dominio.

---

## 5. Protocolo de apertura

Antes de pronunciar la primera palabra:

1. Pararse firme, pies paralelos, mirada al jurado — no a la pantalla.
2. Verificar que la presentación esté en la slide 1 y que la build pública esté abierta en otra ventana, con caché cargada y el dron en estado inicial (para la demo en vivo de la slide 19). El video de esa slide es el respaldo.
3. Tener claros los tres mensajes que se van a defender:
   - Ver piezas no es suficiente; lo difícil es comprender relaciones espaciales.
   - El aporte integra teoría cognitiva, pipeline 3D, WebGL, UI de inspección y evaluación formativa con usuarios reales.
   - La evidencia es favorable, pero acotada y descriptiva — y esa honestidad es una fortaleza, no una debilidad.

Si hay nervios: **no acelerar**. La primera pausa comunica control.

### Checklist técnico (30 min antes)

| # | Verificación | Por qué |
|---|---|---|
| 1 | Abrir `index_final.html` directamente (doble clic) en **Chrome** o Edge, con zoom del navegador al 100 % (`Ctrl+0`). | El deck escala su contenido según la resolución; el zoom del navegador altera ese cálculo. |
| 2 | Conectar el proyector en modo **duplicar** y comprobar la resolución (ideal 1920×1080). Pulsar `F` para pantalla completa. | A 1920×1080 el texto se amplía ~25 %; a 1366×768 o 1280×720 se ajusta para no desbordar. |
| 3 | Confirmar conexión a internet. | La build pública de la demo en vivo y las tipografías del deck se cargan desde la red. |
| 4 | Recorrer una vez las slides 10 (visor 3D) y 19 (video de respaldo) y volver al inicio con `Inicio`. | Deja el modelo 3D y los videos en caché; el visor además se precarga solo al abrir el deck. |
| 5 | Abrir la **landing pública** (twinsight.alexwoodcock.me) en **otra ventana**, dejarla al inicio de la sección 03 con el dron sin ensamblar, y abrir una vez «Abrir visor» para dejar la build de Unity en caché (luego cerrarla). | Es la demo en vivo de la slide 19 (landing + app). |
| 6 | Desactivar notificaciones, actualizaciones automáticas y ahorro de energía; conectar el cargador. | Evita interrupciones y caídas de rendimiento de la GPU. |
| 7 | **No abrir las notas (`N`) durante la exposición**: se muestran sobre la misma pantalla que ve el jurado. Usar `SPEAKER_CARDS.md` impreso o en el teléfono. | Las notas del deck son para ensayo. |
| 8 | Tener un cronómetro visible (teléfono) con las marcas de la tabla de tarjetas. | El deck no muestra tiempo transcurrido. |

---

## 6. Guion principal — slide por slide

---

### SLIDE 1 — Portada: TwinSight X500

**Tiempo:** 0:00 – 0:45 · **Pasos en pantalla:** ninguno
**Visual:** título, claim, ficha (autor, proyecto, institución, caso, artefacto, URL de demo) y captura del visor.

**Guion oral:**

> Buenos días. Mi nombre es Alexander Woodcock Salomón y presento TwinSight X500: un prototipo de visualización 3D interactiva para inspección técnica del dron Holybro X500 V2, trabajo de grado de Ingeniería Multimedia en la UNAD. [mirar jurado]
>
> Dos términos clave. **CAD** son los modelos de diseño asistido por computador: útiles en ingeniería, pero no pensados para el navegador. **WebGL** es la tecnología que renderiza 3D dentro del navegador, sin instalar nada. [pausa]
>
> La propuesta: convertir un ensamblaje complejo, disperso en planos, manuales y CAD pesado, en una experiencia web explorable, seleccionable y explicable. Recorreré el problema, el método, la construcción, la evidencia y sus límites.

**Transición:**
> Para entender por qué esto importa, primero hay que ver el problema.

**No decir:** "gemelo digital completo" · "simulador operacional" · "producto industrial terminado"

---

### SLIDE 2 — El reto central es comprender relaciones, no solo ver piezas

**Tiempo:** 0:45 – 1:45 · **Pasos:** 4
**Visual:** tres filas (Documentación → Fricción → Respuesta) + recuadro "Idea clave".

**Guion oral:**

> Piensen en una tarea concreta: ubicar un motor del dron y entender cómo se une a su montura, usando un manual en PDF. La documentación técnica tradicional no es un error; el problema es otro.
>
> [click 1 — Documentación] Planos, manuales y referencias nombran piezas y muestran pasos, pero la lectura queda repartida en soportes separados. [señalar]
>
> [click 2 — Fricción] El problema aparece cuando el usuario debe reconstruir mentalmente profundidad, ubicación y ensamblaje a partir de vistas planas. A ese esfuerzo lo llamo **reconstrucción espacial**, y en un hardware complejo como el X500 ocurre sobre un sistema completo, no sobre una pieza aislada. [pausa larga]
>
> [click 3 — Respuesta] La respuesta que propongo es una capa 3D web que permite ubicar, seleccionar, aislar y relacionar piezas sin abrir herramientas CAD.
>
> [click 4 — Idea clave] El problema, entonces, no era falta de información: era la distancia entre la información disponible y la comprensión espacial del sistema. [mirar jurado]

**Transición:**
> Esa distancia se explica desde la teoría de la carga cognitiva.

**No decir:** "el 2D no sirve" · "el 3D siempre es mejor"

---

### SLIDE 3 — La carga cognitiva explica por qué la forma de presentar importa

**Tiempo:** 1:45 – 2:50 · **Pasos:** 4
**Visual:** tres filas (Intrínseca / Extrínseca / Germana) + recuadro "Límite declarado".

**Guion oral:**

> La teoría de carga cognitiva plantea que la memoria de trabajo es limitada: no podemos sostener y manipular muchos elementos nuevos a la vez sin costo mental. Sweller distingue tres tipos de carga.
>
> [click 1 — Intrínseca] La **intrínseca** viene de la complejidad propia del contenido: un dron multicomponente es complejo aunque la interfaz sea óptima.
>
> [click 2 — Extrínseca] La **extrínseca** viene de cómo se presenta la información: saltar entre planos, tablas y vistas desconectadas agrega esfuerzo que no ayuda a entender.
>
> [click 3 — Germana] La **germana** es el esfuerzo útil: ubicar una pieza, entender su función y relacionarla con el conjunto. [pausa]
>
> [click 4 — Límite declarado] Y una precisión que importa para la discusión: esta tesis no mide las tres cargas. Las usa como marco para justificar una interfaz 3D guiada que reduzca la reconstrucción espacial innecesaria. Más adelante, NASA-TLX medirá carga de trabajo percibida, que es otra cosa.

**Transición:**
> Esa misma lógica guía cómo está diseñada la interfaz y esta presentación.

**No decir:** "NASA-TLX mide carga intrínseca o germana" · "el 3D elimina la carga cognitiva"

---

### SLIDE 4 — La interfaz traduce la teoría en jerarquía, agrupación, estado y reconocimiento

**Tiempo:** 2:50 – 4:00 · **Pasos:** 4
**Visual:** tabla de cuatro principios citados en el informe, con columnas "En la app" y "En este deck".

**Guion oral:**

> La segunda base es la cognición distribuida de Hutchins: una interfaz no solo muestra información, también funciona como artefacto cognitivo. La cámara orbital externaliza la rotación mental y las fichas funcionan como memoria externa. Para lograrlo, la app aplica cuatro principios que el informe cita, y esta presentación también.
>
> [click 1 — Jerarquía visual] **Jerarquía visual**, de Norman: el Hero, la barra de modos y la ficha inferior separan navegación, acción y detalle. Aquí, cada título es la tesis de la diapositiva.
>
> [click 2 — Agrupación] **Agrupación**, de la Gestalt: los controles se agrupan por modo y las categorías se distinguen por color. Aquí, el lima marca evidencia y el ámbar, límites declarados.
>
> [click 3 — Visibilidad del estado] **Visibilidad del estado**, de Nielsen: el usuario siempre ve qué pieza está seleccionada y qué modo está activo.
>
> [click 4 — Reconocimiento] Y **reconocimiento antes que memoria**, también de Nielsen: hotspots, fichas y onboarding evitan recordar nombres o gestos. En esta defensa, cada término se define antes de usarlo.

**Transición:**
> Con esas bases, puedo delimitar con precisión qué es y qué no es TwinSight.

**No decir:** "estos principios demuestran los resultados" — son criterios de diseño, no evidencia empírica · citar autores que no estén en la bibliografía del informe.

---

### SLIDE 5 — La tesis responde con un visual product twin, no con un digital twin operacional

**Tiempo:** 4:00 – 5:10 · **Pasos:** 2 (1 = exclusiones, 2 = alcance)
**Visual:** dos columnas: "Digital Twin Operacional — Exclusiones" y "Visual Product Twin — Alcance".

**Guion oral:**

> Una parte decisiva de esta defensa es llamar al sistema por su nombre correcto. [mirar jurado]
>
> Un **digital twin operacional** implica sincronización en tiempo real con el activo físico, telemetría, modelos calibrados y apoyo a decisiones. Un **digital shadow** ya recibe datos del activo, aunque no lo controla.
>
> [click 1 — Exclusiones] TwinSight no está en esos niveles, y el informe lo declara: no recibe telemetría, no sincroniza estado con un dron físico, no hace mantenimiento predictivo ni se integra con PLM, ERP, CMMS, SCADA o IoT. Tampoco ejecuta **FEA** —análisis por elementos finitos—, que requeriría modelo físico, propiedades de material y condiciones de frontera. [pausa]
>
> [click 2 — Visual Product Twin] Lo que sí entrega es un **visual product twin**: una capa visual-semántica navegable, con selección, aislamiento, modos visuales, taxonomía, profiler y evaluación con usuarios reales.
>
> Dicho sin rodeos: no prometo operar el dron desde datos vivos; prometo hacerlo legible desde la web.

**Transición:**
> Con esa frontera clara, los objetivos se leen como un contrato verificable.

**No decir:** "TwinSight ya es un gemelo digital" · "Thermal calcula temperatura real"

---

### SLIDE 6 — Los objetivos definen el contrato: construir y evaluar

**Tiempo:** 5:10 – 6:05 · **Pasos:** 4
**Visual:** cuatro tarjetas OE1–OE4.

**Guion oral:**

> La pregunta que guía la tesis está en pantalla: qué diferencias descriptivas de desempeño y carga percibida aparecen entre un visor 3D web y un soporte 2D, y bajo qué condiciones es viable en el navegador. Para responderla, cuatro objetivos específicos.
>
> [click 1 — OE1] OE1: un pipeline que lleve el CAD a un presupuesto geométrico compatible con WebGL, preservando legibilidad.
>
> [click 2 — OE2] OE2: materiales y modos visuales en URP, procurando un frame time de 33,33 milisegundos o menos, es decir, 30 FPS.
>
> [click 3 — OE3] OE3: el prototipo interactivo, con selección, ficha contextual, explosionado, corte y modos analíticos.
>
> [click 4 — OE4] OE4: evaluar formativamente tareas y carga percibida frente a un soporte 2D, y la usabilidad del 3D con SUS. [pausa]
>
> No se evaluó una idea abstracta: se evaluó un artefacto construido y medible.

**Transición:**
> Por eso la metodología tenía que incluir la evaluación del artefacto.

**No decir:** "la evaluación prueba generalización poblacional" · "se aseguró 30 FPS en todo dispositivo"

---

### SLIDE 7 — La metodología usa DSR con validación formativa descriptiva

**Tiempo:** 6:05 – 6:55 · **Pasos:** 7 (1–5 fases del ciclo, 6 marco DSR, 7 formativa/descriptiva)
**Visual:** ciclo DSRM en seis cajas + dos columnas.

**Guion oral:**

> La metodología sigue el ciclo DSRM de Peffers. Parte del problema [señalar primera caja]: la brecha práctica que acabo de describir.
>
> [click 1 — Objetivos] Los objetivos funcionan como contrato verificable. [click 2 — Diseño] Luego, diseño y desarrollo. [click 3 — Demostración] Demostración del artefacto WebGL. [click 4 — Evaluación] Evaluación formativa. [click 5 — Comunicación] Y comunicación trazable de resultados.
>
> [click 6 — Marco DSR] La investigación es aplicada, con enfoque mixto y predominio cualitativo-formativo. Adopté **Design Science Research** porque en ingeniería el conocimiento se produce diseñando, construyendo y evaluando un artefacto; el rigor se revisa con las directrices de Hevner.
>
> [click 7 — Formativa y descriptiva] La evaluación fue **formativa**, para aprender del artefacto y refinarlo, y **descriptiva**, porque reporta patrones sin inferencia poblacional. Con n = 12, esa decisión no es debilidad: es rigor. [mirar jurado]

**Transición:**
> Para sostener esa lectura, la evaluación se diseñó por capas.

**No decir:** "se probó causalidad" · "la muestra representa a la población"

---

### SLIDE 8 — Ninguna métrica mide todo: la evaluación triangula cinco capas

**Tiempo:** 6:55 – 8:05 · **Pasos:** 6 (una capa por paso + triangulación)
**Visual:** diagrama de triangulación + lista de cinco capas.

**Guion oral:**

> La evaluación tiene cinco capas que se leen juntas; ninguna cierra el argumento por sí sola.
>
> [click 1 — KPIs técnicos] Primera: **KPIs técnicos** —FPS, frame time y memoria— registrados por el profiler interno de la app. Miden viabilidad en los entornos probados.
>
> [click 2 — SUS] Segunda: **SUS**, System Usability Scale, diez ítems de usabilidad percibida. Se aplicó solo al prototipo 3D, no como comparación con 2D.
>
> [click 3 — Tareas] Tercera: **desempeño en cuatro tareas**. Completitud y ayudas en T1 a T4; tiempos solo en T1 a T3, porque T4 fue exploratoria.
>
> [click 4 — Think-Aloud] Cuarta: **Think-Aloud**, verbalización concurrente que se codifica para detectar claridad, fricción y comprensión.
>
> [click 5 — NASA-TLX Raw] Quinta: **NASA-TLX Raw**, carga de trabajo percibida en seis dimensiones, aplicada a cada condición. Raw significa promedio sin ponderación pareada; la dimensión de rendimiento se diligenció invertida. [pausa]
>
> [click 6 — Triangulación] Triangular es leer juntos rendimiento técnico, conducta, percepción y comentario cualitativo: cuando coinciden, el argumento se vuelve más robusto.

**Transición:**
> Con el método definido, paso al primer desafío de ingeniería: convertir CAD pesado en una escena WebGL.

**No decir:** "NASA mide carga cognitiva directamente" · "SUS demuestra superioridad del 3D" · "el profiler usa telemetría"

---

### SLIDE 9 — De 6,5 millones a 95 617 triángulos: la traducción de activos CAD para WebGL

**Tiempo:** 8:05 – 9:15 · **Pasos:** 8 (1–6 etapas del flujo, 7 por qué optimizar, 8 qué se conserva)
**Visual:** flujo CAD → MoI3D/STEPper → Blender → Retopo/proxies → Bake → FBX → WebGL + dos columnas.

**Guion oral:**

> Primero defino **runtime**: el tiempo de ejecución, lo que realmente corre cuando el usuario abre la app en el navegador. El reto de este objetivo fue traducir CAD de manufactura a una escena runtime: de más de 6,5 millones de triángulos a 95 617. [señalar título]
>
> [click 1 — MoI3D / STEPper] El STEP se abrió con MoI3D o con el addon STEPper, según el control de teselación necesario; [click 2 — Blender] en Blender se limpió la geometría; [click 3 — Retopo] las piezas críticas se retopologizaron y las repetitivas pasaron a proxies; [click 4 — Bake] se hornearon mapas normal y AO; [click 5 — FBX] se exportó en FBX a Unity [click 6 — WebGL] y se compiló para WebGL.
>
> [click 7 — Por qué optimizar] ¿Por qué? Porque el CAD llega con n-gons, vértices duplicados, caras internas y tornillería repetida como mallas únicas. No está pensado para tiempo real.
>
> [click 8 — Qué se conserva] Y lo que se conserva es la jerarquía: la ganancia no es solo bajar peso, es poder seleccionar, aislar, separar y consultar cada pieza sin romper la lectura del ensamblaje. [mirar jurado]

**Transición:**
> [avanzar] Y el resultado se puede inspeccionar aquí mismo.

**No decir:** "el CAD original se usó intacto" · "optimizar es solo bajar polígonos"

---

### SLIDE 10 — La escena runtime exportada es explorable

**Tiempo:** 9:15 – 9:40 · **Pasos:** 2
**Visual:** visor three.js con el GLB de la build (etiqueta "GLB · 229 070 tri"), botones Auto / Wireframe / Reset.

**Guion oral:**

> Este es el resultado, en vivo, dentro de la presentación. [arrastrar para rotar · pulsar Wireframe]
>
> [click 1 — ficha técnica] Es la escena runtime exportada de la build: 252 mallas y unos 229 000 triángulos, con los mapas del bake.
>
> [click 2 — Por qué aquí] Noten la cifra, porque no es la del título anterior. Esa diferencia tiene explicación.

**Transición:**
> [avanzar] (la frase anterior funciona como transición)

**No decir:** "esto es el activo de 95 617 triángulos"

---

### SLIDE 11 — La reducción geométrica se lee como presupuesto de activo, no como conteo runtime

**Tiempo:** 9:40 – 10:35 · **Pasos:** 3
**Visual:** dos cifras (95 617 activo base · 229 054 escena runtime) + criterio de interpretación.

**Guion oral:**

> En el informe aparecen dos cifras que miden cosas distintas.
>
> [click 1 — Activo base] **95 617 triángulos** es el activo base optimizado: el modelo principal y sus masters después de retopología, limpieza y bake. Es la métrica del pipeline.
>
> [click 2 — Escena runtime] **229 054 triángulos estimados** es lo que reporta el profiler sobre la escena instrumentada: incluye instancias, proxies, assets de apoyo y renderers adicionales, 252 en total. Es lo que acaban de rotar.
>
> [click 3 — Criterio de interpretación] No son equivalentes ni se contradicen: una mide el activo; la otra, la escena en ejecución. [pausa larga] La respuesta correcta no es esconder la diferencia, es explicarla, y el informe la explica junto a las tablas donde aparece cada cifra.

**Transición:**
> Para que esa escena sea mantenible, la arquitectura se organizó en capas.

**No decir:** "se redujo de 229 054 a 95 617" — invierte la relación · "ambas cifras miden lo mismo"

---

### SLIDE 12 — La arquitectura separa UI, orquestación, servicios de escena y datos

**Tiempo:** 10:35 – 11:40 · **Pasos:** 4
**Visual:** cuatro capas con clases reales del código (Fig. 47 del informe).

**Guion oral:**

> La aplicación se organiza en cuatro capas, como en la figura 47 del informe.
>
> [click 1 — UI] Arriba, la presentación: UIManager, la ficha inferior —UIDetailsSheet—, los hotspots y los modos Inspect, Analyze y Studio.
>
> [click 2 — Core] Debajo, la orquestación. Aquí está la decisión clave: un **EventBus** de publicación y suscripción. Cuando la UI selecciona una pieza, publica un evento y no necesita saber quién lo consume. Una máquina de estados, AppStateMachine, gobierna el flujo entre Hero, exploración y herramientas.
>
> [click 3 — Scene] Luego, los servicios de escena: vista explosionada, corte, visibilidad, el subsistema térmico y el profiler interno que exporta JSON y CSV.
>
> [click 4 — Data] En la base, los datos: DronePartData y el catálogo de piezas. [pausa]
>
> Por eso cada botón no es una solución aislada: seleccionar una pieza resalta geometría, actualiza el estado, consulta datos y abre la ficha correcta.

**Transición:**
> La capa de datos se apoya en una taxonomía funcional.

**No decir:** "es solo un visor 3D" · nombres de clases que no estén en la slide o en el código

---

### SLIDE 13 — La taxonomía permite seleccionar piezas madre, subpiezas, hotspots y fasteners

**Tiempo:** 11:40 – 12:35 · **Pasos:** 7
**Visual:** cifras 28 / 30 / 257 + tarjetas Hotspots, Fasteners, Bottom sheet + captura de hotspots.

**Guion oral:**

> **Taxonomía**, aquí, es clasificación operativa: el sistema que le permite a la app saber qué seleccionó el usuario y qué ficha abrir.
>
> [click 1 — 28] Son 28 piezas canónicas, cada una con su DronePartData. [click 2 — 30] En Unity se organizan en 30 anchors: las 28 más un grupo de fasteners y uno de misceláneos. [click 3 — 257] Y eso se traduce en 257 renderers y colliders auditados en la escena.
>
> [click 4 — Hotspots] Un **hotspot** es un punto interactivo que dirige la atención a una zona. [click 5 — Fasteners] Un **fastener** es un sujetador; los tornillos se reconstruyeron con un sistema modular y el resto con proxies ligeros. [click 6 — Bottom sheet] La ficha traduce la selección en información legible. [click 7 — captura]
>
> No es una BOM industrial certificada: es una estructura funcional, trazable y extensible.

**Transición:**
> Esa estructura sostiene el flujo público de la app.

**No decir:** "la taxonomía es una BOM certificada" · "28 categorías" (son 28 piezas canónicas)

---

### SLIDE 14 — El flujo de usuario revela la complejidad del dron de forma progresiva

**Tiempo:** 12:35 – 13:25 · **Pasos:** 7
**Visual:** flujo Hero → Explore → Selección → Bottom Sheet → Inspect/Analyze/Studio + dos columnas + captura.

**Guion oral:**

> El flujo público revela la complejidad por etapas. Parte del Hero, que orienta. [click 1 — Explore] En Explore, el usuario orbita el dron completo. [click 2 — Selección] Al tocar una pieza, la selecciona, [click 3 — Bottom Sheet] y el **bottom sheet** —el panel inferior— muestra su ficha contextual. [click 4 — herramientas] Solo entonces aparecen las herramientas de Inspect, Analyze y Studio.
>
> [click 5 — Revelado por etapas] Es jerarquía visual aplicada: las opciones analíticas no compiten con la primera exploración.
>
> [click 6 — Ruido excluido] Además, los paneles de depuración y las mediciones internas quedaron fuera del recorrido público.
>
> [click 7 — captura] La clave está en la ficha: dato y forma en el mismo espacio visual. Si el problema original era la fragmentación de fuentes, la respuesta es integrarlas en un solo plano. [señalar captura]

**Transición:**
> Sobre ese flujo se montan las herramientas de inspección.

**No decir:** "todos los módulos experimentales quedaron publicados"

---

### SLIDE 15 — Inspect y Analyze eliminan el ruido visual para hacer legible el ensamblaje

**Tiempo:** 13:25 – 14:20 · **Pasos:** 4 (1 Inspect, 2 video Inspect, 3 Analyze, 4 video Explode)
**Visual:** dos columnas y dos clips de la build en móvil.

**Guion oral:**

> Inspect y Analyze no se defienden como efectos; se defienden como herramientas para leer relaciones.
>
> [click 1 — Inspect] **Inspect** aísla la pieza seleccionada, conserva el contexto mínimo y responde qué pieza es y dónde está. [click 2 — video] Aquí se ve: selección, aislamiento y ficha.
>
> [click 3 — Analyze] **Analyze** responde cómo se conecta con el resto: vista explosionada, corte transversal y filtros por categoría. [click 4 — video] La vista explosionada separa el ensamblaje sin perder las posiciones relativas.
>
> Técnicamente, estas acciones cambian visibilidad, transformaciones, materiales y estado de selección; por eso dependen de la arquitectura y la taxonomía. En lenguaje simple: la app le quita al usuario parte del trabajo de imaginar qué hay detrás y qué está conectado. [mirar jurado]

**Transición:**
> Studio complementa esa lectura cambiando cómo se ve la superficie.

**No decir:** "Explode demuestra ensamblaje físicamente exacto" · "Analyze hace diagnóstico real"

---

### SLIDE 16 — Los shaders son herramientas de inspección técnica, no filtros estéticos

**Tiempo:** 14:20 – 15:15 · **Pasos:** 6 (1–4 modos, 5 síntesis, 6 video)
**Visual:** Realistic / X-Ray / Solid / Thermal + clip de Studio (X-Ray → Thermal → Solid).

**Guion oral:**

> Studio agrupa los modos visuales. Un **shader** es el programa que define cómo una superficie responde a la luz, el color y la transparencia; un **preset** es una configuración guardada de esa lectura.
>
> [click 1 — Realistic] Realistic orienta: reconocer el dron como objeto real. [click 2 — X-Ray] X-Ray hace visible lo interno. [click 3 — Solid] Solid usa color plano y contorno para leer forma. [click 4 — Thermal] Y Thermal comunica jerarquías relativas por componente; lo precisaré en un momento. Blueprint, la lectura de planos, se activa como preset de Studio.
>
> [click 5 — síntesis] Cada modo responde una pregunta distinta sobre el mismo ensamblaje. [click 6 — video] Aquí, X-Ray, Thermal y Solid en la build real.
>
> El aporte multimedia está ahí: usar la apariencia como herramienta de lectura técnica, no como decoración.

**Transición:**
> Thermal requiere una advertencia explícita.

**No decir:** "los shaders simulan comportamiento físico" · "cada color es una medición real"

---

### SLIDE 17 — Thermal es una visualización heurística, no una simulación FEA calibrada

**Tiempo:** 15:15 – 16:05 · **Pasos:** 2
**Visual:** lista "Límite declarado" + escala relativa (Estructura / ESC-electrónica / Motores-batería).

**Guion oral:**

> Thermal requiere precisión. **Heurístico** significa criterio aproximado para orientar una lectura, no medición calibrada. [pausa larga]
>
> [click 1 — Límite declarado] Thermal no usa sensores ni telemetría y no ejecuta elementos finitos. Internamente es un modelo reducido por componentes, con el factor de carga del dron y tiempos comprimidos; la leyenda en grados es la escala de ese modelo, no una medición.
>
> [click 2 — escala] Por eso la jerarquía es relativa: motores y batería arriba, electrónica en medio, estructura abajo. [señalar escala]
>
> Lo presento como herramienta de comunicación visual, no de diagnóstico. Llevarlo a simulación real exigiría modelo físico, propiedades de material, condiciones de frontera y validación experimental: está en el roadmap.

**Transición:**
> Con ese alcance claro, la demo se lee como evidencia de funcionamiento.

**No decir:** "Thermal diagnostica temperatura" · "Thermal reemplaza FEA" · leer los °C de la leyenda como temperatura real

---

### SLIDE 18 — La demo debe probar tres capacidades, no navegar improvisadamente

**Tiempo:** 16:05 – 16:25 · **Pasos:** 5
**Visual:** tres capacidades + ruta de demo + captura del prototipo.

**Guion oral:**

> Antes de la demo, tres cosas para observar. [click 1 — Selección] Selección: del dron completo a una pieza con su ficha. [click 2 — Relación] Relación: la pieza no pierde su contexto. [click 3 — Modo visual] Modo visual: cambia la lectura, no el alcance. [click 4 — Ruta] Esta es la ruta [click 5 — captura] sobre la build real.

**Transición:**
> [avanzar]

**No decir:** "voy a navegar un poco a ver qué sale"

---

### SLIDE 19 — Demo: de dron completo a pieza, relación y modo visual

**Tiempo:** 16:25 – 19:25 · **Pasos:** 2
**Modo principal: demo en vivo en dos tramos.** (1) La **landing pública** (WebGL puro, sin Unity), ya abierta en otra ventana y posicionada al inicio de la sección 03, con el dron aún sin ensamblar. (2) La **app Unity**, que se abre desde el botón «Abrir visor» de la propia landing. **Respaldo:** el video `vid_01_demo_compilado.mp4` (88 s) de esta slide cubre el tramo de la app; espera en pausa en 0:00 y se reproduce con un clic.

**Procedimiento:**
1. [click 1 — ruta] Revelar la ruta y pasar a la ventana de la landing (Alt+Tab).
2. **0:00–1:10 · Landing.** Pasar el cursor sobre las partículas hasta que el dron se ensamble (~13 s); luego hacer scroll a ritmo constante por los seis capítulos (~7 s cada uno) y, en la mini app final, activar «Explosionar» y desactivarlo.
3. **1:10–2:50 · App Unity.** Subir al inicio y pulsar «Abrir visor»; recorrer selección → Isolate → ficha → Power → Explode → Cut → X-Ray → Thermal → Solid.
4. Volver al deck (Alt+Tab), [click 2 — contingencia] y cerrar.
5. **Regla de corte:** si la app Unity tarda más de 10 s en responder o se congela, volver al deck sin comentarlo, hacer clic sobre el video (arranca en 0:00) y narrar el mismo recorrido. Si falla la landing, saltar directo a la app.

**Guion oral:**

> [click 1 — ruta] Lo muestro en vivo. [Alt+Tab a la landing]
>
> **[0:00]** Esta es la landing pública: WebGL puro, sin Unity. El dron no aparece hecho: lo ensambla la interacción. Paso el cursor y las partículas encuentran su lugar, vértice por vértice. [pausa]
>
> **[0:15]** Con el scroll, la misma escena cuenta la tesis: de CAD a tiempo real, la taxonomía que resalta los motores, los rayos X, la vista explosionada, la lectura térmica heurística y los resultados de la evaluación. **[1:00]** Al final queda una mini app: puedo orbitar y explosionar el modelo.
>
> **[1:10]** Desde aquí abro el visor completo en Unity. [«Abrir visor»] Selecciono el soporte de riel y batería; Isolate lo aísla y la ficha muestra identificación, especificaciones y ensamblaje. Dato y forma en el mismo plano. [señalar ficha]
>
> **[1:40]** En Inspect, el control de energía cambia el estado de carga: arranque, reposo, vuelo. **[1:55]** En Analyze, la vista explosionada y el corte transversal abren el interior sin modificar la malla. **[2:20]** En Studio, X-Ray muestra lo interno; Thermal, la jerarquía relativa; Solid, la forma limpia.
>
> **[2:45]** Dos tecnologías, un mismo objeto: WebGL directo para contar, Unity para inspeccionar. [Alt+Tab al deck] [click 2 — contingencia] La pregunta ya no es si se ve bien, sino bajo qué condiciones corre y qué evidencia produjo con usuarios.

**Plan de contingencia (único, igual en deck, tarjetas y `DEMO_SCRIPT.md`):**
> Demo en vivo como principal. Si la app Unity no responde en 10 s o se congela, se narra el video de esta slide, que registra el mismo recorrido de la app. Si falla la landing, se salta directamente a la app.

**No decir:** "la demo reemplaza la validación" · "la landing es otra versión de la app" (es una capa de divulgación en WebGL puro) · improvisar un recorrido distinto · comentar el fallo si se pasa al video

---

### SLIDE 20 — El profiler interno vuelve trazable el rendimiento por escenario y dispositivo

**Tiempo:** 19:25 – 20:25 · **Pasos:** 2
**Visual:** lista de trazabilidad + extracto literal del JSON exportado por `WebGLProfiler` (sesión thermal_studio, escritorio, 4 jun 2026).

**Guion oral:**

> Para que el rendimiento no quedara en percepción subjetiva, la app integra un **profiler interno**: registra el comportamiento en ejecución —FPS, frame time, memoria— y el contexto de la escena.
>
> [click 1 — trazabilidad] Cada medición queda asociada a build, dispositivo, navegador, resolución y caché. En WebGL esto importa, porque el rendimiento depende del equipo y del navegador, no solo del modelo. Por eso no reporto "un FPS universal".
>
> [click 2 — export JSON] Esto es un extracto literal de una exportación: la sesión Thermal en el escritorio de pruebas, 59,8 FPS y 16,7 milisegundos por cuadro. [señalar] En el mismo archivo aparece el conteo runtime de 229 054 triángulos que vimos antes.
>
> La evidencia técnica cumple dos funciones: demostrar viabilidad en los entornos probados y reconocer sus límites.

**Transición:**
> La lectura por dispositivo es la siguiente.

**No decir:** "el profiler sustituye pruebas en dispositivos reales" · "telemetría" para referirse al profiler

---

### SLIDE 21 — El rendimiento es viable, pero no universal en todo móvil

**Tiempo:** 20:25 – 21:25 · **Pasos:** 5
**Visual:** barras de FPS (Escritorio 59,8 · iOS 58,7 · Redmi Note 10S 26,5 · Android límite inferior 17,6) con línea de 30 FPS + tres lecturas + recuadro.

**Guion oral:**

> El rendimiento se midió en seis configuraciones; muestro cuatro representativas. [click 1 — gráfico] La línea roja es la meta: 30 FPS. [señalar]
>
> [click 2 — Escritorio / iOS] En escritorio —i7-5820K, GTX 980 Ti, Chrome— y en un iPhone 17 Pro, el promedio ronda los 60 FPS: unos 17 milisegundos por cuadro, muy por debajo del presupuesto de 33.
>
> [click 3 — Gama media] En gama media, el Redmi Note 10S promedia 26,5 FPS: por debajo de la meta, pero funcional en el flujo principal, con picos perceptibles. Fue además el teléfono de las sesiones con usuarios.
>
> [click 4 — Gama baja] En el límite inferior, un Android con Adreno 610 queda en 17,6 FPS: navegable, pero bajo la meta.
>
> [click 5 — Compatibilidad declarada] Por eso no proclamo compatibilidad universal: el prototipo es estable en escritorio y funcional con límites en móvil, y lo documento por dispositivo.

**Transición:**
> La segunda parte de la evidencia viene de las sesiones con usuarios.

**No decir:** "funciona perfectamente en cualquier celular" · "los entornos documentados son dos" (eso aplica solo a las sesiones con usuarios)

---

### SLIDE 22 — SUS de 91,88: recepción favorable del visor interactivo 3D

**Tiempo:** 21:25 – 22:25 · **Pasos:** 4
**Visual:** SUS 91,88 (mediana 95 · DE 11,24 · n=12) · Rango 60–100 · Referencia 68 + recuadro "Lectura correcta".

**Guion oral:**

> La muestra fue de doce participantes anonimizados, con perfiles afines al contexto técnico; diez usaron smartphone y dos, PC.
>
> [click 1 — SUS] SUS se aplicó solo al prototipo 3D. El promedio fue **91,88**, con mediana de 95 y desviación estándar de 11,24. [respirar]
>
> [click 2 — Rango] El rango fue de 60 a 100: hubo un caso con más fricción, que Think-Aloud ayuda a explicar.
>
> [click 3 — Referencia] La referencia de 68 es el promedio histórico del instrumento; no es un umbral de aprobación, es un punto de comparación contextual.
>
> [click 4 — Lectura correcta] La lectura correcta: en esta muestra, con este prototipo, la usabilidad percibida fue favorable. SUS no compara 3D con 2D; esa comparación la hacen los dos instrumentos siguientes.

**Transición:**
> [avanzar] Para comparar condiciones, el instrumento principal fue NASA-TLX Raw.

**No decir:** "SUS prueba que el 3D es mejor que el 2D" · "68 es el mínimo para aprobar"

---

### SLIDE 23 — En la muestra, el visor 3D se asoció con menor carga de trabajo percibida

**Tiempo:** 22:25 – 23:30 · **Pasos:** 5 (1 cifra 3D, 2 cifra 2D, 3 diferencia, 4 tabla de tiempos, 5 nota T4)
**Visual:** NASA-TLX 8,69 vs 19,89 + tabla T1–T3 + nota sobre T4.

**Guion oral:**

> NASA-TLX Raw sí se aplicó en ambas condiciones. [click 1 — 3D] En el visor 3D, la carga de trabajo percibida promedió **8,69**. [click 2 — 2D] En el soporte 2D, **19,89**. [click 3 — diferencia] La diferencia pareada media fue de 11,19 puntos, y en los doce casos la carga fue menor en 3D. [pausa]
>
> [click 4 — tiempos] ¿Recuerdan la tarea del motor? Ubicarlo tomó 5,75 segundos en 3D frente a 13 en 2D, y las tres tareas cronometradas sumaron 20,58 frente a 54. [señalar tabla]
>
> [click 5 — Nota] T4 fue exploratoria guiada y no se cronometró; por eso no aparece aquí. Y NASA-TLX mide carga de trabajo percibida, no carga cognitiva de forma directa.
>
> La conclusión correcta: en esta muestra, el visor 3D se asoció con menor carga percibida y menor tiempo medio. Evidencia descriptiva consistente, no inferencia causal. [mirar jurado]

**Transición:**
> Los números describen el patrón. Las verbalizaciones explican por qué ocurrió.

**No decir:** "NASA mide aprendizaje" · "T4 también fue cronometrada" · "el 3D redujo la carga" como afirmación causal

---

### SLIDE 24 — Think-Aloud explica comprensión espacial y fricciones residuales

**Tiempo:** 23:30 – 24:30 · **Pasos:** 4
**Visual:** barras (lima: comprensión espacial 11/12, percepción de claridad 8/12 · ámbar: navegación y control 10/12, iconos procedurales 6/12) + dos listas.

**Guion oral:**

> Think-Aloud explica por qué ocurrió el patrón. Los participantes verbalizan mientras resuelven y esas verbalizaciones se codifican.
>
> [click 1 — gráfico] En lima, lo que apoya la comprensión; en ámbar, las fricciones. [click 2 — Coincide con lo cuantitativo] La categoría más frecuente fue **comprensión espacial**, en 11 de 12 participantes: relacionaban motor, montura y tornillería con el conjunto. La **percepción de claridad** apareció en 8 de 12, asociada a explosionado, Thermal, X-Ray, Blueprint y aislamiento.
>
> [click 3 — Fricciones] Las fricciones fueron claras: **navegación y control** en 10 de 12 —órbita, pan y sensibilidad táctil en móvil—, **iconos procedurales** en 6 y **selección de piezas pequeñas** en 2.
>
> [click 4 — cierre] Detectar fricciones no debilita el trabajo: demuestra que la evaluación fue real y orienta el siguiente ciclo de mejora. [mirar jurado]

**Transición:**
> Esa evidencia combinada informa la discusión.

**No decir:** "navegación y control fue una fortaleza" (en el informe es fricción) · "Think-Aloud es comentario informal"

---

### SLIDE 25 — La discusión acota el resultado: efecto techo, muestra pequeña y compatibilidad limitada

**Tiempo:** 24:30 – 25:40 · **Pasos:** 2
**Visual:** dos columnas: "Lo que sí soporta" / "Lo que no debe afirmarse".

**Guion oral:**

> La discusión delimita qué se puede afirmar.
>
> [click 1 — Lo que sí soporta] La evidencia sostiene cuatro cosas: menor tiempo medio en T1 a T3, menor carga de trabajo percibida, SUS alto para el prototipo y mejor orientación espacial en las verbalizaciones. Las cinco capas apuntan en la misma dirección, y eso fortalece el argumento aunque la muestra sea pequeña.
>
> [click 2 — Lo que no debe afirmarse] Y hay cuatro cosas que no afirmo. Primero, superioridad en éxito: las cuatro tareas se completaron en ambas condiciones, 96 de 96 registros. Eso es un **efecto techo**: la ventaja no está en completar, sino en hacerlo con menos esfuerzo y tiempo. Segundo, generalización o causalidad: n = 12 sirve para validación formativa, no para inferencia poblacional. Tercero, compatibilidad móvil universal. Cuarto, Thermal como simulación física.
>
> La discusión no es una sección de excusas: es donde se demuestra que entiendo lo que hice y lo que no.

**Transición:**
> Con eso, las conclusiones son directas.

**No decir:** "el efecto techo invalida los resultados" · "n=12 prueba todo" · "n=12 no prueba nada"

---

### SLIDE 26 — Las conclusiones cierran cada objetivo con evidencia trazable

**Tiempo:** 25:40 – 26:45 · **Pasos:** 4
**Visual:** tabla OE → resultado → evidencia.

**Guion oral:**

> Las conclusiones responden a los objetivos, uno por uno.
>
> [click 1 — OE1] OE1: el modelo pasó de más de 6,5 millones de triángulos en las rutas CAD a 95 617 en el activo base, con trazabilidad documentada.
>
> [click 2 — OE2] OE2: frame time dentro del presupuesto de 33,33 milisegundos en escritorio, cinco modos visuales, y en móvil un comportamiento funcional pero no universal.
>
> [click 3 — OE3] OE3: la build está publicada y accesible por URL, con selección, ficha contextual, Inspect, Analyze, Studio y Thermal.
>
> [click 4 — OE4] OE4: SUS de 91,88; NASA-TLX de 8,69 frente a 19,89; tiempos T1 a T3 menores en 3D; y Think-Aloud explicando el patrón. [mirar jurado]
>
> Respondiendo a la pregunta de investigación: en esta muestra, el visor 3D se asoció con menor tiempo y menor carga percibida, y es viable en escritorio y funcional, con límites, en móvil.

**Transición:**
> Las limitaciones son parte de las conclusiones, no su negación.

---

### SLIDE 27 — Las limitaciones son alcance declarado, no fallas ocultas

**Tiempo:** 26:45 – 27:25 · **Pasos:** 5
**Visual:** cuatro tarjetas + recuadro "Rigor metodológico".

**Guion oral:**

> Las limitaciones son decisiones documentadas. [click 1 — Modelo único] Modelo único: la arquitectura está preparada para generalizar, pero no se validó con otros drones. [click 2 — n = 12] Muestra de doce: suficiente para validación formativa, no para inferencia. [click 3 — Adaptación PC] Escritorio: el diseño es mobile-first; la versión PC es una adaptación funcional. [click 4 — Cables] Cables y electrónica interna quedaron fuera del alcance del MVP por tiempo. [click 5 — Rigor] Declararlos con precisión define el espacio de validez de lo que afirmo.

**Transición:**
> Por eso el trabajo futuro está definido como ruta.

---

### SLIDE 28 — El trabajo futuro es una ruta de madurez, no una lista de deseos

**Tiempo:** 27:25 – 27:55 · **Pasos:** 6
**Visual:** escalera Visual Product Twin → Digital Shadow → Digital Twin + tres columnas (Fases 0–1, 2–3, 4–5, Fig. 80 del informe).

**Guion oral:**

> El trabajo futuro es una escalera que parte de lo construido. [click 1 — Digital Shadow] El siguiente nivel es digital shadow; [click 2 — Digital Twin] el último, digital twin operacional. [click 3 — Fases 0 → 5] El informe lo ordena en seis fases: [click 4 — Fases 0–1] ampliar la validación y formalizar un twin manifest por pieza; [click 5 — Fases 2–3] luego, telemetría histórica y en vivo; [click 6 — Fases 4–5] y solo al final, modo servicio y un gemelo operacional con modelos calibrados.

**No decir:** "la próxima versión será un digital twin" · "FEA en servidor" (no está en el informe)

---

### SLIDE 29 — La contribución es técnica, metodológica y comunicativa

**Tiempo:** 27:55 – 28:25 · **Pasos:** 3

**Guion oral:**

> Empecé con la distancia entre la información disponible y la comprensión espacial. Esta tesis muestra que esa distancia se puede acortar desde el navegador. La contribución tiene tres dimensiones. [click 1 — Técnica] Técnica: un pipeline CAD a WebGL documentado y trazable, con taxonomía, modos visuales y profiler. [click 2 — Metodológica] Metodológica: evaluación formativa triangulada, con comparación intra-sujeto 3D frente a 2D. [click 3 — Comunicativa] Comunicativa: hardware complejo legible desde la web, delimitado como visual product twin.

**No decir:** "visual product twin es una categoría validada" — el informe la propone como categoría operativa.

---

### SLIDE 30 — Cierre

**Tiempo:** 28:25 – 28:30 · **Pasos:** ninguno

**Guion oral:**

> [avanzar] Muchas gracias. Quedo atento a sus preguntas.

**No decir:** abrir un tema nuevo · pedir disculpas · alargar el cierre.

---

## 7. Respuestas a preguntas frecuentes del jurado

### P1: ¿Por qué Unity y no Three.js o Babylon.js?

> La decisión no fue por tamaño de build — Unity tiene una huella inicial mayor. La decisión fue por integración de pipeline: editor visual, profiler nativo, sistema de materiales URP, UI Toolkit y flujo coherente entre arte técnico, programación y evaluación desde una sola base de trabajo.

### P2: ¿Por qué n=12 y no más participantes?

> La meta deseable era 30 participantes. El escenario mínimo operativo para validación formativa era entre 8 y 12. El informe integra 12 participantes y lo declara como lectura descriptiva y exploratoria, sin pretensión de inferencia poblacional.

### P3: ¿Los 95 617 y los 229 054 triángulos se contradicen?

> No. 95 617 es el activo base optimizado y sus masters principales. 229 054 es el conteo estimado por el profiler sobre la escena runtime instrumentada, con instancias, proxies, assets de apoyo y renderers adicionales. Una mide el modelo; la otra, la escena en ejecución.

### P4: ¿Thermal se puede convertir en simulación real?

> Sí, técnicamente. Requeriría modelo físico con propiedades de material, condiciones de frontera, un solver FEA o equivalente y datos reales. Está descrito como trabajo futuro, no como capacidad actual.

### P5: Si Thermal no mide temperatura, ¿por qué la leyenda muestra °C?

> Porque el subsistema es un modelo reducido por componentes que calcula una temperatura por nodo a partir del factor de carga del dron, con tiempos deliberadamente comprimidos. La escala en °C es la escala de ese modelo heurístico, no una medición ni una simulación calibrada; el informe lo documenta como simulación térmica híbrida y heurística.

### P6: ¿Por qué 257 renderers en la taxonomía y 252 en el profiler?

> Son conteos de fuentes distintas: 257 es la auditoría de renderers y colliders de la escena final (convención 28/30/257); 252 son los renderers y mallas que el profiler interno contó en la build instrumentada. El informe reporta cada cifra con su fuente y no las equipara.

### P7: ¿Qué haría diferente si lo repitiera?

> Aumentar el tamaño de muestra, diseñar una experiencia de escritorio específica desde el inicio en vez de adaptar la móvil, e incluir usuarios de perfil industrial para contrastar la lectura técnica.

### P8: ¿Por qué el Holybro X500 V2?

> Por disponibilidad de recursos abiertos: archivos CAD/STEP consultables, documentación pública y referencias técnicas verificables. Eso permitió trabajar sin material propietario restringido y con trazabilidad para justificar decisiones de modelado.

---

## 8. Cortes de emergencia

- **Si quedan menos de 7 minutos al llegar a la slide 20:** fusionar 20–21 y decir solo: el profiler exporta por dispositivo; escritorio ≈60 FPS, Redmi 26,5 funcional, límite inferior 17,6; compatibilidad no universal. Continuar en la 22.
- **Si quedan menos de 5 minutos al llegar a la slide 22:** fusionar 22–24: SUS 91,88 solo en 3D; NASA 8,69 frente a 19,89; T1–T3 menores en 3D; fricciones en navegación móvil, iconos y piezas pequeñas. Continuar en la 25 con solo la columna derecha.
- **Si el jurado interrumpe durante la demo:** detener la interacción y responder primero con alcance: *"puedo mostrar lo publicado; las capacidades no integradas en la UI final no las presento como alcance visible."*
- **Si la build no responde durante la demo en vivo:** volver al deck y narrar el video de la slide 19 (clic sobre el video: arranca en 0:00). No improvisar otra ruta ni comentar el fallo.
