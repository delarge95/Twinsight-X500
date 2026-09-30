# SPEAKER_CARDS.md
# Tarjetas orales de sustentación — TwinSight X500

**Estado:** operativo para ensayo cronometrado. Sincronizado con `index_final.html` (pantalla de espera + 21 slides) y `PRESENTATION_SCRIPT.md`.
**Fecha:** 2026-09-30.
**Duración meta:** 22:00 según el guion. Con el ritmo medido en ensayo, ~29–30 min.
**Fuente canónica:** `awoodcocks.pdf` (informe final).
**Numeración:** posición en el deck; coincide con el número visible y con el contador "NN / 21". La pantalla de espera (slide 0) no cuenta: → la cierra; ← en la slide 1 o **P** la reabren. **S** abre la vista del presentador. Backups: B1–B15 (escribir el código en el contador para saltar).

---

## Cómo usarlas

Son la versión corta del guion. Si el tiempo aprieta, manda lo que dice aquí. Cada tarjeta resume la idea que tiene que quedar; dila con tus palabras. "Clics" es cuántas veces pulsar → antes de pasar de slide.

---

## Tarjetas

| Slide | Tiempo | Clics | Lo que tiene que quedar | En pantalla | Evitar |
|---|---|---|---|---|---|
| **0** | antes de empezar | → | Pantalla de espera con el dron (encendido, despegue, vuelo, aterrizaje). Se proyecta mientras el jurado se acomoda. | Dron en vivo + título. | Dejarla abierta al empezar a hablar. |
| **1** | 0:00–0:40 | 0 | "App web para inspeccionar en 3D el Holybro X500 V2. CAD = archivos de diseño, precisos y pesados. WebGL = 3D en el navegador, sin instalar nada." | Título, ficha del proyecto, captura. | "Es un gemelo digital". |
| **2** | 0:40–1:40 | 4 | "Ubicar un motor con el PDF. La información está, pero repartida. Quien lee arma el objeto en la cabeza: reconstrucción espacial. Faltaba un puente." | Documentación → Fricción → Respuesta + Idea clave. | Decir que el 2D no sirve. |
| **3** | 1:40–2:55 | 4 | "Memoria de trabajo limitada. Intrínseca, extrínseca (la que el diseño baja), germana. La tesis no las mide. La app hace parte del trabajo mental: girar con el dedo, ficha como memoria externa." | Tres tipos + Límite declarado. | "NASA-TLX mide la carga intrínseca". |
| **4** | 2:55–3:55 | 2 | "Digital twin = conectado y calibrado. Shadow = recibe datos. TwinSight no hace eso ni FEA. Representa el producto: visual product twin, primer escalón." | Exclusiones (clic 1) y alcance (clic 2). | "Ya es un gemelo digital". |
| **5** | 3:55–4:50 | 4 | "Pregunta: diferencias de desempeño y carga 3D vs 2D, y si funciona en el navegador. Cuatro objetivos. Método: Design Science Research, evaluación formativa con 12 personas." | Pregunta + OE1–OE4. | "30 FPS en todo dispositivo". |
| **6** | 4:50–5:25 | 6 | "Cinco fuentes: rendimiento, SUS, tareas, Think-Aloud, NASA-TLX. Si coinciden, el resultado pesa más." (solo nombrarlas) | Triangulación + lista. | Describir cada instrumento aquí. |
| **7** | 5:25–6:40 | 8 | "El CAD no tiene triángulos: se generan al teselar. Salieron 6,5–6,9 M. Limpieza, retopología, proxies y bake → 95 617, sin perder las piezas." | Flujo + por qué optimizar + cifras por ruta. | "El CAD tenía 6,5 M de triángulos". |
| **8** | 6:40–7:30 | 1 | "Cuatro grupos unidos por un bus de eventos. Tocar una pieza: selección avisa; ficha, marcas y tornillo reaccionan sin conocerse." | Cuatro grupos (visibles) + ejemplo (clic). | Recorrer capa por capa. |
| **9** | 7:30–8:45 | 7 | "28 piezas, 30 anclas, 257 elementos. Hotspots = entrada por grupos. Tornillería: 425 208 → 14 408 triángulos. Tornillo detallado al inspeccionar: 3 cabezas, 1 vuelta de rosca, 1 punta." | Cifras + tres tarjetas + figura. | "Todos los fasteners son modulares". |
| **10** | 8:45–9:30 | 7 | "Entrada → Explore → tocar pieza → ficha. La barra Inspect · Analyze · Studio está siempre visible." | Flujo con barra fija + captura. | "Los modos aparecen al seleccionar". |
| **11** | 9:30–10:00 | 3 | "Inspect: qué es y dónde está. Analyze: cómo se conecta. Studio: qué hay adentro." | Tres capturas móviles. | Explicar cada modo en detalle. |
| **12** | 10:00–11:10 | 2 | "Modelo físico simplificado: fuentes según carga, conducción por área, distancia y material, enfriamiento por aire. Sirve para entender; diagnosticar exige calibrar." | Cómo calcula + diagrama + límite. | "Valores inventados" · leer °C como medición. |
| **13** | 11:10–14:10 | 2 | "Demo EN VIVO: landing WebGL (ensamblaje → 6 capítulos → mini app) y, con «Abrir visor», la app (selección → Isolate → ficha · Power · Explode · Cut · X-Ray → Thermal → Solid). 3:00." | Ruta con tiempos; video de respaldo en pausa. | Pasar de 10 s sin respuesta: video, sin comentarlo. |
| **14** | 14:10–15:10 | 5 | "La app mide su propio rendimiento. Meta 30 FPS. Escritorio e iPhone ≈60; Redmi 26,5; Adreno 610, 17,6. Funciona con límites en gama baja." | Barras + tres lecturas. | "Funciona igual en cualquier celular". |
| **15** | 15:10–16:25 | 4 | "SUS 91,88 (mediana 95). Diez ítems de 1 a 5; positivos R − 1, negativos 5 − R; suma 0–40 × 2,5. Rango 60–100. 68 = promedio histórico. Solo 3D." | SUS + cálculo + rango + 68. | "91,88 %". |
| **16** | 16:25–17:50 | 5 | "NASA-TLX 8,69 vs 19,89, menor en los 12. Motor 5,75 vs 13 s; T1–T3 20,58 vs 54 s. Seis escalas promediadas; Raw = sin pesos; desempeño al revés." | Cifras + tiempos + cálculo. | Lenguaje causal. |
| **17** | 17:50–18:45 | 4 | "Comprensión espacial 11/12, claridad 8/12. Problemas: girar con el dedo 10/12, iconos 6, piezas pequeñas 2." | Barras + listas. | La navegación como fortaleza. |
| **18** | 18:45–19:50 | 2 | "Sí: menos tiempo, menos carga, SUS alto. No: éxito (96/96, efecto techo), generalizar (un dron, 12 personas), cualquier celular, Thermal para diagnosticar." | Dos columnas. | Definir el efecto techo con los tiempos. |
| **19** | 19:50–21:10 | 4 | "OE1–OE4 con su cifra. Responder la pregunta. Lo que deja el trabajo: camino CAD → web, evaluación de cinco fuentes, hardware explorable desde un enlace." | Tabla por objetivo. | Omitir el matiz móvil. |
| **20** | 21:10–21:55 | 6 | "Hoy, primer escalón. Luego datos del dron real y modelos calibrados. Cierre: la distancia entre documentación y comprensión se acorta desde el navegador." | Escalera + fases. | "La próxima versión será un digital twin". |
| **21** | 21:55–22:00 | 0 | "Muchas gracias. Quedo atento a sus preguntas." | "Gracias." + URL. | Alargar el cierre. |

---

## Backups útiles tras el recorte

| Si preguntan por… | Ir a |
|---|---|
| 95 617 vs 229 054 triángulos | B6 |
| El profiler y el JSON exportado | B5 |
| Principios de interfaz (Norman, Gestalt, Nielsen, Hutchins) | B12 |
| Metodología DSR y sus fases | B13 |
| Limitaciones una por una | B14 (o B9) |
| El aporte en tres dimensiones | B15 |
| Thermal: ecuaciones y materiales | B8 |

---

## Cortes de emergencia

| Situación | Qué hacer |
|---|---|
| Quedan **< 8 min** al llegar a la slide 14 | Decir solo: escritorio ≈60 FPS, Redmi 26,5, gama baja 17,6. Seguir en la 15. |
| Quedan **< 6 min** al llegar a la slide 15 | Fusionar 15–17: SUS 91,88 solo 3D / NASA 8,69 vs 19,89 / tareas más rápidas en 3D / problemas de navegación táctil, iconos y piezas pequeñas. En la 18, solo la columna derecha. |
| **La app no responde en la demo** (> 10 s o congelada) | Volver al deck (slide 13), clic en el video y narrar el mismo recorrido. No comentar el fallo. |
| El jurado interrumpe la demo | *"Puedo mostrar lo que está publicado; lo que no está en la interfaz final no lo presento como alcance."* |
| Pregunta fuera del alcance | *"Eso corresponde a una fase posterior del plan. Aquí me limito a lo que se construyó y se evaluó."* |

---

## Frases de apoyo

- **Alcance:** *"TwinSight no opera el dron. Lo hace entendible desde la web."*
- **Resultados:** *"En esta muestra, el 3D se asoció con menos tiempo y menos carga. Es descriptivo."*
- **Thermal:** *"Es física de transferencia de calor, simplificada para correr en un celular."*
- **Límites:** *"Decir hasta dónde llegan los resultados es parte del trabajo."*
- **Pregunta difícil:** *"Eso está en el informe, en [capítulo]. Lo central aquí es..."*
