# Auditoría 7: Lenguaje Natural, Registro Académico y Detección de AI Slop
## Marco de Evaluación, KPIs y Rúbrica de Medición

Esta auditoría evalúa la calidad lingüística de los textos visibles en la presentación (títulos, tarjetas, etiquetas, tablas y notas), verificando que el registro sea académico pero natural, libre de marcadores típicos de generación automática (AI slop), y que cada afirmación esté calibrada para su audiencia real: un jurado evaluador de tesis de grado en Ingeniería Multimedia.

---

### 1. Marco Teórico de Referencia
*   **Swales, J. M. (1990).** *Genre Analysis: English in Academic and Research Settings*: Establece las convenciones del registro académico escrito, distinguiendo entre afirmaciones hedged (matizadas con cautela epistémica: "sugiere", "se observó") y afirmaciones categóricas ("demuestra", "prueba"), y cuándo es correcto usar cada una según la fuerza de la evidencia disponible.
*   **Hyland, K. (2005).** *Metadiscourse: Exploring Interaction in Writing*: Define los marcadores metadiscursivos (hedges, boosters, attitude markers, engagement markers) que distinguen la escritura humana experta de la generación automática formulaica. La escritura humana varía la intensidad retórica; la generación automática tiende a uniformar el tono.
*   **Clark, I. & Gruba, P. (2010).** *The Use of Language in Academic Communication*: Diferencia entre lenguaje de uso interno (notas del autor, instrucciones de proceso, metainformación de diseño) y lenguaje de presentación pública (afirmaciones sustentables ante un jurado, datos verificables, conclusiones defendibles).
*   **Weber-Wulff, D. et al. (2023).** *Testing of Detection Tools for AI-Generated Text*: Identifica patrones lingüísticos recurrentes en texto generado por modelos de lenguaje: sobrecualificación adjetival, simetrías sintácticas artificiales, y uso excesivo de conectores de transición ("cabe destacar", "es importante señalar", "sin lugar a dudas").

---

### 2. Indicadores Clave de Rendimiento (KPIs)

#### KPI 7.1: Naturalidad del Registro (Natural Register Index - NRI)
*   **Definición:** Grado en que los textos visibles suenan como lenguaje escrito por un ingeniero multimedia que domina su proyecto, y no como output de un modelo generativo. Se evalúa la ausencia de marcadores de AI slop: adjetivación excesiva ("impecable", "robusto", "exhaustivo"), simetrías sintácticas forzadas (tres frases paralelas con la misma estructura), muletillas de transición vacías ("cabe destacar que", "es importante mencionar"), y sobrepromesas no respaldadas por los datos.
*   **Medición:** Conteo de instancias de marcadores AI slop por cada 100 palabras visibles. Ideal: < 1 instancia por cada 100 palabras.

#### KPI 7.2: Calibración Epistémica (Epistemic Calibration - EPC)
*   **Definición:** Correspondencia entre la fuerza de las afirmaciones y la fuerza de la evidencia que las respalda. Una muestra de n=12 con diseño intra-sujeto no permite afirmar "superioridad demostrada" ni "diferencia estadísticamente significativa" (no se hicieron pruebas inferenciales), pero sí permite afirmar "patrón descriptivo consistente", "tendencia favorable observada" o "diferencia pareada de X puntos".
*   **Medición:** Clasificación de cada afirmación cuantitativa visible como correctamente hedged (matizada), sobrecalificada (overclaimed), o subcalificada (underclaimed). Ratio ideal: 100% correctamente hedged.

#### KPI 7.3: Filtrado de Lenguaje Interno vs. Público (Internal vs. Public Language Filter - IPF)
*   **Definición:** Verificación de que ningún texto visible en la presentación contenga lenguaje de uso interno (instrucciones de proceso, metainformación de diseño, comentarios de autor) que no debería exponerse ante el jurado. Ejemplos de filtraciones: "Este slide demuestra X" (el slide no demuestra nada, el proyecto lo hace), "Se adjunta evidencia de Y" (la evidencia no se adjunta, se presenta), "A continuación se detalla" (lenguaje de informe escrito, no de presentación visual).
*   **Medición:** Conteo de filtraciones de registro interno. Ideal: 0 filtraciones.

#### KPI 7.4: Consistencia del Tono Comunicativo (Tonal Consistency Index - TCI)
*   **Definición:** Uniformidad del registro a lo largo de todo el deck. El tono debe ser técnico-descriptivo con cautela epistémica, sin saltar entre un tono hiperbólico ("revolucionario", "innovador sin precedentes") y uno excesivamente tímido ("se intentó", "modesta contribución"). El registro objetivo es: afirmativo, preciso, factual, y matizado donde corresponda.
*   **Medición:** Desviación estándar del nivel de formalidad entre diapositivas. Se evalúa si hay slides que rompan el tono general del deck (ej. un slide hiperbólico rodeado de slides cautelosos, o viceversa).

---

### 3. Rúbrica de Evaluación Objetivo-Realista
*Nota: Un puntaje de "Perfecto (5)" en esta auditoría requiere que cada palabra visible en el deck esté calibrada para ser defendida oralmente ante un jurado académico sin que el ponente deba matizar, corregir o justificar lo que está escrito en pantalla.*

| KPI / Criterio | Deficiente (1) | Regular (2) | Bueno (3) | Excelente (4) | Perfecto/Ideal (5) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **KPI 7.1: NRI** | Más de 5 marcadores AI slop por cada 100 palabras. El deck suena a texto generado automáticamente sin edición humana. | 3-5 marcadores por cada 100 palabras. Adjetivos grandilocuentes y simetrías artificiales visibles en slides clave. | 2-3 marcadores por cada 100 palabras. Algunos resabios de generación automática pero el tono general es aceptable. | < 1 marcador por cada 100 palabras. El texto suena a escritura humana experta con vocabulario técnico propio del dominio. | **0 marcadores.** Cada frase suena como si la hubiera escrito el ingeniero que construyó el proyecto, no una herramienta generativa. |
| **KPI 7.2: EPC** | Más de 3 afirmaciones sobrecalificadas (ej. "demuestra superioridad", "prueba que" con n=12). | 2-3 overclaims visibles. Se usa lenguaje inferencial sin pruebas inferenciales. | 1 overclaim o 1 underclaim visible. La mayoría de afirmaciones están bien calibradas. | 0 overclaims. Todas las afirmaciones cuantitativas están correctamente matizadas con hedges epistémicos adecuados. | **Calibración perfecta.** Cada afirmación cuantitativa usa exactamente el grado de certeza que la evidencia permite, sin sobreprometer ni minimizar. |
| **KPI 7.3: IPF** | Más de 3 filtraciones de lenguaje interno visible (ej. "Este slide demuestra", "Se adjunta", instrucciones de diseño en pantalla). | 2-3 filtraciones. Aparecen notas de proceso o instrucciones meta-comunicativas en tarjetas o etiquetas. | 1 filtración visible. Un texto menor de uso interno que pasó al canvas público. | 0 filtraciones. Todo el texto visible es lenguaje de presentación pública apropiado para un jurado. | **Inmaculado.** No existe una sola palabra visible que el ponente no pueda defender o pronunciar literalmente ante el jurado sin contexto adicional. |
| **KPI 7.4: TCI** | Tono errático. Slides con hipérboles ("revolucionario") conviven con slides excesivamente tímidos ("se intentó"). | Inconsistencia en 3+ slides. El deck oscila entre registros formales e informales sin patrón. | Consistencia general con 1-2 desviaciones tonales menores. | Tono uniforme en todo el deck: técnico, preciso y factual con matices epistémicos donde corresponda. | **Registro unitario.** El deck completo lee como si hubiera sido escrito por una sola voz experta con intención comunicativa coherente de principio a fin. |
