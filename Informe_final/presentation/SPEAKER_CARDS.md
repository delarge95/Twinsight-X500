# SPEAKER_CARDS.md
# Tarjetas orales de sustentación — TwinSight X500

**Estado:** operativo para ensayo cronometrado. Sincronizado con `index_final.html` (29 slides) y `PRESENTATION_SCRIPT.md`.
**Fecha:** 2026-09-29.
**Duración meta:** 28:30 + 1:30 de margen = 30:00.
**Fuente canónica:** `awoodcocks.pdf` (informe final).
**Numeración:** posición en el deck; coincide con el número visible y con el contador "NN / 29". Backups: B1–B11 (escribir el código en el contador para saltar).

---

## Cómo usarlas

Son la versión corta del guion. Si el tiempo aprieta, manda lo que dice aquí. Cada tarjeta resume la idea que tiene que quedar; dila con tus palabras. "Clics" es cuántas veces pulsar → antes de pasar de slide.

---

## Tarjetas

| Slide | Tiempo | Clics | Lo que tiene que quedar | En pantalla | Evitar |
|---|---|---|---|---|---|
| **1** | 0:00–0:40 | 0 | "App web para inspeccionar en 3D el Holybro X500 V2. CAD = archivos de diseño, precisos y pesados. WebGL = 3D en el navegador, sin instalar nada." | Título, ficha del proyecto, captura. | "Es un gemelo digital". |
| **2** | 0:40–1:40 | 4 | "Ubicar un motor con el PDF. La información está, pero repartida. Quien lee arma el objeto en la cabeza: reconstrucción espacial. Faltaba un puente." | Documentación → Fricción → Respuesta + Idea clave. | Decir que el 2D no sirve. |
| **3** | 1:40–2:40 | 4 | "Memoria de trabajo limitada. Intrínseca = el contenido; extrínseca = cómo se presenta (la que el diseño baja); germana = el esfuerzo útil. La tesis no las mide." | Tres tipos + Límite declarado. | "NASA-TLX mide la carga intrínseca". |
| **4** | 2:40–3:45 | 4 | "Hutchins: la herramienta piensa con nosotros. Norman, Gestalt y Nielsen, aplicados en la app y en este deck." | Tabla principio / app / deck. | Presentarlos como evidencia. |
| **5** | 3:45–4:45 | 2 | "Digital twin = conectado y calibrado. Shadow = recibe datos. TwinSight no hace eso ni FEA. Representa el producto: visual product twin, primer escalón." | Exclusiones (clic 1) y alcance (clic 2). | "Ya es un gemelo digital". |
| **6** | 4:45–5:35 | 4 | "Pregunta: diferencias de desempeño y carga 3D vs 2D, y si funciona en el navegador. Cuatro objetivos: modelo, rendimiento, app, evaluación." | Pregunta + OE1–OE4. | "30 FPS en todo dispositivo". |
| **7** | 5:35–6:20 | 7 | "DSR: investigar construyendo y evaluando. Ciclo de Peffers. Evaluación formativa y descriptiva: doce personas, sin generalizar." | Ciclo + marco + formativa/descriptiva. | "Se probó causalidad". |
| **8** | 6:20–7:25 | 6 | "Cinco fuentes: rendimiento, SUS (solo 3D), tareas, Think-Aloud, NASA-TLX (3D y 2D). Si coinciden, el resultado pesa más." | Triangulación + lista. | Mezclar SUS con la comparación. |
| **9** | 7:25–8:40 | 8 | "El CAD no tiene triángulos: se generan al teselar. Salieron 6,5–6,9 M. Limpieza, retopología, proxies y bake → 95 617, sin perder las piezas." | Flujo + por qué optimizar + cifras por ruta. | "El CAD tenía 6,5 M de triángulos". |
| **10** | 8:40–9:05 | 2 | "La escena, en vivo: 252 mallas, ~229 000 triángulos. No es la cifra anterior." | Visor 3D con Wireframe. | Decir que es el activo de 95 617. |
| **11** | 9:05–9:50 | 3 | "95 617 = el modelo. 229 054 = la escena corriendo, con copias y proxies. No se restan." | Dos cifras + cómo leerlas. | "Se redujo de 229 054 a 95 617". |
| **12** | 9:50–11:00 | 5 | "Bus de eventos: un módulo avisa y los interesados reaccionan. Tocar una pieza: selección avisa; ficha, marcas y tornillo reaccionan sin conocerse." | Cuatro grupos + ejemplo real. | Explicarlo como "arriba / abajo". |
| **13** | 11:00–12:30 | 7 | "28 piezas, 30 anclas, 257 elementos. BOM = lista de fabricación; esto es para navegar. Hotspots = entrada por grupos. Tornillería: 425 208 → 14 408 triángulos. Tornillo detallado al inspeccionar: 3 cabezas, 1 vuelta de rosca, 1 punta." | Cifras + tres tarjetas + figura de piezas base. | "Todos los fasteners son modulares". |
| **14** | 12:30–13:15 | 7 | "Entrada → Explore → tocar pieza → ficha. La barra Inspect · Analyze · Studio está siempre visible. Lo que aparece bajo demanda es la ficha." | Flujo con barra fija + captura. | "Los modos aparecen al seleccionar". |
| **15** | 13:15–14:05 | 4 | "Inspect: qué es y dónde está. Analyze: cómo se conecta. Le quitan al usuario trabajo de imaginación." | Columnas + dos clips. | Explode como ensamblaje exacto. |
| **16** | 14:05–14:50 | 6 | "Cada modo, una pregunta: cómo se ve, qué hay adentro, qué forma tiene, dónde está el calor. Shader y preset." | Cuatro modos + clip. | "Los shaders simulan física". |
| **17** | 14:50–16:00 | 2 | "Modelo físico simplificado: fuentes según carga, conducción por área, distancia y material, enfriamiento por aire. Un punto por pieza, tiempo acelerado. Sirve para entender; diagnosticar exige calibrar." | Cómo calcula + diagrama + límite. | "Son valores inventados" · leer los °C como medición. |
| **18** | 16:00–19:00 | 2 | "Demo EN VIVO: landing WebGL (ensamblaje → 6 capítulos → mini app) y, con «Abrir visor», la app (selección → Isolate → ficha · Power · Explode · Cut · X-Ray → Thermal → Solid). 3:00." | Ruta con tiempos; video de respaldo en pausa. | Pasar de 10 s sin respuesta: volver al video sin comentarlo. |
| **19** | 19:00–19:45 | 2 | "La app mide su propio rendimiento y anota equipo, navegador y resolución. Extracto real: 59,8 FPS." | Trazabilidad + JSON. | Llamarlo "telemetría". |
| **20** | 19:45–20:40 | 5 | "Meta: 30 FPS. Escritorio e iPhone ≈60; Redmi 26,5 (el de las pruebas); Adreno 610, 17,6. Funciona con límites en gama baja." | Barras + tres lecturas. | "Funciona igual en cualquier celular". |
| **21** | 20:40–21:55 | 4 | "SUS 91,88 (mediana 95). Diez ítems de 1 a 5; positivos R − 1, negativos 5 − R; suma 0–40 × 2,5. Rango 60–100. 68 = promedio histórico, no nota de corte. Solo 3D." | SUS + cómo se calcula + rango + 68. | "91,88 %" · usar SUS para comparar. |
| **22** | 21:55–23:20 | 5 | "NASA-TLX 8,69 vs 19,89, menor en los 12. Motor 5,75 vs 13 s; T1–T3 20,58 vs 54 s. Seis escalas 0–100 promediadas; Raw = sin pesos; desempeño al revés para que más alto sea más carga." | Cifras + tiempos + cómo se calcula. | Lenguaje causal. |
| **23** | 23:20–24:15 | 4 | "Comprensión espacial 11/12, claridad 8/12. Problemas: girar con el dedo 10/12, iconos 6, piezas pequeñas 2. Son lo primero a mejorar." | Barras + listas. | Presentar la navegación como fortaleza. |
| **24** | 24:15–25:20 | 2 | "Sí: menos tiempo, menos carga, SUS alto, mejor descripción espacial. No: éxito (96/96, efecto techo), generalizar, cualquier celular, Thermal para diagnosticar." | Dos columnas. | Definir el efecto techo con los tiempos. |
| **25** | 25:20–26:25 | 4 | "OE1: 6,5 M teselados → 95 617. OE2: escritorio holgado, móvil según equipo. OE3: publicada. OE4: 91,88 y 8,69 vs 19,89. Responder la pregunta." | Tabla por objetivo. | Omitir el matiz móvil. |
| **26** | 26:25–27:05 | 5 | "Un dron, doce personas, interfaz de escritorio adaptada, sin cables. Decirlo marca hasta dónde valen los resultados." | Cuatro tarjetas. | Disculparse. |
| **27** | 27:05–27:45 | 6 | "Hoy, primer escalón. Luego datos del dron real (grabados, en vivo) y al final modelos calibrados, incluido el térmico." | Escalera + fases. | "La próxima versión será un digital twin". |
| **28** | 27:45–28:25 | 3 | "La distancia entre documentación y comprensión se acorta desde el navegador. Aporte técnico, metodológico y comunicativo." | Tres columnas. | "Categoría validada". |
| **29** | 28:25–28:30 | 0 | "Muchas gracias. Quedo atento a sus preguntas." | "Gracias." + URL. | Alargar el cierre. |

---

## Cortes de emergencia

| Situación | Qué hacer |
|---|---|
| Quedan **< 7 min** al llegar a la slide 19 | Fusionar 19–20: la app mide su rendimiento; escritorio ≈60 FPS, Redmi 26,5, gama baja 17,6. Seguir en la 21. |
| Quedan **< 5 min** al llegar a la slide 21 | Fusionar 21–23: SUS 91,88 solo 3D / NASA 8,69 vs 19,89 / tareas más rápidas en 3D / problemas de navegación táctil, iconos y piezas pequeñas. En la 24, solo la columna derecha. |
| **La app no responde en la demo** (> 10 s o congelada) | Volver al deck (slide 18), clic en el video y narrar el mismo recorrido. No comentar el fallo. |
| El jurado interrumpe la demo | Detener la interacción: *"Puedo mostrar lo que está publicado; lo que no está en la interfaz final no lo presento como alcance."* |
| Pregunta fuera del alcance | *"Eso corresponde a una fase posterior del plan. Aquí me limito a lo que se construyó y se evaluó."* |

---

## Frases de apoyo

- **Alcance:** *"TwinSight no opera el dron. Lo hace entendible desde la web."*
- **Resultados:** *"En esta muestra, el 3D se asoció con menos tiempo y menos carga. Es descriptivo."*
- **Thermal:** *"Es física de transferencia de calor, simplificada para correr en un celular."*
- **Límites:** *"Decir hasta dónde llegan los resultados es parte del trabajo."*
- **Pregunta difícil:** *"Eso está en el informe, en [capítulo]. Lo central aquí es..."*
