# Paquete definitivo de sustentación — TwinSight X500

**Trabajo de grado:** TwinSight X500 — prototipo web 3D para visualización técnica de hardware complejo (Ingeniería Multimedia, UNAD).
**Fecha de consolidación:** 2026-09-29.
**Fuente autoritativa:** `Informe_final/awoodcocks.pdf` (informe final) y sus anexos del repositorio.

---

## Qué contiene esta carpeta

1. **La presentación** (`index_final.html`): 30 slides principales + 11 de respaldo (B1–B11), con visor 3D, videos y diagramas.
2. **Los materiales orales**, sincronizados entre sí y con el deck: guion, tarjetas, estructura y guion de demo.
3. **El material de defensa**: banco de preguntas, guía de estudio, atlas bibliográfico y mapa de evidencias.

---

## Cómo abrir la presentación

**Abrir `index_final.html` directamente** (doble clic) en Chrome o Edge. No hace falta servidor: el modelo 3D se carga desde `assets/model/drone_glb_datauri.js`, que funciona con `file://`.

- Requiere internet para las tipografías (Google Fonts) y para la demo en vivo en la build pública.
- Zoom del navegador al 100 % (`Ctrl+0`). El deck amplía el contenido según la resolución: a 1920×1080 el texto crece ~25 %; en 1366×768 o 1280×720 se ajusta para no desbordar.
- El visor 3D se precarga en segundo plano al abrir el deck, y los videos solo se reproducen al llegar a su slide.

### Controles

| Tecla | Acción |
|---|---|
| `→`, `Espacio`, `Av Pág`, clic | Revelar el siguiente paso o avanzar de slide |
| `←`, `Re Pág` | Ocultar el último paso o retroceder |
| `Inicio` / `Fin` | Ir a la portada / al cierre (slide 30) |
| `F` | Pantalla completa |
| `N` | Notas del presentador (tiempo y clics por slide). **Solo para ensayar**: se ven en la misma pantalla que el jurado. |
| Contador superior derecho | Escribir un número (`12`) o un backup (`B3`) y pulsar `Enter` para saltar |

La numeración visible de cada slide (01–30) coincide con la de todos los documentos del paquete.

---

## Mapa de archivos

### Para exponer y ensayar

| Archivo | Función |
|---|---|
| `index_final.html` | Deck oficial. |
| `SPEAKER_CARDS.md` | Una tarjeta por slide: tiempo, clics, tesis oral, evidencia en pantalla y riesgo a evitar. Prioridad si el tiempo aprieta. |
| `PRESENTATION_SCRIPT.md` | Guion oral completo (28:30): un `[click n]` por cada paso del deck, transiciones, frases a evitar, checklist técnico, preguntas frecuentes y cortes de emergencia. |
| `PRESENTATION_OUTLINE.md` | Estructura de las 30 slides, bloques de tiempo, backups e inventario de assets. |
| `DEMO_SCRIPT.md` | Ruta de la demo en vivo (slide 19), respaldo en video y demo extendida para preguntas. |

### Para responder al jurado

| Archivo | Función |
|---|---|
| `JURY_QA_BANK.md` | Banco de preguntas con respuestas cortas y ampliadas. |
| `DEFENSE_STUDY_GUIDE.md` | Guía de estudio por bloques temáticos. |
| `BIBLIOGRAPHY_EVIDENCE_ATLAS.md` | Citas verificadas y límites de uso de cada fuente. |
| `DEFENSE_EVIDENCE_MAP.md` | Trazabilidad slide → tabla o figura del informe, cifras autorizadas y afirmaciones prohibidas. |

### Soporte

| Archivo / carpeta | Función |
|---|---|
| `assets/` | Imágenes (`img/`), modelo GLB y su versión embebida (`model/`), three.js (`vendor/`) y videos a 720p (`video/`). |
| `ASSETS_REQUIREMENTS.md` | Inventario final de assets y su origen. |
| `audits/` | Rúbricas de auditoría (layout, color, tipografía, narrativa, interacción, rigor, lenguaje, imágenes). |
| `rag_thesis_context.md` | Resumen técnico para consulta rápida. |

---

## Reglas de oro

1. **Alcance:** TwinSight X500 es un *visual product twin*. No es un gemelo digital operacional, no recibe telemetría ni ejecuta FEA.
2. **Thermal:** visualización heurística relativa por componentes. La leyenda en °C es la escala del modelo, no una medición.
3. **NASA-TLX Raw:** mide carga de trabajo percibida, no las tres cargas de Sweller.
4. **SUS:** solo del prototipo 3D; no compara 3D con 2D.
5. **Muestra:** n = 12, no probabilística; lectura formativa y descriptiva, sin inferencia poblacional.
6. **Demo:** en vivo en la build pública. Si no responde en 10 s, volver a la slide 19 y reproducir el video de respaldo sin comentar el fallo.
