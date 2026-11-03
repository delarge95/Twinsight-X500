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
