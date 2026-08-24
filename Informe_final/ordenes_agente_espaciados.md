# Órdenes al agente — Corrección definitiva de espaciados (4 fixes)

**Contexto:** la build alternativa (`informe_final_definitivo.tex`) ya tiene el TOC corto y los flotantes al tope. Quedan 4 defectos de espaciado confirmados con capturas del PDF compilado. Aplica los 4 fixes sobre el main que REALMENTE compilas y recompila 3 veces. No toques `informe_final.tex` (build de respaldo).

**Paso 0 (obligatorio antes de editar):** abre el archivo `.tex` que realmente compilas y ejecuta una búsqueda de `1.15` y de `\apanote`. Reporta al usuario lo que encuentres ANTES de aplicar los fixes. El PDF actual muestra notas a interlineado ~1.15, pero el archivo que se subió a revisión ya no lo tiene: eso significa que el main compilado y el main subido no son el mismo archivo. Asegúrate de estar editando el correcto.

---

## Fix 1 — Listas de Tablas/Figuras: rótulo y título "de corrido" con UN solo espacio

**Causa raíz:** `tocloft` mete el rótulo ("Figura 1") en una **caja de ancho fijo** (`\cftfignumwidth`). Cualquier ancho fijo deja hueco cuando el rótulo es más corto ("Figura 1" vs "Figura 10"). Por eso el hueco persistió con 5.7em, con 4.5em y con 4.0em: no es cuestión de ajustar el ancho, hay que **eliminar la caja**.

En el preámbulo del main, justo DESPUÉS del bloque `\setlength{\cftfignumwidth}{4.0em}` / `\setlength{\cftbeforefigskip}{0pt}`, agrega:

```latex
% Entradas de lista como texto corrido: "Figura 1 Título" con un solo espacio.
% Se elimina la caja de ancho fijo del rótulo (causa del hueco persistente).
\cftsetindents{figure}{0pt}{0pt}
\cftsetindents{table}{0pt}{0pt}
\makeatletter
\renewcommand{\cftnumberline}[1]{\@cftbsnum #1\@cftasnum\ \@cftasnumb}
\makeatother
\renewcommand{\numberline}[1]{\cftnumberline{#1}}
```

Resultado esperado: `Figura 1 Fragmentación de Información en Hardware Complejo .... 18` — rótulo en negrilla, título en cursiva, un solo espacio entre ellos, y si el título se parte en dos líneas, la segunda línea vuelve al margen izquierdo (como el ejemplo del instructivo). El TOC no se ve afectado (sus entradas no usan `\numberline`).

**Verifica también la Lista de Apéndices del main:** cada entrada debe ser exactamente `\noindent\textbf{Apéndice A} \textit{Repositorio de Código Fuente} \cftdotfill{\cftdotsep} \pageref{apendice:A}\par` — con UN espacio entre el rótulo en negrilla y el título en cursiva, y **sin** `\quad` ni `\vspace` entre entradas.

## Fix 2 — Notas de tablas y figuras a interlineado 2.0 real

**Causa raíz (doble):** (a) el main compilado conserva `\setstretch{1.15}` dentro del macro `\apanote` (merge manual incompleto), y (b) las notas dentro de entornos `table` heredan el hook `\AtBeginEnvironment{table}{\setstretch{1.15}}`. La p. 8 del instructivo manda 2.0 en todo el trabajo; la excepción de la p. 23 aplica solo al CUERPO de la tabla.

Reemplaza el macro `\apanote` por esta versión blindada (el `\setstretch{2}` explícito dentro del grupo la hace inmune a cualquier herencia):

```latex
\newcommand{\apanote}[1]{%
    \par\vspace{0.3\baselineskip}%
    \begingroup%
    \setstretch{2}%
    \RaggedRight%
    \noindent\textit{Nota}. #1\par%
    \endgroup%
}
```

Mantén los hooks `\AtBeginEnvironment{table}{\setstretch{1.15}}` y `{longtable}` (el cuerpo de tablas extensas sí puede ir reducido, p. 23); con el macro blindado, la nota queda a 2.0 aun dentro de una tabla.

**Verificación con regla:** la distancia entre líneas de la nota debe ser IDÉNTICA a la de un párrafo de texto (ej. la nota de la Figura 1 vs. el párrafo que sigue). Si sigue compacta, el archivo editado no es el que se compila: repite el Paso 0.

## Fix 3 — Títulos run-in (Nivel 4/5): restaurar UN espacio tras el punto

**Causa raíz:** `titlesec` en modo `runin` absorbe el espacio fuente que sigue a `\paragraph{...}`; al poner la separación del macro en `0pt`, el texto quedó pegado ("Densidad de Texel.La densidad"). La separación debe venir del macro, no del fuente.

En el preámbulo, cambia las dos líneas:

```latex
% Antes:
\titlespacing*{\paragraph}{1.27cm}{0pt}{0pt}
\titlespacing*{\subparagraph}{1.27cm}{0pt}{0pt}
% Después (0.25em = exactamente un espacio interpalabra de Times 12pt):
\titlespacing*{\paragraph}{1.27cm}{0pt}{0.25em}
\titlespacing*{\subparagraph}{1.27cm}{0pt}{0.25em}
```

No edites los capítulos. Resultado esperado: `Densidad de Texel. La densidad de texel busca...` con un espacio normal, idéntico al de cualquier oración. Si tras el cambio algún título muestra DOS espacios, ese archivo tiene un espacio fuente sobreviviente tras la llave: elimínalo.

## Fix 4 — Eliminar el espacio extra DESPUÉS de la nota (entre el flotante y el texto)

**Causa raíz:** LaTeX inserta `\textfloatsep` (~20pt por defecto) entre un flotante de tope/fondo de página y el texto, y `\intextsep` (~12pt) alrededor de flotantes `[h]` a mitad del texto. Eso se SUMA al interlineado normal y produce el hueco "mayor" entre la nota y el párrafo siguiente.

En el preámbulo, dentro del bloque de flotantes (junto a los `\renewcommand{\topfraction}...` ya existentes), agrega:

```latex
% Sin espacio extra entre flotante y texto: solo el interlineado normal (p. 7)
\setlength{\textfloatsep}{0pt}
\setlength{\intextsep}{0pt}
\setlength{\floatsep}{0pt}
```

Resultado esperado: entre la última línea de la nota y el siguiente párrafo hay exactamente el mismo espacio que entre dos párrafos de texto. Lo mismo entre el texto previo y el rótulo "Figura 1" cuando el flotante va a mitad de página.

---

## Verificación final (obligatoria, sobre el PDF recompilado)

1. Lista de Figuras y Lista de Tablas: entradas de corrido con un espacio; rótulo negrilla, título cursiva; sin hueco tras el rótulo; sin espacio extra entre entradas.
2. Lista de Apéndices: mismo formato de corrido.
3. Nota de la Figura 1 y de la Tabla 1: interlineado idéntico al del texto (2.0).
4. "Huella Inicial de Datos.", "Densidad de Texel.", "Three.js.": un espacio tras el punto, texto en la misma línea.
5. Espacio nota → texto siguiente = espacio entre párrafos normal (ni mayor ni menor).
6. Confirma que la Tabla 1 (cap. 2) ya tiene la especificación de columnas `\RaggedRight` de las órdenes anteriores (sin "IntegradTotal" montado). Si no la tiene, aplícala ahora.
7. Búsqueda de `??` en el PDF: 0 resultados. Búsqueda de `Overfull` en el `.log`: reporta cualquier tabla que aparezca.

**Reporta al usuario:** qué encontraste en el Paso 0 (contenido real del `\apanote` compilado) y la lista de verificación con capturas.
