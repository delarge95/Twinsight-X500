# Órdenes al agente — Configuración exacta de espaciado (reemplazo total, sin editar valores)

## Por qué la inconsistencia "selectiva" (léelo antes de tocar nada)

La build actual tiene los separadores de flotante en **18.5pt** (`\intextsep`, `\textfloatsep`, `\floatsep`). Ese valor se **SUMA** al pegamento interlineal normal (18.5pt) después de cada flotante, produciendo huecos de ~37pt tras las notas (la Figura 2 midió 37.9pt). Y como el efecto visible depende de la colocación del flotante ([h] a mitad de texto vs [t] al tope), el mismo valor erróneo produce huecos distintos en distintas figuras — por eso parece aleatorio. **No se corrige figura por figura: se corrige la configuración global y todas quedan uniformes a la vez.**

La configuración correcta ya existe y es la del archivo entregado. Tu trabajo NO es recalibrar valores: es **no cambiar ningún valor**.

## Tarea 1 — Reemplazo total del main

Reemplaza **todo** el contenido de `informe_final_definitivo.tex` con el archivo `informe_final_definitivo_v4.tex` entregado. No edites a mano. No combines. No "mejores" ningún número.

## Tarea 2 — Verificar los valores ANTES de compilar (obligatorio)

Ejecuta sobre el main recién reemplazado:

```
grep -n "intextsep\|textfloatsep\|floatsep\|lineskip\|vspace" informe_final_definitivo.tex
```

y confirma que estas líneas existen **exactamente así** (si alguna difiere, el reemplazo no se hizo bien — repite la Tarea 1):

```latex
\setlength{\textfloatsep}{0pt}
\setlength{\intextsep}{0pt}
\setlength{\floatsep}{0pt}
\setlength{\lineskip}{18.5pt}
\AtBeginEnvironment{tabular}{\setlength{\lineskip}{1pt}}
\AtBeginEnvironment{longtable}{\setlength{\lineskip}{1pt}}
```

y que el macro `\apanote` **no** contiene ningún `\vspace` (ni positivo ni negativo):

```latex
\newcommand{\apanote}[1]{%
    \par%
    \begingroup%
    \setstretch{2}%
    \RaggedRight%
    \noindent\textit{Nota}. #1\par%
    \endgroup%
}
```

**Qué hace cada pieza (para que no las "corrijas"):** con separadores en 0pt, el hueco después de la nota es solo el pegamento interlineal (18.5pt, exactamente el ritmo 2.0). El hueco *antes* del rótulo lo da `\lineskip=18.5pt`, porque TeX sustituye el interlineado por `\lineskip` cuando apila la caja alta del flotante (colapso); con 18.5pt queda idéntico al blanco normal. Los resets en `tabular`/`longtable` mantienen las filas de tabla compactas. Y `\apanote` sin `\vspace` inicial deja la nota a un pitch exacto (29pt) bajo la imagen — elimina el "espacio demasiado grande hasta que aparece la nota".

## Tarea 3 — Compilar y verificar con medición

1. Compila 3 veces con `pdflatex -interaction=nonstopmode informe_final_definitivo.tex`.
2. Con `pdftoppm` + `measure_gaps.py`, mide las páginas de la **Figura 2**, **Figura 3**, **Figura 4**, **Figura 7** y **Tabla 1** (las que el usuario marcó). Criterios numéricos:
   - [ ] Blanco antes del rótulo ("Figura N"/"Tabla N") ≈ 18.5pt (±1.5pt).
   - [ ] Blanco entre la imagen/tabla y la primera línea de la nota ≈ 20.5pt (±1.5pt) — un pitch exacto.
   - [ ] Blanco después de la última línea de la nota ≈ 18.5pt (±1.5pt).
   - [ ] Pitch del texto normal ≈ 29pt.
   - [ ] Filas de tablas sin huecos nuevos (siguen compactas).
3. Barrido visual de todas las páginas con flotantes: ningún texto montado sobre la última línea de una nota (0 solapamientos).
4. `??` = 0 en el PDF; reporta cualquier `Overfull` del log.

**Reporta al usuario:** la tabla de mediciones de las 5 páginas (antes del rótulo / imagen→nota / después de la nota) y confirmación del barrido. Si alguna medida se sale del rango, repórtala con el número — no la ajustes a ojo ni cambies los valores del preámbulo.
