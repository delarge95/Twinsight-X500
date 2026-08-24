# Guion maestro de sustentacion - TwinSight X500

Estado: canonico para defensa academica.
Fecha de actualizacion: 2026-06-25.
Duracion objetivo: 28:30 de exposicion real + 1:30 de margen.
Fuente autoritativa: `Informe_final/informe_final.pdf`.
Carpeta de apoyo: `Informe_final/presentation/`.
Uso complementario: ensayar tiempo con `SPEAKER_CARDS.md`, profundizar con `DEFENSE_STUDY_GUIDE.md`, verificar fuentes con `BIBLIOGRAPHY_EVIDENCE_ATLAS.md` y comprobar claims con `DEFENSE_EVIDENCE_MAP.md`.

## 1. Regla de uso

Este documento no es un parrafo para memorizar de corrido. Es una partitura oral: cada slide tiene una tesis, una evidencia visual, un guion base, una transicion y una zona de riesgo. La defensa debe sonar conversacional, tecnica y situada.

La columna vertebral de toda la presentacion sigue los lineamientos del deep research de storytelling tecnico:

```text
problema -> teoria que explica el problema -> alcance -> metodo -> decisiones tecnicas -> implementacion -> evidencia -> limites -> contribucion
```

Frase de tesis oral:

> TwinSight X500 no se defiende como un gemelo digital operacional. Se defiende como un visual product twin: una capa web 3D, optimizada y semanticamente organizada, que hace legibles piezas, relaciones y modos de inspeccion de un hardware complejo.

## 2. Regla de prerequisito conceptual

Ningun termino debe aparecer como argumento antes de haber sido definido. Si un concepto tecnico aparece por primera vez, el guion lo explica en lenguaje comun y luego en lenguaje tecnico.

| Termino | Primera definicion en la ruta | Uso posterior permitido |
|---|---:|---|
| CAD | Slide 1 | Pipeline, activo base, optimizacion |
| WebGL | Slide 1 | Build, runtime, rendimiento |
| Hardware complejo | Slide 2 | Problema, alcance, contribucion |
| Reconstruccion espacial | Slide 2 | Carga cognitiva, tareas, Think-Aloud |
| Carga cognitiva | Slide 3 | Discusion e interpretacion de NASA-TLX |
| Carga intrinseca, extrinseca y germana | Slide 3 | Discusion; nunca como medicion directa |
| Aprendizaje multimedia | Slide 4 | Diseno de slides, app, demo |
| Senalizacion, segmentacion y coherencia | Slide 4 | Animaciones, diagramas, demo |
| Significadores | Slide 4 | UI, iconos, microinteracciones |
| Digital twin, digital shadow y visual product twin | Slide 5 | Alcance, limitaciones, trabajo futuro |
| Telemetria, FEA y simulacion calibrada | Slide 5 | Limites de Thermal y roadmap |
| DSR / Design Science Research | Slide 7 | Metodologia y contribucion |
| Validacion formativa y descriptiva | Slide 7 | Resultados y conclusiones |
| KPIs | Slide 8 | Rendimiento |
| SUS | Slide 8 | Resultados de usabilidad del prototipo 3D |
| NASA-TLX Raw | Slide 8 | Workload percibido por condicion |
| Think-Aloud | Slide 8 | Triangulacion cualitativa |
| Runtime | Slide 9 | Arquitectura, profiler, rendimiento |
| Profiler | Slide 19 | Resultados tecnicos |
| Taxonomia | Slide 12 | Seleccion, hotspots, piezas, fasteners |
| Shaders y presets | Slide 15 | Studio, modos visuales |
| Heuristico | Slide 16 | Thermal |
| Efecto techo | Slide 24 | Discusion metodologica |

## 3. Convenciones orales

- `[pausa]`: detenerse 1 segundo.
- `[pausa larga]`: detenerse 2 segundos.
- `[mirar jurado]`: levantar la vista y cerrar una idea importante.
- `[senalar]`: marcar una zona especifica del visual.
- `[click]`: revelar siguiente capa o pasar slide.
- `[respirar]`: bajar velocidad antes de un dato numerico o una limitacion.

## 4. Apertura antes de iniciar

Antes de hablar:

1. Respirar, pies estables, mirada al jurado.
2. Verificar que la app, el video local y la deck esten abiertos.
3. Mantener en mente tres mensajes:
   - ver piezas no basta; hay que comprender relaciones;
   - el aporte integra teoria cognitiva, pipeline 3D, WebGL, UI de inspeccion y evaluacion formativa;
   - la evidencia es favorable, pero acotada y descriptiva.

Si hay nervios, no acelerar. La primera pausa comunica control.

## 5. Guion principal slide por slide

### Slide 1 - TwinSight X500 convierte un ensamblaje CAD en una experiencia WebGL inspeccionable

Tiempo: 0:00-0:45.

Objetivo: presentar identidad, caso de estudio, definicion minima de CAD/WebGL y alcance sin sobreprometer.

Visual: hero de la app con el dron completo visible, titulo, autor, programa, universidad, enlace o QR de demo.

Guion oral:

> Buenos dias, miembros del jurado. Mi nombre es Alexander Woodcock Salomon y hoy presento TwinSight X500, un prototipo de visualizacion 3D interactiva para inspeccion tecnica del dron Holybro X500 V2.
>
> Cuando digo CAD me refiero a modelos de diseno asistido por computador: utiles para ingenieria y manufactura, pero no necesariamente preparados para consulta rapida en navegador. Cuando digo WebGL me refiero a render 3D dentro del navegador, sin instalar una aplicacion nativa. [pausa]
>
> La idea central es transformar un ensamblaje complejo, que normalmente se consulta en planos, manuales o archivos CAD pesados, en una experiencia web explorable, seleccionable y explicable.
>
> Durante la sustentacion voy a defender tres cosas: el problema tecnico y cognitivo que origina el proyecto, las decisiones de implementacion que hicieron viable la app y la evidencia metodologica que permite interpretarla con honestidad academica.

Clicks/movimiento: no animar demasiado; dejar que el dron sea la primera senal visual.

Transicion:

> Para entender por que esto importa, primero hay que mirar el problema que existia antes de la app.

Evidencia base:

- `Informe_final/chapters/01_introduccion.tex`, introduccion y planteamiento del problema.
- `README.md`, resumen publico y flujo visible.
- `Informe_final/figures/screenshots_contextual/fig_ui_hero_mobile_pc.png`.

No decir:

- "Gemelo digital completo".
- "Simulador operacional".
- "Producto final industrial".

### Slide 2 - El problema no es falta de informacion, sino reconstruccion espacial

Tiempo: 0:45-1:45.

Objetivo: instalar el problema humano y tecnico sin atacar la documentacion 2D.

Visual: comparacion en tres paneles: manual 2D, CAD pesado, app web 3D.

Guion oral:

> La documentacion tecnica tradicional no es un error. Un manual 2D o un plano cumplen una funcion necesaria: nombran piezas, muestran pasos y organizan informacion. El limite aparece cuando el usuario debe reconstruir mentalmente profundidad, orientacion, piezas ocultas y relaciones de ensamblaje.
>
> A eso llamo reconstruccion espacial: el esfuerzo de convertir vistas planas, textos o archivos tecnicos en una imagen mental tridimensional del sistema. En un hardware complejo como el X500, esa reconstruccion no ocurre sobre una sola pieza, sino sobre componentes estructurales, energeticos, electronicos y de control que se conectan entre si. [senalar comparacion]
>
> Por eso el problema no era "falta de informacion". Era una distancia entre informacion disponible y comprension espacial.

Clicks/movimiento: revelar primero manual/CAD y despues la app.

Transicion:

> Esa distancia se entiende mejor desde la teoria de carga cognitiva.

Evidencia base:

- `Informe_final/chapters/01_introduccion.tex`, definicion de hardware complejo.
- `Informe_final/figures/chapter1/fig_1_fragmentacion_hardware_complejo.pdf`.
- `BIBLIOGRAPHY_EVIDENCE_ATLAS.md`, Sweller y Hegarty/Waller.

No decir:

- "El 2D no sirve".
- "El 3D siempre es mejor".

### Slide 3 - La teoria de carga cognitiva explica por que la forma de presentar importa

Tiempo: 1:45-2:55.

Objetivo: introducir teoria cognitiva antes de usar SUS, NASA, resultados o discusion.

Visual: diagrama 2D -> memoria de trabajo -> carga extrinseca / 3D guiado -> apoyo visual -> comprension espacial.

Guion oral:

> La teoria de carga cognitiva parte de una idea sencilla: la memoria de trabajo es limitada. En palabras simples, no podemos sostener muchas piezas nuevas de informacion al mismo tiempo y manipularlas mentalmente sin costo. En terminos tecnicos, Sweller distingue tres cargas: intrinseca, extrinseca y germana.
>
> La carga intrinseca viene de la complejidad propia del contenido. Un dron multicomponente es complejo aunque la interfaz sea buena. La carga extrinseca viene de como se presenta la informacion: si obligo al usuario a saltar entre planos, tablas y vistas no conectadas, aumento esfuerzo que no ayuda a entender. La carga germana es el esfuerzo util que si construye esquemas: por ejemplo, entender funcion, ubicacion y relacion de una pieza.
>
> Esta tesis no afirma medir esas tres cargas directamente. Las usa como marco para explicar por que una interfaz 3D guiada puede reducir reconstruccion espacial innecesaria. La frase de respaldo a recordar del atlas bibliografico es: "limited working memory which can only process". Es una cita corta de Sweller et al. para anclar la idea de memoria de trabajo limitada.

Clicks/movimiento: revelar las tres cargas una por una; no mostrar todo al inicio.

Transicion:

> Esa misma logica tambien guia como se debe presentar una tesis tecnica y como se disena la interfaz.

Evidencia base:

- `Informe_final/chapters/02_marco_referencia.tex`, teoria de carga cognitiva.
- `Informe_final/figures/chapter2/fig_2_carga_cognitiva_2d_3d.pdf`.
- `BIBLIOGRAPHY_EVIDENCE_ATLAS.md`, filas de Sweller 1988 y Sweller et al. 2019.

No decir:

- "NASA-TLX mide carga intrinseca, extrinseca y germana".
- "El 3D elimina la carga cognitiva".

### Slide 4 - El aprendizaje multimedia exige segmentar, senalizar y quitar ruido

Tiempo: 2:55-4:05.

Objetivo: explicar la teoria que guia el deck, la demo y decisiones de interaccion.

Visual: cuatro principios aplicados: una idea por slide, senalizacion visual, segmentacion progresiva, coherencia sin decoracion.

Guion oral:

> La segunda base teorica es el aprendizaje multimedia: las personas construyen representaciones mentales combinando informacion visual y verbal. En palabras sencillas: una imagen y una explicacion pueden ayudarse, pero tambien pueden competir si la pantalla se llena de texto, numeros y decoracion.
>
> Por eso uso cuatro principios en la sustentacion y en la app. Segmentacion: mostrar un proceso por partes. Senalizacion: marcar que debe mirar el usuario. Coherencia: quitar lo que no apoya la idea central. Y preentrenamiento: definir primero los terminos que luego se van a usar.
>
> En interfaz, esto se traduce en hotspots, bottom sheet, iconos y microinteracciones que actuan como significadores: pistas visibles de lo que se puede hacer. En esta presentacion, se traduce en una regla: primero enseno como leer el diagrama o la metrica, y despues digo que significa.
>
> La fuente de apoyo local resume esta idea con la frase "build mental representations from words and pictures". Como Mayer no es una referencia canonica del informe final, lo uso aqui como criterio de diseno de comunicacion, no como resultado empirico de la tesis.

Clicks/movimiento: construir la slide por capas: primero problema de saturacion, luego principios, luego aplicacion a app/deck.

Transicion:

> Con esas bases, puedo delimitar con precision que es y que no es TwinSight.

Evidencia base:

- `desarrollo/docs/investigacion/deep-research-report_storytelling.md`, lineamientos de CTML, una idea por slide y segmentacion.
- `BIBLIOGRAPHY_EVIDENCE_ATLAS.md`, fila de Mayer como fuente local de apoyo no canonica.
- `Informe_final/chapters/04_desarrollo.tex`, iconos, bottom sheet y microinteracciones.

No decir:

- "Mayer demuestra los resultados de TwinSight".
- "La presentacion debe ser llamativa por si misma".

### Slide 5 - TwinSight es un visual product twin, no un digital twin operacional

Tiempo: 4:05-5:15.

Objetivo: fijar alcance academico y evitar objeciones por sobrepromesa.

Visual: frontera incluido/excluido: visual product twin, digital model enriquecido, digital shadow futuro, digital twin operacional excluido.

Guion oral:

> Una parte clave de la defensa es llamar al sistema por su nombre correcto. Un digital twin operacional implica sincronizacion con un activo fisico o con datos que reflejan su estado. Un digital shadow agrega flujo de datos desde el activo hacia el modelo. TwinSight no esta en ese nivel.
>
> TwinSight X500 no recibe telemetria real, no sincroniza estado con un dron fisico, no ejecuta mantenimiento predictivo, no se integra con PLM, CMMS, SCADA o IoT, y Thermal no es una simulacion FEA calibrada. FEA aqui significa analisis por elementos finitos: simulacion fisica numerica para estimar esfuerzos, temperatura u otros fenomenos.
>
> Lo que si entrega es una capa visual-semantica: un visual product twin. Organiza el producto en piezas, categorias, datos contextuales, herramientas de inspeccion, modos visuales y medicion tecnica de la build. [pausa]
>
> Dicho de forma directa: esta tesis no promete operar el dron desde datos vivos; promete hacer legible un hardware complejo desde la web y dejar una base trazable para fases futuras.

Clicks/movimiento: revelar incluido, luego excluido, luego la frase "visual product twin".

Transicion:

> Con esa frontera clara, los objetivos se entienden como contrato verificable.

Evidencia base:

- `Informe_final/chapters/01_introduccion.tex`, alcance y limitaciones.
- `Informe_final/chapters/03_marco_metodologico.tex`, frontera metodologica.
- `README.md`, Academic Scope y Capability Status.
- `Informe_final/chapters/04_desarrollo.tex`, digital model enriquecido y roadmap.

No decir:

- "TwinSight ya es un gemelo digital completo".
- "Thermal calcula temperatura real".
- "La app garantiza compatibilidad universal".

### Slide 6 - Los objetivos conectan pipeline 3D, interaccion, rendimiento y evaluacion

Tiempo: 5:15-6:10.

Objetivo: mostrar que la ruta del proyecto cubre construccion y evaluacion.

Visual: matriz 2x2 con OE1 pipeline, OE2 materiales/modos, OE3 prototipo WebGL, OE4 evaluacion.

Guion oral:

> El objetivo general fue desarrollar un prototipo web 3D interactivo basado en Unity Web, orientado a exploracion tecnica, inspeccion y analisis visual del ensamblaje.
>
> Los objetivos especificos separan el problema en frentes verificables. Primero, disenar un pipeline de optimizacion de activos CAD hacia WebGL. Segundo, integrar materiales y modos visuales. Tercero, implementar la experiencia web con navegacion, seleccion, ficha contextual y herramientas analiticas. Cuarto, evaluar el prototipo con evidencia tecnica y usuarios.
>
> La logica es importante: no se evaluo una idea abstracta. Se evaluo un artefacto construido.

Clicks/movimiento: revelar OE por cuadrantes.

Transicion:

> Por eso la metodologia no podia ser solo desarrollo de software; tenia que evaluar el artefacto.

Evidencia base:

- `Informe_final/chapters/01_introduccion.tex`, objetivos.
- `Informe_final/chapters/03_marco_metodologico.tex`, tipo de investigacion.

No decir:

- "Todos los objetivos son puramente tecnicos".
- "La evaluacion ya prueba generalizacion poblacional".

### Slide 7 - La metodologia usa DSR y validacion formativa descriptiva

Tiempo: 6:10-7:15.

Objetivo: definir DSR y el caracter formativo/descriptivo antes de resultados.

Visual: ciclo DSR aplicado: problema, objetivos, diseno, demostracion, evaluacion, comunicacion.

Guion oral:

> La investigacion se enmarca como aplicada, con enfoque mixto y predominio cualitativo-formativo. El marco principal es Design Science Research, o DSR. En sencillo: se produce conocimiento construyendo y evaluando un artefacto que responde a un problema practico.
>
> En tecnico, DSR exige que el artefacto no sea solo programado, sino justificado, demostrado, evaluado y comunicado. Por eso el informe conecta problema, objetivos, construccion, mediciones tecnicas, usuarios, discusion y anexos.
>
> La evaluacion fue formativa y descriptiva. Formativa significa que busca aprender del prototipo y detectar mejoras; descriptiva significa que reporta patrones observados sin vender inferencia estadistica poblacional. En una tesis de pregrado de Ingenieria Multimedia, esa decision es defendible si hay trazabilidad, instrumentos claros y honestidad de alcance.

Clicks/movimiento: construir ciclo por etapas.

Transicion:

> Para sostener esa lectura, la evaluacion se diseno por capas, no con una sola metrica.

Evidencia base:

- `Informe_final/chapters/03_marco_metodologico.tex`, marco DSR y fases.
- `Informe_final/figures/chapter3/fig_3_dsrm_aplicado_proyecto.pdf`.
- `BIBLIOGRAPHY_EVIDENCE_ATLAS.md`, Peffers et al. 2007.

No decir:

- "Se probo causalidad".
- "La muestra representa a toda la poblacion".

### Slide 8 - La evaluacion triangula KPIs, tareas, SUS, NASA-TLX Raw y Think-Aloud

Tiempo: 7:15-8:35.

Objetivo: definir todos los instrumentos antes de usarlos en resultados.

Visual: diagrama de triangulacion con cinco capas y una frase de que mide cada una.

Guion oral:

> La evaluacion tiene cinco capas. Primera: KPIs tecnicos. KPI significa indicador clave de desempeno; aqui incluye FPS, frame time, memoria, build y profiler. Segunda: desempeno en tareas, con completitud, ayudas y tiempos para T1, T2 y T3. T4 se trato aparte porque fue exploratoria.
>
> Tercera: SUS. SUS es System Usability Scale, una escala breve de diez items para percepcion global de usabilidad. En esta tesis se aplico solo al prototipo 3D, no como comparacion 3D contra 2D.
>
> Cuarta: NASA-TLX Raw. Es una escala de carga de trabajo percibida con dimensiones como demanda mental, fisica, temporal, esfuerzo, frustracion y rendimiento. Raw significa que se promedian subescalas sin ponderacion pareada. En esta adaptacion, rendimiento se diligencio invertido para mantener la direccion del promedio.
>
> Quinta: Think-Aloud. Es verbalizacion concurrente: el participante piensa en voz alta durante la tarea, y esas verbalizaciones se codifican para explicar claridad, friccion y comprension.
>
> La triangulacion consiste en no depender de una sola fuente: rendimiento tecnico, conducta en tareas, percepcion subjetiva y comentarios cualitativos se leen juntos.

Clicks/movimiento: revelar cada instrumento y su limite.

Transicion:

> Con el metodo claro, paso al primer reto de ingenieria: convertir CAD pesado en WebGL usable.

Evidencia base:

- `Informe_final/chapters/03_marco_metodologico.tex`, variables e instrumentos.
- `Informe_final/chapters/05_resultados.tex`, resultados SUS/NASA/Think-Aloud.
- `Informe_final/validacion/03_CUESTIONARIO_SUS_PARTICIPANTE.md`.
- `Informe_final/validacion/04_CUESTIONARIO_NASA_TLX_PARTICIPANTE.md`.
- `Informe_final/validacion/05_FORMATO_REGISTRO_MODERADOR.md`.
- `BIBLIOGRAPHY_EVIDENCE_ATLAS.md`, Brooke, Hart, Bangor y Ericsson/Simon si aplica.

No decir:

- "NASA mide carga cognitiva teorica de forma directa".
- "SUS demuestra que 3D es mejor que 2D".
- "Think-Aloud sustituye los datos cuantitativos".

### Slide 9 - El pipeline traduce activos CAD a geometria runtime para WebGL

Tiempo: 8:35-9:50.

Objetivo: explicar el pipeline como traduccion tecnica, no como importacion directa.

Visual: CAD/STEP -> MoI3D/STEPper/Blender -> limpieza -> retopologia/proxies -> bake -> FBX -> Unity WebGL.

Guion oral:

> El reto 3D no fue importar un modelo y ponerlo en pantalla. Los modelos CAD de manufactura no estan pensados para render en tiempo real. Pueden traer superficies convertidas con n-gons, vertices repetidos, caras internas, piezas repetidas como mallas unicas y detalle geometrico innecesario para inspeccion.
>
> Runtime significa tiempo de ejecucion: lo que realmente corre cuando el usuario abre la app. Una escena runtime para WebGL necesita geometria, materiales, jerarquias, memoria y eventos de interaccion bajo restricciones del navegador.
>
> Por eso el pipeline combina rutas de importacion, saneamiento geometrico, remodelado, optimizacion, bake de mapas y exportacion a Unity. La decision tecnica no fue conservar todo el CAD original, sino traducirlo a un activo runtime legible.
>
> La ganancia no es solo peso. Es poder seleccionar, aislar, explotar, etiquetar y medir la escena sin romper la lectura del ensamblaje.

Clicks/movimiento: mostrar pipeline de izquierda a derecha.

Transicion:

> Esa diferencia entre activo optimizado y escena runtime explica una cifra que puede parecer contradictoria.

Evidencia base:

- `Informe_final/chapters/04_desarrollo.tex`, pipeline y optimizacion.
- `Informe_final/figures/chapter4/fig_4_pipeline_modelado_bake.pdf`.
- `Informe_final/figures/screenshots_contextual/fig_cad_bake_high_pair.png`.
- `Informe_final/figures/screenshots_contextual/fig_cad_bake_low_pair.png`.

No decir:

- "El CAD original se uso intacto".
- "La optimizacion fue solo bajar poligonos".

### Slide 10 - 95 617 y 229 054 triangulos son metricas distintas, no una contradiccion

Tiempo: 9:50-10:45.

Objetivo: neutralizar una objecion tecnica recurrente.

Visual: dos tarjetas comparativas: activo base optimizado vs escena runtime instrumentada.

Guion oral:

> En el informe aparecen dos cifras que deben leerse con cuidado. La primera es 95 617 triangulos: corresponde al activo base optimizado exportado. Es una metrica del modelo principal despues del proceso de optimizacion.
>
> La segunda es 229 054 triangulos estimados: corresponde a la escena runtime instrumentada observada por profiler. Esa escena incorpora instancias, proxies, renderers activos, assets de apoyo y elementos necesarios para la interaccion.
>
> Por eso no son metricas equivalentes. Una mide el activo optimizado base; la otra mide una escena en ejecucion con componentes adicionales. Lo importante en defensa no es esconder la diferencia, sino explicarla junto a la primera tabla donde aparece.

Clicks/movimiento: mostrar 95 617, despues 229 054, despues "no equivalentes".

Transicion:

> Para que esa escena fuera mantenible, la arquitectura se separo en capas.

Evidencia base:

- `Informe_final/chapters/04_desarrollo.tex`, cierre de activo optimizado.
- `Informe_final/validacion/07_TABLAS_RENDIMIENTO_WEBGL_MEDICIONES.tex`.
- `Informe_final/presentation/DEFENSE_EVIDENCE_MAP.md`, valores defendibles.

No decir:

- "Se redujo de 229 054 a 95 617".
- "Ambas cifras miden lo mismo".

### Slide 11 - La arquitectura separa UI, estados, datos, escena y medicion

Tiempo: 10:45-11:55.

Objetivo: demostrar que la app no es un viewer aislado.

Visual: diagrama de arquitectura por capas.

Guion oral:

> La aplicacion se organizo por capas. En la superficie esta la UI: Hero, Explore, bottom sheet y los modos Inspect, Analyze y Studio. Debajo esta la coordinacion de estados: seleccion, visibilidad, exploded view, corte transversal, modos visuales y thermal.
>
> Luego esta la capa de datos, con piezas, categorias, fichas y assets. Finalmente estan la escena runtime, shaders y profiler. Esta separacion importa porque evita que cada boton sea una solucion aislada. Cada interaccion modifica estado, lectura visual y evidencia tecnica.
>
> En palabras sencillas: la app no solo muestra un dron. Mantiene una estructura para saber que pieza se selecciona, que informacion aparece, que modo se activa y como se mide el comportamiento.

Clicks/movimiento: revelar por capas de arriba hacia abajo.

Transicion:

> La capa de datos se apoya en una taxonomia de piezas y relaciones.

Evidencia base:

- `Informe_final/chapters/04_desarrollo.tex`, arquitectura runtime.
- `Informe_final/Manual_tecnico/manual_tecnico.pdf`.
- `Informe_final/figures/chapter4/fig_4_arquitectura_general_twinsight.pdf`.

No decir:

- "Es solo un visor 3D".
- "La UI esta desconectada de la escena".

### Slide 12 - La taxonomia vuelve seleccionables piezas, hotspots y fasteners

Tiempo: 11:55-12:55.

Objetivo: explicar taxonomia antes de mostrar flujos de inspeccion.

Visual: jerarquia 28 categorias, 30 piezas madre, 257 subpiezas, hotspots y fasteners.

Guion oral:

> Taxonomia significa clasificacion operativa. En esta tesis no es una lista decorativa: es el sistema que permite organizar componentes, piezas madre, subpiezas, hotspots y fasteners.
>
> Un hotspot es un punto interactivo que llama la atencion sobre una zona o pieza. Un fastener es un elemento de sujecion, como tornilleria o fijaciones. Sin esta organizacion, la app podria girar el modelo, pero no entenderia que selecciona el usuario ni que ficha debe abrir.
>
> La taxonomia final no pretende ser inventario industrial absoluto del dron. Pretende ser una estructura funcional y trazable para inspeccion visual, seleccion y consulta dentro del prototipo.

Clicks/movimiento: mostrar jerarquia primero y luego ejemplos en UI.

Transicion:

> Esa estructura se ve en el flujo publico de la app.

Evidencia base:

- `Informe_final/chapters/04_desarrollo.tex`, taxonomia y saneamiento de jerarquia.
- `Informe_final/figures/screenshots_contextual/fig_ui_info_panel.png`.
- `Informe_final/figures/screenshots_contextual/fig_explore_hotspot_selection.png`.
- `Informe_final/validacion/02_BUILD_CLOSURE_ACADEMICO.md`.

No decir:

- "La taxonomia es una BOM certificada".
- "Cada tornillo del dron real esta auditado industrialmente".

### Slide 13 - El flujo publico concentra Explore, seleccion y bottom sheet

Tiempo: 12:55-13:55.

Objetivo: mostrar la experiencia evaluada sin mencionar modulos no publicos.

Visual: captura Explore, seleccion de pieza y bottom sheet.

Guion oral:

> La UI publica se concentra en Explore y sus acciones visibles: navegar el ensamblaje, seleccionar una pieza, abrir una ficha contextual y activar herramientas de inspeccion.
>
> El bottom sheet es el panel inferior que aparece con informacion contextual. Su funcion es evitar que el usuario tenga que abandonar la escena para consultar datos. La informacion aparece al lado de la accion, no en un documento separado.
>
> Esta decision conecta teoria y construccion: si la dificultad inicial era reconstruir relaciones dispersas, la UI intenta mantener pieza, contexto y accion en el mismo espacio visual.

Clicks/movimiento: mostrar antes de seleccionar, seleccion y panel abierto.

Transicion:

> Sobre ese flujo se montan herramientas de inspeccion para reducir ruido visual.

Evidencia base:

- `Informe_final/chapters/04_desarrollo.tex`, interfaz.
- `Informe_final/figures/screenshots_contextual/fig_ui_explore_mobile_pc.png`.
- `Informe_final/figures/screenshots_contextual/fig_ui_info_panel.png`.
- `README.md`, public build scope.

No decir:

- "Todos los modulos experimentales quedaron publicados".
- "La UI reemplaza documentacion tecnica completa".

### Slide 14 - Inspect y Analyze ayudan a leer relaciones, no solo a activar efectos

Tiempo: 13:55-14:55.

Objetivo: conectar funciones visibles con comprension espacial.

Visual: secuencia Isolate, Explode, Cut, filtros o outputs de Analyze.

Guion oral:

> Inspect y Analyze no se defienden como efectos visuales. Se defienden como herramientas para leer relaciones. Isolate reduce ruido alrededor de una pieza. Explode separa componentes para ver ensamblaje. Cut ayuda a inspeccionar interior o capas. Los filtros permiten concentrar la atencion.
>
> En palabras sencillas: la app le quita al usuario parte del trabajo de imaginar que hay detras, que esta conectado y que cambia cuando separo un componente.
>
> En tecnico, estas acciones modifican visibilidad, transformaciones, materiales y estados de seleccion sobre la escena runtime. Por eso dependen de la arquitectura y de la taxonomia explicadas antes.

Clicks/movimiento: animar secuencia corta, no recorrer menu completo.

Transicion:

> Los modos visuales complementan esa lectura cambiando como se interpreta la superficie.

Evidencia base:

- `Informe_final/chapters/04_desarrollo.tex`, herramientas de inspeccion.
- `Informe_final/figures/screenshots_contextual/fig_explore_isolate_sequence.png`.
- `Informe_final/figures/screenshots_contextual/fig_analyze_tool_outputs.png`.

No decir:

- "Explode demuestra ensamblaje fisicamente exacto".
- "Analyze hace diagnostico real del dron".

### Slide 15 - Studio y los shaders producen lecturas visuales complementarias

Tiempo: 14:55-15:55.

Objetivo: definir shaders/presets y conectar visualidad con funcion tecnica.

Visual: Realistic, X-Ray, Solid, Thermal y presets de Studio.

Guion oral:

> Studio agrupa modos visuales y presets. Un shader es el programa o conjunto de instrucciones que define como una superficie responde a luz, color, transparencia o estilo. Un preset es una configuracion guardada para cambiar rapidamente esa lectura.
>
> Realistic favorece reconocimiento visual. X-Ray ayuda a leer superposiciones. Solid reduce textura para concentrarse en forma. Otros modos apoyan comparacion o inspeccion. El aporte multimedia esta en usar apariencia como herramienta de lectura, no como decoracion.
>
> Esta parte tambien dialoga con la teoria: si cambio la representacion visual de forma controlada, puedo senalizar relaciones y reducir informacion irrelevante segun la tarea.

Clicks/movimiento: mostrar un modo por clic; no hacer carrusel largo.

Transicion:

> Hay un modo que requiere una advertencia explicita: Thermal.

Evidencia base:

- `Informe_final/chapters/04_desarrollo.tex`, materiales, shaders y Studio.
- `Informe_final/figures/screenshots_contextual/fig_modes_direct_xray_solid_thermal.png`.
- `Informe_final/figures/screenshots_contextual/fig_modes_studio_presets.png`.

No decir:

- "Los shaders simulan comportamiento fisico completo".
- "Cada color representa una medicion real".

### Slide 16 - Thermal es una visualizacion heuristica, no una simulacion FEA

Tiempo: 15:55-16:45.

Objetivo: blindar la interpretacion tecnica de Thermal.

Visual: captura Thermal con etiqueta "heuristico relativo".

Guion oral:

> Thermal debe explicarse con precision. Heuristico significa una regla practica o criterio aproximado para orientar lectura, no una medicion fisica calibrada.
>
> En TwinSight, Thermal no recibe sensores, no usa telemetria, no ejecuta FEA y no calcula temperatura real. Es una visualizacion relativa para comunicar zonas de interes o lectura diferencial dentro del prototipo.
>
> Por eso en la defensa lo presento como herramienta de comunicacion visual, no como modulo de diagnostico. Si un jurado pregunta si se puede convertir en simulacion, la respuesta es si, pero seria otra fase: requeriria modelo fisico, propiedades materiales, condiciones de frontera, validacion y datos.

Clicks/movimiento: mostrar etiqueta de alcance junto a la captura.

Transicion:

> Con el alcance visual claro, la demo debe probar funciones, no improvisar navegacion.

Evidencia base:

- `Informe_final/chapters/01_introduccion.tex`, alcance y limitaciones.
- `Informe_final/chapters/06_conclusiones.tex`, limitaciones y trabajo futuro.
- `Informe_final/figures/screenshots_contextual/fig_thermal_single.png`.

No decir:

- "Thermal diagnostica temperatura".
- "Thermal reemplaza FEA".

### Slide 17 - La demo se lee como evidencia, no como recorrido libre

Tiempo: 16:45-17:05.

Objetivo: preparar al jurado para observar tres capacidades concretas.

Visual: checklist de demo: seleccionar, entender relacion, cambiar modo visual.

Guion oral:

> Antes de mostrar la demo, les pido mirar tres cosas. Primero, si se puede pasar del dron completo a una pieza concreta. Segundo, si la interfaz mantiene contexto de relacion entre pieza y ensamblaje. Tercero, si los modos visuales cambian la lectura sin cambiar el alcance del sistema.
>
> La demo no busca mostrar todo. Busca comprobar la promesa minima del prototipo.

Clicks/movimiento: dejar checklist fijo mientras inicia video o microdemo.

Transicion:

> Con esa lectura, paso al recorrido.

Evidencia base:

- `Informe_final/presentation/DEMO_SCRIPT.md`.
- `Informe_final/presentation/ASSETS_REQUIREMENTS.md`.

No decir:

- "Voy a navegar un poco".
- "Si funciona aqui, funciona en cualquier equipo".

### Slide 18 - Demo: del dron completo a pieza, relacion y modo visual

Tiempo: 17:05-19:05.

Objetivo: demostrar continuidad funcional.

Visual: demo en vivo o video local de 90-120 segundos.

Guion oral:

> Aqui inicio en el dron completo. Lo primero es orientacion general: el usuario entiende que esta en una escena 3D y puede orbitar el ensamblaje. [pausa breve]
>
> Ahora selecciono una pieza. La seleccion no solo resalta geometria; abre informacion contextual y mantiene la relacion con el conjunto. Esto responde al problema inicial: no separar dato y forma. [senalar panel]
>
> Activo una herramienta de inspeccion para aislar o separar visualmente el componente. Lo importante es que la accion no es decorativa: reduce ruido visual para leer relacion espacial.
>
> Finalmente cambio de modo visual. Esta transicion muestra que el mismo ensamblaje puede leerse con distintas capas visuales segun la tarea. [pausa]
>
> Con esto vuelvo a resultados: la pregunta ya no es si la app se ve bien, sino bajo que condiciones corre y que evidencia produjo.

Plan si falla la demo:

> Para no gastar tiempo en troubleshooting, paso al recorrido grabado. La evidencia que quiero mostrar es esta: seleccion, contexto y modo visual.

Clicks/movimiento: no desviarse del recorrido fijo.

Transicion:

> Primero reviso la evidencia tecnica de ejecucion.

Evidencia base:

- `docs/Build/`, build publica.
- `Informe_final/presentation/DEMO_SCRIPT.md`.
- `Informe_final/Manual_usuario/manual_usuario.pdf`.

No decir:

- "La demo reemplaza la validacion".
- "Esto prueba compatibilidad universal".

### Slide 19 - El profiler vuelve trazable el rendimiento por escenario y dispositivo

Tiempo: 19:05-20:05.

Objetivo: definir profiler y evidenciar reproducibilidad tecnica.

Visual: captura profiler + tabla simplificada.

Guion oral:

> Para evitar que el rendimiento quedara en percepcion subjetiva, la app integra mediciones y se apoyo en profiler. Un profiler es una herramienta de observacion del comportamiento runtime: registra indicadores como FPS, frame time, memoria, escena y contexto de ejecucion.
>
> En WebGL esto importa porque el rendimiento depende de hardware, navegador, memoria, cache y escenario activo. Por eso la tesis no reporta un FPS aislado, sino mediciones asociadas a equipo, build, resolucion, cache y condicion de prueba.
>
> La evidencia tecnica sirve para dos cosas: demostrar viabilidad en entornos probados y reconocer limites, especialmente en movil de gama media-baja.

Clicks/movimiento: senalar FPS, memoria, dispositivo y build.

Transicion:

> Leida la medicion, el resultado es viable, pero acotado.

Evidencia base:

- `Informe_final/validacion/06_GUIA_MEDICIONES_TECNICAS_WEBGL.md`.
- `Informe_final/validacion/07_TABLAS_RENDIMIENTO_WEBGL_MEDICIONES.tex`.
- `Informe_final/figures/screenshots_contextual/fig_profiler_internal_evidence.png`.
- `Telemetria/Mediciones_WebGL/` si esta disponible localmente.

No decir:

- "El profiler sustituye pruebas en dispositivos".
- "Los FPS son iguales en cualquier navegador".

### Slide 20 - El rendimiento es viable en desktop y limitado en movil probado

Tiempo: 20:05-21:05.

Objetivo: interpretar rendimiento sin compatibilidad universal.

Visual: matriz desktop/movil: FPS, frame time, memoria, observacion.

Guion oral:

> En desktop, el entorno reportado fue Windows 11, GPU GTX 980 Ti, Intel Core i7-5820K, 48 GB de RAM y navegador Chrome. En movil, se uso Redmi Note 10S con MIUI Global 14.0.11 y Chrome. Tambien se controlo cache cargado y build.
>
> La lectura academica es esta: el prototipo es viable en los entornos probados y muestra una experiencia funcional en desktop. En movil, el soporte existe pero es mas sensible a memoria, resolucion, carga de escena y gestos. Por eso no prometo compatibilidad universal.
>
> Este resultado conecta con el pipeline: WebGL permite distribucion web, pero obliga a optimizar geometria, materiales, memoria y UI.

Clicks/movimiento: revelar primero desktop, despues movil, despues advertencia de alcance.

Transicion:

> La segunda parte de la evidencia viene de usuarios.

Evidencia base:

- `Informe_final/chapters/05_resultados.tex`, rendimiento WebGL.
- `Informe_final/validacion/07_TABLAS_RENDIMIENTO_WEBGL_MEDICIONES.tex`.
- `Informe_final/figures/screenshots_contextual/fig_device_matrix_clean.png`.
- `Informe_final/validacion/02_BUILD_CLOSURE_ACADEMICO.md`.

No decir:

- "Funciona perfectamente en todo celular".
- "Chrome es irrelevante".

### Slide 21 - SUS muestra recepcion favorable del prototipo 3D

Tiempo: 21:05-22:05.

Objetivo: explicar SUS correctamente y no compararlo contra 2D.

Visual: grafico SUS, referencia 68, distribucion n=12.

Guion oral:

> La muestra final fue de 12 participantes anonimizados, con perfiles afines al contexto del proyecto. SUS se aplico solo al prototipo 3D como lectura global de usabilidad percibida.
>
> El promedio SUS fue 91,88, con mediana 95, minimo 60, maximo 100 y desviacion estandar 11,24. La referencia de 68 se usa como promedio historico del instrumento, no como umbral absoluto de aprobacion.
>
> La interpretacion prudente es: dentro de esta muestra y este prototipo, la recepcion de usabilidad fue favorable. No digo que SUS pruebe superioridad frente al soporte 2D, porque no se aplico de esa forma.

Clicks/movimiento: mostrar primero n y aplicacion solo 3D, luego media, luego cautela.

Transicion:

> Para comparar condiciones, el instrumento clave fue NASA-TLX Raw junto con tiempos de tareas.

Evidencia base:

- `Informe_final/chapters/05_resultados.tex`, resultados SUS.
- `Informe_final/validacion/usuarios/` si esta disponible localmente.
- `BIBLIOGRAPHY_EVIDENCE_ATLAS.md`, Brooke, Bangor, Sauro y Lewis.

No decir:

- "SUS prueba superioridad frente a 2D".
- "68 es un umbral universal de aprobacion".

### Slide 22 - NASA-TLX Raw y tiempos favorecen al 3D de forma descriptiva

Tiempo: 22:05-23:10.

Objetivo: conectar workload, tiempos T1-T3 y T4 exploratoria.

Visual: comparativa NASA 3D vs 2D + tiempos T1-T3 + nota de T4.

Guion oral:

> NASA-TLX Raw si se aplico por condicion. El promedio del visor 3D fue 8,69 y el del soporte 2D fue 19,89. La diferencia pareada media fue 11,19 puntos a favor del visor, y en los 12 casos la carga de trabajo percibida fue menor en 3D.
>
> En tiempos, las tareas T1, T2 y T3 se cronometraron porque tenian inicio y cierre comparables. T4 fue exploratoria guiada y no se cronometro; por eso no debe mezclarse con las otras tres.
>
> Los tiempos medios fueron: T1, 5,75 segundos en 3D frente a 13,00 en 2D; T2, 3,50 frente a 18,00; T3, 11,33 frente a 23,00. El total T1-T3 fue 20,58 segundos en 3D y 54,00 en 2D.
>
> La conclusion correcta es descriptiva: en esta muestra, el 3D se asocio con menor workload percibido y menor tiempo medio en tareas cronometradas, no con una prueba universal de superioridad.

Clicks/movimiento: mostrar NASA, luego tiempos, luego nota "T4 exploratoria".

Transicion:

> Los numeros explican el patron, pero las verbalizaciones ayudan a entender por que ocurrio.

Evidencia base:

- `Informe_final/chapters/05_resultados.tex`, tablas de desempeno, NASA-TLX y discusion.
- `Informe_final/validacion/04_CUESTIONARIO_NASA_TLX_PARTICIPANTE.md`.
- `BIBLIOGRAPHY_EVIDENCE_ATLAS.md`, Hart y Hart/Staveland.

No decir:

- "NASA mide aprendizaje".
- "T4 tambien fue cronometrada".
- "Esto demuestra causalidad estadistica poblacional".

### Slide 23 - Think-Aloud explica claridad espacial y fricciones residuales

Tiempo: 23:10-24:10.

Objetivo: hacer visible la triangulacion cualitativa.

Visual: matriz de categorias Think-Aloud: comprension espacial, navegacion/control, iconos, movil, piezas pequenas.

Guion oral:

> Think-Aloud complementa los numeros. Como los participantes verbalizan mientras ejecutan, permite detectar que les ayuda, donde dudan y que no se ve en una metrica agregada.
>
> La categoria mas recurrente fue comprension espacial, presente en 11 de 12 participantes. Navegacion y control aparecio en 10 de 12. Esto coincide con la lectura cuantitativa: el prototipo ayudo a ubicar y relacionar piezas, pero no elimino todas las fricciones.
>
> Las fricciones residuales se concentraron en iconos, navegacion movil y seleccion de piezas pequenas. Para la defensa, esto es importante porque muestra madurez metodologica: la validacion no solo confirma aciertos, tambien produce una lista concreta de mejoras.

Clicks/movimiento: mostrar categoria favorable, despues fricciones.

Transicion:

> Con esas tres evidencias, la discusion debe decir con precision que se demuestra y que no.

Evidencia base:

- `Informe_final/chapters/05_resultados.tex`, Think-Aloud y triangulacion.
- `Informe_final/validacion/05_FORMATO_REGISTRO_MODERADOR.md`.
- `Informe_final/validacion/usuarios/` si esta disponible localmente.

No decir:

- "Todos los usuarios prefirieron todo".
- "Las verbalizaciones son prueba objetiva por si solas".

### Slide 24 - La discusion acota el resultado: efecto techo, muestra y compatibilidad

Tiempo: 24:10-26:40.

Objetivo: integrar resultados con teoria, limites y honestidad academica.

Visual: matriz "demuestra / no demuestra / queda abierto".

Guion oral:

> La discusion es donde se evita vender de mas. Las cuatro tareas se completaron en ambas condiciones. Eso genera efecto techo: cuando todos completan, la tasa de exito ya no distingue bien entre condiciones.
>
> Por eso el hallazgo no es "solo el 3D permite completar", porque eso no seria cierto. El hallazgo es mas fino: en esta muestra, el 3D mantuvo completitud, redujo tiempos T1-T3, redujo NASA-TLX Raw y produjo verbalizaciones consistentes con mejor orientacion espacial.
>
> Desde la teoria de carga cognitiva, la interpretacion defendible es que el visor puede reducir carga extrinseca de reconstruccion espacial. Pero no afirmo que mida directamente carga intrinseca, extrinseca y germana, ni que pruebe aprendizaje duradero.
>
> Tambien hay limites tecnicos. La build es funcional y trazable, pero la compatibilidad movil es acotada; Thermal es heuristico; no hay telemetria real; no hay mantenimiento predictivo; no hay FEA termico; y la muestra es no probabilistica, de 12 participantes.
>
> En terminos academicos, esa honestidad no debilita la tesis. La vuelve defendible porque ajusta las conclusiones a la evidencia.

Clicks/movimiento: revelar "demuestra", "no demuestra", "queda abierto".

Transicion:

> Desde ahi, la contribucion queda en tres niveles.

Evidencia base:

- `Informe_final/chapters/05_resultados.tex`, discusion.
- `Informe_final/chapters/06_conclusiones.tex`, conclusiones y trabajo futuro.
- `Informe_final/figures/chapter5/` si se usan graficos de resultados.

No decir:

- "El prototipo demuestra aprendizaje".
- "Los resultados se generalizan a cualquier usuario".
- "La version movil esta completamente resuelta".

### Slide 25 - La contribucion es tecnica, metodologica y comunicativa

Tiempo: 26:40-27:45.

Objetivo: resumir aporte sin reducirlo a "una app bonita".

Visual: tres columnas: tecnica, metodologica, comunicativa.

Guion oral:

> La contribucion tecnica es un pipeline CAD/Blender/Unity/WebGL con optimizacion geometrica, taxonomia de escena, UI de inspeccion, shaders y profiler.
>
> La contribucion metodologica es una evaluacion formativa diferenciada: rendimiento tecnico, tareas, SUS solo para 3D, NASA-TLX Raw por condicion, Think-Aloud y discusion de limites.
>
> La contribucion comunicativa es convertir hardware complejo en una experiencia inspeccionable donde pieza, relacion y contexto aparecen juntos.
>
> Dicho en una frase: TwinSight no reemplaza el ciclo industrial del dron; aporta una forma trazable de hacer legible su ensamblaje en la web.

Clicks/movimiento: revelar cada columna con una evidencia asociada.

Transicion:

> Cierro volviendo al problema inicial.

Evidencia base:

- `Informe_final/chapters/06_conclusiones.tex`, conclusiones.
- `Informe_final/presentation/DEFENSE_EVIDENCE_MAP.md`, matriz de claims.
- `README.md`, scope academico publico.

No decir:

- "La contribucion es solo estetica".
- "Es un producto comercial terminado".

### Slide 26 - Hacer legible el hardware complejo desde la web es el aporte defendible

Tiempo: 27:45-28:30.

Objetivo: cierre breve, memorizable y honesto.

Visual: render final o comparacion inicial/final con tres mensajes: legibilidad, trazabilidad, limites.

Guion oral:

> El punto de partida fue una dificultad concreta: informacion tecnica suficiente, pero distribuida en formatos que obligan a reconstruir mentalmente relaciones espaciales.
>
> TwinSight X500 responde con un visual product twin WebGL: no telemetria real, sino inspeccion, contexto de piezas, modos visuales y medicion tecnica.
>
> La evidencia muestra build funcional, rendimiento viable con limites, SUS favorable, menor NASA-TLX Raw en la muestra, menores tiempos en T1-T3 y verbalizaciones coherentes con comprension espacial.
>
> Por eso el aporte defendible es este: hacer mas legible un hardware complejo desde la web, con una solucion tecnica trazable y una evaluacion honesta de su alcance. Muchas gracias. Quedo atento a sus preguntas.

Clicks/movimiento: terminar en imagen estable, no cerrar con pantalla negra.

Evidencia base:

- Todo el paquete: informe final, anexos, build publica, README y presentacion.

No decir:

- "Eso seria todo" sin sintesis.
- "El sistema ya no requiere mejoras".

## 6. Ruta de emergencia por tiempo

Si al llegar al slide indicado hay retraso, usar estas fusiones:

1. Si quedan menos de 24 minutos al llegar a Slide 5, fusionar Slides 5-6: alcance + objetivos en 90 segundos.
2. Si quedan menos de 20 minutos al llegar a Slide 9, fusionar Slides 9-10: pipeline + triangulos en 90 segundos.
3. Si quedan menos de 14 minutos al llegar a Slide 14, fusionar Slides 14-16: Inspect/Analyze/Studio/Thermal en 2 minutos.
4. Si quedan menos de 9 minutos al llegar a Slide 19, fusionar Slides 19-20: profiler + rendimiento en 90 segundos.
5. Si quedan menos de 6 minutos al llegar a Slide 21, fusionar Slides 21-23: SUS, NASA, tiempos y Think-Aloud en 2:30.
6. Nunca saltar Slide 24. La discusion es el seguro academico de la defensa.

## 7. Respuestas puente para preguntas de jurado

Si preguntan por alcance:

> No presento un gemelo digital operacional; presento un visual product twin. La diferencia es que aqui no hay sincronizacion fisica ni telemetria viva. Hay representacion visual-semantica, interaccion y medicion tecnica.

Si preguntan por carga cognitiva:

> La teoria de carga cognitiva explica el problema de reconstruccion espacial. La medicion reportada no mide directamente carga intrinseca, extrinseca y germana; NASA-TLX Raw mide workload percibido y se interpreta con esa teoria.

Si preguntan por SUS:

> SUS se aplico solo al prototipo 3D. Por eso no lo uso para comparar 3D contra 2D. La comparacion entre condiciones se apoya en tareas, NASA-TLX Raw y Think-Aloud.

Si preguntan por NASA-TLX:

> Se uso Raw TLX sin ponderacion pareada. La dimension rendimiento se oriento de forma invertida para que el promedio mantuviera coherencia direccional. La lectura es descriptiva, no poblacional.

Si preguntan por T4:

> T4 fue exploratoria guiada y no tenia un cierre temporal comparable. Por eso se reporta como completitud y observacion, no como tiempo cronometrado.

Si preguntan por triangulos:

> 95 617 es activo base optimizado exportado; 229 054 es escena runtime instrumentada/profiler. No son metricas equivalentes.

Si preguntan por Thermal:

> Thermal es heuristico y relativo. No hay sensores, telemetria, FEA ni temperatura real. Convertirlo en simulacion requeriria otra fase con modelo fisico y validacion.

Si preguntan por el repo:

> La tesis es la fuente autoritativa del alcance. El README publico debe leerse con el estado academico vigente: build WebGL, flujo visible y limitaciones declaradas.

Si preguntan por una cifra no recordada:

> La cifra que puedo afirmar es la del informe final y sus anexos. Si no esta en informe, anexo o profiler, prefiero no improvisarla.

## 8. Frases prohibidas

- "TwinSight es un digital twin completo".
- "El sistema tiene telemetria real".
- "Thermal mide temperatura".
- "NASA-TLX mide carga cognitiva exacta".
- "SUS prueba que 3D es mejor que 2D".
- "La muestra permite generalizar estadisticamente".
- "La app funciona perfectamente en cualquier movil".
- "Los 229 054 triangulos son el modelo optimizado".

## 9. Backup slides recomendadas

- B0. Glosario rapido: CAD, WebGL, runtime, visual product twin, profiler, workload.
- B1. Teoria de carga cognitiva: intrinseca, extrinseca, germana y relacion con reconstruccion espacial.
- B2. Aprendizaje multimedia: segmentacion, senalizacion, coherencia y preentrenamiento.
- B3. Formula SUS y referencia historica de 68.
- B4. Formula NASA-TLX Raw y orientacion invertida de rendimiento.
- B5. Variables de control y entorno tecnico completo.
- B6. Pipeline 3D completo con antes/despues.
- B7. Tabla completa de rendimiento WebGL.
- B8. Explicacion 95 617 vs 229 054 triangulos.
- B9. Taxonomia de piezas y evidencia de seleccion.
- B10. Thermal heuristico.
- B11. Evidencia de anexos y rutas publicas/locales.
- B12. Roadmap: twin manifest, digital shadow, telemetria historica, accesibilidad.

## 10. Checklist final de ensayo

- El primer minuto define CAD, WebGL y alcance.
- Antes de NASA ya se explico carga cognitiva y workload.
- Antes de resultados ya se explico SUS, NASA-TLX Raw y Think-Aloud.
- Antes de Thermal ya se definio heuristico y FEA.
- Antes de hablar de triangulos ya se definio activo base y runtime.
- La demo tiene ruta fija y video local de respaldo.
- Cada slide responde una pregunta y no una lista de features.
- El cierre no promete mas que la evidencia.
