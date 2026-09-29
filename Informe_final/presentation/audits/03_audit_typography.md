# Auditoría 3: Tipografía, Jerarquía y Legibilidad (Typography)
## Marco de Evaluación, KPIs y Rúbrica de Medición

Esta auditoría evalúa la efectividad de la tipografía seleccionada para comunicar información técnica de forma inmediata. Analiza la consistencia tipográfica, el tamaño, la longitud de las líneas y la carga textual general para evitar la fatiga visual. Se basa en las teorías clásicas y modernas de **Robert Bringhurst** y **Ellen Lupton**.

---

### 1. Marco Teórico de Referencia
*   **Bringhurst, R. (1992).** *The Elements of Typographic Style*: Describe la tipografía como el arte de dotar de una estructura visual al lenguaje humano. Plantea que la proporción tipográfica (relación entre tamaño e interlineado) es esencial para la fluidez de la lectura y la comodidad visual.
*   **Lupton, E. (2004).** *Thinking with Type*: Analiza la relación de contraste formal entre familias tipográficas. En presentaciones académicas con gran densidad de datos, la tipografía monoespaciada (*JetBrains Mono*) debe reservarse únicamente para metadatos y código, mientras que los textos continuos deben usar sans-serif legibles (*Inter*).

---

### 2. Indicadores Clave de Rendimiento (KPIs)

#### KPI 3.1: Consistencia de la Escala Tipográfica (Type Scale Adherence - TSA)
*   **Definición:** Grado de adherencia a los siete (7) niveles de escala definidos en la CSS del proyecto (64px, 44px, 32px, 22px, 20px, 16px, 14px).
*   **Medición:** Escaneo de elementos tipográficos en el DOM de Reveal.js para detectar valores arbitrarios (ej. `font-size: 18px` o `font-size: 12px` declarados en línea).

#### KPI 3.2: Densidad Textual por Diapositiva (Slide Word Budget - SWB)
*   **Definición:** Conteo de palabras visibles por diapositiva (excluyendo notas internas de orador `aside.notes`). El exceso de palabras aumenta la fatiga cognitiva del público.
*   **Medición:** Número total de palabras visibles en el canvas del slide (límite recomendado <= 45 palabras por slide estándar).

#### KPI 3.3: Ajuste de Longitud de Línea y Leading (Line Length & Leading - LLL)
*   **Definición:** Mediana de caracteres por línea de texto (CPL) y relación de interlineado (`line-height`).
*   **Medición:** Conteo manual/automatizado de caracteres en líneas completas de tarjetas de texto continuo y evaluación del interlineado (rango óptimo: 45-75 CPL, `line-height` de 1.4 a 1.6).

#### KPI 3.4: Consistencia en el Rol de Fuentes (Font Role Consistency - FRC)
*   **Definición:** Cumplimiento estricto en el uso de las tres fuentes tipográficas instaladas según su rol semántico predefinido.
*   **Medición:** Porcentaje de aciertos donde los encabezados usan *Space Grotesk*, el texto descriptivo usa *Inter*, y el código/fichas técnicas usan *JetBrains Mono*.

---

### 3. Rúbrica de Evaluación Objetivo-Realista
*Nota: La calificación "Perfecto (5)" exige que toda la tipografía sea fluida, respete una rejilla de línea base matemática invisible y no posea ninguna desviación de jerarquía en toda la presentación.*

| KPI / Criterio | Deficiente (1) | Regular (2) | Bueno (3) | Excelente (4) | Perfecto/Ideal (5) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **KPI 3.1: TSA** | Más de 5 tamaños tipográficos no estandarizados en la misma pantalla. Jerarquía confusa. | Presencia de 3 a 4 tamaños tipográficos arbitrarios. Títulos y subtítulos no diferenciados. | Adherencia a la escala tipográfica con desviaciones aisladas (ej. tarjetas con tamaños ad-hoc). | Adherencia estricta del 100% en CSS a los tamaños definidos. Jerarquía obvia al instante. | **Escala proporcional modular.** Tamaños y espaciados derivados de una escala matemática áurea en CSS. |
| **KPI 3.2: SWB** | Diapositivas con > 80 palabras de lectura continua. Aspecto de "muro de texto" insoportable. | Slides promedio con 55 a 80 palabras. Obliga al público a leer en lugar de escuchar. | Slides promedio con 41 a 54 palabras. Densidad aceptable pero requiere síntesis. | Todo slide posee <= 40 palabras visibles. Contenido conciso y directo a la idea clave. | **Máximo rigor sintético.** Menos de 20 palabras por slide, delegando el detalle a gráficos y notas. |
| **KPI 3.3: LLL** | Líneas de texto > 95 caracteres o < 30 caracteres. Interlineado apretado (`line-height` < 1.2). | Líneas desproporcionadas. Interlineado de 1.2 a 1.35. Fatiga de lectura en saltos de línea. | Rango de 35-44 CPL o 76-90 CPL. Interlineado aceptable (1.4). | Rango perfecto (45-75 CPL) e interlineado cómodo (1.5 - 1.55) en todo bloque de texto. | **Ajuste óptimo dinámico.** El número de caracteres se adapta perfectamente a la distancia de lectura estimada. |
| **KPI 3.4: FRC** | Las tipografías se mezclan de forma aleatoria (ej. párrafos enteros en *JetBrains Mono* sin razón técnica). | Confusión de roles en el 30% del contenido (ej. títulos en sans-serif e indicadores en mono). | Roles respetados en su mayoría. Desviaciones leves en tarjetas de datos técnicos. | Consistencia del 100%. Cada fuente tipográfica comunica visualmente el tipo de dato que contiene. | **Sistema tipográfico paramétrico.** Clases tipográficas CSS inmutables con control sintáctico estricto. |
