# Demo Script - TwinSight X500
## Guion para demostracion en vivo coherente con la app real

---

## Estado del guion

Este guion reemplaza versiones anteriores que mencionaban herramientas no publicadas en la UI final, como medicion, BOM, conexiones o catalogo legacy. La demo debe mostrar solo el flujo real validado: Hero, Explore, seleccion, bottom sheet, Inspect, Analyze, Studio y fasteners cuando esten visibles y trazados en la build.

Solo se deben afirmar metricas que coincidan con el capitulo 5, las tablas de validacion o el profiler actual. Cualquier reduccion de peso, tiempo de carga o dato no trazable queda como pendiente/futuro.

Este documento se ensaya junto con `PRESENTATION_SCRIPT.md`. La evidencia por slide y las respuestas ante preguntas de jurado se controlan desde `DEFENSE_EVIDENCE_MAP.md` y `JURY_QA_BANK.md`.

---

## Rol de este guion (actualizado 2026-09-29)

- **Durante la exposición (slide 19)** la demo es **en vivo** y dura 3:00: primero la **landing pública** (WebGL puro: ensamblaje con el cursor, seis capítulos de scrollytelling y mini app, ~1:10) y luego, desde su botón «Abrir visor», la **app Unity** con la ruta corta de abajo (~1:40). Narración en `PRESENTATION_SCRIPT.md`.
- **Respaldo:** si la build no responde en 10 s o se congela, volver al deck y hacer clic sobre el video `assets/video/vid_01_demo_compilado.mp4` (88 s, mismo recorrido), que espera en pausa en 0:00.
- **Este guion de 5 minutos** es la demo extendida para la ronda de preguntas si el jurado pide profundizar.

### Ruta corta de la demo en vivo (slide 19)

Preparar antes: landing abierta en otra ventana, al inicio de la sección 03 con el dron sin ensamblar; abrir una vez «Abrir visor» para cachear la build y cerrarla. Tramo de la app, en este orden:

1. Seleccionar el soporte de riel y batería → `Isolate` → abrir la ficha inferior.
2. `Inspect` → control de energía (Starting · Idle · Flying).
3. `Analyze` → Explode → Cut.
4. `Studio` → X-Ray → Thermal → Solid.

---

## Preparacion pre-demo

### Checklist tecnico

- [ ] Abrir la build WebGL o Play Mode con `MainScene_Final`.
- [ ] Restablecer vista inicial del dron completo.
- [ ] Verificar que no haya pieza seleccionada al iniciar.
- [ ] Verificar que el modo base sea `Realistic`.
- [ ] Tener capturas/video de respaldo por si falla la demo en vivo.
- [ ] Confirmar que los controles de zoom, pan y orbit responden sin perder piezas pequenas.

### Estado inicial recomendado

- Vista: `Realistic`.
- Dron completo visible.
- Explode desactivado.
- Cross-section desactivado.
- Ningun filtro activo.
- Ninguna pieza seleccionada.

---

## Guion de demostracion

### [0:00-0:30] Apertura

**Accion:** Mostrar el prototipo en estado inicial.

> "Este es el visor WebGL del Holybro X500 V2. La idea no es solo mostrar un modelo 3D, sino convertir el ensamblaje en una experiencia inspeccionable, filtrable y explicable desde navegador."

**Accion:** Orbitar suavemente el dron.

> "La escena conserva lectura espacial completa y permite explorar el sistema desde cualquier angulo."

---

### [0:30-1:05] Navegacion

**Accion:** Demostrar orbit, zoom y pan.

| Accion | Control |
|--------|---------|
| Orbit | Arrastrar en viewport |
| Zoom | Scroll |
| Pan | Arrastre de paneo configurado |

> "La camara ajusta sensibilidad y rango segun la escala de lo que se analiza. No es lo mismo navegar el dron completo que acercarse a un fastener."

---

### [1:05-1:55] Seleccion y ficha inferior

**Accion:** Seleccionar una pieza madre clara, por ejemplo placa, brazo o motor.

> "Al seleccionar una pieza, la app abre una ficha inferior. Esta ficha no es decorativa: traduce una malla seleccionada a informacion tecnica."

**Accion:** Senalar nombre, categoria, especificaciones y ensamblaje.

> "La seleccion puede representar una pieza madre, una subpieza, un grupo de hotspot o un fastener. La app diferencia esos niveles para que aislamiento, resaltado y datos no se contradigan."

---

### [1:55-2:40] Inspect, aislamiento y fasteners

**Accion:** Usar `Inspect` y aislar una pieza madre con fasteners asociados.

> "Inspect permite limpiar el contexto visual. Si se aisla una pieza madre, se conservan los fasteners reconciliados con esa pieza cuando existe asignacion confiable."

**Accion:** Seleccionar o aislar un fastener individual.

> "Un fastener tambien puede aislarse como unidad completa. Cuando se requiere detalle, el sistema reemplaza el proxy por una representacion modular bajo demanda, sin convertir toda la tornilleria de la escena."

### [2:40-3:35] Analyze

**Accion:** Activar vista explosionada y mover el slider.

> "Analyze permite separar visualmente el ensamblaje para leer relaciones entre piezas sin destruir la malla ni perder metadatos."

**Accion:** Activar cross-section.

> "El corte transversal funciona como una decision de render: el shader decide que fragmentos se dibujan segun un plano matematico."

**Accion:** Mostrar filtros por categoria.

> "Los filtros reducen ruido visual por sistemas funcionales: estructura, propulsion, avionica, comunicaciones, distribucion de energia y fasteners."

---

### [3:35-4:30] Studio y modos visuales visibles

**Accion:** Mostrar `X-Ray`, `Solid Color` y `Thermal`.

> "Studio controla la lectura visual. En la UI final se exponen modos como X-Ray, Solid Color y Thermal sobre una base Realistic."

**Accion:** Cambiar entorno o preset si aplica.

> "Ademas de las tarjetas de modo, Studio publica presets: Studio, Studio Light y Blueprint, que convierte la escena en una lectura de planos y siluetas."

**Accion:** Mostrar Thermal solo como lectura heuristica.

> "Thermal no es FEA ni una medicion fisica calibrada. Es un modelo reducido por componentes, alimentado por el control de energia; la leyenda en grados es la escala de ese modelo heuristico, no una medicion."

---

### [4:30-5:00] Cierre

**Accion:** Volver a dron completo.

> "El resultado defendible es una cadena completa: CAD y Blender para preparar geometria, Unity para interaccion y WebGL para acceso. Las metricas cerradas se citan desde el capitulo 5: SUS, NASA-TLX Raw y rendimiento por dispositivo; lo no medido no se improvisa."

> "La contribucion no es mostrar mas piezas; es hacer legibles sus relaciones."

---

## Backup si falla la demo

| Riesgo | Respuesta |
|--------|-----------|
| Carga lenta | Volver a la slide 19 y reproducir el video de respaldo (clic sobre él). |
| WebGL falla | Volver a la slide 19 y reproducir el video de respaldo. |
| FPS inestable | No improvisar resultados; citar solo la tabla de rendimiento del capitulo 5/anexos y explicar que el dispositivo de la demo puede variar. |
| Fastener ambiguo | Mostrar que el sistema lo reporta para revision y no lo asigna por suposicion. |

---

## Frases seguras

- "La app distingue entre pieza madre, subpieza, hotspot y fastener."
- "Blueprint se publica como preset de Studio; Wireframe y Ghosted estan implementados pero ocultos, y no se prometen como alcance visible."
- "Thermal es heuristico, no FEA."
- "Los fasteners se muestran solo cuando su asignacion visible esta trazada; si hay ambiguedad, se reporta como limite o revision."
- "Las metricas se citan solo si estan en el capitulo 5, anexos de validacion o profiler actual."

---

*Demo Version: 2.2*
*Last Updated: 2026-09-29*
*Project: TwinSight X500*
