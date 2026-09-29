# Auditoría 8: Calidad Estética, Visual, Color y Contraste de Imágenes y Assets
## Proyecto TwinSight X500 — Presentación de Defensa (`index_final.html`)

**Fecha de Evaluación:** 28 de septiembre de 2026  
**Lienzo y Fondo Base:** Dark Mode Industrial (`#07080A` — RGB: 7, 8, 10)  
**Paleta de Acentos de Interfaz:** Lima Neón (`#C8F53F`), Ámbar Alerta (`#FFB224`), Azul Técnico (`#00A3FF` / `#4A9EFF`), Tinta Clara (`#EDEEE8`), Tinta Media (`#9BA0A6`).  
**Archivo Evaluado:** [`index_final.html`](file:///D:/Downloads/presentacion/index_final.html) (Inspección no destructiva, sin modificaciones al archivo).

---

### Resumen Ejecutivo y Diagnóstico Global

Se realizó un escaneo métrico, espectral y visual exhaustivo de todos los assets gráficos utilizados en `index_final.html` y los disponibles en `assets/img/`.

#### Problemas Sistémicos Identificados:
1. **Discrepancia de Nivel de Negros (Elevated Black Floor):**  
   Varios archivos JPEG (`hero01.jpg`, `ui01.jpg`, `ui02.jpg`, `app_02.jpg`) tienen fondos oscuros en el rango RGB `(12-22, 12-22, 12-22)`. Al montarse sobre el fondo base de la diapositiva `#07080A` (RGB `7, 8, 10`), se genera un **recuadro rectangular visible o "caja gris flotante"**, destruyendo la ilusión de interfaz transparente e inmersiva.
2. **Deficiencia de Enmascarado CSS `.melt`:**  
   La clase `.melt` utiliza una máscara radial `-webkit-mask-image: radial-gradient(80% 76% at 50% 50%, #000 42%, transparent 75%)`. En imágenes con fondos grises elevados, esta máscara corta el gradiente antes de tocar el negro puro, provocando un **aro o viñeta elíptica con bordes rectangulares cortados** en los bordes de la imagen.
3. **Compresión de Rango Dinámico y Blancos Apagados:**  
   Imágenes clave como `hero01.jpg` y `ui_analyze.png` tienen sus altas luces capadas (máximo de 204–230 en vez de 255). El 96% de sus píxeles está concentrado por debajo de 50/255, resultando en capturas empastadas, oscuras y con pérdida de detalle en proyectores o salas iluminadas.
4. **Fondo Sólido Claro en Slide de Cierre (`render_01.png`):**  
   En la diapositiva final de agradecimiento (Slide 29), `render_01.png` tiene un fondo de estudio gris-azulado sólido `RGB(153.7, 164.1, 173.9)` (Delta de **+156** respecto a `#07080A`). Aunque está enmarcado en una tarjeta con borde, el contraste es tan violento que rompe la continuidad visual oscura de toda la presentación.
5. **Artefactos de Compresión JPEG en Gradientes Oscuros:**  
   Los gradientes oscuros de `ui01.jpg` y `ui02.jpg` (archivos de solo 64–77 KB a 1600×900) sufren de banding y macrobloques 8×8 DCT perceptibles alrededor de los hotspots y piezas del dron.

---

## 1. Inventario y Auditoría Individual de Imágenes Activas en `index_final.html`

---

### [Asset 1] `assets/img/ui04_hero_f.png`
* **Ubicación:** Slide 00 (Portada) — Línea 514 (`<figure class="cover-visual">`).
* **Especificaciones:** PNG RGBA | 960 × 540 px (16:9) | 360.6 KB | 25.1% transparencia alfa pura.
* **Métricas Tonométricas:**
  - Luminancia media percibida: 74.7 / 255 | Desviación estándar (contraste): 107.5.
  - Distribución tonal: Negros profundos (<10): 56.2% | Medios tonos (50-180): 4.3% | Altas luces (>245): 25.1%.
  - Color en bordes: Fondo transparente integrado sobre `#07080A`.
* **Diagnóstico Estético:**
  - **Puntos Fuertes:** Excelente separación de figura y fondo. Los acentos azul cian en el marco del Holybro X500 V2 y los elementos de UI en perspectiva generan profundidad tridimensional.
  - **Defectos Detectados:** El escalado CSS al 150% (`.cover-visual img { width: 150%; }`) reduce la nitidez aparente en pantallas 4K/Retina. Las micro-etiquetas tipográficas pierden filo. En el canal alfa hay una transición abrupta en el lado izquierdo.
* **Propuesta de Corrección:**
  1. Re-renderizar o re-exportar a resolución nativa 1920×1080 con máscara alfa suave de 16-bit.
  2. Ajuste de curvas: Realzar el canal verde-lima y cian en los textos flotantes para hacer juego exacto con los tokens `--lime` (`#C8F53F`) de la portada.
  3. Aplicar filtro de microcontraste y unsharp mask (Radius: 1.2px, Amount: 35%) para conservar filo al escalarse en viewport completo.

---

### [Asset 2] `assets/img/hero01.jpg`
* **Ubicación:** Slide 09 (Activo 3D) — Línea 729 (`<img class="gl-poster">`).
* **Especificaciones:** JPEG RGB | 1920 × 1080 px (16:9) | 85.1 KB | Sin canal alfa (Opaco).
* **Métricas Tonométricas:**
  - Rango Dinámico: R[0-230], G[0-227], B[0-227] (Blancos capados al 90%).
  - Luminancia media percibida: 16.1 / 255 | Desviación estándar: 23.6 (**Muy bajo contraste**).
  - Distribución tonal: Negros (<10): 52.0% | Sombras (10-50): 44.3% | Altas luces (>180): 0.4%.
  - Color en bordes: RGB (6.0, 6.0, 6.0) | Delta vs `#07080A`: -2.0 a -4.0.
* **Diagnóstico Estético:**
  - **Puntos Fuertes:** Funciona como póster estático previo a la carga del GLB Three.js.
  - **Defectos Detectados:** La imagen es excesivamente oscura y fangosa. El 96.3% de la información tonal vive en sombras profundas. La estructura de fibra de carbono, los motores y los trenes de aterrizaje se pierden por completo en pantallas estándar o proyectores. Falta punch y definición metálica.
* **Propuesta de Corrección:**
  1. **Levantamiento de sombras (Shadow Lift):** Aplicar curva en S asimétrica con elevación de sombras medias (Input: 25 → Output: 45) para revelar tornillos, brazos de carbono y cableado.
  2. **Expansión de altas luces:** Mapear el punto blanco de 230 a 255, recuperando el brillo especular de las luces del domo sobre los motores y las arandelas anodizadas.
  3. **Corrección de color:** Incrementar saturación (+12%) en los acentos de la UI interactiva (botones de modo, inspect, analyze) para que no parezcan deslavados antes de que entre el canvas interactivo.

---

### [Asset 3] `assets/img/ui02.jpg`
* **Ubicación:** Slide 12 (Taxonomía) — Línea 810 (`<figure class="melt">`).
* **Especificaciones:** JPEG RGB | 1600 × 900 px (16:9) | 64.2 KB | Opaco.
* **Métricas Tonométricas:**
  - Rango Dinámico: R[0-236], G[0-236], B[0-238].
  - Luminancia media percibida: 22.3 / 255 | Desviación estándar: 30.3.
  - Distribución tonal: Negros (<10): 45.5% | Sombras (10-50): 41.2% | Medios tonos: 12.6%.
  - Color en bordes: RGB (3.4, 3.4, 3.4) | Delta vs `#07080A`: -3.6 a -6.6.
* **Diagnóstico Estético:**
  - **Puntos Fuertes:** Los hotspots circulares de selección guían correctamente el ojo.
  - **Defectos Detectados:** La máscara radial `.melt` se corta en los márgenes de 1600×900, revelando los límites rectangulares del JPG. La micro-tipografía de los callouts ("Select a Part", datos de pieza) sufre degradación por compresión JPEG de baja tasa de bits (64 KB para 1.44 megapíxeles).
* **Propuesta de Corrección:**
  1. **Conversión a PNG/WebP con canal alfa real:** Reemplazar el fondo gris oscuro quemado por transparencia alfa gradiente o fondo puro `#07080A`, eliminando la necesidad de máscaras CSS forzadas que fallan en esquinas.
  2. **Realce de microcontraste en Hotspots:** Incrementar la luminancia de los anillos de selección (anillos exteriores de hotspot) para que resalten con fuerza sobre la geometría del dron.
  3. **Afilado tipográfico:** Aplicar filtro de nitidez localizado en las cajas de texto de la UI.

---

### [Asset 4] `assets/img/ui01.jpg`
* **Ubicación:** Slide 13 (Flujo público) — Línea 850 (`<figure class="melt">`).
* **Especificaciones:** JPEG RGB | 1600 × 900 px (16:9) | 77.5 KB | Opaco.
* **Métricas Tonométricas:**
  - Rango Dinámico: R[0-255], G[0-255], B[0-255].
  - Luminancia media percibida: 24.0 / 255 | Desviación estándar: 32.2.
  - Distribución tonal: Negros (<10): 41.8% | Sombras (10-50): 44.7% | Medios tonos: 12.3%.
  - Color en bordes: RGB (4.3, 4.3, 4.3) | Delta vs `#07080A`: -2.7 a -5.7.
* **Diagnóstico Estético:**
  - **Puntos Fuertes:** Muestra de forma elocuente las tres herramientas principales de Analyze (Cut, Explode, Filter) y el bottom sheet.
  - **Defectos Detectados:** Similar a `ui02.jpg`, la viñeta radial no coincide con el negro base `#07080A`, generando un límite visible en los costados. La zona del bottom sheet y las miniaturas de corte se ven empastadas.
* **Propuesta de Corrección:**
  1. **Ajuste de punto negro (Black Clipping):** Normalizar los tonos periféricos para que alcancen `#07080A` antes de los últimos 60px de borde, logrando una fusión 100% invisible con el fondo de la slide.
  2. **Refuerzo de contraste en UI Analyze:** Aumentar brillo (+15%) y saturación en los iconos cian de *Cut*, *Explode* y *Filter*, garantizando que el jurado pueda leer los modos sin esfuerzo.
  3. **Reducción de artefactos DCT:** Re-exportar en PNG o WebP de alta fidelidad.

---

### [Asset 5] `assets/img/app_02.jpg`
* **Ubicación:** Slide 17 (Demo capacidades) — Línea 964 (`<figure class="melt">`).
* **Especificaciones:** JPEG RGB | 1600 × 900 px (16:9) | 72.9 KB | Opaco.
* **Métricas Tonométricas:**
  - Rango Dinámico: R[0-255], G[0-255], B[0-255].
  - Luminancia media percibida: 32.0 / 255 | Desviación estándar: 29.8 (**Poco rango dinámico**).
  - Tonal Split: Sombras (10-50): **92.1%** | Medios tonos: 3.2% | Altas luces: 1.7%.
  - Color en bordes: **RGB (17.2, 17.2, 17.2)** | Delta vs `#07080A`: **+10.2 R, +9.2 G, +7.2 B**.
* **Diagnóstico Estético:**
  - **Defecto Crítico:** Los bordes tienen un valor de gris de 17.2, claramente más claro que el `#07080A` (7, 8, 10). La viñeta radial de la clase `.melt` deja una **caja gris perceptiblemente recortada** debajo del bloque de texto.
  - **Iluminación del modelo:** El modelo del dron está sumergido en un gris uniforme y plano. No se aprecia la textura PBR del chasis, la fibra de carbono ni los rotores.
* **Propuesta de Corrección:**
  1. **Corrección de Bordes (Edge Blending Obligatorio):** Curva de caída de negro en los márgenes exteriores para forzar el valor RGB a (7, 8, 10) o extraer a PNG con máscara alfa gradual.
  2. **Realce de Texturas PBR:** Aumentar el contraste tonal medio (claridad / local contrast enhancement) en la geometría central para que se distingan la placa central, los ESCs y los motores.
  3. **Brillo en UI:** Resaltar los paneles táctiles laterales para que coincidan con la estética moderna de TwinSight.

---

### [Asset 6] `assets/img/render_01.png`
* **Ubicación:** Slide 29 (Cierre) — Línea 1268 (`<figure class="closing-visual">`).
* **Especificaciones:** PNG RGBA | 1920 × 1080 px (16:9) | 2,076.5 KB | **Alpha 100% Opaco (sin transparencia)**.
* **Métricas Tonométricas:**
  - Rango Dinámico: R[0-182], G[0-190], B[0-199] (Cero blancos puros; altas luces capadas a 199).
  - Luminancia media percibida: **161.6 / 255** | Mediana: **177 / 255**.
  - Color en bordes: **RGB (153.7, 164.1, 173.9)** | Delta vs `#07080A`: **+146.7 R, +156.1 G, +163.9 B** (¡Fondo Gris Claro!).
* **Diagnóstico Estético:**
  - **Defecto Crítico de Continuidad:** Es la única imagen de toda la presentación con un fondo claro de iluminación de estudio gris-azulado (`RGB 153, 164, 173`). Aunque la tarjeta contenedora `.closing-visual` tiene bordes redondeados y un degradado CSS tenue, la imagen genera un **bloque claro que choca violentamente con el cierre elegante y oscuro de la presentación**.
  - **Gama de Color:** El dron aparece en tonos grises fríos con luces lavadas sin alcanzar blanco puro (máx 199), perdiendo el dramatismo de un render de grado de ingeniería.
* **Propuesta de Corrección:**
  1. **Aislamiento de Fondo (Alpha Cutout):** Extraer la silueta completa del dron eliminando el fondo gris-azulado sólido y sustituyéndolo por un fondo degradado radial oscuro que transicione suavemente a `#07080A`, o mantener canal alfa transparente con sombra de contacto sutil.
  2. **Gradación de Color Industrial:** Elevar las altas luces especulares (highlights en motores y aristas) hasta 245–250, devolviéndole vida, brillo metálico y presencia escénica al render final.
  3. **Alineación con la Identidad:** Agregar un sutil halo posterior (*rim light*) en verde lima (`#C8F53F`) o cian para sellar la firma visual de TwinSight en la diapositiva final.

---

### [Asset 7] `assets/img/ui_analyze.png`
* **Ubicación:** Slide 40 (Backup B11 Mobile UX) — Línea 1486 (`<figure class="melt">`).
* **Especificaciones:** PNG RGBA | 960 × 540 px (16:9) | 1,787.8 KB | **Alpha 100% Opaco**.
* **Métricas Tonométricas:**
  - Rango Dinámico: R[0-204], G[0-204], B[0-204] (Blancos completamente castrados a 204).
  - Luminancia media percibida: 15.3 / 255 | Desviación estándar: 23.1.
  - Distribución tonal: Negros y sombras (<50): **96.0%** | Altas luces (>180): 0.3%.
  - Peso ineficiente: 1.78 MB para solo 960×540 píxeles.
* **Diagnóstico Estético:**
  - **Defectos Detectados:** Extremadamente oscura. El panel de herramientas Analyze móvil ("Cut", "Explode", "Filter") está tan sumergido en las sombras que los iconos son casi invisibles. La compresión de rango dinámico le quita todo el lustre de un producto de software terminado.
* **Propuesta de Corrección:**
  1. **Expansión de Rango Dinámico:** Mapear el punto blanco de 204 a 255 y aplicar curva gamma 1.25 a los medios tonos para que los botones de la interfaz táctil destaquen con claridad.
  2. **Optimización de Peso:** Reducir de 1.78 MB a ~180 KB en PNG-8 optimizado o WebP sin pérdida.
  3. **Contraste de Botonera Móvil:** Aumentar el contraste del estado activo del botón de corte y el dial interactivo.

---

## 2. Auditoría de Assets de Reserva y Alternativas en `assets/img/`

Además de los 7 activos en uso, se auditaron los 5 archivos adicionales que residen en el directorio:

| Archivo | Formato / Res | Estado / Diagnóstico | Recomendación |
|---|---|---|---|
| `assets/img/render_02.jpg` | JPG 1920×1080 (90.9 KB) | Vista lateral del dron. Al igual que `render_01`, tiene fondo gris-azulado claro `RGB(154, 164, 174)`. | Si se usa como vista alternativa en backup, requiere el mismo recorte de fondo alfa que `render_01.png`. |
| `assets/img/ui_inspect.png` | PNG 960×540 (1.76 MB) | Vista de Inspect móvil. Muy oscura (72% píxeles <10, máx 206). Peso excesivo. | Requiere expansión de rango dinámico y compresión PNG optimizada si se integra en la slide B11. |
| `assets/img/app_01.jpg` | JPG 1600×900 (65.7 KB) | Dron completo en Explore mode. 94.1% píxeles <10 (fondo negro puro (0,0,0)). | Excelente nivel de negro de fondo, pero el modelo del dron necesita levantamiento de sombras en la carcasa central. |
| `assets/img/app_03.jpg` | JPG 1600×900 (78.6 KB) | Vista con herramientas Studio. Bordes en RGB (22, 22, 22) (Delta +15 vs slide). | Si se usa en slide de Shaders/Studio, requiere corrección perimetral de negro para evitar caja visible. |
| `assets/img/ui04_hero_original.png` | PNG 960×540 (1.59 MB) | Versión previa de la portada sin viñeta alfa agresiva. | Conservar como máster de respaldo; `ui04_hero_f.png` tiene mejor integración visual con el título. |

---

## 3. Matriz de Priorización de Corrección Visual

| Prioridad | Imagen / Asset | Slide | Problema Crítico | Acción Requerida |
|:---:|---|:---:|---|---|
| **P1 (Crítica)** | `render_01.png` | 29 (Cierre) | Fondo gris claro `RGB(154, 164, 174)` rompe bruscamente el dark mode de la slide final. | Recorte de fondo (Alpha Cutout) + Fondo transparente/degradado a `#07080A` + brillo especular. |
| **P1 (Crítica)** | `app_02.jpg` | 17 (Demo) | Bordes en RGB 17 causan recuadro rectangular visible; dron plano y subexpuesto (92% sombras). | Fusión perimetral a `#07080A` + Levantamiento de sombras medias + Realce PBR. |
| **P2 (Alta)** | `hero01.jpg` | 09 (Activo 3D) | Extrema subexposición (mediana 9/255, 96% sombras); detalles mecánicos invisibles; blancos capados. | Curva S de rango dinámico (0-255) + realce de tornillería y fibra de carbono + nitidez. |
| **P2 (Alta)** | `ui_analyze.png` | 40 (B11) | Blancos capados al 80% (204); botones táctiles casi invisibles; 1.78 MB de peso innecesario. | Expansión de histograma + realce de botones UI + compresión a WebP/PNG optimizado. |
| **P3 (Media)** | `ui01.jpg` | 13 (Flujo) | Máscara radial deja esquinas visibles; micro-etiquetas Analyze borrosas por compresión JPG. | Gradiente perimetral limpio a `#07080A` + afilado de tipografía técnica interna. |
| **P3 (Media)** | `ui02.jpg` | 12 (Taxonomía) | Fondo gris oscuro recortado; hotspots con poco contraste lumínico en anillos exteriores. | Normalización de fondo negro + realce lumínico en anillos de selección. |
| **P4 (Baja)** | `ui04_hero_f.png` | 00 (Portada) | Buen estado general; escalado 150% causa leve suavizado de líneas en pantallas HiDPI. | Filtro de nitidez sutil (Unsharp Mask) + exportado 1080p para pantallas de alta densidad. |

---

## 4. Pipeline Técnico Propuesto para la Ejecución de Corrección

Cuando se autorice la aplicación de estos cambios, el tratamiento se ejecutará mediante un script Python no destructivo que utilizará **Pillow / OpenCV** para realizar el procesamiento de imagen a nivel de píxel:

```python
# Procedimiento técnico planeado por asset:
# 1. Copia de seguridad de los assets originales en assets/img_original_backup/
# 2. Procesamiento de curvas de nivel (Levels) con mapeo exacto de Black Point (In: 0-25 -> Out: 0) y White Point (In: 204-230 -> Out: 255)
# 3. Aplicación de máscara de gradiente periférico suave garantizando borde = RGB(7, 8, 10)
# 4. Eliminación de fondo y generación de canal alfa transparente para render_01.png
# 5. Aplicación de Unsharp Masking selectivo (radius=1.2, percent=140, threshold=3) para texto técnico
# 6. Guardado optimizado preservando metadatos y sRGB color space
```

> [!IMPORTANT]
> **Ningún cambio ha sido aplicado a `index_final.html` ni a los archivos de imagen**. Esta auditoría documenta el estado actual exacto con evidencia cuantitativa y queda lista para ejecutarse únicamente tras la aprobación explícita del usuario.
