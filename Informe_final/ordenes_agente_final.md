# Órdenes finales para el agente — Build alternativa definitiva (APA 7 UNAD)

**Objetivo:** generar `informe_final_definitivo.pdf` con el TOC corto (inicia en Objetivos) y 5 correcciones de formato pendientes. **NO modificar ni recompilar `informe_final.tex`**: esa build y su PDF quedan intactos como respaldo.

---

## Tarea 1 — Crear el nuevo archivo maestro

Crea `Informe_final/informe_final_definitivo.tex` con el contenido exacto del archivo `informe_final_definitivo.tex` entregado en esta conversación. Cambios que ya trae incorporados (no los reprogramar a mano):

1. **Portada:** eliminado el `\vspace*{2\baselineskip}` inicial → el título inicia en el margen superior (antes empezaba muy abajo).
2. **Notas de tablas/figuras a interlineado 2.0:** el macro `\apanote` ya NO lleva `\setstretch{1.15}`. Regla (p. 8): "todo el trabajo va a doble espacio"; la excepción de "mermar el interlineado" (p. 23) aplica solo al CUERPO de la tabla, no a la nota. Se conserva `\AtBeginEnvironment{table}{\setstretch{1.15}}` para el cuerpo de tablas.
3. **Títulos run-in (Nivel 4/5):** `\titlespacing*{\paragraph}{1.27cm}{0pt}{0pt}` (antes el tercer valor era `0.5em`). El macro ya no añade NADA tras el punto: la separación visible será el único espacio fuente del .tex. Antes era 0.5em + 1 espacio ≈ 9pt (casi un cuadratín), por eso se seguía viendo el hueco (ej. "Densidad de Texel." p. 47).
4. **Flotantes:** `\setlength{\@fptop}{0pt}` (las páginas de flotantes se alinean AL TOPE; por defecto LaTeX las centra verticalmente con pegamento `1fil`, por eso las Figuras 1, 3, 4 y 5 aparecían "flotando" a media página) + `\topfraction=0.9`, `\bottomfraction=0.8`, `\textfraction=0.07`, `\floatpagefraction=0.75` (para que una figura grande quepa al tope de una página de texto en vez de ser exiliada a una página de flotantes, que era lo que empujaba "Benchmarking de Soluciones Web 3D" al tope de la página siguiente).
5. **Listas de Tablas/Figuras:** rótulo en negrilla redonda (`\normalfont\bfseries` en el presnum) + título en cursiva (`\cfttabfont`/`\cftfigfont` = `\normalfont\itshape`), ancho de rótulo 4.0em (cabe "Figura 10" sin hueco excesivo) y `\cftbeforetabskip`/`\cftbeforefigskip` = 0pt (sin espacio extra entre entradas). La Lista de Apéndices usa `\textbf{Apéndice X} \textit{Título}` sin `\vspace` entre entradas.
6. **TOC corto:** Resumen, Abstract, Lista de Tablas, Lista de Figuras, Lista de Apéndices e Información General van con `\section*`/`\subsection*` (fuera del TOC) y se eliminaron los `\addcontentsline` de las listas.

## Tarea 2 — Crear la variante del capítulo 1 (sin tocar el original)

Crea `Informe_final/chapters/01_introduccion_toc_corto.tex` como **copia exacta** de `chapters/01_introduccion.tex` con SOLO estos cambios (añadir el asterisco):

- `\section{Introducción}` → `\section*{Introducción}`
- `\section{Planteamiento del Problema}` → `\section*{Planteamiento del Problema}`
- `\section{Justificación}` → `\section*{Justificación}`
- Las 6 subsecciones de Justificación → con asterisco: `Trade-off Explícito entre Tooling y Huella Inicial`, `Ejecución de Lógica Compilada en WebAssembly`, `Pipeline Integrado de Importación y Renderizado`, `Capacidad de Extensión para Visualización Técnica`, `Compatibilidad Esperada de la Plataforma Web`, `Aporte a la Ingeniería Multimedia`.

**NO** asteriscar: `Objetivos`, `Objetivo General`, `Objetivos Específicos` ni `Alcance y Limitaciones` (deben seguir en el TOC). Verifica con una búsqueda de `\\(section|subsection)\{` en la variante que solo queden esos 4 sin asterisco. No toques `chapters/01_introduccion.tex` (lo usa la build de respaldo).

## Tarea 3 — Reparar la Tabla 1 (columnas montadas) en `chapters/02_marco_referencia.tex`

La columna PBR (`p{1.0cm}`) no cabe "Integrado" (~1.7 cm) y se monta sobre la columna Interactividad ("IntegradTotal"). Reemplaza la especificación del `tabular` de la Tabla 1 por:

```latex
\begin{tabular}{@{}>{\RaggedRight\arraybackslash}p{1.9cm}>{\RaggedRight\arraybackslash}p{1.2cm}>{\RaggedRight\arraybackslash}p{2.2cm}>{\RaggedRight\arraybackslash}p{1.8cm}>{\RaggedRight\arraybackslash}p{2.5cm}>{\RaggedRight\arraybackslash}p{2.5cm}>{\RaggedRight\arraybackslash}p{1.6cm}@{}}
```

(`\RaggedRight` en celdas evita guiones forzados como "Strea-ming"; el ancho total queda en ~16.2 cm dentro del área de texto de 16.5 cm.)

Después, compila y **revisa el `.log` de verdad**: busca `Overfull \hbox`. Si otra tabla muestra desbordamiento de columnas, aplícale el mismo criterio (columnas `\RaggedRight` y anchos que quepan a su palabra más larga). No afirmes "sin advertencias" sin haberlo verificado en el log.

## Tarea 4 — Compilar y verificar

1. Compila `informe_final_definitivo.tex` **3 veces** con `pdflatex -interaction=nonstopmode` (estabiliza TOC, listas y `\pageref`).
2. Checklist obligatorio sobre el PDF generado:
   - [ ] Portada: el título inicia en el margen superior (sin empuje vertical); la página muestra el número 1.
   - [ ] El TOC **inicia en "Objetivos"** y NO contiene: Resumen, Abstract, Lista de Tablas, Lista de Figuras, Lista de Apéndices, Información General, Introducción, Planteamiento del Problema ni Justificación. Sí contiene Objetivos, Alcance y Limitaciones, todos los capítulos, Recomendaciones, Referencias Bibliográficas y los 14 apéndices.
   - [ ] La Lista de Figuras sigue mostrando TODAS las figuras (incluidas Figura 1-3 del capítulo 1) y la Lista de Tablas todas las tablas (incluidas Tabla H1, I1, M1, M2, N1).
   - [ ] En las listas: "Figura 1"/"Tabla 1"/"Apéndice A" en negrilla, título en cursiva, sin hueco grande tras el rótulo y sin espacio extra entre entradas.
   - [ ] Notas de tablas/figuras al mismo interlineado 2.0 que el texto (compara visualmente la nota de la Tabla 1 con el párrafo siguiente).
   - [ ] Títulos run-in ("Densidad de Texel.", "Huella Inicial de Datos.", "Three.js."): el texto sigue tras el punto con UN solo espacio normal. Si alguno aparece pegado ("Texel.La"), el archivo fuente perdió el espacio tras la llave: agrégalo (un espacio, nunca dos).
   - [ ] Figuras 1, 3, 4 y 5 al TOPE de su página (no centradas verticalmente) y, cuando quepan, compartiendo página con texto.
   - [ ] "Benchmarking de Soluciones Web 3D" ya no abre página en falso: debe fluir tras el texto anterior. Si una figura aún cae en página de flotantes, mueve el entorno `figure` en el .tex para que quede inmediatamente DESPUÉS del párrafo que la menciona por primera vez (p. 22 del instructivo).
   - [ ] Tabla 1: "Integrado" y "Total" en columnas separadas, sin montaje; sin `Overfull \hbox` en el log para esa tabla.
   - [ ] Búsqueda de `??` en el PDF: 0 resultados.

## Notas

- La variante `01_introduccion_toc_corto.tex` es una copia: si en el futuro editas contenido de `01_introduccion.tex`, refleja el cambio en la variante (o regenera la variante copiando y asteriscando).
- No cambies el contenido textual de ningún capítulo en esta tarea; solo formato.
- Si el usuario decide quedarse con el TOC corto como versión oficial, la variante pasa a ser el `01_introduccion.tex` canónico y este main pasa a ser `informe_final.tex`.
