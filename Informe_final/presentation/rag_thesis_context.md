# RAG Thesis Context: TwinSight X500
## Archivo de Referencia Canónico (Factual Source of Truth) para Auditorías

Este archivo contiene toda la información metodológica, técnica, de usabilidad y de narrativa de la tesis de grado **TwinSight X500**, recopilada de `PRESENTATION_SCRIPT.md`, `PRESENTATION_OUTLINE.md` e `informe_final.pdf`. Su propósito es servir como la única fuente de verdad factual para las auditorías, evitando desviaciones conceptuales o alucinaciones (AI slop).

---

## 1. Identificación y Metadatos del Proyecto
*   **Título de la Tesis:** TwinSight X500: prototipo web 3D para visualización técnica de hardware complejo.
*   **Autor (Estudiante):** Alexander Woodcock Salomón.
*   **Asesor de Tesis:** Gustavo Enrique Vejarano Matiz.
*   **Programa Académico:** Ingeniería Multimedia.
*   **Institución:** Universidad Nacional Abierta y a Distancia (UNAD), Escuela de Ciencias Básicas, Tecnología e Ingeniería (ECBTI), Pasto, Colombia.
*   **Año:** 2026.
*   **Caso de Estudio:** Dron Holybro X500 V2 (PX4 development kit).
*   **Línea de Investigación:** Ingeniería Multimedia, Visualización Técnica 3D Web.

---

## 2. Delimitación y Alcance Técnico (Frontera Metodológica)
*   **Clasificación del Artefacto:** **Visual Product Twin** (o *Technical Product Twin*). Es un modelo visual-semántico web 3D interactivo optimizado.
*   **Exclusiones Críticas (Lo que NO es ni hace):**
    *   **NO es un Digital Twin Operacional:** No tiene telemetría en tiempo real ni en vivo del dron físico, no sincroniza variables de estado activo ni realiza diagnósticos o mantenimiento predictivo. Tampoco se conecta con sistemas PLM, ERP, CMMS, SCADA ni redes IoT.
    *   **NO es una Simulación FEA:** El modo *Thermal* es una **visualización heurística relativa** basada en familias funcionales de componentes activos (fuentes de calor primarias como batería, motores, ESC y controladora) y pasivos (chasis, hélices). No ejecuta simulación física termo-mecánica ni solvers de análisis de elementos finitos (FEA), ni calcula o diagnostica temperaturas reales en grados centígrados (ºC).
    *   **Navegación Móvil de Escritorio:** La UI es mobile-first. La versión de escritorio es una adaptación funcional, no un rediseño de UI optimizado específicamente para PC.

---

## 3. Métricas Técnicas y Complejidad Geométrica (Assets & Runtime)

### Conteo de Triángulos y Optimización
*   **Modelos CAD Originales (STEP/NURBS de entrada):** Superior a **6.5 millones de triángulos** (con geometría caótica, n-gons y vértices duplicados).
*   **Activo Base Optimizado (Exportado de Blender):** **95,617 triángulos** (representa el modelo 3D optimizado mediante retopología, limpieza y bake de texturas).
*   **Escena Runtime Completa (GPU WebGL):** **229,054 triángulos estimados** (medido por el profiler interno; incluye la carga de la escena en ejecución: mallas duplicadas, proxies de colisión, UI Toolkit, entorno, etc.).
*   *Nota:* No comparar 95,617 y 229,054 como una reducción del uno al otro. El primero es el peso del activo en disco y el segundo es la complejidad de la escena cargada en memoria.

### Payload de la Build y Configuración WebGL
*   **Payload Activo Publicado:** **15.78 MB** (15.05 MiB) en total.
    *   Archivo `.data.unityweb` (huella principal de datos): 8.48 MB.
    *   Archivo `.wasm.unityweb` (código WebAssembly comprimido): 7.11 MB.
    *   Framework + loader de JavaScript: 0.19 MB (0.08 MB framework + 0.12 MB loader).
*   **Configuración del Heap WebGL:** 32 MB iniciales, con capacidad de crecimiento dinámico (paso geométrico de 20%, tope en 96 MB) hasta un máximo absoluto de 512 MB.
*   **Estrategia de Texturas:** Materiales PBR utilizando BaseColor, Normal y una máscara compacta de canales (R = Oclusión Ambiental, G = Rugosidad, B = Curvatura, A = Metálico).

---

## 4. Clasificación y Taxonomía del Sistema (Normalización)
*   **Piezas Canónicas Semánticas:** **28 categorías/piezas** representadas en el catálogo de la base de datos de la app.
*   **Anchors de Escena:** **30 anchors** en la jerarquía (las 28 piezas + `x500v2_fastener_group` + `x500v2_misc_group`).
*   **Renderers/Colliders Finales:** **257 elementos** auditados en la escena final.
*   **Assets Generados en Unity:** **257 assets** bajo la ruta `Assets/Core/Data/X500V2Generated`.
*   **Sistema de Fasteners (Tornillería):**
    *   Familias de fasteners: 20 familias reutilizables.
    *   Instancias en la escena: 168 instancias de fasteners.
    *   Entradas de reconciliación: 9 entradas para asociar nombres entre Blender, catálogo y Unity.
*   **Estructura de Scripts (C#):** 129 archivos `.cs` en total (95 archivos runtime propios, 103 archivos Assets sin tests, 26 de editor/plugins).

---

## 5. Rendimiento WebGL por Dispositivo (Medición con Profiler)

Todas las mediciones se realizaron con la caché del navegador previamente cargada (warm start) y resoluciones específicas:

| Dispositivo | SO / Navegador | Escenario Operativo | FPS Promedio | Frame Time Promedio | Pico de Memoria | Lectura / Viabilidad |
| :--- | :--- | :--- | :---: | :---: | :---: | :--- |
| **PC Escritorio** (i7-5820K, GTX 980 Ti, 48GB RAM) | Windows 11 / Chrome 141 | Escenarios válidos (base, selection, explode, cut, thermal, studio) | **59.8 FPS** | **16.7 ms** | 413 MB | Fluidez estable en todos los escenarios. |
| **iPhone 17 Pro** (A19 Pro) | iOS / Safari | Recorrido completo | **58.7 FPS** | **17.0 ms** | 356 MB | Fluidez excelente, muy cercana a 60 FPS. |
| **Móvil Android Adreno 650** | Android 12 / Chrome 114 | Escenarios válidos | **30.0 FPS** | **36.2 ms** | 254 MB | Cerca de la meta de 30 FPS. |
| **Redmi Note 10S** (Mali-G76) | MIUI Global 14.0.11 / Chrome 148 | Escenarios válidos (base, selection, thermal/studio, explode, cut) | **26.5 FPS** | **40.3 ms** | 226 MB | Experiencia móvil funcional y usable. |
| **Móvil Android Adreno 610** | Android 10 / Chrome 148 | Escenarios válidos | **24.5 FPS** | **52.1 ms** | 311 MB | Rendimiento usable con picos perceptibles en Thermal. |
| **Móvil Android Adreno 610** | Android 12 / Chrome 114 | Escenarios válidos | **17.6 FPS** | **61.4 ms** | 263 MB | Límite operativo inferior. Por debajo de 30 FPS. |

---

## 6. Resultados de Evaluación con Usuarios (n=12)

### Diseño del Experimento
*   **Metodología:** Diseño comparativo intra-sujeto de carácter formativo y descriptivo.
*   **Muestra:** 12 participantes con perfil afín (estudiantes, ingenieros, técnicos multimedia).
*   **Balanceo de Sesiones (Contrabalanceo):** Secuencias AB/BA (códigos impares inician con Visor 3D; códigos pares inician con Soporte 2D) para mitigar el sesgo de fatiga o aprendizaje.
*   **Condición A:** Visor 3D interactivo en web (TwinSight).
*   **Condición B:** Soporte 2D estático (Assembly Manuals de Holybro + lámina de rotulado taxonómico).

### Usabilidad Percibida (SUS - System Usability Scale)
*   *Nota:* Aplicado exclusivamente al prototipo 3D.
*   **Media (Promedio):** **91.88** (sobre 100).
*   **Mediana:** **95.00**
*   **Desviación Estándar:** **11.24**
*   **Rango:** Min 60.00 – Max 100.00.
*   **Referencia Histórica:** 68 (promedio global de usabilidad). Indica usabilidad muy favorable en la muestra.

### Carga de Trabajo Percibida (NASA-TLX Raw)
*   *Nota:* Medido en escala de 0 a 100 por dimensión. Promediado directamente sin ponderaciones ("Raw").
*   **Promedio Condición 3D:** **8.69**
*   **Promedio Condición 2D:** **19.89**
*   **Diferencia Pareada Media:** **11.19 puntos** a favor de la condición 3D (menor carga percibida en el 100% de los 12 casos).
*   *Nota:* La subescala de rendimiento se registró invertida (0 = perfecto, 100 = fallido) para mantener la consistencia direccional.

### Desempeño en Tareas Cronometradas (Tiempos Medios en Segundos)
*   **T1 (Identificación / Ubicación de pieza):** **5.75s** (3D) frente a **13.00s** (2D) — Diferencia de 7.25s.
*   **T2 (Interpretación de relación estructural):** **3.50s** (3D) frente a **18.00s** (2D) — Diferencia de 14.50s.
*   **T3 (Uso de herramientas / modos):** **11.33s** (3D) frente a **23.00s** (2D) — Diferencia de 11.67s.
*   **Tiempo Total T1-T3 (Acumulado medio):** **20.58s** (3D) frente a **54.00s** (2D) — Reducción neta de **33.42s** (reducción del 61.9% en tiempo de ejecución).
*   **T4 (Análisis visual exploratorio):** Tarea libre y guiada no cronometrada (sin métrica de tiempo comparable).
*   **Eficacia:** Completitud del 100% (12/12) en ambas condiciones (efecto techo).

### Triangulación Cualitativa (Think-Aloud)
*   **Comprensión Espacial:** Identificada en 11 de 12 participantes (fortaleza principal del visor 3D para ubicar e interpretar componentes).
*   **Navegación y Control:** Presente en 10 de 12 participantes.
*   **Fricciones de Interfaz Detectadas:**
    *   Estados incorrectos o confusión en iconos de UI (6 de 12 participantes).
    *   Dificultades en órbita o pan táctil en móvil (5 de 12 participantes).
    *   Dificultad de selección de fasteners o piezas extremadamente pequeñas (4 de 12 participantes).

---

## 7. Roadmap y Evolución Futura
*   **Fase 0 (Corto plazo):** Ampliación del *Visual Product Twin* (muestra de pruebas n >= 30, rediseño de iconos, depuración de interacción táctil).
*   **Fase 1 (Corto plazo):** Formalización de un *Twin Manifest* independiente en formato JSON.
*   **Fase 2 (Medio plazo):** Telemetría histórica o simulada (reproducción de estados en CSV/JSON en línea de tiempo) - Nivel **Digital Shadow**.
*   **Fase 3 (Medio plazo):** Live digital shadow (MQTT, WebSocket, REST, MAVLink, ROS).
*   **Fase 4 (Largo plazo):** Modo servicio y checklists interactivos de mantenimiento guiado.
*   **Fase 5 (Largo plazo):** Digital twin operacional completo (FEA en servidor, simulaciones interactivos "what-if" en tiempo real).
