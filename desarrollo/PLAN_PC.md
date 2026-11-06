# PLAN_PC — TwinSight X500 para Windows (build de escritorio)

> Rama de trabajo: `pc-desktop` (nunca `master`: Pages publica `docs/` desde `master` y la build WebGL no se puede romper).
> Este archivo vive en `desarrollo/`, NO en `docs/` (que se publica).
> Inventario completo (solo lectura): `E:\twinsight-web-twin\docs\unity_pc\INVENTARIO.md`. Capturas de la fase 0: `E:\twinsight-web-twin\docs\unity_pc\capturas_fase0\`.

## 1. Decisiones aprobadas por el usuario (2026-11-02)

1. **Orden de fases: 0 -> 1 -> 2 -> 3** (base, gráficos rápidos, UI de escritorio, realismo).
2. **La cápsula de modos** (Inspect / Analyze / Studio) **se queda abajo, al centro**, igual que en la web.
3. **La ficha de pieza pasa a un panel lateral derecho** en escritorio (en lugar del bottom sheet al 55 %).
4. **Los ajustes de PC se separan por plataforma** (niveles de calidad, assets URP, escala de UI, atajos...) para **no tocar WebGL**: lo de PC va en `#if UNITY_STANDALONE`, niveles de calidad por plataforma y assets URP propios; `ShaderVariantStripper` y los clamps de WebGL se quedan como están.

## 2. Fases

### Fase 0 — Base (esta fase, hecha)
Plugin MCP en el original, verificaciones en el Editor, build Windows sin cambios gráficos, línea base de FPS (`PerfLogger`) y este plan.

### Fase 1 — Gráficos rápidos (G1, G2, G3, G4, G5, G12)
| # | Qué |
|---|---|
| G1 | Perfil de Volume real (Tonemapping, Bloom sutil, Color Adjustments, Vignette, DOF opcional) asignado al `Global Volume`. Mantener DESMARCADA "Strip Unused Post Processing Variants". |
| G2 | HDR + MSAA 4x/8x + FXAA/SMAA/TAA en cámara (asset URP propio de PC). |
| G3 | Entorno HDRI + Reflection Probe / cubemap para metales y carbono (arregla los reflejos negros, ver 3a). |
| G4 | Sombras de calidad: 4 cascadas, 4096, soft alta, 60 m; sombras en 1-2 luces extra; keywords en `ClippableLit`. |
| G5 | Niveles de calidad PC (Low/Medium/High/Ultra) con un asset URP por nivel, vsync=1, mip limit 0; actualizar `QualityLevelFixer` para que no los pise. |
| G12 | Quitar limitaciones WebGL del código de calidad solo para PC (HDR en runtime, tope de render scale, vsync/FPS objetivo). |

### Fase 2 — UI de escritorio (U2, U3, U4, U5, U9; después U1, U6, U7)
| # | Qué |
|---|---|
| U2 | Atajos de teclado + chips de atajo (1/2/3 modos, F enfocar, Home reset, H pins, I aislar, E explosión, Tab, ?...). |
| U3 | Tooltips y estados hover (los `tooltip=` del UXML no se muestran hoy). |
| U4 | Escala de UI y DPI (referencia 1920x1080, Match 0,5-1,0, "Tamaño de interfaz" 80-150 %). |
| U5 | Ventana y pantalla: redimensionable, F11/Alt+Enter, resolución, vsync, objetivo de FPS en CONFIG; EXIT cierra la app. |
| U9 | Corregir la selección del motor (`SelectionManager.cs` L398-401, L476-482, L1075-1086) y breadcrumb arm -> motor -> hélice. |
| U1 | Layout de escritorio: ficha en panel lateral derecho (~420-480 px); cápsula de modos abajo al centro (decisión 2); clase `.layout--desktop`. |
| U6 | Cámara de escritorio (doble clic para enfocar, zoom al cursor, teclas, presets). |
| U7 | Hero y onboarding para escritorio (sin rama táctil por defecto, texto "click"). |

### Fase 3 — Realismo y extras (G6-G11, U8, U10)
G6 shader PBR de escritorio (ORM, emisión, clear-coat, anisotropía, SSAO/probes); G7 materiales realistas del dron; G8 texturas 4K/8K y conectar la rugosidad; G9 SSAO y sombras de contacto; G10 rig de estudio mejorado y ciclo de hora; G11 modos de visualización en alta calidad; U8 panel de propiedades denso (árbol de piezas, BOM); U10 deep link de escritorio (`-part <id>` y/o `twinsight://part/<id>`).

## 3. Resultados de la Fase 0 (medidos el 2026-11-02, Unity 6000.6.5f1)

**Plugin:** `com.anklebreaker.unity-mcp` (v2.41.4) resuelto, 0 errores de compilación; MCP conectado en el puerto 7890 al proyecto original.

**Verificaciones en el Editor (solo lectura):**
- **(a) Reflejos de entorno: CONFIRMADO que NO hay.** `RenderSettings.defaultReflectionMode = Custom` con `customReflection = null`, 0 Reflection Probes, skybox del Editor = Default-Skybox. Los metales y la fibra de carbono salen casi negros (capturas `01_game_editmode.png`, `02_scene_editmode.png`). Se arregla en G3.
- **(b) `Global Volume`:** `isGlobal=true`, peso 1, **`sharedProfile = null`** (sin perfil). Solo actúa el perfil por defecto de URP, con todos los efectos en neutro (sin tonemapping ni bloom). Se arregla en G1.
- **(c) Cámara (`Main Camera`):** HDR de cámara = true, pero el asset activo `URP_AutoFixed` tiene `supportsHDR = false` (la cámara no obtiene HDR); AA de cámara = None; MSAA = 4x (solo del asset); post-procesado = activado (`renderPostProcessing = true`). Calidad: 1 solo nivel (`Low`), `vSyncCount = 0`.
- **(d) `QualityLevelFixer`** (`Assets/Editor/Antigravity/Fixes/QualityLevelFixer.cs`): es un script **solo de Editor** que asigna el mismo asset URP (`URP_AutoFixed`, si no `URP_HighFidelity` o `High_PipelineAsset`) a TODOS los niveles de calidad. **No corre solo**: se ejecuta desde el menú `WebGL > Fixes > 2. Set WebGL Quality Level`, desde `WebGLBuildFixer` ("Fix ALL WebGL Issues" / "Fix & Build WebGL") y desde `WebGLBatchBuildEntry`. No intervino en la build de Windows (no se usó el menú WebGL). Riesgo para G5: al crear niveles PC, "Fix & Build WebGL" los pisaría; hay que limitarlo a niveles WebGL.

**Build base Windows (sin cambios gráficos):** Windows Standalone x86_64, Mono (IL2CPP no instalado), Release, escena `MainScene_Final`, D3D11 (D3D12 en la lista). Éxito en ~63 s (cambio de plataforma incluido la primera vez). Salida: `E:\TwinSight_builds\pc_baseline\TwinSightX500.exe` (69 MB en disco; `totalSize` de Unity 116 MB). 0 errores, 136 avisos. Sin Build Profile nuevo: se usó la configuración clásica de Build Settings (el único perfil, `New Web Profile`, se dejó intacto). El cambio de plataforma no modificó ningún archivo de `ProjectSettings` versionado.
Avisos útiles: "Pre-baked collision is disabled on 60 meshes" (colisiones de malla sin precalcular) y avisos del shader `CRTGlitch` (`pow` con negativos).

**Línea base de rendimiento** (`PerfLogger -perflog`, ventana 1920x1080, `Calidad=Low`, vSync=0, D3D11, visor abierto con el dron quieto, ventanas de 10 s):

| Ventana | Qué era | FPS media | mínimo | p1 |
|---|---|---|---|---|
| 1 | arranque y Hero (carga) | 231 | 0,3 (carga) | 292 |
| 2 | Hero | 559 | 71 | 313 |
| 3 | transición Hero -> visor | 227 | 8 | 80 |
| 4 | **visor** | 143 | 41 | 79 |
| 5 | **visor** | 147 | 64 | 83 |
| 6 | **visor** | 154 | 48 | 87 |
| 7 (parcial, 6,4 s) | **visor** | 140 | 72 | 80 |

Resumen del visor (ventanas 4-7): **media ~146 FPS, mínimo 41-72 (peor ventana 41), p1 ~80**. Es un valor alto porque hoy el render es muy ligero (Low, 0 post-procesado, sombras y luces mínimas) y no hay vsync: sirve como techo a comparar tras las Fases 1-3 (HDR, MSAA, sombras, SSAO costarán FPS). Con vsync (G5) el tope pasará a 60.

**Hardware de la medición:** GPU NVIDIA GeForce RTX 5060 Ti (driver 32.0.16.1742), CPU Intel Core i7-5820K @ 3,30 GHz (6 núcleos / 12 hilos), 48 GB de RAM, Windows 11 Pro 10.0.26300, pantalla 1920x1080 @ 60 Hz.

**Cómo repetir la medición:**
`TwinSightX500.exe -perflog -logFile <ruta>\perf.log -screen-width 1920 -screen-height 1080 -screen-fullscreen 0` y leer las líneas `[PerfLogger] ventana N ...` (media, mínimo y p1 por cada 10 s). Script: `Assets/Scripts/Core/Utils/PerfLogger.cs` (solo `UNITY_STANDALONE`, no en el Editor, solo con `-perflog`). El visor del propio juego (F8, `WebGLProfiler`) es interactivo y no da mínimo ni p1, por eso se añadió el PerfLogger.

**Hallazgos/sorpresas para fases siguientes:**
- Al lanzar el .exe aparece el onboarding de 15 pasos (PlayerPrefs `Onboarding_Seen` no estaba puesto en el registro de este equipo).
- `ProjectSettings` sin cambios al cambiar de plataforma; el plugin MCP solo tocó `Packages/manifest.json` y `packages-lock.json`.
- Un script del proyecto (`ObsidianTelemetry`) escribe un log por build en `E:\WebGL_tesis\Telemetria\Unity_Builds\` (fuera de git/no versionado).
- Resolución de la ventana: 1920x1080 de cliente (`resizableWindow: 0`, la ventana de Windows añade barra de título).

## 4. Reglas que se mantienen
- No tocar `master` ni `docs/` del repo de la tesis. Sin push desde esta rama sin confirmación.
- "Strip Unused Post Processing Variants (and Resources)" DESMARCADA (`UniversalRenderPipelineGlobalSettings.asset` L22/L29).
- Compilar siempre desde el Editor (con `-batchmode` no funciona la licencia). No cerrar Unity a la fuerza.
- Al terminar un trabajo de PC, dejar la plataforma activa en Web si el siguiente paso es un build WebGL.

## 5. Fase 1 — resultados (2026-11-03, Unity 6000.6.5f1)

### 5.1 Qué se hizo
| # | Resultado |
|---|---|
| **G5** niveles PC | 4 niveles nuevos `PC_Low / PC_Medium / PC_High / PC_Ultra` en `QualitySettings` (por defecto de Standalone: `PC_Ultra`; vsync=1, mip limit 0 salvo Low, anisotrópico 2x, LOD bias 1-2). Cada uno con su asset URP en `Assets/Settings/PC/` (`PC_Low.asset` ... `PC_Ultra.asset`) y un renderer propio `PC_Renderer.asset` (copia del de WebGL con sus 2 features; deja listo SSAO de la fase 3 sin tocar WebGL). Se generan con el menú `TwinSight > PC > Build PC Graphics (fase 1)` (`Assets/Editor/PC/PCGraphicsSetup.cs`, idempotente). |
| **G2** HDR + AA | HDR ON en los 4 assets (64-bit solo en Ultra), MSAA 4x en High/Ultra (2x en Low/Medium), render scale 1,25 en Ultra (1,0 en el resto). AA de cámara por tier (`RenderQualityController.ApplyPcCameraAntiAliasing`): **SMAA High (Ultra), SMAA Medium (High), FXAA (Medium), ninguno (Low)**. Además, solo en PC, se activan `renderShadows` y `dithering` de la Main Camera. |
| **G4** sombras | Ultra: 4 cascadas (splits 0,08/0,22/0,50), 4096, 40 m, soft High; High: 4 cascadas, 2048, 35 m; Medium: 2 cascadas, 2048, 30 m; Low: 1 cascada, 1024, 20 m, sin soft. El dron mide ~10 m en la escena (no 0,6 m): 40 m cubre el zoom máximo de la órbita (25 m) y la cascada 1 (3,2 m) da resolución a los primeros planos. Sombras de 2 luces adicionales (Fill y Top softbox, `EnvironmentController`, solo PC) en High/Ultra (atlas 1024/2048); fuerza de sombra de la luz principal 0,85 en PC. **Hallazgo:** la Main Camera tenía `Render Shadows = off` en la escena, por eso nunca había sombras; en PC se activa por código (la escena no cambia). `ClippableLit.shader`: añadidos `multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS` y `multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW` (+ bias por luz puntual en el ShadowCaster); `_MAIN_LIGHT_SHADOWS_CASCADE` ya estaba. `ShaderVariantStripper` (solo WebGL) ya eliminaba `_ADDITIONAL_LIGHT_SHADOWS` y ahora también `_CASTING_PUNCTUAL_LIGHT_SHADOW` solo para `WebGL/ClippableLit`: las variantes compiladas en WebGL son las de antes. |
| **G1** post-procesado | `Assets/Settings/PC/PC_PostProcess.asset`: Tonemapping **Neutral**, Bloom (umbral 1,0, intensidad 0,4, scatter 0,6), Color Adjustments (exposición post +0,15, contraste +8), Vignette 0,2, DOF desactivado (listo para activar). Se asigna en el campo *Volume Profile* de cada asset URP de PC (no en el `Global Volume`, que sigue sin perfil). "Strip Unused Post Processing Variants" sigue desmarcada. **Hallazgo:** `URP_AutoFixed` tiene `Volume Update Mode = Via Scripting` (el stack de volúmenes no se actualiza solo); en los assets PC está en *Every Frame*. |
| **G3** reflejos | Ver 5.3. |
| **G12** código | `RenderQualityController` (solo `UNITY_STANDALONE`): selecciona el nivel `PC_*` según el tier (Auto -> por VRAM -> Ultra), respeta los valores del asset y solo aplica overrides si el usuario los tocó; render scale hasta 2,0, MSAA hasta 8x, distancia de sombra hasta 100 m, HDR configurable (`SetAdvancedHdr`), sin el tope de WebGL. Argumentos de línea de comandos: `-quality <PC_Low\|PC_Medium\|PC_High\|PC_Ultra\|low\|...\|auto>` y `-vsync <0\|1\|2>` (mediciones). `QualityManager` no toca la escala de render en PC. `UIQualityConfigPanel`: sliders hasta 2,0 / 80 m y MSAA 8x solo en PC (el UXML no cambia). |

### 5.2 AA elegido: SMAA (High en Ultra) + MSAA 4x
Comparación a 1920x1080 con primeros planos del dron (sin AA, SMAA High, TAA) en PC_Ultra: con MSAA 4x + render scale 1,25 los bordes ya salen limpios; **SMAA** limpia el aliasing residual de especulares/texturas sin perder el trenzado de la fibra ni la serigrafía; **TAA** suaviza más (emborrona el trenzado y las letras finas) y necesita varios fotogramas para converger al orbitar. Tonemapping: **Neutral** y no ACES, porque ACES aplasta el degradado oscuro del fondo (de 28/255 a 6/255 en el centro) y satura los azules de las piezas (`cmp_tonemap_aces.png` frente a `cmp_tonemap_neutral.png`).

### 5.3 Reflejos de entorno (sin descargar nada)
`Assets/Scripts/Core/Rendering/PCStudioReflections.cs` (todo bajo `#if UNITY_STANDALONE`, se crea por código desde `RenderQualityController.Bootstrap`: la escena y los assets de WebGL no cambian). 6 paneles emisivos HDR tipo softbox (key, tira de relleno, tira de contra, cenital, línea de horizonte, suelo tenue) en la capa 31 `PCReflectionOnly` (nueva en `TagManager`), que la Main Camera excluye de su `cullingMask`, y un Reflection Probe **Realtime / ViaScripting** centrado en el dron que solo ve esa capa. El skybox degradado usa coordenadas de pantalla y no sirve para un cubemap: el fondo del probe es el color ambiente (trilight) del preset actual. El probe se re-renderiza cuando `EnvironmentController` cambia de preset, rotación/intensidad de luz o fondo (evento `OnEnvironmentChanged`, solo PC) y al cambiar el tier; la resolución depende del tier (64-512). `RenderSettings.defaultReflectionMode` pasa a Custom con el cubemap del probe, así nada queda negro. Comprobado en Play: tras `ApplyPreset("Studio Light")` el probe se renderizó otra vez y `EnvironmentController` no pisa los RenderSettings de PC.

**HDRI externo (opcional, NO descargado):** habría un entorno más rico con un HDRI de estudio de Poly Haven (CC0), por ejemplo `studio_small_09_2k.hdr` (unos 6 MB; nombre y tamaño a confirmar en polyhaven.com). No es necesario para esta fase; se propone para la fase 3 si el usuario lo aprueba.

### 5.4 FPS (RTX 5060 Ti, i7-5820K, 1920x1080 ventana, D3D11, visor abierto con el dron quieto, ventanas de 10 s)
| Configuración | FPS media | mínimo (peor ventana) | p1 |
|---|---|---|---|
| Fase 0 (Low, sin HDR/post/sombras, vsync 0) | ~146 | 41 | ~80 |
| Fase 1 `PC_Ultra`, vsync 0 (`-vsync 0`, 7 ventanas) | 145 (141-152) | 54 | 84 (80-89) |
| Fase 1 `PC_High`, vsync 0 (5 ventanas) | 146 (143-149) | 43 | 87 (83-90) |
| Fase 1 `PC_Ultra`, vsync 1 (por defecto) | 60,0 (tope del monitor) | 59,9 (una ventana con un tirón a 10) | 59,9 |

La app está limitada por la CPU (254 renderers y scripts; el Hero sin visor da 440-540 FPS): HDR 64-bit, MSAA 4x, render scale 1,25, 4 cascadas de 4096 y post-procesado no cuestan FPS medibles en esta GPU. Logs en `E:\TwinSight_builds\pc_fase1\logs\`. Build: `E:\TwinSight_builds\pc_fase1\TwinSightX500.exe` (Windows x86_64 Mono). Capturas del Editor y del .exe en `E:\twinsight-web-twin\docs\unity_pc\capturas_fase1\` (X-Ray, Térmica y Wireframe verificadas con HDR + post-procesado). En las capturas `*_exe.png` aparece una barra de otra aplicación del escritorio en el borde inferior.

### 5.5 Cómo se aisló WebGL
1. Niveles `PC_*` con `excludedTargetPlatforms` = todas menos Standalone; el nivel `Low` de WebGL solo añade `Standalone` a su lista de exclusión (WebGL sigue en el nivel 0 y no ve los nuevos).
2. `URP_AutoFixed.asset`, `URP_HighFidelity_Renderer.asset` y la escena `MainScene_Final.unity`: `git diff` vacío (los assets PC son copias; el post-procesado va en el Volume Profile del asset URP de cada nivel, no en el `Global Volume`).
3. Todo el comportamiento nuevo de runtime va bajo `#if UNITY_STANDALONE` (`RenderQualityController`, `QualityManager`, `EnvironmentController`, `UIQualityConfigPanel`, `PCStudioReflections`); el código de WebGL es el anterior. Probado en Editor con plataforma WebGL: asset `URP_AutoFixed`, sin HDR, sin perfil, sin probes, AA None, `renderShadows` off.
4. `QualityLevelFixer` y `URPPipelineFixer` (Editor) ignoran los niveles `PC_*`.
5. Cambios en `ProjectSettings`: `QualitySettings.asset` (niveles nuevos, `Standalone: 4`) y `TagManager.asset` (capa 31 `PCReflectionOnly`).
6. Build de prueba WebGL: build WebGL de prueba compilada con éxito (~4 min, 0 errores) a `E:\TwinSight_builds\webgl_check\` (nunca a `docs/Build`; `git status` de `docs/` limpio). Al terminar, la plataforma activa del Editor es WebGL y 0 errores de compilación.

## 6. Fase 2A — resultados (2026-11-03, Unity 6000.6.5f1, rama `pc-desktop`)

Alcance: parte funcional de la UI de escritorio (atajos, tooltips, escala/DPI, ventana, selección del motor y arreglos de la auditoría UX/UI). El layout lateral de la ficha (U1) queda para la 2B. No se tocaron los ajustes gráficos de la fase 1 ni la cápsula/tarjetas/pills/celeste/Space Grotesk.

### 6.1 Cómo está hecho (aislamiento de PC)
Todo lo exclusivo de escritorio vive en `Assets/Scripts/UI/Desktop/` bajo `#if UNITY_STANDALONE` y se crea por código (`DesktopUIController.Bootstrap`, `RuntimeInitializeOnLoadMethod`): la escena y `MainPanelSettings.asset` NO cambian (verificado con `git status`). Sus estilos están en `Assets/Resources/UI/DesktopUI.uss`, que solo se carga en Standalone (clase `.desktop-ui` en la raíz del UIDocument). Archivos: `DesktopUIController` (arranque, atajos, pastilla "?"), `DesktopTooltip`, `DesktopHelpOverlay`, `DesktopDisplayPanel` (sección PANTALLA de CONFIG), `DesktopUIScaler`, `DesktopWindowSettings`, `DesktopCaptureDemo` (solo con `-desk-demo <carpeta>`, para las capturas). El celeste usado es el de `Theme.uss` (`rgb(70,175,255)`), para ser coherente con la UI existente.

### 6.2 Tabla de atajos (U2, solo Standalone; Input Manager antiguo)
| Tecla | Acción |
|---|---|
| `1` / `2` / `3` | INSPECT / ANALYZE / STUDIO (mismo conmutador de 3 estados que el botón; no actúa en el Hero ni con el tutorial abierto) |
| `F` | Enfocar la selección (sin selección: reset de vista) |
| `Home` o `R` | Reset de vista (igual que el botón: sale de Explode/Analyze, desactiva el corte y deselecciona) |
| `H` | Pins (mostrar/ocultar) |
| `I` | Aislar la selección (alterna) |
| `E` | Explosión (abre Analyze si hace falta y alterna) |
| `Esc` | Atrás por capas: ayuda, tutorial, ficha, submenú/modo, **deseleccionar** (nuevo, solo PC), volver al Hero |
| `F11` o `Alt+Enter` | Ventana / pantalla completa (se desactivó el Alt+Enter nativo de Unity, `allowFullscreenSwitch = 0`, para guardar el estado) |
| `?` o `F1` | Overlay de ayuda (pills celestes, ES/EN); también se cierra con `Esc` o clic. Botón "?" abajo a la izquierda |

No se disparan con Ctrl/Alt pulsados ni mientras hay foco en un campo de texto (`ITextEdition`); con la ayuda abierta solo funcionan `F11`, `?`, `F1` y `Esc`.

### 6.3 Resultados por punto
| # | Resultado |
|---|---|
| 1 U2 atajos | Hecho. Comprobado en el .exe real (teclas enviadas con SendKeys y trazas `[DesktopUI] shortcut:` en `Player.log`: 1/2/3, ?, F1, Esc cierra la ayuda, E, H, F11 dos veces con cambio a 1920x1080 y vuelta). F, I, Home/R y Esc-deselecciona comprobados en Play llamando a la misma API `UIManager.Shortcut*`. |
| 2 U3 tooltips/hover | Hecho. UI Toolkit no pinta `tooltip=` en runtime: etiqueta flotante propia (0,4 s de reposo, arriba o abajo según la posición) con el texto del UXML (traducido ES/EN) y un pill con la tecla (por ejemplo "Toggle Hotspots [H]", "Reset Camera [R / Home]"). Los `:hover` de tarjetas, botones de modo y círculos ya existían en `Theme.uss` y se mantienen. |
| 3 U4 escala/DPI | Hecho. `DesktopUIScaler` sustituye (solo en memoria, se restaura al salir) el "Scale With Screen Size / match 0,5" por una escala constante `clamp(alto/1080 * "Tamaño de interfaz", 0,92, 2,0)`: 1280x720 da 0,92 (lógico 1391x783; antes 0,67), 1920x1080 da 1,0 y 2560x1440 da 1,33. 0,92 es el suelo que mantiene el control más pequeño (48 px) en 44 px o más. Por debajo de 900 px lógicos de alto se añade `.ui-compact` (tarjetas 120x104, cápsula 96 px) para que el panel STUDIO quepa. También 44 px mínimo en CONFIG (tarjetas, botones cíclicos, botón "i", cerrar del tutorial) vía `DesktopUI.uss`. "Tamaño de interfaz" 80-150 % en CONFIG llama a `AccessibilityManager.SetUIScale` (no estaba en la escena: se crea por código en PC; en PC ya no aplica `style.scale`, multiplica la escala del panel). |
| 3b cápsula cortada | Causa: una ventana con área cliente de 1080 px en un monitor 1080p deja el borde inferior bajo la barra de tareas. `DesktopWindowSettings.FitWindowToWorkArea` encoge el área cliente al área de trabajo (`Screen.mainWindowDisplayInfo.workArea` menos unos 48 px de marco) y centra la ventana (se desactiva con `-desk-nofit`). |
| 4 U5 ventana | Hecho. `resizableWindow = 1` (ProjectSettings, solo Standalone). Mínimo 1280x720 (el Player de Unity no tiene mínimo nativo: si queda por debajo y se suelta el ratón, vuelve al mínimo a los 0,35 s; limitado por el área de trabajo en pantallas pequeñas). CONFIG > PANTALLA (solo PC): modo ventana o pantalla completa (sin bordes, a resolución nativa; la resolución se deshabilita en pantalla completa), resolución (lista menor o igual al monitor, aplica al tamaño de la ventana), VSYNC y TAMAÑO DE INTERFAZ. Se guarda en PlayerPrefs `Desk_*`; los argumentos `-screen-*` y `-vsync` mandan. En PC el Hero muestra **QUIT/SALIR** (`Application.Quit`) en vez de EXIT, **MODEL/MODELO** en vez de DEVICE, y los textos "WebGL" pasan a "escritorio". |
| 5 U9 motor | Hecho. Helper `BelongsToFull(raw, full) = raw.IsChildOf(full) \|\| ResolvePrimarySelection(raw) == full` en `SelectionManager` (y equivalente en `UIManager.IsSubSelection` para aislar) más `ResolveDetachedAssemblyPart` (motor_XX es hermano de arm_XX; la hélice ya es hija del brazo). Verificado con `SelectionManager.SimulateClickOn` (mismo código que el clic real): clic en el motor sin selección da **motor solo** (full = brazo, sub = motor); clic en la geometría del brazo con el motor seleccionado da **el brazo**; clic en el brazo da el brazo; fondo vuelve al brazo y luego a nada; clic en el motor de otro brazo selecciona ese motor. |
| 6 Auditoría | **AUX-U03**: el bloqueo era una lista de nombres (faltaban STUDIO/THERMAL, etc.); ahora `InputManager.IsScreenPositionOverPickableUI` usa `Panel.Pick` (cualquier elemento visible con `picking-mode: Position`) en la ruta de selección y hover. Comprobado: THERMAL, XRAY, EnvPreset, StudioPanel, botones de modo y Home/Reset se detectan; en reposo solo bloquean pins, cápsula y botones (sin falsos positivos). **AUX-U04**: Reset llama a `SelectionManager.Deselect()` (antes solo publicaba el evento y quedaba el resaltado). **AUX-U10**: el cambio de modo conserva la selección (verificado: tras 2/3/1 la selección y el título siguen) y hay botón "<" en los subpaneles CUT y FILTER (Esc también retrocede). **AUX-U09**: estado activo único, borde, icono y texto celestes sin relleno (reglas finales en `Theme.uss`; el relleno solo mientras se pulsa). **AUX-U07**: eliminado el rótulo "CODE PROTOTYPE · CONCEPTUAL DRAFT" (UXML, código y diccionario). **AUX-U12**: leyenda térmica con "DIDACTIC SIMULATION · ILLUSTRATIVE" / "SIMULACIÓN DIDÁCTICA · ILUSTRATIVO". |
| 7 Pin sobre el título | Hecho. `HotspotManager` calcula cada frame los rectángulos de `TopContextLabel`, `HomeBtn` y `ResetViewBtn`; `SmartHotspot` oculta los pins cuyo círculo (radio 22 más 8) los toque. |

### 6.4 Qué cambia en WebGL al recompilar
Los arreglos de fallos son de todas las plataformas y cambiarán WebGL: U9 (motor), AUX-U03 (bloqueo de clic por `Panel.Pick`, también mejora el táctil), AUX-U04 (Reset deselecciona), AUX-U10 (botones "<" en CUT/FILTER, `MainLayout.uxml`), AUX-U09 (estado activo, `Theme.uss`), AUX-U07 (sin rótulo CODE PROTOTYPE), AUX-U12 (nota térmica), pin enmascarado bajo la cabecera, nuevas traducciones en `AppLanguageManager` y API pública nueva en `UIManager`/`UIModeController` (inerte). NO cambian en WebGL (solo `#if UNITY_STANDALONE` o recursos que WebGL no usa): atajos, tooltips, escala, ventana/PANTALLA, Salir/Modelo, la regla de Esc que deselecciona, `AccessibilityManager` (en WebGL sigue con `style.scale`), `DesktopUI.uss` (queda en `Resources`, sin usarse), `resizableWindow` y `allowFullscreenSwitch` (ajustes de Standalone). **No se compiló ni publicó WebGL en `docs/`** (hace falta al usuario). Con WebGL activo el proyecto compila con 0 errores.

### 6.5 Verificación, builds y capturas
- Compilación: 0 errores (Standalone y WebGL). Build Windows x86_64 Mono: `E:\TwinSight_builds\pc_fase2a\TwinSightX500.exe`.
- Capturas generadas por el propio .exe con `-desk-demo` (no dependen del monitor): `docs/unity_pc/capturas_fase2a/` (por resolución: cápsula completa, tooltip con atajo, STUDIO, ayuda, motor seleccionado solo y CONFIG con PANTALLA). 1280x720 en ventana; "1920x1080" en ventana, que el ajuste al área de trabajo deja en 1904x984 (cápsula completa, escala 0,92); y 2560x1440 NO se pudo obtener en este monitor 1080p: con `-screen-width 2560 -screen-height 1440 -desk-nofit` Windows limita el ancho a 1920, así que la captura es 1920x1440 (escala 1,333 como en 2560x1440, aunque con 1440x1080 lógicos en vez de 1920x1080). Pantalla completa forzada a 2560x1440 tampoco es posible con este monitor.
- `MainPanelSettings.asset` no queda modificado en disco.

### 6.6 Pendiente / no verificado
- Clic real con ratón (hover, arrastre del borde de la ventana hasta el mínimo 1280x720) no se automatizó: lógica verificada por código y Play; el mínimo por sondeo puede parpadear al arrastrar.
- Las capturas por `CopyFromScreen` (SendKeys) salían desfasadas; las definitivas son las del propio juego (`ReadPixels`).
- Tarjetas RENDER MODE/ENVIRONMENT sin icono (2ª parte de AUX-U09) y color del título "SELECT A PART": sin tocar.
- Escala de Windows (125/150 %) no probada; la escala depende de la altura del área cliente.
- U1 (ficha lateral), U6 (cámara), U7 (hero/onboarding de escritorio), U8 y U10 siguen pendientes (2B y siguientes).

## 7. Fase 2B — resultados (2026-11-03, Unity 6000.6.5f1, rama `pc-desktop`)

Alcance: layout de escritorio (U1), cámara de escritorio (U6) e inicio/tutorial de PC (U7). Todo va bajo `#if UNITY_STANDALONE` o en `Assets/Resources/UI/DesktopUI.uss` (solo se carga en Standalone): la escena, `MainLayout.uxml` y `Theme.uss` no cambian.

### 7.1 U1 — ficha lateral (clase `.layout--desktop` en el Root)
- `DesktopSidePanel` (nuevo, `Assets/Scripts/UI/Desktop/`) añade `.layout--desktop` al Root y convierte el `BottomSheet` en un **panel acoplado a la derecha**: tarjeta con esquinas redondeadas, 16 px de margen, desde la cabecera (y=112) hasta 16 px del borde inferior, **ancho = 34 % del ancho lógico entre 380 y 480 px** (1280x720: 473; 1920x1080: 479; con la escala de la 2A son px lógicos). Foldouts y botón PART INFO se conservan; el handle y los gestos de "arrastrar hacia abajo para cerrar" no se registran en PC.
- **Entrada: pastilla "PART INFO" pequeña abajo a la derecha** (simétrica a la pastilla ATAJOS de abajo a la izquierda), no apertura automática. Motivo: abrir el panel en cada clic movería y recortaría la vista (480 px) cada vez que se selecciona una pieza; así la vista solo se aparta cuando el usuario pide la ficha. Al deseleccionar (clic en el fondo, Esc, Reset) el panel se cierra para no mostrar datos de la pieza anterior.
- **Cápsula y subpaneles:** la `BottomBar` (cápsula + tarjetas + CUT/FILTER/STUDIO/ENVIRONMENT) mueve su borde derecho el ancho del panel, así que queda **centrada en el área libre** (abajo al centro del espacio sin panel) y nunca lo solapa. En PC el panel y los modos **conviven**: se quitó el cierre mutuo (`UIManager.OnAnyModeActivated` y `UIModeController.SetSheetOpenState`, solo Standalone). La leyenda térmica pasa a la izquierda del panel y los pins no se dibujan bajo él (4º rectángulo de máscara en `SmartHotspot`). `InputManager` sigue bloqueando la cámara sobre la cápsula/paneles con el panel abierto (`Panel.Pick`).
- **Cámara (AUX-U05):** `OrbitCameraController.SetDesktopViewShiftX(ocupado / (2·ancho))` desplaza la cámara en paralelo (sin girar) para que la pieza quede centrada en el área libre. Medido en el .exe: pieza a 408 px con centro del área libre en 408 px (1280x720) y 716 frente a 717 (1904x984); centro de la cápsula = centro del área libre; 0 solapes cápsula/subpaneles con el panel.
- **Lectura (AUX-U06):** contenido del panel de ~430 px con tipografía 14-15 px: **~60 caracteres por línea** en la descripción (antes ~200); etiquetas de datos en columna fija de 112 px y valores alineados a la izquierda.

### 7.2 U6 — cámara de escritorio (`OrbitCameraController.Desktop.cs`, parcial, solo Standalone)
- **Doble clic** en una pieza = seleccionar y **enfocar** (`UIManager.TryDesktopDoubleClickFocus`). Decisión: en PC el doble clic ya no aísla (el aislamiento sigue en el botón ISOLATE y la tecla `I`); en WebGL/táctil se conserva. El doble clic en el fondo sigue retrocediendo el aislamiento.
- **Rueda hacia el cursor:** F' = F + (1 − r)(H − F) (r = nueva/antigua distancia, H = punto bajo el cursor: raycast o plano del foco). Comprobado con deriva de **0,00 px** a 1280x720 y 1904x984 (dos cursores y zooms de 18 m a 4 m).
- **Órbita con clic izquierdo sobre el fondo** (sin pieza bajo el cursor ni UI): umbral de 6 px; el clic simple sin arrastre sigue seleccionando/deseleccionando (`SelectionManager` aplaza la deselección del fondo hasta soltar y solo si no hubo arrastre). **Pan** con clic central y con **Shift + clic derecho**; el clic derecho sigue orbitando y la amortiguación es la existente (el foco usa la misma velocidad que el zoom mientras dura el zoom al cursor).
- Verificado con ratón real en el .exe (SetCursorPos + mouse_event) a 1280x720: arrastre izquierdo sobre fondo orbita y la selección se conserva; clic en el fondo deselecciona; doble clic enfoca el brazo FR; rueda acerca (zoom al cursor); Shift+derecho desplaza. Trazas `[DesktopUI] camera:` en `capturas_fase2b/mouse_exe_1280x720_Player.log`. El overlay de ayuda (`?`) incluye ahora las filas de ratón nuevas y pasa WINDOW a la columna izquierda.

### 7.3 U7 — inicio y tutorial
- **Tutorial:** en Standalone `_isTouchDevice = Application.isMobilePlatform` (antes `|| Input.touchSupported`, que en este PC da `true`: arrancaba en la rama táctil). Textos "click"/"double click" y los controles nuevos (arrastrar el fondo, Shift+derecho, rueda al cursor, `I` para aislar). El interruptor PC/móvil sigue funcionando. Mientras el tutorial está abierto se ocultan la cápsula y la cabecera de detrás (se transparentaban entre los botones, AUX-U07).
- **Hero:** se mantiene tal cual (título glitch y pills iguales, centrados y simétricos; cabe en 1280x720). Se probó un fondo translúcido con el dron vivo detrás y no queda más limpio (se ve la cápsula y la cabecera del visor a través, y UI Toolkit no tiene desenfoque), así que se descartó.

### 7.4 Qué cambia en WebGL
Nada en el comportamiento: las clases/estilos de escritorio no existen en WebGL (`DesktopUI.uss` no se carga) y el código nuevo está bajo `#if UNITY_STANDALONE`. Cambios inertes compartidos: `OrbitCameraController` pasa a `partial`, `SmartHotspot.HeaderMaskRects` de 3 a 4 elementos (el cuarto solo lo usa PC) y un campo estático `IsSideDocked` (false) en `UIDetailsSheet`. Compilación: 0 errores en Standalone y en WebGL; plataforma activa al terminar: WebGL. No se compiló WebGL en `docs/`. Control en Play con WebGL: `capturas_fase2b/99_webgl_control.png` (la hoja inferior sigue al 55 %).

### 7.5 Verificación y archivos
- Build Windows x86_64 Mono: `E:\TwinSight_builds\pc_fase2b\TwinSightX500.exe`. Capturas (1280x720 y 1904x984, la ventana de "1920x1080" ajustada al área de trabajo como en la 2A) en `E:\twinsight-web-twin\docs\unity_pc\capturas_fase2b\`: `01_viewer`, `02_panel_selected`, `03_panel_foldout`, `04_panel_analyze` y `04b_panel_analyze_cut` (panel + subpanel de ANALYZE), `05_hero`, `06_tutorial_step1`, `07_selected_peek` (PART INFO cerrado), `10-12_mouse_*` (ratón real) y los logs de medición `demo2b_*.log` (`-desk-demo2b <carpeta>`, `DesktopCaptureDemo2B`).
- Pendiente / no verificado: la rueda del ratón real (se probó con `mouse_event`, la posición solo es válida tras un movimiento); escala de Windows al 125/150 %; ventana en 21:9; el panel deja ver a través las zonas HDR muy brillantes del dron (fondo al 94 %).

## 8. Fase 3A — resultados (2026-11-03, Unity 6000.6.5f1, rama `pc-desktop`)

Alcance: realismo del dron en PC (HDRI, SSAO, shader PBR, materiales por categoría, rig de estudio) y dos arreglos visibles de la 2B. Todo va bajo `#if UNITY_STANDALONE`, en `Assets/Resources/PC` (materiales, texturas y shader que solo se asignan en Standalone) o en `Assets/Settings/PC`. Menú de generación idempotente: `TwinSight > PC > Build PC Realism (fase 3A)` (`Assets/Editor/PC/PCRealismSetup.cs`).

### 8.1 Qué se hizo
| # | Resultado |
|---|---|
| 1a placa "quemada" | Causa: los 6 paneles emisivos de `PCStudioReflections` (key x3,0, tiras x3,6-4,5...) reflejados por una placa plana con metallic 0,6 / smoothness 0,6, más bloom y exposición. Ahora el carbono es dieléctrico (metallic 0, smoothness 0,42) con clear-coat (smoothness 0,7), el entorno viene del HDRI y los paneles se retiran (`PanelWeight = 0`): sin zonas blancas saturadas (capturas 01, 02 y 08). Los paneles siguen en el código y se pueden reactivar con `PCStudioReflections.PanelWeight`. |
| 1b panel lateral | `.layout--desktop .details-sheet` pasa de `rgba(0,0,0,0.94)` a `rgba(5,7,11,0.98)` en `DesktopUI.uss` (solo PC). El dron ya no se transparenta (captura 09). |
| 2 HDRI | `studio_small_09_2k.hdr` (ver 8.2) importado como cubemap (1024 por cara). Se asigna como skybox de la escena (`RenderSettings.skybox`, material `Resources/PC/PC_HDRISky.mat`, shader `Skybox/Cubemap`): da el **ambiente** (SH, `ambientMode = Skybox`, intensidad 1,05) y el **Reflection Probe** (clear flags Skybox). El fondo visible sigue siendo el degradado: se pasa a la cámara con un componente `Skybox` (tiene prioridad sobre `RenderSettings.skybox`) y `EnvironmentController` no pisa el skybox ni el ambiente en PC (`PCStudioReflections.OwnsEnvironment`). El probe sigue refrescándose con `OnEnvironmentChanged`: en cada refresco el HDRI rota con la luz clave, la exposición sigue a la intensidad del preset (x1,2) y el tinte al color de la luz. |
| 3 G9 SSAO | Feature `ScreenSpaceAmbientOcclusion` (`PC_SSAO`) en `PC_Renderer.asset`: Interleaved Gradient, fuente Depth Normals, radio 0,22 (el dron mide ~10 m, los tornillos 0,15-0,7), intensidad 1,2, luz directa 0,2, After Opaque desactivado (así el shader recibe `_SCREEN_SPACE_OCCLUSION`). Se enciende solo en **High y Ultra** (`PCStudioReflections.ApplySsaoForTier`, `SetActive`). `ClippableLit` y `DroneLitPC` declaran `multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION`; `ShaderVariantStripper` elimina esa keyword de `ClippableLit` en WebGL (las variantes de WebGL son las de antes). |
| 4 G6 shader | Nuevo `TwinSight/DroneLitPC` (`Assets/Shaders/PC/DroneLitPC.shader`), extensión de `ClippableLit`: mismas propiedades, mismo recorte global CUT (planos `_GlobalClipPlane/2`, también en ShadowCaster, DepthOnly y DepthNormals) y mismo realce de selección por MPB. Keywords nuevos, apagados por defecto: `_PC_METALLICMAP` (metallic R / smoothness A), `_PC_ROUGHNESSMAP` (p. ej. `final_curve_roughness.png`), `_CLEARCOAT` (2ª capa especular nativa de URP), `_PC_DETAIL` (normal de detalle con tiling), `_PC_EMISSION`, `_PC_TRANSLUCENT` (translucidez de borde). Además SSAO, sombras de luces adicionales y sombras suaves de URP 17. `exclude_renderers gles gles3`: no se compila para WebGL. `CrossSectionManager.CreateClippableCopy` no degrada a ClippableLit los materiales `DroneLitPC` (CUT sigue recortando, captura 04); X-Ray, Térmico y Wireframe son shaders propios y no cambian (capturas 05-07). |
| 5 G7 materiales | 18 materiales en `Resources/PC/Materials` y `PCMaterialSwapper` (runtime, solo Standalone), que mapea por material original y nombre de renderer y pide a `ViewModeManager` reconstruir su caché. **GRAY63 (160 fasteners) -> `PC_ScrewZinc` (114), `PC_NutZinc` (36), `PC_Nylon` (10)**; carbono: `PC_CarbonPlate` (placas, brazos, BAKE_PREVIEW.001: dieléctrico + clear-coat; el trenzado es el ORIGINAL del atlas) y `PC_CarbonTube`; `PC_AnodizedBlue` (metallic 0,85, smoothness 0,58), `PC_AnodizedBlack`, `PC_Motor`, `PC_Prop` (plástico semimate con translucidez de borde), `PC_PlasticBlack`, `PC_Rubber`, `PC_PCB` (mate), `PC_Steel`, `PC_Housing.00x`. **Ajuste del carbono:** la normal de trenzado procedural (sarga 2x2, 512 px, `pc_carbon_weave_n.png`, la sigue generando el script de Editor y el shader conserva `_PC_DETAIL`) quedó demasiado gruesa y brillante (aspecto de cuerda con destellos en los tubos). Se **desactivó** en los materiales de carbono: el atlas original ya trae el twill fino en albedo y normal (con `_BumpScale` 0,25), que es más fiel a un X500 real y no genera destellos de alta frecuencia; de la 3A solo se conserva el clear-coat (smoothness 0,7). Comparación con la fase 1 a la misma vista: capturas 02 (placa) y 08 (tubos). 251 de 258 renderers cambian de material. `final_curve_roughness.png` queda soportada por el shader pero **no conectada** a ningún material (sin ganancia visible; queda para G8). PCB sin pistas brillantes: no hay UV propia (atlas común). Los .mat de WebGL no se tocan. |
| 6 G10 rig | `EnvironmentController` (solo PC): fill 7000 K, rim 8500 K, top 5600 K, bounce 4300 K (`Mathf.CorrelatedColorTemperatureToRGB`, mezclados con el tinte del preset) y softboxes con penumbra amplia (cono interior = 30 % del exterior). Presets y hora siguen funcionando (capturas 10-16: Studio, Studio Light, Day, Sunset, Night, Blue, White). |

### 8.2 HDRI
`studio_small_09_2k.hdr`, **Studio Small 09** (Sergej Majboroda), https://polyhaven.com/a/studio_small_09, descargado de `https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/studio_small_09_2k.hdr`, **6,3 MB** (SHA-1 `3558b7e49a8a54b6971ef768c59c3a427ee81f60`), licencia **CC0 1.0** (Poly Haven). Ruta: `Assets/Settings/PC/HDRI/` (versionado; atribución en `ATTRIBUTION.txt` de esa carpeta). El override del importer para WebGL lo reduce a 16 px (no se usa allí).

### 8.3 FPS (corregido; RTX 5060 Ti, i7-5820K, 1920x1080 ventana, D3D11, `-vsync 0`, visor abierto con `-perf-viewer` a los 8 s, ventanas de 10 s, misma sesión y mismo método)
La medición anterior (~360) no se pudo confirmar. Ahora cada ejecución guarda una captura del propio juego en mitad de la medición (`-perf-shot`; `capturas_fase3a2/perf_PC_Ultra_visor.png` y `perf_PC_Low_visor.png`) que confirma el visor abierto con el dron en pantalla.

| Nivel | FPS media (ventanas 2-9) | rango de medias | p1 (rango) | mínimo |
|---|---|---|---|---|
| **PC_Ultra** | **~324** | 314-340 | 150-157 | 4-133 (tirones aislados) |
| **PC_Low** (referencia) | **~421** | 368-446 | 177-232 | 4-84 |

Salvedades: la vista por defecto del visor es lejana (el dron ocupa poco de la pantalla), así que los FPS son muy altos y la app está limitada por CPU; por eso la diferencia entre Low y Ultra es solo de ~23 %. Frente a la fase 1 (145 de media, vista y método de apertura no documentados) las cifras no son comparables; lo válido es la comparación Ultra frente a Low de esta sesión. Logs: `E:\TwinSight_builds\pc_fase3a2\logs\perf_PC_*.log` (copias en `capturas_fase3a2\`). Build: `E:\TwinSight_builds\pc_fase3a2\TwinSightX500.exe` (la carpeta `pc_fase3a` conserva la build anterior).

### 8.4 Aislamiento de WebGL
- `URP_AutoFixed.asset`, `URP_HighFidelity_Renderer.asset`, `MainScene_Final.unity` y los `.mat` de `Assets/Models/Materials`: sin cambios (`git diff` vacío). Cambios compartidos e inertes: `ClippableLit.shader` (+1 `multi_compile_fragment _SCREEN_SPACE_OCCLUSION`, eliminado en WebGL por `ShaderVariantStripper`), `ShaderVariantStripper` (strip total de `TwinSight/DroneLitPC` en WebGL), `UniversalRenderPipelineGlobalSettings.asset` (al registrar la feature Unity añadió a `m_RuntimeSettings` los recursos de SSAO; "Strip Unused Post Processing Variants" sigue DESMARCADA) y `PerfLogger` (solo Standalone).
- `Resources/PC` entra en cualquier build, pero el shader se elimina en WebGL y las texturas (HDRI, trenzado) se reducen a 16-32 px por override del importer.
- Compilación: 0 errores en Standalone y en WebGL. Plataforma activa al terminar: WebGL.

### 8.5 Capturas
`E:\twinsight-web-twin\docs\unity_pc\capturas_fase3a\` (generadas por el propio .exe con `-desk-demo3a`, 1920x1080): 01 vista general, 02 primer plano de carbono, 03 primer plano de motor, 08 pieza seleccionada, 09 panel lateral abierto, 04 CUT, 05 X-Ray, 06 Térmica, 07 Wireframe, 10-16 presets (Studio, Studio Light, Day, Sunset, Night, Blue, White) y `99_webgl_control.png` (control de WebGL).

### 8.6 Pendiente / no verificado
- G8 (texturas 4K/8K y conectar la rugosidad real), anisotropía del aluminio y PCB con pistas (necesita UV propia) quedan para la 3B.
- La captura 03 muestra el motor, no un primer plano limpio del aluminio azul (se ve mejor en 08 y 09).
- Las vistas de las capturas son equivalentes a las de la fase 1, no idénticas.
- El apagado de SSAO al bajar de tier en caliente solo se comprobó por código.
- La build WebGL real no se compiló en `docs/` (como en fases anteriores).

## 9. Pulido de materiales (2026-11-04, rama `pc-desktop`, solo PC)

Todo en `Assets/Resources/PC`, `Assets/Shaders/PC/DroneLitPC.shader`, `PCRealismSetup` (Editor) y `PC_Renderer` (SSAO de PC). WebGL no cambia (ver 9.5). Se regenera con `TwinSight > PC > Build PC Realism (fase 3A)` (idempotente). Capturas ANTES/DESPUES (PC_Ultra, mismas vistas, 1920x1080, sin interfaz; ver 9.6) generadas con `PCMaterialShots` (`-desk-matshots <carpeta> <antes|despues>`).

### 9.1 Carbono plano: causa y solucion
- **Causa.** El atlas del dron (`final_curve_*`) lleva el twill como imagen en el albedo (4096 px) y la normal `final_curve_normal_resultante.png` lo lleva con muy poca amplitud, con una inclinacion media (x,y ~ -0,2) por el bake. En PC se le bajo la `_BumpScale` a 0,25 (para no inclinar toda la luz) y el importer la deja en 2048 px con mips: el relieve casi desaparece y el trenzado queda "impreso" sobre una superficie lisa. Tampoco se usaba `final_curve_roughness.png` (en la zona de carbono es un gris constante 0,75: no aporta informacion).
- **Solucion.** (1) Nuevo keyword `_PC_ALBEDOBUMP` en `DroneLitPC`: relieve por diferencias finitas de la luminancia del albedo (4 muestras, paso en texels). Va alineado al twill original y al mismo tamano de trenzado (no hay textura nueva ni cambia la escala), es de media cero (no inclina la luz) y se filtra solo con los mips. Carbono: escala 2,0, paso 1,5 texels. (2) Variacion de brillo por hebra (`_AlbedoBumpSpec` 0,3: las hebras altas son algo mas pulidas, los huecos mas mates). (3) Brillo anisotropo falso `_PC_ANISO` (Kajiya-Kay en la luz principal, con la direccion de hebra perpendicular al gradiente del relieve), muy suave (0,18). (4) La normal original del atlas se conserva a 0,35 (antes 0,25) y se mantienen AO y curvatura. (5) El clear-coat queda como capa encima (smoothness 0,72) sin aplastar el relieve. Antes/despues: capturas 01, 02, 03, 04, 15.
- Descartado: normal procedural gruesa de la 3A (cuerda con destellos); un normal map nuevo de 4096 px (+20 MB en el repo) frente a derivar el relieve del albedo; mapa de rugosidad del carbono (constante).

### 9.2 Tornillos temporales (GRAY63): causa y solucion
- **Causa.** El `GRAY63` original usa **`Hex_AO.png`** (albedo, AO y curvatura: hexagono gris sobre blanco, 256 px) y **`Hex_Normal_Atlas.png`** (aristas del hexagono). Las cabezas llevan UV propia con ese hexagono. En la 3A, `PCRealismSetup` creo `PC_ScrewZinc`, `PC_NutZinc` y `PC_Nylon` con `atlas = false`, que **anulaba `_BaseMap`, `_BumpMap`, `_CurvatureMap` y `_OcclusionMap`**: se perdio la textura, no era el HDRI ni la exposicion. Ademas el color 0,8 con metallic 1 reflejaba el HDRI casi blanco (cabezas planas y claras) y el SSAO con muestreo medio dejaba grano negro en los contactos.
- **Solucion.** Los tres materiales vuelven a usar `Hex_AO`/`Hex_Normal_Atlas` (bump 1,6, AO y curvatura bajas). Tornillos y tuercas: acero pavonado/gunmetal oscuro (albedo 0,04, metallic 0,85, smoothness 0,34-0,36, borde de curvatura +0,10 para el leve brillo). Nylon: se mantiene el color (blanco roto) y se hace mate (smoothness 0,18); no se pudo confirmar el color real en el X500 V2. SSAO de PC: 12 muestras, normales High y ruido Interleaved Gradient (en esta version del enum `BlueNoise = 0`, `InterleavedGradient = 1`; la 3A tenia el comentario al reves). Capturas 04, 05, 16.
- Limite: las cabezas son cilindros de 12 lados (malla de baja resolucion): el hueco es de textura (normal + AO), no geometria; a muy poca distancia se nota. Las tuercas son hexagonales de malla y su cara no usa la UV del hexagono.

### 9.3 Los 18 materiales de PC frente a su original de WebGL
Mapas del original (todos salvo GRAY63 usan el atlas: `diffuse_union` 4096, `normal_resultante` 2048 en import, `curve` 2048, `ao` 2048; ninguno trae mapa de emision ni de metallic). "Antes" = fase 3A. La emision de LEDs/pantallas no existe como mapa en el original; las serigrafias van en el albedo del atlas (se ven nitidas en 08 y 09), asi que no hay emision que restaurar.

| # | Material PC | Original (WebGL) | Antes (3A) | Despues (pulido) |
|---|---|---|---|---|
| 1 | PC_CarbonPlate | carbon_fiber_01 / BAKE_PREVIEW.001 (atlas) | atlas, normal 0,25, clear-coat 0,7, sin relieve | + relieve por albedo (2,0), brillo por hebra, anisotropo suave, normal 0,35, clear-coat 0,72; AO y curvatura del atlas |
| 2 | PC_CarbonTube | MAT_Plastic_Black (tubos) | igual que 1 | igual que 1 |
| 3 | PC_AnodizedBlue | MAT_Plastic_Blue | atlas, metallic 0,85, smooth 0,58 | + mecanizado fino (`pc_brushed_n`, escala 0,15), rugosidad real al 25 % |
| 4 | PC_AnodizedBlack | MAT_Alum_Black (Pixhawk/IMU) | atlas, metallic 0,9 | + mecanizado 0,10, rugosidad real al 25 % |
| 5 | PC_Motor | BAKE_PREVIEW.007 (campana) | atlas, metallic 0,8 | metallic 0,9, smooth 0,5, mecanizado 0,12, rugosidad real 25 % |
| 6 | PC_Prop | MAT_Plastic_Black (helices) | atlas + translucidez de borde | sin cambios |
| 7 | PC_PlasticBlack | MAT_Plastic_Black | atlas | sin cambios (serigrafia y radio nitidos en el albedo) |
| 8 | PC_Rubber | MAT_Rubber / MAT_Rubber_black | atlas, smooth 0,15 | sin cambios |
| 9 | PC_PCB | MAT_Electronics / BAKE_PREVIEW.003 | atlas, metallic 0,1 | sin cambios (sin UV propia para pistas) |
| 10 | PC_Steel | MAT_Steel | atlas, metallic 1 | + mecanizado 0,10, rugosidad real 20 % |
| 11 | PC_ScrewZinc (114) | GRAY63 (Hex_AO + Hex_Normal_Atlas) | **sin texturas**, color 0,8 | Hex_AO/Hex_Normal restaurados, gunmetal oscuro |
| 12 | PC_NutZinc (36) | GRAY63 | **sin texturas**, color 0,7 | Hex restaurados, gunmetal oscuro |
| 13 | PC_Nylon (10) | GRAY63 | sin texturas, smooth 0,3 | Hex restaurados, mate (0,18) |
| 14 | PC_Housing.002 | BAKE_PREVIEW.002 | atlas | sin cambios |
| 15 | PC_Housing.004 (bateria) | BAKE_PREVIEW.004 | atlas | sin cambios |
| 16 | PC_Housing.005 (GPS) | BAKE_PREVIEW.005 | atlas | sin cambios |
| 17 | PC_Housing.006 (radio) | BAKE_PREVIEW.006 | atlas | sin cambios |
| 18 | PC_Housing.008 (bobinado) | BAKE_PREVIEW.008 | atlas, metallic 0 | metallic 0,7, smooth 0,45 (cobre) |

`final_curve_roughness.png` queda conectada solo a los metales (3, 4, 5, 10) con fuerza 0,2-0,25: en el resto es casi constante. Las gomas y los cables (MAT_Rubber) ya tenian todos los mapas del original y no mejoraron al tocarlos.

### 9.4 Cambios de motor
- `DroneLitPC`: `_PC_ALBEDOBUMP` (+`_AlbedoBumpScale/_AlbedoBumpStep/_AlbedoBumpSpec`) y `_PC_ANISO` (+`_AnisoStrength/_AnisoShininess/_AnisoColor`). Apagados por defecto. El shader no se compila para WebGL.
- `PCRealismSetup`: nuevo `GenerateBrushed()` (`pc_brushed_n.png`, 256 px tileable, 32 px en WebGL; el PNG esta excluido por `.git/info/exclude` y se versiona con `git add -f`), Def con mapas Hex, rugosidad y detalle por material, SSAO de PC afinado. `PCMaterialShots` (solo Standalone) para las capturas.
- Los 5 `PC_*.asset` (URP de PC) solo cambian los `m_PrefilterSSAO*` que Unity recalcula con el nuevo ruido y muestreo.

### 9.5 Aislamiento de WebGL
`git diff` vacio en `Assets/Models`, `Assets/Materials`, `Assets/Scenes` y `URP_AutoFixed.asset` (0 lineas). `UniversalRenderPipelineGlobalSettings.asset` cambio solo al compilar para Standalone (Unity quita una entrada de `m_RuntimeSettings`) y se restauro con `git checkout`; no se incluye en el commit. Control en Play con WebGL activo: 0 errores, 289 materiales, 0 con `DroneLitPC` (`capturas_materiales/99_webgl_control.png`, pantalla de inicio). Compilacion: 0 errores en Standalone y en WebGL; plataforma activa al terminar: WebGL. No se compilo WebGL en `docs/`.

### 9.6 Capturas y build
`E:\twinsight-web-twin\docs\unity_pc\capturas_materiales\` (ignoradas por git, como las de las fases previas): `01_carbono_placa`, `02_carbono_tubo`, `03_carbono_placa_inferior` (luz rasante, 14 grados), `04_tornillos_cabeza`, `05_tuerca`, `06_aluminio_motor`, `07_motor`, `08_pixhawk`, `09_radio`, `10_bateria`, `11_patas_gomas`, `12_vista_general`, `13_vista_media`, `14-16_entorno_Day|Sunset_*` (general, carbono, tornillos), cada una `_antes` y `_despues`. Build Windows x86_64 Mono: `E:\TwinSight_builds\pc_materiales\TwinSightX500.exe` (prueba con `-desk-matshots`: 251 de 258 renderers con material PC, nivel Ultra).

### 9.7 Pendiente / no verificado
- Las capturas salen del Editor (Play, plataforma Standalone) a 1920x1080 sin interfaz; el .exe se probo con las mismas vistas a 1600x900 (carbono y tornillos equivalentes). A 0,6 m el reflejo de la luz clave lava la placa mas en el .exe que en el Editor.
- El anisotropo es falso (no hay tangentes de hebra en la malla): suma poco; si se quiere real, habria que bakear tangentes.
- Sin emision de LEDs ni de pantallas: el original no tiene mapas de emision; hacerlos exigiria pintarlos por script sobre el atlas (queda para la 3B junto a las texturas 4K/8K).
- Color real del nylon del X500 V2 sin confirmar.

## 10. Fase 3B (2026-11-04, rama `pc-desktop`, solo PC)

Alcance: retoques de materiales (carbono oscuro, tornillos redondos), arbol de piezas con busqueda (U8), enlace profundo `-part` (U10), modos de vista en alta calidad (G11) y texturas 4K (G8). Todo bajo `#if UNITY_STANDALONE`, en `Assets/Resources/PC`, `Assets/Shaders/PC` o en overrides de importacion de Standalone. Capturas: `E:\twinsight-web-twin\docs\unity_pc\capturas_fase3b\`. Build: `E:\TwinSight_builds\pc_fase3b\TwinSightX500.exe`.

### 10.1 Materiales
- **Carbono (`DroneLitPC`, `PC_CarbonPlate`/`PC_CarbonTube`).** Nuevas propiedades (apagadas por defecto): `_AlbedoMax` (techo suave del albedo, solo con `_PC_ALBEDOBUMP`), `_BumpFilter` (el relieve derivado del albedo se atenua cuando un texel es menor que un pixel: menos destellos ruidosos), `_SmoothnessMax`, `_SpecAA` (Toksvig a partir del gradiente del relieve), `_SpecDark` (multiplica la oclusion: menos reflejo de entorno) y `_FinalDark` (multiplicador final de iluminacion). Carbono: smoothness 0,30 (max 0,45), clear-coat 0,85/0,62, anisotropia 0,07, `_AlbedoMax` 0,045, `_SpecDark` 0,45, `_FinalDark` 0,5 (placa) y 0,75 (tubo). El relieve de sarga (albedo bump 2,0) se mantiene.
- **Por que el .exe se veia mas claro que el Editor.** No se pudo aislar la causa exacta: en el .exe la placa quedaba en gris plano (~sRGB 102) aunque se pusiera el color base a negro, se quitaran luces o ambiente, o se bajara el techo del albedo, mientras que la misma configuracion se veia oscura en el Editor. Descartado: material equivocado (el renderer tiene `PC_CarbonPlate`/`DroneLitPC`, sin MaterialPropertyBlock), variante sin keyword, textura (misma importacion). El multiplicador `_FinalDark` es una solucion directa que oscurece el resultado en el .exe; queda pendiente entender el origen (se sospecha de la reflexion del HDRI a baja inclinacion y del brillo de las luces adicionales).
- **Tornillos (`PCScrewSmoother`, en `PCMaterialSwapper.cs`).** Las normales de la pared ya eran radiales; lo que se veia facetado era la silueta de 12 (o 8) lados. En runtime y solo en PC se copia la malla y se subdivide dos veces, empujando los puntos medios del anillo exterior a la circunferencia (12 -> 48 lados; tapa y hueco hexagonal con su UV `Hex_AO`/`Hex_Normal_Atlas` intactos, sin grietas). Se omiten mallas de 4-6 puntos de anillo (tuercas hexagonales, nylon). 82 de 160 fasteners redondeados (15 mallas, ~1400 triangulos cada una). No se toca el FBX ni WebGL.

### 10.2 U8 - arbol de piezas (`DesktopPartTree.cs`)
Panel izquierdo (340 px, simetrico al panel derecho; `Ctrl+F` o `/` lo abren y enfocan la busqueda, `Esc` limpia/cierra, flechas + Enter) y pastilla "PIEZAS" junto a "ATAJOS". Jerarquia del catalogo bilingue: categorias (Estructura, Propulsion, Avionica y energia, Fijaciones) -> pieza -> subpiezas; los grupos sin objeto propio (p. ej. "Conjunto de brazo") solo pliegan. Busqueda sin acentos y sin mayusculas, por palabras, en ES/EN y alias. El clic usa `DeepLinkSelector.RequestSelect` (mismo camino que el enlace profundo, no abre la ficha: solo selecciona y enfoca); la seleccion hecha en la escena resalta la fila (`PartSelectedEvent`) y abre su rama. La capsula se centra en el area libre (como con el panel derecho).

### 10.3 U10 - enlace profundo
`TwinSightX500.exe -part <entryId> [-lang es|en]` (tambien acepta un primer argumento `twinsight://part/<id>`). Reutiliza `DeepLinkSelector` (se activa solo en Standalone con la linea de comandos). Verificado: `-part x500v2_gps_m10 -lang es` abre en espanol con el modulo GPS seleccionado y la ficha abierta (`05_deeplink_part_gps_screen.png`). **Protocolo `twinsight://` (solo documentacion, no se escribe nada en el registro):** para registrarlo el usuario puede importar un `.reg` con `HKEY_CURRENT_USER\Software\Classes\twinsight` -> `URL Protocol=""`, `shell\open\command` = `"C:\ruta\TwinSightX500.exe" "%1"`; Windows pasa la URL como primer argumento y el codigo extrae el id.

### 10.4 G11 - modos de vista
`TwinSight/XRayPC` (mezcla aditiva: no depende del orden de dibujo; borde fresnel HDR con el bloom del perfil) y `TwinSight/WireframePC` (grosor en pixeles con antialiasing, distancias a aristas `noperspective`, atenuacion en triangulos de pocos pixeles para que las zonas densas no formen una mancha solida, aditivo). Mismo recorte global CUT. `ViewModeManager` los carga solo en Standalone desde `Resources/PC/Materials/PC_XRay|PC_Wireframe`; el resto de modos no cambia. SSAO no afecta a los modos transparentes (se aplica en el shader de los materiales opacos). Strip total de ambos shaders en WebGL (`ShaderVariantStripper`). Capturas `10_modo_xray` ... `15_modo_thermal`, `*_cerca` y antes (`V_*_ANTES`).

### 10.5 G8 - texturas
Override de importacion **Standalone** (no afecta a WebGL, que ya tenia su propio override) para `final_curve_{diffuse_union,normal_resultante,ao,curve,roughness}`: 4096 px, compresion HQ (BC7/BC5), sin crunch. El build pasa de ~116 a ~234 MB. Sin descargas.

### 10.5b FPS (RTX 5060 Ti, 1920x1080 ventana, vsync 0, visor abierto con `-perf-viewer`, ventanas de 10 s, logs en `E:\TwinSight_builds\pc_fase3b\logs\`)
| Nivel | FPS media (ventanas 3-14) | p1 | referencia 3A |
|---|---|---|---|
| PC_Ultra | ~107 (101-112) | ~63 | ~324 |
| PC_High | ~96 (90-100) | ~57 | n/d |
La bajada frente a la 3A (324) no se aislo: entre la 3A y ahora entraron el pulido de materiales (relieve por albedo: 4 muestras extra de una textura de 4096), texturas 4K con BC7, ~82 mallas de tornillo con 1400 triangulos y el arbol de piezas. Pendiente medir cada cambio por separado.

### 10.6 No hecho / pendiente
- Opcional 7 (contorno #bfeaff y atenuacion de no seleccionados): no se hizo.
- Causa raiz del gris del carbono en el .exe (ver 10.1): en pruebas, ni `_FinalDark`, ni `_AlbedoMax`, ni el color base negro, ni quitar el post-procesado cambiaron el gris de la placa (sRGB ~95-100) en el .exe; la placa se ve oscura en otros angulos (`04_arbol_resaltado_gps`).
- Aislar el coste de FPS de la fase.
- Aislamiento WebGL: `git diff 4ce5ab9` sin cambios en .mat originales, `URP_AutoFixed`, escena ni calidad WebGL; los .meta de las 5 texturas `final_curve_*` solo cambian el bloque Standalone (y 2 campos de sprite que Unity anade). Control en Play con WebGL activo: 289 materiales, 0 con shaders PC, sin `_DesktopUI`, asset `URP_AutoFixed` (`99_webgl_control.png`). `UniversalRenderPipelineGlobalSettings.asset` se restauro con `git checkout`.
- 82 de 160 tornillos redondeados (el resto tiene anillos de 4-8 puntos u otra forma).
- La fila resaltada del arbol no siempre se desplaza hasta la vista si el arbol estaba cerrado al seleccionar.

## 11. Fase 3B.1 (2026-11-05, rama `pc-desktop`, solo PC)

### 11.1 Placa superior gris: era el HOVER (confirmado por el usuario)
- **Causa.** `HighlightSystem` aplica al pasar el raton `hoverColor` = blanco (1,1,1) con intensidad 0,28 por MaterialPropertyBlock (`_HighlightColor`/`_HighlightIntensity`). `DroneLitPC` lo sumaba como `color += _HighlightColor * h * (0,35 + 0,85*rim)`: un relleno blanco de ~0,1 lineal (sRGB ~90-100) sobre una placa que mira hacia arriba (rim ~0). Por eso no dependia del albedo, del clear-coat ni del entorno, y desaparecia al desactivar el renderer. En el Editor guiado por MCP no hay raton real y nunca se activaba. Fue ademas la razon de la serie de sondas anteriores (color base negro, probe0, etc.): ninguna tocaba el realce.
- **Arreglo (solo PC, `DroneLitPC.shader`).** El realce pasa a un borde celeste (#bfeaff mezclado al 60 % con el color del realce) con relleno casi nulo: `color += hc * h * (0,03 + 1,1*rim)`, y el oscurecimiento del albedo baja de 0,30 a 0,10. WebGL usa `ClippableLit` (sin cambios). Comprobado en el .exe con `HighlightSystem.OnHoverEnter` sobre la placa superior: `capturas_fase3b19_hover_placa_cenital`, `19_hover_placa_media` y `19_sin_hover_placa_media` (carbono oscuro con y sin hover, hover como borde fino).
- **Relieve.** `_AlbedoBumpScale` 2,0 -> 1,0 (sarga fina en vez de chapa estriada).
- **Compatibilidad SRP Batcher.** Los pases ShadowCaster/DepthOnly/DepthNormals de `DroneLitPC` no declaraban el cbuffer `UnityPerMaterial` (con `_ClipPlane` suelto); ahora los 4 pases comparten el mismo cbuffer (con `_BaseMap_TexelSize` dentro). `useSRPBatcher` ya estaba activo en los assets PC.

### 11.2 FPS: no hay regresion de codigo
Medicion `-perflog -quality PC_Ultra -vsync 0 -perf-viewer` (viewer abierto, ventanas de 10 s; las ventanas de 340-380 FPS de algunas ejecuciones son el Hero sin visor y no cuentan). Logs en `E:\TwinSight_builds\pc_fase3b1\logs\`.
| Configuracion | FPS visor (ventanas 3-7) |
|---|---|
| 3B.1 base (renderScale 1,5) | ~118-125 |
| 3B.1 base (renderScale 1,25) | ~119-122 |
| `-pcdbg noalbedobump,noaniso` | ~123 |
| `-pcdbg noscrews` | ~120 |
| `-pcdbg notree` | ~128 |
| `-pcdbg nossao` | ~125 |
| `-pcdbg rs1` | ~118 |
| `-pcdbg mip1` (texturas a la mitad) | ~105-110 |
| `-pcdbg noswap` (materiales originales ClippableLit) | ~110-137 |
| todas las anteriores juntas | ~100-125 |
| **Build de la fase 3A (`pc_fase3a2`), medida hoy** | **~125-132** |
Conclusion: la build de la 3A, que dio ~324 en su dia, da hoy ~128 en esta misma maquina: la diferencia es del entorno de medicion (Editor de Unity abierto, estado de la maquina), no del codigo. Entre 3A y 3B.1 la diferencia medida es ~5 %. Ningun interruptor (4K, relieve por albedo, tornillos, arbol, SSAO, escala de render, materiales PC) recupera 250. Para comparar hay que medir con el Editor cerrado. Se devolvio `PC_Ultra.renderScale` de 1,5 a 1,25 (valor de la 3A; el cambio a 1,5 vino con el pulido de materiales, sin motivo documentado). Las texturas 4K se mantienen (cuestan ~5 % y no hay ganancia de FPS al reducirlas).

### 11.3 Pendiente
- Medir FPS con el Editor cerrado para confirmar ~300.
- Opcional: desplazar el arbol a la fila seleccionada al abrirlo (no hecho).
- Opcional 7 (contorno #bfeaff y atenuado del resto) en la web/escritorio completo: aqui solo se ajusto el realce de PC.

## 12. Arrastre de sliders que atravesaba a la escena y propuesta de layout (2026-11-05, rama `pc-desktop`)

### 12.1 Arrastre de UI que pasaba a la escena (corregido, solo PC)
- **Causa.** El bloqueo de la UI (`InputManager.IsPointerOverCameraBlockingUI` / `IsPointerOverSelectionBlockingUI`) solo miraba el punto donde esta el cursor en ese instante y, con la hoja cerrada, solo contaba una lista de "controles" (no todo `Panel.Pick`). Un gesto que empezaba sobre un slider/panel perdia el bloqueo en cuanto el cursor salia del control: el hover y la seleccion volvian a actuar sobre el dron (medido con ratón real: `hover=rubber_grommet`, `hover=x500v2_prop_FR` mientras se arrastraba el deslizador de CORTE) y el arrastre de ENTORNO empezo una orbita de fondo (`[DesktopUI] camera: left-drag orbit started on the background`).
- **Arreglo** (`InputManager`, `OrbitCameraController.Desktop`; todo `#if UNITY_STANDALONE`). Un gesto de raton que EMPIEZA sobre UI pickable (`Panel.Pick`) es dueno del gesto hasta soltar todos los botones (`UIGestureOwnsPointer`, se evalua en el primer fotograma con boton pulsado): sin orbita, pan, zoom, seleccion ni cambios de hover. Ademas la rueda no hace zoom cuando el cursor esta sobre cualquier UI pickable (arbol de piezas, ficha, paneles).
- **Verificado en el .exe con ratón real** (SetCursorPos + mouse_event, 1920x1080, build `E:\TwinSight_builds\pc_slider\`, flag `-desk-sliderprobe <carpeta> cut|explode|env|wheel`, guiones en `.../pc_slider/tests/`): se arrastra el pulgar de CORTE, EXPLOTAR y ENTORNO hasta muy fuera del control (sobre el dron y las 4 esquinas). Con el arreglo: 0 lineas `[DesktopUI] camera:`, angulos/distancia/foco constantes, seleccion `x500v2_arm_FL` intacta, `hover=none`. Con `-no-uigesture` (comportamiento anterior): hover cambiando sobre piezas (CORTE) y una orbita iniciada (ENTORNO). Tras soltar, la orbita sobre el fondo y la rueda vuelven a funcionar. Rueda sobre el arbol: distancia constante.
- **WebGL/tactil.** El bug de seleccion/hover (el bloqueo no sobrevive a la salida del cursor) existe tambien en WebGL con raton, pero el tactil ya retiene el gesto (`InputBlocked` hasta soltar el dedo) y los gestos de camara de WebGL solo se deciden al pulsar. Como el arreglo cambia que superficies bloquean el gesto de camara en una build que no puedo probar aqui, **se dejo solo en PC**. Si se quiere en WebGL: quitar los `#if` en `InputManager` (cambio de 3 sitios) y probar en el navegador.
- Ayudas de verificacion (solo Standalone): `-desk-demoui <carpeta>` (inventario de superficies con cobertura del canvas), `-desk-sliderprobe`, `-no-uigesture`.

### 12.2 Inventario de la UI actual y maqueta de escritorio
- Capturas del .exe a 1920x1080 en `E:\twinsight-web-twin\docs\unity_pc\capturas_ui_actual\` (hero, tutorial, visor, INSPECT/ANALYZE/STUDIO, CORTE, FILTRO, EXPLOTAR, termico, entorno, pieza, ficha, arbol, ayuda, tooltip) y `inventario_cobertura.log` con el % de canvas cubierto por estado (ficha 22 %, arbol 14,5 %, panel ESTUDIO 20,5 %, ficha + CORTE 34,9 %).
- **Maqueta propuesta (HTML estatico, no implementada):** `E:\twinsight-web-twin\docs\unity_pc\maqueta_desktop\index.html`. Decisiones: barra superior de 44 px con conmutador segmentado de modos; rieles acoplados (arbol 280 px a la izquierda, inspector 340 px a la derecha) que reencuadran el canvas en vez de tapar; tira de herramientas contextual de 36 px y subpaneles como popovers de 300 px; barra de estado con pistas de raton y leyenda termica; densidad de raton (botones 28-32 px, texto 13 px) y atajos visibles. Espera la aprobacion del usuario antes de tocar Unity.
- **Aviso sobre las build de esta sesion.** Tras relanzar el Editor, la build de Standalone sale sin dibujar los materiales URP Lit (la UI y los shaders propios si, p. ej. X-Ray; `DroneLitPC` y `ClippableLit` no se dibujan; probado con `-pcdbg noswap`/`nossao`): el Editor modifica `UniversalRenderPipelineGlobalSettings.asset` durante la build (quita el recurso de SSAO de `m_RuntimeSettings`; se restauro con `git checkout`) y la build anterior `pc_fase3b1` si dibuja. Causa no aislada; por eso las capturas de `capturas_ui_actual` no muestran el dron y el fondo de la maqueta sale de las capturas de la fase 3A. Conviene reconstruir desde una sesion de Editor que ya construyo bien antes de la siguiente fase.

### 12.3 Aclaracion del aviso de 12.2 (resuelto en la fase 4)
La build "sin materiales URP Lit" era una cache de shaders vacia tras relanzar el Editor: reimportar `Assets/Shaders` y `Packages/com.unity.render-pipelines.universal/Shaders` (ForceUpdate, con la plataforma Standalone activa) y reconstruir devolvio el dibujo del dron (la build pasa de ~230,9 a ~234,1 MB). `UniversalRenderPipelineGlobalSettings.asset` lo modifica el Editor en cada build (quita el recurso de SSAO de `m_RuntimeSettings`); no era la causa y se restaura con `git checkout`. Receta si vuelve a pasar: cambiar a Standalone, reimportar esas dos carpetas de shaders, esperar a que termine la compilacion y construir; comprobar con `-desk-sliderprobe <carpeta> clean`.

## 13. Fase 4 — interfaz de escritorio de la maqueta aprobada (2026-11-05, rama `pc-desktop`, solo PC)

La maqueta `docs/unity_pc/maqueta_desktop/index.html` quedo aprobada y se implemento bajo `#if UNITY_STANDALONE` + `DesktopUI.uss` (clases `desktop-ui layout--desktop chrome2` en la raiz). `MainLayout.uxml` y `Theme.uss` no cambian; WebGL no cambia. El flag `-pcdbg oldchrome` desactiva el cromo nuevo (diagnostico).

### 13.1 Que hay
- **`DesktopChrome`** (nuevo): barra superior de 44 px (inicio, marca, miga con la pieza, conmutador segmentado Inspeccionar/Analizar/Estudio con tecla, Puntos H / Aislar I / Restablecer R, alternar arbol y inspector, ES/EN, Ajustes, Atajos), rieles de 40 px, barra de estado de 26 px (pistas de raton, leyenda termica horizontal con degradado propio, calidad, Atajos) y pie del inspector (Enfocar F / Aislar I). Iconos vectoriales `DesktopVectorIcon` (Painter2D, tinte por USS: hover/activo en celeste).
- **La logica no se duplica:** las tarjetas de modo (PUNTOS/AISLAR/ENERGIA, CORTE/EXPLOTAR/FILTRAR) pasan por USS a una tira de 36 px bajo la barra y los subpaneles (CORTE, FILTRO, EXPLOTAR, ENERGIA, RENDER, ENTORNO) a popovers de 260-320 px; la capsula inferior, la pastilla PART INFO, la leyenda vertical y las pastillas ATAJOS/PIEZAS se ocultan en PC. ESTUDIO se divide en dos botones (Render / Entorno) que muestran una seccion del mismo panel. Se exponen Realista, Alambre y Fantasma (ocultos en el UXML).
- **Inspector acoplado:** `DesktopSidePanel` pasa a 340 px (300 si el ancho logico < 1500), sin margenes; se abre solo al seleccionar si el ancho logico >= 1600 y se cierra al deseleccionar; manual con el boton, el riel o `P`/`]`. **Arbol** de 280 px acoplado. La camara se reencuadra con `SetDesktopViewShiftX((derecha - izquierda) / (2 * ancho))` contando los rieles de 40 px.
- **Ajustes (CONFIG dentro del visor):** el engranaje reubica `HeroSubmenu_Config` (con su seccion PANTALLA) en un popover y lo devuelve al cerrar; Esc lo cierra.
- **Atajos nuevos:** `C` corte, `G` filtro, `V` cicla render (Realista, Rayos X, Solido, Termico, Alambre, Fantasma), `T` o `[` arbol, `P` o `]` inspector (con teclados donde `[`/`]` exigen AltGr, `T`/`P` funcionan). Ayuda actualizada y convertida en popover arriba a la derecha.

### 13.2 Verificacion (.exe `E:\TwinSight_builds\pc_ui_desktop\`)
- Capturas en `docs/unity_pc/capturas_ui_desktop/` (1920x1080, 1280x720 y 1920x1440 de una ventana de 2560x1440 recortada al escritorio): reposo, pieza + inspector, INSPECT/energia, ANALIZAR corte/explotar/filtro, ESTUDIO render/termico/entorno, arbol, ajustes, ayuda; `cobertura_*.log` con el % por estado.
- Raton real (SetCursorPos + mouse_event, `-desk-sliderprobe <carpeta> flow` y `flow.ps1`): clic en la pieza (inspector se abre solo), alternar inspector, modos, CORTE (arrastre del deslizador fuera del control: valor 0 -> 2, camara y seleccion constantes), ENTORNO (rotacion de luz 45 -> 327), render termico, arbol, ajustes (abre/cierra con su X), ayuda (Esc la cierra), teclas C, G y V.
- Cobertura del canvas a 1920x1080 (union de superficies pickables / de ellas, lo que se superpone al modelo): reposo 12,3 % / 0 %; pieza + inspector 26,7 % / 0 %; ANALIZAR corte 29,4 % / 2,9 %; ESTUDIO entorno 30,1 % / 3,6 %; arbol + inspector 37,9 % / 0 %; antes (UI movil): reposo 4,9 %, ficha 26,9 %, ficha + corte 34,9 %, ESTUDIO 25,7 % **tapando el modelo**. Ahora el cromo es acoplado (el modelo se reencuadra y nada lo tapa salvo popovers de 2-4 %).
- Pendiente: probar escala de Windows al 125/150 %, doble monitor y ventana 21:9; `[`/`]` con teclados que los necesiten con AltGr.

## 14. Pulido de la interfaz de escritorio y hoja de iconos (2026-11-05, rama `pc-desktop`)

### 14.1 Pulido (build `E:\TwinSight_builds\pc_ui_pulido\`, capturas `docs/unity_pc/capturas_ui_pulido/`, antes = `ANTES_*`)
- **Tildes:** `IDENTIFICACIÓN`, `FILTRAR CATEGORÍA`, `TÉRMICO`, `Filtrar categorías` (diccionario compartido `AppLanguageManager` y `UIDetailsSheet`: es una corrección ortográfica de texto, también afecta al español de WebGL) y los tooltips nuevos de la barra (`Volver al menú`, `Árbol de piezas`, `energía`, `explosión`).
- **Deslizadores en cuadrícula:** columna de etiqueta de 132 px (118 en Ajustes) con ajuste de línea en lugar de desbordar bajo el pulgar ("INTENSIDAD DEL OBJETO" ya no se solapa) y pista alineada en todos los popovers; etiquetas de datos del inspector con ajuste de línea.
- **Inspector:** IDENTIFICACIÓN se abre por defecto al abrir el panel; separador bajo la descripción y filas alineadas arriba.
- **Árbol:** `DesktopPartTree` se suscribe desde el constructor y guarda la última selección: al abrirlo (aunque la pieza se eligiera antes de la primera apertura) resalta la fila, despliega la rama y se desplaza hasta ella; se limpia si no hay selección.
- **Selección 3D:** `DroneLitPC` suma contorno celeste más grueso y un velo mínimo cuando la intensidad es la de selección (0,55; el hover, 0,28, no cambia).
- **Pruebas:** 1920x1080, 1536x864 (equivale a Windows al 125 %), 1280x720 (150 %), 1920x820 (21:9; el escritorio limita la ventana al área de trabajo, no se pudo 2560x1080) y 1920x1080 con "Tamaño de interfaz" 150 % (`-uiscale 1.5`): sin solapes. A 720p el inspector no se abre solo (ancho lógico < 1600), a demanda.
- Receta de shaders (12.3) funcionó de nuevo: sin necesidad de reimportar esta vez.

### 14.2 Hoja de diseño de iconos, botones y tutorial (solo maqueta)
`docs/unity_pc/maqueta_iconos/index.html`: inventario de iconos/botones con incoherencias (tres sistemas de iconos, trazos 1,5/1,8/1,95, tarjetas de RENDER/ENTORNO/FILTRO sin icono, emoji en el tutorial), set de 40 iconos a 1,5 px sobre 24, sistema de botones (7 variantes x 6 estados, escritorio frente a móvil), tutorial de 6 pasos en 1920x1080 y 390x844 y lista de cambios para WebGL/móvil. No implementado: afecta a código compartido y necesita aprobación y una build WebGL nueva.

## 15. Fase 5 — set de iconos, sistema de botones y tutorial de 6 pasos (2026-11-05, rama `pc-desktop`, solo PC)

Se implementa en PC lo aprobado de `docs/unity_pc/maqueta_iconos/index.html`. WebGL y móvil no cambian (siguiente tanda).

### 15.1 Iconos
- No hay paquete SVG/Vector Graphics en `Packages/manifest.json` y no se descargó nada: `DesktopVectorIcon` (Painter2D) ahora dibuja **datos de trazado SVG** sobre cuadrícula de 24. `DesktopIconData.cs` (generado desde las SVG de la hoja con un script; 40 iconos de la hoja + `orbit`, `pan`, `angle`, `keyboard`, `check` y 5 de categoría de filtro: `propulsion`, `avionics`, `sensors`, `battery`, `fastener`) se analiza una vez (M L H V C S A Z, relativos y absolutos), se aplana a polilíneas y se traza con 1,5 px (2 px a 32 px), puntas y uniones redondas, sin relleno ni animación; el discontinuo de Rayos X se calcula a mano. Tinte por `color` de USS (reposo, hover, activo); `DesktopVectorIcon.RefreshAll()` repinta solo si cambió el tinte. Tamaños 16/20/24/40.
- Sustituidos: barra superior (inicio, puntos, aislar, restablecer, paneles, idioma, ajustes, ayuda), conmutador de modos (con icono), rieles, tira de herramientas (los `Procedural*Icon` del UXML se ocultan por USS y se inserta el vector), tarjetas de RENDER, ENTORNO y FILTRO (antes sin icono; el icono de Render sigue al modo activo), cerrar del popover de corte/filtro (antes texto `x`), cerrar del inspector, cerrar y chevrones del árbol (antes `✕`, `▸`, `▾`), cabecera de la ayuda, cerrar de ajustes, botones de invertir/ángulo del corte, hero (`i` y `<` de texto, iconos en MODELO, CONFIGURACIÓN y VER TUTORIAL) y tutorial (sin emoji).

### 15.2 Botones (`DesktopUI.uss`, bloque «Phase 5»)
Clases `.dk-b` + `--primary` (relleno #bfeaff, texto oscuro, hover blanco, pulsado #8fd0f2), `--secondary`, `--ghost`, `--icon`, `--pill`, `--lg` (hero, 40 px) y `--on` (celeste al 16 %); estados normal, hover, pulsado (`:active`), foco (anillo celeste de 2 px solo tras Tab, clase `dk-keynav` en la raíz, se quita al hacer clic), activo y desactivado (40 %). Los controles anteriores (barra, tira, popovers, árbol, ajustes) recibieron pulsado, foco y desactivado con el mismo lenguaje. Hero de PC: Abrir visor primario, el resto secundarios/fantasma, en mayúsculas y minúsculas de la hoja (solo bajo `#if UNITY_STANDALONE` en `UIHeroController.ApplyDesktopTexts`).

### 15.3 Tutorial de 6 pasos (solo PC)
`DesktopTutorial` (nuevo): tarjeta centrada con contador, barra de progreso de 6 segmentos, interruptor PC/Móvil (cambia las teclas por gestos), Atrás / Siguiente / Omitir con sus teclas y ← → (Esc lo cierra UIManager). Pasos: 1 Navegar (orbitar, desplazar, zoom al cursor), 2 Seleccionar (clic, doble clic enfocar, soltar), 3 Modos y aislar (1/2/3, I), 4 Analizar (corte C, explotar E, filtrar G), 5 Estudio (render V, entorno, térmico), 6 Listo (árbol T, ajustes, atajos). `OnboardingController` lo crea y delega `Show/Dismiss` en él bajo `#if UNITY_STANDALONE` (flag `-pcdbg oldtutorial` devuelve el de 15 pasos); `IsOpen`, `OnDismissed` y `Onboarding_Seen` siguen igual. En WebGL el código no existe.

### 15.4 Verificación (build `E:\TwinSight_builds\pc_iconos\`, capturas `docs/unity_pc/capturas_iconos/`)
- Capturas a 1920x1080 (pantalla completa) y 1280x720: reposo, pieza + inspector, cada modo con su popover, árbol, ajustes, ayuda, hero, ajustes y acerca del hero, los 6 pasos del tutorial, recortes `cierre_*` de barra superior y tira, pares `ANTES_`/`DESPUES_` de barra superior y hero.
- Ratón y teclado reales (`SetCursorPos` + `mouse_event` + `keybd_event`, carpeta `real/`): hover y pulsado del primario del hero, Abrir Acerca de, Ver tutorial, Siguiente (paso 2), → (paso 3), ← (paso 2), Atrás (paso 1), PC/Móvil, Tab (anillo de foco), Esc (cierra), hover y pulsado en la barra, árbol, modo 1, volver al hero, tutorial otra vez y Omitir. El log `[DesktopTutorial]` confirma cada paso.
- Hallazgo: `keybd_event` necesita el indicador de tecla extendida para las flechas; no es fallo de la app.
- Pulido que salió al verificar: las teclas de la ayuda se envuelven en vez de solaparse con la descripción (ya fallaba antes); fila de calidad de ajustes sin recorte.
- **Prueba de WebGL:** plataforma activa WebGL, Play en el Editor: raíz con solo `unity-ui-document__root`, cero elementos `dk-*`/`desktop-*`, sin `DkTopBar`/`DkTutorial`, sin objeto `_DesktopUI`, botones del hero con su texto original (`OPEN VIEWER`, `i`, `<`); captura `99_webgl_control.png`. `OnboardingController.cs` y `UIHeroController.cs` solo cambian dentro de `#if UNITY_STANDALONE`; `MainLayout.uxml` y `Theme.uss` no se tocan.

### 15.5 Pendiente para web/móvil
Mismo set como VectorImage o atlas PNG sustituyendo los `Procedural*Icon`; iconos para RENDER/ENTORNO/FILTRO en el UXML compartido (hoy solo PC); `x`/`<` de texto de los subpaneles y hero; botones con variantes táctiles (48 px) y tutorial de 6 pasos con hoja inferior (`OnboardingController` compartido, build WebGL nueva); flecha de los `Foldout` del inspector (sigue el triángulo de Unity); modos Realista/Alambre/Fantasma siguen sin verse en el popover Render (preexistente); revisión del texto en mayúsculas de la ayuda y los ajustes.

## 16. Retoques de PC y lote web/móvil (2026-11-05, rama `pc-desktop`)

### 16.1 Retoques de PC
- **Hero simétrico:** los cuatro botones (Abrir visor, Modelo, Configuración, Salir) llevan icono (`inspect`, `studio`, `settings`, `power`) y icono + texto se centran como un grupo (el icono se coloca cada frame según `MeasureTextSize`; relleno izquierdo de 50 px para desplazar el texto, 22 px a la derecha).
- **Popover Render:** ahora muestra Realista, Rayos X, Sólido, Malla, Fantasma y Térmico. Causa: el UXML compartido deja `style="display: none;"` en esas tarjetas y `style.display = StyleKeyword.Null` no lo quitaba; se asigna `DisplayStyle.Flex` explícito (solo PC; en web siguen ocultas).
- **Foldouts del inspector:** `OutlineIcon.ReplaceFoldoutArrows` quita el triángulo de Unity y pone el chevron del set, que gira 90 grados al abrir (también en web/móvil).
- **Lema del hero:** «VISOR 3D INTERACTIVO · HOLYBRO X500 V2» / «INTERACTIVE 3D VIEWER · HOLYBRO X500 V2» en todas las plataformas (UXML y `UIHeroController`). No se afirma gemelo digital de la unidad física. Otros textos con «gemelo digital» o «digital twin» sin cualificar (no se cambian): `twinsight-web-twin/index.html` (`og:description`: «Gemelo digital web de un dron…»; la `description` ya lo cualifica como sombra digital y gemelo digital SITL), `twinsight-web-twin/src/web/i18n/es.ts` y `en.ts`, y documentos de `desarrollo/docs/` (`08_Portafolio_Tech_Artist.md`, `investigacion/*.md`, `Holybro/*`) y `README.md`.

### 16.2 Lote web/móvil (cambia la UI compartida)
- **Iconos:** `OutlineIcon` (`Assets/Scripts/UI/ProceduralIcons/OutlineIcon.cs` + `OutlineIconData.cs`, antes `DesktopVectorIcon`/`DesktopIconData`) es ahora un `VisualElement` compartido con `[UxmlElement]` (`<pi:OutlineIcon shape="pins"/>`), sin `#if`: mismo set (1,5 px sobre 24, puntas redondas, sin hover ni animación), tinte por `color` de USS (activo `#bfeaff`, `.ui-light-bg` oscuro). `MainLayout.uxml` sustituye todos los `Procedural*Icon` (inicio, restablecer, cerrar, tarjetas de Inspeccionar/Analizar, cápsula de modos, corte, invertir, ángulo) y añade icono a las tarjetas de RENDER, ENTORNO y FILTRO (antes vacías); los `x` y `<` de texto de los subpaneles, el hero y la ficha pasan a iconos. `DesktopVectorIcon` queda como envoltorio de PC (tamaño fijo). Las clases `Procedural*Icon` siguen en el repositorio sin usarse (el overlay de tutorial antiguo del UXML también, oculto).
- **Botones (`Theme.uss`, bloque final):** primario relleno `#bfeaff` con texto oscuro (`.btn-start`, `.btn-primary`), secundario (`.btn-ghost`), activo celeste al 16 % en tarjetas, objetivos >= 48 px y texto 14-16 px (`config-tier-*`, `config-cycle-btn`, `hero-info-btn`), clases `.dk-b` compartidas a densidad táctil que PC sobrescribe a 28-32 px en `DesktopUI.uss`. Textos del hero en minúscula inicial en todas las plataformas.
- **Tutorial de 6 pasos en todas las plataformas:** `TutorialOverlay` (antes `DesktopTutorial`, ahora en `WebGL.UI.Panels`, sin `#if`). Hoja inferior (`dk-tut--sheet`) en móvil (`Application.isMobilePlatform`) o ventana de menos de 820 px, con gestos (un dedo orbitar, dos dedos desplazar/pellizco, toque, doble toque), progreso, Atrás / Siguiente / Omitir; tarjeta centrada con teclas y ← → Esc en escritorio. Tres juegos de pistas: PC (atajos), WebGL con ratón (botones y ratón; WebGL no tiene atajos de teclado) y táctil. `OnboardingController` lo usa siempre (`-pcdbg oldtutorial` conserva el de 15 pasos en PC). Se descartó `Input.touchSupported` para elegir la hoja: es verdadero en portátiles con pantalla táctil.
- Layout móvil, cápsula inferior y comportamiento: sin cambios.

### 16.3 Verificación
- PC (`E:\TwinSight_builds\pc_iconos2\`, capturas `docs/unity_pc/capturas_pc_iconos2/` a 1280x720): reposo, pieza + inspector con chevrons, popovers (Render con 6 modos, Filtro, Corte), árbol, ajustes, ayuda, hero y tutorial. No se repitió la prueba con ratón real a 1920x1080 en esta ronda (la ventana no estaba en primer plano y el script actuó sobre otro programa; se descartaron esas capturas).
- WebGL (`E:\TwinSight_builds\webgl_iconos\`, servido con `python -m http.server`, Edge dirigido por DevTools porque el panel del navegador integrado mide unos 300 px): móvil 390x844 y escritorio 1920x1080 con hero, reposo, un modo con panel, ficha de pieza y los 6 pasos del tutorial en `docs/unity_pc/capturas_web_iconos/`; `ANTES_` = versión publicada (solo lectura).
- Publicar exigiría confirmación del usuario: copiar `webgl_iconos\Build\*` y `index.html` al hosting de `twinsight.alexwoodcock.me/Build/` (misma estructura que la carpeta `docs/` publicada; el repositorio de Unity no se toca), con el mismo procedimiento de publicación de siempre. No se ha publicado nada.

### 16.4 Estado al pausar (2026-11-05)
- **Hecho y compilado (WebGL y Standalone):** 16.1 y 16.2 completos. WebGL construido en `E:\TwinSight_builds\webgl_iconos\` y PC en `E:\TwinSight_builds\pc_iconos2\` (esta es anterior al último cambio de 2 líneas: flechas `<`/`>` en WebGL en `TutorialOverlay.cs`; conviene reconstruirla). Capturas: `docs/unity_pc/capturas_web_iconos/` (móvil 390x844 y escritorio 1920x1080, con los 6 pasos del tutorial), `ANTES_movil_390x844/` (publicada), y `capturas_pc_iconos2/` (PC a 1280x720).
- **Sin hacer:** repetir la prueba con ratón real en PC a 1920x1080 (el script debe comprobar que el .exe está en primer plano antes de actuar; una ejecución actuó sobre otra ventana y se borraron esas capturas); revisar la hoja móvil con los iconos y los 6 pasos en un móvil real; `Procedural*Icon` y el overlay de tutorial antiguo del UXML siguen sin usarse (limpieza pendiente).
- **Herramientas (fuera del repo, scratchpad):** `cdp.js` (Edge dirigido por DevTools para capturar WebGL) y los scripts `.ps1`; el panel del navegador integrado mide ~300 px y no sirve para capturas de 1920x1080. Quedó una carpeta vacía accidental `C:\m4` (ruta mal formada) que no se pudo borrar.
- **Publicar** (no se ha hecho; requiere confirmación del usuario): copiar el contenido de `webgl_iconos\` al hosting de `twinsight.alexwoodcock.me/Build/`.

## 17. Animaciones de la interfaz de escritorio y tutorial animado (2026-11-06, rama `pc-desktop`, solo PC)

Decisión del usuario: la versión Unity WebGL se sustituirá por la web nativa; el lote web/móvil (4e7c7f3) queda archivado, sin publicar, y solo se mantiene compilando. Todo lo de esta fase está bajo `#if UNITY_STANDALONE` o en `DesktopUI.uss`.

### 17.1 Micro-interacciones
- **`DesktopMotion.cs`:** constante global `Speed` (código) y conmutador «Reducir animaciones» (CONFIG > Pantalla, guardado en `PlayerPrefs`, por defecto apagado; `-desk-reduce` / `-desk-noreduce` lo fuerzan). Con él, la raíz lleva `dk-reduce-motion` y todas las duraciones USS valen 0, las entradas no se animan y las ilustraciones del tutorial se quedan en un fotograma.
- **USS (`DesktopUI.uss`, bloque «Phase 6»):** transiciones de 140-200 ms con `ease-out` en botones de barra, rieles, tira, popovers, árbol, ajustes y tutorial: tinte de fondo, borde e icono, elevación de 1 px y escala 1,03-1,07 al pasar, escala 0,96 con destello celeste al pulsar, anillo de foco con fundido, chevrones que giran, tooltips con fundido y 4 px de desplazamiento. Los iconos se repintan cada 16 ms (`OutlineIcon.PollMs`, solo PC) para que el tinte transicione sin saltos.
- **Código:** pastilla deslizante bajo el conmutador de modos 1/2/3 y bajo ES/EN (`SlidingPill`); popovers (corte, filtro, explotar, energía, render/entorno, ajustes) con fundido + 6 px al abrir y el árbol entrando por la izquierda (`DesktopMotion.Enter` tras `display:none`); el inspector ya se deslizaba y ahora la cámara lo sigue con suavizado (`DesktopSidePanel`, en vez de saltar); la píldora de la pieza y el título del inspector se animan al cambiar la selección; el hero entra con título, botones y esquina escalonados al volver al menú. No se anima el cierre de los popovers (otro código los oculta con `display:none`).
### 17.2 Tutorial animado (`TutorialAnim.cs` + `TutorialOverlay.cs`)
Ilustración en bucle por paso (Painter2D + etiquetas, sin vídeo): dron de alambre que gira y cursor / ratón / teclas que actúan. Las fases de 2,6 s coinciden con las filas del paso y la fila activa se resalta. 1 Navegar: orbitar (dron girando, cursor por un arco, botón izquierdo), desplazar (dron deslizando, Shift + botón derecho) y zoom (rueda, escala hacia el cursor). 2 Seleccionar: clic con onda y aro celeste, doble clic que centra la vista, clic en el fondo que suelta. 3 Modos: tres chips 1/2/3 que se encienden (puntos, plano de corte, sólido a térmico) y aislar con I. 4 Analizar: plano de corte que barre, vista explosionada con deslizador, filtro por categorías con embudo. 5 Estudio: render (contorno, sólido, rayos X discontinuos, térmico), entorno (fondo oscuro/claro/azul y sol) y leyenda térmica. 6 Listo: árbol de piezas que entra y se expande, ajustes con deslizadores, atajos «?». Iconos de fila con sentido: orbitar (anillo con flecha), desplazar (cuatro flechas), zoom (lupa), seleccionar (puntero). Transición entre pasos (texto con fundido y desplazamiento, ya hacia atrás con el sentido contrario), relleno de progreso que avanza, tarjeta que entra, y las teclas del paso se «pulsan» en pantalla al pulsarlas (flechas, Esc, letras).
### 17.3 Verificación y rendimiento
- Build `E:\TwinSight_builds\pc_animaciones\`; tiras de fotogramas en `docs/unity_pc/capturas_animaciones/` (varias imágenes de cada interacción en un PNG, tiempos en `player.log`/`anim.log`) generadas dentro del reproductor (`-desk-anim <carpeta>`).
- **Ratón real:** el script (`anim.ps1`, con comprobación de ventana en primer plano antes de cada entrada) abortó correctamente porque otro programa (Comet) recupera el foco a los pocos segundos de lanzar el .exe; no se envió ninguna entrada al otro programa. Hover y pulsado reales quedan sin tira de fotogramas; se comprobaron en la ronda anterior (`capturas_iconos/real`) y por USS.
- **Rendimiento** (`-desk-fps`, 1904x984, vsync): hero, visor en reposo y tutorial animado a 60 fps (16,67 ms) tanto con animaciones como con «Reducir» activado; sin diferencia medible. La asignación gestionada medida es ~19 KB por fotograma en el visor en reposo con y sin animaciones (por tanto no la causan las animaciones; viene de otra parte del proyecto y conviene investigarla aparte). Con el tutorial cerrado no hay ninguna tarea programada de repintado.
- Pendiente: tira con ratón real (si el usuario no usa otro programa a la vez), cierre animado de popovers, retirar `Procedural*Icon` y el overlay de tutorial antiguo del UXML (el UXML compartido todavía los referencia). `C:\m4` (carpeta accidental) está protegida contra borrado por el sistema; que la borre el usuario.

## 18. Feedback 1 del usuario sobre pc_animaciones (2026-11-06, rama `pc-desktop`, solo PC)

Todo bajo `#if UNITY_STANDALONE`, `DesktopUI.uss` o ficheros de escritorio; WebGL no cambia (el motor de iconos se activa con `OutlineIcon.AnimateHover`, solo PC). Build `E:\TwinSight_builds\pc_feedback1\`, capturas y tiras en `docs/unity_pc/capturas_feedback1/`.

### 18.1 Micro-animaciones de iconos (fuente única: `OutlineIconAnim.cs`)
Como los `Procedural*Icon` originales (física por icono: explotar se abre, corte con bisturí, etc.), cada icono del set tiene una animación al pasar el cursor (una vez; bucle suave solo donde es natural) y una variante de clic (`ClickMul` = 1,8 veces la amplitud, 0,85 veces la duración). El motor está en `OutlineIcon` (se engancha al botón padre: enter, leave, down), los datos en `OutlineIconAnim.Specs` y se desactivan con «Reducir animaciones». Tipos: Burst (las piezas salen del centro y vuelven; `rebote` = pequeño asentamiento), Pinch (hacia el centro), Slice (cuchilla diagonal que cruza y separa las mitades), Rotate (giro, eased), Tilt (giro y vuelta), Pulse (escala), Shift (desplazamiento por trazo), Draw (se redibuja), Stagger (barras que crecen una tras otra), Float (sube y se asienta), Wobble (oscilación amortiguada). Unidades: cuadrícula de 24; tiempos en ms con suavizado smoothstep. Tiras de cada icono: `00_icono_*_hover.png` y `*_clic.png`. Tabla (generada desde el código):

| Icono | Tipo | Amp. (u) | Giro (°) | ms | Bucle / rebote | Animación al pasar el cursor |
|---|---|---|---|---|---|---|
| `explode` | Burst | 3.4 |  | 560 |  rebote | Las piezas salen hacia fuera desde el centro y se reasientan (rebote corto) |
| `cut` | Slice | 1.5 |  | 520 |  | Una cuchilla cruza el icono en diagonal y las dos mitades se separan al pasar |
| `inspect` | Pinch | 1.7 |  | 460 |  | Las cuatro esquinas del visor se cierran sobre el punto y vuelven |
| `focus` | Pinch | 1.5 |  | 460 |  | Las marcas de la mira se cierran hacia el centro y vuelven |
| `isolate` | Burst | 1.4 |  | 460 |  | Las esquinas se abren y se cierran de nuevo alrededor de la pieza |
| `analyze` | Stagger | 0.38 |  | 560 |  | Las barras crecen una tras otra |
| `studio` | Tilt |  | 28 | 480 |  | El cubo se inclina y vuelve |
| `solid` | Tilt |  | 28 | 480 |  | El cubo se inclina y vuelve |
| `xray` | Tilt |  | 28 | 480 |  | El cubo transparente se inclina y vuelve |
| `wire` | Tilt |  | 28 | 480 |  | La malla se inclina y vuelve |
| `realistic` | Tilt |  | 40 | 520 |  | La esfera gira y el brillo se desplaza |
| `ghost` | Float | 2.4 |  | 560 |  | El fantasma flota hacia arriba y se asienta |
| `thermal` | Shift |  |  | 520 |  | El mercurio sube dentro del termómetro |
| `pins` | Float | 2.8 |  | 520 |  | El marcador salta y cae con un pequeño rebote |
| `reset` | Rotate |  | -360 | 620 |  | Giro completo en sentido antihorario |
| `home` | Shift |  |  | 420 |  | El tejado se levanta un instante |
| `power` | Shift |  |  | 420 |  | La línea de encendido sale hacia arriba y vuelve |
| `settings` | Shift |  |  | 520 |  | Los deslizadores cruzan en sentido contrario |
| `language` | Draw |  |  | 700 |  | El globo se dibuja de nuevo |
| `help` | Pulse | 0.14 |  | 420 |  | Late una vez |
| `info` | Pulse | 0.14 |  | 420 |  | Late una vez |
| `parts` | Draw |  |  | 650 |  | El árbol se dibuja rama a rama |
| `search` | Wobble |  | 16 | 560 |  | La lupa oscila como buscando |
| `panel_l` | Shift |  |  | 420 |  | El divisor se desplaza como un panel que se pliega |
| `panel_r` | Shift |  |  | 420 |  | El divisor se desplaza como un panel que se pliega |
| `close` | Rotate |  | 90 | 360 |  | Giro de 90 grados |
| `back` | Shift |  |  | 360 |  | La flecha se desplaza hacia atrás |
| `chevron` | Shift |  |  | 360 |  | El chevrón avanza |
| `env_studio` | Pulse | 0.1 |  | 420 |  | La pantalla late |
| `env_time` | Rotate |  | 360 | 2600 | sí | El sol gira despacio mientras se pasa el cursor (bucle suave) |
| `env_color` | Wobble |  | 14 | 520 |  | La paleta oscila |
| `filter` | Wobble |  | 12 | 520 |  | El embudo se sacude como tamizando |
| `invert` | Rotate |  | 180 | 460 |  | Las flechas dan media vuelta |
| `angle` | Wobble |  | 10 | 520 |  | El ángulo se abre y se cierra |
| `orbit` | Rotate |  | 360 | 900 |  | El anillo orbita una vuelta |
| `pan` | Pulse | 0.18 |  | 420 |  | Las flechas se dilatan |
| `zoom` | Pulse | 0.2 |  | 440 |  | La lupa se acerca |
| `pointer` | Shift |  |  | 320 |  | El puntero hace clic (baja y vuelve) |
| `keyboard` | Pulse | 0.08 |  | 320 |  | La tecla se hunde |
| `check` | Draw |  |  | 420 |  | La marca se dibuja |
| `propulsion` | Rotate |  | 360 | 900 | sí | La hélice gira mientras se pasa el cursor (bucle suave) |
| `avionics` | Pulse | 0.12 |  | 420 |  | El chip late |
| `sensors` | Pulse | 0.16 |  | 480 |  | Las ondas se expanden |
| `battery` | Pulse | 0.1 |  | 420 |  | La batería late |
| `fastener` | Tilt |  | 60 | 460 |  | La tuerca da un cuarto de vuelta y vuelve |

### 18.2 Tutorial
- **Secuencia o acción elegida:** las animaciones se reproducen en secuencia; al pulsar una fila solo se repite (en bucle) la animación de esa acción con la fila resaltada (`dk-tut-row--locked`); volver a pulsarla, o pulsar →, reanuda la secuencia. El interruptor PC/Táctil se mantiene.
- **Variante táctil (tabletas):** guantes de dedo en lugar de cursor y ratón: un dedo arrastra (orbitar), dos dedos desplazan, pellizco para el zoom, toque, doble toque y toque en el fondo; sin teclas; los pies de ilustración cambian («Un dedo: orbitar», «Toca Inspeccionar», etc.).
- **Arreglos:** (a) el zoom crece con el dron centrado en el encuadre; (b) la primera fila se resalta en cuanto cambia el paso (`HighlightRow` + `TutorialAnim.Refresh` tras construir las filas; antes el resaltado se perdía porque la fase se anunciaba antes de crear las filas); (c) Inspeccionar rehecho: una lupa recorre el dron, las piezas reciben el aro #bfeaff, aparece su nombre (etiqueta) y entra la hoja de información con reencuadre; (d) Corte: el plano barre, la sección queda visible, el plano se retira y el dron vuelve entero; (e) Explotar: placa superior, batería, placa inferior, brazos, motores y hélices se separan radialmente con huecos y guías, y se recomponen; (f) Entorno: día (sol, cielo claro), noche (luna creciente y estrellas) y atardecer (degradado magenta a naranja, sol bajo) con transición suave en el orden del ciclo TIME.
### 18.3 Chips de efectos activos en la barra superior
Junto al botón de puntos aparecen los iconos de filtro, vista explosiva y corte mientras estén activos; clic = desactivar (tooltip «Quitar filtro / vista explosiva / corte» con la tecla G / E / C); entrada y salida animadas, tinte activo y rojo suave al pasar. El estado se lee del motor (`CrossSectionManager.IsEnabled`, `ExplodedViewManager.GetExplosionFactor`, y `UIManager.FilterActive` = el conjunto de categorías ya no es el predeterminado: `HasActiveCategoryFilters` del gestor devuelve verdadero con las seis categorías por defecto y no sirve), así que sigue sincronizado con el popover, los atajos y Restablecer. Apagar usa `ShortcutClearFilter` / `ShortcutClearExplode` / `DisableCrossSection`.
### 18.4 Botón de puntos invertido
Causa: `InspectModeHandler._hotspotsEnabled` empieza en verdadero pero `UIManager` llama a `HotspotManager.SetVisible(true/false)` al entrar y volver al menú sin tocarlo, y la clase de la tarjeta se desfasaba. Ahora el botón de la barra y la tarjeta leen `HotspotManager.IsVisible` cada fotograma. Comprobación de regresión: `-desk-check <carpeta>` (`DesktopSelfCheck`) registra 13 líneas PASS/FAIL (puntos al inicio, tras ocultar, tras Inicio y reentrada, chips de corte, explosión y filtro, que no aparezca el filtro con las categorías por defecto); 13/13 PASS.
### 18.5 Brillo del alambre y rayos X
`PC_Wireframe`: `_Intensity` 1,5 a 0,42, `_FillAlpha` 0,025 a 0,008, `_WirePixels` 1,15 a 0,9, `_DenseMin` 0,18 a 0,12; `PC_XRay`: `_Intensity` 0,75 a 0,5, `_BehindIntensity` 0,5 a 0,3 (materiales y valores por defecto del shader). Antes y después: `wire_antes/despues.png`, `xray_antes/despues.png`.
### 18.6 Estructura de la barra superior
Zonas documentadas en `DesktopChrome.BuildTop`: [inicio, marca, miga] [modos 1/2/3] [hueco futuro `DkTopSlotFuture`, oculto, para un modo Gemelo/Telemetría] [acciones de vista: puntos + chips, aislar, restablecer] [paneles] [idioma, ajustes, ayuda]; añadir un botón o panel no obliga a recolocar nada. No se toca telemetría.
### 18.7 Para el port a la web (todo parametrizado)
Tokens: duraciones y easing en el bloque «Phase 6» de `DesktopUI.uss` y `DesktopMotion.Speed`; iconos en `OutlineIconData.cs` (SVG de la hoja) y `OutlineIconAnim.cs` (tabla de arriba); contenido del tutorial (pasos, filas, pistas ratón/táctil/PC, fases de 2,6 s) en `TutorialOverlay.cs` y `TutorialAnim.cs` (escenas dibujadas con trazos de la cuadrícula 210x230). 
### 18.8 Pendiente
Tira de hover/pulsado reales con ratón del SO (Comet roba el foco; el script `anim.ps1` aborta si el reproductor no está en primer plano); animación de cierre de popovers; retirar `Procedural*Icon` del UXML compartido.
