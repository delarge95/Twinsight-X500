# Auditoría 1: Maquetación, Rejillas y Composición Visual (Layout & Grids)
## Marco de Evaluación, KPIs y Rúbrica de Medición

Esta auditoría evalúa la precisión técnica de la maquetación, el uso de sistemas de rejillas para ordenar la información y el balance compositivo de la presentación. Se basa en los principios de diseño modular de **Josef Müller-Brockmann** y las **Leyes de la Gestalt** aplicadas a interfaces digitales.

---

### 1. Marco Teórico de Referencia
*   **Müller-Brockmann, J. (1981).** *Grid Systems in Graphic Design*: Establece que la rejilla modular es un organizador intelectual que aporta consistencia, claridad y un orden estético que reduce la fatiga cognitiva del espectador.
*   **Gestalt (Leyes de Proximidad y Región Común):** Indican que los elementos visuales agrupados físicamente son percibidos como relacionados. Esto justifica la separación e integridad espacial de las tarjetas (`.card`) y bloques métricos (`.metric`).

---

### 2. Indicadores Clave de Rendimiento (KPIs)

#### KPI 1.1: Desviación de Alineación Geométrica (Grid Alignment Deviation - GAD)
*   **Definición:** Distancia en píxeles en la que los bordes de elementos teóricamente alineados se desvían de las líneas de la rejilla invisible.
*   **Medición:** Mapeo de coordenadas en el inspector de CSS para elementos horizontales y verticales correlativos.

#### KPI 1.2: Consistencia de la Escala Espacial (Spatial Scale Consistency - SSC)
*   **Definición:** Uso exclusivo de incrementos basados en un sistema de espaciado estricto (múltiplos de 4px u 8px) en paddings, margins y gaps.
*   **Medición:** Análisis estático de las hojas de estilo y estilos en línea (`style="..."`).

#### KPI 1.3: Ratio de Espacio Negativo (Negative Space Ratio - NSR)
*   **Definición:** Porcentaje de superficie del slide que no contiene texto, gráficos o imágenes activos, permitiendo el reposo ocular.
*   **Medición:** Área total libre / área total del viewport (`1920x1080`).

#### KPI 1.4: Equilibrio de Peso Visual (Visual Weight Equilibrium - VWE)
*   **Definición:** Distribución armónica de la masa visual de los componentes en las diapositivas de dos o más columnas (ej. `.split-hero` y `.grid-2`).
*   **Medición:** Desviación del centro de masa visual en comparación con el eje geométrico del slide.

---

### 3. Rúbrica de Evaluación Objetivo-Realista
*Nota: La calificación "Perfecto (5)" exige un rigor matemático absoluto, prácticamente inalcanzable en implementaciones web sin un compilador de diseño de precisión o diseño paramétrico estricto.*

| KPI / Criterio | Deficiente (1) | Regular (2) | Bueno (3) | Excelente (4) | Perfecto/Ideal (5) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **KPI 1.1: GAD** | Desviación > 24px entre elementos adyacentes. Layout desalineado visiblemente. | Desviación de 12px a 24px. Desajustes notables en proyectores. | Desviación de 5px a 11px. Desalineaciones menores no obstructivas. | Desviación < 4px. Alineación limpia percibida al primer vistazo. | **0px de desviación.** Alineación a nivel subpíxel en todo render. |
| **KPI 1.2: SSC** | Uso de valores arbitrarios (ej. 13px, 19px, 3.4%) en más del 50% de las reglas CSS. | Entre 20% y 50% de valores fuera de la escala de 8px (ej. `margin-top: 18px`). | Menos del 20% de desajustes espaciales. La mayoría son múltiplos de 4/8px. | Todo el espaciado es múltiplo estricto de 4/8px (8, 16, 24, 32, 48, etc.). | **Uso paramétrico matemático.** Espaciado derivado de variables CSS unificadas. |
| **KPI 1.3: NSR** | NSR < 10% (saturación de datos) o NSR > 60% (desperdicio excesivo de espacio). | NSR entre 10%-19% o 51%-60%. Lectura tensa o vacía. | NSR entre 20%-25% o 41%-50%. Balance aceptable. | NSR entre 26% y 40%. Densidad perfecta de información por slide. | **NSR dinámico adaptativo.** NSR calculado por slide según nivel de carga cognitiva. |
| **KPI 1.4: VWE** | Asimetría descompensada. Elementos pesados arrastran la mirada fuera de la pantalla. | Desviación del centro visual > 15% del eje geométrico sin intención comunicativa. | Desviación del centro visual entre 6% y 15%. Desequilibrio sutil. | Desviación < 5%. Equilibrio compositivo sólido y natural. | **Equilibrio áureo.** Centro de masa visual coincide exactamente con la espiral áurea. |
