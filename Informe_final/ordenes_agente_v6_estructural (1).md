# Órdenes al agente — Diagnóstico estructural final: saturación del colocador de flotantes

## Lee esto primero (por qué las 8 rondas anteriores no funcionaron)

Ninguna ronda anterior estaba mal razonada en aislamiento: `\intextsep`, `\lineskip`, `[tp]` — cada una es una palanca real de TeX. El problema es que **ninguna ataca la causa raíz**, que es estructural: el documento tiene ~114 flotantes en 8 capítulos, con picos de **51 figuras + 11 tablas en un solo capítulo** (el 4). Con esa densidad, el colocador de flotantes de LaTeX se satura — su pool interno de "flotantes pendientes de imprimir" se llena, y a partir de ahí el comportamiento de espaciado deja de ser determinista: depende de cuántos flotantes lleve en cola en cada punto del documento. Por eso el ajuste "funcionaba" en la Figura 2 de prueba y fallaba en la Figura 3, 5 o 7: no es un patrón de código, es una cola de flotantes que se vacía y se llena de forma irregular.

Forzar `[tp]` en la última ronda **empeoró la saturación**, porque le quitó a LaTeX la opción de imprimir un flotante pequeño en el mismo punto del texto y lo obligó a todos a competir por el mismo pool de páginas de flotantes.

**Ningún ajuste adicional de separadores va a resolver esto.** Se necesita una intervención estructural: vaciar la cola de flotantes con frecuencia (`\FloatBarrier`) para que nunca se acumule suficiente backlog como para saturar el colocador, y aumentar el pool permitido por página.

## Tarea 1 — Reemplazar el main COMPLETO

Reemplaza **todo** `informe_final_definitivo.tex` con `informe_final_definitivo_v6.tex` entregado. Cambios clave respecto a v5:

1. **`\usepackage{flafter}`**: impide que un flotante se imprima antes del punto donde se menciona (causa adicional de reordenamiento errático).
2. **`\apptocmd{\subsection}{\FloatBarrier}{}{}`**: inserta automáticamente un `\FloatBarrier` al final de cada `\subsection`. Esto obliga a LaTeX a imprimir TODOS los flotantes pendientes antes de continuar — la cola nunca acumula más de lo que cabe entre dos subsecciones. Es la intervención que realmente ataca la saturación.
3. **`\setcounter{topnumber}{4}`, `\setcounter{bottomnumber}{2}`, `\setcounter{totalnumber}{6}`**: aumentan cuántos flotantes puede colocar LaTeX en una sola página antes de verse forzado a diferir el resto (valores por defecto de la clase `article` son más bajos: 2, 1, 3).
4. Todo lo demás (portada, TOC corto, listas de corrido, `\apanote` limpio, `[tp]` en capítulos ya aplicado en la ronda anterior) se conserva.

**No es necesario revertir el `[tp]` de los capítulos** — con `\FloatBarrier` vaciando la cola cada subsección, `[tp]` deja de ser un problema porque nunca hay más de un puñado de flotantes esperando a la vez.

## Tarea 2 — Compilar y verificar (esta vez el criterio es distinto)

1. Compila 3 veces con `pdflatex -interaction=nonstopmode informe_final_definitivo.tex`.
2. **Revisa el log buscando la advertencia que confirma el diagnóstico:**
   ```
   grep -i "too many unprocessed floats\|float too large" informe_final_definitivo.log
   ```
   Si esta advertencia aparecía en compilaciones anteriores (probablemente sí, y probablemente nadie la reportó porque no se buscó), es la prueba directa de la saturación. Repórtala si aparece en el log ANTERIOR a este cambio (si tienes el log viejo) o confirma que ya NO aparece con v6.
3. Verificación visual — barrido de **todas** las páginas con flotantes (no solo 3-5 de muestra, porque el defecto era intermitente):
   - [ ] El hueco entre la nota y el texto siguiente es visualmente uniforme en TODAS las figuras/tablas, no solo en las que se probaron antes.
   - [ ] Ningún flotante queda "flotando" a mitad de página con huecos grandes arriba o abajo.
   - [ ] Los `\FloatBarrier` automáticos no generaron páginas en blanco nuevas (revisa que ninguna página quede con menos del 50% de contenido de forma injustificada).
4. `??` = 0 en el PDF.

## Tarea 3 (si persiste algo después de esto)

Si tras `\FloatBarrier` + pool ampliado sigue habiendo 1-2 casos puntuales de espaciado raro, ya no es un problema sistémico — son casos aislados de una imagen específica con proporciones inusuales. En ese caso, y SOLO en ese caso, usa `\FloatBarrier` manual inmediatamente después del flotante problemático (no un `\vspace`, positivo ni negativo). No reintroduzcas ajustes de `\intextsep`/`\lineskip`/`\vspace` a nivel global: ya se probó que no son la palanca correcta.

## Reporta al usuario

- Si la advertencia "too many unprocessed floats" aparecía antes y ya no aparece.
- Confirmación de barrido completo (todas las páginas con flotantes, no una muestra).
- Si tras esto persisten casos puntuales, cuáles son (con página y figura) para decidir si vale la pena un `\FloatBarrier` manual o si son aceptables como quedan.
