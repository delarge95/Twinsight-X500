# Auditoría 5: Dinámicas Técnicas, Microinteracciones y Animaciones (UX & Interaction)
## Marco de Evaluación, KPIs y Rúbrica de Medición

Esta auditoría evalúa la calidad de las microinteracciones, la fluidez y pertinencia de las transiciones de Reveal.js y las animaciones vectoriales de apoyo visual. Se apoya en la adaptación de los **Principios de Animación de Disney** para interfaces digitales y la teoría de microinteracciones de **Dan Saffer**.

---

### 1. Marco Teórico de Referencia
*   **Thomas, F., & Johnston, O. (1981).** *The Illusion of Life: Disney Animation*: Conceptos como *Timing* (duración), *Staging* (puesta en escena clara de la acción) y *Slow In and Slow Out* (aceleración/deceleración natural) son esenciales en interfaces. En una presentación técnica, las animaciones no deben ser decorativas, sino funcionales: dirigir el foco visual y hacer visibles las transformaciones físicas del dron (desglose o cambio de modo shader).
*   **Saffer, D. (2013).** *Microinteractions*: Estructura las microinteracciones en cuatro partes: disparador (trigger), reglas (rules), retroalimentación (feedback) y bucles/modos. Se auditará la efectividad con la que las animaciones de Reveal.js y los SVG interactivos confirman las acciones del orador o simulan el comportamiento real del prototipo.

---

### 2. Indicadores Clave de Rendimiento (KPIs)

#### KPI 5.1: Ajuste de Temporización y Fluidificación (Pacing & Easing Consistency - PEC)
*   **Definición:** Medida en que las transiciones de Reveal.js (`fade-up`, `fade-right`) y animaciones SVG respetan los rangos óptimos de duración (250ms - 400ms) y usan curvas de aceleración naturales (`ease` o `cubic-bezier`).
*   **Medición:** Análisis de los tiempos y curvas declarados en las reglas CSS de `.reveal .slides section .fragment`.

#### KPI 5.2: Claridad del Staging en Animaciones Vectoriales (Staging & Animation Clarity - SAC)
*   **Definición:** Grado de legibilidad de las animaciones que representan cambios estructurales en el dron (ej. simulación SVG de vista explotada en slide 14 o ciclo de modos de renderizado en slide 15). El ojo del jurado debe seguir el movimiento sin esfuerzo analítico.
*   **Medición:** Análisis de la superposición de trayectorias y velocidad de desplazamiento de nodos SVG.

#### KPI 5.3: Latencia del Bucle de Retroalimentación (Feedback Loop Latency - FLL)
*   **Definición:** Tiempo transcurrido entre la activación de un fragmento/animación y la respuesta visual perceptible en pantalla.
*   **Medición:** Análisis del frametime y delay en el render del navegador durante la ejecución de bucles interactivos (`sel-pulse`, `mode-cycle`), debiendo ser inferior a 100ms para evitar sensación de lag.

#### KPI 5.4: Ergonomía de Interacción Táctil (Tactile Interaction Ergonomics - TIE)
*   **Definición:** Diseño de interacciones específicas de mobile-backup (zonas táctiles y gestos) adaptados a la fisiología humana en pantallas pequeñas.
*   **Medición:** Dimensiones mínimas de elementos accionables (mínimo 44x44px según Apple HIG/Android Design) y tolerancia de gestos multi-touch.

---

### 3. Rúbrica de Evaluación Objetivo-Realista
*Nota: La calificación "Perfecto (5)" en dinámicas técnicas exige que todas las animaciones se ejecuten a 60 FPS estables sin pérdida de frames, sincronizadas con el discurso físico y con tiempos de respuesta instantáneos.*

| KPI / Criterio | Deficiente (1) | Regular (2) | Bueno (3) | Excelente (4) | Perfecto/Ideal (5) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **KPI 5.1: PEC** | Animaciones lentas (> 800ms) que retrasan el discurso o transiciones lineales abruptas (`linear`). | Duración de fragmentos descompensada (ej. unos aparecen a 100ms y otros a 600ms sin criterio). | Transiciones estándar a 350ms pero con curvas de aceleración bruscas al final. | Transiciones y animaciones fluidas con timing óptimo (250-400ms) y curvas `ease-out` naturales. | **Timing cinemático.** Transición adaptativa que responde al ritmo del habla del presentador. |
| **KPI 5.2: SAC** | Movimientos caóticos en múltiples direcciones a la vez (ej. chasis y brazos explotando sin eje claro). | Animaciones SVG se superponen entre sí, provocando que se pierda la relación espacial del dron. | Staging correcto. Los componentes se desplazan en ejes ortogonales claros, con pequeñas colisiones visuales. | Desglose visual limpio. Las piezas se expanden revelando la estructura interna con total claridad. | **Staging matemático.** Ejes y trayectorias alineados dinámicamente con los vectores de diseño CAD originales. |
| **KPI 5.3: FLL** | Latencia perceptible (> 200ms) en la activación de animaciones CSS. Lag o tirones visuales. | Latencia entre 100ms y 200ms. Sensación de pesadez al interactuar con el simulador SVG. | Respuesta visual dentro de los 100ms. Fluidez aceptable con caídas ocasionales de FPS. | Respuesta inmediata (< 50ms). Sin saltos de fotogramas detectables al ojo humano. | **Sincronización a nivel de hardware.** Latencia de render < 16.7ms (60 FPS estables) en runtime. |
| **KPI 5.4: TIE** | Botones táctiles de control < 28px. Imposible interactuar en móvil sin hacer clics erróneos. | Zonas táctiles entre 28px y 43px. Gestos táctiles mal configurados (confusión entre scroll y zoom). | Botones y zonas táctiles de 44px o superior, con feedback visual sutil al toque. | Ergonomía táctil completa. Zonas de control amplias y gestos intuitivos (tap, pinch, swipe). | **Ergonomía adaptativa.** Interfaz autocalibrada según el tamaño físico de la pantalla detectada. |
