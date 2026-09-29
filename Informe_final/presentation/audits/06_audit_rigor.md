# Auditoría 6: Rigor Científico, Coherencia de Datos y Soporte Técnico (Academic Rigor)
## Marco de Evaluación, KPIs y Rúbrica de Medición

Esta auditoría evalúa la precisión metodológica del contenido visualizado, la integridad de los gráficos de datos frente a distorsiones perceptivas y el nivel de preparación de los slides de respaldo para la ronda de preguntas. Se basa en los principios de diseño de información cuantitativa de **Edward Tufte** y el marco de **Design Science Research (DSR)** de **Alan Hevner**.

---

### 1. Marco Teórico de Referencia
*   **Tufte, E. R. (1983).** *The Visual Display of Quantitative Information*: Define la "integridad gráfica" indicando que la representación visual de los datos debe ser directamente proporcional a las cantidades numéricas reales (evitando distorsionar el eje Y de gráficos de barras). Promueve altos ratios de "dato-tinta" (eliminar líneas de cuadrícula inútiles o adornos 3D).
*   **Hevner, A. R., et al. (2004).** *Design Science Research in Information Systems*: Establece que un artefacto de ingeniería multimedia debe evaluarse de forma rigurosa mediante metodologías estructuradas (triangulación, SUS, NASA-TLX), y que la comunicación debe declarar de manera transparente los límites de validez del artefacto.

---

### 2. Indicadores Clave de Rendimiento (KPIs)

#### KPI 6.1: Integridad Visual de Gráficos (Data Integrity Ratio - DIR)
*   **Definición:** Grado de ausencia de distorsiones visuales en gráficos e infografías SVG (ej. barras de FPS con base en 0, representación exacta de porcentajes, etiquetado de desviaciones estándar).
*   **Medición:** Contraste entre las longitudes físicas de los elementos gráficos en píxeles y sus valores numéricos reales (factor de distorsión de Tufte = 1.0 ideal).

#### KPI 6.2: Transparencia de Límites Científicos (Scientific Boundary Transparency - SBT)
*   **Definición:** Claridad con la que se declaran las simplificaciones técnicas o heurísticas de la tesis, evitando que el jurado asuma capacidades inexistentes (ej. recalcar que el modo *Thermal* es puramente visual y no calcula transferencia de calor real).
*   **Medición:** Presencia de etiquetas de advertencia, notas aclaratorias explícitas y uso correcto del color naranja en diapositivas críticas.

#### KPI 6.3: Cobertura y Estructura de Respaldo (Backup Coverage & Clarity - BCC)
*   **Definición:** Organización técnica de la sección de Backup (slides B1 a B11) para responder a las 3 dimensiones críticas del jurado: metodológica (diseño AB/BA), de usabilidad (fórmulas SUS/NASA-TLX) y técnica (triángulos, profiler, rendimiento de GPU).
*   **Medición:** Tasa de correspondencia entre posibles preguntas metodológicas del jurado y la existencia de una diapositiva de respaldo específica para responderla visualmente.

#### KPI 6.4: Trazabilidad Objetivos-Conclusiones (Methodological Traceability - MT)
*   **Definición:** Alineación matemática y conceptual entre los objetivos específicos declarados (OE1-OE4), la metodología aplicada (DSR + A/B) y las conclusiones presentadas.
*   **Medición:** Verificación cruzada uno a uno de las metas del proyecto frente a los resultados validados en el slide final.

---

### 3. Rúbrica de Evaluación Objetivo-Realista
*Nota: Un puntaje de "Perfecto (5)" en esta auditoría requiere una integridad matemática de los gráficos y diagramas impecable, junto con diapositivas de respaldo automatizadas que cubran cualquier cuestionamiento empírico imaginable sin dejar brechas de información.*

| KPI / Criterio | Deficiente (1) | Regular (2) | Bueno (3) | Excelente (4) | Perfecto/Ideal (5) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **KPI 6.1: DIR** | Factor de distorsión de Tufte > 1.5 (ej. gráficos de barras con ejes Y recortados artificialmente para exagerar diferencias). | Gráficos confusos. Ejes mal etiquetados o ausencia de leyenda en gráficos complejos (ej. barra de FPS móvil). | Gráficos correctos en su mayoría, pero con ratios dato-tinta bajos (demasiada decoración visual). | Gráficos SVG limpios con factor de distorsión = 1.0. Ratios de dato-tinta perfectos. | **Integridad absoluta.** Gráficos vectoriales generados dinámicamente desde la base de datos de pruebas sin sesgo visual. |
| **KPI 6.2: SBT** | Se presenta el prototipo ocultando los límites (ej. insinuando que el dron lee telemetría en tiempo real o hace simulación FEA activa). | Delimitaciones escritas en texto tan pequeño (`.mini`) que resulta imposible leerlas durante la proyección. | Los límites se mencionan pero no se codifican visualmente, perdiendo impacto ante preguntas del jurado. | Transparencia total. El slide de exclusiones (Slide 05) y la advertencia térmica son explícitas y legibles. | **Rigor de laboratorio.** Los límites del sistema se integran como parte del modelo matemático del prototipo en pantalla. |
| **KPI 6.3: BCC** | Sin sección de backup o compuesta únicamente por capturas genéricas desorganizadas. | Backup con menos de 4 slides. Deja sin responder temas clave como la fórmula de SUS o el orden AB/BA. | Sección de backup amplia pero desorganizada. El ponente tarda más de 10 segundos en encontrar el slide pertinente. | Excelente estructuración. 11 slides de respaldo ordenados con su identificador visual único (B1-B11). | **Indexación interactiva.** Buscador dinámico integrado que despliega el slide de respaldo exacto según la pregunta. |
| **KPI 6.4: MT** | Las conclusiones no responden a los objetivos declarados. Se listan logros genéricos no medidos. | Correspondencia parcial. Se detallan resultados técnicos pero se obvian los resultados de usabilidad o viceversa. | Correspondencia lógica en texto, pero requiere esfuerzo de vinculación mental por parte del jurado. | Tabla de conclusiones (Slide 25) vincula de forma explícita y gráfica cada OE con su resultado y evidencia. | **Verificación formal.** Mapeo matemático donde cada conclusión es un teorema derivado de los objetivos evaluados. |
