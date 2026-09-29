# Auditoría 2: Teoría del Color, Contraste y Accesibilidad (Color & Contrast)
## Marco de Evaluación, KPIs y Rúbrica de Medición

Esta auditoría evalúa el uso de la paleta cromática, el contraste lumínico sobre fondos degradados oscuros y el cumplimiento de pautas de accesibilidad para la proyección digital. Se fundamenta en las teorías de **Josef Albers** y los estándares internacionales **WCAG 2.1**.

---

### 1. Marco Teórico de Referencia
*   **Albers, J. (1963).** *Interaction of Color*: Explica que el color es el medio más relativo en el arte y el diseño. Un mismo acento de color puede verse modificado por el fondo. En pantallas oscuras (`#0A0E17`), los acentos intensos adquieren mayor vibración óptica, lo que puede provocar fatiga si no se controlan sus bordes y luminancia.
*   **Johannes Itten (Teoría del Contraste de Color):** Específicamente el contraste de claro-oscuro y el contraste cualitativo (saturación).
*   **Estándar WCAG 2.1 (W3C Web Accessibility Guidelines):** Define las reglas matemáticas de contraste de color requeridas para personas con baja visión o proyección bajo iluminación desfavorable.

---

### 2. Indicadores Clave de Rendimiento (KPIs)

#### KPI 2.1: Consistencia Cromática Semántica (Semantic Color Consistency - SCC)
*   **Definición:** Porcentaje de elementos visuales cuyos colores coinciden exactamente con la función cognitiva asignada en el sistema (ej. azul para estructura base, verde para resultados favorables, naranja para límites/alertas).
*   **Medición:** Conteo de clases semánticas CSS aplicadas frente a su significado en el slide.

#### KPI 2.2: Ratio de Contraste Lumínico (Luminance Contrast Ratio - LCR)
*   **Definición:** Ratio de contraste matemático entre el color de primer plano (texto/iconos) y el fondo degradado en las peores condiciones del slide.
*   **Medición:** Cálculo matemático de luminancia relativa: `(L1 + 0.05) / (L2 + 0.05)` en los puntos más claros del fondo.

#### KPI 2.3: Independencia de Información del Color (Accessibility Color Independence - ACI)
*   **Definición:** Grado en que la información crítica no depende exclusivamente de la percepción del color (ej. uso concomitante de símbolos como ✓/✗, etiquetas textuales claras o variaciones de línea).
*   **Medición:** Simulación del slide en escala de grises y dicromías (deuteranopía/protanopía) analizando la legibilidad de la información.

#### KPI 2.4: Balance Cromático de Interfaz (Interface Color Balance - ICB)
*   **Definición:** Distribución porcentual de los colores en pantalla para mantener el confort ocular. Se adapta la regla clásica de diseño `60-30-10` a la relación `80-15-5` (80% Fondo oscuro, 15% Texto e iconografía muted, 5% Acentos de color).
*   **Medición:** Análisis de píxeles por espectro de color en capturas representativas del slide.

---

### 3. Rúbrica de Evaluación Objetivo-Realista
*Nota: La calificación "Perfecto (5)" exige que todo el contenido cumpla con el nivel AAA de WCAG de manera demostrada bajo herramientas automatizadas en condiciones severas.*

| KPI / Criterio | Deficiente (1) | Regular (2) | Bueno (3) | Excelente (4) | Perfecto/Ideal (5) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **KPI 2.1: SCC** | Los colores cambian de significado de slide a slide sin lógica clara (ej. verde usado para problemas). | Desajustes de color semántico en más del 25% de los slides. Confusión de roles. | Desajustes de color menores (ej. uso ocasional de naranja fuera de advertencias). | Consistencia del 100%. Cada color cumple un rol semántico predecible. | **Consistencia unificada.** Reglas lógicas de color inmutables en código y gráficos. |
| **KPI 2.2: LCR** | LCR < 3:1 para texto normal. Textos que se confunden con el degradado del fondo. | LCR entre 3:1 y 4.4:1 para texto normal. Requiere esfuerzo de lectura. | LCR > 4.5:1 para texto normal (Nivel AA) pero falla en elementos de visualización SVG. | LCR > 7:1 para texto normal y > 4.5:1 para texto grande (Nivel AAA completo). | **Contraste adaptativo dinámico.** LCR superior a 10:1 en toda circunstancia visual. |
| **KPI 2.3: ACI** | La diferenciación de datos clave depende únicamente del color (ej. barras de gráfico diferenciadas solo por tono). | Faltan etiquetas secundarias en más del 30% de elementos coloreados. | Uso de redundancia (iconos + color) en la mayoría de slides, con omisiones leves. | Redundancia total (iconos, etiquetas de texto e indicadores de forma) en todo el deck. | **Legibilidad universal.** Estructura semántica descrita de forma no visual completa. |
| **KPI 2.4: ICB** | El color de acento satura la pantalla (> 20% del área). Provoca destello visual e incomodidad. | Acentos cromáticos representan entre 11% y 20%. Fatiga ocular media en sala oscura. | Distribución aceptable. Acentos representan entre 6% y 10%. | Distribución óptima. Acentos estables en el rango del 3% al 5%. | **Composición exacta 80-15-5.** Relación de masa visual armónica al píxel. |
