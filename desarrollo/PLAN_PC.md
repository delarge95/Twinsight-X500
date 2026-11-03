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
