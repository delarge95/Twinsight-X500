# PRESENTATION_SCRIPT.md
# Guion maestro de sustentación — TwinSight X500

**Estado:** canónico para la defensa. Sincronizado con `index_final.html` (pantalla de espera + 21 slides principales + B1–B15).
**Fecha de actualización:** 2026-09-29.
**Duración objetivo:** 22:00 de exposición según el guion (~135 palabras por minuto). Con el ritmo medido en ensayo (~33 % más lento) queda cerca de 29–30 min.
**Fuente autoritativa:** `awoodcocks.pdf` (informe final) y sus anexos. Las cifras de tornillería de la slide 9 salen de los archivos del proyecto (ver P9).
**Uso complementario:** ensayar con `SPEAKER_CARDS.md`.

---

## 1. Cómo usar este guion

Esto es una partitura para hablar, no un texto para recitar. Cada slide trae lo que hay que decir, cuándo hacer clic y qué frases evitar. Si en el ensayo una frase no te sale natural, cámbiala por una tuya que diga lo mismo. Lo que no se toca son las cifras.

El hilo de la presentación:

```
problema → teoría → alcance → método → cómo se construyó → demo → evidencia → límites → aporte
```

**La idea que tiene que quedar, dicha en una frase:**

> TwinSight X500 es un *visual product twin*: una versión web del dron, optimizada y organizada por piezas, que permite entender cómo está armado sin instalar nada. No se conecta al dron real ni pretende hacerlo.

---

## 2. Numeración

El número de cada slide en este guion es su posición en el deck. Coincide con el número visible (kicker) y con el contador de arriba a la derecha ("NN / 21"). Antes de la slide 1 está la pantalla de espera (slide 0), que no cuenta: → la cierra y **P** la vuelve a abrir. Los backups van aparte, B1–B15. Para saltar a uno, escribe su código (por ejemplo `B3`) en el contador y pulsa Enter.

---

## 3. Términos y dónde se definen

Ningún término se usa como argumento antes de explicarlo.

| Término | Se explica en | Cómo se explica |
|---|---|---|
| CAD, WebGL | Slide 1 | Modelos de ingeniería / 3D dentro del navegador |
| Reconstrucción espacial | Slide 2 | Armar en la cabeza el objeto a partir de vistas planas |
| Carga cognitiva | Slide 3 | Memoria de trabajo limitada; tres tipos de carga |
| Digital twin, digital shadow, visual product twin | Slide 4 | Según cuánto dato real reciben del objeto físico |
| FEA | Slide 4 | Simulación por elementos finitos |
| DSR, evaluación formativa | Slide 5 | Investigar construyendo y evaluando un artefacto |
| SUS, NASA-TLX, Think-Aloud | Slide 6 (qué son) · slides 15–16 (cómo se calculan) | |
| Teselación, triángulo | Slide 7 | El CAD no tiene triángulos hasta que se exporta |
| Bus de eventos | Slide 8 | Un módulo avisa; los que están suscritos reaccionan |
| Taxonomía, hotspot, fastener, proxy | Slide 9 | |
| Bottom sheet | Slide 10 | La ficha que sube desde abajo |
| Inspect, Analyze, Studio | Slide 11 | Tres preguntas: qué es, cómo se conecta, qué hay adentro |
| Modelo térmico por componentes | Slide 12 | Fuentes de calor, conducción, convección |
| Efecto techo | Slide 18 | Todos completan todo: el éxito deja de distinguir |

---

## 4. Marcas en el guion

| Marca | Qué hacer |
|---|---|
| `[click n — etiqueta]` | Pulsar → para mostrar el paso *n* (coincide con `data-step="n"`). Decir lo que sigue cuando ya está en pantalla. |
| `[avanzar]` | Pasar a la siguiente slide. |
| `[pausa]` | Un segundo de silencio. |
| `[pausa larga]` | Dos segundos. Antes de un límite o un número importante. |
| `[mirar jurado]` | Levantar la vista y cerrar la idea mirando al jurado. |
| `[señalar]` | Indicar la zona de la pantalla. |

Si notas que aceleras, son los nervios. Bajar el ritmo se lee como seguridad.

---

## 5. Antes de empezar

1. De pie, mirando al jurado, no a la pantalla.
2. Deck abierto en la pantalla de espera (slide 0), proyectándose mientras el jurado se acomoda. Al empezar, → la cierra y entra la slide 1. La landing abierta en otra ventana, lista para la demo de la slide 13. El video de esa slide es el respaldo.
3. Tres ideas que tienen que quedar:
   - Ver las piezas no alcanza: lo difícil es entender cómo se relacionan.
   - El trabajo junta teoría, un pipeline 3D, una app web y una evaluación con doce personas.
   - Los resultados son buenos y tienen límites claros. Decirlos es parte del trabajo.

### Checklist técnico (30 min antes)

| # | Verificación | Por qué |
|---|---|---|
| 1 | Abrir `index_final.html` con doble clic en **Chrome** o Edge, con zoom del navegador al 100 % (`Ctrl+0`). | El deck ajusta su tamaño a la resolución; el zoom del navegador altera ese cálculo. |
| 2 | Proyector en modo **duplicar**, idealmente a 1920×1080. `F` para pantalla completa. | A 1920×1080 el texto crece ~25 %; a resoluciones menores se ajusta solo. |
| 3 | Confirmar conexión a internet. | La landing y la app de la demo se cargan desde la red. |
| 4 | Pasar una vez por las slides 11 (capturas) y 13 (video), volver con `Inicio` y pulsar **P** para dejar puesta la pantalla de espera. | Deja los videos en caché. |
| 5 | Abrir la **landing** (twinsight.alexwoodcock.me) en **otra ventana**, al inicio de la sección 03 con el dron sin ensamblar. Abrir una vez «Abrir visor» para dejar la app en caché y cerrarla. | Es la demo de la slide 13. |
| 6 | Desactivar notificaciones, actualizaciones y ahorro de energía. Cargador conectado. | Evita interrupciones y caídas de rendimiento. |
| 7 | **No abrir las notas (`N`) durante la exposición**: se ven en la misma pantalla que el jurado. Usar `SPEAKER_CARDS.md` impreso o en el teléfono. | Las notas son para ensayar. |
| 8 | Cronómetro visible con las marcas de las tarjetas. | El deck no muestra el tiempo. |

---

## 6. Guion slide por slide

---

### SLIDE 1 — Portada

**Tiempo:** 0:00 – 0:40 · **Pasos:** ninguno
**En pantalla:** título, ficha del proyecto y captura del visor.

> Buenos días. Soy Alexander Woodcock Salomón y les presento TwinSight X500, mi trabajo de grado de Ingeniería Multimedia en la UNAD. [mirar jurado]
>
> Es una aplicación web para inspeccionar en 3D el dron Holybro X500 V2. El punto de partida son sus modelos **CAD**, los archivos de diseño que usan los ingenieros, que son precisos pero pesados y no están hechos para abrirse en un navegador. El resultado corre en **WebGL**, la tecnología que dibuja 3D dentro del navegador sin instalar nada.

**Transición:**
> Empiezo por el problema que me llevó a hacerlo.

**No decir:** "gemelo digital completo" · "simulador" · "producto terminado"

---

### SLIDE 2 — El problema

**Tiempo:** 0:40 – 1:40 · **Pasos:** 4

> Imaginen que tienen que ubicar un motor del dron y entender cómo se sujeta a su brazo, con el manual en PDF abierto.
>
> [click 1 — Documentación] La información está. Hay planos, manuales, listas de piezas. Pero está repartida en varios documentos, y cada uno muestra una parte.
>
> [click 2 — Fricción] Entonces el trabajo lo hace la cabeza de quien lee: mirar vistas planas y armar mentalmente dónde está cada cosa, qué va delante, qué va detrás. A eso lo llamo **reconstrucción espacial**. Con un dron de decenas de piezas, se vuelve cansado muy rápido. [pausa]
>
> [click 3 — Respuesta] Mi propuesta fue llevar el dron a una vista 3D en la web, donde uno pueda girarlo, tocar una pieza, aislarla y ver con qué se conecta.
>
> [click 4 — Idea clave] Los datos existen. Lo que faltaba era un puente entre esos datos y la comprensión del conjunto. [mirar jurado]

**Transición:**
> Hay una teoría que explica por qué ese puente importa.

**No decir:** "el 2D no sirve" · "el 3D siempre es mejor"

---

### SLIDE 3 — Carga cognitiva

**Tiempo:** 1:40 – 2:55 · **Pasos:** 4

> La teoría de la carga cognitiva parte de algo simple: la memoria de trabajo es limitada. Sweller distingue tres tipos de carga.
>
> [click 1 — Intrínseca] La intrínseca viene del contenido. Un dron con muchas piezas es difícil de entender por sí mismo, y eso ninguna interfaz lo cambia.
>
> [click 2 — Extrínseca] La extrínseca viene de cómo se presenta. Saltar de un plano a una tabla gasta esfuerzo que no enseña nada. Esta es la que el diseño puede bajar.
>
> [click 3 — Germana] Y la germana es el esfuerzo que vale la pena: entender para qué sirve una pieza y dónde encaja.
>
> [click 4 — Límite declarado] Aclaro algo desde ya. La tesis no mide estos tres tipos de carga. Los usa para justificar decisiones de diseño. Lo que sí se mide más adelante, con NASA-TLX, es la carga de trabajo que cada persona percibe.
>
> Con esa idea diseñé la interfaz: que la app haga parte del trabajo mental. Girar el dron con el dedo reemplaza el giro que antes se hacía en la cabeza, y la ficha de cada pieza funciona como memoria externa.

**Transición:**
> Antes de seguir, conviene dejar claro qué es TwinSight y qué no.

**No decir:** "NASA-TLX mide la carga intrínseca" · "el 3D elimina la carga cognitiva"

---

### SLIDE 4 — Alcance

**Tiempo:** 2:55 – 3:55 · **Pasos:** 2

> Esta slide es importante porque evita malentendidos. [mirar jurado]
>
> Un **digital twin** operacional es una copia digital conectada al objeto real: recibe sus datos en vivo, está calibrada y ayuda a tomar decisiones. Un **digital shadow** recibe datos del objeto, pero no actúa sobre él.
>
> [click 1 — Exclusiones] TwinSight no hace nada de eso. No recibe datos del dron físico, no predice mantenimiento y no se integra con sistemas industriales. Tampoco hace **FEA**, que es la simulación por elementos finitos que usan los ingenieros para calcular esfuerzos o calor con precisión. [pausa]
>
> [click 2 — Visual Product Twin] Lo que sí hace es representar el producto: su forma, sus piezas, cómo se relacionan y distintas maneras de mirarlo. Por eso lo llamo **visual product twin**. Es el primer escalón. Los otros dos quedan como trabajo futuro.

**Transición:**
> Con ese alcance, los objetivos se pueden verificar uno por uno.

**No decir:** "TwinSight ya es un gemelo digital" · "Thermal mide temperatura real"

---

### SLIDE 5 — Objetivos

**Tiempo:** 3:55 – 4:50 · **Pasos:** 4

> La pregunta de investigación está arriba: qué diferencias aparecen en desempeño y en carga percibida entre un visor 3D web y la documentación 2D, y si ese visor puede funcionar bien en un navegador.
>
> [click 1 — OE1] El primer objetivo fue técnico: llevar el CAD a un modelo liviano sin perder forma ni piezas. [click 2 — OE2] El segundo, lograr buenos materiales y modos visuales manteniendo al menos 30 cuadros por segundo. [click 3 — OE3] El tercero, construir la app con selección, ficha, vista explosionada y corte. [click 4 — OE4] Y el cuarto, probarla con personas y compararla con la documentación 2D.

> El método fue Design Science Research: investigar construyendo un artefacto y evaluándolo. La evaluación fue formativa, con doce personas, así que describe lo que pasó en esa muestra sin generalizar.

**Transición:**
> Para evaluarlo usé cinco fuentes.

**No decir:** "la evaluación generaliza a la población" · "30 FPS en todos los dispositivos"

---

### SLIDE 6 — Evaluación en cinco capas

**Tiempo:** 4:50 – 5:25 · **Pasos:** 6

> Ninguna medida sola cuenta la historia completa. [click 1 — KPIs técnicos] El rendimiento de la app, [click 2 — SUS] el cuestionario de usabilidad SUS, [click 3 — Tareas] cuatro tareas cronometradas, [click 4 — Think-Aloud] lo que las personas decían en voz alta [click 5 — NASA-TLX Raw] y el cuestionario de carga NASA-TLX, en 3D y en 2D.
>
> [click 6 — Triangulación] Cuando las cinco apuntan en la misma dirección, el resultado pesa más.

**Transición:**
> Antes de los resultados, cómo se construyó. Empiezo por el modelo 3D.

**No decir:** "NASA-TLX mide la carga cognitiva directamente" · "SUS demuestra que el 3D es mejor" · "telemetría" para hablar del medidor interno

---

### SLIDE 7 — Del CAD al activo WebGL

**Tiempo:** 5:25 – 6:40 · **Pasos:** 8

> Un archivo CAD, como el STEP del fabricante, no tiene triángulos. Describe superficies con ecuaciones: un cilindro es un radio y una altura. Para dibujarlo hay que convertir esas superficies en triángulos, y eso se llama **teselar**. Según la herramienta, el dron dio entre 6,5 y 6,9 millones de triángulos. Imposible de mover en un celular. [señalar título]
>
> [click 1 — MoI3D · STEPper] Teselé el STEP con MoI3D o con STEPper, según cuánto control necesitaba sobre la densidad. [click 2 — Blender] En Blender limpié la malla. [click 3 — Retopo] Las piezas importantes las rehice con menos polígonos y las repetidas las cambié por versiones livianas, que llamo proxies. [click 4 — Bake] Después pasé el detalle fino a texturas, con mapas de normales y de oclusión. [click 5 — FBX] Exporté a Unity [click 6 — WebGL] y compilé para la web.
>
> [click 7 — Por qué optimizar] El problema de la teselación es que llena el modelo de caras internas, vértices duplicados y tornillos repetidos, que cuestan mucho y no se ven.
>
> [click 8 — Qué se conserva] Cuidé que cada pieza siguiera siendo una pieza, con su nombre y su lugar. Si no, después no se podría seleccionar nada. El resultado: 95 617 triángulos. [mirar jurado]

**Transición:**
> Ahora, cómo está organizada la app por dentro.

**No decir:** "el CAD tenía 6,5 millones de triángulos" (los tuvo al teselarlo) · "optimizar es solo bajar polígonos"

---

### SLIDE 8 — Arquitectura

**Tiempo:** 6:40 – 7:30 · **Pasos:** 1

> La app está dividida en cuatro grupos: interfaz, coordinación, servicios que modifican el modelo y datos de cada pieza. Lo que los une es un **bus de eventos**, que funciona como un tablero de avisos. [señalar EventBus]
>
> [click 1 — Ejemplo real] Un ejemplo. Cuando tocas una pieza, el módulo de selección publica un solo aviso. La interfaz abre la ficha, las marcas se reacomodan y, si es un tornillo, otro módulo arma su versión detallada. El que avisó no conoce a ninguno. Por eso agregar una función no obliga a tocar lo que ya funciona. [mirar jurado]

**Transición:**
> Para que ese aviso diga qué pieza se tocó, la app necesita una clasificación.

**No decir:** "es solo un visor 3D" · nombres de clases que no estén en la slide o en el código

---

### SLIDE 9 — Taxonomía y tornillería

**Tiempo:** 7:30 – 8:45 · **Pasos:** 7

> La taxonomía es la clasificación que le dice a la app qué tocó el usuario y qué mostrar.
>
> [click 1 — 28] Hay 28 piezas principales, cada una con su ficha. [click 2 — 30] En la escena son 30 anclas: esas 28, más un grupo de tornillería y otro de piezas menores. [click 3 — 257] La auditoría contó 257 elementos que se dibujan o se pueden tocar.
>
> [click 4 — Hotspots] Los **hotspots** son marcas sobre el modelo: con un toque seleccionas un grupo sin apuntarle a una pieza diminuta.
>
> [click 5 — Tornillería] **Fastener** es cualquier sujetador: tornillos, tuercas, separadores. Eran el mayor problema. En la importación con STEPper sumaban 425 208 triángulos, el 79 % del archivo, y en pantalla apenas se ven. [pausa]
>
> [click 6 — Tornillo modular] En la escena, cada sujetador es una forma simple de 88 triángulos o menos: 14 408 en total. Cuando alguien inspecciona un tornillo, la app lo arma en detalle. [click 7 — figura] Con estas piezas: tres cabezas, una vuelta de rosca y una punta. Se elige la cabeza, se ajusta el diámetro y la vuelta se repite según el largo dividido por el paso de la rosca. Cinco piezas cubren todos los tornillos del dron. [mirar jurado]

**Transición:**
> Con esa estructura, veamos cómo la recorre el usuario.

**Si preguntan por la BOM** (lista de materiales industrial): la taxonomía se le parece, pero está pensada para navegar, no para fabricar. **No decir:** "28 categorías" (son 28 piezas) · "todos los fasteners son modulares" (el sistema modular es solo para tornillos)

---

### SLIDE 10 — Flujo de uso

**Tiempo:** 8:45 – 9:30 · **Pasos:** 7

> La app arranca en una pantalla de entrada. [click 1 — Explore] Luego, en Explore, giras y acercas el dron. [click 2 — Selección] Tocas una pieza [click 3 — Ficha] y sube la ficha con sus datos, lo que en diseño de interfaces se llama **bottom sheet**.
>
> [click 4 — Barra inferior] Abajo está la barra con Inspect, Analyze y Studio. Esa barra está siempre visible, desde el primer momento. Cambiar de modo nunca exige buscar un menú.
>
> [click 5 — Detalle bajo demanda] Lo que aparece por etapas es el detalle: la ficha sube solo cuando eliges una pieza. [click 6 — Build pública] Y los paneles que usé para medir y depurar quedaron fuera de la versión pública.
>
> [click 7 — captura] En la captura: la pieza, Analyze y la barra. [señalar]

**Transición:**
> Qué hace cada uno de esos modos.

**No decir:** "los modos aparecen al seleccionar una pieza" · "todos los módulos experimentales quedaron publicados"

---

### SLIDE 11 — La app en el móvil

**Tiempo:** 9:30 – 10:00 · **Pasos:** 3
**En pantalla:** tres capturas de la build en el Redmi Note 10S.

> Así se ve en un celular. [click 1 — Inspect] Inspect responde qué es una pieza y dónde está: la aísla y abre su ficha. [click 2 — Analyze] Analyze responde cómo se conecta: separa las piezas y corta el modelo. [click 3 — Studio] Y Studio cambia la forma de mirarlo: rayos X, calor o solo la forma.

**Transición:**
> El modo térmico merece su propia explicación.

**No decir:** "Explode es un ensamblaje físicamente exacto" · "los shaders simulan física"

---

### SLIDE 12 — Thermal

**Tiempo:** 10:00 – 11:10 · **Pasos:** 2

> Thermal muestra cómo se distribuye el calor en el dron, calculado con un modelo físico simplificado.
>
> [click 1 — Cómo calcula] Cada pieza tiene su propia temperatura. Los motores, los controladores de velocidad y la batería generan calor según lo que haga el dron: apagado, arrancando, en reposo o volando. Ese calor pasa a las piezas en contacto, y cuánto pasa depende del área de contacto, la distancia y el material. El cobre conduce mucho y la fibra de carbono, poco. Y cada pieza se enfría con el aire según cuánto esté expuesta. Es la física de transferencia de calor, simplificada.
>
> [click 2 — diagrama y límite] ¿Por qué simplificada? Una simulación por elementos finitos divide cada pieza en miles de celdas y tarda minutos. Esto corre en un celular, en tiempo real. Así que cada pieza es un punto, el tiempo va acelerado y las conductividades están escaladas. Sirve para entender por dónde viaja el calor. Para diagnosticar habría que calibrarlo con mediciones reales. [mirar jurado]

**Transición:**
> Ahora lo muestro funcionando.

**No decir:** "Thermal mide la temperatura real" · "Thermal reemplaza un FEA" · "son valores inventados" (salen de un modelo con base física) · leer los °C como medición

---

### SLIDE 13 — Demo en vivo

**Tiempo:** 11:10 – 14:10 · **Pasos:** 2
**Modo principal: demo en vivo en dos tramos.** (1) La **landing** (WebGL puro, sin Unity), abierta en otra ventana al inicio de la sección 03, con el dron sin ensamblar. (2) La **app Unity**, que se abre desde «Abrir visor» en la landing. **Respaldo:** el video de esta slide (88 s), en pausa en 0:00; se reproduce con un clic.

**Procedimiento:**
1. [click 1 — ruta] Mostrar la ruta y pasar a la landing (Alt+Tab).
2. **0:00–1:10 · Landing.** Pasar el cursor hasta que el dron se ensamble (~13 s). Luego scroll a ritmo parejo por los seis capítulos (~7 s cada uno). En la mini app final, activar «Explosionar» y desactivarlo.
3. **1:10–2:50 · App Unity.** Volver arriba y pulsar «Abrir visor». Recorrer: selección → Isolate → ficha → Power → Explode → Cut → X-Ray → Thermal → Solid.
4. Volver al deck (Alt+Tab), [click 2 — contingencia] y cerrar.
5. **Regla de corte:** si la app tarda más de 10 s en responder o se congela, volver al deck sin comentarlo, hacer clic en el video y narrar el mismo recorrido. Si falla la landing, pasar directo a la app.

**Guion oral:**

> [click 1 — ruta] Lo muestro en vivo. [Alt+Tab a la landing]
>
> **[0:00]** Esta es la página del proyecto. Está hecha con WebGL directo, sin Unity. El dron empieza desarmado, como una nube de puntos, y se arma cuando paso el cursor. Cada punto es un vértice del modelo. [pausa]
>
> **[0:15]** Al bajar, la página cuenta el proyecto con el mismo modelo: el paso del CAD a la web, las piezas por subsistema, los rayos X, la vista explosionada, el calor y los resultados. **[1:00]** Al final queda una versión pequeña de la app, donde puedo girarlo y separarlo.
>
> **[1:10]** Desde aquí abro la aplicación completa. [«Abrir visor»] Toco el soporte de la batería. Isolate lo deja solo en pantalla y la ficha me dice qué es, sus medidas y cómo se monta. [señalar ficha]
>
> **[1:40]** En Inspect cambio el estado del dron: arranque, reposo, vuelo. Eso es lo que alimenta el cálculo térmico. **[1:55]** En Analyze separo las piezas y hago un corte para ver el interior. **[2:20]** En Studio, X-Ray deja ver lo que está adentro, Thermal muestra el calor con el dron en vuelo y Solid deja solo la forma.
>
> **[2:45]** La página usa WebGL directo para contar el proyecto, y la app usa Unity para inspeccionarlo. [Alt+Tab al deck] [click 2 — contingencia] Lo que falta saber es qué tan bien corre y qué pasó cuando la usaron otras personas.

**Contingencia (igual en deck, tarjetas y `DEMO_SCRIPT.md`):**
> Demo en vivo como principal. Si la app no responde en 10 s o se congela, se narra el video de esta slide, que registra el mismo recorrido. Si falla la landing, se pasa directo a la app.

**No decir:** "la demo reemplaza la evaluación" · "la landing es otra versión de la app" · improvisar otro recorrido · comentar el fallo si se pasa al video

---

### SLIDE 14 — Rendimiento por dispositivo

**Tiempo:** 14:10 – 15:10 · **Pasos:** 5

> La app trae su propio medidor de rendimiento, que anota cuántos cuadros por segundo logra y en qué equipo se midió. Probé seis configuraciones y aquí están cuatro. [click 1 — gráfico] La línea roja es la meta: 30 cuadros por segundo, que es el mínimo para que el movimiento se sienta fluido. [señalar]
>
> [click 2 — Escritorio / iOS] En el computador de escritorio y en un iPhone 17 Pro va cerca de 60. Sobra margen.
>
> [click 3 — Gama media] En un Redmi Note 10S, un Android de gama media, baja a 26,5. Se usa bien, aunque se notan tirones. Fue el teléfono de las pruebas con usuarios.
>
> [click 4 — Gama baja] En un Android más modesto, con gráfica Adreno 610, llega a 17,6. Funciona, pero lento.
>
> [click 5 — Compatibilidad declarada] Así que la app funciona bien en escritorio y en teléfonos potentes, y con limitaciones en los de gama baja. Lo reporto así, equipo por equipo.

**Transición:**
> Pasemos a las personas.

**No decir:** "funciona perfecto en cualquier celular"

---

### SLIDE 15 — SUS

**Tiempo:** 15:10 – 16:25 · **Pasos:** 4

> Participaron doce personas con perfil técnico, diez en celular y dos en computador.
>
> [click 1 — SUS] El cuestionario SUS dio un promedio de 91,88 sobre 100. La mediana fue 95.
>
> [click 2 — Cómo se calcula] Ese número no es un porcentaje, así que explico de dónde sale. Son diez afirmaciones, como "me pareció fácil de usar" o "necesitaría ayuda técnica para usarlo", y cada una se responde de 1 a 5. Las positivas aportan la respuesta menos uno. Las negativas, cinco menos la respuesta. Así, cada ítem vale entre 0 y 4 puntos. La suma va de 0 a 40 y se multiplica por 2,5 para llevarla a una escala de 0 a 100. [pausa]
>
> [click 3 — Rango] Los puntajes fueron de 60 a 100. El 60 fue una persona que tuvo más dificultades, y lo que dijo en voz alta ayuda a entender por qué.
>
> [click 4 — Referencia] El 68 es el promedio histórico de este cuestionario en muchos estudios. No es una nota para aprobar. Sirve de referencia. Y SUS se aplicó solo al visor 3D. La comparación con el 2D viene ahora.

**Transición:**
> [avanzar] Para comparar 3D y 2D usé NASA-TLX.

**No decir:** "SUS prueba que el 3D es mejor que el 2D" · "68 es el mínimo para aprobar" · "91,88 %"

---

### SLIDE 16 — NASA-TLX y tiempos

**Tiempo:** 16:25 – 17:50 · **Pasos:** 5

> NASA-TLX sí se aplicó en las dos condiciones. [click 1 — 3D] Con el visor 3D, la carga de trabajo percibida promedió 8,69. [click 2 — 2D] Con la documentación 2D, 19,89. [click 3 — diferencia] En promedio, 11,19 puntos menos en 3D. Y no fue un promedio arrastrado por unos pocos: en los doce casos, la carga fue menor en 3D. [pausa]
>
> [click 4 — tiempos] ¿Recuerdan el motor del principio? Ubicarlo tomó 5,75 segundos en 3D y 13 en 2D. Sumando las tres tareas cronometradas, 20,58 segundos contra 54.
>
> [click 5 — Cómo se calcula] Cómo funciona NASA-TLX. La persona califica de 0 a 100 seis aspectos de la tarea: exigencia mental, exigencia física, prisa, qué tan bien le fue, esfuerzo y frustración. La versión original además compara esos aspectos de a pares para darles pesos. La versión **Raw**, la que usé, se salta ese paso y promedia los seis. Un detalle: en desempeño, 0 significa que le fue perfecto. Está al revés a propósito, para que en las seis escalas más alto sea siempre más carga y el promedio tenga sentido.
>
> En esta muestra, el 3D se asoció con menos carga y menos tiempo. Es una observación descriptiva, sin pretensión causal. [mirar jurado]

**Transición:**
> Los números dicen qué pasó. Lo que dijeron las personas ayuda a entender por qué.

**No decir:** "NASA-TLX mide aprendizaje" · "T4 también se cronometró" · "el 3D redujo la carga" como causa

---

### SLIDE 17 — Think-Aloud

**Tiempo:** 17:50 – 18:45 · **Pasos:** 4

> Mientras resolvían las tareas, los participantes pensaban en voz alta. Después clasifiqué lo que dijeron.
>
> [click 1 — gráfico] En verde está lo que ayudó y en ámbar lo que estorbó. [click 2 — Coincide con lo cuantitativo] Lo más frecuente fue la comprensión espacial: once de doce personas relacionaron en voz alta el motor, su soporte y los tornillos con el resto del dron. Ocho de doce dijeron que algo les quedó más claro gracias a la vista explosionada, Thermal, X-Ray, Blueprint o el aislamiento.
>
> [click 3 — Fricciones] También hubo problemas. Diez de doce tuvieron dificultades para girar y desplazar el modelo con el dedo. Seis no entendieron algunos iconos a la primera. Dos tuvieron problemas para tocar piezas muy pequeñas.
>
> [click 4 — cierre] Esos problemas ya tienen dirección: son lo primero que hay que mejorar. [mirar jurado]

**Transición:**
> Con todo esto, qué se puede afirmar y qué no.

**No decir:** "la navegación fue una fortaleza" (fue la fricción más frecuente)

---

### SLIDE 18 — Discusión

**Tiempo:** 18:45 – 19:50 · **Pasos:** 2

> [click 1 — Lo que sí soporta] La evidencia sostiene cuatro cosas: en esta muestra, con el 3D se tardó menos, se percibió menos carga, la usabilidad fue alta y las personas describieron mejor cómo se relacionan las piezas. Que cinco fuentes distintas coincidan es lo que le da peso al resultado, aunque la muestra sea chica.
>
> [click 2 — Lo que no debe afirmarse] Y hay cuatro cosas que no afirmo. Que el 3D tenga más éxito: las 96 tareas se completaron en las dos condiciones. Eso se llama **efecto techo**. Cuando todos completan todo, el éxito deja de distinguir, y la diferencia aparece en el tiempo y el esfuerzo. Tampoco afirmo que esto se generalice, ni a otros drones ni a toda la población: doce personas alcanzan para aprender del prototipo, no para hablar de toda la población. Ni que funcione igual en cualquier celular. Ni que Thermal sirva para diagnosticar. [mirar jurado]

**Transición:**
> Con eso, las conclusiones por objetivo.

**No decir:** "el efecto techo invalida los resultados" · "n = 12 prueba todo" · "n = 12 no prueba nada"

---

### SLIDE 19 — Conclusiones

**Tiempo:** 19:50 – 21:10 · **Pasos:** 4

> Vuelvo a los cuatro objetivos.
>
> [click 1 — OE1] El modelo. El CAD, una vez teselado, rondaba los 6,5 millones de triángulos. El activo que usa la web tiene 95 617, y cada paso quedó documentado.
>
> [click 2 — OE2] El rendimiento. En escritorio, cada cuadro se dibuja muy por debajo de los 33 milisegundos que exige la meta de 30 cuadros por segundo. En celulares depende del equipo. Y la app ofrece cinco modos visuales.
>
> [click 3 — OE3] La app. Está publicada, cualquiera puede abrirla desde una dirección web, y tiene selección, ficha, los tres modos y Thermal.
>
> [click 4 — OE4] La evaluación. Usabilidad de 91,88. Carga percibida de 8,69 en 3D contra 19,89 en 2D. Tareas más rápidas en 3D. Y los comentarios explican por qué.
>
> Entonces, respondiendo la pregunta: en esta muestra, el visor 3D se asoció con menos tiempo y menos carga, y es viable en el navegador, con límites en los celulares más modestos. [mirar jurado]
>
> Lo que deja este trabajo es un camino documentado para llevar un CAD a la web sin perder sus piezas, una evaluación que cruza cinco fuentes y un hardware complejo que cualquiera puede explorar desde un enlace.

**Transición:**
> Y lo que sigue.

---

### SLIDE 20 — Trabajo futuro

**Tiempo:** 21:10 – 21:55 · **Pasos:** 6

> ¿Recuerdan los tres niveles del alcance? Hoy TwinSight está en el primero. [click 1 — Digital Shadow] El siguiente es recibir datos del dron real, [click 2 — Digital Twin] y el último, un gemelo que además ayude a decidir. [click 3 — Fases 0 → 5] El informe lo divide en seis fases. [click 4 — Fases 0–1] Primero, más participantes, arreglar la navegación en el celular y describir cada pieza en un formato que no dependa de Unity. [click 5 — Fases 2–3] Después, conectar datos de vuelo: primero grabados, luego en vivo. [click 6 — Fases 4–5] Y al final, un modo de mantenimiento y modelos calibrados, incluido el térmico.

> Empecé hablando de la distancia entre la documentación de un dron y entender cómo está armado. Este trabajo muestra que esa distancia se puede acortar desde el navegador. [mirar jurado]

**No decir:** "la próxima versión será un digital twin" · "FEA en servidor" (no está en el informe)

---

### SLIDE 21 — Cierre

**Tiempo:** 21:55 – 22:00 · **Pasos:** ninguno

> [avanzar] Muchas gracias. Quedo atento a sus preguntas.

**No decir:** abrir un tema nuevo · pedir disculpas · alargar el cierre

---

## 7. Preguntas probables del jurado

### P1: ¿Por qué Unity y no Three.js o Babylon.js?

> No fue por tamaño: Unity pesa más al cargar. Lo elegí porque me daba en un solo lugar el editor visual, el sistema de materiales, la interfaz y un medidor de rendimiento, y eso me permitió trabajar arte técnico, programación y evaluación sin cambiar de herramienta. La landing, en cambio, sí está hecha en WebGL directo, y ahí se ve la otra opción.

### P2: ¿Por qué 12 participantes?

> Lo deseable eran 30. Para una evaluación formativa, el mínimo razonable estaba entre 8 y 12. Llegué a 12 y por eso todo se reporta como descriptivo, sin generalizar.

### P3: ¿Los 95 617 y los 229 054 triángulos se contradicen?

> No. 95 617 es el modelo optimizado, cada pieza distinta una vez. 229 054 es lo que cuenta el medidor con la escena corriendo: todas las copias, los proxies de tornillería y los elementos de apoyo. Uno mide el modelo y el otro la escena.

### P4: ¿Thermal podría convertirse en una simulación real?

> Sí. Habría que pasar de un punto por pieza a una malla de simulación, usar conductividades y unidades reales, definir las condiciones de borde y calibrar con mediciones del dron, por ejemplo con cámara térmica. Está en el trabajo futuro.

### P5: Si Thermal no mide temperatura, ¿por qué la leyenda está en °C?

> Porque el modelo calcula una temperatura para cada pieza. Parte de la temperatura ambiente, sube en las piezas que generan calor según la carga del dron, pasa calor entre piezas en contacto según área, distancia y material, y enfría con el aire. Esa temperatura es la que muestra la leyenda. Lo que no tiene es calibración: el tiempo va acelerado y las conductividades están escaladas, así que el °C describe el modelo y no una medición del dron.

### P6: ¿De dónde salen los valores de conductividad?

> De conductividades reales de cada material: cobre unos 390 W/m·K, aluminio 167, acero 16, fibra de carbono 2,5, FR4 0,3. Esos valores se comprimieron a una escala relativa, de 1,8 para el cobre a 0,18 para la batería, porque con los valores reales y el tiempo acelerado las piezas se igualarían en un solo cuadro. El informe documenta la verificación de ese criterio con WolframAlpha. Está en el backup B8.

### P7: ¿Por qué 257 elementos en la taxonomía y 252 en el profiler?

> Son dos conteos distintos. 257 es la auditoría de elementos que se dibujan o se pueden tocar en la escena final. 252 son los objetos que el medidor contó dibujándose en la sesión medida. Cada cifra está con su fuente.

### P8: ¿Por qué el Holybro X500 V2?

> Porque el fabricante publica sus archivos CAD y su documentación. Eso me permitió trabajar sin material restringido y justificar cada decisión de modelado.

### P9: ¿De dónde salen las cifras de tornillería (425 208 y 14 408)?

> Las medí sobre los archivos del proyecto: la importación con STEPper que se conserva en el repositorio (`blender_files/welded/stepper.fbx`) y la escena exportada final (`x500v2_runtime_low_final.fbx`). En la primera, los 160 sujetadores suman 425 208 triángulos, el 79 % de ese archivo. En la segunda, los 161 proxies suman 14 408. El informe documenta el sistema (20 familias, 168 instancias registradas, 9 reconciliaciones) y las cinco piezas base; estas cifras las agregué para la presentación. La diferencia entre 160, 161 y 168 se debe a que cada fuente cuenta sobre un archivo distinto.

### P10: ¿Qué haría distinto?

> Más participantes, una interfaz de escritorio diseñada desde el principio y usuarios del sector industrial para contrastar la lectura técnica.

---

## 8. Cortes de emergencia

- **Si quedan menos de 8 minutos al llegar a la slide 14:** decir solo: escritorio cerca de 60 FPS, Redmi 26,5, gama baja 17,6; funciona con límites en celulares modestos. Seguir en la 15.
- **Si quedan menos de 6 minutos al llegar a la slide 15:** fusionar 15 a 17. SUS 91,88 solo en 3D; NASA-TLX 8,69 contra 19,89; tareas más rápidas en 3D; fricciones en navegación táctil, iconos y piezas pequeñas. En la 18, solo la columna de lo que no se afirma.
- **Si el jurado interrumpe en la demo:** detener la interacción y responder: *"puedo mostrar lo que está publicado; lo que no está en la interfaz final no lo presento como alcance."*
- **Si la app no responde en la demo:** volver al deck y narrar el video de la slide 13. No improvisar otra ruta ni comentar el fallo.
