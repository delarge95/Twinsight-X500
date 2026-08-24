# Órdenes al agente — Eliminar los solapamientos de las notas (causa: el `\vspace` negativo)

## La causa raíz (confirmada en el main actual)

El main compilado tiene dos desviaciones respecto a la configuración sana:

1. `\apanote` termina con `\vspace{-\intextsep}%` (un salto vertical de **−18.5pt** dentro del flotante, al final de la nota).
2. Los separadores quedaron en `\intextsep=18.5pt` y `\floatsep=18.5pt` (debían ser 0pt).

**Por qué el `\vspace` negativo solapa el último renglón de las notas:** al poner −18.5pt al final del contenido del flotante, la caja del flotante se *declara* 18.5pt más corta de lo que realmente ocupa. El último renglón de la nota queda sobresaliendo por debajo del borde declarado de la caja. El armador de páginas de LaTeX coloca el texto siguiente respecto al borde **declarado**, no al real: cuando el pegamento que sigue al flotante no alcanza a librar los glifos reales de la nota, el primer renglón del texto siguiente sube y se imprime **encima** del último renglón de la nota. Que ocurra o no depende de la colocación del flotante y del pegamento circundante — por eso la Figura 2 se ve bien y la Figura 1 se solapa, y por eso la víctima es siempre el último renglón de la nota. Es un hack geométrico que desacopla lo declarado de lo visible: se elimina por completo, no se ajusta.

## Los 3 cambios (todos en el main, ninguno en los capítulos)

**Reemplaza el archivo `informe_final_definitivo.tex` COMPLETO con el contenido de `informe_final_definitivo_v3.tex` entregado en esta conversación.** No combines a mano. El archivo entregado conserva todo lo ya verificado (listas de corrido, notas a 2.0 con `\setstretch{2}` explícito, run-in con 0.25em, flotantes al tope, TOC corto, Figura H1) y solo cambia esto:

1. **Eliminada** la línea `\vspace{-\intextsep}%` al final de `\apanote`. El macro queda:
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
2. **Separadores en 0pt** (estaban en 18.5pt):
   ```latex
   \setlength{\textfloatsep}{0pt}
   \setlength{\intextsep}{0pt}
   \setlength{\floatsep}{0pt}
   ```
3. **Agregado el bloque `\lineskip`** (el hueco previo al flotante sin tocar el posterior):
   ```latex
   \setlength{\lineskip}{18.5pt}
   \AtBeginEnvironment{tabular}{\setlength{\lineskip}{1pt}}
   \AtBeginEnvironment{longtable}{\setlength{\lineskip}{1pt}}
   ```

**Recordatorio de mecánica para no revertirlo:** cuando TeX apila la caja del flotante (muy alta) tras una línea de texto, el pegamento interlineal calculado da negativo y TeX lo sustituye por `\lineskip`. Con `\lineskip=18.5pt`, ese hueco colapsado queda igual al blanco interlineal normal (18.5pt). El hueco *después* de la nota nunca colapsa (la caja siguiente es una línea de texto normal) y con los separadores en 0pt queda en 18.5pt exactos. `\lineskip` no afecta flotantes al tope de página ni texto normal; los resets en `tabular`/`longtable` protegen las filas de tabla con celdas multilínea (que también colapsan a `\lineskip` y deben seguir a 1pt).

## Verificación (con medición y barrido completo)

1. Compila 3 veces con `pdflatex -interaction=nonstopmode informe_final_definitivo.tex`.
2. **El caso que fallaba:** renderiza la página de la Figura 1 (p. 18) con `pdftoppm` y confirma que el texto ya NO se monta sobre "ción propia." — debe haber un blanco de ~18.5pt entre la nota y el párrafo siguiente.
3. Ejecuta `measure_gaps.py` sobre la página de la Figura 1, la de la Figura 2 (p. 20) y la de la Tabla 1 (p. 35): blanco antes del rótulo ≈ 18.5pt (±1), blanco después de la nota ≈ 18.5pt (±1), pitch del texto ≈ 29pt.
4. **Barrido de solapamientos:** como el defecto aparecía disperso, extrae en imagen TODAS las páginas con figuras o tablas y verifica visualmente que ningún texto se monta sobre el último renglón de una nota. (Con la geometría honesta restaurada, el solapamiento es imposible por construcción; el barrido es la confirmación.)
5. Verifica que las filas de las tablas (ej. Tabla 1) siguen compactas, sin huecos nuevos entre filas.
6. `??` = 0 en el PDF; reporta cualquier `Overfull` del log.

**Reporta al usuario:** la captura de la página de la Figura 1 (la que se solapaba), las mediciones numéricas de las 3 páginas de referencia y confirmación del barrido sin solapamientos.
