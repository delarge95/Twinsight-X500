# Paquete Definitivo de Sustentación — TwinSight X500
**Tesis de Grado: Visual Product Twin WebGL para Inspección Técnica del Dron X500**  
**Fecha de consolidación:** Septiembre 2026  
**Estado:** Canónico, autocontenido y verificado para defensa académica.

---

## 🧭 ¿Qué es esta carpeta?

Esta carpeta reúne de forma **unificada, autocontenida y definitiva** todos los artefactos necesarios para la sustentación oral ante el jurado:
1. **La presentación web interactiva final** (`index_final.html`) con sus modelos 3D, texturas, videos y diagramas.
2. **El guión maestro oral** y las **tarjetas de ensayo cronometrado** más actualizadas (01 de julio de 2026).
3. **El arsenal completo de defensa técnica**: banco de preguntas de jurado, guía profunda de estudio, atlas bibliográfico con páginas de citas, mapa de trazabilidad y guion de demo en vivo.

---

## 🚀 Cómo Ejecutar la Presentación Web (`index_final.html`)

Para que los modelos 3D (`assets/model/x500v2_runtime_low.glb`), texturas PBR y videos carguen sin restricciones de CORS del navegador, se recomienda ejecutar un servidor web local:

### Opción 1: Python (Recomendado y sin dependencias adicionales)
Abre una terminal en esta carpeta y ejecuta:
```bash
python -m http.server 8000
```
Luego abre en Chrome, Edge o Firefox:  
👉 **`http://localhost:8000/index_final.html`**

### Opción 2: Node.js / npx
```bash
npx serve .
```

### Controles de Navegación del Deck
- **Avanzar slide:** Flecha Derecha (`→`), Espacio o Clic en botón siguiente.
- **Retroceder slide:** Flecha Izquierda (`←`) o Clic en botón anterior.
- **Estructura:** 41 slides en total:
  - **Slides 00 a 29:** Ruta principal de exposición (28:30 min de exposición real + 1:30 min de margen).
  - **Slides 30 a 40 (B1 a B11):** Slides de respaldo (Backups) para responder preguntas específicas del jurado durante la ronda de preguntas.

---

## 📚 Mapa de Archivos y Cómo Usarlos

### 1. Para Exponer y Ensayar
| Archivo | Función | Cómo usarlo |
|---|---|---|
| **`index_final.html`** | Deck interactivo oficial de 41 slides con 3D integrado. | Proyectar durante la sustentación. |
| **`SPEAKER_CARDS.md`** | **34 tarjetas de ensayo cronometrado** (actualizadas al 01-Jul-2026). | Practicar la exposición con reloj. Memorizar la **tesis oral** y vigilar la columna **riesgo crítico a evitar**. |
| **`PRESENTATION_SCRIPT.md`** | **Guión maestro oral completo** (50.5 KB, 01-Jul-2026). | Lectura profunda previa para asimilar transiciones, zonas de riesgo y ritmo verbal. |
| **`PRESENTATION_OUTLINE.md`** | Estructura temática, blueprint de 28 slides + 11 backups y requerimientos visuales. | Consultar la visión global de la defensa. |
| **`DEMO_SCRIPT.md`** | Guion técnico de 2 minutos para la demostración en vivo (Slide 17/18). | Seguir la secuencia exacta: Explorar → Seleccionar → Bottom Sheet → Inspect → Analyze → Regreso. Tener videos de respaldo listos. |

### 2. Para Responder las Preguntas del Jurado (Q&A)
| Archivo | Función | Cómo usarlo |
|---|---|---|
| **`JURY_QA_BANK.md`** | **Banco maestro de preguntas y respuestas blindadas** (29 KB). | Estudiar antes de la sustentación. Cubre por qué $n=12$, por qué SUS no compara 3D vs 2D, NASA-TLX Raw vs carga cognitiva de Sweller, T4 no cronometrada, discrepancia 95k vs 229k triángulos y Thermal heurístico. |
| **`DEFENSE_STUDY_GUIDE.md`** | **Guía de estudio exhaustiva** (14 secciones temáticas). | Reforzar fundamentos sobre metodología DSR (Peffers), pipeline CAD, shaders URP, arquitectura por capas y usabilidad. |
| **`BIBLIOGRAPHY_EVIDENCE_ATLAS.md`** | **Atlas de citas textuales y anclaje en PDF** (9.6 KB). | Responder a la pregunta "¿De dónde sale esto?" citando autor, año, página exacta de PDF y frase textual (Sweller, Mayer, Brooke, Hart & Staveland, Norman, Khronos). |
| **`DEFENSE_EVIDENCE_MAP.md`** | **Mapa de trazabilidad contra `informe_final.pdf`**. | Conecta cada número de slide con la página, tabla o figura correspondiente en el documento escrito de la tesis. |

### 3. Especificaciones Visuales y Auditorías
| Archivo / Carpeta | Función |
|---|---|
| **`DESIGN_SYSTEM.md`** | Guía de tokens de diseño (`--bg: #07080A`, `--lime: #C8F53F`, tipografías Clash Display, Space Grotesk, JetBrains Mono). |
| **`assets/`** | Carpeta autocontenida con imágenes (`img/`), modelo 3D GLB (`model/`), texturas PBR (`textures/`), librerías Three.js (`vendor/`) y videos/animaciones de apoyo (`video/`). |
| **`audits/`** | Informes de auditoría de contraste, color, tipografía, narrativa, layout y rigor visual aplicados al deck. |
| **`rag_thesis_context.md`** | Resumen ejecutivo del contexto técnico para consulta rápida o agentes de IA. |

---

## ⚠️ Reglas de Oro para la Sustentación

1. **Definición de alcance:** TwinSight X500 es un **Visual Product Twin** para comprensión técnica e inspección web. **NO** es un gemelo digital operacional, no recibe telemetría en tiempo real ni ejecuta análisis de elementos finitos (FEA).
2. **Modo Thermal:** Es una **visualización heurística relativa** para comunicación e inspección visual, no una simulación multifísica.
3. **NASA-TLX Raw:** Mide **carga de trabajo percibida** (*workload*), no mide directamente las tres cargas cognitivas teóricas de Sweller (intrínseca, extrínseca, germana).
4. **Muestra:** $n=12$ es una muestra no probabilística de conveniencia, apropiada para validación formativo-descriptiva en *Design Science Research* (DSR), no para inferencia poblacional estadística.
5. **Plan de contingencia:** Si la demo interactiva en vivo presenta algún titubeo o lentitud por la red o GPU de la sala, pasar de inmediato a los videos y capturas de respaldo sin interrumpir el ritmo de la exposición.
