# PLAN_UX_ESCRITORIO — seguir en Unity de escritorio el rediseño UX de la web

2026-11-20, rama `pc-ux-1` (desde `pc-desktop` e5868d0), solo PC. **Solo estudio: no se implementó nada.**
Referencia web (solo lectura): `E:\twinsight-web-twin`, rama `web/telemetria-control` 8326c44 — `docs/UX_INVENTARIO_RUIDO.md`, `src/shared/{telemetrymenu,surface,dock}.ts`.
Los conteos son **aproximados, leídos del código, no medidos en pantalla**. La interfaz de escritorio se construye en C# (no hay UXML propio): `Assets/Scripts/UI/Desktop/*.cs` + `Assets/Resources/UI/DesktopUI.uss` (2564 líneas); `MainLayout.uxml` y `Theme.uss` son compartidos con WebGL y no se tocan.

## 0. Cómo está montada hoy la telemetría en escritorio

- Pestaña 4 «Telemetría» (`DkMode4`, tecla 4) abre `DkTelemetry` dentro de `BottomBar` con **cuatro píldoras** (`DesktopTelemetryUI.cs/.Pilot.cs/.Tools.cs`) que se dibujan siempre, sea cual sea la fuente:
  1. `DkTelStrip`: Encendido · Fuente · Alertas · Fallas · Análisis (5)
  2. `DkTelPilotStrip`: Pilotar · Despegar · Aterrizar · Mantener · RTL · Seguir/Libre · Modo juego (7)
  3. `DkTelEnvStrip`: Suelo · Escenario · Geocerca · Viento · Batería (5)
  4. `DkTelToolsStrip`: Mapa · Comprobar · Misión · Grabar · Entrenar (5) (+ tarjeta térmica `DkThermal`)
- Cada botón abre un popover bajo demanda (`NewPopover`, `dk-pop`, uno abierto a la vez; `Popovers.cs`, `Fence/Wind/Mission/Record/...cs`). Las lecturas viven en el **inspector acoplado** (pestañas Pieza / Vuelo / Análisis, `Flight.cs`, `Analysis.cs`), 340 px a la derecha; el árbol, 280 px a la izquierda.
- Lo que no aplica a la fuente se **atenúa** (`SetEnabled(false)` / `dk-strip-btn--dim`), no se oculta.

## (a) Inventario por fuente (aprox.)

Siempre visible (viewer): barra superior ≈13 botones (inicio, 4 pestañas, chip de estado, pines, aislar, restablecer, árbol, inspector, idioma, ajustes, ayuda) + 0-11 chips de efectos activos + 4 en los rieles + ayuda en la barra de estado. Inspeccionar: tira de 3 tarjetas (Puntos · Aislar · Energía).

| Fuente | Pestaña 4 abierta: botones de las 4 píldoras | de ellos atenuados | Inspector (Vuelo / Análisis) | Popovers a demanda |
|---|---|---|---|---|
| Inspector (apagado) | 22 | ≈15 | Vuelo: estado vacío; Análisis: vacío | Fuente (4 filas con texto largo cada una), Alertas, Fallas (inyección sintética aunque no haya vuelo) |
| Simulador | 22 | ≈1-2 (Modo juego sin pilotar) | ≈25-30 valores en 6 secciones abiertas (estado 2, posición 4, actitud, batería 5 + salud ≈5, motores 4×2 + 2 cabeceras + nota, FC 2) + nota de fuente; Análisis ≈5 gráficas | Fallas (motor 1-4 + 4 tipos + Cortar motor 1-4 + Restaurar + compensación + Limpiar + Explicación ≈ 16 B), Mapa, Comprobar, Misión (≈14 B), Grabar, Entrenar, Geocerca, Viento, Batería |
| Repetición (vuelo de muestra) | 22 | ≈11 (pilotaje y 4 comandos, Geocerca, Viento, Batería, Escenario, Entrenar, Grabar) | igual que simulador | Fuente, Alertas, Fallas (inyección sintética), Mapa, Comprobar, Misión (se puede abrir aunque no acepta órdenes) |
| PX4 SITL | 22 | ≈4 (Geocerca, Viento, Batería, Entrenar) | igual + filas de ESC/GPS | Fuente + bloque URL (campo, Conectar, 2 notas), Fallas con «Agotar batería», Misión PX4 |
| Sesión propia (.twf) | 22 | ≈11 (como repetición) | igual; lecturas «simulado» | Registro con lista de sesiones guardadas |

Texto largo visible (no ⓘ) hoy: `src.*detail` en las 4 filas de Fuente, `_flBadgeDetail` y `_flSource` en Vuelo, `fx.*` (9, explicaciones de fallas), `faults.*`, `fence/wind/battery/course/an.*` notas. En `FlightText.cs` hay 386 cadenas y ≈22 de más de 110 caracteres (ES). Presupuesto de la web (≤8 controles y ≤3 filas de texto por tarjeta abierta) lo superan: Fallas, Misión, Fuente-SITL.

## (b) Reglas de la web → Unity

Esfuerzo S/M/L. «Probe» = sondas dentro del jugador (`-mvp-capture`, `-desk-fix`, `-desk-close`, etc.; ≈640 `Check(...)` repartidos en `DesktopCapture*.cs`) que localizan elementos por nombre (`DkTelPower` 6 archivos, `DkTelSource` 7, `DkSrcSim` 5, `DkTelAlerts` 6, `DkTelFaults` 5, `DkTelPilot` 5 …) o leen `Label.text`. No hay goldens de píxel de la interfaz: las capturas se revisan a ojo (en el repo web, `docs/unity_pc/capturas_*`); los goldens de `Contracts~` (telemetría) no se tocan.

| # | Regla | Qué cambiar y dónde | Esf. | Riesgo |
|---|---|---|---|---|
| 1 | Arranca apagada; Telemetría = solo 3 fuentes (Vuelo real incl. Mis sesiones · Simulador · PX4 SITL); re-pulsar la elegida apaga; sin «Apagar»/«Inspector» | Quitar `_btnPower`/`_stateLabel`/`ToggleSession`/`StartPreferred`/`_preferred`/`PlayerPrefs Desk_Source` (`DesktopTelemetryUI.cs`); `BuildSourcePopover` (`Popovers.cs`): fuera la fila `_rowInspector`, las filas pasan a 3 tarjetas con «pulsar la activa = `StopSession`»; el SITL necesita URL (bloque `_sitlBlock`); `DesktopPad.cs:265` (primer foco = «Encendido»); `OnLanguage`, tooltips `power.switch.*`; tutorial paso 7 y ayuda: `tut.tel.mode.strip`, `tut.tel.t.power` (`FlightText.cs`), `TutorialAnim.Telemetry.cs:185`, `DesktopHelpOverlay.cs`. Ya cumplido: no hay arranque automático (solo flags `-tel-*`); el chip `DkChipSession` (clic = parar) puede quedar como indicador global | M | **Alto**: 6 sondas clican `DkTelPower`, 12 llaman `Tel.StopSession`, 12 `StartSimulator`; foco del mando; textos del tutorial |
| 2 | Al elegir fuente, secciones de 3 tarjetas de tamaño fijo (Vuelo real: Datos·Vuelos·Herramientas; Simulador: Datos·Mando·Herramientas; SITL: +PX4); contenido a los lados | Nueva tabla pura `Telemetry/Logic/TelemetryMenu.cs` (port de `surface.ts` + `telemetrymenu.ts`: `Surface`, `ToolId`, `TGroup`, `TCard`, `cardsOf`, `tgroupsFor`) con EditMode (port de `surface.test.ts`/`ux4.test.ts`). UI: sustituir las 4 píldoras por 2 filas (fuentes / secciones) en `BuildMode`; reasignar: **Datos** = inspector Vuelo+Análisis (derecha) + Alertas/Eventos; **Mando** = píldora de pilotaje + Fallas del simulador + comandos; **Vuelos** = Mis sesiones (`Record.cs`) + vuelo de muestra; **Herramientas** = env + tools; **PX4** = ver duda 3. Tarjetas fijas: clase `.dk-tcard` en `DesktopUI.uss` (mismo tamaño en todos los menús) | L | Alto: reencuadre de cámara con rieles (`DesktopSidePanel`, `SetDesktopViewShiftX`), 720p (≥1600 lógico para abrir el inspector solo), `DesktopMotion.WatchVisible`, sondas de popovers |
| 3 | Ocultar lo que no aplica, no atenuar | Sustituir `SetEnabled(false)`/`--dim` por ocultar con la tabla de la regla 2: `Pilot.cs:404-405`, `Fence.cs:190`, `Wind.cs:233`, `BatteryReact.cs:117`, `Course.cs:183-185`, `Map.cs:342`, `Mission.cs:400`, `Preflight.cs:219`, `Record.cs:222`, `Training.cs:287`; dejar una línea «Disponible en: …» con ⓘ. Chips de efectos (`DesktopActiveEffects`) ya se ocultan | S-M | Medio: ≈19 comprobaciones de sondas leen `enabledSelf`/desactivado (`DesktopCaptureTools/Lote1*/Fixes`); el orden de foco del mando cambia |
| 4 | Tarjetas con lo esencial + «Avanzado» cerrado (Comandos) | Nuevo `DesktopFold` (pliegue pequeño reutilizable, cerrado por defecto). Candidatos: yaw/orbitar/mantener/RTL (hoy 4 botones fijos de la píldora, con tecla H/R, ver duda 6); Fallas (Explicación, Limpiar, compensación); Misión (Importar/Exportar/Borrar); Fuente-SITL (URL); Comprobar manual; en Vuelo: Motores, Batería (salud) y «más datos» plegados como en la web (`2c3b373`). El `DkFault`-dock ya es una sección plegada | M | Medio: sondas que leen filas dentro de secciones que pasarían a cerradas |
| 5 | Explicación larga → ⓘ (hover/foco); etiqueta corta visible; ninguna etiqueta de honestidad desaparece | Nuevo `DesktopInfo` (icono `info` + `tooltip`). `DesktopTooltip` ya muestra cualquier `tooltip` a los 0,4 s **pero con `white-space: nowrap`** (`DesktopUI.uss`, `.desktop-tooltip-text`): hay que permitir salto de línea y `max-width` ≈360 px y disparar también con `FocusInEvent` (Tab/mando). Mover a ⓘ solo textos largos (≥≈110 car.): `src.*detail`, `fx.*`, `faults.*`, notas de Geocerca/Viento/Batería/Circuito, `_flBadgeDetail`, `_flSource`. **Se quedan visibles**: insignia de origen (`DkChipStatus`, `_flBadge`), tags medido/estimado/simulado (`dk-tag`), «Posición: estimado», «no es PX4 ni un dron real» (corto), «Gemelo digital SITL». No se edita ningún texto (los nuevos necesitan aprobación) | M | **Alto**: muchas sondas leen `Label.text` de esas notas (48 en `DesktopCaptureTelemetryUI`, 54 en `Lote1B`, 50 en `Lote1C`); regla de honestidad: añadir prueba «cada cadena de honestidad sigue en el árbol (texto o ⓘ)» |
| 6 | Inspeccionar = Pines, Aislar, Encuadrar; «Encendido» del Inspector → Estudio › Modo junto a la vista térmica | Tira `ToolsCardGrid`: la 3.ª tarjeta (`ToolPowerBtn`, «ENERGÍA») pasa a «Encuadrar» (`ShortcutFocusSelection`, hoy `DkFocus` en el pie del inspector + F); reubicar `ToolPowerBtn` y `ToolPowerLoadPanel` en el popover de Render (`DesktopChrome.PrepareModePanels`, `RelabelPowerCard`, `_chipPower`) **por C# bajo `UNITY_STANDALONE`** (no tocar `MainLayout.uxml`/`InspectModeHandler`, compartidos con WebGL). El chip de energía de la barra se queda | M | Medio: `DesktopPickProbe.cs:186` clica `ToolPowerBtn` en el modo 1; `DesktopChrome` clases de la tira; WebGL debe compilar igual |
| 7 | Los 4 menús iguales: pestaña abre y cierra, sin ✕ | Ya hecho en lo esencial (ver c). Cabos: la 2.ª pulsación de Analizar con subpanel abierto vuelve primero a la rejilla (`UIModeController.HandleModeBtnClick`, línea 162) y no cierra; los popovers tienen ✕ (`NewPopover`, `DesktopChrome.BuildSettings`) | S | Bajo (solo verificar; decidir si el ✕ de popovers se quita, duda 9) |
| — | Estudio: 3 tarjetas (Modo · Entorno · Luz) | Hoy 2 (`DkStudioRender`, `DkStudioEnv`; la luz está dentro de Entorno) | S | Bajo; opcional |

## (c) Lo que el escritorio ya cumple

- Telemetría **apagada al abrir**: el Inspector es el estado inicial; sin mapa, batería ni gráficas (estado vacío de `DkFlEmpty`).
- Pestañas `DkMode1..4`: la activa abre/cierra su tira (teclas 1-4, Esc), sin ✕ en las tiras (`UIModeController`, `DesktopTelemetryUI.ToggleMode`).
- Honestidad completa y centralizada: insignia de origen, tags por lectura (`FlightOrigins`, `HonestyLabels`), «Origen local (EKF)», «Posición: estimado» (aprobado el 2026-11-19), D-1…D-10.
- Tooltips por hover ya existen para todo elemento con `tooltip` (`DesktopTooltip`, 0,4 s, con tecla); el set de iconos y los botones `dk-b` ya están (§15).
- «Contenido a los lados»: inspector 340 px a la derecha y árbol 280 px a la izquierda, con reencuadre de cámara.
- Mapa, Misión, Grabar, Comprobar y Entrenar se abren bajo petición (popovers); las explicaciones de fallas ya están en una sección plegada del inspector; los chips de efectos activos de la barra son «una vía por acción».
- Inspeccionar ya tiene 3 tarjetas (aunque la 3.ª es Energía, ver regla 6) y Analizar 3 (Corte · Explotar · Filtro).
- **No cumple**: ocultar en vez de atenuar (regla 3), arranque por tarjetas de fuente (1), niveles de secciones (2), pliegue «Avanzado» y ⓘ con foco (4, 5).

## (d) Orden de trabajo recomendado (cada paso verificable; commit por paso)

0. **Línea base** (sin cambios): EditMode 322/322, sonda `-mvp-capture` 133/133 a 1280x720 ES, capturas «ANTES» (reposo, cada fuente con la pestaña 4 abierta). Se prueba: nada que cambie.
1. **Tabla pura por fuente** `Telemetry/Logic/TelemetryMenu.cs` + EditMode (port de `surface.test.ts`/`ux4.test.ts`: cada tarjeta/herramienta pertenece a una sección, ≤3 tarjetas por fila, `toolsFor`). Sin UI. Prueba: EditMode (+≈15).
2. **Regla 3 — ocultar** con esa tabla (10 archivos de la regla 3). Prueba: EditMode; sonda nueva `-ux-capture` que cuenta botones visibles por fuente y compara con la tabla; actualizar las ≈19 comprobaciones de «desactivado»; `-mvp-capture` 133/133.
3. **Regla 6 — Encuadrar y Energía en Estudio**. Prueba: clic en las 3 tarjetas de Inspeccionar, el interruptor y el deslizador de carga en Estudio > Render, chip de energía; `DesktopPickProbe` ajustada; WebGL compila 0 errores.
4. **Auxiliares `DesktopInfo` + `DesktopFold`** y ajustar `DesktopTooltip` (salto de línea, `max-width`, foco). Prueba: sonda de ⓘ (hover y foco/Tab muestran el texto completo, también en 1280x720) y EditMode «las cadenas de honestidad siguen en el árbol».
5. **Aplicar ⓘ y «Avanzado»** pantalla a pantalla (Fuente, Fallas, Misión, Vuelo, Geocerca/Viento/Batería); un commit por popover. Prueba: `-mvp-capture` + `-desk-close`; capturas ES/EN.
6. **Regla 1 — fuentes**: 3 tarjetas, re-pulsar apaga, quitar Encendido/`_preferred`/`Desk_Source`, foco inicial del mando, tutorial y ayuda. Prueba: sondas con `DkTelPower` reescritas, bucle encender/apagar ×3 por fuente, mando falso (`-desk-fix`, B), `Player.log` limpio.
7. **Regla 2 — secciones fijas y lados**. Prueba: 1280x720, 1904x984 y 150 % (`-uiscale 1.5`) sin solapes, cobertura del lienzo (≈ §13.2), el reencuadre de cámara con inspector abierto.
8. **Cierre**: regla 7 (verificar), PLAN_PC §38, capturas ES/EN 1280x720 y 1904x984, EditMode completo, build de desarrollo y puerta `pc_postbuild_gate.ps1` **solo si se pide**, y recetas de §37.4 (plataforma WebGL activa, `git checkout` de `UniversalRenderPipelineGlobalSettings.asset`, `DynamicsManager.asset`, `ProjectSettings.asset`). Sin zip.

## (e) Diferencias de plataforma y dudas

**Diferencias** (la web es táctil + ratón; el .exe es ratón + teclado + mando):
- **ⓘ por hover**: el hover existe (0,4 s) pero el foco por teclado y el modo «Menús» del mando no lo disparan hoy; en la web el ⓘ se abre también con toque, aquí no hace falta. El objetivo del ⓘ debe medir ≥24 px (el cursor es preciso pero los iconos de 16 px son pequeños).
- **Mando**: en modo Menús la navegación (`NavigationMove`, `DesktopPad`) es geométrica: ocultar o reordenar botones cambia el orden de foco; el primer foco hoy es «Encendido» (`DesktopPad.cs:265`). En modo Dron los menús no reciben nada: el pilotaje, el pliegue «Avanzado» y las tarjetas deben seguir alcanzables con A/B en Menús. Sin mando real probado (§27.2).
- **Ratón**: re-pulsar una fuente apaga: riesgo de apagar con un doble clic; SITL necesita una URL (campo de texto) y la web no lo tiene.
- **Teclado**: L (fuente), Shift+A (alertas), T/H/R/B/X; con un campo de texto enfocado las teclas no actúan (`DesktopKeyboardGate`).
- **Pantalla**: 720p es el caso difícil (cuatro píldoras ya no cabían en inglés, §23.5; el inspector no se abre solo con <1600 lógico). La web hace «contenido a los lados»; en el .exe el lado derecho ya es el inspector.
- **WebGL de Unity**: `MainLayout.uxml`/`Theme.uss`/`InspectModeHandler` son compartidos: todo bajo `UNITY_STANDALONE`; WebGL debe seguir compilando con 0 errores (Unity WebGL será reemplazado por la web nativa, no se invierte esfuerzo allí).
- **Sin Pixhawk/HITL**: Lote 2 (parámetros, failsafe, calibración guía, misión PX4 con editor) no está en escritorio (§37.5).

**Dudas para el usuario**
1. ¿Seguimos la web al pie de la letra (2 niveles: fuentes → secciones, reglas 1-2: esfuerzo L) o hacemos la versión ligera (mantener píldoras + inspector, aplicar solo ocultar, Avanzado, ⓘ y Energía/Encuadrar: reglas 3-6)? Recomiendo empezar por la ligera (pasos 1-5) y decidir el rediseño de niveles después.
2. «Vuelo real» en escritorio es un vuelo de muestra + Mis sesiones (.twf). No hay `.ulg` ni Comparar. ¿Se porta el lector `.ulg` (la sección «Vuelos») o «Vuelos» solo tendrá Mis sesiones y la muestra?
3. SITL: sin parámetros/failsafe/calibración, la 4.ª tarjeta «PX4» quedaría vacía. ¿La omitimos (SITL con 3 secciones) hasta portar el Lote 2?
4. Elegir la tarjeta SITL: ¿conecta ya con la última URL guardada (la URL se edita en «Avanzado»)?
5. ¿Se mantiene el chip de sesión de la barra superior (clic = parar) como único «Apagar» global?
6. Comandos: ¿Mantener (H) y RTL (R) siguen visibles por seguridad o pasan a «Avanzado»?
7. ¿Se deja de recordar la fuente (`Desk_Source`) como en la web (no guarda nada)?
8. ¿Dónde van las capturas de verificación: `docs/unity_pc/capturas_*` del repo web (como hasta ahora) o este repo?
9. ¿Se quitan los ✕ de los popovers (cierra la pestaña/Esc) o solo el de las tiras? ¿Estudio con 3 tarjetas (Modo · Entorno · Luz)?
