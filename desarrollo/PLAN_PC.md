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
