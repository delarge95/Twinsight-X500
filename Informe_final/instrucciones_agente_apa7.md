# Paquete definitivo de correcciones APA 7 UNAD — Instrucciones para el agente

**Objetivo:** dejar el informe TwinSight X500 100% conforme al *Instructivo para el uso de Normas APA 7a Edición* de la UNAD (marzo 2023), corrigiendo SOLO formato. No se altera contenido académico salvo donde se indica expresamente (captions, Conclusiones/Limitaciones/Recomendaciones).

**Referencias normativas citadas** (p. = página del instructivo PDF):
- p. 7: interlineado 2.0, sin espacios entre párrafos; paginación arábiga superior derecha desde la portada; sangría a partir del segundo párrafo en títulos de primer nivel; no páginas en blanco.
- p. 8: márgenes 2.54 cm; Times New Roman 12; texto alineado a la izquierda sin justificar; cada título de nivel 1 inicia página nueva; los títulos no se etiquetan con números ni letras.
- p. 9: portada (título en minúsculas, negrita, centrado; mayúscula solo en siglas/nombres propios).
- pp. 12-13: Resumen/Abstract, un párrafo sin sangría; "Palabras clave:" / "Keywords:" con sangría, cursiva y negrita.
- pp. 3, 14-17: orden de preliminares: Tabla de Contenido → Lista de Tablas → Lista de Figuras → Lista de Apéndices.
- p. 21: jerarquía de títulos niveles 1-5.
- pp. 22-24: figuras y tablas (número en negrita, título en cursiva con mayúsculas capitalizadas y sin punto final; nota; la página antes de una figura debe ser una página llena de texto; tablas/figuras en apéndices se nombran con el rótulo del apéndice).
- pp. 24-26: Conclusiones y Recomendaciones son títulos de nivel 1, sin viñetas ni numeraciones, en párrafos.
- p. 31: referencias en orden alfabético con sangría francesa de 1,27 cm.
- p. 55: apéndices — línea 1: "Apéndice A" en negrita, sin punto, centrado; línea 2: título en cursiva, sin negrita, mayúsculas capitalizadas, centrado; cada apéndice inicia en página nueva.

---

## REGLAS DE ORO (lo que el agente NO debe hacer)

Estas son trampas en las que cayeron Gemini y/o Qwen. Respétalas:

1. **NO agregar espacio vertical extra alrededor de ningún título** (`\titlespacing` queda en `{0pt}{0pt}` para niveles 1-3 y `{0pt}` antes para niveles 4-5). El título ya ocupa una línea a interlineado 2.0; espacio extra = violación de p. 7 y es exactamente lo que el revisor ordenó eliminar.
2. **NO poner `\noindent` ni `\indent` manuales** al inicio de párrafos. El preámbulo (`\titlespacing*` estrellado) ya suprime la sangría del primer párrafo tras cualquier título y la aplica (1.27 cm) desde el segundo. Borra los manuales que encuentres.
3. **NO escribir "Palabras Clave" con C mayúscula.** La forma correcta es `Palabras clave:` (ejemplo de p. 12). La lista de palabras clave NO termina en punto.
4. **NO poner el título del apéndice en negrita.** Línea 1 (rótulo) en negrita; línea 2 (título) en cursiva SIN negrita (p. 55).
5. **NO bajar `\cfttabnumwidth`/`\cftfignumwidth` por debajo de 4.5em** ("Figura 10" mide ~3.9em; 2.3em monta el número sobre el título).
6. **NO poner `\FloatBarrier` de forma sistemática** antes de cada sección/subsección. La causa raíz de los huecos es el flotante rígido `[H]` y las alturas excesivas (ver Tarea 9). `\FloatBarrier` solo como parche puntual si tras compilar persiste un hueco concreto.
7. **NO escribir el título de la portada en mayúsculas capitalizadas.** Va en minúsculas (p. 9).
8. **NO usar el hack `\everypar{\hangindent...}`** en referencias (ver Tarea 7 para el método correcto si hace falta).
9. **NO añadir `\vspace` manual en ningún punto** de los capítulos (salvo el que ya existe dentro de macros del preámbulo).
10. **NO cambiar la numeración de tablas/figuras del cuerpo** (Tabla 1-28, Figura 1-80). La renumeración con letra aplica SOLO a tablas/figuras ubicadas dentro de los apéndices (Tarea 8).

---

## Tarea 0 — Reemplazar el archivo maestro

Reemplaza `informe_final.tex` por el archivo `informe_final_definitivo.tex` entregado. Cambios respecto al anterior (todos verificados contra el instructivo):

| # | Cambio | Motivo |
|---|--------|--------|
| 1 | `tocdepth` 4 → 3 | El TOC de 7 páginas incluía títulos run-in de nivel 4 (Three.js, Carga Intrínseca, Hero, OE1...). El propio TOC del instructivo lista hasta 3 niveles. |
| 2 | `\cfttabnumwidth` / `\cftfignumwidth` 5.7em → 4.5em | El revisor marcó el hueco entre "Figura 1" y su título en las listas. |
| 3 | `\cfttabfont` / `\cftfigfont` sin cursiva | Las entradas de las listas van en letra redonda (ejemplos pp. 15-16). |
| 4 | Niveles 4-5: separación tras el punto 1em → 0.5em; espacio antes = 0 | El revisor marcó la separación exagerada tras el punto (ej. "Densidad de Texel.") y los espacios en títulos de 4.º/5.º nivel. |
| 5 | `\apanote` con `\setstretch{1.15}` y `\vspace{0.3\baselineskip}` | Notas pegadas al objeto con interlineado reducido (permitido p. 23). |
| 6 | `\parskip` = 0 explícito | Blindaje de "sin espacios entre párrafos" (p. 7). |
| 7 | Portada sin entorno `titlepage` | Higiene: el resultado visible es idéntico (la portada YA imprimía el número 1), pero se elimina el `\setcounter{page}{2}` manual. |
| 8 | Orden de preliminares corregido | Estaba Lista de Figuras ANTES de Lista de Tablas; el orden UNAD es Tablas → Figuras (pp. 3, 15-16). |
| 9 | Resumen, Abstract, Lista de Tablas, Lista de Figuras, Lista de Apéndices e Información General ahora entran al TOC | El TOC del propio instructivo lista todas las preliminares. Se logra DESACTIVANDO el asterisco (`\section{...}`) en vez de `\section*` + `\addcontentsline` manual. |
| 10 | Lista de palabras clave sin punto final | APA 7 / ejemplo p. 12. |

---

## Tarea 1 — `chapters/01_introduccion.tex`

1. Quitar el asterisco de las secciones principales para que entren al TOC (con `secnumdepth=0` NO aparecerán números):
   - `\section*{Introducción}` → `\section{Introducción}`
   - `\section*{Planteamiento del Problema}` → `\section{Planteamiento del Problema}`
   - `\section*{Justificación}` → `\section{Justificación}`
   - Si `Objetivos` o `Alcance y Limitaciones` están como `\section*{...}` + `\addcontentsline` manual: quita el asterisco y BORRA la línea `\addcontentsline` (de lo contrario quedarán duplicadas en el TOC).
2. **Jerarquía de la Justificación (salto de nivel 1→3):** dentro de `\section{Justificación}`, convierte los 6 `\subsubsection{...}` en `\subsection{...}`:
   - `Trade-off Explícito entre Tooling y Huella Inicial`
   - `Ejecución de Lógica Compilada en WebAssembly`
   - `Pipeline Integrado de Importación y Renderizado`
   - `Capacidad de Extensión para Visualización Técnica`
   - `Compatibilidad Esperada de la Plataforma Web`
   - `Aporte a la Ingeniería Multimedia`
3. **Títulos run-in de nivel 4** en Planteamiento del Problema: mayúsculas capitalizadas y SIN punto dentro de las llaves (el macro ya añade el punto):
   - `\paragraph{Huella inicial de datos.}` → `\paragraph{Huella Inicial de Datos}`
   - `\paragraph{Rendimiento en tiempo real.}` → `\paragraph{Rendimiento en Tiempo Real}`
   - `\paragraph{Latencia de interacción.}` → `\paragraph{Latencia de Interacción}`
   - `\paragraph{Fragmentación de datos técnicos.}` → `\paragraph{Fragmentación de Datos Técnicos}`
4. Verifica que los Objetivos Específicos NO estén en `itemize`/`enumerate` (p. 20 los prohíbe como viñetas). Si lo están, conviértelos en párrafos normales (en el PDF compilado actual ya aparecen como párrafos; solo verificar).
5. Aplica las sistemáticas de la Tarea 9.

## Tarea 2 — `chapters/02_marco_referencia.tex`

1. **Elimina cualquier `\clearpage` o `\newpage` que preceda a un `\subsection` o `\subsubsection`.** Caso confirmado: el salto antes de "Benchmarking de Soluciones Web 3D" deja medio vacío la página anterior (PDF p. 35-36). Solo los títulos de nivel 1 abren página (p. 8).
2. Captions a mayúsculas capitalizadas y sin punto final (regla general de la Tarea 9). Casos confirmados en este capítulo:
   - Figura 5: `Interoperabilidad futura mediante twin manifest por componente.` → `Interoperabilidad Futura mediante Twin Manifest por Componente`
   - Figura 6: `Capas del prototipo como modelo visual-semántico de producto.` → `Capas del Prototipo como Modelo Visual-Semántico de Producto`
   - Figura 20: `Triangulación de evidencia para interpretar el prototipo.` → `Triangulación de Evidencia para Interpretar el Prototipo`
   - Figura 21: `Taxonomía del sistema: relación entre piezas semánticas, anchors y renderers.` → `Taxonomía del Sistema: Relación entre Piezas Semánticas, Anchors y Renderers`
   - Figuras 22, 23, 24, 26 y 27: misma regla (quitar punto final; capitalizar palabras sustantivas; conectores `de, la, el, en, y, o, con, por, para, como, del` en minúscula salvo que sean la primera palabra).
3. Aplica las sistemáticas de la Tarea 9.

## Tarea 3 — `chapters/03_marco_metodologico.tex`

1. Captions: aplica la regla Title Case + sin punto (revisa Figuras 14-20 y Tablas 2-3).
2. Aplica las sistemáticas de la Tarea 9.

## Tarea 4 — `chapters/04_desarrollo.tex`

1. Captions: regla Title Case + sin punto (Figuras 21-74, Tablas 3-13).
2. Verifica que no haya `\clearpage`/`\newpage` antes de subsecciones.
3. Aplica las sistemáticas de la Tarea 9.

## Tarea 5 — `chapters/05_resultados.tex`

1. Captions: regla Title Case + sin punto (Figuras 72-80, Tablas 14-28).
2. Aplica las sistemáticas de la Tarea 9.

## Tarea 6 — `chapters/06_conclusiones.tex`

1. **Recomendaciones es título de Nivel 1** (p. 25): `\subsection{Recomendaciones}` → `\section{Recomendaciones}`. Abrirá página nueva automáticamente.
2. **Redacción en párrafos** (pp. 24-26: "En este apartado no se ubican viñetas, ni tampoco numeraciones"):
   - `Limitaciones`: hoy son ~11 oraciones aisladas, cada una en su propio párrafo. Reescríbelas en 2-3 párrafos discursivos conectados (mismo contenido, enlazado con conectores; sin viñetas).
   - `Recomendaciones`: hoy son 5 oraciones-párrafo sueltas (PDF p. 186). Reescríbelas en 1-2 párrafos discursivos.
   - Elimina cualquier entorno `itemize`/`enumerate` de estas tres secciones si existe.
3. Aplica las sistemáticas de la Tarea 9.

## Tarea 7 — `chapters/07_referencias.tex`

1. **Reordenamientos confirmados** (orden alfabético letra por letra, p. 31; el caso Babylon.js tras Bangor está verificado en el PDF p. 187):
   - `Babylon.js. (s. f.)` → ANTES de `Bangor, A., Kortum, P. T., & Miller, J. T. (2008)`.
   - `Epic Games. (s. f.-a)` y `(s. f.-b)` → ANTES de `Ericsson` (1993).
   - `Köhler, W. (1929)` → ANTES de `Kritzinger` (2018).
   - `Marmoset. (s. f.)` → ANTES de `Miller, G. A. (1956)`.
   - `Unity Technologies. (s. f.)` → ANTES de `Unity Technologies. (2024a)` (mismo autor: primero las sin fecha, luego orden cronológico; APA 7 §9.46).
2. **Sangría francesa (1,27 cm, p. 31):** primero VERIFICA en el PDF compilado si ya existe. Solo si falta, envuelve la lista así (NO uses `\everypar`):
   ```latex
   \section{Referencias Bibliográficas}
   \begingroup
   \setlength{\parindent}{-1.27cm}
   \setlength{\leftskip}{1.27cm}
   \setlength{\parskip}{0pt}
   % ... entradas ...
   \endgroup
   ```
3. Verifica que las entradas no lleven viñetas, números ni letras (p. 53).

## Tarea 8 — `chapters/08_apendices.tex`

1. **Encabezado de cada apéndice** (p. 55): reemplaza el inicio de cada apéndice por este bloque exacto (ejemplo con A; repite cambiando letra, título y label):
   ```latex
   \newpage
   \phantomsection
   \addcontentsline{toc}{subsection}{Apéndice A: Repositorio de Código Fuente}
   \label{apendice:A}
   {\centering\textbf{Apéndice A}\par
   \textit{Repositorio de Código Fuente}\par}

   % texto del apéndice...
   ```
   - Línea 1: rótulo en NEGRITA, sin punto, centrado.
   - Línea 2: título en CURSIVA, SIN negrita, mayúsculas capitalizadas, centrado.
   - Mantén la sección divisoria `\section{Apéndices}` con su párrafo introductorio tal como está.
2. **Renumeración de tablas/figuras DENTRO de apéndices** (p. 22: "Si se ubican tablas o imágenes en el apéndice estas deben ser nombradas con el rótulo Apéndice A, B, C"). Añade al inicio de cada apéndice que contenga tablas/figuras (después del encabezado):
   ```latex
   \setcounter{table}{0}\renewcommand{\thetable}{H\arabic{table}}
   \setcounter{figure}{0}\renewcommand{\thefigure}{H\arabic{figure}}
   ```
   Mapa de renumeración (número actual → nuevo):
   - Apéndice H: `Tabla 29` → **Tabla H1**; `Figura 81` → **Figura H1**
   - Apéndice I: `Tabla 30` → **Tabla I1**
   - Apéndice M: `Tabla 31` → **Tabla M1**; `Tabla 32` → **Tabla M2**
   - Apéndice N: `Tabla 33` → **Tabla N1**
3. **Actualiza las referencias cruzadas:** busca en TODOS los `.tex` las cadenas `Tabla 29`, `Tabla 30`, `Tabla 31`, `Tabla 32`, `Tabla 33` y `Figura 81`.
   - Si están escritas como texto plano, reemplázalas por el nuevo rótulo (o mejor, conviértelas a `\ref{...}` apuntando al `\label` del caption).
   - Si ya usan `\ref`, no toques nada: se actualizan solas.
   - La Lista de Tablas y la Lista de Figuras se actualizan solas al compilar.
4. **Tabla N1 (antes 33, Apéndice N):** la columna de rutas corta palabras con interletrado roto (`d o c s i n d e x . h t m l`). Envuelve las rutas en `\path{...}` (el paquete `xurl` ya está cargado y permite cortes en cualquier punto) o amplía esa columna a `p{4.2cm}`.
5. Aplica las sistemáticas de la Tarea 9.

## Tarea 9 — Sistemáticas en TODOS los capítulos (01 a 08)

Aplica en orden, con búsqueda global (regex de VS Code o ripgrep):

1. **Flotantes rígidos → flexibles** (causa raíz de los huecos de media página):
   - Buscar: `\\begin\{(figure|table)\}\[H\]` → Reemplazar: `\begin{$1}[htbp]`
2. **Altura máxima de figuras** (para que figura + título + nota compartan página con texto, p. 22):
   - Buscar: `height=0\.[5-9]\d*\\textheight` dentro de `\includegraphics` → reemplazar por `height=0.45\textheight,keepaspectratio` (si ya tiene `keepaspectratio`, solo ajusta el valor). Si una figura usa solo `width`, asegúrate de que no supere `width=0.9\linewidth`.
3. **Doble espacio antes de notas:**
   - Buscar: `\\vspace\{[^}]*\}(\s*\n\s*)\\apanote` → eliminar la línea del `\vspace` (el macro `\apanote` ya trae su propio espacio).
4. **Sangrías manuales:**
   - Buscar: `\\noindent\s` y `\\indent\s` al inicio de párrafos de texto corrido → eliminar el comando. (Excepción: NO tocar los `\noindent` de la Lista de Apéndices en el main ni el que está dentro del macro `\apanote`.)
5. **Saltos de página antes de subtítulos:**
   - Buscar: `(\\clearpage|\\newpage)\s*\n\s*\\(subsection|subsubsection)` → eliminar el salto.
6. **Títulos run-in (nivel 4/5):**
   - Buscar: `\\(paragraph|subparagraph)\{([^}]*)\.\}` → quitar el punto final dentro de las llaves (el macro lo añade).
   - Verificar que el texto del párrafo siga INMEDIATAMENTE después de las llaves, en la misma línea lógica (sin línea en blanco intermedia, o el título quedaría solo en su línea).
   - Capitalización: mayúscula inicial en cada palabra sustantiva; conectores en minúscula.
7. **Captions (figuras y tablas):**
   - Buscar: `\\caption\{([^}]*)\.\}` → quitar el punto final.
   - Revisar uno por uno para dejarlos en mayúsculas capitalizadas (Title Case): mayúscula en la primera palabra y en todas las palabras sustantivas; minúscula en conectores (de, la, el, en, y, o, u, con, por, para, como, del, los, las, un, una) salvo al inicio; respetar nombres propios y siglas (WebGL, Unity, URP, PBR, MAD-T, SUS, NASA-TLX, Holybro X500 V2, Blender, MoI 3D, STEPper, RizomUV, etc.).
8. **Espacios verticales manuales:** buscar `\\vspace` en los capítulos y eliminar todos los que separen párrafos o antecedan a captions/notas (los únicos `\vspace` permitidos viven en el preámbulo y en la portada del main).
9. **`\FloatBarrier` puntual (opcional, solo si hace falta):** tras compilar, si una figura/tabla sigue dejando un hueco grande, escribe `\FloatBarrier` justo antes del título que sigue a la zona afectada. No lo uses de forma preventiva.

---

## Verificación final (obligatoria antes de entregar)

1. Compila **3 veces** seguidas (o `latexmk -pdf`) para estabilizar TOC, listas y `\pageref` de apéndices.
2. Checklist visual sobre el PDF:
   - [ ] Portada muestra el número 1 arriba a la derecha; título en minúsculas y negrita.
   - [ ] TOC empieza con Resumen e incluye: Abstract, Lista de Tablas, Lista de Figuras, Lista de Apéndices, Información General, Introducción, Planteamiento del Problema, Justificación... y NO incluye títulos run-in (Three.js, Carga Intrínseca, Hero, OE1, etc.).
   - [ ] Orden de preliminares: Tabla de Contenido → Lista de Tablas → Lista de Figuras → Lista de Apéndices.
   - [ ] En las listas, no hay hueco exagerado entre "Figura 10"/"Tabla 10" y el título.
   - [ ] Cada título de nivel 1 abre página nueva; ningún título de nivel 2 o 3 abre página (salvo flujo natural).
   - [ ] Títulos run-in: sangría 1.27 cm, negrita, punto final, texto en la misma línea con un espacio normal; sin espacio vertical extra antes.
   - [ ] Ninguna página queda con más de ~40% en blanco antes de una figura/tabla.
   - [ ] Apéndices: rótulo en negrita + título en cursiva sin negrita, ambos centrados; cada apéndice en página nueva; tablas/figuras de apéndices con letra (Tabla H1, Figura H1...).
   - [ ] Recomendaciones como título de nivel 1 en página nueva, redactada en párrafos.
   - [ ] Referencias: orden corregido (Babylon antes de Bangor, etc.) y sangría francesa de 1,27 cm.
3. Greps de control (no deben devolver nada):
   - `\[H\]` en capítulos
   - `\\section\*` en capítulos
   - `\\vspace` seguido de `\\apanote`
   - `\\caption\{[^}]*\.\}`
   - `Tabla (29|30|31|32|33)` y `Figura 81` como texto plano

## Notas y decisiones conscientes

- **Títulos run-in bajo subsecciones (salto de nivel 2→4):** APA pide descender nivel a nivel, y estrictamente un `\paragraph` debería ir bajo un `\subsubsection`. Se DECIDIÓ no reestructurar porque el revisor ya vio esas páginas y solo pidió espacios y capitalización; promover ~50 títulos a nivel 3 cambiaría radicalmente la maquetación y re-saturaría el TOC. Si el asesor lo exige, la alternativa estricta es convertir esos `\paragraph` en `\subsubsection` y bajar `tocdepth` a 2.
- **`es-nodecimaldot`:** no se añade. El documento escribe las comas decimales a mano (91,88; 33,33) y el comportamiento por defecto de `babel` en español es el deseado.
- **Encabezado de página (running head):** el instructivo (p. 7) lo declara OPCIONAL; el documento no lo lleva y se mantiene así.
