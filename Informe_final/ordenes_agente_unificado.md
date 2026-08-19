# Órdenes al agente — Corrección del espaciado flotante-texto + restauración Figura H1

**Contexto:** el intento anterior de calibrar los separadores con `0.65\baselineskip` no tuvo el efecto esperado por una trampa de evaluación (ver explicación abajo). Esta ronda se entrega el archivo maestro COMPLETO corregido para reemplazo total, más una restauración de una línea en apéndices.

## Tarea 1 — Reemplazar el main COMPLETO (sin merge manual)

Reemplaza **todo el contenido** de `informe_final_definitivo.tex` con el archivo `informe_final_definitivo_v2.tex` entregado en esta conversación. No edites a mano ni combines: el archivo entregado ya integra todo lo verificado antes (listas de corrido sin caja, notas con `\setstretch{2}` explícito, run-in con 0.25em, flotantes al tope, TOC corto) más la corrección de esta ronda.

**La única diferencia funcional respecto a la versión actual:**

```latex
% Antes (incorrecto):
\setlength{\textfloatsep}{0.65\baselineskip}
\setlength{\intextsep}{0.65\baselineskip}
\setlength{\floatsep}{0.65\baselineskip}
% Después (correcto):
\setlength{\textfloatsep}{0pt}
\setlength{\intextsep}{0pt}
\setlength{\floatsep}{0pt}
```

**Por qué `0.65\baselineskip` dejó todo igual (explicación para no repetirla):** en el preámbulo, el registro `\baselineskip` vale 14.5pt (el valor crudo de la clase a 12pt), NO los ~29pt del doble espacio, porque `\setstretch{2}` actúa a través de `\baselinestretch` al seleccionar la fuente, no sobre ese registro. Por tanto `0.65\baselineskip` = **9.4pt**, no los ~19pt buscados. Y el valor correcto es 0pt de todos modos: el interlineado 2.0 ya separa las líneas; el flotante no debe añadir pegamento propio. Con los tres separadores en 0pt, la distancia de línea base a línea base es exactamente una línea a doble espacio tanto antes del rótulo ("Tabla 1") como después de la nota — uniforme con el ritmo entre párrafos. Regla permanente: **nunca uses múltiplos de `\baselineskip` en el preámbulo; si algún día se quiere "aire" alrededor de un flotante, usa un valor fijo en pt (ej. 6pt).**

## Tarea 2 — Restaurar la Figura H1 en `chapters/08_apendices.tex`

En la ronda anterior se revirtió sin necesidad la numeración de la figura del Apéndice H (quedó como "Figura 81"). El instructivo (p. 22) ordena que tablas e imágenes en apéndices se nombren con el rótulo del apéndice, y las tablas sí quedaron así (Tabla H1, I1, M1, M2, N1): la figura debe ser consistente.

En el bloque del Apéndice H, justo debajo de las líneas `\setcounter{table}{0}\renewcommand{\thetable}{H\arabic{table}}`, vuelve a agregar:

```latex
\setcounter{figure}{0}\renewcommand{\thefigure}{H\arabic{figure}}
```

La referencia en el texto usa `\ref{...}`, así que se actualiza sola a "Figura H1", igual que la entrada en la Lista de Figuras. No hay más figuras en apéndices, así que nada más se ve afectado.

## Tarea 3 — Compilar y verificar con medición

1. Compila `informe_final_definitivo.tex` **3 veces** con `pdflatex -interaction=nonstopmode`.
2. Verificación con `pdftoppm` (extracción de páginas) sobre la página de la Tabla 1 y la página de la Figura 6:
   - [ ] El hueco entre la última línea de texto y el rótulo "Tabla 1"/"Figura 6" es el MISMO que hay entre dos líneas de texto (ritmo 2.0 uniforme). Ni pegado ni con aire extra.
   - [ ] El hueco entre la última línea de la nota y el párrafo siguiente es idéntico al anterior.
   - [ ] La nota sigue a interlineado 2.0 (sus líneas tan separadas como las del texto).
   - [ ] Los títulos run-in siguen con un solo espacio tras el punto.
3. Verifica en la Lista de Figuras que la figura del Apéndice H aparece como **Figura H1** y que las entradas siguen de corrido (rótulo negrilla + título cursiva, un espacio).
4. Búsqueda de `??` en el PDF: 0. Búsqueda de `Overfull` en el `.log`: reporta lo que aparezca.
5. **No toques** `informe_final.tex` (build de respaldo) ni ningún capítulo salvo la línea del Apéndice H.

**Reporta al usuario:** capturas de la página de la Tabla 1 y de la página de la Figura 6 donde se vea el espaciado uniforme antes y después del flotante, y la entrada "Figura H1" en la Lista de Figuras.
